namespace EVBSS.Api.Models;

public class SubscriptionPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int SwapsPerDay { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum UserSubscriptionStatus
{
    Active = 0,
    Expired = 1,
    Cancelled = 2,
    Suspended = 3
}

public class UserSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid SubscriptionPlanId { get; set; }
    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public UserSubscriptionStatus Status { get; set; } = UserSubscriptionStatus.Active;
    public int SwapsUsed { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}