using SystemCompass.Core.Changes;
using SystemCompass.Core.Redaction;
using SystemCompass.Core.Reporting;
using SystemCompass.Core.Startup;

namespace SystemCompass.Core.Demo;

/// <summary>
/// Entirely fictional data used for screenshots and the sample report. No real
/// entry from the development machine appears here.
/// </summary>
public static class DemoDataSet
{
    public static Redactor CreateRedactor()
    {
        return new Redactor(new RedactionOptions
        {
            UserName = "demo-user",
            UserProfilePath = @"C:\Users\demo-user",
            MachineName = "COMPASS-DEMO",
            ExtraPrefixes = [@"H:\Company"],
        });
    }

    public static DiagnosticsReport CreateReport(DateTimeOffset now, string appVersion)
    {
        var entries = CreateEntries();
        var journal = new[]
        {
            new JournalRecord(
                "demo-change-1", now.AddMinutes(-12), entries[1].Id, entries[1].Name,
                "ScheduledTask", "Disable", "Enabled", "Disabled", ChangeStatus.Verified),
            new JournalRecord(
                "demo-change-2", now.AddMinutes(-4), entries[5].Id, entries[5].Name,
                "StartupFolder", "Enable", "Disabled", "Enabled", ChangeStatus.Verified),
        };

        var sections = new[]
        {
            new DiagnosticsSection("System", [
                new DiagnosticsItem("OS", "Windows 11 Pro (fictional demo build 26100)"),
                new DiagnosticsItem("Power plan", "Balanced"),
                new DiagnosticsItem("Game Mode", "On"),
                new DiagnosticsItem("Hardware-accelerated GPU scheduling", "Available"),
                new DiagnosticsItem("Virtualization-based security", "Running"),
                new DiagnosticsItem("Secure Boot", "On"),
            ]),
            new DiagnosticsSection("Startup summary", [
                new DiagnosticsItem("Entries found", entries.Length.ToString()),
                new DiagnosticsItem("Enabled", entries.Count(entry => entry.State == EntryState.Enabled).ToString()),
                new DiagnosticsItem("Disabled", entries.Count(entry => entry.State == EntryState.Disabled).ToString()),
                new DiagnosticsItem("Protected", entries.Count(entry => entry.IsProtected).ToString()),
                new DiagnosticsItem("Unclear", entries.Count(entry => entry.Assessment.Category == EntryCategory.Unclear).ToString()),
            ]),
            new DiagnosticsSection("Privacy", [
                new DiagnosticsItem("Network requests", "None"),
                new DiagnosticsItem("Telemetry", "None"),
                new DiagnosticsItem("Export redaction", "User names, profile paths, machine names, SIDs, non-system drives"),
            ]),
        };

        var boot = new BootMeasurement(
            true,
            now.AddHours(-3),
            18.4,
            "Event 100, Diagnostics-Performance (single sample, not a benchmark)");

        return new DiagnosticsReport(
            now,
            appVersion,
            "Windows 11 Pro (fictional demo build 26100)",
            sections,
            entries,
            journal,
            boot);
    }

