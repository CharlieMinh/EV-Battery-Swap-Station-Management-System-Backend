using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EVBSS.Api.Data;
using EVBSS.Api.Dtos.Auth;
using EVBSS.Api.Models;
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

    public AuthController(AppDbContext db, IConfiguration cfg)
    {
        _db = db; _cfg = cfg;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Invalid input data.", details = ModelState } });
        }

        var email = req.Email.Trim().ToLower();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { error = new { code = "EMAIL_EXISTS", message = "Email already registered." } });

        try
        {
            var user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                Name = req.Name?.Trim(),
                Phone = req.Phone?.Trim(),
                Role = Role.Driver
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Me), null, new { user.Id, user.Email, role = user.Role.ToString() });
        }
        catch (Exception)
        {
            return StatusCode(500, new { error = new { code = "REGISTRATION_FAILED", message = "Registration failed. Please try again." } });
        }
    }

    [HttpPost("login")]
[AllowAnonymous]
[ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
[Produces("application/json")]
public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Invalid input data.", details = ModelState } });
    }

    try
    {
        var email = req.Email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            return Unauthorized(new { error = new { code = "INVALID_CREDENTIALS", message = "Invalid email or password." } });
        }

        var token = GenerateJwt(user);

        // Set HttpOnly cookie with secure settings
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps, // Use HTTPS in production, allow HTTP in development
            SameSite = SameSiteMode.Strict, // More secure for same-site requests
            Expires = DateTime.UtcNow.AddMinutes(int.Parse(_cfg["Jwt:ExpireMinutes"] ?? "60")),
            Path = "/"
        };

        // In development, allow less strict settings for CORS
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            cookieOptions.SameSite = SameSiteMode.None;
            cookieOptions.Secure = false; // Allow HTTP in development
        }

        Response.Cookies.Append("jwt", token, cookieOptions);

        user.LastLogin = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new AuthResponse { Token = token, Role = user.Role.ToString(), Name = user.Name });
    }
    catch (Exception)
    {
        return StatusCode(500, new { error = new { code = "LOGIN_FAILED", message = "Login failed. Please try again." } });
    }
}
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        // Delete the JWT cookie with the same settings used when creating it
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTime.UtcNow.AddDays(-1) // Expire immediately
        };

        // In development, match the settings used when creating the cookie
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            cookieOptions.SameSite = SameSiteMode.None;
            cookieOptions.Secure = false;
        }

        Response.Cookies.Append("jwt", "", cookieOptions);
        return Ok(new { message = "Logged out successfully." });
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
        var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? jwt["Key"];
        
        if (string.IsNullOrEmpty(jwtKey))
        {
            throw new InvalidOperationException("JWT key is not configured.");
        }
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Unique token identifier
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        var expires = DateTime.UtcNow.AddMinutes(int.Parse(jwt["ExpireMinutes"] ?? "60"));
        var token = new JwtSecurityToken(jwt["Issuer"], jwt["Audience"], claims, expires: expires, signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
