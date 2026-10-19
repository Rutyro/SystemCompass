using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SystemCompass.Core.Changes;
using SystemCompass.Core.Demo;
using SystemCompass.Core.Startup;
using SystemCompass.Core.Tweaks;
using SystemCompass.Windows;
using WinColor = System.Windows.Media.Color;

namespace SystemCompass.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private static readonly SolidColorBrush Blue = Make(0x60, 0xA5, 0xFA);
    private static readonly SolidColorBrush Violet = Make(0xA7, 0x8B, 0xFA);
    private static readonly SolidColorBrush Green = Make(0x34, 0xD3, 0x99);
    private static readonly SolidColorBrush Amber = Make(0xFB, 0xBF, 0x24);
    private static readonly SolidColorBrush Red = Make(0xF8, 0x71, 0x71);
    private static readonly SolidColorBrush Cyan = Make(0x22, 0xD3, 0xEE);
    private static readonly SolidColorBrush Pink = Make(0xF4, 0x72, 0xB6);
    private static readonly SolidColorBrush Teal = Make(0x2D, 0xD4, 0xBF);
    private static readonly SolidColorBrush Slate = Make(0x94, 0xA3, 0xB8);
    private static readonly SolidColorBrush Track = Make(0x33, 0xFF, 0xFF, 0xFF);
    private static readonly SolidColorBrush[] Palette =
        [Blue, Violet, Teal, Amber, Pink, Green, Cyan, Red, Slate];

    private readonly SystemServices _services;
    private readonly bool _demo;
    private readonly Action<string>? _navigate;
    private IReadOnlyList<StartupEntry> _entries = [];

    public DashboardViewModel(SystemServices services, bool demo, Action<string>? navigate = null)
    {
        _services = services;
        _demo = demo;
        _navigate = navigate;
        StatusMessage = string.Empty;
        CatalogWarning = string.Empty;
        CategorySlices = [];
        SignatureSlices = [];
        MechanismBars = [];
        CatalogBars = [];
        ScoreSlices = [];
        ActivityValues = [];
        RecommendationRows = [];
        RecentChanges = [];
        ActivityRange = string.Empty;
        HygieneSummary = string.Empty;
        AuditSummary = string.Empty;
        ActivitySummary = string.Empty;
        EnabledLabel = "0";
        TotalEntriesLabel = "0";
        RecommendationsLabel = "0";
        ProtectedLabel = "0";
        CatalogLabel = "0";
        DocumentedLabel = "0";
        UnsignedLabel = "0";
        ScoreLabel = "0";
        RecommendationCaption = string.Empty;
        CatalogCaption = string.Empty;
        TopRecommendations = 5;
        ScoreBrush = Green;
        Refresh();
    }

    [ObservableProperty] public partial int TotalEntries { get; set; }
    [ObservableProperty] public partial int EnabledEntries { get; set; }
    [ObservableProperty] public partial int RecommendationCount { get; set; }
    [ObservableProperty] public partial int ProtectedCount { get; set; }
    [ObservableProperty] public partial int HygieneScore { get; set; }
    [ObservableProperty] public partial string HygieneSummary { get; set; }
    [ObservableProperty] public partial string AuditSummary { get; set; }
    [ObservableProperty] public partial string ActivitySummary { get; set; }
    [ObservableProperty] public partial string ActivityRange { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; }
    [ObservableProperty] public partial string CatalogWarning { get; set; }
    [ObservableProperty] public partial bool HasCatalogWarning { get; set; }
    [ObservableProperty] public partial bool HasRecommendations { get; set; }
    [ObservableProperty] public partial bool HasRecentChanges { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial Brush ScoreBrush { get; set; }
    [ObservableProperty] public partial string EnabledLabel { get; set; }
    [ObservableProperty] public partial string TotalEntriesLabel { get; set; }
    [ObservableProperty] public partial string RecommendationsLabel { get; set; }
    [ObservableProperty] public partial string ProtectedLabel { get; set; }
    [ObservableProperty] public partial string CatalogLabel { get; set; }
    [ObservableProperty] public partial string DocumentedLabel { get; set; }
    [ObservableProperty] public partial string UnsignedLabel { get; set; }
    [ObservableProperty] public partial string ScoreLabel { get; set; }
    [ObservableProperty] public partial string RecommendationCaption { get; set; }
    [ObservableProperty] public partial string CatalogCaption { get; set; }
    [ObservableProperty] public partial Visibility RecentChangesVisibility { get; set; }
    [ObservableProperty] public partial Visibility EmptyJournalVisibility { get; set; }
    [ObservableProperty] public partial Visibility RecommendationsVisibility { get; set; }
    [ObservableProperty] public partial Visibility EmptyRecommendationsVisibility { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ChartSlice> CategorySlices { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ChartSlice> SignatureSlices { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ChartSlice> ScoreSlices { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ChartBar> MechanismBars { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ChartBar> CatalogBars { get; set; }
    [ObservableProperty] public partial IReadOnlyList<int> ActivityValues { get; set; }
    [ObservableProperty] public partial IReadOnlyList<StartupEntryViewModel> RecommendationRows { get; set; }
    [ObservableProperty] public partial IReadOnlyList<JournalRowViewModel> RecentChanges { get; set; }
    [ObservableProperty] public partial int TopRecommendations { get; set; }

    partial void OnTopRecommendationsChanged(int value) => BuildRecommendations(_entries);

    [RelayCommand]
    private void OpenStartup() => _navigate?.Invoke("startup");

    [RelayCommand]
    private void OpenTweaks() => _navigate?.Invoke("tweaks");

    [RelayCommand]
    private void OpenJournal() => _navigate?.Invoke("journal");

    [RelayCommand]
    public void Refresh()
    {
        IsBusy = true;
        try
        {
            var report = _demo ? DemoDataSet.CreateReport(DateTimeOffset.UtcNow, "0.1.0") : null;
            var entries = _demo ? report!.StartupEntries : _services.Audit.EnumerateAll();
            var records = _demo ? report!.Journal : JournalView.LatestPerChange(_services.Journal.ReadAll());
            _entries = entries;

            BuildStartup(entries);
            BuildSignature(entries);
            BuildScore(entries);
            BuildCatalog();
            BuildJournal(records);
            BuildRecommendations(entries);

            var problems = _services.Catalog.Validate();
            HasCatalogWarning = problems.Count > 0;
            CatalogWarning = string.Join(Environment.NewLine, problems);
            StatusMessage = _demo
                ? "Demo data. No entry on this machine was read."
                : $"Audited {TotalEntries} startup entries at {DateTimeOffset.Now:HH:mm}.";
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or UnauthorizedAccessException
            or IOException
            or System.ComponentModel.Win32Exception)
        {
            StatusMessage = $"Overview failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BuildStartup(IReadOnlyList<StartupEntry> entries)
    {
        TotalEntries = entries.Count;
        EnabledEntries = entries.Count(entry => entry.State == EntryState.Enabled);
        ProtectedCount = entries.Count(entry => entry.IsProtected);
        RecommendationCount = entries.Count(entry =>
            entry.Assessment.Category == EntryCategory.DisableRecommended
            && entry.State == EntryState.Enabled);

        var categories = new (string Label, EntryCategory Category, Brush Brush)[]
        {
            ("Disable recommended", EntryCategory.DisableRecommended, Amber),
            ("Optional", EntryCategory.Optional, Blue),
            ("Unclear", EntryCategory.Unclear, Slate),
            ("Keep", EntryCategory.Keep, Green),
            ("Security relevant", EntryCategory.SecurityRelevant, Violet),
            ("System critical", EntryCategory.SystemCritical, Cyan),
        };
        CategorySlices = categories
            .Select(item => Slice(
                item.Label,
                entries.Count(entry => entry.Assessment.Category == item.Category),
                TotalEntries,
                item.Brush))
            .ToArray();

        var mechanisms = new (string Label, StartupMechanism Mechanism, Brush Brush)[]
        {
            ("Run key", StartupMechanism.RunKey, Blue),
            ("RunOnce", StartupMechanism.RunOnceKey, Cyan),
            ("Startup folder", StartupMechanism.StartupFolder, Teal),
            ("Scheduled task", StartupMechanism.ScheduledTask, Violet),
            ("Service", StartupMechanism.Service, Amber),
            ("Winlogon", StartupMechanism.Winlogon, Pink),
        };
        MechanismBars = Bars(mechanisms
            .Select(item => (item.Label, Count: entries.Count(entry => entry.Mechanism == item.Mechanism), item.Brush))
            .ToArray());

        TotalEntriesLabel = TotalEntries.ToString(CultureInfo.InvariantCulture);
        EnabledLabel = EnabledEntries.ToString(CultureInfo.InvariantCulture);
        RecommendationsLabel = RecommendationCount.ToString(CultureInfo.InvariantCulture);
        ProtectedLabel = ProtectedCount.ToString(CultureInfo.InvariantCulture);
        RecommendationCaption = RecommendationCount == 0
            ? "Nothing to review right now"
            : $"{RecommendationCount} enabled · review the list";
        AuditSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"{TotalEntries} entries · {EnabledEntries} enabled · {ProtectedCount} protected");
    }

    private void BuildSignature(IReadOnlyList<StartupEntry> entries)
    {
        var signatures = new (string Label, SignatureStatus Status, Brush Brush)[]
        {
            ("Signed", SignatureStatus.Valid, Green),
            ("Catalog signed", SignatureStatus.ValidCatalog, Teal),
            ("Unsigned", SignatureStatus.Unsigned, Amber),
            ("Invalid", SignatureStatus.Invalid, Red),
            ("Unknown", SignatureStatus.Unknown, Slate),
        };
        SignatureSlices = signatures
            .Select(item => Slice(
                item.Label,
                entries.Count(entry => entry.Signature.Status == item.Status),
                TotalEntries,
                item.Brush))
            .ToArray();
        var unsigned = entries.Count(entry => entry.Signature.Status == SignatureStatus.Unsigned);
        UnsignedLabel = unsigned.ToString(CultureInfo.InvariantCulture);
    }

    private void BuildScore(IReadOnlyList<StartupEntry> entries)
    {
        var enabled = entries.Where(entry => entry.State == EntryState.Enabled).ToArray();
        var recommendations = enabled.Count(entry =>
            entry.Assessment.Category == EntryCategory.DisableRecommended);
        var unclear = enabled.Count(entry => entry.Assessment.Category == EntryCategory.Unclear);
        var unsigned = enabled.Count(entry => entry.Signature.Status == SignatureStatus.Unsigned);
        var invalid = entries.Count(entry => entry.Signature.Status == SignatureStatus.Invalid);

        var score = 100
            - Math.Min(40, recommendations * 4)
            - Math.Min(10, unclear)
            - Math.Min(10, unsigned * 2)
            - Math.Min(20, invalid * 10);
        HygieneScore = Math.Clamp(score, 0, 100);
        ScoreLabel = HygieneScore.ToString(CultureInfo.InvariantCulture);
        ScoreBrush = HygieneScore >= 85 ? Green : HygieneScore >= 65 ? Amber : Red;
        ScoreSlices =
        [
            new ChartSlice("Score", HygieneScore, HygieneScore / 100d, ScoreLabel,
                HygieneScore.ToString(CultureInfo.InvariantCulture) + "%", ScoreBrush),
            new ChartSlice("Remaining", 100 - HygieneScore, (100 - HygieneScore) / 100d,
                string.Empty, string.Empty, Track),
        ];
        HygieneSummary = recommendations > 0
            ? $"{recommendations} recommended disable{(recommendations == 1 ? string.Empty : "s")} still enabled."
            : unsigned > 0
                ? $"{unsigned} enabled entr{(unsigned == 1 ? "y has" : "ies have")} no verified signature."
                : "No startup recommendations are currently enabled.";
    }

    private void BuildCatalog()
    {
        var catalog = _services.Catalog;
        CatalogLabel = catalog.Tweaks.Count.ToString(CultureInfo.InvariantCulture);
        DocumentedLabel = catalog.Tweaks.Count(tweak =>
            tweak.Evidence == TweakEvidence.MicrosoftDocumented).ToString(CultureInfo.InvariantCulture);
        CatalogCaption = $"{CatalogLabel} curated tweaks · {DocumentedLabel} Microsoft documented";

        var categories = catalog.Tweaks
            .GroupBy(tweak => tweak.Category)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select((group, index) => (Label: group.Key, Count: group.Count(),
                Brush: (Brush)Palette[index % Palette.Length]))
            .ToArray();
        CatalogBars = Bars(categories);
    }

    private void BuildJournal(IReadOnlyList<JournalRecord> records)
    {
        var ordered = records.OrderByDescending(record => record.Timestamp).ToArray();
        RecentChanges = ordered.Take(5).Select(record => new JournalRowViewModel(record)).ToArray();
        HasRecentChanges = RecentChanges.Count > 0;
        RecentChangesVisibility = HasRecentChanges ? Visibility.Visible : Visibility.Collapsed;
        EmptyJournalVisibility = HasRecentChanges ? Visibility.Collapsed : Visibility.Visible;

        var counts = new int[14];
        var today = DateTime.Today;
        foreach (var record in records)
        {
            var day = (today - record.Timestamp.ToLocalTime().Date).Days;
            if (day >= 0 && day < counts.Length)
            {
                counts[counts.Length - 1 - day]++;
            }
        }

        ActivityValues = counts;
        ActivityRange = $"{today.AddDays(-(counts.Length - 1)):dd MMM} – {today:dd MMM}";
        var verified = records.Count(record => record.Status == ChangeStatus.Verified);
        ActivitySummary = records.Count == 0
            ? "No changes recorded yet."
            : $"{records.Count} changes in the journal · {verified} verified";
    }

    private void BuildRecommendations(IReadOnlyList<StartupEntry> entries)
    {
        var recommendations = entries
            .Where(entry => entry.State == EntryState.Enabled
                && entry.Assessment.Category == EntryCategory.DisableRecommended)
            .OrderByDescending(entry => entry.Assessment.Risk)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, TopRecommendations))
            .Select(entry => new StartupEntryViewModel(entry, canChange: false))
            .ToArray();
        RecommendationRows = recommendations;
        HasRecommendations = recommendations.Length > 0;
        RecommendationsVisibility = HasRecommendations ? Visibility.Visible : Visibility.Collapsed;
        EmptyRecommendationsVisibility = HasRecommendations ? Visibility.Collapsed : Visibility.Visible;
    }

    private static ChartSlice Slice(string label, int value, int total, Brush brush)
    {
        var fraction = total > 0 ? value / (double)total : 0;
        return new ChartSlice(label, value, fraction,
            value.ToString(CultureInfo.InvariantCulture),
            fraction.ToString("P0", CultureInfo.InvariantCulture), brush);
    }

    private static IReadOnlyList<ChartBar> Bars(
        IEnumerable<(string Label, int Count, Brush Brush)> source)
    {
        var items = source.ToArray();
        var maximum = Math.Max(1, items.Max(item => item.Count));
        return items.Select(item => new ChartBar(item.Label, item.Count,
            item.Count / (double)maximum,
            item.Count.ToString(CultureInfo.InvariantCulture), item.Brush)).ToArray();
    }

    private static SolidColorBrush Make(byte red, byte green, byte blue)
        => new(WinColor.FromRgb(red, green, blue));

    private static SolidColorBrush Make(byte alpha, byte red, byte green, byte blue)
        => new(WinColor.FromArgb(alpha, red, green, blue));
}
