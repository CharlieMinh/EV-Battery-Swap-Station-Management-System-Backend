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
    Task<SelectCashMethodResponse> SelectCashMethodAsync(Guid userId, Guid paymentId);
    
    Task<Payment> CompleteCashPaymentAsync(Guid paymentId, Guid staffId);

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
            var payment = await _context.Payments
                .Include(p => p.UserSubscription)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                return new SelectCashMethodResponse { Success = false, Message = "Không tìm thấy payment." };
            }

            if (payment.UserId != userId)
            {
                return new SelectCashMethodResponse { Success = false, Message = "Payment này không thuộc về bạn." };
            }

            if (payment.Status != PaymentStatus.Pending)
            {
                return new SelectCashMethodResponse { Success = false, Message = $"Payment đã được xử lý ({payment.Status}). Không thể thay đổi phương thức thanh toán." };
            }

            payment.Method = PaymentMethod.Cash;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment {PaymentId} switched to CASH method by user {UserId}", paymentId, userId);

            return new SelectCashMethodResponse
            {
                Success = true,
                Message = "Đã chuyển sang phương thức thanh toán tiền mặt.",
                PaymentId = payment.Id,
                Amount = payment.Amount,
                Instructions = "Vui lòng đến bất kỳ trạm đổi pin nào để thanh toán tiền mặt và kích hoạt gói. Xuất trình mã thanh toán cho nhân viên trạm."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting cash method for payment {PaymentId}", paymentId);
            return new SelectCashMethodResponse { Success = false, Message = "Có lỗi xảy ra khi chuyển phương thức thanh toán." };
        }
    }

    public async Task<Payment> CompleteCashPaymentAsync(Guid paymentId, Guid staffId)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null)
        {
            throw new KeyNotFoundException("Không tìm thấy thanh toán.");
        }

        if (payment.Method != PaymentMethod.Cash)
        {
            throw new InvalidOperationException("Đây không phải là thanh toán bằng tiền mặt.");
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            throw new InvalidOperationException($"Thanh toán đã ở trạng thái {payment.Status}, không thể xác nhận.");
        }

        // Update payment status
        payment.Status = PaymentStatus.Completed;
        payment.CompletedAt = DateTime.UtcNow;
        payment.ProcessedByStaffId = staffId;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Cash payment {PaymentId} for reservation {ReservationId} was completed by staff {StaffId}.",
            payment.Id, payment.ReservationId, staffId);

        return payment;
    }

    public async Task<CreatePayPerSwapReservationResponse> CreatePayPerSwapReservationAsync(
        Guid userId, 
        CreatePayPerSwapReservationRequest request, 
        string ipAddress)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
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
            catch (ActiveReservationExistsException ex)
            {
                await transaction.RollbackAsync();
                return new CreatePayPerSwapReservationResponse { Success = false, Message = ex.Message };
            }
            catch (SlotNotAvailableException ex)
            {
                await transaction.RollbackAsync();
                return new CreatePayPerSwapReservationResponse { Success = false, Message = ex.Message };
            }

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ReservationId = reservation.Id,
                UserSubscriptionId = null,
                Method = request.PaymentMethod,
                Type = PaymentType.PayPerSwap,
                Amount = request.Amount,
                Status = PaymentStatus.Pending,
                Description = $"Thanh toán đặt lịch đổi pin - {request.SlotDate:dd/MM/yyyy} {request.SlotStartTime:hh:mm}-{request.SlotEndTime:hh:mm}",
                VnpTxnRef = GenerateTransactionReference(),
                PaymentReference = GenerateTransactionReference(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            if (request.PaymentMethod == PaymentMethod.VNPay)
            {
                var paymentUrl = GenerateVnPayUrlForReservation(payment, reservation, ipAddress);
                await transaction.CommitAsync();
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
            else
            {
                await transaction.CommitAsync();
                return new CreatePayPerSwapReservationResponse
                {
                    Success = true,
                    Message = "Đã tạo lịch hẹn thành công. Vui lòng thanh toán tiền mặt tại trạm khi check-in.",
                    ReservationId = reservation.Id,
                    PaymentId = payment.Id,
                    QRCode = reservation.QRCode,
                    Status = "Pending",
                    Amount = payment.Amount,
                    Instructions = $"Vui lòng đến trạm đúng giờ hẹn ({reservation.SlotDate:dd/MM/yyyy} {reservation.SlotStartTime:hh:mm}-{reservation.SlotEndTime:hh:mm}) và xuất trình mã QR này để check-in. Thanh toán {payment.Amount:N0} VNĐ bằng tiền mặt tại quầy."
                };
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating pay-per-swap reservation for user {UserId}", userId);
            return new CreatePayPerSwapReservationResponse { Success = false, Message = "Có lỗi xảy ra khi tạo lịch đặt. Vui lòng thử lại." };
        }
    }

    private string GenerateTransactionReference()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var random = new Random().Next(100, 999);
        return $"EVB{timestamp}{random}";
    }

    private string GenerateVnPayUrlForReservation(Payment payment, Reservation reservation, string ipAddress)
    {
        var orderInfo = $"Thanh toán đặt lịch đổi pin - {reservation.SlotDate:dd/MM/yyyy} {reservation.SlotStartTime:hh:mm}";
        
        var vnpParams = new Dictionary<string, string>
        {
            {"vnp_Version", "2.1.0"},
            {"vnp_Command", "pay"},
            {"vnp_TmnCode", _vnPayConfig.TmnCode},
            {"vnp_Amount", ((long)(payment.Amount * 100)).ToString()},
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

        var sortedParams = vnpParams.OrderBy(x => x.Key).ToList();
        var hashData = string.Join("&", sortedParams.Select(p => $"{p.Key}={p.Value}"));
        var vnpSecureHash = ComputeHmacSha512(_vnPayConfig.HashSecret, hashData);
        
        var queryString = string.Join("&", sortedParams.Select(p => $"{p.Key}={HttpUtility.UrlEncode(p.Value)}"));
        
        return $"{_vnPayConfig.BaseUrl}?{queryString}&vnp_SecureHash={vnpSecureHash}";
    }

    private string ComputeHmacSha512(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        
        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        
        return Convert.ToHexString(hashBytes).ToLower();
    }
}