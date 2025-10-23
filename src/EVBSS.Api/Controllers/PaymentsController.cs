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
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IVnPayService vnPayService, 
        IPaymentService paymentService,
        ILogger<PaymentsController> logger)
    {
        _vnPayService = vnPayService;
        _paymentService = paymentService;
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
                _logger.LogInformation("Created VNPay payment for user {UserId}, subscription {SubscriptionId}", userId, request.SubscriptionId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("Failed to create VNPay payment for user {UserId}, subscription {SubscriptionId}: {Message}", 
                    userId, request.SubscriptionId, result.Message);
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

    /// <summary>
    /// ⭐ LUỒNG 2: Tạo đặt lịch lẻ (Pay-per-Swap) với thanh toán VNPay hoặc Cash
    /// </summary>
    [HttpPost("create-pay-per-swap-reservation")]
    [Authorize] // Only authenticated users
    public async Task<ActionResult<CreatePayPerSwapReservationResponse>> CreatePayPerSwapReservation(
        [FromBody] CreatePayPerSwapReservationRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var ipAddress = GetClientIpAddress();
            
            var result = await _paymentService.CreatePayPerSwapReservationAsync(
                userId, 
                request, 
                ipAddress);
            
            if (result.Success)
            {
                _logger.LogInformation(
                    "Created pay-per-swap reservation for user {UserId}, station {StationId}, method {Method}, payment {PaymentId}", 
                    userId, request.StationId, request.PaymentMethod, result.PaymentId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to create pay-per-swap reservation for user {UserId}: {Message}", 
                    userId, result.Message);
                return BadRequest(result);
            }
        }
        catch (ActiveReservationExistsException ex)
        {
            _logger.LogWarning(ex, "User {UserId} already has active reservation", GetCurrentUserId());
            return BadRequest(new CreatePayPerSwapReservationResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
        catch (SlotNotAvailableException ex)
        {
            _logger.LogWarning(ex, "Slot not available for user {UserId}", GetCurrentUserId());
            return BadRequest(new CreatePayPerSwapReservationResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pay-per-swap reservation for user {UserId}", GetCurrentUserId());
            return StatusCode(500, new CreatePayPerSwapReservationResponse
            {
                Success = false,
                Message = "Có lỗi xảy ra khi tạo đặt lịch. Vui lòng thử lại sau."
            });
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

    /// <summary>
    /// User chọn thanh toán bằng tiền mặt
    /// </summary>
    [HttpPost("{paymentId:guid}/select-cash")]
    public async Task<ActionResult<SelectCashMethodResponse>> SelectCashMethod(Guid paymentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _paymentService.SelectCashMethodAsync(userId, paymentId);
            
            if (result.Success)
            {
                _logger.LogInformation("User {UserId} selected CASH for payment {PaymentId}", userId, paymentId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("Failed to select CASH for payment {PaymentId}: {Message}", paymentId, result.Message);
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting cash method for payment {PaymentId}", paymentId);
            return StatusCode(500, new SelectCashMethodResponse
            {
                Success = false,
                Message = "Có lỗi xảy ra khi chọn phương thức thanh toán."
            });
        }
    }

    /// <summary>
    /// Staff xác nhận đã nhận tiền mặt
    /// </summary>
    [HttpPost("{paymentId:guid}/confirm-cash")]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<ActionResult<ConfirmCashPaymentResponse>> ConfirmCashPayment(
        Guid paymentId, 
        [FromBody] ConfirmCashPaymentRequest request)
    {
        try
        {
            var staffId = GetCurrentUserId();
            var result = await _paymentService.ConfirmCashPaymentAsync(staffId, paymentId, request);
            
            if (result.Success)
            {
                _logger.LogInformation("Staff {StaffId} confirmed CASH payment {PaymentId}", staffId, paymentId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("Failed to confirm CASH payment {PaymentId}: {Message}", paymentId, result.Message);
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming cash payment {PaymentId}", paymentId);
            return StatusCode(500, new ConfirmCashPaymentResponse
            {
                Success = false,
                Message = "Có lỗi xảy ra khi xác nhận thanh toán."
            });
        }
    }
}