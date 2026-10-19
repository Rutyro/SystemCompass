using System.Diagnostics;
using Microsoft.Win32;
using SystemCompass.Core.Startup;

namespace SystemCompass.Windows;

/// <summary>
/// Enumerates automatic Win32 services from the service registry. Driver
/// services and non-automatic services are not startup entries. Disabling a
/// service sets it to Manual; it is never deleted and never stopped.
/// </summary>
public sealed class ServiceProvider : IStartupProvider
{
    private const int AutomaticStart = 2;
    private const int ManualStart = 3;
    private const int DisabledStart = 4;

    private readonly Classifier _classifier;
    private readonly SignatureVerifier _signatures;

    public ServiceProvider(Classifier classifier, SignatureVerifier signatures)
    {
        _classifier = classifier;
        _signatures = signatures;
    }

    public StartupMechanism Mechanism => StartupMechanism.Service;

    public bool CanSetState => true;

    public IReadOnlyList<StartupEntry> Enumerate()
    {
        var entries = new List<StartupEntry>();
        using var servicesKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
        if (servicesKey is null)
        {
            return entries;
        }

        foreach (var serviceName in servicesKey.GetSubKeyNames())
        {
            using var key = servicesKey.OpenSubKey(serviceName);
            if (key is null)
            {
                continue;
            }

            var type = key.GetValue("Type") as int? ?? 0;
            var start = key.GetValue("Start") as int? ?? -1;
            if (start != AutomaticStart || (type & 0x30) == 0)
            {
                continue;
            }

            var imagePath = key.GetValue("ImagePath") as string;
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                continue;
            }

            var displayName = key.GetValue("DisplayName") as string;
            if (string.IsNullOrWhiteSpace(displayName) || displayName.StartsWith('@'))
            {
                displayName = serviceName;
            }

            entries.Add(CreateEntry(serviceName, displayName, imagePath));
        }

        return entries;
    }

    public void SetState(string entryId, bool enabled)
    {
        var entry = Enumerate().FirstOrDefault(candidate => candidate.Id == entryId)
            ?? throw new InvalidOperationException("Unknown service entry.");
        var serviceName = entry.Location["Service: ".Length..];
        ConfigureService(serviceName, enabled ? "auto" : "demand");
    }

    public EntryState? GetState(string entryId)
    {
        var entry = Enumerate().FirstOrDefault(candidate => candidate.Id == entryId);
        var serviceName = entry?.Location["Service: ".Length..];
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return null;
        }

        using var key = Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Services\{serviceName}");
        return (key?.GetValue("Start") as int?) switch
        {
            AutomaticStart => EntryState.Enabled,
            ManualStart or DisabledStart => EntryState.Disabled,
            _ => EntryState.Unknown,
        };
    }

    private StartupEntry CreateEntry(string serviceName, string displayName, string imagePath)
    {
        var commandLine = NormalizeImagePath(imagePath);
        var resolved = CommandLineParser.TryGetExecutable(commandLine);
        var location = $"Service: {serviceName}";
        var entry = new StartupEntry(
            StartupEntryId.Create(StartupMechanism.Service, StartupScope.System, location, displayName),
            displayName,
            location,
            StartupMechanism.Service,
            StartupScope.System,
            EntryState.Enabled,
            commandLine,
            resolved,
            _signatures.Verify(resolved),
            new Assessment(EntryCategory.Unclear, RiskLevel.Medium, string.Empty, string.Empty),
            RequiresElevation: true);

        return entry with { Assessment = _classifier.Assess(entry) };
    }

    private static string NormalizeImagePath(string imagePath)
    {
        var text = Environment.ExpandEnvironmentVariables(imagePath);
        if (text.StartsWith(@"\??\", StringComparison.Ordinal))
        {
            text = text[4..];
        }

        if (text.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
        {
            text = Environment.ExpandEnvironmentVariables(@"%SystemRoot%") + text[11..];
        }

        return text;
    }

    private static void ConfigureService(string serviceName, string startMode)
    {
        var startInfo = new ProcessStartInfo("sc.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("config");
        startInfo.ArgumentList.Add(serviceName);
        startInfo.ArgumentList.Add("start=");
        startInfo.ArgumentList.Add(startMode);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("sc.exe could not be started.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"sc config failed with exit code {process.ExitCode}: {error.Trim()} {output.Trim()}".Trim());
        }
    }
}
