using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SystemCompass.Core.Startup;

namespace SystemCompass.Windows;

/// <summary>
/// Reads task definitions from schtasks XML so localized console output never
/// has to be parsed. Only tasks with a boot or logon trigger are startup entries.
/// </summary>
public sealed class ScheduledTasks : ManageStartup
{
    private static readonly XNamespace TaskNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";
    private static readonly Regex TaskBlock = new(
        @"<!--\s*(?<name>[^>]*?)\s*-->\s*<\?xml[^>]*\?>\s*(?<task><Task\b.*?</Task>)",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    private readonly Classifier _classifier;
    private readonly SignatureVerifier _signatures;

    public ScheduledTasks(Classifier classifier, SignatureVerifier signatures)
    {
        _classifier = classifier;
        _signatures = signatures;
    }

    public StartupMechanism Mechanism => StartupMechanism.ScheduledTask;

    public bool CanSetState => true;

    public IReadOnlyList<StartupEntry> Enumerate()
    {
        var document = RunSchtasks("/Query", "/XML");
        var entries = new List<StartupEntry>();

        foreach (Match match in TaskBlock.Matches(document))
        {
            var xml = match.Groups["task"].Value;
            if (!TryParseTask(xml, out var uri, out var command, out var enabled, out var isStartupTrigger))
            {
                continue;
            }

            if (!isStartupTrigger || string.IsNullOrWhiteSpace(uri))
            {
                continue;
            }

            entries.Add(CreateEntry(uri, command, enabled));
        }

        return entries;
    }

    public void SetState(string entryId, bool enabled)
    {
        var entry = Enumerate().FirstOrDefault(candidate => candidate.Id == entryId)
            ?? throw new InvalidOperationException("Unknown startup task.");
        var uri = entry.Location + entry.Name;
        RunSchtasks("/Change", "/TN", uri, enabled ? "/ENABLE" : "/DISABLE");
    }

    private StartupEntry CreateEntry(string uri, string? command, bool enabled)
    {
        var name = uri[(uri.LastIndexOf('\\') + 1)..];
        var folder = uri[..(uri.LastIndexOf('\\') + 1)];
        var resolved = CommandLineParser.TryGetExecutable(command);
        var entry = new StartupEntry(
            StartupEntryId.Create(StartupMechanism.ScheduledTask, StartupScope.AllUsers, folder, name),
            name,
            folder,
            StartupMechanism.ScheduledTask,
            StartupScope.AllUsers,
            enabled ? EntryState.Enabled : EntryState.Disabled,
            command,
            resolved,
            _signatures.Verify(resolved),
            new Assessment(EntryCategory.Unclear, RiskLevel.Medium, string.Empty, string.Empty),
            RequiresElevation: true);

        return entry with { Assessment = _classifier.Assess(entry) };
    }

    private static bool TryParseTask(
        string xml,
        out string? uri,
        out string? command,
        out bool enabled,
        out bool isStartupTrigger)
    {
        uri = null;
        command = null;
        enabled = true;
        isStartupTrigger = false;

        try
        {
            var document = XDocument.Parse(xml);
            var root = document.Root;
            if (root is null)
            {
                return false;
            }

            uri = root.Element(TaskNamespace + "RegistrationInfo")?.Element(TaskNamespace + "URI")?.Value;
            var settings = root.Element(TaskNamespace + "Settings");
            enabled = !string.Equals(
                settings?.Element(TaskNamespace + "Enabled")?.Value,
                "false",
                StringComparison.OrdinalIgnoreCase);

            var triggers = root.Element(TaskNamespace + "Triggers");
            isStartupTrigger = triggers?.Elements().Any(trigger =>
                trigger.Name.LocalName is "BootTrigger" or "LogonTrigger") == true;

            var actions = root.Element(TaskNamespace + "Actions");
            var exec = actions?.Elements().FirstOrDefault(action => action.Name.LocalName == "Exec");
            command = exec?.Element(TaskNamespace + "Command")?.Value;
            var arguments = exec?.Element(TaskNamespace + "Arguments")?.Value;
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                command = $"{command} {arguments}";
            }

            return true;
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    private static string RunSchtasks(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("schtasks.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("schtasks.exe could not be started.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"schtasks failed with exit code {process.ExitCode}: {error.Trim()}"));
        }

        return output;
    }
}
