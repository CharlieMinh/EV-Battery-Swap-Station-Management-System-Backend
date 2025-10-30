namespace EVBSS.Api.Models;

public class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }                   // chủ xe (từ JWT)
    public string VIN { get; set; } = null!;           // 5–17 ký tự, UPPERCASE
    public string Plate { get; set; } = null!;         // ≤20 ký tự, UPPERCASE
    public Guid? VehicleModelId { get; set; }    /// Loại xe của hãng (VF3, VF5, VF8, VF9)
    public Guid CompatibleBatteryModelId { get; set; }    /// Model pin tương thích (tự động lấy từ VehicleModel)
    public string? PhotoUrl { get; set; }    /// URL ảnh xe của chủ xe (để nhận diện)
    public string? RegistrationPhotoUrl { get; set; }    /// URL ảnh cà vẹt xe (giấy đăng ký xe)
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
    [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(VehicleModelId))]
    public VehicleModel VehicleModel { get; set; } = null!;
    [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(CompatibleBatteryModelId))]
    public BatteryModel CompatibleModel { get; set; } = null!;

    // Navigation property: Một xe có thể có nhiều subscription (lịch sử)
    public ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
}
