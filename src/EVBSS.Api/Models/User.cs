namespace EVBSS.Api.Models;
using System.Text.Json.Serialization;

public enum Role { Driver = 0, Staff = 1, Admin = 2 }

public enum AuthMethod { Local = 0, Google = 1 }

public enum UserStatus 
{ 
    Active = 0,      // Hoạt động
    Locked = 1       // Bị khóa
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public Role Role { get; set; } = Role.Driver;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; 
    public DateTime? LastLogin { get; set; }
    
    // Penalty tracking: Số lần vi phạm (hủy muộn + no-show)
    public int NoShowCount { get; set; } = 0;
    
    // Authentication fields
    public AuthMethod AuthMethod { get; set; } = AuthMethod.Local;
    public string? GoogleId { get; set; }
    public string? ProfilePictureUrl { get; set; }

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();    // Navigation property: Một user có nhiều xe

    // Navigation property: Một user có nhiều subscription
    public ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();    // Navigation property: Một user có nhiều subscription

    // Staff-specific fields: A staff member can be assigned to one station
    public Guid? StationId { get; set; }
    [JsonIgnore]
    public Station? Station { get; set; }
}
