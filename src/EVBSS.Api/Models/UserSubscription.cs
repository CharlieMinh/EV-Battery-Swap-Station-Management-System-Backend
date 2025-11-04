namespace EVBSS.Api.Models;

public class UserSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    public Guid? VehicleId { get; set; }                          // Xe được áp dụng gói (nullable theo mô hình mới)

    // Subscription details
    public DateTime StartDate { get; set; }                      // Ngày bắt đầu gói
    public DateTime? EndDate { get; set; }                       // Ngày kết thúc (null = vô thời hạn)
    public bool IsActive { get; set; } = true;

    // ✅ SIMPLIFIED: 30-day billing period (từ ngày đăng ký)
    public DateTime CurrentBillingPeriodStart { get; set; }      // VD: 2025-10-20
    public DateTime CurrentBillingPeriodEnd { get; set; }        // VD: 2025-11-19 (30 ngày)

    // ✅ SWAP COUNTER (thay vì tracking km)
    public int CurrentMonthSwapCount { get; set; } = 0;          // Số lần đổi pin trong tháng

    // Deposit & Payment tracking (không dùng cọc nữa)
    public decimal DepositPaid { get; set; } = 0;                // Luôn = 0
    public DateTime? DepositPaidDate { get; set; }               // Luôn = null
    public DateTime? LastPaymentDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
    [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(SubscriptionPlanId))]
    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;
    [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(VehicleId))]
    public Vehicle? Vehicle { get; set; }
}