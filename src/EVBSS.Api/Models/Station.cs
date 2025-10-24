namespace EVBSS.Api.Models;

public class Station
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Display ID in format T01, T02, T03... (auto-generated)
    /// </summary>
    public string? DisplayId { get; set; }
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string City { get; set; } = "HCM";
    public double Lat { get; set; }
    public double Lng { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Giờ mở cửa (mặc định 8:00 AM)
    /// </summary>
    public TimeSpan OpenTime { get; set; } = new TimeSpan(8, 0, 0);

    /// <summary>
    /// Giờ đóng cửa (mặc định 6:00 PM)
    /// </summary>
    public TimeSpan CloseTime { get; set; } = new TimeSpan(18, 0, 0);

    /// <summary>
    /// Số điện thoại liên hệ trạm (format: 0901234567)
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// URL ảnh đại diện của trạm
    /// </summary>
    public string? PrimaryImageUrl { get; set; }

    /// <summary>
    /// Kiểm tra trạm có đang mở cửa không (dựa vào giờ hiện tại)
    /// </summary>
    public bool IsOpenNow()
    {
        var now = DateTime.Now.TimeOfDay;
        return now >= OpenTime && now <= CloseTime;
    }

    // Navigation property: One station can have many staff members
    public ICollection<User> Staff { get; set; } = new List<User>();
}

