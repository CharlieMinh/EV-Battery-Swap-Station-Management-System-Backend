using System.ComponentModel.DataAnnotations;
using EVBSS.Api.Models;

namespace EVBSS.Api.Dtos.BatteryInventory;

/// <summary>
/// Request to change battery status in bulk (e.g., from Charging to Full)
/// This updates both BatteryInventory counts and individual BatteryUnit records
/// </summary>
public class ChangeStatusRequest
{
    [Required(ErrorMessage = "BatteryModelId is required")]
    public Guid BatteryModelId { get; set; }

    [Required(ErrorMessage = "StationId is required")]
    public Guid StationId { get; set; }

    [Required(ErrorMessage = "FromStatus is required")]
    public BatteryStatus FromStatus { get; set; }

    [Required(ErrorMessage = "ToStatus is required")]
    public BatteryStatus ToStatus { get; set; }

    [Required(ErrorMessage = "Quantity is required")]
    [Range(1, 1000, ErrorMessage = "Quantity must be between 1 and 1000")]
    public int Quantity { get; set; }
}
