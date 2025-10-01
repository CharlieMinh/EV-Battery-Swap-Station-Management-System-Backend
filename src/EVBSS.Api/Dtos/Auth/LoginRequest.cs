using System.ComponentModel.DataAnnotations;
using EVBSS.Api.Validation;

namespace EVBSS.Api.Dtos.Auth;

public class LoginRequest
{
    [Required, CustomEmail, StringLength(255)]
    public string Email { get; set; } = default!;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = default!;
}
