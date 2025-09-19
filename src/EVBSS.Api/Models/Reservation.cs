namespace EVBSS.Api.Models;

public class Reservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid StationId { get; set; }
    public Guid BatteryModelId { get; set; }
    public Guid BatteryUnitId { get; set; }              // viên pin cụ thể đang giữ
    public DateTime StartTime { get; set; }              // UTC
    public ReservationStatus Status { get; set; } = ReservationStatus.Held;
    public int HoldDurationMinutes { get; set; } = 15;   // giữ 15'
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navs
    public User User { get; set; } = null!;
    public Station Station { get; set; } = null!;
    public BatteryModel BatteryModel { get; set; } = null!;
    public BatteryUnit BatteryUnit { get; set; } = null!;
}
