using EVBSS.Api.Dtos.BatteryUnits;
using EVBSS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace EVBSS.Api.Controllers;

/// <summary>
/// API cho Admin quản lý yêu cầu tăng pin từ Staff
/// </summary>
[Authorize(Roles = "Admin")]
[Route("api/v1/admin/stock-requests")]
[ApiController]
public class AdminBatteryStockRequestsController : ControllerBase
{
    private readonly IBatteryStockRequestService _stockRequestService;
    private readonly ILogger<AdminBatteryStockRequestsController> _logger;

    public AdminBatteryStockRequestsController(
        IBatteryStockRequestService stockRequestService,
        ILogger<AdminBatteryStockRequestsController> logger)
    {
        _stockRequestService = stockRequestService;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        
        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
        {
            throw new InvalidOperationException("User ID not found in token.");
        }
        return userId;
    }

    /// <summary>
    /// Admin duyệt hoặc từ chối yêu cầu tăng pin từ Staff
    /// Nếu duyệt, hệ thống sẽ TỰ ĐỘNG tạo BulkCreateRequest với dữ liệu từ yêu cầu Staff
    /// </summary>
    [HttpPost("{id:guid}/review")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewRequest(Guid id, [FromBody] ReviewBatteryStockRequestDto reviewDto)
    {
        try
        {
            var adminId = GetCurrentUserId();
            var request = await _stockRequestService.ReviewRequestAsync(adminId, id, reviewDto);

            if (request.Status == Models.BatteryStockRequestStatus.Rejected)
            {
                return Ok(new
                {
                    message = "❌ Yêu cầu đã được từ chối.",
                    requestId = request.Id,
                    status = request.Status.ToString(),
                    adminNote = request.AdminNote
                });
            }

            // Nếu duyệt thành công
            return Ok(new
            {
                message = "✅ Yêu cầu đã được duyệt thành công! " +
                         "Hệ thống đã TỰ ĐỘNG tạo yêu cầu tăng pin (BulkCreateRequest) " +
                         "và gửi thông báo đến Staff tại trạm để xác nhận.",
                requestId = request.Id,
                status = request.Status.ToString(),
                bulkCreateRequestId = request.RelatedBulkCreateRequestId,
                adminNote = request.AdminNote
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Request not found: {RequestId}", id);
            return NotFound(new { error = new { code = "REQUEST_NOT_FOUND", message = ex.Message } });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation on request {RequestId}", id);
            return BadRequest(new { error = new { code = "INVALID_OPERATION", message = ex.Message } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing request {RequestId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Lỗi nội bộ khi xử lý yêu cầu." } });
        }
    }

    /// <summary>
    /// Admin xem tất cả yêu cầu chờ duyệt
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IEnumerable<BatteryStockRequestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingRequests()
    {
        try
        {
            var requests = await _stockRequestService.GetPendingRequestsAsync();

            var response = requests.Select(r => new BatteryStockRequestResponse
            {
                Id = r.Id,
                StationId = r.StationId,
                StationName = r.Station?.Name,
                BatteryModelId = r.BatteryModelId,
                BatteryModelName = r.BatteryModel?.Name,
                Quantity = r.Quantity,
                StaffNote = r.StaffNote,
                Status = r.Status.ToString(),
                RequestedByStaffId = r.RequestedByStaffId,
                RequestedByStaffName = r.RequestedByStaff?.Name,
                RequestDate = r.RequestDate,
                UpdatedAt = r.UpdatedAt
            });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending requests");
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Lỗi nội bộ." } });
        }
    }

    /// <summary>
    /// Admin xem chi tiết yêu cầu
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BatteryStockRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRequestById(Guid id)
    {
        try
        {
            var request = await _stockRequestService.GetRequestByIdAsync(id);

            if (request == null)
            {
                return NotFound(new { error = new { code = "REQUEST_NOT_FOUND", message = "Không tìm thấy yêu cầu." } });
            }

            var response = new BatteryStockRequestResponse
            {
                Id = request.Id,
                StationId = request.StationId,
                StationName = request.Station?.Name,
                BatteryModelId = request.BatteryModelId,
                BatteryModelName = request.BatteryModel?.Name,
                Quantity = request.Quantity,
                StaffNote = request.StaffNote,
                Status = request.Status.ToString(),
                RequestedByStaffId = request.RequestedByStaffId,
                RequestedByStaffName = request.RequestedByStaff?.Name,
                RequestDate = request.RequestDate,
                AdminReviewerId = request.AdminReviewerId,
                AdminReviewerName = request.AdminReviewer?.Name,
                AdminReviewDate = request.AdminReviewDate,
                AdminNote = request.AdminNote,
                RelatedBulkCreateRequestId = request.RelatedBulkCreateRequestId,
                UpdatedAt = request.UpdatedAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting request {RequestId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Lỗi nội bộ." } });
        }
    }
}
