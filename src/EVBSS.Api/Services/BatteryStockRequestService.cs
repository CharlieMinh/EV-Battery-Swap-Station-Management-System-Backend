using EVBSS.Api.Data;
using EVBSS.Api.Dtos.BatteryUnits;
using EVBSS.Api.Hubs;
using EVBSS.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Services;

public class BatteryStockRequestService : IBatteryStockRequestService
{
    private readonly AppDbContext _context;
    private readonly ILogger<BatteryStockRequestService> _logger;
    private readonly IHubContext<NotificationHub> _hubContext;

    public BatteryStockRequestService(
        AppDbContext context, 
        ILogger<BatteryStockRequestService> logger, 
        IHubContext<NotificationHub> hubContext)
    {
        _context = context;
        _logger = logger;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Staff tạo yêu cầu tăng pin
    /// </summary>
    public async Task<BatteryStockRequest> RequestStockAsync(Guid staffId, RequestBatteryStockDto dto)
    {
        // Kiểm tra Staff có được gán cho StationId này không
        var staff = await _context.Users.FindAsync(staffId);
        if (staff == null)
        {
            throw new InvalidOperationException("Không tìm thấy thông tin Staff.");
        }

        if (staff.Role != Role.Staff)
        {
            throw new InvalidOperationException("Chỉ Staff mới có thể tạo yêu cầu tăng pin.");
        }

        if (staff.StationId != dto.StationId)
        {
            throw new InvalidOperationException("Bạn không có quyền tạo yêu cầu cho trạm này.");
        }

        // Kiểm tra StationId và BatteryModelId có tồn tại không
        var stationExists = await _context.Stations.AnyAsync(s => s.Id == dto.StationId);
        if (!stationExists)
        {
            throw new InvalidOperationException("Trạm không tồn tại.");
        }

        var batteryModelExists = await _context.BatteryModels.AnyAsync(b => b.Id == dto.BatteryModelId);
        if (!batteryModelExists)
        {
            throw new InvalidOperationException("Loại pin không tồn tại.");
        }

        // Tạo yêu cầu mới
        var request = new BatteryStockRequest
        {
            StationId = dto.StationId,
            BatteryModelId = dto.BatteryModelId,
            Quantity = dto.Quantity,
            StaffNote = dto.StaffNote,
            RequestedByStaffId = staffId,
            Status = BatteryStockRequestStatus.PendingAdminReview,
            RequestDate = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.BatteryStockRequests.Add(request);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Staff {StaffId} created battery stock request {RequestId} for Station {StationId}, " +
            "BatteryModel {BatteryModelId}, Quantity {Quantity}",
            staffId, request.Id, dto.StationId, dto.BatteryModelId, dto.Quantity);

        // Gửi thông báo SignalR đến tất cả Admin
        var admins = await _context.Users.Where(u => u.Role == Role.Admin).ToListAsync();
        foreach (var admin in admins)
        {
            var notification = new Notification
            {
                UserId = admin.Id,
                SenderId = staffId,
                Message = $"Staff {staff.Name} yêu cầu tăng {dto.Quantity} pin loại {dto.BatteryModelId} cho trạm.",
                Type = NotificationType.StockRequestCreated,
                RelatedEntityId = request.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Notifications.Add(notification);
        }
        await _context.SaveChangesAsync();

        await _hubContext.Clients.Group("Admins").SendAsync("NewStockRequest", request);

        // Reload request với navigation properties để trả về đầy đủ thông tin
        var requestWithDetails = await _context.BatteryStockRequests
            .Include(r => r.Station)
            .Include(r => r.BatteryModel)
            .Include(r => r.RequestedByStaff)
            .FirstOrDefaultAsync(r => r.Id == request.Id);

        return requestWithDetails!;
    }

    /// <summary>
    /// Admin duyệt hoặc từ chối yêu cầu (Tự động tạo BulkCreateRequest nếu approve)
    /// </summary>
    public async Task<BatteryStockRequest> ReviewRequestAsync(
        Guid adminId, 
        Guid requestId, 
        ReviewBatteryStockRequestDto dto)
    {
        // 1. Lấy yêu cầu Staff
        var request = await _context.BatteryStockRequests
            .Include(r => r.RequestedByStaff)
            .Include(r => r.Station)
            .Include(r => r.BatteryModel)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
        {
            throw new KeyNotFoundException("Không tìm thấy yêu cầu.");
        }

        if (request.Status != BatteryStockRequestStatus.PendingAdminReview)
        {
            throw new InvalidOperationException(
                $"Yêu cầu đã được xử lý với trạng thái {request.Status}. Không thể duyệt lại.");
        }

        // Lấy thông tin Admin
        var admin = await _context.Users.FindAsync(adminId);
        if (admin == null || admin.Role != Role.Admin)
        {
            throw new InvalidOperationException("Chỉ Admin mới có thể duyệt yêu cầu.");
        }

        // Cập nhật thông tin duyệt
        request.AdminReviewerId = adminId;
        request.AdminReviewDate = DateTime.UtcNow;
        request.AdminNote = dto.AdminNote;
        request.UpdatedAt = DateTime.UtcNow;

        if (dto.IsApproved)
        {
            // ===== DUYỆT: Tự động tạo BulkCreateRequest =====
            request.Status = BatteryStockRequestStatus.Approved;

            // Tạo BulkCreateRequest tự động
            var newBulkRequest = new BulkCreateRequest
            {
                // Dữ liệu TỰ ĐỘNG ĐIỀN TỪ YÊU CẦU CỦA STAFF
                StationId = request.StationId,
                BatteryModelId = request.BatteryModelId,
                Quantity = request.Quantity,

                // Dữ liệu mặc định
                Status = RequestStatus.PendingConfirmation,
                RequestedByAdminId = adminId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,

                StaffNotes = $"[Tự động] Được tạo từ yêu cầu #{request.Id} của Staff {request.RequestedByStaff?.Name ?? "Unknown"}. " +
                             $"Ghi chú Staff: {request.StaffNote ?? "Không có"}. " +
                             $"Ghi chú Admin: {dto.AdminNote ?? "Không có"}."
            };

            _context.BulkCreateRequests.Add(newBulkRequest);
            await _context.SaveChangesAsync(); // Lưu để có BulkCreateRequest.Id

            // Liên kết hai yêu cầu
            request.RelatedBulkCreateRequestId = newBulkRequest.Id;

            // Gửi thông báo đến Staff tại trạm
            var staffInStation = await _context.Users
                .Where(u => u.StationId == request.StationId && u.Role == Role.Staff)
                .ToListAsync();

            var adminIdentifier = !string.IsNullOrEmpty(admin.Name) ? admin.Name : admin.Email;
            var notificationMessage = 
                $"✅ Admin {adminIdentifier} đã duyệt yêu cầu pin của bạn. " +
                $"Yêu cầu tạo pin #{newBulkRequest.Id} đã được tự động tạo và đang chờ xác nhận tại trạm.";

            foreach (var staff in staffInStation)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = staff.Id,
                    SenderId = adminId,
                    Message = notificationMessage,
                    Type = NotificationType.NewBulkRequest,
                    RelatedEntityId = newBulkRequest.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            // SignalR notification
            await _hubContext.Clients.Group($"Station_{request.StationId}")
                .SendAsync("NewBulkRequest", newBulkRequest);

            _logger.LogInformation(
                "✅ Staff Battery Stock Request {RequestId} approved by Admin {AdminId}. " +
                "Auto-created BulkCreateRequest {BulkRequestId}",
                requestId, adminId, newBulkRequest.Id);
        }
        else
        {
            // ===== TỪ CHỐI =====
            request.Status = BatteryStockRequestStatus.Rejected;

            // Gửi thông báo cho Staff đã yêu cầu
            if (request.RequestedByStaff != null)
            {
                var adminIdentifier = !string.IsNullOrEmpty(admin.Name) ? admin.Name : admin.Email;
                var notificationMessage = 
                    $"❌ Yêu cầu pin của bạn đã bị Admin {adminIdentifier} từ chối. " +
                    $"Lý do: {dto.AdminNote ?? "Không có"}";

                _context.Notifications.Add(new Notification
                {
                    UserId = request.RequestedByStaff.Id,
                    SenderId = adminId,
                    Message = notificationMessage,
                    Type = NotificationType.StockRequestRejected,
                    RelatedEntityId = request.Id,
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                await _hubContext.Clients.User(request.RequestedByStaff.Id.ToString())
                    .SendAsync("StockRequestRejected", request);
            }

            _logger.LogInformation(
                "❌ Staff Battery Stock Request {RequestId} rejected by Admin {AdminId}. Reason: {Reason}",
                requestId, adminId, dto.AdminNote);
        }

        await _context.SaveChangesAsync();
        return request;
    }

    /// <summary>
    /// Lấy tất cả yêu cầu chờ duyệt (Admin)
    /// </summary>
    public async Task<List<BatteryStockRequest>> GetPendingRequestsAsync()
    {
        return await _context.BatteryStockRequests
            .Include(r => r.Station)
            .Include(r => r.BatteryModel)
            .Include(r => r.RequestedByStaff)
            .Where(r => r.Status == BatteryStockRequestStatus.PendingAdminReview)
            .OrderBy(r => r.RequestDate)
            .ToListAsync();
    }

    /// <summary>
    /// Lấy yêu cầu theo ID
    /// </summary>
    public async Task<BatteryStockRequest?> GetRequestByIdAsync(Guid requestId)
    {
        return await _context.BatteryStockRequests
            .Include(r => r.Station)
            .Include(r => r.BatteryModel)
            .Include(r => r.RequestedByStaff)
            .Include(r => r.AdminReviewer)
            .Include(r => r.RelatedBulkCreateRequest)
            .FirstOrDefaultAsync(r => r.Id == requestId);
    }

    /// <summary>
    /// Lấy danh sách yêu cầu của Staff
    /// </summary>
    public async Task<List<BatteryStockRequest>> GetStaffRequestsAsync(Guid staffId)
    {
        return await _context.BatteryStockRequests
            .Include(r => r.Station)
            .Include(r => r.BatteryModel)
            .Include(r => r.AdminReviewer)
            .Where(r => r.RequestedByStaffId == staffId)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    /// <summary>
    /// Cập nhật trạng thái yêu cầu thành Completed khi Staff xác nhận BulkCreateRequest
    /// Hàm này được gọi từ BulkCreateRequestsController khi Staff confirm
    /// </summary>
    public async Task CompleteStockRequestAsync(Guid bulkCreateRequestId)
    {
        var relatedRequest = await _context.BatteryStockRequests
            .FirstOrDefaultAsync(r => 
                r.RelatedBulkCreateRequestId == bulkCreateRequestId &&
                r.Status == BatteryStockRequestStatus.Approved);

        if (relatedRequest != null)
        {
            relatedRequest.Status = BatteryStockRequestStatus.Completed;
            relatedRequest.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "✅ Battery Stock Request {RequestId} marked as Completed after BulkCreateRequest {BulkRequestId} confirmation",
                relatedRequest.Id, bulkCreateRequestId);
        }
    }
}
