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

    /// <summary>
    /// Reference to the battery model type
    /// </summary>
    [Required]
    public Guid BatteryModelId { get; set; }

    /// <summary>
    /// Reference to the station where batteries are stored
    /// </summary>
    [Required]
    public Guid StationId { get; set; }

    /// <summary>
    /// Current status of batteries in this inventory group
    /// </summary>
    [Required]
    public BatteryStatus Status { get; set; }

    /// <summary>
    /// Total quantity of batteries with this model, at this station, with this status
    /// IMPORTANT: Must always match COUNT(*) of BatteryUnits with same criteria
    /// </summary>
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative")]
    public int Quantity { get; set; }

    /// <summary>
    /// Last time this inventory record was updated
    /// Used for audit and debugging sync issues
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public BatteryModel? BatteryModel { get; set; }
    public Station? Station { get; set; }
}
