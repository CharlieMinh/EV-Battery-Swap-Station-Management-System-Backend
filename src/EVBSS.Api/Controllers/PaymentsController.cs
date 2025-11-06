using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Payments;
using EVBSS.Api.Models;
using EVBSS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    private readonly AppDbContext _db;

    public PaymentsController(
        IVnPayService vnPayService, 
        IPaymentService paymentService,
        ILogger<PaymentsController> logger,
        AppDbContext db)
    {
        _vnPayService = vnPayService;
        _paymentService = paymentService;
        _logger = logger;
        _db = db;
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
            
            // Redirect về FE theo cấu hình PaymentBackReturnUrl
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var backUrl = config["Vnpay:PaymentBackReturnUrl"];
            if (string.IsNullOrWhiteSpace(backUrl)) backUrl = "/payment-result";

            if (isValid && isSuccess)
            {
                return Redirect($"{backUrl}?status=success&ref={returnData.vnp_TxnRef}&amount={returnData.vnp_Amount}");
            }
            else
            {
                return Redirect($"{backUrl}?status=failure&ref={returnData.vnp_TxnRef}&code={returnData.vnp_ResponseCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing VNPay return");
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var backUrl = config["Vnpay:PaymentBackReturnUrl"] ?? "/payment-result";
            return Redirect($"{backUrl}?status=error");
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
/// ⭐ NEW: Lấy danh sách các thanh toán tiền mặt đang chờ xác nhận
/// Staff xem danh sách này trước khi xác nhận thanh toán
/// </summary>
[HttpGet("pending-cash")]
[Authorize(Roles = "Staff,Admin")]
[ProducesResponseType(typeof(List<CompleteCashPaymentResponse>), StatusCodes.Status200OK)]
public async Task<ActionResult<List<CompleteCashPaymentResponse>>> GetPendingCashPayments()
{
    try
    {
        // Query tất cả payment có Status = Pending VÀ Method = Cash
        var pendingPayments = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Pending && p.Method == PaymentMethod.Cash)
            .Include(p => p.User)
            .Include(p => p.UserSubscription)
                .ThenInclude(us => us!.SubscriptionPlan)
                    .ThenInclude(sp => sp.BatteryModel)
            .Include(p => p.UserSubscription)
                .ThenInclude(us => us!.Vehicle)
                    .ThenInclude(v => v!.VehicleModel)
            .Include(p => p.Reservation)
            .Include(p => p.Station)
            .OrderByDescending(p => p.CreatedAt) // Mới nhất lên đầu
            .ToListAsync();

        // Map sang response DTO
        var response = pendingPayments.Select(payment => new CompleteCashPaymentResponse
        {
            Success = true,
            PaymentId = payment.Id,
            Status = payment.Status.ToString(),
            Message = "Thanh toán đang chờ xác nhận",
            PaymentDetail = new PaymentDetailInfo
            {
                Amount = payment.Amount,
                Method = payment.Method.ToString(),
                Type = payment.Type.ToString(),
                CreatedAt = payment.CreatedAt,
                CompletedAt = payment.CompletedAt,
                Description = payment.Description,
                
                // Thông tin người thanh toán
                User = new UserInfo
                {
                    Id = payment.User.Id,
                    Name = payment.User.Name ?? "N/A",
                    Email = payment.User.Email,
                    PhoneNumber = payment.User.Phone
                },
                
                // Thông tin gói dịch vụ (nếu là Subscription)
                SubscriptionPlan = payment.UserSubscription?.SubscriptionPlan != null ? new SubscriptionPlanInfo
                {
                    Id = payment.UserSubscription.SubscriptionPlan.Id,
                    Name = payment.UserSubscription.SubscriptionPlan.Name,
                    MonthlyPrice = payment.UserSubscription.SubscriptionPlan.MonthlyPrice,
                    MaxSwapsPerMonth = payment.UserSubscription.SubscriptionPlan.MaxSwapsPerMonth ?? 0,
                    BatteryModelName = payment.UserSubscription.SubscriptionPlan.BatteryModel?.Name ?? "N/A"
                } : null,
                
                // Thông tin xe
                Vehicle = payment.UserSubscription?.Vehicle != null ? new VehicleInfo
                {
                    Id = payment.UserSubscription.Vehicle.Id,
                    Plate = payment.UserSubscription.Vehicle.Plate,
                    VIN = payment.UserSubscription.Vehicle.VIN,
                    VehicleModelName = payment.UserSubscription.Vehicle.VehicleModel?.Name
                } : null,
                
                // Thông tin đặt lịch (nếu là Pay-per-Swap)
                Reservation = payment.Reservation != null ? new ReservationInfo
                {
                    Id = payment.Reservation.Id,
                    SlotDate = payment.Reservation.SlotDate,
                    SlotStartTime = payment.Reservation.SlotStartTime,
                    SlotEndTime = payment.Reservation.SlotEndTime,
                    Status = payment.Reservation.Status.ToString()
                } : null,
                
                // ProcessedByStaff = null (chưa xử lý)
                ProcessedByStaff = null,
                
                // Thông tin trạm
                Station = payment.Station != null ? new StationInfo
                {
                    Id = payment.Station.Id,
                    Name = payment.Station.Name,
                    Address = payment.Station.Address
                } : null
            }
        }).ToList();

        _logger.LogInformation("Retrieved {Count} pending cash payments", response.Count);
        return Ok(response);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving pending cash payments");
        return StatusCode(500, new List<CompleteCashPaymentResponse>());
    }
}

/// <summary>
/// ⭐ LUỒNG 4A: Staff xác nhận đã nhận tiền mặt cho một thanh toán đang chờ.
/// Response bao gồm đầy đủ thông tin: người thanh toán, gói dịch vụ, xe, staff xử lý
/// </summary>
[HttpPost("{paymentId:guid}/complete-cash")]
[Authorize(Roles = "Staff,Admin")]
[ProducesResponseType(typeof(CompleteCashPaymentResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<ActionResult<CompleteCashPaymentResponse>> CompleteCashPayment(Guid paymentId)
{
    try
    {
        var staffId = GetCurrentUserId();
        
        // 1. Gọi service để complete payment
        var result = await _paymentService.CompleteCashPaymentAsync(paymentId, staffId);
        
        _logger.LogInformation("Staff {StaffId} completed CASH payment {PaymentId}", staffId, paymentId);
        
        // 2. Load đầy đủ thông tin liên quan để trả về
        var paymentDetail = await _db.Payments
            .Include(p => p.User)
            .Include(p => p.UserSubscription)
                .ThenInclude(us => us!.SubscriptionPlan)
                    .ThenInclude(sp => sp.BatteryModel)
            .Include(p => p.UserSubscription)
                .ThenInclude(us => us!.Vehicle)
                    .ThenInclude(v => v!.VehicleModel)
            .Include(p => p.Reservation)
            .Include(p => p.ProcessedByStaff)
            .Include(p => p.Station)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (paymentDetail == null)
        {
            return NotFound(new CompleteCashPaymentResponse { Success = false, Message = "Không tìm thấy thông tin thanh toán" });
        }

        // 3. Map sang response DTO với đầy đủ thông tin
        var response = new CompleteCashPaymentResponse
        {
            Success = true,
            PaymentId = paymentDetail.Id,
            Status = paymentDetail.Status.ToString(),
            Message = "Xác nhận thanh toán tiền mặt thành công",
            PaymentDetail = new PaymentDetailInfo
            {
                Amount = paymentDetail.Amount,
                Method = paymentDetail.Method.ToString(),
                Type = paymentDetail.Type.ToString(),
                CreatedAt = paymentDetail.CreatedAt,
                CompletedAt = paymentDetail.CompletedAt,
                Description = paymentDetail.Description,
                
                // Thông tin người thanh toán
                User = new UserInfo
                {
                    Id = paymentDetail.User.Id,
                    Name = paymentDetail.User.Name ?? "N/A",
                    Email = paymentDetail.User.Email,
                    PhoneNumber = paymentDetail.User.Phone
                },
                
                // Thông tin gói dịch vụ (nếu là Subscription)
                SubscriptionPlan = paymentDetail.UserSubscription?.SubscriptionPlan != null ? new SubscriptionPlanInfo
                {
                    Id = paymentDetail.UserSubscription.SubscriptionPlan.Id,
                    Name = paymentDetail.UserSubscription.SubscriptionPlan.Name,
                    MonthlyPrice = paymentDetail.UserSubscription.SubscriptionPlan.MonthlyPrice,
                    MaxSwapsPerMonth = paymentDetail.UserSubscription.SubscriptionPlan.MaxSwapsPerMonth ?? 0,
                    BatteryModelName = paymentDetail.UserSubscription.SubscriptionPlan.BatteryModel?.Name ?? "N/A"
                } : null,
                
                // Thông tin xe
                Vehicle = paymentDetail.UserSubscription?.Vehicle != null ? new VehicleInfo
                {
                    Id = paymentDetail.UserSubscription.Vehicle.Id,
                    Plate = paymentDetail.UserSubscription.Vehicle.Plate,
                    VIN = paymentDetail.UserSubscription.Vehicle.VIN,
                    VehicleModelName = paymentDetail.UserSubscription.Vehicle.VehicleModel?.Name
                } : null,
                
                // Thông tin đặt lịch (nếu là Pay-per-Swap)
                Reservation = paymentDetail.Reservation != null ? new ReservationInfo
                {
                    Id = paymentDetail.Reservation.Id,
                    SlotDate = paymentDetail.Reservation.SlotDate,
                    SlotStartTime = paymentDetail.Reservation.SlotStartTime,
                    SlotEndTime = paymentDetail.Reservation.SlotEndTime,
                    Status = paymentDetail.Reservation.Status.ToString()
                } : null,
                
                // Thông tin staff xử lý
                ProcessedByStaff = paymentDetail.ProcessedByStaff != null ? new StaffInfo
                {
                    Id = paymentDetail.ProcessedByStaff.Id,
                    Name = paymentDetail.ProcessedByStaff.Name ?? "N/A",
                    Email = paymentDetail.ProcessedByStaff.Email
                } : null,
                
                // Thông tin trạm
                Station = paymentDetail.Station != null ? new StationInfo
                {
                    Id = paymentDetail.Station.Id,
                    Name = paymentDetail.Station.Name,
                    Address = paymentDetail.Station.Address
                } : null
            }
        };
        
        return Ok(response);
    }
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