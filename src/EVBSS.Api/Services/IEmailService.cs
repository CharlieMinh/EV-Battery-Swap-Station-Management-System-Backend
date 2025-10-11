namespace EVBSS.Api.Services;

public interface IEmailService
{
    Task<bool> SendPasswordResetOtpAsync(string toEmail, string otpCode, string userName = "");
    Task<bool> SendEmailAsync(string toEmail, string subject, string htmlContent);
}