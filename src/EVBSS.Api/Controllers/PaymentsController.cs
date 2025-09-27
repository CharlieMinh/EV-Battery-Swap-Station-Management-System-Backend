using EVBSS.Api.Dtos.Payments;
using EVBSS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IVnPayService _vnPayService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IVnPayService vnPayService, ILogger<PaymentsController> logger)
    {
        _vnPayService = vnPayService;
        _logger = logger;
    }

    /// <summary>
    /// Tạo link thanh toán VNPay
    /// </summary>
    [HttpPost("vnpay/create")]
    public async Task<ActionResult<VnPayPaymentResponse>> CreateVnPayPayment(CreateVnPayPaymentRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ipAddress = GetClientIpAddress();
            
            var result = await _vnPayService.CreatePaymentAsync(userId, request, ipAddress);
            
            if (result.Success)
            {
                _logger.LogInformation("Created VNPay payment for user {UserId}, invoice {InvoiceId}", userId, request.InvoiceId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("Failed to create VNPay payment for user {UserId}, invoice {InvoiceId}: {Message}", 
                    userId, request.InvoiceId, result.Message);
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating VNPay payment");
            return StatusCode(500, new VnPayPaymentResponse 
            { 
                Success = false, 
                Message = "Có lỗi xảy ra khi tạo thanh toán." 
            });
        }
    }

    /// <summary>
    /// Xử lý callback từ VNPay (IPN)
    /// </summary>
    [HttpGet("vnpay/callback")]
    [AllowAnonymous] // VNPay callback doesn't include authorization
    public async Task<ActionResult<VnPayCallbackResponse>> VnPayCallback([FromQuery] VnPayCallbackRequest callback)
    {
        try
        {
            _logger.LogInformation("Received VNPay callback for TxnRef: {TxnRef}", callback.vnp_TxnRef);
            
            var result = await _vnPayService.ProcessCallbackAsync(callback);
            
            // Return plain text response as expected by VNPay
            return Content($"RspCode={result.RspCode}&Message={result.Message}", "text/plain");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing VNPay callback");
            return Content("RspCode=99&Message=Unknown error", "text/plain");
        }
    }

    /// <summary>
    /// Xử lý return từ VNPay (user redirect back)
    /// </summary>
    [HttpGet("vnpay/return")]
    [AllowAnonymous]
    public IActionResult VnPayReturn([FromQuery] VnPayCallbackRequest returnData)
    {
        try
        {
            _logger.LogInformation("Received VNPay return for TxnRef: {TxnRef}", returnData.vnp_TxnRef);
            
            // Validate the return data
            var isValid = _vnPayService.ValidateCallback(returnData);
            var isSuccess = returnData.vnp_ResponseCode == "00" && returnData.vnp_TransactionStatus == "00";
            
            if (isValid && isSuccess)
            {
                // Payment successful - redirect to success page
                return Redirect($"/payment/success?ref={returnData.vnp_TxnRef}&amount={returnData.vnp_Amount}");
            }
            else
            {
                // Payment failed - redirect to failure page
                return Redirect($"/payment/failure?ref={returnData.vnp_TxnRef}&code={returnData.vnp_ResponseCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing VNPay return");
            return Redirect("/payment/error");
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
        // Try to get IP from X-Forwarded-For header (if behind proxy)
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        // Try to get IP from X-Real-IP header
        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fallback to connection remote IP
        return Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }
}