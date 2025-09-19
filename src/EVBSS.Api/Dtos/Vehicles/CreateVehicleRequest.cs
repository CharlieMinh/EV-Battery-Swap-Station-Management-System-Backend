using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Vehicles;

public class CreateVehicleRequest
{
    [Required, StringLength(17, MinimumLength = 5)]
    public string Vin { get; set; } = default!;

    [Required, StringLength(20, MinimumLength = 3)]
    public string Plate { get; set; } = default!;

    [Required]
    public Guid CompatibleBatteryModelId { get; set; }
}
