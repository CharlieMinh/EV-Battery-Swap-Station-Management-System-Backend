namespace EVBSS.Api.Dtos.Subscriptions;

public class UserSubscriptionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SubscriptionPlanId { get; set; }
    // Một subscription có thể có nhiều xe
    public List<Guid> VehicleIds { get; set; } = new();
    
    // Subscription details
    public DateTime? StartDate { get; set; }  // NULL = chưa kích hoạt
    public DateTime? EndDate { get; set; }    // NULL = chưa kích hoạt hoặc vô thời hạn
    public bool IsActive { get; set; }
    public Guid VehicleId { get; set; } 
    public SubscriptionVehicleDto? Vehicle { get; set; }
    
    // Billing info
    public DateTime CurrentBillingPeriodStart { get; set; }
    public DateTime CurrentBillingPeriodEnd { get; set; }
    
    // ⭐ NEW: Check if subscription has expired
    public bool IsExpired => DateTime.UtcNow > CurrentBillingPeriodEnd;
    
    // ⭐ NEW: Days remaining until expiration (null if already expired)
    public int? DaysRemaining => IsExpired ? null : (int)(CurrentBillingPeriodEnd - DateTime.UtcNow).TotalDays;
    
    // ✅ SIMPLIFIED: Swap counter instead of km tracking
    public int CurrentMonthSwapCount { get; set; }
    
    // ⭐ LUỒNG 3: Computed properties for Frontend compatibility
    /// <summary>
    /// Alias for CurrentMonthSwapCount (Frontend expects "swapsUsed")
    /// </summary>
    public int SwapsUsed => CurrentMonthSwapCount;
    
    /// <summary>
    /// Swap limit from SubscriptionPlan (Frontend expects "swapsLimit" at root level)
    /// Will be populated after SubscriptionPlan is loaded
    /// </summary>
    public int? SwapsLimit => SubscriptionPlan?.MaxSwapsPerMonth;
    
    /// <summary>
    /// Remaining swaps in current billing period (null = unlimited)
    /// </summary>
    public int? SwapsRemaining => SwapsLimit.HasValue ? SwapsLimit.Value - SwapsUsed : null;
    
    // Payment info
    public DateTime? LastPaymentDate { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Related data
    public SubscriptionPlanDto SubscriptionPlan { get; set; } = null!;
    // Một subscription có thể có nhiều xe
    public List<SubscriptionVehicleDto> Vehicles { get; set; } = new();
}

public class SubscriptionPlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    
    // ✅ SIMPLIFIED PRICING
    public decimal MonthlyPrice { get; set; }
    public int? MaxSwapsPerMonth { get; set; }
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