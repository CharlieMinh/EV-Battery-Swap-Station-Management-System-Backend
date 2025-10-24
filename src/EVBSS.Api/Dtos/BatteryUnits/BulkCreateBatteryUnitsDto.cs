
using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.BatteryUnits;

public class BulkCreateBatteryUnitsDto
{
    [Required(ErrorMessage = "Station ID is required")]
    public Guid StationId { get; set; }

    [Required(ErrorMessage = "Battery model ID is required")]
    public Guid BatteryModelId { get; set; }

    [Required(ErrorMessage = "Quantity is required")]
    [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100")]
    public int Quantity { get; set; }
}
