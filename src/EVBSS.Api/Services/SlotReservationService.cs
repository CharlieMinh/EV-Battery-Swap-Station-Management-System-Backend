using EVBSS.Api.Configuration;
using EVBSS.Api.Data;
using EVBSS.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace EVBSS.Api.Services;

/// <summary>
/// Service xử lý logic slot-based reservation
/// </summary>
public class SlotReservationService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<SlotReservationService> _logger;
    private readonly VnPayConfig _vnPayConfig;
    private readonly IServiceProvider? _serviceProvider;

    // Secret key để sign QR Code (nên lưu trong appsettings.json hoặc Azure Key Vault)
    private string QRSecret => _config["QRCode:SecretKey"] ?? "DEFAULT_SECRET_KEY_CHANGE_ME";

    public SlotReservationService(
        AppDbContext db,
        IConfiguration config,
        ILogger<SlotReservationService> logger,
        IOptions<VnPayConfig> vnPayConfig,
        IServiceProvider? serviceProvider = null)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _vnPayConfig = vnPayConfig.Value;
        _serviceProvider = serviceProvider;
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
    /// Lấy danh sách slots available cho một ngày cho việc đặt lịch kiểm tra pin (từ khiếu nại)
    /// </summary>
    public async Task<List<SlotAvailabilityDto>> GetAvailableInspectionSlotsAsync(
        Guid stationId,
        DateOnly date,
        Guid complaintId)
    {
        // 1. Resolve Complaint -> Vehicle -> BatteryModelId
        var complaint = await _db.BatteryComplaints
            .Include(c => c.IssuedBattery)
            .Include(c => c.SwapTransaction!)
                .ThenInclude(t => t.Vehicle)
            .FirstOrDefaultAsync(c => c.Id == complaintId);

        if (complaint == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy khiếu nại (ID: {complaintId}).");
        }

        // Xác định loại pin cần kiểm tra/đổi lại.
        // Dựa trên luồng, pin mới cần tương thích với xe của người dùng
        Guid batteryModelId;
        if (complaint.SwapTransaction != null && complaint.SwapTransaction.Vehicle != null)
        {
            // Lấy BatteryModel tương thích với xe của giao dịch đổi pin liên quan
            batteryModelId = complaint.SwapTransaction.Vehicle.CompatibleBatteryModelId;

            _logger.LogInformation("Resolved BatteryModelId {BatteryModelId} from SwapTransaction.Vehicle for Complaint {ComplaintId}",
                batteryModelId, complaintId);
        }
        else if (complaint.IssuedBatteryId != Guid.Empty && complaint.IssuedBattery != null)
        {
            // Fallback: Lấy BatteryModel từ pin bị lỗi
            batteryModelId = complaint.IssuedBattery.BatteryModelId;

            _logger.LogInformation("Resolved BatteryModelId {BatteryModelId} from IssuedBattery for Complaint {ComplaintId}",
                batteryModelId, complaintId);
        }
        else
        {
            // Xử lý kịch bản không có thông tin pin/xe liên quan
            throw new InvalidOperationException("Không thể xác định loại pin cần kiểm tra. Khiếu nại không có thông tin xe hoặc pin liên quan.");
        }

        // 2. Tái sử dụng logic lấy slots có sẵn
        var slots = await GetAvailableSlotsAsync(stationId, date, batteryModelId);

        _logger.LogInformation("Found {Count} inspection slots for Complaint {ComplaintId} (BatteryModel {BatteryModelId}) at Station {StationId} on {Date}",
            slots.Count, complaintId, batteryModelId, stationId, date);

        return slots;
    }

    /// <summary>
    /// ⭐ LUỒNG 3 & 4: Tạo reservation mới theo slot
    /// - Nếu có subscription → MIỄN PHÍ (paymentRequired = false)
    /// - Nếu không có subscription → PAY-PER-SWAP (paymentRequired = true, tạo Payment)
    /// </summary>
    public async Task<Reservation> CreateReservationAsync(
        Guid userId,
        Guid stationId,
        // NOTE: vehicleId is used to resolve BatteryModelId server-side
        Guid vehicleId,
        DateOnly slotDate,  // UPDATED: Changed from DateTime to DateOnly
        TimeSpan slotStartTime,
        TimeSpan slotEndTime,
        PaymentMethod? paymentMethod = null,
        // ⭐ ĐÃ THÊM: Cho phép truyền ComplaintId để đặt lịch MIỄN PHÍ
        Guid? relatedComplaintId = null)  // ⭐ NEW: If provided, skip payment/subscription checks
    {
        // Validation 1: User chỉ được có 1 active reservation
        var hasActive = await _db.Reservations
            .AnyAsync(r =>
                r.UserId == userId &&
                (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.CheckedIn));

        if (hasActive)
        {
            throw new ActiveReservationExistsException("Bạn đã có lịch đặt đang hoạt động. Vui lòng hủy hoặc hoàn thành lịch cũ trước khi đặt mới.");
        }

        // Resolve vehicle -> batteryModel (needed for selecting correct subscription)
        var vehicle = await _db.Vehicles
            .Include(v => v.VehicleModel)
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.UserId == userId);

        if (vehicle == null)
        {
            throw new ArgumentException($"Không tìm thấy xe (ID: {vehicleId}) cho người dùng này.");
        }

        var batteryModelId = vehicle.CompatibleBatteryModelId;

        // ⭐ BỔ SUNG LOGIC: Nếu có RelatedComplaintId, đây là lịch kiểm tra MIỄN PHÍ, bỏ qua kiểm tra thanh toán
        bool paymentRequired = false;
        Guid? userSubscriptionId = null;
        if (relatedComplaintId.HasValue)
        {
            _logger.LogInformation("Booking Complaint Inspection Reservation {ComplaintId}. Skipping Subscription/Payment checks.", relatedComplaintId.Value);
            paymentRequired = false;
            userSubscriptionId = null;
            // skip subscription/payment checks by jumping to slot validation
            goto SkipPaymentCheck;
        }

        // ⭐ CHỈ kiểm tra subscription khi user MUỐN dùng subscription (paymentMethod == null)
        // ⭐ Nếu user chọn Cash/VNPay thì KHÔNG dùng subscription (ngay cả khi có)
        UserSubscription? activeSubscription = null;

        _logger.LogInformation(
            "CreateReservation: UserId={UserId}, VehicleId={VehicleId}, BatteryModelId={BatteryModelId}, PaymentMethod={PaymentMethod}",
            userId, vehicleId, batteryModelId, paymentMethod?.ToString() ?? "NULL (use subscription)");

        if (paymentMethod == null)
        {
            // User muốn dùng subscription → Kiểm tra xem có subscription phù hợp không
            activeSubscription = await _db.UserSubscriptions
                .Include(s => s.SubscriptionPlan)
                .FirstOrDefaultAsync(s =>
                    s.UserId == userId &&
                    s.IsActive &&
                    s.CurrentBillingPeriodEnd >= DateTime.UtcNow &&
                    s.SubscriptionPlan.BatteryModelId == batteryModelId);

            _logger.LogInformation(
                "User requested subscription booking. Found subscription: {Found}",
                activeSubscription != null ? $"Yes (ID: {activeSubscription.Id})" : "No");
        }
        else
        {
            _logger.LogInformation(
                "User requested pay-per-swap booking with {PaymentMethod}. Skipping subscription lookup.",
                paymentMethod);
        }

        if (activeSubscription != null)
        {
            // ✅ LUỒNG SUBSCRIPTION: User có gói phù hợp model pin → Đặt lịch miễn phí
            if (activeSubscription.SubscriptionPlan.MaxSwapsPerMonth.HasValue)
            {
                if (activeSubscription.CurrentMonthSwapCount >= activeSubscription.SubscriptionPlan.MaxSwapsPerMonth.Value)
                {
                    throw new NoActiveSubscriptionException("Bạn đã hết lượt đổi pin trong tháng này của gói. Vui lòng chọn phương thức thanh toán (Cash/VNPay) để đặt lịch theo lượt.");
                }
            }

            var swapsRemaining = activeSubscription.SubscriptionPlan.MaxSwapsPerMonth.HasValue
                ? activeSubscription.SubscriptionPlan.MaxSwapsPerMonth.Value - activeSubscription.CurrentMonthSwapCount
                : int.MaxValue;

            // ⭐ TRỪ QUOTA NGAY KHI ĐẶT LỊCH (Logic mới: "Immediate Deduction")
            activeSubscription.CurrentMonthSwapCount++;

            var newSwapsRemaining = activeSubscription.SubscriptionPlan.MaxSwapsPerMonth.HasValue
                ? activeSubscription.SubscriptionPlan.MaxSwapsPerMonth.Value - activeSubscription.CurrentMonthSwapCount
                : int.MaxValue;

            _logger.LogInformation(
                "User {UserId} booked with subscription {SubscriptionId} ({PlanName}) for BatteryModel {BatteryModelId}. " +
                "Quota deducted immediately: {CurrentCount}/{MaxCount}. Remaining swaps: {Remaining}",
                userId, activeSubscription.Id, activeSubscription.SubscriptionPlan.Name, batteryModelId,
                activeSubscription.CurrentMonthSwapCount,
                activeSubscription.SubscriptionPlan.MaxSwapsPerMonth ?? 999,
                newSwapsRemaining == int.MaxValue ? "unlimited" : newSwapsRemaining);

            paymentRequired = false;
            userSubscriptionId = activeSubscription.Id;
        }
        else
        {
            // ✅ LUỒNG PAY-PER-SWAP: User không có gói phù hợp → Phải chọn phương thức thanh toán
            if (paymentMethod == null)
            {
                throw new NoActiveSubscriptionException("Bạn không có gói subscription hoạt động phù hợp với xe này. Vui lòng chọn phương thức thanh toán (Cash/VNPay) để đặt lịch theo lượt.");
            }

            // ⭐ CHECK NOSHOWCOUNT: Nếu thanh toán bằng Cash và user vi phạm >= 3 lần → Chặn
            if (paymentMethod == PaymentMethod.Cash)
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user != null && user.NoShowCount >= 3)
                {
                    _logger.LogWarning("User {UserId} is BLOCKED from cash payment due to NoShowCount={NoShowCount} >= 3",
                        userId, user.NoShowCount);
                    throw new InvalidOperationException(
                        "Bạn đã bị chặn thanh toán bằng tiền mặt do vi phạm 3 lần hủy muộn hoặc không tới. " +
                        "Vui lòng liên hệ quản trị viên để được mở khóa.");
                }
            }

            _logger.LogInformation("User {UserId} booking pay-per-swap with payment method {PaymentMethod} (BatteryModel {BatteryModelId})",
                userId, paymentMethod, batteryModelId);

            paymentRequired = true;
            userSubscriptionId = null;  // Không dùng subscription
        }

    SkipPaymentCheck: // ⭐ ĐIỂM NHẢY TỪ LOGIC KHIẾU NẠI

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var maxDate = today.AddDays(ReservationSlotConfig.MaxAdvanceBookingDays);
        if (slotDate > maxDate)
        {
            throw new SlotNotAvailableException($"Chỉ có thể đặt lịch trong vòng {ReservationSlotConfig.MaxAdvanceBookingDays} ngày tới.");
        }

        var currentCount = await _db.Reservations
            .CountAsync(r =>
                r.StationId == stationId &&
                r.SlotDate == slotDate &&
                r.SlotStartTime == slotStartTime &&
                r.SlotEndTime == slotEndTime &&
                r.BatteryModelId == batteryModelId &&
                (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.CheckedIn));

        if (currentCount >= ReservationSlotConfig.DefaultSlotCapacity)
        {
            throw new SlotNotAvailableException("Slot này đã đầy. Vui lòng chọn slot khác.");
        }

        // ⭐ Query BatteryModel để lấy SwapPricePerSession cho pay-per-swap
        var batteryModel = await _db.BatteryModels
            .FirstOrDefaultAsync(bm => bm.Id == batteryModelId);

        if (batteryModel == null)
        {
            throw new ArgumentException("Loại pin không tồn tại.");
        }

        var reservation = new Reservation
        {
            UserId = userId,
            StationId = stationId,
            BatteryModelId = batteryModelId,
            VehicleId = vehicleId,
            BatteryUnitId = null,
            UserSubscriptionId = userSubscriptionId,  // ⭐ Set subscription ID (null if pay-per-swap)
            // ⭐ SỬA: Đã thêm RelatedComplaintId vào Model Reservation trước đó
            RelatedComplaintId = relatedComplaintId,
            SlotDate = slotDate,
            SlotStartTime = slotStartTime,
            SlotEndTime = slotEndTime,
            Status = ReservationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        reservation.QRCode = GenerateQRCode(reservation.Id);

        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();  // Save to get reservation.Id for Payment

        // ⭐ SỬ DỤNG 'paymentRequired' ĐÃ ĐƯỢC THIẾT LẬP (Hoặc là true/false từ logic Subscription, hoặc là false khi có relatedComplaintId)
        if (paymentRequired && paymentMethod.HasValue)
        {
            var batteryModelForPayment = await _db.BatteryModels
                 .FirstOrDefaultAsync(bm => bm.Id == batteryModelId)
                 ?? throw new InvalidOperationException("Không tìm thấy thông tin giá pin để tạo Payment.");

            var payment = new Payment
            {
                UserId = userId,
                ReservationId = reservation.Id,
                Method = paymentMethod.Value,
                Type = PaymentType.PayPerSwap,
                Amount = batteryModelForPayment.SwapPricePerSession,
                Status = PaymentStatus.Pending,
                VnpTxnRef = GenerateTransactionReference(),
                PaymentReference = GenerateTransactionReference(),
                Description = $"Đổi pin {batteryModelForPayment.Name} - Slot {slotStartTime:hh\\:mm}-{slotEndTime:hh\\:mm}",
                CreatedAt = DateTime.UtcNow
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            _logger.LogInformation("✅ Created pay-per-swap reservation {ReservationId} with Payment {PaymentId} ({Method}, {Amount} VND) for {BatteryModel}",
                reservation.Id, payment.Id, paymentMethod.Value, payment.Amount, batteryModelForPayment.Name);
        }
        else if (relatedComplaintId.HasValue)
        {
            _logger.LogInformation("✅ Created Complaint Inspection Reservation {ReservationId} for user {UserId} (no payment required)",
                reservation.Id, userId);
        }
        else
        {
            _logger.LogInformation("✅ Created subscription-based reservation {ReservationId} for user {UserId} (no payment required)",
                reservation.Id, userId);
        }

        return reservation;
    }

    /// <summary>
    /// Lấy thông tin chi tiết reservation theo ID
    /// </summary>
    public async Task<Reservation> GetReservationByIdAsync(Guid reservationId, Guid userId, bool isStaffOrAdmin = false)
    {
        var reservation = await _db.Reservations
            .Include(r => r.Station)
            .Include(r => r.BatteryModel)
            .Include(r => r.BatteryUnit)
            .Include(r => r.User)
            .Include(r => r.VerifiedByStaff)
            .Include(r => r.Vehicle)                    // ⭐ NEW: Include vehicle info
                .ThenInclude(v => v.VehicleModel)       // ⭐ NEW: Include vehicle model for name
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null)
            throw new KeyNotFoundException("Không tìm thấy lịch đặt");

        // Staff and Admin can view all reservations, regular users can only view their own
        if (!isStaffOrAdmin && reservation.UserId != userId)
            throw new UnauthorizedAccessException("Bạn không có quyền xem lịch đặt này");

        return reservation;
    }

    /// <summary>
    /// Lấy danh sách reservations với filter
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
            .Include(r => r.Vehicle)                    // ⭐ NEW: Include vehicle info
                .ThenInclude(v => v!.VehicleModel)      // ⭐ NEW: Include vehicle model for name
            .AsQueryable();

        if (date.HasValue)
        {
            var dateOnly = DateOnly.FromDateTime(date.Value);
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
    /// Staff check-in driver bằng QR Code (LUỒNG 4)
    /// </summary>
    public async Task<Reservation> CheckInAsync(
        Guid reservationId,
        string qrCodeData,
        Guid staffId)
    {
        if (!VerifyQRCode(reservationId, qrCodeData))
        {
            throw new InvalidOperationException("QR Code không hợp lệ hoặc đã hết hạn.");
        }

        var reservation = await _db.Reservations
            .Include(r => r.BatteryModel)
            .Include(r => r.Payment) // Eager load the associated payment
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null)
        {
            throw new KeyNotFoundException("Không tìm thấy reservation.");
        }

        if (reservation.Status != ReservationStatus.Pending)
        {
            throw new InvalidOperationException($"Reservation đã ở trạng thái {reservation.Status}. Không thể check-in.");
        }

        var now = DateTime.UtcNow;

        // LUỒNG 4: Phân tích kịch bản thanh toán
        // Kịch bản 3 (Dùng gói): reservation.PaymentId == null -> Bỏ qua kiểm tra
        if (reservation.Payment != null)
        {
            // Kịch bản 2 (Trả lẻ): reservation.PaymentId != null
            var payment = reservation.Payment;
            if (payment.Status == PaymentStatus.Pending)
            {
                if (payment.Method == PaymentMethod.Cash)
                {
                    // Yêu cầu FE xử lý thu tiền mặt
                    throw new PaymentPendingCashException(
                        $"Cần thu {payment.Amount:N0} VND tiền mặt.",
                        payment.Id,
                        payment.Amount
                    );
                }
                else if (payment.Method == PaymentMethod.VNPay)
                {
                    // Lỗi: Thanh toán VNPay chưa hoàn tất
                    throw new InvalidOperationException("Thanh toán VNPay chưa hoàn tất. Vui lòng yêu cầu khách hàng hoàn tất thanh toán trên ứng dụng.");
                }
            }
            else if (payment.Status != PaymentStatus.Completed)
            {
                // Lỗi chung cho các trạng thái khác (Failed, Cancelled, etc.)
                throw new InvalidOperationException($"Thanh toán đang ở trạng thái không hợp lệ: {payment.Status}.");
            }
            // Nếu payment.Status == Completed, không làm gì cả, tiếp tục quy trình
        }

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

        // Find the best available battery (longest time in 'Full' status)
        var battery = await _db.BatteryUnits
            .Where(b =>
                b.StationId == reservation.StationId &&
                b.BatteryModelId == reservation.BatteryModelId &&
                b.Status == BatteryStatus.Full) // We only care if the battery is 'Full'
            .OrderBy(b => b.UpdatedAt)
            .FirstOrDefaultAsync();

        if (battery == null)
        {
            throw new NoBatteryException("Không có pin phù hợp hoặc pin đã được sạc đầy tại trạm.");
        }

        // Atomically update reservation and battery status and optionally transition complaint
        using var tx = await _db.Database.BeginTransactionAsync();

        reservation.Status = ReservationStatus.CheckedIn;
        reservation.CheckedInAt = now;
        reservation.VerifiedByStaffId = staffId;
        reservation.BatteryUnitId = battery.Id;

        // Set battery status to 'Reserved' to prevent other transactions from picking it.
        battery.Status = BatteryStatus.Reserved;
        battery.UpdatedAt = now;

        // If this reservation is linked to a complaint, try to transition the complaint to CheckedIn
        if (reservation.RelatedComplaintId.HasValue && _serviceProvider != null)
        {
            try
            {
                var complaintService = _serviceProvider.GetService(typeof(BatteryComplaintService)) as BatteryComplaintService;
                if (complaintService != null)
                {
                    await complaintService.TransitionToCheckedInAsync(reservation.RelatedComplaintId.Value, staffId);
                }
            }
            catch (Exception ex)
            {
                // Log the error and rethrow to ensure callers are aware of partial failures
                _logger.LogError(ex, "Failed to transition Complaint status during reservation check-in (Reservation: {ReservationId})", reservationId);
                throw new InvalidOperationException("Check-in thành công nhưng không thể cập nhật trạng thái khiếu nại liên quan.", ex);
            }
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        _logger.LogInformation("Checked in reservation {ReservationId}, assigned battery {BatteryId}", reservationId, battery.Id);

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
            .Include(r => r.User)
            .Include(r => r.Payment)
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null)
        {
            throw new KeyNotFoundException("Không tìm thấy reservation.");
        }

        if (!isStaff && reservation.UserId != userId)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền hủy reservation này.");
        }

        if (reservation.Status != ReservationStatus.Pending)
        {
            throw new InvalidOperationException($"Không thể hủy reservation có status {reservation.Status}.");
        }

        // ====== TIME-BASED PENALTY LOGIC ======
        // ⭐ FIX: Assume SlotDate/SlotStartTime are in Vietnam time (UTC+7), convert to UTC for comparison
        var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); // UTC+7
        var slotDateTimeLocal = reservation.SlotDate.ToDateTime(TimeOnly.MinValue).Add(reservation.SlotStartTime);
        var slotDateTimeUtc = TimeZoneInfo.ConvertTimeToUtc(slotDateTimeLocal, vietnamTimeZone);

        var now = DateTime.UtcNow;
        var timeUntilSlot = slotDateTimeUtc - now;
        bool isLateCancellation = timeUntilSlot <= TimeSpan.FromHours(1);

        // ⭐ DEBUG LOG - Xem chi tiết thời gian
        _logger.LogWarning(
            "🔍 CANCEL DEBUG: ReservationId={ReservationId}, " +
            "NowUTC={NowUTC}, NowVN={NowVN}, " +
            "SlotDateTimeVN={SlotVN}, SlotDateTimeUTC={SlotUTC}, " +
            "TimeUntilSlot={TimeUntilSlotHours}h ({TimeUntilSlotMinutes}min), " +
            "IsLateCancellation={IsLate}, IsStaff={IsStaff}",
            reservationId,
            now.ToString("yyyy-MM-dd HH:mm:ss"),
            TimeZoneInfo.ConvertTimeFromUtc(now, vietnamTimeZone).ToString("yyyy-MM-dd HH:mm:ss"),
            slotDateTimeLocal.ToString("yyyy-MM-dd HH:mm:ss"),
            slotDateTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"),
            timeUntilSlot.TotalHours,
            timeUntilSlot.TotalMinutes,
            isLateCancellation,
            isStaff);

        // Update reservation status
        reservation.Status = ReservationStatus.Cancelled;
        reservation.CancelReason = reason;
        reservation.CancelNote = note;
        reservation.CancelledAt = now;

        // ====== HANDLE PAYMENT CANCELLATION (if cash payment) ======
        if (reservation.Payment != null)
        {
            reservation.Payment.Status = PaymentStatus.Cancelled;
            _logger.LogInformation("Cancelled payment {PaymentId} for reservation {ReservationId}",
                reservation.Payment.Id, reservationId);
        }

        // ====== QUOTA REFUND LOGIC FOR SUBSCRIPTION (Logic mới: "Balanced Refund") ======
        if (reservation.UserSubscriptionId.HasValue)
        {
            var subscription = await _db.UserSubscriptions
                .Include(s => s.SubscriptionPlan)
                .FirstOrDefaultAsync(s => s.Id == reservation.UserSubscriptionId.Value);

            if (subscription != null)
            {
                // Staff hủy HOẶC User hủy sớm → HOÀN quota
                if (isStaff || !isLateCancellation)
                {
                    subscription.CurrentMonthSwapCount--;

                    _logger.LogInformation(
                        "Refunded quota for subscription {SubscriptionId} ({PlanName}). " +
                        "Reason: {Reason}. New count: {CurrentCount}/{MaxCount}",
                        subscription.Id,
                        subscription.SubscriptionPlan.Name,
                        isStaff ? "Staff cancelled (always refund)" : "Early cancellation (>1h before slot)",
                        subscription.CurrentMonthSwapCount,
                        subscription.SubscriptionPlan.MaxSwapsPerMonth ?? 999);
                }
                else
                {
                    // User hủy muộn → KHÔNG hoàn quota (user mất 1 lượt)
                    _logger.LogWarning(
                        "User {UserId} late-cancelled subscription reservation {ReservationId}. " +
                        "Quota NOT refunded (user lost 1 swap). Current count: {CurrentCount}/{MaxCount}",
                        userId,
                        reservationId,
                        subscription.CurrentMonthSwapCount,
                        subscription.SubscriptionPlan.MaxSwapsPerMonth ?? 999);
                }
            }
        }

        // ====== PENALTY LOGIC (only for USER late cancellation) ======
        if (!isStaff && isLateCancellation)
        {
            reservation.User.NoShowCount++;
            _logger.LogWarning("User {UserId} late-cancelled reservation {ReservationId}. NoShowCount incremented to {NoShowCount}",
                userId, reservationId, reservation.User.NoShowCount);
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Cancelled reservation {ReservationId} by {CancelledBy}, reason: {Reason}, isLateCancellation: {IsLate}",
            reservationId, isStaff ? "Staff" : "User", reason, isLateCancellation);
    }

    /// <summary>
    /// Auto-expire reservations quá hạn (background job gọi)
    /// </summary>
    public async Task<int> ExpireOverdueReservationsAsync()
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var currentTime = now.TimeOfDay;

        // ====== QUERY WITH RELATIONSHIPS ======
        var allPendingReservations = await _db.Reservations
            .Include(r => r.User)        // Need to update NoShowCount
            .Include(r => r.Payment)     // Need to cancel payment if cash
            .Where(r => r.Status == ReservationStatus.Pending)
            .ToListAsync();

        var overdueReservations = allPendingReservations
            .Where(r =>
                r.SlotDate < today ||
                (r.SlotDate == today &&
                 currentTime > r.SlotEndTime.Add(ReservationSlotConfig.CheckInBuffer)))
            .ToList();

        foreach (var reservation in overdueReservations)
        {
            // ====== UPDATE RESERVATION STATUS ======
            reservation.Status = ReservationStatus.Expired;
            reservation.CancelReason = Models.CancelReason.NoShow;
            reservation.CancelledAt = now;

            // ====== CANCEL PAYMENT (if cash payment exists) ======
            if (reservation.Payment != null)
            {
                reservation.Payment.Status = PaymentStatus.Cancelled;
                _logger.LogInformation("No-show: Cancelled payment {PaymentId} for reservation {ReservationId}",
                    reservation.Payment.Id, reservation.Id);
            }

            // ====== QUOTA LOSS LOGIC FOR SUBSCRIPTION (Logic mới: No refund for no-show) ======
            if (reservation.UserSubscriptionId.HasValue)
            {
                // KHÔNG hoàn quota vì đã bị trừ lúc đặt lịch
                // User đã mất 1 lượt do không đến
                _logger.LogWarning(
                    "No-show: User {UserId} lost quota for subscription reservation {ReservationId}. " +
                    "Quota was deducted at booking time and will NOT be refunded. " +
                    "Subscription: {SubscriptionId}",
                    reservation.UserId,
                    reservation.Id,
                    reservation.UserSubscriptionId.Value);
            }

            // ====== PENALTY LOGIC: Increment NoShowCount ======
            // Both subscription and cash payment users get penalized for no-show
            reservation.User.NoShowCount++;
            _logger.LogWarning("No-show: User {UserId} did not show up for reservation {ReservationId}. NoShowCount incremented to {NoShowCount}",
                reservation.UserId, reservation.Id, reservation.User.NoShowCount);
        }

        if (overdueReservations.Any())
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("Expired {Count} overdue reservations (no-show penalty applied)", overdueReservations.Count);
        }

        return overdueReservations.Count;
    }

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

    private bool VerifyQRCode(Guid reservationId, string qrCodeData)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(qrCodeData));
            var parts = decoded.Split('|');

            if (parts.Length != 2) return false;

            var json = parts[0];
            var signature = parts[1];

            var computedSignature = ComputeHMAC(json);
            if (signature != computedSignature) return false;

            var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (payload == null) return false;

            var rid = payload["rid"].GetString();
            if (rid != reservationId.ToString()) return false;

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

    /// <summary>
    /// ⭐ Helper method: Generate unique transaction reference for Payment
    /// Format: EVByyyyMMddHHmmssRAND (e.g., EVB202510251630001234)
    /// </summary>
    private string GenerateTransactionReference()
    {
        return $"EVB{DateTime.Now:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
    }
}

/// <summary>
/// DTO cho slot availability
/// </summary>
public class SlotAvailabilityDto
{
    public TimeSpan SlotStartTime { get; set; }
    public TimeSpan SlotEndTime { get; set; }

    /// <summary>
    /// Thời gian slot dạng range (VD: "09:00 - 10:00")
    /// </summary>
    public string TimeRange => $"{SlotStartTime:hh\\:mm} - {SlotEndTime:hh\\:mm}";

    public int TotalCapacity { get; set; }
    public int CurrentReservations { get; set; }
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Số slot còn trống
    /// </summary>
    public int AvailableSlots => TotalCapacity - CurrentReservations;
}