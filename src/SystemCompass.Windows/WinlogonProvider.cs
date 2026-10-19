using Microsoft.Win32;
using SystemCompass.Core.Startup;

namespace SystemCompass.Windows;

/// <summary>
/// Winlogon Shell and Userinit are reported for completeness and are read-only.
/// </summary>
public sealed class WinlogonProvider : IStartupProvider
{
    private const string KeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
    private static readonly string[] ValueNames = ["Shell", "Userinit"];

    private readonly Classifier _classifier;
    private readonly SignatureVerifier _signatures;

    public WinlogonProvider(Classifier classifier, SignatureVerifier signatures)
    {
        _classifier = classifier;
        _signatures = signatures;
    }

    public StartupMechanism Mechanism => StartupMechanism.Winlogon;

    public bool CanSetState => false;

    public IReadOnlyList<StartupEntry> Enumerate()
    {
        using var key = Registry.LocalMachine.OpenSubKey(KeyPath);
        if (key is null)
        {
            return Array.Empty<StartupEntry>();
        }

        var entries = new List<StartupEntry>();
        foreach (var valueName in ValueNames)
        {
            if (key.GetValue(valueName) is not string commandLine || string.IsNullOrWhiteSpace(commandLine))
            {
                continue;
            }

            var resolved = CommandLineParser.TryGetExecutable(commandLine);
            var entry = new StartupEntry(
                StartupEntryId.Create(StartupMechanism.Winlogon, StartupScope.System, $"HKLM\\{KeyPath}", valueName),
                valueName,
                $"HKLM\\{KeyPath}",
                StartupMechanism.Winlogon,
                StartupScope.System,
                EntryState.Enabled,
                commandLine,
                resolved,
                _signatures.Verify(resolved),
                new Assessment(EntryCategory.Unclear, RiskLevel.Medium, string.Empty, string.Empty),
                RequiresElevation: true);

            entries.Add(entry with { Assessment = _classifier.Assess(entry) });
        }

        return entries;
    }

    public void SetState(string entryId, bool enabled)
    {
        throw new InvalidOperationException("Winlogon entries are read-only.");
    }
}
