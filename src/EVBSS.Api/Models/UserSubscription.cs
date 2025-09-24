namespace EVBSS.Api.Models;

public class UserSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    public Guid VehicleId { get; set; }                          // Xe được áp dụng gói

    // Subscription details
    public DateTime StartDate { get; set; }                      // Ngày bắt đầu gói
    public DateTime? EndDate { get; set; }                       // Ngày kết thúc (null = vô thời hạn)
    public bool IsActive { get; set; } = true;

    // VinFast billing cycle (26th to 25th)
    public DateTime CurrentBillingPeriodStart { get; set; }      // 26/tháng trước
    public DateTime CurrentBillingPeriodEnd { get; set; }        // 25/tháng hiện tại
    public int CurrentMonthKmUsed { get; set; } = 0;             // Km đã sử dụng trong tháng
    
    // Deposit & Payment tracking
    public decimal DepositPaid { get; set; } = 0;                // Tiền cọc đã đóng
    public DateTime? DepositPaidDate { get; set; }
    public int ConsecutiveOverdueMonths { get; set; } = 0;       // Số tháng nợ liên tiếp
    
    // Status & restrictions (VinFast penalty system)
    public bool IsBlocked { get; set; } = false;                 // Có bị chặn không?
    public int ChargingLimitPercent { get; set; } = 100;         // Giới hạn sạc (100%, 50%, 30%)
    public DateTime? LastPaymentDate { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}