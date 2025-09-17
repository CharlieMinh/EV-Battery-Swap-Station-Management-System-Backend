namespace EVBSS.Api.Models;

public class BatteryModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public int Voltage { get; set; }          // V
    public int CapacityWh { get; set; }       // Wh
    public string? Manufacturer { get; set; }
}
