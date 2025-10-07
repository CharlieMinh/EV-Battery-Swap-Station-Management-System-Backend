namespace EVBSS.Api.Models;

/// <summary>
/// Đại diện cho các dòng xe điện của hãng (VF3, VF5, VF8, VF9, etc.)
/// Chỉ những xe thuộc VehicleModel này mới được phép sử dụng dịch vụ đổi pin
/// </summary>
public class VehicleModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Tên model xe (VF3, VF5, VF8, VF9)
    /// </summary>
    public string Name { get; set; } = null!;
    
    /// <summary>
    /// Tên đầy đủ (VinFast VF3, VinFast VF5 Plus)
    /// </summary>
    public string FullName { get; set; } = null!;
    
    /// <summary>
    /// Hãng sản xuất (VinFast, Tesla, BYD)
    /// </summary>
    public string Brand { get; set; } = "VinFast";
    
    /// <summary>
    /// Loại pin tương thích với model xe này
    /// </summary>
    public Guid CompatibleBatteryModelId { get; set; }
    
    /// <summary>
    /// URL ảnh đại diện của model xe
    /// </summary>
    public string? ImageUrl { get; set; }
    
    /// <summary>
    /// Có đang cho phép đăng ký dịch vụ không
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Mô tả ngắn
    /// </summary>
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public BatteryModel CompatibleBatteryModel { get; set; } = null!;
}
