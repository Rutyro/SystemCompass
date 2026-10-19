namespace SystemCompass.Core.Startup;

public static class CommandLineParser
{
    /// <summary>
    /// Extracts the executable path from a Run value or a scheduled task action.
    /// Returns null when the command line cannot be interpreted.
    /// </summary>
    public static string? TryGetExecutable(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        var text = Environment.ExpandEnvironmentVariables(commandLine).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        if (text[0] == '"')
        {
            var closing = text.IndexOf('"', 1);
            return closing > 1 ? text[1..closing] : null;
        }

        var exeIndex = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex >= 0)
        {
            return text[..(exeIndex + 4)];
        }

        var space = text.IndexOf(' ', StringComparison.Ordinal);
        return space > 0 ? text[..space] : text;
    }
}
