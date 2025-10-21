namespace EVBSS.Api.Dtos.Subscriptions;

public class SubscriptionUsageDto
{
    public Guid SubscriptionId { get; set; }
    public string SubscriptionPlanName { get; set; } = null!;
    public string VehiclePlate { get; set; } = null!;
    
    // Current period usage
    public DateTime CurrentBillingPeriodStart { get; set; }
    public DateTime CurrentBillingPeriodEnd { get; set; }
    
    // ⭐ NEW: Expiration info
    public bool IsExpired => DateTime.UtcNow > CurrentBillingPeriodEnd;
    public int? DaysRemaining => IsExpired ? null : (int)(CurrentBillingPeriodEnd - DateTime.UtcNow).TotalDays;
    
    // ✅ SIMPLIFIED: Swap count instead of km
    public int CurrentMonthSwapCount { get; set; }
    public int? MaxSwapsPerMonth { get; set; }
    
    // Pricing (fixed)
    public decimal CurrentMonthFee { get; set; }
    public string UsageTier { get; set; } = null!; // Now shows: "5/10 lần" or "12 lần (không giới hạn)"
    
    // Statistics
    public int TotalSwapTransactions { get; set; }
    public decimal TotalAmountPaid { get; set; }
    
    // Monthly breakdown for last 6 months
    public List<MonthlyUsageDto> MonthlyUsage { get; set; } = new();
}

public class MonthlyUsageDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = null!;
    
    // ✅ SIMPLIFIED: Only swap count matters
    public int SwapCount { get; set; }
    public decimal MonthlyFee { get; set; }
    public string UsageTier { get; set; } = null!; // e.g., "15/20 lần" or "25 lần (không giới hạn)"
    public bool IsPaid { get; set; }
}