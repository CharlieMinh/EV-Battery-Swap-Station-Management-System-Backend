using System.ComponentModel.DataAnnotations;
using EVBSS.Api.Models;

namespace EVBSS.Api.Dtos.BatteryInventory;

/// <summary>
/// Request to remove battery stock in bulk (for maintenance, disposal, etc.)
/// </summary>
public class RemoveStockRequest
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
    /// Reason for removing stock (maintenance, disposal, damaged, etc.)
    /// </summary>
    public string? Reason { get; set; }
}
