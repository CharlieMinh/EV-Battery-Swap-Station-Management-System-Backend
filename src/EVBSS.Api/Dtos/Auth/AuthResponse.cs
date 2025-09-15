namespace EVBSS.Api.Dtos.Auth;

public class AuthResponse
{
    public string Token { get; set; } = default!;
    public string Role { get; set; } = default!;
    public string? Name { get; set; }
}
