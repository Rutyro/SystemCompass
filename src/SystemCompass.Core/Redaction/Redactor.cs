using System.Text.RegularExpressions;

namespace SystemCompass.Core.Redaction;

public sealed class RedactionOptions
{
    public string? UserName { get; init; }

    public string? UserProfilePath { get; init; }

    public string? MachineName { get; init; }

    public IReadOnlyList<string> ExtraPrefixes { get; init; } = [];

    public IReadOnlyList<string> ExtraLiterals { get; init; } = [];
}

/// <summary>
/// Removes personal identifiers from every exported report. Reports are always
/// rendered through this class; there is no unredacted export path.
/// </summary>
public sealed partial class Redactor
{
    private readonly RedactionOptions _options;

    public Redactor(RedactionOptions options)
    {
        _options = options;
    }

    public static Redactor CreateForCurrentUser()
    {
        return new Redactor(new RedactionOptions
        {
            UserName = Environment.UserName,
            UserProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            MachineName = Environment.MachineName,
        });
    }

    public string Redact(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var text = input;

        foreach (var literal in _options.ExtraLiterals)
        {
            if (!string.IsNullOrEmpty(literal))
            {
                text = text.Replace(literal, "<REDACTED>", StringComparison.OrdinalIgnoreCase);
            }
        }

        foreach (var prefix in _options.ExtraPrefixes)
        {
            if (!string.IsNullOrEmpty(prefix))
            {
                text = text.Replace(prefix, "<PATH>", StringComparison.OrdinalIgnoreCase);
            }
        }

        if (!string.IsNullOrEmpty(_options.UserProfilePath))
        {
            text = text.Replace(_options.UserProfilePath, "<USERPROFILE>", StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrEmpty(_options.MachineName))
        {
            text = text.Replace(_options.MachineName, "<MACHINE>", StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrEmpty(_options.UserName))
        {
            text = text.Replace(_options.UserName, "<USER>", StringComparison.OrdinalIgnoreCase);
        }

        text = UserPathRegex().Replace(text, "<USERPROFILE>");
        text = SidRegex().Replace(text, "<SID>");
        text = NonSystemDriveRegex().Replace(text, "<DRIVE>\\…");
        return text;
    }

    [GeneratedRegex(@"[A-Za-z]:\\Users\\[^\\\r\n""]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UserPathRegex();

    [GeneratedRegex(@"S-1-5-\d+(?:-\d+)+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SidRegex();

    [GeneratedRegex("(?<![A-Za-z0-9])[D-Z]:\\\\[^;\\r\\n\"]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NonSystemDriveRegex();
}
