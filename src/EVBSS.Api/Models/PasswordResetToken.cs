using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Models;

/// <summary>
/// Model cho password reset với OTP
/// </summary>
public class PasswordResetToken
{
    public Guid Id { get; set; }
    
    [Required]
    public Guid UserId { get; set; }
    
    /// <summary>
    /// Hash của OTP code (6 chữ số)
    /// </summary>
    [Required]
    [StringLength(255)]
    public string OtpHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Thời gian tạo OTP
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Thời gian hết hạn OTP (10 phút sau khi tạo)
    /// </summary>
    [Required]
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// OTP đã được sử dụng chưa (one-time use)
    /// </summary>
    [Required]
    public bool IsUsed { get; set; } = false;
    
    /// <summary>
    /// Thời gian sử dụng OTP
    /// </summary>
    public DateTime? UsedAt { get; set; }
    
    /// <summary>
    /// IP address của người yêu cầu reset (để audit)
    /// </summary>
    [StringLength(45)] // IPv6 max length
    public string? RequestIpAddress { get; set; }
    
    /// <summary>
    /// User agent của browser (để audit)
    /// </summary>
    [StringLength(500)]
    public string? RequestUserAgent { get; set; }
    
    /// <summary>
    /// Số lần thử OTP sai (max 3 lần)
    /// </summary>
    public int AttemptCount { get; set; } = 0;
    
    /// <summary>
    /// Navigation property đến User
    /// </summary>
    public virtual User User { get; set; } = null!;
}