using EVBSS.Api.Models;

namespace EVBSS.Api.Dtos.BatteryInventory;

/// <summary>
/// Summary response for inventory at a station
/// Provides quick overview without querying individual BatteryUnit records
/// </summary>
public class InventorySummaryResponse
{
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public List<InventoryByModelResponse> InventoryByModel { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Inventory breakdown by battery model
/// </summary>
public class InventoryByModelResponse
{
    public Guid BatteryModelId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public int FullQuantity { get; set; }
    public int ChargingQuantity { get; set; }
    public int MaintenanceQuantity { get; set; }
    public int IssuedQuantity { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Detailed inventory item (for drill-down view)
/// </summary>
public class InventoryDetailResponse
{
    public Guid Id { get; set; }
    public Guid BatteryModelId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public BatteryStatus Status { get; set; }
    public int Quantity { get; set; }
    public DateTime UpdatedAt { get; set; }
}
