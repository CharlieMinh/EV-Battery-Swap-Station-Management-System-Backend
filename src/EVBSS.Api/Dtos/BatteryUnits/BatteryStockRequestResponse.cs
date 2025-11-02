using EVBSS.Api.Models;

namespace EVBSS.Api.Dtos.BatteryUnits;

/// <summary>
/// DTO response cho BatteryStockRequest
/// </summary>
public class BatteryStockRequestResponse
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public string? StationName { get; set; }
    public Guid BatteryModelId { get; set; }
    public string? BatteryModelName { get; set; }
    public int Quantity { get; set; }
    public string? StaffNote { get; set; }
    public string Status { get; set; } = null!;
    
    public Guid RequestedByStaffId { get; set; }
    public string? RequestedByStaffName { get; set; }
    public DateTime RequestDate { get; set; }
    
    public Guid? AdminReviewerId { get; set; }
    public string? AdminReviewerName { get; set; }
    public DateTime? AdminReviewDate { get; set; }
    public string? AdminNote { get; set; }
    
    public Guid? RelatedBulkCreateRequestId { get; set; }
    public DateTime UpdatedAt { get; set; }
}
