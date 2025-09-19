namespace EVBSS.Api.Models;

public class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }                   // chủ xe (từ JWT)
    public string VIN { get; set; } = null!;           // 5–17 ký tự, UPPERCASE
    public string Plate { get; set; } = null!;         // ≤20 ký tự, UPPERCASE
    public Guid CompatibleBatteryModelId { get; set; } // model pin tương thích
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navs
    public User User { get; set; } = null!;
    public BatteryModel CompatibleModel { get; set; } = null!;
}
