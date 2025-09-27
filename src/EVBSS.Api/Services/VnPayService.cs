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
            // 1. Validate invoice exists and belongs to user
            var invoice = await _context.Invoices
                .Include(i => i.UserSubscription)
                    .ThenInclude(us => us!.Vehicle)
                .Include(i => i.UserSubscription)
                    .ThenInclude(us => us!.SubscriptionPlan)
                .FirstOrDefaultAsync(i => i.Id == request.InvoiceId && i.UserId == userId);

            if (invoice == null)
            {
                return new VnPayPaymentResponse 
                { 
                    Success = false, 
                    Message = "Hóa đơn không tồn tại hoặc không thuộc về bạn." 
                };
            }

            if (invoice.Status == PaymentStatus.Completed)
            {
                return new VnPayPaymentResponse 
                { 
                    Success = false, 
                    Message = "Hóa đơn này đã được thanh toán." 
                };
            }

            // 2. Check if payment already exists for this invoice
            var existingPayment = await _context.Payments
                .FirstOrDefaultAsync(p => p.InvoiceId == request.InvoiceId && p.Status == PaymentStatus.Pending);

            if (existingPayment != null)
            {
                return new VnPayPaymentResponse 
                { 
                    Success = false, 
                    Message = "Đã có giao dịch thanh toán đang chờ xử lý cho hóa đơn này." 
                };
            }

            // 3. Create payment record
            var payment = new Payment
            {
                InvoiceId = request.InvoiceId,
                UserId = userId,
                Method = PaymentMethod.VNPay,
                Type = GetPaymentType(invoice.Type),
                Amount = invoice.RemainingAmount,
                Status = PaymentStatus.Pending,
                VnpTxnRef = GenerateTransactionReference(),
                PaymentReference = GenerateTransactionReference(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // 4. Generate VNPay payment URL
            var paymentUrl = GenerateVnPayUrl(payment, invoice, request.OrderInfo ?? GetDefaultOrderInfo(invoice), ipAddress);

            _logger.LogInformation("Created VNPay payment {PaymentId} for invoice {InvoiceId}, user {UserId}", 
                payment.Id, invoice.Id, userId);

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
            _logger.LogError(ex, "Error creating VNPay payment for user {UserId}, invoice {InvoiceId}", userId, request.InvoiceId);
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
                .Include(p => p.Invoice)
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

            // 5. Update payment record
            payment.VnpTransactionNo = callback.vnp_TransactionNo;
            payment.VnpResponseCode = callback.vnp_ResponseCode;
            payment.VnpSecureHash = callback.vnp_SecureHash;
            payment.ProcessedAt = DateTime.UtcNow;

            if (DateTime.TryParseExact(callback.vnp_PayDate, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var payDate))
            {
                payment.VnpPayDate = payDate;
            }

            if (isSuccess && amount == payment.Amount)
            {
                // Payment successful
                payment.Status = PaymentStatus.Completed;
                payment.CompletedAt = DateTime.UtcNow;

                // Update invoice
                payment.Invoice.PaidAmount += payment.Amount;
                payment.Invoice.Status = payment.Invoice.RemainingAmount <= 0 ? PaymentStatus.Completed : PaymentStatus.PartiallyPaid;
                if (payment.Invoice.Status == PaymentStatus.Completed)
                {
                    payment.Invoice.PaidDate = DateTime.UtcNow;
                }
                payment.Invoice.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation("Payment {PaymentId} completed successfully for amount {Amount}", payment.Id, amount);
            }
            else
            {
                // Payment failed
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = $"VNPay response: {callback.vnp_ResponseCode}";
                
                _logger.LogWarning("Payment {PaymentId} failed with response code {ResponseCode}", payment.Id, callback.vnp_ResponseCode);
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

    private string GenerateVnPayUrl(Payment payment, Invoice invoice, string orderInfo, string ipAddress)
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

    private PaymentType GetPaymentType(InvoiceType invoiceType)
    {
        return invoiceType switch
        {
            InvoiceType.SubscriptionMonthly => PaymentType.Subscription,
            InvoiceType.Deposit => PaymentType.Subscription,
            InvoiceType.SwapTransaction => PaymentType.PayPerSwap,
            InvoiceType.BatteryPurchase => PaymentType.BuyOutright,
            InvoiceType.TradeInCredit => PaymentType.TradeIn,
            _ => PaymentType.Subscription
        };
    }

    private string GetDefaultOrderInfo(Invoice invoice)
    {
        return invoice.Type switch
        {
            InvoiceType.SubscriptionMonthly => $"Thanh toán thuê pin tháng {invoice.BillingPeriodStart:MM/yyyy}",
            InvoiceType.Deposit => "Thanh toán tiền cọc thuê pin",
            InvoiceType.SwapTransaction => "Thanh toán phí đổi pin",
            InvoiceType.OverdueFee => "Thanh toán phí phạt trễ hạn",
            _ => $"Thanh toán hóa đơn {invoice.InvoiceNumber}"
        };
    }
}