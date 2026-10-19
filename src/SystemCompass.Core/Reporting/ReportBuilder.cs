using System.Globalization;
using System.Text;
using System.Text.Json;
using SystemCompass.Core.Redaction;

namespace SystemCompass.Core.Reporting;

public sealed class ReportBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Redactor _redactor;

    public ReportBuilder(Redactor redactor)
    {
        _redactor = redactor;
    }

    public string BuildMarkdown(DiagnosticsReport report)
    {
        var text = new StringBuilder();
        text.AppendLine("# SystemCompass startup audit report");
        text.AppendLine();
        text.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC"));
        text.AppendLine();
        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"App version: {report.AppVersion}"));
        text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"OS: {_redactor.Redact(report.OsDescription)}"));
        text.AppendLine();

        foreach (var section in report.Sections)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"## {_redactor.Redact(section.Title)}"));
            text.AppendLine();
            text.AppendLine("| Item | Value |");
            text.AppendLine("|---|---|");
            foreach (var item in section.Items)
            {
                text.AppendLine($"| {Escape(_redactor.Redact(item.Name))} | {Escape(_redactor.Redact(item.Value))} |");
            }

            text.AppendLine();
        }

        text.AppendLine("## Boot measurement");
        text.AppendLine();
        if (report.Boot.Available)
        {
            text.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"- Last boot event: {report.Boot.LastBootEvent:yyyy-MM-dd HH:mm:ss}"));
            text.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"- Boot duration: {report.Boot.Seconds:F1} s (single sample, not a benchmark)"));
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- Source: {report.Boot.Source}"));
        }
        else
        {
            text.AppendLine("- Not available. Boot measurements require administrator rights or the event log to be readable.");
        }

        text.AppendLine();
        text.AppendLine("## Startup entries");
        text.AppendLine();
        text.AppendLine("| Name | Mechanism | Scope | State | Category | Risk | Signature | Command |");
        text.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var entry in report.StartupEntries)
        {
            text.AppendLine(
                $"| {Escape(_redactor.Redact(entry.Name))} | {entry.Mechanism} | {entry.Scope} | {entry.State} "
                + $"| {entry.Assessment.Category} | {entry.Assessment.Risk} | {entry.Signature.Status} "
                + $"| {Escape(_redactor.Redact(entry.CommandLine))} |");
        }

        text.AppendLine();
        text.AppendLine("## Recent changes");
        text.AppendLine();
        text.AppendLine("| Time | Entry | Action | Status |");
        text.AppendLine("|---|---|---|---|");
        foreach (var record in report.Journal)
        {
            text.AppendLine($"| {record.Timestamp:yyyy-MM-dd HH:mm:ss} | {Escape(_redactor.Redact(record.EntryName))} | {Escape(_redactor.Redact(record.Action))} | {record.Status} |");
        }

        text.AppendLine();
        text.AppendLine("_Names, user-profile paths, machine names, SIDs and non-system drive paths are redacted._");
        return text.ToString().TrimEnd() + Environment.NewLine;
    }

    public string BuildJson(DiagnosticsReport report)
    {
        var dto = new
        {
            generatedAt = report.GeneratedAt,
            appVersion = report.AppVersion,
            os = _redactor.Redact(report.OsDescription),
            boot = new
            {
                available = report.Boot.Available,
                lastBootEvent = report.Boot.LastBootEvent,
                seconds = report.Boot.Seconds,
                source = report.Boot.Source,
            },
            sections = report.Sections.Select(section => new
            {
                title = _redactor.Redact(section.Title),
                items = section.Items.Select(item => new
                {
                    name = _redactor.Redact(item.Name),
                    value = _redactor.Redact(item.Value),
                }),
            }),
            startup = report.StartupEntries.Select(entry => new
            {
                id = entry.Id,
                name = _redactor.Redact(entry.Name),
                mechanism = entry.Mechanism.ToString(),
                scope = entry.Scope.ToString(),
                state = entry.State.ToString(),
                category = entry.Assessment.Category.ToString(),
                risk = entry.Assessment.Risk.ToString(),
                signature = entry.Signature.Status.ToString(),
                signer = _redactor.Redact(entry.Signature.Signer),
                command = _redactor.Redact(entry.CommandLine),
                path = _redactor.Redact(entry.ResolvedPath),
            }),
            changes = report.Journal.Select(record => new
            {
                changeId = record.ChangeId,
                timestamp = record.Timestamp,
                entry = _redactor.Redact(record.EntryName),
                action = _redactor.Redact(record.Action),
                status = record.Status.ToString(),
                error = _redactor.Redact(record.Error),
            }),
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "—";
        }

        return value
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }
}
