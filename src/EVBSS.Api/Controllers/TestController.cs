using EVBSS.Api.Services;
using EVBSS.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TestController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly ILogger<TestController> _logger;
    private readonly AppDbContext _context;

    public TestController(IEmailService emailService, ILogger<TestController> logger, AppDbContext context)
    {
        _emailService = emailService;
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Kiểm tra email có tồn tại trong hệ thống User không
    /// </summary>
    [HttpPost("check-email")]
    public async Task<IActionResult> CheckEmailExists([FromBody] CheckEmailRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

            if (user == null)
            {
                return Ok(new { 
                    exists = false, 
                    message = $"Email '{request.Email}' không tồn tại trong hệ thống.",
                    suggestion = "Vui lòng kiểm tra lại email hoặc đăng ký tài khoản mới."
                });
            }

            return Ok(new { 
                exists = true, 
                message = $"Email '{MaskEmail(request.Email)}' tồn tại trong hệ thống.",
                maskedEmail = MaskEmail(request.Email)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking email existence: {Email}", request.Email);
            return StatusCode(500, new { 
                error = "Lỗi hệ thống khi kiểm tra email." 
            });
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
    /// Test gửi email OTP (chỉ dùng cho development)
    /// </summary>
    [HttpPost("send-test-email")]
    public async Task<IActionResult> SendTestEmail([FromBody] TestEmailRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var testOtp = "123456"; // OTP test
            var result = await _emailService.SendPasswordResetOtpAsync(
                request.Email, 
                testOtp, 
                request.UserName ?? "Test User"
            );

            if (result)
            {
                _logger.LogInformation("Test email sent successfully to {Email}", request.Email);
                return Ok(new { 
                    success = true, 
                    message = $"Email đã được gửi thành công đến {request.Email}",
                    otpSent = testOtp // Chỉ show trong test
                });
            }
            else
            {
                _logger.LogError("Failed to send test email to {Email}", request.Email);
                return BadRequest(new { 
                    success = false, 
                    message = "Không thể gửi email. Kiểm tra lại cấu hình SMTP." 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test email to {Email}", request.Email);
            return StatusCode(500, new { 
                success = false, 
                message = "Lỗi hệ thống khi gửi email.",
                error = ex.Message 
            });
        }
    }

    /// <summary>
    /// DEBUG: Kiểm tra swap transactions theo stationId
    /// </summary>
    [HttpGet("debug-swaps-by-station/{stationId}")]
    public async Task<IActionResult> DebugSwapsByStation(Guid stationId)
    {
        try
        {
            // Đếm tất cả swap transactions
            var totalSwaps = await _context.SwapTransactions.CountAsync();
            
            // Đếm swaps theo stationId (RAW - không Include)
            var swapsAtStationRaw = await _context.SwapTransactions
                .Where(s => s.StationId == stationId)
                .CountAsync();

            // Đếm swaps theo stationId (WITH Include - như trong service)
            var swapsAtStationWithInclude = await _context.SwapTransactions
                .Include(s => s.Station)
                .Include(s => s.Vehicle)
                .Include(s => s.User)
                .Include(s => s.Payment)
                .Include(s => s.CheckedInByStaff)
                .Include(s => s.CompletedByStaff)
                .Where(s => s.StationId == stationId)
                .CountAsync();

            // Kiểm tra null navigation properties
            var swapsWithNullNavigation = await _context.SwapTransactions
                .Where(s => s.StationId == stationId)
                .Select(s => new
                {
                    id = s.Id,
                    transactionNumber = s.TransactionNumber,
                    stationId = s.StationId,
                    hasStation = s.Station != null,
                    hasVehicle = s.Vehicle != null,
                    hasUser = s.User != null,
                    status = s.Status.ToString()
                })
                .ToListAsync();

            var nullStationCount = swapsWithNullNavigation.Count(s => !s.hasStation);
            var nullVehicleCount = swapsWithNullNavigation.Count(s => !s.hasVehicle);
            var nullUserCount = swapsWithNullNavigation.Count(s => !s.hasUser);

            // Lấy thông tin station
            var station = await _context.Stations.FindAsync(stationId);

            return Ok(new
            {
                stationId = stationId,
                stationName = station?.Name ?? "Unknown",
                totalSwapsInDB = totalSwaps,
                swapsAtThisStationRaw = swapsAtStationRaw,
                swapsAtThisStationWithInclude = swapsAtStationWithInclude,
                nullNavigationCounts = new
                {
                    nullStation = nullStationCount,
                    nullVehicle = nullVehicleCount,
                    nullUser = nullUserCount
                },
                sampleSwaps = swapsWithNullNavigation.Take(5)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error debugging swaps for station {StationId}", stationId);
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }
}

/// <summary>
/// Request model để test email
/// </summary>
public class TestEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public string? UserName { get; set; }
}

/// <summary>
/// Request model để kiểm tra email tồn tại
/// </summary>
public class CheckEmailRequest
{
    public string Email { get; set; } = string.Empty;
}