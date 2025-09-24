namespace EVBSS.Api.Models;

public class SubscriptionPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;                    // "VF3 - Gói 1500km", "VF5 - Gói 3000km"
    public string Description { get; set; } = null!;             // Mô tả chi tiết gói
    
    // VinFast-based pricing structure
    public decimal MonthlyFeeUnder1500Km { get; set; }           // Phí < 1500km/tháng
    public decimal MonthlyFee1500To3000Km { get; set; }          // Phí 1500-3000km/tháng  
    public decimal MonthlyFeeOver3000Km { get; set; }            // Phí >= 3000km/tháng
    public decimal DepositAmount { get; set; }                   // Tiền cọc pin
    
    // Battery compatibility
    public Guid BatteryModelId { get; set; }                     // Loại pin tương thích
    public BatteryModel BatteryModel { get; set; } = null!;
    
    // Plan settings
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Business rules
    public int BillingCycleDay { get; set; } = 25;               // Ngày 25 hàng tháng chốt cước
    public decimal OverdueInterestRate { get; set; } = 0.10m;    // 10%/năm phí trễ hạn
    public int MaxOverdueMonths { get; set; } = 2;               // Tối đa 2 tháng nợ
}