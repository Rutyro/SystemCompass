using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using Microsoft.Win32;
using SystemCompass.Core.Changes;
using SystemCompass.Core.Reporting;
using SystemCompass.Core.Startup;

namespace SystemCompass.Windows;

public sealed class WindowsDiagnostics
{
    private static readonly Dictionary<Guid, string> KnownPowerPlans = new()
    {
        [new Guid("381b4222-f694-41f0-9685-ff5bb260df2e")] = "Balanced",
        [new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c")] = "High performance",
        [new Guid("a1841308-3541-4fab-bc81-f71556f20b4a")] = "Power saver",
    };

    public DiagnosticsReport Create(
        IReadOnlyList<StartupEntry> entries,
        IReadOnlyList<JournalRecord> journal,
        string appVersion)
    {
        var now = DateTimeOffset.UtcNow;
        var sections = new[]
        {
            new DiagnosticsSection("System", [
                new DiagnosticsItem("OS", GetOsDescription()),
                new DiagnosticsItem("Power plan", GetPowerPlan()),
                new DiagnosticsItem("Game Mode", GetDword(
                    Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled") switch
                {
                    1 => "On",
                    0 => "Off",
                    _ => "Not configured",
                }),
                new DiagnosticsItem("Hardware-accelerated GPU scheduling", GetDword(
                    Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode") switch
                {
                    2 => "On",
                    1 => "Off",
                    _ => "Not configured",
                }),
                new DiagnosticsItem("Virtualization-based security", GetDword(
                    Registry.LocalMachine,
                    @"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity") switch
                {
                    1 => "Enabled",
                    0 => "Disabled",
                    _ => "Not configured",
                }),
                new DiagnosticsItem("Secure Boot", GetDword(
                    Registry.LocalMachine,
                    @"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled") switch
                {
                    1 => "On",
                    0 => "Off",
                    _ => "Unavailable",
                }),
            ]),
            new DiagnosticsSection("Startup summary", [
                new DiagnosticsItem("Entries found", entries.Count.ToString(CultureInfo.InvariantCulture)),
                new DiagnosticsItem("Enabled",
                    entries.Count(entry => entry.State == EntryState.Enabled).ToString(CultureInfo.InvariantCulture)),
                new DiagnosticsItem("Disabled",
                    entries.Count(entry => entry.State == EntryState.Disabled).ToString(CultureInfo.InvariantCulture)),
                new DiagnosticsItem("Protected",
                    entries.Count(entry => entry.IsProtected).ToString(CultureInfo.InvariantCulture)),
                new DiagnosticsItem("Unclear",
                    entries.Count(entry => entry.Assessment.Category == EntryCategory.Unclear)
                        .ToString(CultureInfo.InvariantCulture)),
            ]),
            new DiagnosticsSection("Privacy", [
                new DiagnosticsItem("Network requests", "None"),
                new DiagnosticsItem("Telemetry", "None"),
                new DiagnosticsItem("Report redaction", "User names, profile paths, machine names, SIDs, non-system drives"),
            ]),
        };

        return new DiagnosticsReport(
            now,
            appVersion,
            GetOsDescription(),
            sections,
            entries,
            journal,
            ReadBootMeasurement());
    }

    private static string GetOsDescription()
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var product = key?.GetValue("ProductName") as string ?? "Windows";
        var display = key?.GetValue("DisplayVersion") as string;
        var build = key?.GetValue("CurrentBuildNumber") as string
            ?? Environment.OSVersion.Version.Build.ToString(CultureInfo.InvariantCulture);
        var revision = key?.GetValue("UBR");
        if (int.TryParse(build, NumberStyles.Integer, CultureInfo.InvariantCulture, out var buildNumber)
            && buildNumber >= 22000
            && product.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase))
        {
            // Windows 11 still stores ProductName = "Windows 10 Pro".
            product = product.Replace("Windows 10", "Windows 11", StringComparison.OrdinalIgnoreCase);
        }

        var buildLabel = revision is null ? build : $"{build}.{revision}";
        return string.IsNullOrWhiteSpace(display)
            ? $"{product} (build {buildLabel})"
            : $"{product} {display} (build {buildLabel})";
    }

    private static string GetPowerPlan()
    {
        var scheme = PowerNative.GetActiveScheme();
        if (scheme is null)
        {
            return "Unavailable";
        }

        return KnownPowerPlans.TryGetValue(scheme.Value, out var name)
            ? string.Create(CultureInfo.InvariantCulture, $"{name} ({scheme.Value})")
            : scheme.Value.ToString();
    }

    private static int? GetDword(RegistryKey root, string path, string valueName)
    {
        using var key = root.OpenSubKey(path);
        return key?.GetValue(valueName) as int?;
    }

    private static BootMeasurement ReadBootMeasurement()
    {
        try
        {
            var startInfo = new ProcessStartInfo("wevtutil.exe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var argument in new[]
            {
                "qe",
                "Microsoft-Windows-Diagnostics-Performance/Operational",
                "/q:*[System[(EventID=100)]]",
                "/c:1",
                "/rd:true",
                "/f:xml",
            })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return UnavailableBoot();
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return UnavailableBoot();
            }

            var document = XDocument.Parse(output);
            var bootTime = document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Data"
                    && (string?)element.Attribute("Name") == "BootTime")
                ?.Value;
            var timestamp = document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "TimeCreated")
                ?.Attribute("SystemTime")?.Value;

            if (bootTime is null
                || !double.TryParse(bootTime, CultureInfo.InvariantCulture, out var milliseconds)
                || timestamp is null
                || !DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time))
            {
                return UnavailableBoot();
            }

            return new BootMeasurement(
                true,
                time,
                milliseconds / 1000.0,
                "Event 100, Diagnostics-Performance (single sample, not a benchmark)");
        }
        catch (Exception ex) when (ex is System.Xml.XmlException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            return UnavailableBoot();
        }
    }

    private static BootMeasurement UnavailableBoot()
        => new(false, null, null,
            "Event 100 unavailable. Administrator rights or event log access are required.");
}
