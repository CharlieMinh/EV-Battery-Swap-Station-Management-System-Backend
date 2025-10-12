using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.BatteryUnits;

public class AddBatteriesToStationDto
{
    [Required(ErrorMessage = "Station ID is required")]
    public Guid StationId { get; set; }

    [Required(ErrorMessage = "Battery units list is required")]
    [MinLength(1, ErrorMessage = "At least one battery unit is required")]
    public List<BatteryUnitCreateData> BatteryUnits { get; set; } = new();
}

public class BatteryUnitCreateData
{
    [Required(ErrorMessage = "Serial number is required")]
    [StringLength(50, ErrorMessage = "Serial number cannot exceed 50 characters")]
    public string Serial { get; set; } = null!;

    [Required(ErrorMessage = "Battery model ID is required")]
    public Guid BatteryModelId { get; set; }
}