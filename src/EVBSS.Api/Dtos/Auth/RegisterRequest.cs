using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Auth;

public class RegisterRequest
{
    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = default!;

    [Required, StringLength(100, MinimumLength = 6,
        ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = default!;

    [StringLength(200)]
    public string? Name { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }
}
