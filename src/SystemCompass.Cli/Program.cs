using System.CommandLine;

namespace SystemCompass.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        var root = new RootCommand(
            "SystemCompass audits Windows startup entries, applies reversible changes and writes redacted reports.");
        foreach (var command in CliCommands.CreateAll())
        {
            root.Subcommands.Add(command);
        }

        return root.Parse(args).Invoke();
    }
}
