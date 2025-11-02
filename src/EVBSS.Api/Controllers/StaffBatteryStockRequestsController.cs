using EVBSS.Api.Dtos.BatteryUnits;
using EVBSS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace EVBSS.Api.Controllers;

/// <summary>
/// API cho Staff quản lý yêu cầu tăng pin
/// </summary>
[Authorize(Roles = "Staff")]
[Route("api/v1/staff/stock-requests")]
[ApiController]
public class StaffBatteryStockRequestsController : ControllerBase
{
    private readonly IBatteryStockRequestService _stockRequestService;
    private readonly ILogger<StaffBatteryStockRequestsController> _logger;

    public StaffBatteryStockRequestsController(
        IBatteryStockRequestService stockRequestService,
        ILogger<StaffBatteryStockRequestsController> logger)
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
    /// Staff tạo yêu cầu Admin tăng pin cho trạm
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BatteryStockRequestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateStockRequest([FromBody] RequestBatteryStockDto requestDto)
    {
        try
        {
            var staffId = GetCurrentUserId();
            var request = await _stockRequestService.RequestStockAsync(staffId, requestDto);

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

            return CreatedAtAction(
                nameof(GetRequestById), 
                new { id = request.Id }, 
                new
                {
                    message = "✅ Yêu cầu tăng pin đã được gửi đến Admin.",
                    request = response
                });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create stock request");
            return BadRequest(new { error = new { code = "INVALID_OPERATION", message = ex.Message } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating stock request");
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Lỗi nội bộ khi tạo yêu cầu." } });
        }
    }

    /// <summary>
    /// Staff xem chi tiết yêu cầu của mình
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BatteryStockRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRequestById(Guid id)
    {
        try
        {
            var staffId = GetCurrentUserId();
            var request = await _stockRequestService.GetRequestByIdAsync(id);

            if (request == null)
            {
                return NotFound(new { error = new { code = "REQUEST_NOT_FOUND", message = "Không tìm thấy yêu cầu." } });
            }

            // Kiểm tra quyền: Staff chỉ được xem yêu cầu của chính mình
            if (request.RequestedByStaffId != staffId)
            {
                return Forbid();
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

    /// <summary>
    /// Staff xem tất cả yêu cầu của mình
    /// </summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IEnumerable<BatteryStockRequestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyRequests()
    {
        try
        {
            var staffId = GetCurrentUserId();
            var requests = await _stockRequestService.GetStaffRequestsAsync(staffId);

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
                RequestDate = r.RequestDate,
                AdminReviewerId = r.AdminReviewerId,
                AdminReviewerName = r.AdminReviewer?.Name,
                AdminReviewDate = r.AdminReviewDate,
                AdminNote = r.AdminNote,
                RelatedBulkCreateRequestId = r.RelatedBulkCreateRequestId,
                UpdatedAt = r.UpdatedAt
            });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting staff requests");
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Lỗi nội bộ." } });
        }
    }
}
