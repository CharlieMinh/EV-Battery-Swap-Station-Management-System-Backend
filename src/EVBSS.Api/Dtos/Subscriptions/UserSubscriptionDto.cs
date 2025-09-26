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
    public int CurrentMonthKmUsed { get; set; }
    
    // Payment info
    public decimal DepositPaid { get; set; }
    public DateTime? DepositPaidDate { get; set; }
    public int ConsecutiveOverdueMonths { get; set; }
    
    // Status
    public bool IsBlocked { get; set; }
    public int ChargingLimitPercent { get; set; }
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
    public decimal MonthlyFeeUnder1500Km { get; set; }
    public decimal MonthlyFee1500To3000Km { get; set; }
    public decimal MonthlyFeeOver3000Km { get; set; }
    public decimal DepositAmount { get; set; }
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