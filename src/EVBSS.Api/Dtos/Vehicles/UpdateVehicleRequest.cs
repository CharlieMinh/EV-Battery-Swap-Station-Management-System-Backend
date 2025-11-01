using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Vehicles;

/// <summary>
/// Request để cập nhật thông tin xe với file upload
/// User chỉ có thể update ảnh xe và ảnh giấy đăng ký.
/// Thông tin như Plate, VIN sẽ tự động được OCR từ ảnh giấy đăng ký.
/// </summary>
public class UpdateVehicleRequest
{
    /// <summary>
    /// File ảnh xe mới (có thể upload file ảnh trực tiếp)
    /// </summary>
    public IFormFile? Photo { get; set; }
    
    /// <summary>
    /// File ảnh cà vẹt xe mới / giấy đăng ký xe (có thể upload file ảnh trực tiếp)
    /// Khi upload ảnh mới, hệ thống sẽ tự động OCR để cập nhật Plate, VIN và thông tin khác
    /// </summary>
    public IFormFile? RegistrationPhoto { get; set; }
}
