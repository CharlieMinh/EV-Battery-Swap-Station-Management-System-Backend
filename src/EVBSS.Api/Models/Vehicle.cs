namespace EVBSS.Api.Models;

public class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }                   // chủ xe (từ JWT)
    public string VIN { get; set; } = null!;           // 5–17 ký tự, UPPERCASE
    public string Plate { get; set; } = null!;         // ≤20 ký tự, UPPERCASE
    
    /// <summary>
    /// Loại xe của hãng (VF3, VF5, VF8, VF9)
    /// Tạm nullable cho đến khi seed VehicleModels và migrate data cũ
    /// </summary>
    public Guid? VehicleModelId { get; set; }
    
    /// <summary>
    /// Model pin tương thích (tự động lấy từ VehicleModel)
    /// </summary>
    public Guid CompatibleBatteryModelId { get; set; }
    
    /// <summary>
    /// URL ảnh xe của chủ xe (để nhận diện)
    /// </summary>
    public string? PhotoUrl { get; set; }
    
    /// <summary>
    /// URL ảnh cà vẹt xe (giấy đăng ký xe) - bắt buộc khi tạo xe
    /// </summary>
    public string? RegistrationPhotoUrl { get; set; }
    
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
