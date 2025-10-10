using EVBSS.Api.Data;
using EVBSS.Api.Models;
using EVBSS.Api.Dtos.Auth;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using BCrypt.Net;

namespace EVBSS.Api.Services;

/// <summary>
/// Service xử lý chức năng quên mật khẩu với OTP
/// </summary>
public class PasswordResetService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PasswordResetService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    
    // Salt riêng cho OTP hash (tăng bảo mật)
    private readonly string _otpSalt;
    private const int MaxOtpAttempts = 3; // Số lần nhập OTP tối đa
    
    public PasswordResetService(
        AppDbContext context,
        ILogger<PasswordResetService> logger,
        IConfiguration configuration,
        IEmailService emailService)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _emailService = emailService;
        _otpSalt = _configuration["Security:OtpSalt"] ?? "EVBSS_OTP_SALT_2024_V1";
    }

    /// <summary>
    /// Tạo và gửi OTP qua email
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

            if (user == null)
            {
                _logger.LogWarning("Password reset requested for non-existent email: {Email} from IP: {IP}", 
                    request.Email, ipAddress);
                
                return new ForgotPasswordResponse
                {
                    Success = false,
                    Message = $"Email '{request.Email}' không tồn tại trong hệ thống. Vui lòng kiểm tra lại email hoặc đăng ký tài khoản mới.",
                    MaskedEmail = MaskEmail(request.Email)
                };
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

            // 3. Vô hiệu hóa các OTP cũ chưa sử dụng
            await InvalidateExistingTokensAsync(user.Id);

            // 4. Tạo mã OTP 6 số ngẫu nhiên
            var otpCode = GenerateOtpCode();

            // 5. Hash OTP với salt và user info để bảo mật
            var otpData = $"{otpCode}:{_otpSalt}:{user.Email}:{user.Id}";
            var otpHash = BCrypt.Net.BCrypt.HashPassword(otpData, 12);

            // 6. Tạo và lưu password reset token
            var resetToken = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OtpHash = otpHash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false,
                AttemptCount = 0,
                RequestIpAddress = ipAddress,
                RequestUserAgent = userAgent
            };

            _context.PasswordResetTokens.Add(resetToken);
            await _context.SaveChangesAsync();

            // 7. Gửi OTP qua email
            var userName = user.Email.Split('@')[0];
            var emailSent = await _emailService.SendPasswordResetOtpAsync(user.Email, otpCode, userName);
            
            if (!emailSent)
            {
                _logger.LogError("Failed to send OTP email to user: {UserId}", user.Id);
                return new ForgotPasswordResponse
                {
                    Success = false,
                    Message = "Không thể gửi email OTP. Vui lòng thử lại sau.",
                    MaskedEmail = MaskEmail(user.Email)
                };
            }

            _logger.LogInformation("Password reset OTP created for user: {UserId} from IP: {IP}", 
                user.Id, ipAddress);
            
            return new ForgotPasswordResponse
            {
                Success = true,
                Message = $"Mã OTP đã được gửi đến email {MaskEmail(user.Email)}. Vui lòng kiểm tra hộp thư và nhập mã OTP.",
                MaskedEmail = MaskEmail(user.Email)
            };
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
    /// Xác thực mã OTP
    /// </summary>
    public async Task<VerifyOtpResponse> VerifyOtpAsync(VerifyOtpRequest request, string? ipAddress = null)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

            if (user == null)
            {
                return new VerifyOtpResponse
                {
                    Success = false,
                    Message = "Email không tồn tại trong hệ thống."
                };
            }

            var resetToken = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.Id && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (resetToken == null)
            {
                return new VerifyOtpResponse
                {
                    Success = false,
                    Message = "Không tìm thấy OTP hoặc OTP đã hết hạn. Vui lòng yêu cầu OTP mới."
                };
            }

            if (resetToken.AttemptCount >= MaxOtpAttempts)
            {
                resetToken.IsUsed = true;
                resetToken.UsedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return new VerifyOtpResponse
                {
                    Success = false,
                    Message = "OTP đã bị khóa do nhập sai quá nhiều lần. Vui lòng yêu cầu OTP mới."
                };
            }

            var otpData = $"{request.Otp}:{_otpSalt}:{user.Email}:{user.Id}";
            var isValidOtp = BCrypt.Net.BCrypt.Verify(otpData, resetToken.OtpHash);

            if (!isValidOtp)
            {
                resetToken.AttemptCount++;
                await _context.SaveChangesAsync();

                var remainingAttempts = MaxOtpAttempts - resetToken.AttemptCount;
                return new VerifyOtpResponse
                {
                    Success = false,
                    Message = remainingAttempts > 0 
                        ? $"Mã OTP không đúng. Bạn còn {remainingAttempts} lần thử."
                        : "Mã OTP không đúng. OTP sẽ bị khóa."
                };
            }

            return new VerifyOtpResponse
            {
                Success = true,
                Message = "Xác thực OTP thành công. Bạn có thể đặt lại mật khẩu."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying OTP for email: {Email} from IP: {IP}", 
                request.Email, ipAddress);
            
            return new VerifyOtpResponse
            {
                Success = false,
                Message = "Có lỗi xảy ra khi xác thực OTP. Vui lòng thử lại sau."
            };
        }
    }

    /// <summary>
    /// Đặt lại mật khẩu mới sau khi đã verify OTP
    /// </summary>
    public async Task<ResetPasswordResponse> ResetPasswordAsync(
        ResetPasswordRequest request, 
        string? ipAddress = null, 
        string? userAgent = null)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

            if (user == null)
            {
                return new ResetPasswordResponse
                {
                    Success = false,
                    Message = "Email không tồn tại trong hệ thống."
                };
            }

            var resetToken = await _context.PasswordResetTokens
                .Where(t => t.UserId == user.Id && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (resetToken == null)
            {
                return new ResetPasswordResponse
                {
                    Success = false,
                    Message = "Session đã hết hạn. Vui lòng yêu cầu OTP mới."
                };
            }

            var otpData = $"{request.Otp}:{_otpSalt}:{user.Email}:{user.Id}";
            var isValidOtp = BCrypt.Net.BCrypt.Verify(otpData, resetToken.OtpHash);

            if (!isValidOtp)
            {
                return new ResetPasswordResponse
                {
                    Success = false,
                    Message = "Mã OTP không hợp lệ."
                };
            }

            if (BCrypt.Net.BCrypt.Verify(request.NewPassword, user.PasswordHash))
            {
                return new ResetPasswordResponse
                {
                    Success = false,
                    Message = "Mật khẩu mới phải khác mật khẩu hiện tại."
                };
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 12);
            resetToken.IsUsed = true;
            resetToken.UsedAt = DateTime.UtcNow;

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
            _logger.LogError(ex, "Error resetting password for email: {Email} from IP: {IP}", 
                request.Email, ipAddress);
            
            return new ResetPasswordResponse
            {
                Success = false,
                Message = "Có lỗi xảy ra khi đặt lại mật khẩu. Vui lòng thử lại sau."
            };
        }
    }

    /// <summary>
    /// Tạo mã OTP 6 số ngẫu nhiên
    /// </summary>
    private static string GenerateOtpCode()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var number = Math.Abs(BitConverter.ToInt32(bytes, 0));
        return (number % 1000000).ToString("D6");
    }

    /// <summary>
    /// Vô hiệu hóa các OTP tokens cũ của user
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
    /// Mask email để bảo mật
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
    /// Cleanup expired tokens
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