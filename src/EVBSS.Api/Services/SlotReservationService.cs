using EVBSS.Api.Data;
using EVBSS.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EVBSS.Api.Services;

// Custom Exceptions
public class NoBatteryException : Exception
{
    public NoBatteryException() : base("No full battery available.") {}
}

public class ActiveReservationExistsException : Exception
{
    public ActiveReservationExistsException() : base("Bạn đã có lịch đặt đang hoạt động. Vui lòng hủy hoặc hoàn thành lịch cũ trước khi đặt mới.") {}
}

public class SlotNotAvailableException : Exception
{
    public SlotNotAvailableException(string message) : base(message) {}
}

public class InvalidCheckInTimeException : Exception
{
    public InvalidCheckInTimeException(string message) : base(message) {}
}

/// <summary>
/// Service xử lý logic slot-based reservation
/// </summary>
public class SlotReservationService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<SlotReservationService> _logger;
    
    // Secret key để sign QR Code (nên lưu trong appsettings.json hoặc Azure Key Vault)
    private string QRSecret => _config["QRCode:SecretKey"] ?? "DEFAULT_SECRET_KEY_CHANGE_ME";

    public SlotReservationService(AppDbContext db, IConfiguration config, ILogger<SlotReservationService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách slots available cho một ngày
    /// </summary>
    public async Task<List<SlotAvailabilityDto>> GetAvailableSlotsAsync(
        Guid stationId, 
        DateOnly date,  // UPDATED: Changed from DateTime to DateOnly
        Guid batteryModelId)
    {
        // Lấy tất cả slots trong ngày
        var allSlots = ReservationSlotConfig.GetAllSlotsForDay();
        
        // Đếm số reservations cho mỗi slot
        var reservationCounts = await _db.Reservations
            .Where(r => 
                r.StationId == stationId &&
                r.SlotDate == date &&  // UPDATED: Direct DateOnly comparison, no need for .Date
                r.BatteryModelId == batteryModelId &&
                (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.CheckedIn))
            .GroupBy(r => new { r.SlotStartTime, r.SlotEndTime })
            .Select(g => new
            {
                g.Key.SlotStartTime,
                g.Key.SlotEndTime,
                Count = g.Count()
            })
            .ToListAsync();
        
        var result = new List<SlotAvailabilityDto>();
        
        foreach (var slot in allSlots)
        {
            var reserved = reservationCounts
                .FirstOrDefault(r => r.SlotStartTime == slot.Start && r.SlotEndTime == slot.End)
                ?.Count ?? 0;
            
            result.Add(new SlotAvailabilityDto
            {
                SlotStartTime = slot.Start,
                SlotEndTime = slot.End,
                TotalCapacity = ReservationSlotConfig.DefaultSlotCapacity,
                CurrentReservations = reserved,
                IsAvailable = reserved < ReservationSlotConfig.DefaultSlotCapacity
            });
        }
        
        return result;
    }

    /// <summary>
    /// Tạo reservation mới theo slot
    /// </summary>
    public async Task<Reservation> CreateReservationAsync(
        Guid userId,
        Guid stationId,
        Guid batteryModelId,
        DateOnly slotDate,  // UPDATED: Changed from DateTime to DateOnly
        TimeSpan slotStartTime,
        TimeSpan slotEndTime)
    {
        // Validation 1: User chỉ được có 1 active reservation
        var hasActive = await _db.Reservations
            .AnyAsync(r => 
                r.UserId == userId &&
                (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.CheckedIn));
        
        if (hasActive)
        {
            throw new ActiveReservationExistsException();
        }
        
        // Validation 2: Slot phải trong tương lai (ít nhất 1 giờ trước) - TẠMĐỪNG
        // var slotDateTime = slotDate.ToDateTime(TimeOnly.FromTimeSpan(slotStartTime));
        // if (slotDateTime <= DateTime.UtcNow.AddHours(1))
        // {
        //     throw new SlotNotAvailableException("Vui lòng đặt lịch trước ít nhất 1 giờ.");
        // }
        
        // Validation 3: Không đặt quá xa (max 7 ngày)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);  // UPDATED: Convert current DateTime to DateOnly
        var maxDate = today.AddDays(ReservationSlotConfig.MaxAdvanceBookingDays);
        if (slotDate > maxDate)
        {
            throw new SlotNotAvailableException($"Chỉ có thể đặt lịch trong vòng {ReservationSlotConfig.MaxAdvanceBookingDays} ngày tới.");
        }
        
        // Validation 4: Check slot capacity
        var currentCount = await _db.Reservations
            .CountAsync(r =>
                r.StationId == stationId &&
                r.SlotDate == slotDate &&  // UPDATED: Direct DateOnly comparison
                r.SlotStartTime == slotStartTime &&
                r.SlotEndTime == slotEndTime &&
                r.BatteryModelId == batteryModelId &&
                (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.CheckedIn));
        
        if (currentCount >= ReservationSlotConfig.DefaultSlotCapacity)
        {
            throw new SlotNotAvailableException("Slot này đã đầy. Vui lòng chọn slot khác.");
        }
        
        // Tạo reservation
        var reservation = new Reservation
        {
            UserId = userId,
            StationId = stationId,
            BatteryModelId = batteryModelId,
            BatteryUnitId = null, // Chưa assign pin
            SlotDate = slotDate,  // UPDATED: No need for .Date anymore
            SlotStartTime = slotStartTime,
            SlotEndTime = slotEndTime,
            Status = ReservationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        
        // Generate QR Code
        reservation.QRCode = GenerateQRCode(reservation.Id);
        
        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();
        
        _logger.LogInformation("Created reservation {ReservationId} for user {UserId} at slot {SlotStart}-{SlotEnd}", 
            reservation.Id, userId, slotStartTime, slotEndTime);
        
        return reservation;
    }

    /// <summary>
    /// Lấy thông tin chi tiết reservation theo ID
    /// User chỉ xem được reservation của mình, Staff/Admin xem được tất cả
    /// </summary>
    public async Task<Reservation> GetReservationByIdAsync(Guid reservationId, Guid userId)
    {
        var reservation = await _db.Reservations
            .Include(r => r.Station)
            .Include(r => r.BatteryModel)
            .Include(r => r.BatteryUnit)
            .Include(r => r.User)
            .Include(r => r.VerifiedByStaff)
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null)
            throw new KeyNotFoundException("Không tìm thấy lịch đặt");

        // User chỉ xem được reservation của mình
        if (reservation.UserId != userId)
            throw new UnauthorizedAccessException("Bạn không có quyền xem lịch đặt này");

        return reservation;
    }

    /// <summary>
    /// Lấy danh sách reservations với filter (cho Admin/Staff)
    /// </summary>
    public async Task<List<Reservation>> GetReservationsAsync(
        DateTime? date = null,
        Guid? stationId = null,
        ReservationStatus? status = null,
        Guid? userId = null)
    {
        var query = _db.Reservations
            .Include(r => r.Station)
            .Include(r => r.User)
            .Include(r => r.BatteryModel)
            .Include(r => r.BatteryUnit)
            .Include(r => r.VerifiedByStaff)
            .AsQueryable();

        if (date.HasValue)
        {
            var dateOnly = DateOnly.FromDateTime(date.Value);  // UPDATED: Convert DateTime to DateOnly
            query = query.Where(r => r.SlotDate == dateOnly);
        }

        if (stationId.HasValue)
            query = query.Where(r => r.StationId == stationId.Value);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        if (userId.HasValue)
            query = query.Where(r => r.UserId == userId.Value);

        return await query
            .OrderBy(r => r.SlotDate)
            .ThenBy(r => r.SlotStartTime)
            .ToListAsync();
    }

    /// <summary>
    /// Staff check-in driver bằng QR Code
    /// </summary>
    public async Task<Reservation> CheckInAsync(
        Guid reservationId,
        string qrCodeData,
        Guid staffId)
    {
        // Verify QR Code
        if (!VerifyQRCode(reservationId, qrCodeData))
        {
            throw new InvalidOperationException("QR Code không hợp lệ hoặc đã hết hạn.");
        }
        
        var reservation = await _db.Reservations
            .Include(r => r.BatteryModel)
            .FirstOrDefaultAsync(r => r.Id == reservationId);
        
        if (reservation == null)
        {
            throw new KeyNotFoundException("Không tìm thấy reservation.");
        }
        
        // Validation: Status phải là Pending
        if (reservation.Status != ReservationStatus.Pending)
        {
            throw new InvalidOperationException($"Reservation đã {reservation.Status}. Không thể check-in.");
        }

        // ⭐ TASK 25 & 26: Validate and auto-confirm PayPerSwap payment (LUỒNG 2)
        var now = DateTime.UtcNow;  // Move now declaration here to reuse
        
        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.ReservationId == reservationId && p.Type == PaymentType.PayPerSwap);
        
        if (payment != null)
        {
            // TASK 26: Auto-confirm cash payments (CÁCH 2)
            if (payment.Status == PaymentStatus.Pending)
            {
                if (payment.Method == PaymentMethod.Cash)
                {
                    // Auto-confirm cash during check-in
                    payment.Status = PaymentStatus.Completed;
                    payment.ProcessedByStaffId = staffId;
                    payment.CompletedAt = now;
                    
                    _logger.LogInformation(
                        "Auto-confirmed cash payment {PaymentId} for reservation {ReservationId} during check-in by staff {StaffId}",
                        payment.Id, reservationId, staffId);
                }
                else if (payment.Method == PaymentMethod.VNPay)
                {
                    throw new InvalidOperationException(
                        "Thanh toán VNPay chưa hoàn tất. Vui lòng yêu cầu khách hàng hoàn tất thanh toán trực tuyến trước khi check-in.");
                }
            }
            else if (payment.Status != PaymentStatus.Completed)
            {
                throw new InvalidOperationException(
                    $"Thanh toán chưa hoàn tất (Status: {payment.Status}). Không thể check-in.");
            }
        }
        
        // Validation: Phải trong check-in window
        if (!ReservationSlotConfig.IsWithinCheckInWindow(
            reservation.SlotDate, 
            reservation.SlotStartTime, 
            reservation.SlotEndTime, 
            now))
        {
            var (earliest, latest) = ReservationSlotConfig.GetCheckInWindow(
                reservation.SlotDate, 
                reservation.SlotStartTime, 
                reservation.SlotEndTime);
            
            throw new InvalidCheckInTimeException(
                $"Check-in chỉ được phép từ {earliest:HH:mm} đến {latest:HH:mm}. Hiện tại: {now:HH:mm}");
        }
        
        // Assign battery
        var battery = await _db.BatteryUnits
            .Where(b => 
                b.StationId == reservation.StationId &&
                b.BatteryModelId == reservation.BatteryModelId &&
                b.Status == BatteryStatus.Full &&
                !b.IsReserved)
            .OrderBy(b => b.UpdatedAt)
            .FirstOrDefaultAsync();
        
        if (battery == null)
        {
            throw new NoBatteryException();
        }
        
        // Update reservation
        reservation.Status = ReservationStatus.CheckedIn;
        reservation.CheckedInAt = now;
        reservation.VerifiedByStaffId = staffId;
        reservation.BatteryUnitId = battery.Id;
        
        // Mark battery as reserved
        battery.IsReserved = true;
        battery.UpdatedAt = now;
        
        await _db.SaveChangesAsync();
        
        _logger.LogInformation("Checked in reservation {ReservationId}, assigned battery {BatteryId}", 
            reservationId, battery.Id);
        
        return reservation;
    }

    /// <summary>
    /// User/Staff hủy reservation
    /// </summary>
    public async Task CancelReservationAsync(
        Guid reservationId,
        Guid userId,
        CancelReason reason,
        string? note = null,
        bool isStaff = false)
    {
        var reservation = await _db.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId);
        
        if (reservation == null)
        {
            throw new KeyNotFoundException("Không tìm thấy reservation.");
        }
        
        // Validation: Chỉ owner hoặc staff mới được hủy
        if (!isStaff && reservation.UserId != userId)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền hủy reservation này.");
        }
        
        // Validation: Chỉ hủy được nếu status = Pending
        if (reservation.Status != ReservationStatus.Pending)
        {
            throw new InvalidOperationException($"Không thể hủy reservation có status {reservation.Status}.");
        }
        
        // Update status
        reservation.Status = ReservationStatus.Cancelled;
        reservation.CancelReason = reason;
        reservation.CancelNote = note;
        reservation.CancelledAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync();
        
        _logger.LogInformation("Cancelled reservation {ReservationId}, reason: {Reason}", 
            reservationId, reason);
    }

    /// <summary>
    /// Auto-expire reservations quá hạn (background job gọi)
    /// </summary>
    public async Task<int> ExpireOverdueReservationsAsync()
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);  // UPDATED: Convert to DateOnly
        var currentTime = now.TimeOfDay;
        
        // Tìm reservations đã quá check-in window
        // Note: Fetch pending reservations and filter in-memory to avoid LINQ translation issues
        var allPendingReservations = await _db.Reservations
            .Where(r => r.Status == ReservationStatus.Pending)
            .ToListAsync();
        
        var overdueReservations = allPendingReservations
            .Where(r => 
                // Slot của ngày hôm qua - UPDATED: Direct DateOnly comparison
                r.SlotDate < today ||
                // Slot hôm nay nhưng đã quá window
                (r.SlotDate == today && 
                 currentTime > r.SlotEndTime.Add(ReservationSlotConfig.CheckInBuffer)))
            .ToList();
        
        foreach (var reservation in overdueReservations)
        {
            reservation.Status = ReservationStatus.Expired;
            reservation.CancelReason = Models.CancelReason.NoShow;
            reservation.CancelledAt = now;
        }
        
        if (overdueReservations.Any())
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("Expired {Count} overdue reservations", overdueReservations.Count);
        }
        
        return overdueReservations.Count;
    }

    /// <summary>
    /// Generate QR Code cho reservation
    /// </summary>
    private string GenerateQRCode(Guid reservationId)
    {
        var payload = new
        {
            rid = reservationId.ToString(),
            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        
        var json = JsonSerializer.Serialize(payload);
        var signature = ComputeHMAC(json);
        
        var combined = $"{json}|{signature}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(combined));
    }

    /// <summary>
    /// Verify QR Code
    /// </summary>
    private bool VerifyQRCode(Guid reservationId, string qrCodeData)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(qrCodeData));
            var parts = decoded.Split('|');
            
            if (parts.Length != 2) return false;
            
            var json = parts[0];
            var signature = parts[1];
            
            // Verify signature
            var computedSignature = ComputeHMAC(json);
            if (signature != computedSignature) return false;
            
            // Verify reservationId
            var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (payload == null) return false;
            
            var rid = payload["rid"].GetString();
            if (rid != reservationId.ToString()) return false;
            
            // Verify timestamp (QR valid trong 24h)
            var ts = payload["ts"].GetInt64();
            var qrTime = DateTimeOffset.FromUnixTimeSeconds(ts);
            var age = DateTimeOffset.UtcNow - qrTime;
            
            if (age.TotalHours > 24) return false;
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string ComputeHMAC(string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(QRSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }
}

/// <summary>
/// DTO cho slot availability
/// </summary>
public class SlotAvailabilityDto
{
    public TimeSpan SlotStartTime { get; set; }
    public TimeSpan SlotEndTime { get; set; }
    public int TotalCapacity { get; set; }
    public int CurrentReservations { get; set; }
    public bool IsAvailable { get; set; }
}
