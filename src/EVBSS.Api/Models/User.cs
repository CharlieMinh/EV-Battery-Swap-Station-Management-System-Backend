namespace EVBSS.Api.Models;

public enum Role { Driver = 0, Staff = 1, Admin = 2 }

public enum AuthMethod { Local = 0, Google = 1 }

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
    
    // Authentication fields
    public AuthMethod AuthMethod { get; set; } = AuthMethod.Local;
    public string? GoogleId { get; set; }
    public string? ProfilePictureUrl { get; set; }
}
