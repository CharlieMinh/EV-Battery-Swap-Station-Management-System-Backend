namespace EVBSS.Api.Configuration;

public class VnPayConfig
{
    public string TmnCode { get; set; } = null!;          // Terminal ID
    public string HashSecret { get; set; } = null!;       // Secret key
    public string BaseUrl { get; set; } = null!;          // VNPay Gateway URL
    public string ReturnUrl { get; set; } = null!;        // Return URL after payment
    public string IpnUrl { get; set; } = null!;           // IPN callback URL
    public string Version { get; set; } = "2.1.0";        // VNPay API version
    public string Command { get; set; } = "pay";          // Payment command
    public string CurrCode { get; set; } = "VND";         // Currency
    public string Locale { get; set; } = "vn";            // Language
}