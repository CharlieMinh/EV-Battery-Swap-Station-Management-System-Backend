namespace EVBSS.Api.Models;

public class SubscriptionPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;                    /// Tên gói (VD: "Gói Basic - 10 lần/tháng")
    public string Description { get; set; } = null!;             // Mô tả chi tiết gói
    public decimal MonthlyPrice { get; set; }                    /// Giá tháng (VD: 450,000 VND)
    public int? MaxSwapsPerMonth { get; set; }                   // VD: 10 lần (null = không giới hạn)
    
    // Chính sách hoàn tiền & Quyền lợi
    public string? RefundPolicy { get; set; }                    // "Hoàn tiền theo tỷ lệ ngày còn lại"
    public string? Benefits { get; set; }                        // "Tiết kiệm 10%, Ưu tiên đặt chỗ"
    
    // Battery compatibility
    public Guid BatteryModelId { get; set; }                     // Loại pin tương thích
    [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(BatteryModelId))]
    public BatteryModel BatteryModel { get; set; } = null!;
    
    // Plan settings
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation property: Một gói có nhiều subscription
    public ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
}