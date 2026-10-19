using System.CommandLine;
using System.Globalization;
using System.Security.Principal;
using System.Text.Json;
using SystemCompass.Core.Changes;
using SystemCompass.Core.Demo;
using SystemCompass.Core.Reporting;
using SystemCompass.Core.Startup;
using SystemCompass.Core.Tweaks;
using SystemCompass.Windows;

namespace SystemCompass.Cli;

internal static class CliCommands
{
    private static readonly JsonSerializerOptions IntentJson = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static string Version => typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    public static IReadOnlyList<Command> CreateAll() =>
    [
        CreateAudit(),
        CreateReport(),
        CreateChange("disable", "Disable one startup entry. The change is verified and journaled.", enabled: false),
        CreateChange("enable", "Re-enable one startup entry. The change is verified and journaled.", enabled: true),
        CreateRollback(),
        CreateTweak(),
        CreateData(),
        CreateApplyIntent(),
    ];

    private static Command CreateAudit()
    {
        var json = new Option<bool>("--json") { Description = "Write JSON instead of a table." };
        var all = new Option<bool>("--all") { Description = "Include Windows system-critical entries in the listing." };
        var demo = new Option<bool>("--demo") { Description = "Use fictional demo data; nothing is read from this machine." };
        var dataDir = new Option<string>("--data-dir") { Description = "Directory for the journal and snapshots." };
        var command = new Command("audit", "List startup entries from every supported mechanism.");
        command.Options.Add(json);
        command.Options.Add(all);
        command.Options.Add(demo);
        command.Options.Add(dataDir);
        command.SetAction(parse => RunAudit(
            parse.GetValue(json), parse.GetValue(all), parse.GetValue(demo), parse.GetValue(dataDir)));
        return command;
    }

    private static Command CreateReport()
    {
        var json = new Option<bool>("--json") { Description = "Write JSON instead of Markdown." };
        var output = new Option<string>("--output") { Description = "Write the redacted report to this file." };
        var demo = new Option<bool>("--demo") { Description = "Use fictional demo data; nothing is read from this machine." };
        var dataDir = new Option<string>("--data-dir") { Description = "Directory for the journal and snapshots." };
        var command = new Command("report", "Generate a redacted startup and diagnostics report.");
        command.Options.Add(json);
        command.Options.Add(output);
        command.Options.Add(demo);
        command.Options.Add(dataDir);
        command.SetAction(parse => RunReport(
            parse.GetValue(json), parse.GetValue(output), parse.GetValue(demo), parse.GetValue(dataDir)));
        return command;
    }

    private static Command CreateChange(string name, string description, bool enabled)
    {
        var id = new Argument<string>("id") { Description = "Entry id shown by 'syscompass audit'." };
        var demo = new Option<bool>("--demo") { Description = "Demo mode is read-only." };
        var dataDir = new Option<string>("--data-dir") { Description = "Directory for the journal and snapshots." };
        var command = new Command(name, description);
        command.Arguments.Add(id);
        command.Options.Add(demo);
        command.Options.Add(dataDir);
        command.SetAction(parse => RunChange(
            parse.GetValue(id) ?? string.Empty,
            enabled,
            parse.GetValue(demo),
            parse.GetValue(dataDir)));
        return command;
    }

    private static Command CreateRollback()
    {
        var id = new Argument<string>("change-id") { Description = "Change id printed by disable or enable." };
        var dataDir = new Option<string>("--data-dir") { Description = "Directory for the journal and snapshots." };
        var command = new Command("rollback", "Restore the state recorded before one verified change.");
        command.Arguments.Add(id);
        command.Options.Add(dataDir);
        command.SetAction(parse => RunRollback(
            parse.GetValue(id) ?? string.Empty, parse.GetValue(dataDir)));
        return command;
    }

