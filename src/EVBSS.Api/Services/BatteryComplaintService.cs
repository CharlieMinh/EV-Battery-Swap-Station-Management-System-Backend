// File mới: src/EVBSS.Api/Services/BatteryComplaintService.cs

namespace EVBSS.Api.Services;

using EVBSS.Api.Data;
using EVBSS.Api.Models;
using EVBSS.Api.Dtos.Complaints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using EVBSS.Api.Hubs;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
// using EVBSS.Api.Models; // models are already imported above

public class BatteryComplaintService
{
    private readonly AppDbContext _context;
    private readonly ILogger<BatteryComplaintService> _logger;
    private readonly IBatteryInventoryService _inventoryService;
    private readonly ReservationService? _reservationService;
    private readonly IHubContext<NotificationHub>? _hubContext;


    public BatteryComplaintService(
        AppDbContext context,
        ILogger<BatteryComplaintService> logger,
        IBatteryInventoryService inventoryService,
        ReservationService? reservationService = null,
        IHubContext<NotificationHub>? hubContext = null)
    {
        _context = context;
        _logger = logger;
        _inventoryService = inventoryService;
        _reservationService = reservationService;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Mark complaint as Investigating by a staff member and optionally append investigation notes.
    /// </summary>
    public async Task<BatteryComplaint> InvestigateComplaintAsync(Guid staffId, Guid complaintId, InvestigateComplaintRequest request)
    {
        var complaint = await _context.BatteryComplaints
            .FirstOrDefaultAsync(c => c.Id == complaintId);

        if (complaint == null)
            throw new KeyNotFoundException("Không tìm thấy khiếu nại.");

        if (complaint.Status != ComplaintStatus.Pending)
            throw new InvalidOperationException($"Chỉ có thể chuyển sang Investigating từ trạng thái Pending. Trạng thái hiện tại: {complaint.Status}.");

        complaint.Status = ComplaintStatus.Investigating;
        complaint.HandledByStaffId = staffId;
        complaint.ResolutionNotes = (complaint.ResolutionNotes ?? string.Empty) + "\n[Investigating Note]: " + (request.InvestigationNotes ?? string.Empty);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Complaint {ComplaintId} marked as Investigating by staff {StaffId}", complaintId, staffId);

        return complaint;
    }

    public async Task<List<BatteryComplaint>> GetComplaintsAsync(int page = 1, int pageSize = 20)
    {
        var skip = Math.Max(0, (page - 1) * pageSize);
        return await _context.BatteryComplaints
            .Include(c => c.SwapTransaction)
            .Include(c => c.ReportedByUser)
            .Include(c => c.HandledByStaff)
            .OrderByDescending(c => c.ReportDate)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<BatteryComplaint> GetComplaintByIdAsync(Guid id)
    {
        var complaint = await _context.BatteryComplaints
            .Include(c => c.SwapTransaction)
                .ThenInclude(s => s.Station)
            .Include(c => c.IssuedBattery)
                .ThenInclude(b => b.Model)
            .Include(c => c.ReportedByUser)
            .Include(c => c.HandledByStaff)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (complaint == null)
            throw new KeyNotFoundException("Không tìm thấy khiếu nại.");

        return complaint;
    }

    public async Task<BatteryComplaint> ReportFaultyBatteryAsync(Guid driverId, ReportFaultyBatteryRequest request)
    {
        var swap = await _context.SwapTransactions
            .Include(s => s.IssuedBattery)
            .FirstOrDefaultAsync(s => s.Id == request.SwapTransactionId && s.UserId == driverId);

        if (swap == null || swap.IssuedBattery == null)
            throw new KeyNotFoundException("Giao dịch đổi pin không hợp lệ hoặc không thuộc về bạn.");

        var existingComplaint = await _context.BatteryComplaints
            .AnyAsync(c => c.SwapTransactionId == request.SwapTransactionId);

        if (existingComplaint)
            throw new InvalidOperationException("Một khiếu nại cho giao dịch này đã tồn tại.");

        var complaint = new BatteryComplaint
        {
            ReportedByUserId = driverId,
            SwapTransactionId = request.SwapTransactionId,
            IssuedBatteryId = swap.IssuedBatteryId,
            ComplaintDetails = request.ComplaintDetails,
            Status = ComplaintStatus.Pending,
            ReportDate = DateTime.UtcNow
        };

        _context.BatteryComplaints.Add(complaint);
        await _context.SaveChangesAsync();

        _logger.LogInformation("New battery complaint {ComplaintId} created for swap {SwapId} by user {UserId}", complaint.Id, swap.Id, driverId);

        // Push real-time notification to staff group (if hub is available)
        try
        {
            if (_hubContext != null)
            {
                var payload = new
                {
                    ComplaintId = complaint.Id,
                    SwapId = swap.Id,
                    StationId = swap.StationId,
                    Message = "Người dùng đã báo pin lỗi. Vui lòng kiểm tra."
                };
                await _hubContext.Clients.Group("Staff").SendAsync("ReceiveComplaint", payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send realtime notification for complaint {ComplaintId}", complaint.Id);
        }

        return complaint;
    }

    public async Task<BatteryComplaint> ResolveComplaintAsync(Guid staffId, Guid complaintId, ResolveComplaintRequest request)
    {
        if (request.NewStatus != ComplaintStatus.Confirmed && request.NewStatus != ComplaintStatus.Rejected)
        {
            throw new ArgumentException("Trạng thái giải quyết không hợp lệ. Chỉ chấp nhận Confirmed hoặc Rejected.");
        }

        var complaint = await GetComplaintByIdAsync(complaintId);

        if (complaint.Status != ComplaintStatus.Pending && complaint.Status != ComplaintStatus.Investigating)
            throw new InvalidOperationException($"Không thể xử lý khiếu nại đã ở trạng thái {complaint.Status}.");

        complaint.Status = request.NewStatus;
        complaint.HandledByStaffId = staffId;
        complaint.ResolutionNotes = request.ResolutionNotes;
        complaint.ResolvedAt = DateTime.UtcNow;

        string message;
        if (request.NewStatus == ComplaintStatus.Confirmed)
        {
            var stationName = complaint.SwapTransaction?.Station?.Name ?? "trạm liên quan";
            message = $"Khiếu nại pin lỗi đã được XÁC NHẬN. Vui lòng mang pin lỗi đến {stationName} để nhân viên xác nhận và thực hiện đổi pin thay thế miễn phí.";
        }
        else // Rejected
        {
            message = $"Khiếu nại pin lỗi đã bị TỪ CHỐI. Pin không phát hiện lỗi hệ thống. Chi tiết: {request.ResolutionNotes}";
        }

        _context.Notifications.Add(new Notification
        {
            UserId = complaint.ReportedByUserId,
            SenderId = staffId,
            Message = message,
            CreatedAt = DateTime.UtcNow,
            IsRead = false,
            Type = NotificationType.Generic,
            RelatedEntityId = complaint.Id
        });


        await _context.SaveChangesAsync();
        _logger.LogInformation("Complaint {ComplaintId} has been {Status} by staff {StaffId}", complaintId, request.NewStatus, staffId);

        return complaint;
    }

    public async Task<Reservation> ProcessFaultyBatteryReturnAndCreateReswapAsync(Guid staffId, Guid complaintId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var complaint = await GetComplaintByIdAsync(complaintId);

            if (complaint.Status != ComplaintStatus.Confirmed)
                throw new InvalidOperationException($"Không thể tạo re-swap cho khiếu nại ở trạng thái {complaint.Status}.");

            var issuedBattery = complaint.IssuedBattery
                ?? throw new InvalidOperationException("Không tìm thấy thông tin pin bị lỗi.");
            
            var swap = complaint.SwapTransaction;

            // 1. Update battery status to Faulty and update inventory
            var oldStatus = issuedBattery.Status;
            if (oldStatus != BatteryStatus.Faulty)
            {
                issuedBattery.Status = BatteryStatus.Faulty;
                issuedBattery.UpdatedAt = DateTime.UtcNow;
                await _inventoryService.UpdateInventoryCountAsync(
                    issuedBattery.BatteryModelId,
                    issuedBattery.StationId,
                    oldStatus,
                    BatteryStatus.Faulty,
                    1);
            }

            // 2. Create a new free reservation (re-swap) directly
            var stationId = swap?.StationId ?? issuedBattery.StationId;
            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                UserId = complaint.ReportedByUserId,
                StationId = stationId,
                BatteryModelId = issuedBattery.BatteryModelId,
                SlotDate = DateOnly.FromDateTime(DateTime.UtcNow),
                SlotStartTime = TimeSpan.Zero,
                SlotEndTime = TimeSpan.FromMinutes(30),
                Status = ReservationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                // Link reservation to the originating complaint so downstream processes can trace back
                RelatedComplaintId = complaint.Id
            };

            _context.Reservations.Add(reservation);

            // 3. Append a note that a re-swap reservation was created.
            // NOTE: keep the complaint in the Confirmed state until the re-swap is actually completed.
            // The complaint will be finalized (Resolved) later by FinalizeComplaintAsync when the re-swap completes.
            complaint.ResolutionNotes = (complaint.ResolutionNotes ?? string.Empty) + $" Pin lỗi đã được thu hồi. Đã tạo lượt đổi pin miễn phí (Reservation ID: {reservation.Id}).";

            var stationNameForNotif = swap?.Station?.Name ?? "trạm liên quan";
            _context.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = complaint.ReportedByUserId,
                SenderId = staffId,
                Message = $"Bạn đã nhận được một lượt đổi pin miễn phí tại trạm {stationNameForNotif}. Vui lòng kiểm tra mục 'Lịch hẹn'.",
                Type = NotificationType.Generic,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                RelatedEntityId = complaint.Id
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Successfully processed faulty battery return for complaint {ComplaintId}. Created free reservation {ReservationId}", complaintId, reservation.Id);

            return reservation;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error processing faulty battery return for complaint {ComplaintId}", complaintId);
            throw;
        }
    }

    /// <summary>
    /// Staff/Hệ thống tự động đóng khiếu nại sau khi quá trình Re-swap hoàn tất.
    /// </summary>
    public async Task<BatteryComplaint> FinalizeComplaintAsync(Guid staffId, Guid complaintId)
    {
        var complaint = await _context.BatteryComplaints
            .FirstOrDefaultAsync(c => c.Id == complaintId);

        if (complaint == null)
            throw new KeyNotFoundException("Không tìm thấy khiếu nại.");

        // Chỉ có thể Resolved khi khiếu nại đã được xác nhận (Confirmed)
        if (complaint.Status != ComplaintStatus.Confirmed)
            throw new InvalidOperationException($"Chỉ có thể giải quyết (Resolve) khiếu nại đã được xác nhận (Confirmed). Trạng thái hiện tại: {complaint.Status}.");

        complaint.Status = ComplaintStatus.Resolved; // ⭐ Chuyển trạng thái
        complaint.HandledByStaffId = staffId; // Hoặc dùng StaffId đang thực hiện Re-swap
        complaint.ResolvedAt = DateTime.UtcNow;
        complaint.ResolutionNotes = (complaint.ResolutionNotes ?? string.Empty) + "\n[System Note]: Complaint finalized after successful battery re-swap.";

        await _context.SaveChangesAsync();
        
        // ⭐ Notification: Báo Driver: Khiếu nại đã được đóng.
        _context.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = complaint.ReportedByUserId,
            SenderId = staffId,
            Message = $"Khiếu nại số {complaint.Id} đã được ĐÓNG (Resolved) sau khi bạn hoàn tất giao dịch đổi pin thay thế. Cảm ơn bạn.",
            CreatedAt = DateTime.UtcNow,
            IsRead = false,
            Type = NotificationType.Generic,
            RelatedEntityId = complaint.Id
        });
        await _context.SaveChangesAsync();
        
        return complaint;
    }
}