namespace EVBSS.Api.Dtos.Vehicles;

/// <summary>
/// Request để scan ảnh đăng ký xe từ URL
/// </summary>
public record ScanRegistrationUrlRequest
{
    public string ImageUrl { get; set; } = string.Empty;
}
