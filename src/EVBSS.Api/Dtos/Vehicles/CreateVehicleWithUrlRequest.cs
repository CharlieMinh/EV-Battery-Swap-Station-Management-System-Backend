using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Vehicles;

/// <summary>
/// Request để tạo xe mới với URL ảnh (sau khi đã upload riêng)
/// </summary>
public class CreateVehicleWithUrlRequest
{
    [Required, StringLength(17, MinimumLength = 5)]
    public string Vin { get; set; } = default!;

    [Required, StringLength(20, MinimumLength = 3)]
    public string Plate { get; set; } = default!;

    [Required]
    public Guid VehicleModelId { get; set; }

    /// <summary>
    /// URL ảnh xe của chủ xe (bắt buộc để nhận diện)
    /// </summary>
    [Required(ErrorMessage = "Vehicle photo URL is required")]
    [Url(ErrorMessage = "Photo URL must be a valid URL")]
    [StringLength(500)]
    public string PhotoUrl { get; set; } = default!;

    /// <summary>
    /// URL ảnh cà vẹt xe / giấy đăng ký xe (bắt buộc để xác minh)
    /// </summary>
    [Required(ErrorMessage = "Vehicle registration photo URL is required")]
    [Url(ErrorMessage = "Registration photo URL must be a valid URL")]
    [StringLength(500)]
    public string RegistrationPhotoUrl { get; set; } = default!;
}
