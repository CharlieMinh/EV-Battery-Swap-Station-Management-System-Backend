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
    /// URL ảnh xe của chủ xe (để nhận diện)
    /// </summary>
    [Url]
    [StringLength(500)]
    public string? PhotoUrl { get; set; }
}
