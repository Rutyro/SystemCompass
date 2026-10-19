using SystemCompass.Core.Changes;
using SystemCompass.Core.Redaction;
using SystemCompass.Core.Reporting;
using SystemCompass.Core.Startup;
using SystemCompass.Core.Tweaks;

namespace SystemCompass.Windows;

/// <summary>
/// Composition root shared by the CLI and the desktop app. Every provider is
/// constructed here so both front ends audit the same mechanisms.
/// </summary>
public sealed class SystemServices
{
    private SystemServices(string dataDirectory)
    {
        DataDirectory = dataDirectory;
        Classifier = Classifier.FromEmbeddedRules();
        Signatures = new SignatureVerifier();
        Journal = new JsonLinesJournal(Path.Combine(dataDirectory, "journal.jsonl"));
        Providers =
        [
            new RunKeyProvider(Classifier, Signatures, TimeProvider.System),
            new StartupFolderProvider(Classifier, Signatures, TimeProvider.System),
            new ScheduledTaskProvider(Classifier, Signatures),
            new ServiceProvider(Classifier, Signatures),
            new WinlogonProvider(Classifier, Signatures),
        ];
        Audit = new StartupAuditService(Providers);
        Changes = new ChangeExecutor(Providers, Journal, TimeProvider.System);
        Catalog = TweakCatalog.FromEmbedded();
        Tweaks = new TweakEngine(new WindowsTweakBackend(), Journal, TimeProvider.System);
        Diagnostics = new WindowsDiagnostics();
        Redactor = Redactor.CreateForCurrentUser();
        Reports = new ReportBuilder(Redactor);
    }

    public string DataDirectory { get; }

    public Classifier Classifier { get; }

    public SignatureVerifier Signatures { get; }

    public IReadOnlyList<IStartupProvider> Providers { get; }

    public StartupAuditService Audit { get; }

    public ChangeExecutor Changes { get; }

    public TweakCatalog Catalog { get; }

    public TweakEngine Tweaks { get; }

    public WindowsDiagnostics Diagnostics { get; }

    public Redactor Redactor { get; }

    public ReportBuilder Reports { get; }

    public IJournalStore Journal { get; }

    public static SystemServices Create(string? dataDirectory = null)
    {
        var directory = dataDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SystemCompass");
        return new SystemServices(directory);
    }
}
