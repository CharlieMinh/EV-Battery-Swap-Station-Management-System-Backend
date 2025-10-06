using EVBSS.Api.Data;
using EVBSS.Api.Models;
using EVBSS.Api.Dtos.Auth;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;

namespace EVBSS.Api.Services;

/// <summary>
/// Service xử lý chức năng quên mật khẩu an toàn
/// </summary>
public class PasswordResetService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PasswordResetService> _logger;
    private readonly IConfiguration _configuration;
    
    // Salt riêng cho token hash (tăng bảo mật)
    private readonly string _tokenSalt;
    private readonly string _frontendUrl;
    
    public PasswordResetService(
        AppDbContext context,
        ILogger<PasswordResetService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _tokenSalt = _configuration["Security:TokenSalt"] ?? "EVBSS_TOKEN_SALT_2024_V1";
        _frontendUrl = _configuration["App:FrontendUrl"] ?? "http://localhost:3000";
    }

    /// <summary>
    /// Tạo yêu cầu reset mật khẩu
    /// </summary>
    public async Task<ForgotPasswordResponse> RequestPasswordResetAsync(
        ForgotPasswordRequest request, 
        string? ipAddress = null, 
        string? userAgent = null)
    {
        try
        {
            // 1. Tìm user theo email (case-insensitive)
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

            // Luôn trả về success message để tránh Email Enumeration Attack
            var response = new ForgotPasswordResponse
            {
                Success = true,
                Message = "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được link đặt lại mật khẩu trong vòng 5-10 phút.",
                MaskedEmail = MaskEmail(request.Email)
            };

            if (user == null)
            {
                _logger.LogWarning("Password reset requested for non-existent email: {Email} from IP: {IP}", 
                    request.Email, ipAddress);
                
                // Delay để giống như đang xử lý thật (tránh timing attack)
                await Task.Delay(Random.Shared.Next(1000, 3000));
                return response;
            }

            // 2. Rate limiting - chỉ cho phép 3 request/hour cho mỗi user
            var recentRequests = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.Id && t.CreatedAt > DateTime.UtcNow.AddHours(-1))
                .CountAsync();

            if (recentRequests >= 3)
            {
                _logger.LogWarning("Too many password reset requests for user: {UserId} from IP: {IP}", 
                    user.Id, ipAddress);
                
                return new ForgotPasswordResponse
                {
                    Success = false,
                    Message = "Bạn đã yêu cầu quá nhiều lần. Vui lòng thử lại sau 1 giờ.",
                    MaskedEmail = MaskEmail(user.Email)
                };
            }

            // 3. Vô hiệu hóa các token cũ chưa sử dụng
            await InvalidateExistingTokensAsync(user.Id);

            // 4. Tạo secure random token (256 bits)
            var tokenBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }
            
            // Convert to URL-safe Base64
            var token = Convert.ToBase64String(tokenBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");

            // 5. Hash token với salt và user info
            var tokenData = $"{token}:{_tokenSalt}:{user.Email}:{user.Id}";
            var tokenHash = BCrypt.Net.BCrypt.HashPassword(tokenData, 12); // Cost factor 12

            // 6. Tạo và lưu password reset token
            var resetToken = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = tokenHash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(2), // Hết hạn sau 2 giờ
                IsUsed = false,
                RequestIpAddress = ipAddress,
                RequestUserAgent = userAgent
            };

            _context.PasswordResetTokens.Add(resetToken);
            await _context.SaveChangesAsync();

            // 7. Gửi email reset password
            await SendPasswordResetEmailAsync(user, token);

            _logger.LogInformation("Password reset token created for user: {UserId} from IP: {IP}", 
                user.Id, ipAddress);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating password reset request for email: {Email} from IP: {IP}", 
                request.Email, ipAddress);
            
            return new ForgotPasswordResponse
            {
                Success = false,
                Message = "Có lỗi xảy ra trong hệ thống. Vui lòng thử lại sau."
            };
        }
    }

    /// <summary>
    /// Validate reset token
    /// </summary>
    public async Task<ValidateResetTokenResponse> ValidateResetTokenAsync(ValidateResetTokenRequest request)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.UserId);

            if (user == null)
            {
                return new ValidateResetTokenResponse
                {
                    IsValid = false,
                    Message = "Người dùng không tồn tại."
                };
            }

            // Tìm token hợp lệ
            var resetTokens = await _context.PasswordResetTokens
                .Where(t => t.UserId == request.UserId && 
                           !t.IsUsed && 
                           t.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            if (!resetTokens.Any())
            {
                return new ValidateResetTokenResponse
                {
                    IsValid = false,
                    Message = "Token không tồn tại hoặc đã hết hạn."
                };
            }

            // Verify token
            var tokenData = $"{request.Token}:{_tokenSalt}:{user.Email}:{user.Id}";
            var validToken = resetTokens.FirstOrDefault(t => 
                BCrypt.Net.BCrypt.Verify(tokenData, t.TokenHash));

            if (validToken == null)
            {
                _logger.LogWarning("Invalid reset token attempted for user: {UserId}", user.Id);
                return new ValidateResetTokenResponse
                {
                    IsValid = false,
                    Message = "Token không hợp lệ."
                };
            }

            return new ValidateResetTokenResponse
            {
                IsValid = true,
                Message = "Token hợp lệ.",
                UserEmail = MaskEmail(user.Email),
                ExpiresAt = validToken.ExpiresAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating reset token for user: {UserId}", request.UserId);
            return new ValidateResetTokenResponse
            {
                IsValid = false,
                Message = "Có lỗi xảy ra khi xác thực token."
            };
        }
    }

    /// <summary>
    /// Đặt lại mật khẩu mới
    /// </summary>
    public async Task<ResetPasswordResponse> ResetPasswordAsync(
        ResetPasswordRequest request, 
        string? ipAddress = null, 
        string? userAgent = null)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // 1. Tìm user
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.UserId);

            if (user == null)
            {
                return new ResetPasswordResponse
                {
                    Success = false,
                    Message = "Người dùng không tồn tại."
                };
            }

            // 2. Tìm token hợp lệ
            var resetTokens = await _context.PasswordResetTokens
                .Where(t => t.UserId == request.UserId && 
                           !t.IsUsed && 
                           t.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            if (!resetTokens.Any())
            {
                return new ResetPasswordResponse
                {
                    Success = false,
                    Message = "Token không tồn tại hoặc đã hết hạn."
                };
            }

            // 3. Verify token
            var tokenData = $"{request.Token}:{_tokenSalt}:{user.Email}:{user.Id}";
            var validToken = resetTokens.FirstOrDefault(t => 
                BCrypt.Net.BCrypt.Verify(tokenData, t.TokenHash));

            if (validToken == null)
            {
                _logger.LogWarning("Invalid reset token used for password reset. User: {UserId}, IP: {IP}", 
                    user.Id, ipAddress);
                
                return new ResetPasswordResponse
                {
                    Success = false,
                    Message = "Token không hợp lệ."
                };
            }

            // 4. Kiểm tra mật khẩu mới khác mật khẩu cũ
            if (BCrypt.Net.BCrypt.Verify(request.NewPassword, user.PasswordHash))
            {
                return new ResetPasswordResponse
                {
                    Success = false,
                    Message = "Mật khẩu mới phải khác mật khẩu hiện tại."
                };
            }

            // 5. Cập nhật mật khẩu mới
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 12);

            // 6. Đánh dấu token đã sử dụng
            validToken.IsUsed = true;
            validToken.UsedAt = DateTime.UtcNow;

            // 7. Vô hiệu hóa tất cả tokens còn lại của user
            await InvalidateExistingTokensAsync(user.Id);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Password reset successful for user: {UserId} from IP: {IP}", 
                user.Id, ipAddress);

            return new ResetPasswordResponse
            {
                Success = true,
                Message = "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập bằng mật khẩu mới."
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error resetting password for user: {UserId} from IP: {IP}", 
                request.UserId, ipAddress);
            
            return new ResetPasswordResponse
            {
                Success = false,
                Message = "Có lỗi xảy ra khi đặt lại mật khẩu. Vui lòng thử lại sau."
            };
        }
    }

    /// <summary>
    /// Vô hiệu hóa các token cũ của user
    /// </summary>
    private async Task InvalidateExistingTokensAsync(Guid userId)
    {
        var existingTokens = await _context.PasswordResetTokens
            .Where(t => t.UserId == userId && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in existingTokens)
        {
            token.IsUsed = true;
            token.UsedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Gửi email reset password
    /// </summary>
    private async Task SendPasswordResetEmailAsync(User user, string token)
    {
        var resetUrl = $"{_frontendUrl}/reset-password?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        
        // TODO: Implement proper email service (SendGrid, SMTP, etc.)
        _logger.LogInformation("🔐 Password reset email for {Email}:\n" +
                              "Reset URL: {ResetUrl}\n" +
                              "Token expires in 2 hours.", 
                              MaskEmail(user.Email), resetUrl);
        
        // Mock email sending
        await Task.CompletedTask;
    }

    /// <summary>
    /// Mask email để bảo mật (ví dụ: khai@gmail.com -> k***@gmail.com)
    /// </summary>
    private static string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            return "***@***.***";

        var parts = email.Split('@');
        var username = parts[0];
        var domain = parts[1];

        if (username.Length <= 1)
            return $"{username}***@{domain}";

        var maskedUsername = $"{username[0]}{new string('*', Math.Max(1, username.Length - 1))}";
        return $"{maskedUsername}@{domain}";
    }

    /// <summary>
    /// Cleanup expired tokens (có thể chạy background job)
    /// </summary>
    public async Task CleanupExpiredTokensAsync()
    {
        try
        {
            var expiredTokens = await _context.PasswordResetTokens
                .Where(t => t.ExpiresAt < DateTime.UtcNow || t.IsUsed)
                .ToListAsync();

            if (expiredTokens.Any())
            {
                _context.PasswordResetTokens.RemoveRange(expiredTokens);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Cleaned up {Count} expired password reset tokens", 
                    expiredTokens.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired password reset tokens");
        }
    }
}