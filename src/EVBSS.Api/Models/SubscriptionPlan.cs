namespace EVBSS.Api.Models;

public class SubscriptionPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;                    // "Gói Basic - 10 lần/tháng"
    public string Description { get; set; } = null!;             // Mô tả chi tiết gói
    
    // ✅ SIMPLIFIED PRICING - Giá cố định hàng tháng
    public decimal MonthlyPrice { get; set; }                    // VD: 450,000 VND (giá đã bao gồm tất cả)
    public int? MaxSwapsPerMonth { get; set; }                   // VD: 10 lần (null = không giới hạn)
    
    // ✅ NO DEPOSIT REQUIRED - Không cần cọc
    public bool RequiresDeposit { get; set; } = false;           // Luôn false
    public decimal DepositAmount { get; set; } = 0;              // Luôn 0
    
    // ✅ REFUND & BENEFITS
    public string? RefundPolicy { get; set; }                    // "Hoàn tiền theo tỷ lệ ngày còn lại"
    public string? Benefits { get; set; }                        // "Tiết kiệm 10%, Ưu tiên đặt chỗ"
    
    // Battery compatibility
    public Guid BatteryModelId { get; set; }                     // Loại pin tương thích
    public BatteryModel BatteryModel { get; set; } = null!;
    
    // Plan settings
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}