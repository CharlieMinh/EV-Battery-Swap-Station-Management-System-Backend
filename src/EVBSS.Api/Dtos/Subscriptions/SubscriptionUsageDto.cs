namespace EVBSS.Api.Dtos.Subscriptions;

public class SubscriptionUsageDto
{
    public Guid SubscriptionId { get; set; }
    public string SubscriptionPlanName { get; set; } = null!;
    public string VehiclePlate { get; set; } = null!;
    
    // Current period usage
    public DateTime CurrentBillingPeriodStart { get; set; }
    public DateTime CurrentBillingPeriodEnd { get; set; }
    public int CurrentMonthKmUsed { get; set; }
    
    // Pricing tiers
    public decimal CurrentMonthFee { get; set; }
    public string UsageTier { get; set; } = null!; // "Under1500", "1500To3000", "Over3000"
    
    // Statistics
    public int TotalSwapTransactions { get; set; }
    public int TotalKmUsed { get; set; }
    public decimal TotalAmountPaid { get; set; }
    
    // Monthly breakdown for last 6 months
    public List<MonthlyUsageDto> MonthlyUsage { get; set; } = new();
}

public class MonthlyUsageDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = null!;
    public int KmUsed { get; set; }
    public int SwapCount { get; set; }
    public decimal MonthlyFee { get; set; }
    public string UsageTier { get; set; } = null!;
    public bool IsPaid { get; set; }
}