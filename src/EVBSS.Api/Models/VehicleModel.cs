namespace EVBSS.Api.Models;
public class VehicleModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;    /// Tên model xe (VF3, VF5, VF8, VF9)
    public string FullName { get; set; } = null!;    /// Tên đầy đủ (VinFast VF3, VinFast VF5 Plus)
    public string Brand { get; set; } = "VinFast";    /// Hãng sản xuất (VinFast)
    public Guid CompatibleBatteryModelId { get; set; }    /// Loại pin tương thích với model xe này
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public BatteryModel CompatibleBatteryModel { get; set; } = null!;
}
