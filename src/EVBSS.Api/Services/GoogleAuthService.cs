using Google.Apis.Auth;
using EVBSS.Api.Models;
using EVBSS.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Services;

/// <summary>
/// Service xử lý xác thực Google OAuth 2.0
/// </summary>
public class GoogleAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(AppDbContext db, IConfiguration config, ILogger<GoogleAuthService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Xác thực Google ID Token và trả về thông tin user
    /// </summary>
    /// <param name="idToken">Google ID Token từ client</param>
    /// <returns>User object nếu xác thực thành công</returns>
    /// <exception cref="InvalidJwtException">Nếu token không hợp lệ</exception>
    public async Task<GoogleJsonWebSignature.Payload> VerifyGoogleTokenAsync(string idToken)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _config["GoogleAuth:ClientId"] ?? throw new InvalidOperationException("Google ClientId not configured") }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return payload;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify Google token");
            throw;
        }
    }

    /// <summary>
    /// Tìm hoặc tạo user từ Google account
    /// </summary>
    /// <param name="payload">Google payload đã verify</param>
    /// <returns>User object</returns>
    public async Task<User> FindOrCreateUserAsync(GoogleJsonWebSignature.Payload payload)
    {
        var email = payload.Email.ToLower();
        
        // Tìm user theo email
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            // Tạo user mới nếu chưa tồn tại
            user = new User
            {
                Email = email,
                Name = payload.Name,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()), // Random password cho Google accounts
                Role = Role.Driver, // Default role
                AuthMethod = AuthMethod.Google,
                GoogleId = payload.Subject,
                ProfilePictureUrl = payload.Picture
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created new user from Google account: {Email}", email);
        }
        else
        {
            // Cập nhật Google ID nếu user đã tồn tại nhưng chưa có Google ID
            if (string.IsNullOrEmpty(user.GoogleId))
            {
                user.AuthMethod = AuthMethod.Google;
                user.GoogleId = payload.Subject;
                user.ProfilePictureUrl = payload.Picture;
            }

            // Cập nhật last login
            user.LastLogin = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation("User logged in with Google: {Email}", email);
        }

        return user;
    }
}
