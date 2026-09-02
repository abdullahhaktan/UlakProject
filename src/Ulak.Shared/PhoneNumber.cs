using System.Text.RegularExpressions;

namespace Ulak.Shared;

/// <summary>
/// Normalises and validates phone numbers to E.164-ish form: a leading '+'
/// followed by 8–15 digits. Input may carry spaces, dashes, parens or a
/// "00" international prefix; a bare national number is assumed to be
/// Turkish (+90) after dropping a single leading zero.
/// </summary>
public static partial class PhoneNumber
{
    [GeneratedRegex(@"^\+[1-9]\d{7,14}$")]
    private static partial Regex E164();

    /// <summary>Returns the normalised number, or null when it can't be made valid.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var s = Regex.Replace(raw.Trim(), @"[\s\-().]", string.Empty);

        if (s.StartsWith("00", StringComparison.Ordinal))
        {
            s = "+" + s[2..];
        }
        else if (!s.StartsWith('+'))
        {
            s = "+90" + s.TrimStart('0');
        }

        return E164().IsMatch(s) ? s : null;
    }

    public static bool IsValid(string? raw) => Normalize(raw) is not null;
}
