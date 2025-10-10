using System.Net;
using System.Net.Mail;

namespace EVBSS.Api.Services;

/// <summary>
/// Email service để gửi OTP và email notification
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly bool _enableSsl;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // SMTP Settings từ appsettings.json
        var emailSettings = _configuration.GetSection("EmailSettings");
        _smtpHost = emailSettings["SmtpHost"] ?? "smtp.gmail.com";
        _smtpPort = int.Parse(emailSettings["SmtpPort"] ?? "587");
        _smtpUsername = emailSettings["SmtpUsername"] ?? "";
        _smtpPassword = emailSettings["SmtpPassword"] ?? "";
        _fromEmail = emailSettings["FromEmail"] ?? "";
        _fromName = emailSettings["FromName"] ?? "EVBSS - Hệ thống đổi pin điện";
        _enableSsl = bool.Parse(emailSettings["EnableSsl"] ?? "true");
    }

    public async Task<bool> SendPasswordResetOtpAsync(string toEmail, string otpCode, string userName = "")
    {
        var displayName = !string.IsNullOrEmpty(userName) ? userName : "Khách hàng";
        
        var subject = "🔐 Mã xác thực đặt lại mật khẩu EVBSS";
        
        var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Mã xác thực đặt lại mật khẩu</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f4f4f4;
        }}
        .email-container {{
            background-color: white;
            padding: 40px;
            border-radius: 10px;
            box-shadow: 0 0 10px rgba(0,0,0,0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 30px;
        }}
        .logo {{
            font-size: 28px;
            font-weight: bold;
            color: #2c5aa0;
            margin-bottom: 10px;
        }}
        .otp-container {{
            background-color: #f8f9fa;
            border: 2px solid #2c5aa0;
            border-radius: 8px;
            padding: 20px;
            text-align: center;
            margin: 30px 0;
        }}
        .otp-code {{
            font-size: 36px;
            font-weight: bold;
            color: #2c5aa0;
            letter-spacing: 8px;
            margin: 10px 0;
            font-family: 'Courier New', monospace;
        }}
        .warning {{
            background-color: #fff3cd;
            border: 1px solid #ffeaa7;
            border-radius: 5px;
            padding: 15px;
            margin: 20px 0;
        }}
        .footer {{
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #eee;
            font-size: 14px;
            color: #666;
            text-align: center;
        }}
        .highlight {{
            color: #2c5aa0;
            font-weight: bold;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <div class='logo'>🔋 EVBSS</div>
            <h2>Mã xác thực đặt lại mật khẩu</h2>
        </div>
        
        <p>Xin chào <span class='highlight'>{displayName}</span>,</p>
        
        <p>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản EVBSS của mình. Vui lòng sử dụng mã xác thực dưới đây:</p>
        
        <div class='otp-container'>
            <div>Mã xác thực của bạn:</div>
            <div class='otp-code'>{otpCode}</div>
            <div style='font-size: 14px; color: #666; margin-top: 10px;'>
                Mã có hiệu lực trong <span class='highlight'>10 phút</span>
            </div>
        </div>
        
        <div class='warning'>
            <strong>⚠️ Lưu ý bảo mật:</strong>
            <ul style='margin: 10px 0; padding-left: 20px;'>
                <li>Không chia sẻ mã này với bất kỳ ai</li>
                <li>EVBSS sẽ không bao giờ yêu cầu mã qua điện thoại</li>
                <li>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này</li>
            </ul>
        </div>
        
        <p>Sau khi nhập mã thành công, bạn có thể thiết lập mật khẩu mới cho tài khoản.</p>
        
        <div class='footer'>
            <p>Email này được gửi tự động từ hệ thống EVBSS</p>
            <p>🌐 <strong>EVBSS</strong> - Hệ thống quản lý trạm đổi pin điện thông minh</p>
            <p style='font-size: 12px; color: #999;'>
                Nếu bạn gặp khó khăn, vui lòng liên hệ support@evbss.local
            </p>
        </div>
    </div>
</body>
</html>";

        return await SendEmailAsync(toEmail, subject, htmlContent);
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlContent)
    {
        try
        {
            if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
            {
                _logger.LogWarning("SMTP credentials not configured. Email not sent to {Email}", toEmail);
                
                // For development - log email content
                _logger.LogInformation("📧 EMAIL TO: {Email}\nSUBJECT: {Subject}\nCONTENT: {Content}", 
                    toEmail, subject, htmlContent);
                
                return true; // Return true for development
            }

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = _enableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword)
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = htmlContent,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            await smtpClient.SendMailAsync(mailMessage);
            
            _logger.LogInformation("Email sent successfully to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            return false;
        }
    }
}