using System.ComponentModel.DataAnnotations;
using EVBSS.Api.Validation;

namespace EVBSS.Api.Dtos.Auth;

public class RegisterRequest
{
    [Required, CustomEmail, StringLength(255)]
    public string Email { get; set; } = default!;

    [Required, StrongPassword, StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = default!;

    [StringLength(200)]
    public string? Name { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }
}
