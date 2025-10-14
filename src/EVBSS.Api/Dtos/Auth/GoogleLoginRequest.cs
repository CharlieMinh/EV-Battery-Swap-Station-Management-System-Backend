using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Auth;

/// <summary>
/// Request DTO cho Google OAuth Login
/// </summary>
public class GoogleLoginRequest
{
    /// <summary>
    /// Google ID Token nhận được từ Google Sign-In client
    /// </summary>
    [Required(ErrorMessage = "Google ID Token is required")]
    public string IdToken { get; set; } = null!;
}
