using System.ComponentModel.DataAnnotations;
using EVBSS.Api.Models;

namespace EVBSS.Api.Dtos.BatteryInventory;

/// <summary>
/// Request to add battery stock in bulk (for Staff/Admin)
/// This is much faster than creating individual BatteryUnit records
/// </summary>
public class AddStockRequest
{
    [Required(ErrorMessage = "BatteryModelId is required")]
    public Guid BatteryModelId { get; set; }

    [Required(ErrorMessage = "StationId is required")]
    public Guid StationId { get; set; }

    [Required(ErrorMessage = "Status is required")]
    public BatteryStatus Status { get; set; }

    [Required(ErrorMessage = "Quantity is required")]
    [Range(1, 1000, ErrorMessage = "Quantity must be between 1 and 1000")]
    public int Quantity { get; set; }

    /// <summary>
    /// Optional: Serial number prefix for auto-generating serial numbers
    /// If provided, system will generate serials like: "PREFIX-001", "PREFIX-002", etc.
    /// If not provided, system will generate random serials
    /// </summary>
    public string? SerialPrefix { get; set; }
}
