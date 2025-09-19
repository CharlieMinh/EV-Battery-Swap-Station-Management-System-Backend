namespace EVBSS.Api.Models;

public class BatteryUnit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Serial { get; set; } = null!;   // duy nhất
    public Guid BatteryModelId { get; set; }
    public Guid StationId { get; set; }
    public BatteryStatus Status { get; set; } = BatteryStatus.Full;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navs
    public BatteryModel Model { get; set; } = null!;
    public Station? Station { get; set; }
    public bool IsReserved { get; set; } = false;

}
