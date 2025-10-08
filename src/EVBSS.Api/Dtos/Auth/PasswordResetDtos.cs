using System.ComponentModel.DataAnnotations;
using EVBSS.Api.Validation;

namespace EVBSS.Api.Dtos.Auth;

/// <summary>
/// Request để yêu cầu reset mật khẩu
/// </summary>
public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    [StringLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự")]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Request để đặt lại mật khẩu mới
/// </summary>
public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Token là bắt buộc")]
    public string Token { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "UserId là bắt buộc")]
    public Guid UserId { get; set; }
    
    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
    [StrongPassword]
    public string NewPassword { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc")]
    [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// Response cho forgot password request
/// </summary>
public class ForgotPasswordResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Email được mask để bảo mật (ví dụ: k***@gmail.com)
    /// </summary>
    public string? MaskedEmail { get; set; }
}

/// <summary>
/// Response cho reset password
/// </summary>
public class ResetPasswordResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Request để validate reset token
/// </summary>
public class ValidateResetTokenRequest
{
    [Required(ErrorMessage = "UserId là bắt buộc")]
    public Guid UserId { get; set; }
    
    [Required(ErrorMessage = "Token là bắt buộc")]
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Response cho validate token
/// </summary>
public class ValidateResetTokenResponse
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? UserEmail { get; set; } // Masked email để hiển thị cho user
    public DateTime? ExpiresAt { get; set; }
}