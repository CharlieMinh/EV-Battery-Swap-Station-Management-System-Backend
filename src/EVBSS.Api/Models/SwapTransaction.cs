namespace EVBSS.Api.Models;

public enum SwapStatus
{
    Completed = 0,
    Failed = 1,
    Cancelled = 2
}

public class SwapTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid StationId { get; set; }
    public Station Station { get; set; } = null!;
    public Guid? OldBatteryId { get; set; }
    public BatteryUnit? OldBattery { get; set; }
    public Guid? NewBatteryId { get; set; }
    public BatteryUnit? NewBattery { get; set; }
    public SwapStatus Status { get; set; } = SwapStatus.Completed;
    public decimal? Cost { get; set; }
    public DateTime SwapTime { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}