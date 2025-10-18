using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Vehicles;

/// <summary>
/// Request để cập nhật thông tin xe với file upload
/// </summary>
public class UpdateVehicleRequest
{
    /// <summary>
    /// Biển số xe (có thể cập nhật nếu đổi biển)
    /// </summary>
    [StringLength(20, MinimumLength = 3)]
    public string? Plate { get; set; }
    
    /// <summary>
    /// File ảnh xe mới (có thể upload file ảnh trực tiếp)
    /// </summary>
    public IFormFile? Photo { get; set; }
    
    /// <summary>
    /// File ảnh cà vẹt xe mới / giấy đăng ký xe (có thể upload file ảnh trực tiếp)
    /// </summary>
    public IFormFile? RegistrationPhoto { get; set; }
}
