using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Auth;
using EVBSS.Api.Models;
using EVBSS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;




namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly PasswordResetService _passwordResetService;

    public AuthController(AppDbContext db, IConfiguration cfg, PasswordResetService passwordResetService)
    {
        _db = db; 
        _cfg = cfg;
        _passwordResetService = passwordResetService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        // Validate input according to data annotations
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value!.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key.ToLower(),
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );
            return BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "Invalid input data.", details = errors } });
        }

        var email = req.Email.Trim().ToLower();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { error = new { code = "EMAIL_EXISTS", message = "Email already registered." } });

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Name = req.Name,
            Phone = req.Phone,
            Role = Role.Driver
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Me), null, new { user.Id, user.Email, role = user.Role.ToString() });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [Produces("application/json")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        // Validate input according to data annotations
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value!.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key.ToLower(),
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );
            return BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "Invalid input data.", details = errors } });
        }

        var email = req.Email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = new { code = "INVALID_CREDENTIALS", message = "Invalid email or password." } });

        var token = GenerateJwt(user);
        user.LastLogin = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        //  Set JWT token vào HTTP-only Cookie
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,        
            Secure = Request.IsHttps,  
            SameSite = SameSiteMode.Lax,  
            Expires = DateTimeOffset.UtcNow.AddMinutes(int.Parse(_cfg["Jwt:ExpiresMinutes"] ?? "120"))
        };
        Response.Cookies.Append("jwt", token, cookieOptions);

        return Ok(new AuthResponse { Token = token, Role = user.Role.ToString(), Name = user.Name });
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Logout()
    {
        //  Xóa JWT cookie 
        Response.Cookies.Delete("jwt", new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps, 
            SameSite = SameSiteMode.Lax  
        });

        return Ok(new { message = "Logged out successfully" });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(sub, out var userId)) return Unauthorized();

        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        if (u is null) return Unauthorized();

        return new MeResponse { Id = u.Id, Email = u.Email, Name = u.Name, Role = u.Role.ToString(), CreatedAt = u.CreatedAt, LastLogin = u.LastLogin };
    }

    private string GenerateJwt(User user)
    {
        var jwt = _cfg.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        var expires = DateTime.UtcNow.AddMinutes(int.Parse(jwt["ExpiresMinutes"] ?? "120"));
        var token = new JwtSecurityToken(jwt["Issuer"], jwt["Audience"], claims, expires: expires, signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // =================== PASSWORD RESET ENDPOINTS ===================

    /// <summary>
    /// Yêu cầu đặt lại mật khẩu - gửi link reset qua email
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value!.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key.ToLower(),
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );
            
            return BadRequest(new { errors });
        }

        var ipAddress = GetClientIpAddress();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _passwordResetService.RequestPasswordResetAsync(request, ipAddress, userAgent);
        
        // Luôn trả về 200 OK để tránh email enumeration
        return Ok(result);
    }

    /// <summary>
    /// Validate reset token - kiểm tra token có hợp lệ không
    /// </summary>
    [HttpPost("validate-reset-token")]
    [AllowAnonymous]
    public async Task<ActionResult<ValidateResetTokenResponse>> ValidateResetToken([FromBody] ValidateResetTokenRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _passwordResetService.ValidateResetTokenAsync(request);
        
        if (!result.IsValid)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Đặt lại mật khẩu mới với token hợp lệ
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value!.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key.ToLower(),
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );
            
            return BadRequest(new { errors });
        }

        var ipAddress = GetClientIpAddress();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _passwordResetService.ResetPasswordAsync(request, ipAddress, userAgent);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Lấy IP address của client
    /// </summary>
    private string? GetClientIpAddress()
    {
        // Kiểm tra các header proxy phổ biến
        var xForwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            // X-Forwarded-For có thể chứa nhiều IP, lấy IP đầu tiên
            return xForwardedFor.Split(',')[0].Trim();
        }

        var xRealIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xRealIp))
        {
            return xRealIp;
        }

        // Fallback to connection remote IP
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
