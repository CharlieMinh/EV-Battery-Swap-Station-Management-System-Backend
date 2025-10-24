using EVBSS.Api.Configuration;
using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Payments;
using EVBSS.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace EVBSS.Api.Services;

public interface IVnPayService
{
    Task<VnPayPaymentResponse> CreatePaymentAsync(Guid userId, CreateVnPayPaymentRequest request, string ipAddress);
    Task<VnPayCallbackResponse> ProcessCallbackAsync(VnPayCallbackRequest callback);
    bool ValidateCallback(VnPayCallbackRequest callback);
}

public class VnPayService : IVnPayService
{
    private readonly AppDbContext _context;
    private readonly VnPayConfig _config;
    private readonly ILogger<VnPayService> _logger;

    public VnPayService(AppDbContext context, IOptions<VnPayConfig> config, ILogger<VnPayService> logger)
    {
        _context = context;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<VnPayPaymentResponse> CreatePaymentAsync(Guid userId, CreateVnPayPaymentRequest request, string ipAddress)
    {
        try
        {
            // ✅ REFACTORED: Payment for subscription directly (no invoice)
            // 1. Validate subscription exists and belongs to user
            var subscription = await _context.UserSubscriptions
                .Include(us => us.Vehicle)
                .Include(us => us.SubscriptionPlan)
                .FirstOrDefaultAsync(us => us.Id == request.SubscriptionId && us.UserId == userId);

            if (subscription == null)
            {
                return new VnPayPaymentResponse 
                { 
                    Success = false, 
                    Message = "Gói dịch vụ không tồn tại hoặc không thuộc về bạn." 
                };
            }

            // 2. Check if payment already exists for this billing period
            var existingPayment = await _context.Payments
                .FirstOrDefaultAsync(p => p.UserSubscriptionId == request.SubscriptionId 
                    && p.Status == PaymentStatus.Pending
                    && p.CreatedAt >= subscription.CurrentBillingPeriodStart);

            if (existingPayment != null)
            {
                return new VnPayPaymentResponse 
                { 
                    Success = false, 
                    Message = "Đã có giao dịch thanh toán đang chờ xử lý cho chu kỳ này." 
                };
            }

            // 3. Create payment record
            var payment = new Payment
            {
                UserSubscriptionId = request.SubscriptionId,
                UserId = userId,
                Method = PaymentMethod.VNPay,
                Type = PaymentType.Subscription,
                Amount = subscription.SubscriptionPlan.MonthlyPrice,
                Status = PaymentStatus.Pending,
                VnpTxnRef = GenerateTransactionReference(),
                PaymentReference = GenerateTransactionReference(),
                Description = $"Thanh toán {subscription.SubscriptionPlan.Name} - {subscription.CurrentBillingPeriodStart:dd/MM/yyyy}",
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // 4. Generate VNPay payment URL
            var orderInfo = request.OrderInfo ?? $"{subscription.SubscriptionPlan.Name} - {subscription.Vehicle.Plate}";
            var paymentUrl = GenerateVnPayUrl(payment, subscription, orderInfo, ipAddress);

            _logger.LogInformation("Created VNPay payment {PaymentId} for subscription {SubscriptionId}, user {UserId}", 
                payment.Id, subscription.Id, userId);

            return new VnPayPaymentResponse
            {
                Success = true,
                PaymentUrl = paymentUrl,
                PaymentReference = payment.PaymentReference,
                PaymentId = payment.Id,
                Message = "Tạo link thanh toán thành công."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating VNPay payment for user {UserId}, subscription {SubscriptionId}", userId, request.SubscriptionId);
            return new VnPayPaymentResponse 
            { 
                Success = false, 
                Message = "Có lỗi xảy ra khi tạo thanh toán." 
            };
        }
    }

    public async Task<VnPayCallbackResponse> ProcessCallbackAsync(VnPayCallbackRequest callback)
    {
        try
        {
            _logger.LogInformation("Processing VNPay callback for TxnRef: {TxnRef}", callback.vnp_TxnRef);

            // 1. Validate callback signature
            if (!ValidateCallback(callback))
            {
                _logger.LogWarning("Invalid VNPay callback signature for TxnRef: {TxnRef}", callback.vnp_TxnRef);
                return new VnPayCallbackResponse 
                { 
                    RspCode = "97", 
                    Message = "Invalid signature" 
                };
            }

            // 2. Find payment by TxnRef
            var payment = await _context.Payments
                .Include(p => p.UserSubscription)
                .Include(p => p.Reservation)  // ⭐ LUỒNG 2: Include Reservation for pay-per-swap
                .FirstOrDefaultAsync(p => p.VnpTxnRef == callback.vnp_TxnRef);

            if (payment == null)
            {
                _logger.LogWarning("Payment not found for TxnRef: {TxnRef}", callback.vnp_TxnRef);
                return new VnPayCallbackResponse 
                { 
                    RspCode = "01", 
                    Message = "Order not found" 
                };
            }

            // 3. Check if already processed
            if (payment.Status != PaymentStatus.Pending)
            {
                _logger.LogInformation("Payment {PaymentId} already processed with status {Status}", payment.Id, payment.Status);
                return new VnPayCallbackResponse(); // Success - already processed
            }

            // 4. Parse payment result
            var isSuccess = callback.vnp_ResponseCode == "00" && callback.vnp_TransactionStatus == "00";
            var amount = decimal.Parse(callback.vnp_Amount) / 100; // VNPay sends amount in cents

            // 5. Update payment record with VNPay response
            payment.VnpTransactionNo = callback.vnp_TransactionNo;
            payment.VnpResponseCode = callback.vnp_ResponseCode;
            payment.VnpSecureHash = callback.vnp_SecureHash;

            if (isSuccess && amount == payment.Amount)
            {
                // ✅ Payment successful
                payment.Status = PaymentStatus.Completed;
                payment.CompletedAt = DateTime.UtcNow;

                // ⭐ Phân nhánh xử lý theo payment.Type
                if (payment.Type == PaymentType.Subscription && payment.UserSubscription != null)
                {
                    // 🔹 LUỒNG 1: KÍCH HOẠT SUBSCRIPTION (nếu chưa active)
                    // Kịch bản 1: Subscription MỚI (pending) → Kích hoạt lần đầu
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

                        _logger.LogInformation(
                            "Subscription {SubscriptionId} ACTIVATED for user {UserId}. Valid from {Start} to {End}", 
                            payment.UserSubscription.Id, 
                            payment.UserSubscription.UserId,
                            now.ToString("yyyy-MM-dd HH:mm:ss"),
                            now.AddDays(30).ToString("yyyy-MM-dd HH:mm:ss")
                        );
                    }
                    // Kịch bản 2: Subscription RENEWAL (đã active) → Chỉ update payment date
                    else
                    {
                        payment.UserSubscription.LastPaymentDate = DateTime.UtcNow;
                        payment.UserSubscription.UpdatedAt = DateTime.UtcNow;

                        _logger.LogInformation(
                            "Subscription {SubscriptionId} payment RENEWED for user {UserId}", 
                            payment.UserSubscription.Id, 
                            payment.UserSubscription.UserId
                        );
                    }
                }
                else if (payment.Type == PaymentType.PayPerSwap && payment.Reservation != null)
                {
                    // 🔹 LUỒNG 2: PAY-PER-SWAP - Chỉ log, không update reservation status
                    // Reservation status sẽ được update khi user check-in tại trạm
                    _logger.LogInformation(
                        "Pay-per-swap payment {PaymentId} completed for reservation {ReservationId}. User {UserId} can now check-in at station.",
                        payment.Id, 
                        payment.Reservation.Id,
                        payment.UserId
                    );
                }

                _logger.LogInformation("Payment {PaymentId} completed successfully for amount {Amount}", payment.Id, amount);
            }
            else
            {
                // ❌ Payment failed - reason stored in VnpResponseCode
                // 24 = User cancelled, 51 = Insufficient balance, etc.
                payment.Status = PaymentStatus.Failed;
                payment.CompletedAt = DateTime.UtcNow; // Mark when it failed
                
                _logger.LogWarning("Payment {PaymentId} failed with VNPay response code {ResponseCode}", 
                    payment.Id, callback.vnp_ResponseCode);
            }

            await _context.SaveChangesAsync();

            return new VnPayCallbackResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing VNPay callback for TxnRef: {TxnRef}", callback.vnp_TxnRef);
            return new VnPayCallbackResponse 
            { 
                RspCode = "99", 
                Message = "Unknown error" 
            };
        }
    }

    public bool ValidateCallback(VnPayCallbackRequest callback)
    {
        try
        {
            // Create parameter dictionary (excluding hash)
            var vnpParams = new Dictionary<string, string>
            {
                {"vnp_Amount", callback.vnp_Amount},
                {"vnp_BankCode", callback.vnp_BankCode},
                {"vnp_BankTranNo", callback.vnp_BankTranNo},
                {"vnp_CardType", callback.vnp_CardType},
                {"vnp_OrderInfo", callback.vnp_OrderInfo},
                {"vnp_PayDate", callback.vnp_PayDate},
                {"vnp_ResponseCode", callback.vnp_ResponseCode},
                {"vnp_TmnCode", callback.vnp_TmnCode},
                {"vnp_TransactionNo", callback.vnp_TransactionNo},
                {"vnp_TransactionStatus", callback.vnp_TransactionStatus},
                {"vnp_TxnRef", callback.vnp_TxnRef}
            };

            // Sort parameters and create hash data
            var sortedParams = vnpParams.OrderBy(x => x.Key).ToList();
            var hashData = string.Join("&", sortedParams.Select(p => $"{p.Key}={p.Value}"));

            // Generate hash
            var computedHash = ComputeHmacSha512(_config.HashSecret, hashData);

            return computedHash.Equals(callback.vnp_SecureHash, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating VNPay callback signature");
            return false;
        }
    }

    private string GenerateVnPayUrl(Payment payment, UserSubscription subscription, string orderInfo, string ipAddress)
    {
        var vnpParams = new Dictionary<string, string>
        {
            {"vnp_Version", _config.Version},
            {"vnp_Command", _config.Command},
            {"vnp_TmnCode", _config.TmnCode},
            {"vnp_Amount", ((long)(payment.Amount * 100)).ToString()}, // Convert to cents
            {"vnp_CurrCode", _config.CurrCode},
            {"vnp_TxnRef", payment.VnpTxnRef!},
            {"vnp_OrderInfo", orderInfo},
            {"vnp_OrderType", "other"},
            {"vnp_Locale", _config.Locale},
            {"vnp_ReturnUrl", _config.ReturnUrl},
            {"vnp_IpnUrl", _config.IpnUrl},
            {"vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss")},
            {"vnp_IpAddr", ipAddress}
        };

        // Sort parameters and create query string
        var sortedParams = vnpParams.OrderBy(x => x.Key).ToList();
        var hashData = string.Join("&", sortedParams.Select(p => $"{p.Key}={p.Value}"));
        var vnpSecureHash = ComputeHmacSha512(_config.HashSecret, hashData);
        
        var queryString = string.Join("&", sortedParams.Select(p => $"{p.Key}={HttpUtility.UrlEncode(p.Value)}"));
        
        return $"{_config.BaseUrl}?{queryString}&vnp_SecureHash={vnpSecureHash}";
    }

    private string ComputeHmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        
        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        
        return Convert.ToHexString(hashBytes).ToLower();
    }

    private string GenerateTransactionReference()
    {
        return $"EVB{DateTime.Now:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
    }

   
}