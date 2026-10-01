using System.Text.RegularExpressions;

namespace Vault.Core.Validation;

/// <summary>
/// Central input validation. All user-provided data is validated before reaching services.
/// Validation errors are raised as <see cref="ValidationException"/> with safe messages.
/// </summary>
public static class InputValidator
{
    public const int MaxLabelLength = 200;
    public const int MaxUsernameLength = 320;
    public const int MaxPasswordLength = 1024;
    public const int MaxUrlLength = 2048;
    public const int MaxNotesLength = 8192;
    public const int MaxNameLength = 200;
    public const int MaxTagLength = 64;
    public const int MaxTags = 20;

    private static readonly Regex SidPattern =
        new(@"^S-\d+(-\d+)+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AccountNamePattern =
        new(@"^[^\\/:*?""<>|]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string RequiredText(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException($"{fieldName} is required.");

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ValidationException($"{fieldName} must not exceed {maxLength} characters.");

        return trimmed;
    }

    public static string? OptionalText(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ValidationException($"{fieldName} must not exceed {maxLength} characters.");

        return trimmed;
    }

    public static string RequiredPassword(string? value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ValidationException("Password is required.");
        if (value.Length > MaxPasswordLength)
            throw new ValidationException($"Password must not exceed {MaxPasswordLength} characters.");
        return value;
    }

    public static string? OptionalUrl(string? value)
    {
        var url = OptionalText(value, "URL", MaxUrlLength);
        if (url is null)
            return null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ValidationException("URL must be an absolute http or https address.");

        return url;
    }

    public static string RequiredSid(string? value)
    {
        var sid = RequiredText(value, "Windows SID", 256);
        if (!SidPattern.IsMatch(sid))
            throw new ValidationException("Windows SID has an invalid format.");
        return sid;
    }

    public static string RequiredAccountName(string? value, string fieldName)
    {
        var name = RequiredText(value, fieldName, MaxNameLength);
        if (!AccountNamePattern.IsMatch(name))
            throw new ValidationException($"{fieldName} contains invalid characters.");
        return name;
    }

    public static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
            return Array.Empty<string>();

        var normalized = tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count > MaxTags)
            throw new ValidationException($"A credential can have at most {MaxTags} tags.");

        foreach (var tag in normalized)
        {
            if (tag.Length > MaxTagLength)
                throw new ValidationException($"Tags must not exceed {MaxTagLength} characters.");
        }

        return normalized;
    }
}

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
