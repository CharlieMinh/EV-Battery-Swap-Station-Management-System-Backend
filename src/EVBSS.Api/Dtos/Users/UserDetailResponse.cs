namespace EVBSS.Api.Dtos.Users;

using EVBSS.Api.Dtos.Vehicles;
using EVBSS.Api.Dtos.Subscriptions;

/// <summary>
/// Detailed user response with full statistics
/// </summary>
public class UserDetailResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string? Name { get; set; }
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = default!;
    public string Status { get; set; } = default!; // "Active" or "Locked"
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
    
    // Statistics
    public int TotalReservations { get; set; }
    public int CompletedReservations { get; set; }
    public int CancelledReservations { get; set; }
    public int TotalVehicles { get; set; }
    
    // ⭐ NEW: Detailed vehicles list (nullable - only populated if requested)
    public List<VehicleDto>? Vehicles { get; set; }
    
    // ⭐ NEW: Subscriptions history (nullable - only populated if requested)
    public List<UserSubscriptionDto>? Subscriptions { get; set; }
}
