namespace EVBSS.Api.Dtos.Users;

/// <summary>
/// Response for password change operation
/// </summary>
public class ChangePasswordResponse
{
    /// <summary>
    /// Success message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when password was changed
    /// </summary>
    public DateTime ChangedAt { get; set; }
}
