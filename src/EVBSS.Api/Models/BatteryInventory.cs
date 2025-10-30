using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Models;

/// <summary>
/// Represents aggregated battery inventory for quantity-based management.
/// This table works alongside BatteryUnit table in a HYBRID solution:
/// - BatteryInventory: Fast quantity tracking for bulk operations
/// - BatteryUnit: Individual battery tracking with serial numbers
/// </summary>
public class BatteryInventory
{
    [Key]
    public Guid Id { get; set; }
    [Required]
    public Guid BatteryModelId { get; set; }    /// Reference to the battery model type
    [Required]
    public Guid StationId { get; set; }    /// Reference to the station where batteries are stored
    [Required]
    public BatteryStatus Status { get; set; }    /// Current status of batteries in this inventory group
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative")] 
    public int Quantity { get; set; }    /// Total quantity of batteries with this model, at this station, with this status
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow; 

    // Navigation properties
    public BatteryModel? BatteryModel { get; set; }
    public Station? Station { get; set; }
}
