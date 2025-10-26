namespace EVBSS.Api.Dtos.Auth;

public class MeResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string? Name { get; set; }
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = default!;
    public Guid? StationId { get; set; }  // ⭐ Staff's assigned station
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
}
