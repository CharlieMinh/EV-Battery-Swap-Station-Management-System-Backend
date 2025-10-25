using EVBSS.Api.Dtos.Payments;
using EVBSS.Api.Models;
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
    /// ⭐ API MỚI: Lấy danh sách payments (Staff/Admin dashboard)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<ActionResult<object>> GetPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] PaymentStatus? status = null,
        [FromQuery] PaymentMethod? method = null,
        [FromQuery] PaymentType? type = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var (payments, totalCount) = await _paymentService.GetPaymentsAsync(
                page, pageSize, status, method, type, fromDate, toDate);

            return Ok(new
            {
                payments,
                pagination = new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payments list");
            return StatusCode(500, new { message = "Có lỗi xảy ra khi lấy danh sách thanh toán." });
        }
    }

    /// <summary>
    /// ⭐ API MỚI: Driver lấy danh sách payments của chính mình
    /// </summary>
    [HttpGet("my-payments")]
    [Authorize(Roles = "Driver")]
    public async Task<ActionResult<object>> GetMyPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] PaymentStatus? status = null,
        [FromQuery] PaymentMethod? method = null,
        [FromQuery] PaymentType? type = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // Sử dụng lại method GetPaymentsAsync với userId filter
            var (payments, totalCount) = await _paymentService.GetPaymentsAsync(
                page, pageSize, status, method, type, fromDate, toDate, userId);

            return Ok(new
            {
                payments,
                pagination = new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting my payments for user {UserId}", GetCurrentUserId());
            return StatusCode(500, new { message = "Có lỗi xảy ra khi lấy lịch sử thanh toán." });
        }
    }

    /// <summary>
    /// ⭐ API MỚI: Lấy chi tiết 1 payment
    /// </summary>
    [HttpGet("{paymentId:guid}")]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<ActionResult<PaymentDetailResponse>> GetPaymentDetail(Guid paymentId)
    {
        try
        {
            var payment = await _paymentService.GetPaymentDetailAsync(paymentId);

            if (payment == null)
                return NotFound(new { message = "Không tìm thấy thanh toán." });

            return Ok(payment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment detail for {PaymentId}", paymentId);
            return StatusCode(500, new { message = "Có lỗi xảy ra khi lấy chi tiết thanh toán." });
        }
    }

   // Trong file: Controllers/PaymentsController.cs

/// <summary>
/// ⭐ LUỒNG 4A: Staff xác nhận đã nhận tiền mặt cho một thanh toán đang chờ.
/// </summary>
[HttpPost("{paymentId:guid}/complete-cash")] // Đổi tên endpoint cho nhất quán
[Authorize(Roles = "Staff,Admin")]
[ProducesResponseType(typeof(CompleteCashPaymentResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<ActionResult<CompleteCashPaymentResponse>> CompleteCashPayment(Guid paymentId) // Bỏ tham số request body
{
    try
    {
        var staffId = GetCurrentUserId();
        
        // 1. Gọi đúng phương thức từ service
        var result = await _paymentService.CompleteCashPaymentAsync(paymentId, staffId);
        
        _logger.LogInformation("Staff {StaffId} completed CASH payment {PaymentId}", staffId, paymentId);
        
        // 2. Tạo đối tượng response thành công
        return Ok(new CompleteCashPaymentResponse { Success = true, PaymentId = result.Id, Status = result.Status.ToString() });
    }
    // 3. Bắt các exception cụ thể mà service ném ra
    catch (KeyNotFoundException ex)
    {
        _logger.LogWarning("Failed to complete cash payment. Payment not found: {PaymentId}. Message: {Message}", paymentId, ex.Message);
        return NotFound(new CompleteCashPaymentResponse { Success = false, Message = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        _logger.LogWarning("Failed to complete cash payment {PaymentId}: {Message}", paymentId, ex.Message);
        return BadRequest(new CompleteCashPaymentResponse { Success = false, Message = ex.Message });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error completing cash payment {PaymentId}", paymentId);
        return StatusCode(500, new CompleteCashPaymentResponse
        {
            Success = false,
            Message = "Có lỗi xảy ra khi xác nhận thanh toán."
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
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }
}