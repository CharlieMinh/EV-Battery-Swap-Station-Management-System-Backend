using EVBSS.Api.Configuration;
using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Payments;
using EVBSS.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace EVBSS.Api.Services;

public interface IPaymentService
{
    /// <summary>
    /// User chọn thanh toán bằng tiền mặt thay vì VNPay
    /// </summary>
    Task<SelectCashMethodResponse> SelectCashMethodAsync(Guid userId, Guid paymentId);
    
    /// <summary>
    /// Staff xác nhận đã nhận tiền mặt và kích hoạt subscription
    /// </summary>
    Task<ConfirmCashPaymentResponse> ConfirmCashPaymentAsync(Guid staffId, Guid paymentId, ConfirmCashPaymentRequest request);
    
    /// <summary>
    /// Tạo reservation + payment cho pay-per-swap (đặt lịch lẻ không cần subscription).
    /// Hỗ trợ cả 2 phương thức thanh toán: VNPay (online) và Cash (tại trạm).
    /// </summary>
    /// <param name="userId">ID của user đặt lịch</param>
    /// <param name="request">Thông tin reservation và payment</param>
    /// <param name="ipAddress">IP address của client (cần cho VNPay)</param>
    /// <returns>Response chứa paymentUrl (VNPay) hoặc QRCode (Cash)</returns>
    Task<CreatePayPerSwapReservationResponse> CreatePayPerSwapReservationAsync(Guid userId, CreatePayPerSwapReservationRequest request, string ipAddress);
}

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PaymentService> _logger;
    private readonly SlotReservationService _slotReservationService;
    private readonly VnPayConfig _vnPayConfig;

    public PaymentService(
        AppDbContext context, 
        ILogger<PaymentService> logger,
        SlotReservationService slotReservationService,
        IOptions<VnPayConfig> vnPayConfig)
    {
        _context = context;
        _logger = logger;
        _slotReservationService = slotReservationService;
        _vnPayConfig = vnPayConfig.Value;
    }

    public async Task<SelectCashMethodResponse> SelectCashMethodAsync(Guid userId, Guid paymentId)
    {
        try
        {
            // 1. Tìm payment theo paymentId
            var payment = await _context.Payments
                .Include(p => p.UserSubscription)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                return new SelectCashMethodResponse
                {
                    Success = false,
                    Message = "Không tìm thấy payment."
                };
            }

            // 2. Validate payment belongs to user
            if (payment.UserId != userId)
            {
                return new SelectCashMethodResponse
                {
                    Success = false,
                    Message = "Payment này không thuộc về bạn."
                };
            }

            // 3. Validate payment is Pending
            if (payment.Status != PaymentStatus.Pending)
            {
                return new SelectCashMethodResponse
                {
                    Success = false,
                    Message = $"Payment đã được xử lý ({payment.Status}). Không thể thay đổi phương thức thanh toán."
                };
            }

            // 4. Update payment method to Cash
            payment.Method = PaymentMethod.Cash;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment {PaymentId} switched to CASH method by user {UserId}", paymentId, userId);

            // 5. Return success with instructions
            return new SelectCashMethodResponse
            {
                Success = true,
                Message = "Đã chuyển sang phương thức thanh toán tiền mặt.",
                PaymentId = payment.Id,
                Amount = payment.Amount,
                Instructions = "Vui lòng đến bất kỳ trạm đổi pin nào để thanh toán tiền mặt và kích hoạt gói. " +
                              "Xuất trình mã thanh toán cho nhân viên trạm."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting cash method for payment {PaymentId}", paymentId);
            return new SelectCashMethodResponse
            {
                Success = false,
                Message = "Có lỗi xảy ra khi chuyển phương thức thanh toán."
            };
        }
    }

    public async Task<ConfirmCashPaymentResponse> ConfirmCashPaymentAsync(Guid staffId, Guid paymentId, ConfirmCashPaymentRequest request)
    {
        try
        {
            // 1. Tìm payment với Method=Cash, Status=Pending
            var payment = await _context.Payments
                .Include(p => p.UserSubscription)
                .Include(p => p.Reservation)  // ⭐ LUỒNG 2: Include Reservation for pay-per-swap
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                return new ConfirmCashPaymentResponse
                {
                    Success = false,
                    Message = "Không tìm thấy payment."
                };
            }

            if (payment.Method != PaymentMethod.Cash)
            {
                return new ConfirmCashPaymentResponse
                {
                    Success = false,
                    Message = "Payment này không phải là thanh toán tiền mặt."
                };
            }

            if (payment.Status != PaymentStatus.Pending)
            {
                return new ConfirmCashPaymentResponse
                {
                    Success = false,
                    Message = $"Payment đã được xử lý ({payment.Status})."
                };
            }

            // 2. Update payment status
            payment.Status = PaymentStatus.Completed;
            payment.CompletedAt = DateTime.UtcNow;
            payment.ProcessedByStaffId = staffId;
            payment.Description = payment.Description + (string.IsNullOrEmpty(request.Notes) 
                ? "" 
                : $" | Ghi chú: {request.Notes}");

            // 3. ⭐ Phân nhánh xử lý theo payment.Type
            if (payment.Type == PaymentType.Subscription && payment.UserSubscription != null)
            {
                // 🔹 LUỒNG 1: KÍCH HOẠT SUBSCRIPTION
                bool subscriptionActivated = false;
                Guid? subscriptionId = null;

                if (!payment.UserSubscription.IsActive)
                {
                    var now = DateTime.UtcNow;
                    
                    payment.UserSubscription.IsActive = true;
                    payment.UserSubscription.StartDate = now;
                    payment.UserSubscription.EndDate = now.AddDays(30);  // 30-day subscription
                    payment.UserSubscription.CurrentBillingPeriodStart = now;
                    payment.UserSubscription.CurrentBillingPeriodEnd = now.AddDays(30);
                    payment.UserSubscription.CurrentMonthSwapCount = 0;  // Reset counter
                    payment.UserSubscription.LastPaymentDate = now;
                    payment.UserSubscription.UpdatedAt = now;

                    subscriptionActivated = true;
                    subscriptionId = payment.UserSubscription.Id;

                    _logger.LogInformation(
                        "Subscription {SubscriptionId} ACTIVATED by cash payment. Staff: {StaffId}", 
                        subscriptionId, 
                        staffId
                    );
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Payment {PaymentId} confirmed as CASH by staff {StaffId}", paymentId, staffId);

                return new ConfirmCashPaymentResponse
                {
                    Success = true,
                    Message = subscriptionActivated 
                        ? "Xác nhận thanh toán tiền mặt thành công. Gói subscription đã được kích hoạt!" 
                        : "Xác nhận thanh toán tiền mặt thành công.",
                    PaymentId = payment.Id,
                    SubscriptionActivated = subscriptionActivated,
                    SubscriptionId = subscriptionId
                };
            }
            else if (payment.Type == PaymentType.PayPerSwap && payment.Reservation != null)
            {
                // 🔹 LUỒNG 2: PAY-PER-SWAP - Không activate gì, chỉ confirm payment
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Cash payment {PaymentId} confirmed for reservation {ReservationId} by staff {StaffId}. Customer can now check-in.",
                    paymentId, 
                    payment.Reservation.Id,
                    staffId
                );

                return new ConfirmCashPaymentResponse
                {
                    Success = true,
                    Message = "Xác nhận thanh toán thành công. Khách hàng có thể check-in tại trạm.",
                    PaymentId = payment.Id,
                    SubscriptionActivated = false,
                    ReservationId = payment.Reservation.Id  // ⭐ Return ReservationId for LUỒNG 2
                };
            }
            else
            {
                // Edge case: Payment không có subscription hoặc reservation
                await _context.SaveChangesAsync();

                _logger.LogWarning("Payment {PaymentId} confirmed but no subscription or reservation linked", paymentId);

                return new ConfirmCashPaymentResponse
                {
                    Success = true,
                    Message = "Xác nhận thanh toán tiền mặt thành công.",
                    PaymentId = payment.Id,
                    SubscriptionActivated = false
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming cash payment {PaymentId}", paymentId);
            return new ConfirmCashPaymentResponse
            {
                Success = false,
                Message = "Có lỗi xảy ra khi xác nhận thanh toán."
            };
        }
    }

    /// <summary>
    /// Tạo reservation + payment cho pay-per-swap (đặt lịch lẻ).
    /// Hỗ trợ cả VNPay (online) và Cash (tại trạm).
    /// </summary>
    public async Task<CreatePayPerSwapReservationResponse> CreatePayPerSwapReservationAsync(
        Guid userId, 
        CreatePayPerSwapReservationRequest request, 
        string ipAddress)
    {
        // BƯỚC 1: Bắt đầu transaction để đảm bảo atomicity
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // BƯỚC 2: Tạo Reservation thông qua SlotReservationService
            _logger.LogInformation(
                "Creating pay-per-swap reservation for user {UserId} at station {StationId} on {SlotDate} {SlotTime}",
                userId, request.StationId, request.SlotDate, request.SlotStartTime);

            Reservation reservation;
            try
            {
                reservation = await _slotReservationService.CreateReservationAsync(
                    userId,
                    request.StationId,
                    request.BatteryModelId,
                    request.SlotDate,
                    request.SlotStartTime,
                    request.SlotEndTime);
            }
            catch (ActiveReservationExistsException)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning("User {UserId} already has an active reservation", userId);
                
                return new CreatePayPerSwapReservationResponse
                {
                    Success = false,
                    Message = "Bạn đã có lịch đặt đang hoạt động. Vui lòng hoàn thành hoặc hủy lịch hiện tại trước khi đặt lịch mới."
                };
            }
            catch (SlotNotAvailableException)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(
                    "Slot not available at station {StationId} on {SlotDate} {SlotTime}",
                    request.StationId, request.SlotDate, request.SlotStartTime);
                
                return new CreatePayPerSwapReservationResponse
                {
                    Success = false,
                    Message = "Slot thời gian này đã đầy. Vui lòng chọn slot khác."
                };
            }

            _logger.LogInformation("Reservation {ReservationId} created successfully", reservation.Id);

            // BƯỚC 3: Tạo Payment record
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ReservationId = reservation.Id,
                UserSubscriptionId = null, // Pay-per-swap không liên quan đến subscription
                Method = request.PaymentMethod,
                Type = PaymentType.PayPerSwap,
                Amount = request.Amount,
                Status = PaymentStatus.Pending,
                Description = $"Thanh toán đặt lịch đổi pin - {request.SlotDate:dd/MM/yyyy} {request.SlotStartTime:hh\\:mm}-{request.SlotEndTime:hh\\:mm}",
                VnpTxnRef = GenerateTransactionReference(),
                PaymentReference = GenerateTransactionReference(),
                CreatedAt = DateTime.UtcNow,
                CompletedAt = null,
                ProcessedByStaffId = null
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Payment {PaymentId} created for reservation {ReservationId} with method {PaymentMethod}",
                payment.Id, reservation.Id, payment.Method);

            // BƯỚC 4: VNPay flow - Generate payment URL
            if (request.PaymentMethod == PaymentMethod.VNPay)
            {
                var paymentUrl = GenerateVnPayUrlForReservation(payment, reservation, ipAddress);
                
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "VNPay payment URL generated for reservation {ReservationId}, payment {PaymentId}",
                    reservation.Id, payment.Id);

                return new CreatePayPerSwapReservationResponse
                {
                    Success = true,
                    Message = "Đã tạo lịch hẹn thành công. Vui lòng thanh toán qua VNPay.",
                    PaymentUrl = paymentUrl,
                    ReservationId = reservation.Id,
                    PaymentId = payment.Id,
                    Amount = payment.Amount,
                    Status = "Pending"
                };
            }
            // BƯỚC 5: Cash flow - Return QR code and instructions
            else
            {
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Cash payment reservation created. Reservation {ReservationId}, payment {PaymentId}",
                    reservation.Id, payment.Id);

                return new CreatePayPerSwapReservationResponse
                {
                    Success = true,
                    Message = "Đã tạo lịch hẹn thành công. Vui lòng thanh toán tiền mặt tại trạm khi check-in.",
                    ReservationId = reservation.Id,
                    PaymentId = payment.Id,
                    QRCode = reservation.QRCode,
                    Status = "Pending",
                    Amount = payment.Amount,
                    Instructions = $"Vui lòng đến trạm đúng giờ hẹn ({reservation.SlotDate:dd/MM/yyyy} {reservation.SlotStartTime:hh\\:mm}-{reservation.SlotEndTime:hh\\:mm}) và xuất trình mã QR này để check-in. Thanh toán {payment.Amount:N0} VNĐ bằng tiền mặt tại quầy."
                };
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating pay-per-swap reservation for user {UserId}", userId);
            
            return new CreatePayPerSwapReservationResponse
            {
                Success = false,
                Message = "Có lỗi xảy ra khi tạo lịch đặt. Vui lòng thử lại."
            };
        }
    }

    /// <summary>
    /// Generate unique transaction reference code.
    /// Format: EVB + yyyyMMddHHmmss + 3 random digits
    /// Example: EVB20251023143000123
    /// </summary>
    private string GenerateTransactionReference()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var random = new Random().Next(100, 999); // 3 digits: 100-999
        return $"EVB{timestamp}{random}";
    }

    /// <summary>
    /// Generate VNPay payment URL for pay-per-swap reservation.
    /// Similar to VnPayService but for reservation payments.
    /// </summary>
    private string GenerateVnPayUrlForReservation(Payment payment, Reservation reservation, string ipAddress)
    {
        var orderInfo = $"Thanh toán đặt lịch đổi pin - {reservation.SlotDate:dd/MM/yyyy} {reservation.SlotStartTime:hh\\:mm}";
        
        var vnpParams = new Dictionary<string, string>
        {
            {"vnp_Version", "2.1.0"},
            {"vnp_Command", "pay"},
            {"vnp_TmnCode", _vnPayConfig.TmnCode},
            {"vnp_Amount", ((long)(payment.Amount * 100)).ToString()}, // Convert to cents
            {"vnp_CurrCode", "VND"},
            {"vnp_TxnRef", payment.VnpTxnRef!},
            {"vnp_OrderInfo", orderInfo},
            {"vnp_OrderType", "other"},
            {"vnp_Locale", "vn"},
            {"vnp_ReturnUrl", _vnPayConfig.ReturnUrl},
            {"vnp_IpnUrl", _vnPayConfig.IpnUrl},
            {"vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss")},
            {"vnp_IpAddr", ipAddress}
        };

        // Sort parameters and create query string
        var sortedParams = vnpParams.OrderBy(x => x.Key).ToList();
        var hashData = string.Join("&", sortedParams.Select(p => $"{p.Key}={p.Value}"));
        var vnpSecureHash = ComputeHmacSha512(_vnPayConfig.HashSecret, hashData);
        
        var queryString = string.Join("&", sortedParams.Select(p => $"{p.Key}={HttpUtility.UrlEncode(p.Value)}"));
        
        return $"{_vnPayConfig.BaseUrl}?{queryString}&vnp_SecureHash={vnpSecureHash}";
    }

    /// <summary>
    /// Compute HMAC-SHA512 hash for VNPay signature.
    /// </summary>
    private string ComputeHmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        
        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        
        return Convert.ToHexString(hashBytes).ToLower();
    }
}