    private static Command CreateTweak()
    {
        var parent = new Command("tweak", "Inspect, apply and revert catalog tweaks.");

        var listJson = new Option<bool>("--json") { Description = "Write JSON instead of a table." };
        var listDemo = new Option<bool>("--demo") { Description = "Demo mode does not read tweak state from this machine." };
        var list = new Command("list", "List catalog tweaks with their current status.");
        list.Options.Add(listJson);
        list.Options.Add(listDemo);
        list.SetAction(parse => RunTweakList(parse.GetValue(listJson), parse.GetValue(listDemo)));

        var applyId = new Argument<string>("id") { Description = "Tweak id shown by 'syscompass tweak list'." };
        var applyDemo = new Option<bool>("--demo") { Description = "Demo mode is read-only." };
        var dataDir = new Option<string>("--data-dir") { Description = "Directory for the journal and snapshots." };
        var apply = new Command("apply", "Apply one tweak with a captured previous state.");
        apply.Arguments.Add(applyId);
        apply.Options.Add(applyDemo);
        apply.Options.Add(dataDir);
        apply.SetAction(parse => RunTweakApply(
            parse.GetValue(applyId) ?? string.Empty, parse.GetValue(applyDemo), parse.GetValue(dataDir)));

        var revertId = new Argument<string>("id") { Description = "Tweak id to revert." };
        var revertDemo = new Option<bool>("--demo") { Description = "Demo mode is read-only." };
        var revertDataDir = new Option<string>("--data-dir") { Description = "Directory for the journal and snapshots." };
        var revert = new Command("revert", "Restore the values recorded by an earlier apply.");
        revert.Arguments.Add(revertId);
        revert.Options.Add(revertDemo);
        revert.Options.Add(revertDataDir);
        revert.SetAction(parse => RunTweakRevert(
            parse.GetValue(revertId) ?? string.Empty, parse.GetValue(revertDemo), parse.GetValue(revertDataDir)));

        parent.Subcommands.Add(list);
        parent.Subcommands.Add(apply);
        parent.Subcommands.Add(revert);
        return parent;
    }

    private static Command CreateData()
    {
        var parent = new Command("data", "Export or delete locally stored data.");

        var output = new Option<string>("--output") { Description = "Destination file for the redacted report." };
        var export = new Command("export", "Write a redacted report of locally stored data.");
        export.Options.Add(output);
        export.SetAction(parse => RunDataExport(parse.GetValue(output)));

        var yes = new Option<bool>("--yes") { Description = "Confirm deletion of the local data directory." };
        var dataDir = new Option<string>("--data-dir") { Description = "Directory to delete." };
        var delete = new Command("delete", "Delete the local journal and snapshots.");
        delete.Options.Add(yes);
        delete.Options.Add(dataDir);
        delete.SetAction(parse => RunDataDelete(parse.GetValue(yes), parse.GetValue(dataDir)));

        parent.Subcommands.Add(export);
        parent.Subcommands.Add(delete);
        return parent;
    }

    private static Command CreateApplyIntent()
    {
        var path = new Argument<string>("path") { Description = "Intent file written by the desktop app." };
        var sid = new Argument<string>("requester-sid") { Description = "SID of the user that requested the change." };
        var dataDir = new Option<string>("--data-dir") { Description = "Directory for the journal and snapshots." };
        var command = new Command(
            "apply-intent",
            "Internal elevated helper: applies a batch of startup changes requested by the app.");
        command.Arguments.Add(path);
        command.Arguments.Add(sid);
        command.Options.Add(dataDir);
        command.SetAction(parse => RunApplyIntent(
            parse.GetValue(path) ?? string.Empty,
            parse.GetValue(sid) ?? string.Empty,
            parse.GetValue(dataDir)));
        return command;
    }

    private static int RunAudit(bool json, bool all, bool demo, string? dataDir)
    {
        return Guard(() =>
        {
            if (demo)
            {
                var demoReport = DemoDataSet.CreateReport(DateTimeOffset.UtcNow, Version);
                WriteAudit(demoReport.StartupEntries, json, all,
                    new ReportBuilder(DemoDataSet.CreateRedactor()), demoReport);
                return 0;
            }

            var services = SystemServices.Create(dataDir);
            var entries = services.Audit.EnumerateAll();
            var report = services.Diagnostics.Create(
                entries, JournalView.LatestPerChange(services.Journal.ReadAll()), Version);
            WriteAudit(entries, json, all, services.Reports, report);
            return 0;
        });
    }

    private static void WriteAudit(
        IReadOnlyList<StartupEntry> entries,
        bool json,
        bool all,
        ReportBuilder builder,
        DiagnosticsReport report)
    {
        var visible = all
            ? entries
            : entries.Where(entry => entry.Assessment.Category != EntryCategory.SystemCritical).ToArray();
        var hidden = entries.Count - visible.Count;

        if (json)
        {
            Console.WriteLine(builder.BuildJson(report with { StartupEntries = visible }));
            return;
        }

        Console.WriteLine($"{"ID",-16} {"STATE",-9} {"CATEGORY",-19} {"MECHANISM",-14} NAME");
        foreach (var entry in visible
                     .OrderBy(candidate => candidate.Assessment.Category)
                     .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase))
        {
            var name = entry.Name.Length > 52 ? entry.Name[..49] + "..." : entry.Name;
            var marker = entry.IsProtected ? "  [protected]" : string.Empty;
            Console.WriteLine(
                $"{entry.Id,-16} {entry.State,-9} {entry.Assessment.Category,-19} {entry.Mechanism,-14} {name}{marker}");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Total {entries.Count} | keep {Count(entries, EntryCategory.Keep)} "
            + $"| disable recommended {Count(entries, EntryCategory.DisableRecommended)} "
            + $"| optional {Count(entries, EntryCategory.Optional)} "
            + $"| unclear {Count(entries, EntryCategory.Unclear)} "
            + $"| protected {entries.Count(entry => entry.IsProtected)}");
        if (hidden > 0)
        {
            Console.WriteLine($"{hidden} Windows system-critical entries hidden; use --all to list them.");
        }

