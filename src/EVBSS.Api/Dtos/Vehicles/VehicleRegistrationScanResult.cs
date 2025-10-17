namespace EVBSS.Api.Dtos.Vehicles;

/// <summary>
/// Kết quả quét ảnh đăng ký xe (cà vẹt xe) từ AWS Rekognition
/// </summary>
public record VehicleRegistrationScanResult
{
    public string? VIN { get; set; }
    public string? Plate { get; set; }
    public string? Brand { get; set; }
    public string? VehicleModel { get; set; }
    public float Confidence { get; set; }
    public Dictionary<string, string> RawData { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
