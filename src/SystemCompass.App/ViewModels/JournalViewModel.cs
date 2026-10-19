using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SystemCompass.Core.Changes;
using SystemCompass.Core.Demo;
using SystemCompass.Windows;

namespace SystemCompass.App.ViewModels;

public sealed record JournalRowViewModel(JournalRecord Record)
{
    public string TimeLabel => Record.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public string EntryName => Record.EntryName;

    public string ActionLabel => Record.Action;

    public string StatusLabel => Record.Status switch
    {
        ChangeStatus.Pending => "Pending",
        ChangeStatus.Verified => "Verified",
        ChangeStatus.Failed => "Failed",
        _ => "Rolled back",
    };

    public bool CanRollback => Record.Status == ChangeStatus.Verified;
}

public sealed partial class JournalViewModel : ObservableObject
{
    private readonly SystemServices _services;
    private readonly bool _demo;

    public JournalViewModel(SystemServices services, bool demo = false)
    {
        _services = services;
        _demo = demo;
        StatusMessage = string.Empty;
        Refresh();
    }

    public ObservableCollection<JournalRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [RelayCommand]
    public void Refresh()
    {
        Rows.Clear();
        var records = _demo
            ? DemoDataSet.CreateReport(DateTimeOffset.UtcNow, "0.1.0").Journal
            : JournalView.LatestPerChange(_services.Journal.ReadAll());
        foreach (var record in records)
        {
            Rows.Add(new JournalRowViewModel(record));
        }

        StatusMessage = Rows.Count == 0
            ? "No changes recorded yet."
            : $"{Rows.Count} changes · {Rows.Count(row => row.CanRollback)} can be rolled back";
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