    private static StartupEntry[] CreateEntries()
    {
        return
        [
            Entry(
                "Contoso Cloud Sync", @"C:\Users\demo-user\AppData\Local\Contoso\sync.exe", EntryState.Enabled,
                StartupMechanism.RunKey, StartupScope.CurrentUser, EntryCategory.DisableRecommended, RiskLevel.Low,
                new SignatureInfo(SignatureStatus.Valid, "Contoso Ltd"), @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run",
                @"C:\Users\demo-user\AppData\Local\Contoso\sync.exe --background",
                "Background sync client starts at sign-in; no visible window.",
                "Re-enable in Task Manager.", false),
            Entry(
                "Northwind Update Helper", @"C:\Program Files\Northwind\updater.exe", EntryState.Disabled,
                StartupMechanism.ScheduledTask, StartupScope.AllUsers, EntryCategory.DisableRecommended, RiskLevel.Low,
                new SignatureInfo(SignatureStatus.Valid, "Northwind Software Inc."), @"\Northwind\Update",
                @"C:\Program Files\Northwind\updater.exe /silent",
                "Vendor updater runs at sign-in and can run on demand.",
                "Re-enable in Task Scheduler.", false),
            Entry(
                "Windows Security notification icon", @"C:\Windows\System32\SecurityHealthSystray.exe", EntryState.Enabled,
                StartupMechanism.RunKey, StartupScope.AllUsers, EntryCategory.SecurityRelevant, RiskLevel.Low,
                new SignatureInfo(SignatureStatus.ValidCatalog, "Microsoft Windows"), @"HKLM\Software\Microsoft\Windows\CurrentVersion\Run",
                @"C:\Windows\System32\SecurityHealthSystray.exe",
                "Windows Security status icon.", "Protected. Do not disable.", true),
            Entry(
                "Remote Support Agent", @"C:\Program Files\Example Remote\agent.exe", EntryState.Enabled,
                StartupMechanism.Service, StartupScope.System, EntryCategory.SecurityRelevant, RiskLevel.Medium,
                new SignatureInfo(SignatureStatus.Valid, "Example Remote GmbH"), @"Service: ExampleRemoteAgent",
                @"""C:\Program Files\Example Remote\agent.exe"" --service",
                "Remote-support service explicitly retained.", "Protected. Do not disable.", true),
            Entry(
                "Contoso Audio Service", @"C:\Windows\System32\DriverStore\FileRepository\contoso_audio\ContosoAudio.exe", EntryState.Enabled,
                StartupMechanism.Service, StartupScope.System, EntryCategory.SystemCritical, RiskLevel.High,
                new SignatureInfo(SignatureStatus.Valid, "Contoso Ltd"), @"Service: ContosoAudio",
                @"C:\Windows\System32\DriverStore\FileRepository\contoso_audio\ContosoAudio.exe",
                "Audio driver companion service.", "Protected. Change only through vendor tooling.", true),
            Entry(
                "Studio Notes", @"C:\Users\demo-user\AppData\Roaming\Studio\notes.exe", EntryState.Enabled,
                StartupMechanism.StartupFolder, StartupScope.CurrentUser, EntryCategory.Optional, RiskLevel.Low,
                new SignatureInfo(SignatureStatus.Unsigned), @"Startup folder: user",
                @"C:\Users\demo-user\AppData\Roaming\Studio\notes.exe /minimized",
                "Notes app can start on demand.", "Re-enable in Task Manager.", false),
            Entry(
                "Hardware Vendor Driver", @"C:\Windows\System32\drivers\vendor.sys", EntryState.Enabled,
                StartupMechanism.Service, StartupScope.System, EntryCategory.SystemCritical, RiskLevel.High,
                new SignatureInfo(SignatureStatus.Valid, "Vendor Devices"), @"Service: VendorDriver",
                @"C:\Windows\System32\drivers\vendor.sys",
                "Kernel driver service.", "Protected. Do not change.", true),
            Entry(
                "Zoom Meetings", @"C:\Users\demo-user\AppData\Roaming\Zoom\bin\Zoom.exe", EntryState.Enabled,
                StartupMechanism.RunOnceKey, StartupScope.CurrentUser, EntryCategory.Unclear, RiskLevel.Medium,
                new SignatureInfo(SignatureStatus.Valid, "Zoom Video Communications, Inc."), @"HKCU\Software\Microsoft\Windows\CurrentVersion\RunOnce",
                @"C:\Users\demo-user\AppData\Roaming\Zoom\bin\Zoom.exe --autostart",
                "No classification rule matched. Review before changing.", "RunOnce entries are read-only.", false),
        ];
    }

    private static StartupEntry Entry(
        string name,
        string path,
        EntryState state,
        StartupMechanism mechanism,
        StartupScope scope,
        EntryCategory category,
        RiskLevel risk,
        SignatureInfo signature,
        string location,
        string commandLine,
        string reason,
        string recovery,
        bool protectedEntry)
    {
        return new StartupEntry(
            StartupEntryId.Create(mechanism, scope, location, name),
            name,
            location,
            mechanism,
            scope,
            state,
            commandLine,
            path,
            signature,
            new Assessment(category, risk, reason, recovery, protectedEntry),
            RequiresElevation: mechanism is StartupMechanism.Service or StartupMechanism.ScheduledTask);
    }
}
