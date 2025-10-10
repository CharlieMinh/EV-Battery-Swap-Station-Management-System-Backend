using System.ComponentModel.DataAnnotations;
using EVBSS.Api.Validation;

namespace EVBSS.Api.Dtos.Auth;

/// <summary>
/// Request để yêu cầu OTP reset mật khẩu
/// </summary>
public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    [StringLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự")]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Request để xác thực OTP
/// </summary>
public class VerifyOtpRequest
{
    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Mã OTP là bắt buộc")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có đúng 6 chữ số")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP phải là 6 chữ số")]
    public string Otp { get; set; } = string.Empty;
}

/// <summary>
/// Request để đặt lại mật khẩu mới với OTP đã xác thực
/// </summary>
public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Mã OTP là bắt buộc")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có đúng 6 chữ số")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP phải là 6 chữ số")]
    public string Otp { get; set; } = string.Empty;
    
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
    
    /// <summary>
    /// Thời gian hết hạn OTP
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Response cho verify OTP
/// </summary>
public class VerifyOtpResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Token tạm thời để reset password (có thể bỏ nếu dùng OTP trực tiếp)
    /// </summary>
    public string? TempToken { get; set; }
}

/// <summary>
/// Response cho reset password
/// </summary>
public class ResetPasswordResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}