        Console.WriteLine("Change one entry with 'syscompass disable <id>' or 'syscompass enable <id>'.");
    }

    private static int Count(IReadOnlyList<StartupEntry> entries, EntryCategory category)
        => entries.Count(entry => entry.Assessment.Category == category);

    private static int RunReport(bool json, string? output, bool demo, string? dataDir)
    {
        return Guard(() =>
        {
            DiagnosticsReport report;
            ReportBuilder builder;
            if (demo)
            {
                report = DemoDataSet.CreateReport(DateTimeOffset.UtcNow, Version);
                builder = new ReportBuilder(DemoDataSet.CreateRedactor());
            }
            else
            {
                var services = SystemServices.Create(dataDir);
                var entries = services.Audit.EnumerateAll();
                report = services.Diagnostics.Create(
                    entries, JournalView.LatestPerChange(services.Journal.ReadAll()), Version);
                builder = services.Reports;
            }

            var text = json ? builder.BuildJson(report) : builder.BuildMarkdown(report);
            if (string.IsNullOrWhiteSpace(output))
            {
                Console.Write(text);
                return 0;
            }

            var path = Path.GetFullPath(output);
            File.WriteAllText(path, text);
            Console.WriteLine($"Redacted report written to {path}");
            return 0;
        });
    }

    private static int RunChange(string entryId, bool enabled, bool demo, string? dataDir)
    {
        if (demo)
        {
            Console.Error.WriteLine("error: demo mode is read-only.");
            return 1;
        }

        return Guard(() =>
        {
            var services = SystemServices.Create(dataDir);
            var entry = services.Audit.Find(entryId)
                ?? throw new InvalidOperationException($"Unknown entry '{entryId}'.");
            var result = services.Changes.SetState(entry, enabled);
            Console.WriteLine(result.Message);
            if (result.Success)
            {
                Console.WriteLine($"Change id: {result.ChangeId}");
            }
            else
            {
                Console.WriteLine($"Observed state: {result.ObservedState}");
            }

            return result.Success ? 0 : 1;
        });
    }

    private static int RunRollback(string changeId, string? dataDir)
    {
        return Guard(() =>
        {
            var services = SystemServices.Create(dataDir);
            var result = services.Changes.Rollback(changeId);
            Console.WriteLine(result.Message);
            return result.Success ? 0 : 1;
        });
    }

    private static int RunTweakList(bool json, bool demo)
    {
        return Guard(() =>
        {
            var catalog = TweakCatalog.FromEmbedded();
            var problems = catalog.Validate();
            if (problems.Count > 0)
            {
                foreach (var problem in problems)
                {
                    Console.Error.WriteLine($"catalog warning: {problem}");
                }
            }

            var engine = new TweakEngine(new WindowsTweaks(), new InMemoryJournal(), TimeProvider.System);
            var build = Environment.OSVersion.Version.Build;
            var rows = catalog.Tweaks
                .Select(tweak => new
                {
                    tweak.Id,
                    tweak.Title,
                    tweak.Category,
                    Evidence = tweak.Evidence.ToString(),
                    Risk = tweak.Risk.ToString(),
                    Status = demo
                        ? "NotEvaluated"
                        : engine.GetStatus(tweak, build).ToString(),
                    tweak.RequiresElevation,
                    tweak.RequiresReboot,
                    Operations = tweak.Operations.Select(operation => operation.Describe()).ToArray(),
                })
                .ToArray();

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(rows, IntentJson));
                return 0;
            }

            Console.WriteLine($"{"ID",-34} {"STATUS",-14} {"RISK",-7} {"EVIDENCE",-20} TITLE");
            foreach (var row in rows)
            {
                Console.WriteLine($"{row.Id,-34} {row.Status,-14} {row.Risk,-7} {row.Evidence,-20} {row.Title}");
            }

            Console.WriteLine();
            Console.WriteLine("Apply with 'syscompass tweak apply <id>'. Revert with 'syscompass tweak revert <id>'.");
            return 0;
        });
    }

    private static int RunTweakApply(string tweakId, bool demo, string? dataDir)
    {
        if (demo)
        {
            Console.Error.WriteLine("error: demo mode is read-only.");
            return 1;
        }

        return Guard(() =>
        {
            var services = SystemServices.Create(dataDir);
            var tweak = FindTweak(services.Catalog, tweakId);
            var result = services.Tweaks.Apply(tweak, Environment.OSVersion.Version.Build);
            foreach (var step in result.Steps)
            {
                Console.WriteLine($"ok  {step}");
            }

            if (!result.Success)
            {
                Console.Error.WriteLine($"error: {result.Error}");
                return 1;
            }

            Console.WriteLine($"Applied '{tweak.Title}'.");
            return 0;
        });
    }

    private static int RunTweakRevert(string tweakId, bool demo, string? dataDir)
    {
        if (demo)
        {
            Console.Error.WriteLine("error: demo mode is read-only.");
            return 1;
        }

        return Guard(() =>
        {
            var services = SystemServices.Create(dataDir);
            var tweak = FindTweak(services.Catalog, tweakId);
            var result = services.Tweaks.Revert(tweak);
            foreach (var step in result.Steps)
            {
                Console.WriteLine(step);
            }

            if (!result.Success)
            {
                Console.Error.WriteLine($"error: {result.Error}");
                return 1;
            }

            Console.WriteLine($"Reverted '{tweak.Title}'.");
            return 0;
        });
    }

    private static TweakDefinition FindTweak(TweakCatalog catalog, string tweakId)
        => catalog.Tweaks.FirstOrDefault(tweak =>
               tweak.Id.Equals(tweakId, StringComparison.OrdinalIgnoreCase))
           ?? throw new InvalidOperationException($"Unknown tweak '{tweakId}'.");

    private static int RunDataExport(string? output)
    {
        return Guard(() =>
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                Console.Error.WriteLine("error: --output is required.");
                return 1;
            }

            var services = SystemServices.Create(null);
            var report = services.Diagnostics.Create(
                services.Audit.EnumerateAll(),
                JournalView.LatestPerChange(services.Journal.ReadAll()),
                Version);
            var path = Path.GetFullPath(output);
            File.WriteAllText(path, services.Reports.BuildMarkdown(report));
            Console.WriteLine($"Redacted report written to {path}");
            return 0;
        });
    }

    private static int RunDataDelete(bool yes, string? dataDir)
    {
        var directory = dataDir
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SystemCompass");
        var fullPath = Path.GetFullPath(directory);
        if (!yes)
        {
            Console.Error.WriteLine(
                $"Refusing to delete '{fullPath}'. Pass --yes to confirm.");
            return 1;
        }

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
            Console.WriteLine($"Deleted {fullPath}");
        }
        else
        {
            Console.WriteLine($"Nothing to delete at {fullPath}");
        }

        return 0;
    }

    private static int RunApplyIntent(string path, string requesterSid, string? dataDir)
    {
        return Guard(() =>
        {
            var fullPath = Path.GetFullPath(path);
            var file = new FileInfo(fullPath);
            if (!file.Exists)
            {
                throw new InvalidOperationException("The intent file does not exist.");
            }

            if (file.Length > 1_000_000)
            {
                throw new InvalidOperationException("The intent file exceeds the size limit.");
            }

            using var identity = WindowsIdentity.GetCurrent();
            var currentSid = identity.User?.Value;
            if (!string.Equals(currentSid, requesterSid, StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine(
                    "error: the elevated token belongs to a different user than the requester.");
                return 2;
            }

            var intent = JsonSerializer.Deserialize<ChangeIntent>(File.ReadAllText(fullPath), IntentJson)
                ?? throw new InvalidOperationException("The intent file is empty.");
            if (intent.Entries.Count == 0 || intent.Entries.Count > 100)
            {
                throw new InvalidOperationException("The intent file contains an invalid number of entries.");
            }

            var services = SystemServices.Create(dataDir);
            var results = new List<IntentResult>();
            foreach (var item in intent.Entries)
            {
                var entry = services.Audit.Find(item.EntryId);
                if (entry is null)
                {
                    results.Add(new IntentResult(item.EntryId, false, "Unknown entry."));
                    continue;
                }

                var result = services.Changes.SetState(entry, item.Enabled);
                results.Add(new IntentResult(item.EntryId, result.Success, result.Message));
            }

            File.WriteAllText(
                fullPath + ".result.json",
                JsonSerializer.Serialize(new IntentResultFile(results), IntentJson));
            return results.All(result => result.Success) ? 0 : 1;
        });
    }

    private static int Guard(Func<int> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or UnauthorizedAccessException
            or IOException
            or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }
}
