using System;
using System.Security.Claims;

namespace EVBSS.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Try get current user id as Guid. Returns null if not present or invalid.
    /// </summary>
    public static Guid? GetUserId(this ClaimsPrincipal? principal)
    {
        if (principal == null) return null;
        var val = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(val, out var g)) return g;
        return null;
    }

    /// <summary>
    /// Get current user id or throw InvalidOperationException when missing.
    /// </summary>
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var id = principal.GetUserId();
        if (id == null) throw new InvalidOperationException("Missing or invalid user id in claims.");
        return id.Value;
    }
}
