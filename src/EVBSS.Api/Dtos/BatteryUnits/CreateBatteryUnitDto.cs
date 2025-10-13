using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.BatteryUnits;

public class CreateBatteryUnitDto
{
    [Required(ErrorMessage = "Serial number is required")]
    [StringLength(50, ErrorMessage = "Serial number cannot exceed 50 characters")]
    public string Serial { get; set; } = null!;

    [Required(ErrorMessage = "Battery model ID is required")]
    public Guid BatteryModelId { get; set; }

    [Required(ErrorMessage = "Station ID is required")]
    public Guid StationId { get; set; }
}