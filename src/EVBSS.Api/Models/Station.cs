namespace EVBSS.Api.Models;

public class Station
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? DisplayId { get; set; }    /// Display ID in format T01, T02, T03... (auto-generated)
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string City { get; set; } = "HCM";
    public double Lat { get; set; }
    public double Lng { get; set; }
    public bool IsActive { get; set; } = true;

    public TimeSpan OpenTime { get; set; } = new TimeSpan(8, 0, 0);
    public TimeSpan CloseTime { get; set; } = new TimeSpan(18, 0, 0);


    public string? PhoneNumber { get; set; }

    public string? PrimaryImageUrl { get; set; }

    /// Kiểm tra trạm có đang mở cửa không (dựa vào giờ hiện tại)
    public bool IsOpenNow()
    {
        var now = DateTime.Now.TimeOfDay;
        return now >= OpenTime && now <= CloseTime;
    }

    // Navigation property: One station can have many staff members
    public ICollection<User> Staff { get; set; } = new List<User>();
}

