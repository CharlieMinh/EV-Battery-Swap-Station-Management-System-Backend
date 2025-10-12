namespace EVBSS.Api.Dtos.Users;

/// <summary>
/// Customer/Driver response with additional statistics
/// </summary>
public class CustomerResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string? Name { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
    
    // Statistics
    public int TotalReservations { get; set; }
    public int CompletedReservations { get; set; }
}
