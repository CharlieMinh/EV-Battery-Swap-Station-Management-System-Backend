namespace EVBSS.Api.Dtos.Users;

/// <summary>
/// Detailed staff response with work statistics
/// </summary>
public class StaffDetailResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string? Name { get; set; }
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = default!;
    public string Status { get; set; } = default!; // "Active" or "Locked"
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }

    // Station assignment
    public Guid? StationId { get; set; }
    public string? StationName { get; set; }
    
    // Staff work statistics
    /// <summary>
    /// Total reservations verified by this staff
    /// </summary>
    public int TotalReservationsVerified { get; set; }
    
    /// <summary>
    /// Total swap transactions handled by this staff
    /// </summary>
    public int TotalSwapTransactions { get; set; }
    
    /// <summary>
    /// Reservations verified in the last 30 days
    /// </summary>
    public int RecentReservationsVerified { get; set; }
    
    /// <summary>
    /// Swap transactions handled in the last 30 days
    /// </summary>
    public int RecentSwapTransactions { get; set; }
}
