using EVBSS.Api.Dtos.Subscriptions;
using EVBSS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/subscriptions")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(ISubscriptionService subscriptionService, ILogger<SubscriptionsController> logger)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    /// <summary>
    /// ⭐ NEW: Tạo subscription pending (chờ thanh toán) - FLOW FRONTEND YÊU CẦU
    /// Tạo UserSubscription với IsActive=false + Payment pending + VNPay URL
    /// </summary>
    [HttpPost("create-pending")]
    public async Task<ActionResult<CreatePendingSubscriptionResponse>> CreatePendingSubscription(CreatePendingSubscriptionRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ipAddress = GetClientIpAddress();
            
            var result = await _subscriptionService.CreatePendingSubscriptionAsync(userId, request, ipAddress);
            
            _logger.LogInformation("User {UserId} created pending subscription {SubscriptionId}, payment {PaymentId}", 
                userId, result.UserSubscriptionId, result.PaymentId);
            
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request for creating pending subscription");
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business rule violation for creating pending subscription");
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pending subscription");
            return StatusCode(500, new { message = "Có lỗi xảy ra khi tạo subscription." });
        }
    }

    /// <summary>
    /// Đăng ký gói subscription cho user (API cũ - dùng cho admin/special cases)
    /// NOTE: User thường dùng /create-pending để tạo subscription với flow thanh toán
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SubscriptionCreatedResponse>> CreateSubscription(CreateSubscriptionRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _subscriptionService.CreateSubscriptionAsync(userId, request);
            
            _logger.LogInformation("User {UserId} successfully created subscription {SubscriptionId}", 
                userId, result.SubscriptionId);
            
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request for creating subscription");
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business rule violation for creating subscription");
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subscription");
            return StatusCode(500, new { message = "Có lỗi xảy ra khi tạo subscription." });
        }
    }

    /// <summary>
    /// Lấy thông tin subscription hiện tại của user
    /// </summary>
    [HttpGet("mine")]
    public async Task<ActionResult<UserSubscriptionDto>> GetMySubscription()
    {
        try
        {
            var userId = GetCurrentUserId();
            var subscription = await _subscriptionService.GetUserActiveSubscriptionAsync(userId);
            
            if (subscription == null)
            {
                return NotFound(new { message = "Bạn chưa có gói subscription nào đang hoạt động." });
            }
            
            return Ok(subscription);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user subscription");
            return StatusCode(500, new { message = "Có lỗi xảy ra khi lấy thông tin subscription." });
        }
    }

    /// <summary>
    /// Hủy subscription hiện tại của user
    /// </summary>
    [HttpPut("mine/cancel")]
    public async Task<ActionResult<CancelSubscriptionResponse>> CancelMySubscription()
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _subscriptionService.CancelSubscriptionAsync(userId);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }
            
            _logger.LogInformation("User {UserId} successfully cancelled their subscription", userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling subscription");
            return StatusCode(500, new { message = "Có lỗi xảy ra khi hủy subscription." });
        }
    }

    /// <summary>
    /// Xem thống kê sử dụng pin của user
    /// </summary>
    [HttpGet("mine/usage")]
    public async Task<ActionResult<SubscriptionUsageDto>> GetMySubscriptionUsage()
    {
        try
        {
            var userId = GetCurrentUserId();
            var usage = await _subscriptionService.GetSubscriptionUsageAsync(userId);
            
            if (usage == null)
            {
                return NotFound(new { message = "Bạn chưa có gói subscription nào đang hoạt động." });
            }
            
            return Ok(usage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription usage");
            return StatusCode(500, new { message = "Có lỗi xảy ra khi lấy thống kê sử dụng." });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }

    private string GetClientIpAddress()
    {
        // Try X-Forwarded-For header first (for proxies/load balancers)
        var forwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        // Fallback to RemoteIpAddress
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }
}