using System.ComponentModel.DataAnnotations;
using EVBSS.Api.Models;

namespace EVBSS.Api.Dtos.Users;

/// <summary>
/// Request to update user information
/// </summary>
public class UpdateUserRequest
{
    [StringLength(100)]
    public string? Name { get; set; }

    [Phone]
    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Profile picture file upload (optional)
    /// </summary>
    public IFormFile? ProfilePicture { get; set; }

    public Role? Role { get; set; }

    /// <summary>
    /// User account status (Admin only)
    /// </summary>
    public UserStatus? Status { get; set; }

    /// <summary>
    /// Station ID to assign the staff member to (Admin only)
    /// </summary>
    public Guid? StationId { get; set; }
}
