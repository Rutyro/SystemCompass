using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SystemCompass.Core.Changes;
using SystemCompass.Core.Demo;
using SystemCompass.Windows;
using WinColor = System.Windows.Media.Color;

namespace SystemCompass.App.ViewModels;

public sealed record JournalRowViewModel(JournalRecord Record)
{
    private static readonly SolidColorBrush VerifiedBrush = new(WinColor.FromRgb(0x34, 0xD3, 0x99));
    private static readonly SolidColorBrush FailedBrush = new(WinColor.FromRgb(0xF8, 0x71, 0x71));
    private static readonly SolidColorBrush PendingBrush = new(WinColor.FromRgb(0xFB, 0xBF, 0x24));
    private static readonly SolidColorBrush RolledBackBrush = new(WinColor.FromRgb(0x94, 0xA3, 0xB8));

    public DateTime Date => Record.Timestamp.LocalDateTime;
    public string TimeLabel => Record.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string EntryName => Record.EntryName;
    public string ActionLabel => Record.Action;
    public string Before => Record.Before ?? string.Empty;
    public string After => Record.After ?? string.Empty;
    public string Error => Record.Error ?? string.Empty;
    public string StatusLabel => Record.Status switch
    {
        ChangeStatus.Pending => "Pending",
        ChangeStatus.Verified => "Verified",
        ChangeStatus.Failed => "Failed",
        _ => "Rolled back",
    };

    public Brush StatusBrush => Record.Status switch
    {
        ChangeStatus.Verified => VerifiedBrush,
        ChangeStatus.Failed => FailedBrush,
        ChangeStatus.Pending => PendingBrush,
        _ => RolledBackBrush,
    };

    public bool CanRollback => Record.Status == ChangeStatus.Verified;
}

public sealed partial class JournalViewModel : ObservableObject
{
    private readonly SystemServices _services;
    private readonly bool _demo;
    private IReadOnlyList<JournalRecord> _records = [];

    public JournalViewModel(SystemServices services, bool demo = false)
    {
        _services = services;
        _demo = demo;
        Rows = [];
        StatusMessage = string.Empty;
        ActivityRange = string.Empty;
        ActivitySummary = string.Empty;
        VerifiedCountLabel = "0";
        FailedCountLabel = "0";
        RollbackCountLabel = "0";
        ActivityValues = [];
        EmptyVisibility = Visibility.Visible;
        SinceDate = null;
        SelectedDay = null;
        Refresh();
    }

    public ObservableCollection<JournalRowViewModel> Rows { get; }

    [ObservableProperty] public partial string StatusMessage { get; set; }
    [ObservableProperty] public partial string ActivityRange { get; set; }
    [ObservableProperty] public partial string ActivitySummary { get; set; }
    [ObservableProperty] public partial string VerifiedCountLabel { get; set; }
    [ObservableProperty] public partial string FailedCountLabel { get; set; }
    [ObservableProperty] public partial string RollbackCountLabel { get; set; }
    [ObservableProperty] public partial IReadOnlyList<int> ActivityValues { get; set; }
    [ObservableProperty] public partial Visibility EmptyVisibility { get; set; }
    [ObservableProperty] public partial bool TimelineMode { get; set; }
    [ObservableProperty] public partial JournalRowViewModel? SelectedRow { get; set; }
    [ObservableProperty] public partial DateTime? SinceDate { get; set; }
    [ObservableProperty] public partial DateTime? SelectedDay { get; set; }
    [ObservableProperty] public partial DateTime TimelineBegin { get; set; }
    [ObservableProperty] public partial DateTime TimelineEnd { get; set; }

    partial void OnSinceDateChanged(DateTime? value) => ApplyDateFilter();
    partial void OnSelectedDayChanged(DateTime? value) => ApplyDateFilter();

    partial void OnTimelineModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ListVisibility));
        OnPropertyChanged(nameof(TimelineVisibility));
    }

    public Visibility ListVisibility => TimelineMode ? Visibility.Collapsed : Visibility.Visible;
    public Visibility TimelineVisibility => TimelineMode ? Visibility.Visible : Visibility.Collapsed;

    [RelayCommand]
    private void ClearDates()
    {
        SinceDate = null;
        SelectedDay = null;
    }

    private void ApplyDateFilter()
    {
        Rows.Clear();
        foreach (var record in _records.OrderByDescending(record => record.Timestamp))
        {
            var local = record.Timestamp.ToLocalTime();
            if (SinceDate is { } since && local.Date < since.Date)
            {
                continue;
            }

            if (SelectedDay is { } day && local.Date != day.Date)
            {
                continue;
            }

            Rows.Add(new JournalRowViewModel(record));
        }

        EmptyVisibility = Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusMessage = Rows.Count == 0
            ? "No changes match the selected dates."
            : $"{Rows.Count} changes · {Rows.Count(row => row.CanRollback)} can be rolled back";
    }

    [RelayCommand]
    public void Refresh()
    {
        Rows.Clear();
        _records = _demo
            ? DemoDataSet.CreateReport(DateTimeOffset.UtcNow, "0.1.0").Journal
            : JournalView.LatestPerChange(_services.Journal.ReadAll());

        VerifiedCountLabel = _records.Count(record => record.Status == ChangeStatus.Verified).ToString();
        FailedCountLabel = _records.Count(record => record.Status == ChangeStatus.Failed).ToString();
        RollbackCountLabel = _records.Count(record => record.Status == ChangeStatus.RolledBack).ToString();

        var counts = new int[14];
        var today = DateTime.Today;
        foreach (var record in _records)
        {
            var day = (today - record.Timestamp.ToLocalTime().Date).Days;
            if (day >= 0 && day < counts.Length)
            {
                counts[counts.Length - 1 - day]++;
            }
        }

        ActivityValues = counts;
        ActivityRange = $"{today.AddDays(-(counts.Length - 1)):dd MMM} – {today:dd MMM}";
        TimelineBegin = today.AddDays(-(counts.Length - 1));
        TimelineEnd = today.AddDays(1);
        ActivitySummary = _records.Count == 0
            ? "No changes recorded yet."
            : $"{_records.Count} changes · {RollbackCountLabel} rolled back";
        ApplyDateFilter();
    }

    public (bool Success, string Message) Rollback(JournalRowViewModel row)
    {
        if (!row.CanRollback)
        {
            return (false, "Only verified changes can be rolled back.");
        }

        var result = _services.Changes.Rollback(row.Record.ChangeId);
        Refresh();
        return (result.Success, result.Message);
    }
}
