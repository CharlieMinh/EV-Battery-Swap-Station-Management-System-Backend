namespace EVBSS.Api.Dtos.Users;

/// <summary>
/// Basic user response
/// </summary>
public class UserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string? Name { get; set; }
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
}
