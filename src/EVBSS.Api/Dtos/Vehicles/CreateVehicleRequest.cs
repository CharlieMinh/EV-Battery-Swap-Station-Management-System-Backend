using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Vehicles;

public class CreateVehicleRequest
{
    [Required, StringLength(17, MinimumLength = 5)]
    public string Vin { get; set; } = default!;

    [Required, StringLength(20, MinimumLength = 3)]
    public string Plate { get; set; } = default!;

    /// <summary>
    /// Loại xe của hãng (VF3, VF5, VF8, VF9)
    /// </summary>
    [Required]
    public Guid VehicleModelId { get; set; }
    
    /// <summary>
    /// File ảnh xe của chủ xe (bắt buộc để nhận diện)
    /// </summary>
    [Required(ErrorMessage = "Vehicle photo is required")]
    public IFormFile Photo { get; set; } = default!;
    
    /// <summary>
    /// File ảnh cà vẹt xe / giấy đăng ký xe (bắt buộc để xác minh)
    /// </summary>
    [Required(ErrorMessage = "Vehicle registration photo is required")]
    public IFormFile RegistrationPhoto { get; set; } = default!;
}
