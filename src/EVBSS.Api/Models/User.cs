namespace EVBSS.Api.Models;

public enum Role { Driver = 0, Staff = 1, Admin = 2 }

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public Role Role { get; set; } = Role.Driver;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogin { get; set; }
}
