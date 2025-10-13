namespace EVBSS.Api.Dtos.BatteryUnits;

public class BatteryUnitResponseDto
{
    public Guid Id { get; set; }
    public string Serial { get; set; } = null!;
    public Guid BatteryModelId { get; set; }
    public string BatteryModelName { get; set; } = null!;
    public int Voltage { get; set; }
    public int CapacityWh { get; set; }
    public string? Manufacturer { get; set; }
    public Guid StationId { get; set; }
    public string StationName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool IsReserved { get; set; }
    public DateTime UpdatedAt { get; set; }
}