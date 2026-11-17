using System.Globalization;

namespace TrailHub.BuildingBlocks.Domain;

/// <summary>
/// Names people are shown by. Auth0 email/password sign-ups get their email as their name, and an email must never be
/// shown to other people as a name, so it is turned into a readable one ("anna.k_smith+hikes@x.io" → "Anna K Smith").
/// </summary>
public static class DisplayNames
{
    public const int MaxLength = 80;

    /// <summary>For someone with neither a usable name nor an email.</summary>
    public const string Fallback = "Hiker";

    private static readonly char[] Separators = ['.', '_', '-', '+'];

    /// <summary>
    /// The trimmed <paramref name="name"/> when it is a real name; otherwise a name made from the local part of the
    /// email (<paramref name="email"/>, or <paramref name="name"/> when that is the email), or "Hiker".
    /// </summary>
    public static string Friendly(string? name, string? email = null)
    {
        var trimmed = name?.Trim();
        if (!string.IsNullOrEmpty(trimmed) && !IsEmail(trimmed))
        {
            return Cap(trimmed);
        }

        var source = IsEmail(trimmed) ? trimmed! : email?.Trim();
        return string.IsNullOrEmpty(source) ? Fallback : FromEmail(source);
    }

    /// <summary>Whether the value looks like an email address (something@something).</summary>
    public static bool IsEmail(string? value)
    {
        var at = value?.IndexOf('@') ?? -1;
        return at > 0 && at < value!.Length - 1 && !value.Any(char.IsWhiteSpace);
    }

    private static string FromEmail(string email)
    {
        var at = email.IndexOf('@');
        var local = at >= 0 ? email[..at] : email;

        // "anna+hikes@…" is Anna's address with a tag for filtering mail; the tag isn't part of her name.
        var plus = local.IndexOf('+');
        if (plus > 0)
        {
            local = local[..plus];
        }

        var words = local
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(piece => piece.TrimEnd("0123456789".ToCharArray()))
            .Where(piece => piece.Length > 0)
            .Select(Capitalise)
            .ToList();
        return words.Count == 0 ? Fallback : Cap(string.Join(' ', words).Trim());
    }

    private static string Capitalise(string word) =>
        char.ToUpper(word[0], CultureInfo.InvariantCulture) + word[1..];

    private static string Cap(string value) => value.Length <= MaxLength ? value : value[..MaxLength].TrimEnd();
}
