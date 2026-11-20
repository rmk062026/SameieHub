namespace SameieHub.Domain;

/// <summary>Validation shared by the charging classes, so the rules exist in one place.</summary>
internal static class TextRules
{
    /// <summary>Trims the text. Blank text becomes null. Too long text is rejected with the field name.</summary>
    public static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{fieldName} can be at most {maxLength} characters.", fieldName);
        }

        return trimmed;
    }

    public static string NormalizeRequired(string? value, int maxLength, string fieldName)
    {
        var normalized = NormalizeOptional(value, maxLength, fieldName);
        if (normalized == null)
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }

        return normalized;
    }

    public static void EnsureUtc(DateTime time, string paramName)
    {
        if (time.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be a UTC time.", paramName);
        }
    }

    public static void EnsurePositiveId(int id, string paramName)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id, paramName);
    }
}
