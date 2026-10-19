using SystemCompass.Core.Changes;
using SystemCompass.Core.Startup;

namespace SystemCompass.Core.Reporting;

public sealed record DiagnosticsItem(string Name, string Value);

public sealed record DiagnosticsSection(string Title, IReadOnlyList<DiagnosticsItem> Items);

public sealed record BootMeasurement(
    bool Available,
    DateTimeOffset? LastBootEvent,
    double? Seconds,
    string Source);

public sealed record DiagnosticsReport(
    DateTimeOffset GeneratedAt,
    string AppVersion,
    string OsDescription,
    IReadOnlyList<DiagnosticsSection> Sections,
    IReadOnlyList<StartupEntry> StartupEntries,
    IReadOnlyList<JournalRecord> Journal,
    BootMeasurement Boot);
