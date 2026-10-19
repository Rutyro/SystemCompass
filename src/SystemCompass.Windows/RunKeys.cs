using Microsoft.Win32;
using SystemCompass.Core.Startup;

namespace SystemCompass.Windows;

public sealed class RunKeys : ManageStartup
{
    private sealed record Location(
        string Hive,
        string KeyPath,
        StartupScope Scope,
        StartupMechanism Mechanism,
        string? ApprovalPath);

    private static readonly Location[] Locations =
    [
        new("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run",
            StartupScope.CurrentUser, StartupMechanism.RunKey,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"),
        new("HKCU", @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
            StartupScope.CurrentUser, StartupMechanism.RunOnceKey, null),
        new("HKLM", @"Software\Microsoft\Windows\CurrentVersion\Run",
            StartupScope.AllUsers, StartupMechanism.RunKey,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"),
        new("HKLM", @"Software\Microsoft\Windows\CurrentVersion\RunOnce",
            StartupScope.AllUsers, StartupMechanism.RunOnceKey, null),
        new("HKLM", @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
            StartupScope.AllUsers, StartupMechanism.RunKey,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32"),
        new("HKLM", @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce",
            StartupScope.AllUsers, StartupMechanism.RunOnceKey, null),
    ];

    private readonly Classifier _classifier;
    private readonly SignatureVerifier _signatures;
    private readonly TimeProvider _clock;

    public RunKeys(Classifier classifier, SignatureVerifier signatures, TimeProvider clock)
    {
        _classifier = classifier;
        _signatures = signatures;
        _clock = clock;
    }

    public StartupMechanism Mechanism => StartupMechanism.RunKey;

    public bool CanSetState => true;

    public IReadOnlyList<StartupEntry> Enumerate()
    {
        var entries = new List<StartupEntry>();
        foreach (var location in Locations)
        {
            entries.AddRange(EnumerateLocation(location));
        }

        return entries;
    }

    public void SetState(string entryId, bool enabled)
    {
        var entry = Enumerate().FirstOrDefault(candidate => candidate.Id == entryId)
            ?? throw new InvalidOperationException("Unknown startup entry.");
        var location = Locations.FirstOrDefault(candidate =>
            $"{candidate.Hive}\\{candidate.KeyPath}".Equals(entry.Location, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Unknown startup location.");
        if (location.ApprovalPath is null)
        {
            throw new InvalidOperationException("RunOnce entries are read-only.");
        }

        using var baseKey = WindowsRegistry.OpenBase(location.Hive);
        using var approvalKey = baseKey.CreateSubKey(location.ApprovalPath, writable: true)
            ?? throw new InvalidOperationException("The StartupApproved key could not be opened.");
        approvalKey.SetValue(
            entry.Name,
            StartupApprovedValue.Create(enabled, _clock.GetUtcNow()),
            RegistryValueKind.Binary);
    }

    private IEnumerable<StartupEntry> EnumerateLocation(Location location)
    {
        using var baseKey = WindowsRegistry.OpenBase(location.Hive);
        using var runKey = baseKey.OpenSubKey(location.KeyPath);
        if (runKey is null)
        {
            yield break;
        }

        using var approvalKey = location.ApprovalPath is null
            ? null
            : baseKey.OpenSubKey(location.ApprovalPath);

        foreach (var name in runKey.GetValueNames())
        {
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var commandLine = runKey.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                continue;
            }

            var state = location.ApprovalPath is null
                ? EntryState.Enabled
                : StartupApprovedValue.Parse(approvalKey?.GetValue(name) as byte[] ?? []);
            yield return CreateEntry(location, name, commandLine, state);
        }
    }

    private StartupEntry CreateEntry(Location location, string name, string commandLine, EntryState state)
    {
        var resolved = CommandLineParser.TryGetExecutable(commandLine);
        var entry = new StartupEntry(
            StartupEntryId.Create(location.Mechanism, location.Scope, $"{location.Hive}\\{location.KeyPath}", name),
            name,
            $"{location.Hive}\\{location.KeyPath}",
            location.Mechanism,
            location.Scope,
            state,
            commandLine,
            resolved,
            _signatures.Verify(resolved),
            new Assessment(EntryCategory.Unclear, RiskLevel.Medium, string.Empty, string.Empty),
            RequiresElevation: location.Hive == "HKLM");

        return entry with { Assessment = _classifier.Assess(entry) };
    }
}
