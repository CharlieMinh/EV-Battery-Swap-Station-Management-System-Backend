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
using EVBSS.Api.Models;   
     


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
        var email = req.Email.Trim().ToLower();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { error = new { code = "EMAIL_EXISTS", message = "Email already registered." }});
        
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
    var email = req.Email.Trim().ToLower();
    var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return Unauthorized(new { error = new { code="INVALID_CREDENTIALS", message="Invalid email or password." }});

    var token = GenerateJwt(user);

        // set HttpOnly cookie
        Response.Cookies.Append("jwt", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // chỉ cho https
            SameSite = SameSiteMode.None, // cho phép FE và BE khác domain
            Expires = DateTime.UtcNow.AddMinutes(int.Parse(_cfg["Jwt:ExpireMinutes"] ?? "120"))
        });

        user.LastLogin = DateTime.UtcNow;
    await _db.SaveChangesAsync();

    return Ok(new AuthResponse { Token = token, Role = user.Role.ToString(), Name = user.Name });
}
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("jwt"); //xóa cookie
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
}
