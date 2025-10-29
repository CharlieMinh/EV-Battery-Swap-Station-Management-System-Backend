using EVBSS.Api.Dtos.BatteryInventory;

namespace EVBSS.Api.Services;

/// <summary>
/// Interface for battery inventory management service
/// Provides quantity-based operations alongside individual BatteryUnit tracking
/// </summary>
public interface IBatteryInventoryService
{
    /// <summary>
    /// Add battery stock in bulk (creates BatteryInventory record + individual BatteryUnits)
    /// </summary>
    Task<(bool Success, string Message, int ActualQuantityAdded)> AddStockAsync(AddStockRequest request);

    /// <summary>
    /// Remove battery stock in bulk (updates BatteryInventory + removes BatteryUnits)
    /// </summary>
    Task<(bool Success, string Message, int ActualQuantityRemoved)> RemoveStockAsync(RemoveStockRequest request);

    /// <summary>
    /// Change battery status in bulk (e.g., Charging -> Full)
    /// Updates both BatteryInventory counts and BatteryUnit records
    /// </summary>
    Task<(bool Success, string Message, int ActualQuantityChanged)> ChangeStatusAsync(ChangeStatusRequest request);

    /// <summary>
    /// Get inventory summary for a specific station (fast aggregated query)
    /// </summary>
    Task<InventorySummaryResponse?> GetSummaryByStationAsync(Guid stationId);

    /// <summary>
    /// Get all inventory details across all stations (for admin dashboard)
    /// </summary>
    Task<List<InventoryDetailResponse>> GetAllInventoryAsync();

    /// <summary>
    /// Internal method: Update inventory count when individual BatteryUnit changes
    /// Called by SwapTransactionService to maintain sync
    /// </summary>
    Task UpdateInventoryCountAsync(Guid batteryModelId, Guid stationId, Models.BatteryStatus fromStatus, Models.BatteryStatus toStatus, int quantity = 1);

    /// <summary>
    /// Change inventory count for a single status. Useful for decrementing or incrementing a specific status
    /// at a given station (e.g., decrement InUse at source station when a battery is returned elsewhere).
    /// </summary>
    Task ChangeInventoryCountByStatusAsync(Guid batteryModelId, Guid stationId, Models.BatteryStatus status, int delta);

    /// <summary>
    /// Tự động tạo pin mới với Serial VF3-XXX (XXX là 3 số tự tăng)
    /// Phục vụ trường hợp pin trả về không nằm trong hệ thống (tạo đại diện đợi kiểm tra)
    /// </summary>
    Task<Models.BatteryUnit> AutoCreateNewBatteryUnitAsync(Guid batteryModelId, Guid stationId, Guid staffId);
}
