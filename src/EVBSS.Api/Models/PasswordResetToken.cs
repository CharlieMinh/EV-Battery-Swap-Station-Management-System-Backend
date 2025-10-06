using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Models;

/// <summary>
/// Token để reset mật khẩu - lưu hash để bảo mật
/// </summary>
public class PasswordResetToken
{
    public Guid Id { get; set; }
    
    [Required]
    public Guid UserId { get; set; }
    
    /// <summary>
    /// Hash của token gốc (không lưu plain text)
    /// </summary>
    [Required]
    [StringLength(255)]
    public string TokenHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Thời gian tạo token
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Thời gian hết hạn token (2 giờ sau khi tạo)
    /// </summary>
    [Required]
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// Token đã được sử dụng chưa (one-time use)
    /// </summary>
    [Required]
    public bool IsUsed { get; set; } = false;
    
    /// <summary>
    /// Thời gian sử dụng token
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
    
    // Navigation property
    public User User { get; set; } = null!;
}