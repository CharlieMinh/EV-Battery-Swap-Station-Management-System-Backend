using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace EVBSS.Api.Validation;

/// <summary>
/// Custom validation attribute for strong password requirements
/// Must contain: uppercase, lowercase, number, min 8 characters
/// </summary>
public class StrongPasswordAttribute : ValidationAttribute
{
    private static readonly Regex UppercaseRegex = new(@".*[A-Z].*", RegexOptions.Compiled);
    private static readonly Regex LowercaseRegex = new(@".*[a-z].*", RegexOptions.Compiled);
    private static readonly Regex NumberRegex = new(@".*[0-9].*", RegexOptions.Compiled);

    public override bool IsValid(object? value)
    {
        if (value is not string password)
            return false;

        // Check minimum length
        if (password.Length < 8)
            return false;

        // Check for uppercase letter
        if (!UppercaseRegex.IsMatch(password))
            return false;

        // Check for lowercase letter  
        if (!LowercaseRegex.IsMatch(password))
            return false;

        // Check for number
        if (!NumberRegex.IsMatch(password))
            return false;

        return true;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be at least 8 characters and contain uppercase, lowercase, and number.";
    }
}

/// <summary>
/// Custom validation attribute for email format
/// Uses specific regex pattern for stricter validation
/// </summary>
public class CustomEmailAttribute : ValidationAttribute
{
    private static readonly Regex EmailRegex = new(@"^[A-Za-z0-9+_.-]+@[A-Za-z0-9.-]+$", RegexOptions.Compiled);

    public override bool IsValid(object? value)
    {
        if (value is not string email)
            return false;

        return EmailRegex.IsMatch(email);
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be a valid email format.";
    }
}