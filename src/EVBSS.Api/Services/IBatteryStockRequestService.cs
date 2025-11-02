using EVBSS.Api.Dtos.BatteryUnits;
using EVBSS.Api.Models;

namespace EVBSS.Api.Services;

public interface IBatteryStockRequestService
{
    /// <summary>
    /// Staff tạo yêu cầu tăng pin
    /// </summary>
    Task<BatteryStockRequest> RequestStockAsync(Guid staffId, RequestBatteryStockDto dto);
    
    /// <summary>
    /// Admin duyệt hoặc từ chối yêu cầu (Tự động tạo BulkCreateRequest nếu approve)
    /// </summary>
    Task<BatteryStockRequest> ReviewRequestAsync(Guid adminId, Guid requestId, ReviewBatteryStockRequestDto dto);
    
    /// <summary>
    /// Lấy tất cả yêu cầu chờ duyệt (Admin)
    /// </summary>
    Task<List<BatteryStockRequest>> GetPendingRequestsAsync();
    
    /// <summary>
    /// Lấy yêu cầu theo ID
    /// </summary>
    Task<BatteryStockRequest?> GetRequestByIdAsync(Guid requestId);
    
    /// <summary>
    /// Lấy danh sách yêu cầu của Staff
    /// </summary>
    Task<List<BatteryStockRequest>> GetStaffRequestsAsync(Guid staffId);
    
    /// <summary>
    /// Cập nhật trạng thái yêu cầu thành Completed khi Staff xác nhận BulkCreateRequest
    /// </summary>
    Task CompleteStockRequestAsync(Guid bulkCreateRequestId);
}
