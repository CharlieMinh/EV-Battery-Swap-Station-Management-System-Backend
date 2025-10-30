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

    public int CurrentMonthSwapCount { get; set; } = 0;          // Số lần đổi pin trong tháng
    
    public DateTime? LastPaymentDate { get; set; }               // Ngày thanh toán gói
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;                      // Người dùng
    [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(SubscriptionPlanId))]
    public SubscriptionPlan SubscriptionPlan { get; set; } = null!; // Gói đăng ký
    [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(VehicleId))]
    public Vehicle Vehicle { get; set; } = null!;                  // Xe được áp dụng gói
}