namespace EVBSS.Api.Dtos.Subscriptions;

public class UserSubscriptionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    public Guid VehicleId { get; set; }
    
    // Subscription details
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
    
    // Billing info
    public DateTime CurrentBillingPeriodStart { get; set; }
    public DateTime CurrentBillingPeriodEnd { get; set; }
    
    // ✅ SIMPLIFIED: Swap counter instead of km tracking
    public int CurrentMonthSwapCount { get; set; }
    
    // Payment info
    public decimal DepositPaid { get; set; }
    public DateTime? DepositPaidDate { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Related data
    public SubscriptionPlanDto SubscriptionPlan { get; set; } = null!;
    public SubscriptionVehicleDto Vehicle { get; set; } = null!;
}

public class SubscriptionPlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    
    // ✅ SIMPLIFIED PRICING
    public decimal MonthlyPrice { get; set; }
    public int? MaxSwapsPerMonth { get; set; }
    public bool RequiresDeposit { get; set; }
    public decimal DepositAmount { get; set; }
    public string? Benefits { get; set; }
    public string? RefundPolicy { get; set; }
    
    public Guid BatteryModelId { get; set; }
    public string BatteryModelName { get; set; } = null!;
    public bool IsActive { get; set; }
}

public class SubscriptionVehicleDto
{
    public Guid Id { get; set; }
    public string Brand { get; set; } = null!;
    public string Model { get; set; } = null!;
    public string VIN { get; set; } = null!;
    public string Plate { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int Year { get; set; }
}