using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Vehicles;

/// <summary>
/// Request để cập nhật thông tin xe
/// </summary>
public class UpdateVehicleRequest
{
    /// <summary>
    /// Biển số xe (có thể cập nhật nếu đổi biển)
    /// </summary>
    [StringLength(20, MinimumLength = 3)]
    public string? Plate { get; set; }
    
    /// <summary>
    /// URL ảnh xe mới
    /// </summary>
    [Url]
    [StringLength(500)]
    public string? PhotoUrl { get; set; }
}
