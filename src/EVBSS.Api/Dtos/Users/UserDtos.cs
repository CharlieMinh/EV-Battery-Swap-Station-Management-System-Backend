using EVBSS.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Users;

public record AdminUserDto(
    Guid Id,
    string Email,
    string? Name,
    string? Phone,
    Role Role,
    DateTime CreatedAt,
    DateTime? LastLogin,
    string? CurrentSubscriptionPlan,
    DateTime? SubscriptionEndDate,
    UserSubscriptionStatus? SubscriptionStatus
);

public class CreateUserRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = default!;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = default!;

    [StringLength(200)]
    public string? Name { get; set; }

    [Phone]
    public string? Phone { get; set; }

    public Role Role { get; set; } = Role.Driver;
}

public class UpdateUserRequest
{
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(200)]
    public string? Name { get; set; }

    [Phone]
    public string? Phone { get; set; }

    public Role? Role { get; set; }
}

public class AssignSubscriptionRequest
{
    [Required]
    public Guid SubscriptionPlanId { get; set; }

    public DateTime? StartDate { get; set; }
}