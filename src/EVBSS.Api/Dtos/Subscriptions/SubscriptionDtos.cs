using EVBSS.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Subscriptions;

public record SubscriptionPlanDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int DurationDays,
    int SwapsPerDay,
    bool IsActive,
    DateTime CreatedAt
);

public class CreateSubscriptionPlanRequest
{
    [Required, StringLength(200)]
    public string Name { get; set; } = default!;

    [Required, StringLength(500)]
    public string Description { get; set; } = default!;

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    [Range(1, 365)]
    public int DurationDays { get; set; }

    [Range(1, 100)]
    public int SwapsPerDay { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateSubscriptionPlanRequest
{
    [StringLength(200)]
    public string? Name { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Price { get; set; }

    [Range(1, 365)]
    public int? DurationDays { get; set; }

    [Range(1, 100)]
    public int? SwapsPerDay { get; set; }

    public bool? IsActive { get; set; }
}

public record UserSubscriptionDto(
    Guid Id,
    Guid UserId,
    string UserEmail,
    string? UserName,
    SubscriptionPlanDto SubscriptionPlan,
    DateTime StartDate,
    DateTime EndDate,
    UserSubscriptionStatus Status,
    int SwapsUsed,
    DateTime CreatedAt
);