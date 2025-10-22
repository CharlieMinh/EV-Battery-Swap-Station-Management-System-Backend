using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Payments;
using EVBSS.Api.Models;
using Microsoft.EntityFrameworkCore;

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
}

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(AppDbContext context, ILogger<PaymentService> logger)
    {
        _context = context;
        _logger = logger;
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

            // 3. ⭐ KÍCH HOẠT SUBSCRIPTION (giống VNPay callback)
            bool subscriptionActivated = false;
            Guid? subscriptionId = null;

            if (payment.UserSubscription != null && !payment.UserSubscription.IsActive)
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
}
