using System.Collections.ObjectModel;
using System.Security.Principal;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SystemCompass.Core.Tweaks;
using SystemCompass.Windows;

namespace SystemCompass.App.ViewModels;

/// <summary>Shell state: documents, explorer, selection and the activity log.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly SystemServices _services;

    public MainViewModel(SystemServices services, bool demo)
    {
        _services = services;
        IsDemo = demo;
        StatusMessage = string.Empty;
        SelectedItemLabel = "Nothing selected";
        ElevationLabel = IsAdministrator() ? "Administrator" : "Standard user";
        CatalogStatus = "Catalog valid";
        ExplorerRoots = [];
        SelectedOperations = [];
        ActivityLog = [];
        Timeline = [];
        Dashboard = new DashboardViewModel(services, demo, Navigate);
        Startup = new StartupViewModel(services, demo);
        ControlCenter = new ControlCenterViewModel(services, demo);
        Journal = new JournalViewModel(services, demo);
        Report = new ReportViewModel(services, demo);
        RefreshExplorer();
        RefreshTimeline();
        if (Startup.AllItems.FirstOrDefault() is { } first)
        {
            SelectStartup(first);
        }

        Log("SystemCompass started in " + (demo ? "demo" : "live") + " mode.");
    }

    public DashboardViewModel Dashboard { get; }
    public StartupViewModel Startup { get; }
    public ControlCenterViewModel ControlCenter { get; }
    public JournalViewModel Journal { get; }
    public ReportViewModel Report { get; }
    public bool IsDemo { get; }

    public ObservableCollection<ExplorerNode> ExplorerRoots { get; }
    public ObservableCollection<string> SelectedOperations { get; }
    public ObservableCollection<string> ActivityLog { get; }
    public ObservableCollection<JournalRowViewModel> Timeline { get; }

    [ObservableProperty] public partial object? SelectedObject { get; set; }
    [ObservableProperty] public partial ExplorerNode? SelectedExplorerNode { get; set; }
    [ObservableProperty] public partial string SelectedItemLabel { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; }
    [ObservableProperty] public partial string ElevationLabel { get; set; }
    [ObservableProperty] public partial string CatalogStatus { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }

    public event Action<string>? NavigateRequested;

    partial void OnSelectedExplorerNodeChanged(ExplorerNode? value)
    {
        switch (value?.Payload)
        {
            case StartupEntryViewModel entry:
                SelectStartup(entry);
                break;
            case TweakItemViewModel tweak:
                SelectTweak(tweak);
                break;
        }
    }

    public void SelectStartup(StartupEntryViewModel entry)
    {
        SelectedObject = entry;
        SelectedItemLabel = $"{entry.Name} · {entry.CategoryLabel}";
        SelectedOperations.Clear();
        foreach (var operation in entry.Entry.Assessment.Reason
                     .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            SelectedOperations.Add(operation);
        }
    }

    public void SelectTweak(TweakItemViewModel tweak)
    {
        SelectedObject = tweak.Definition;
        SelectedItemLabel = $"{tweak.Title} · {tweak.Category}";
        SelectedOperations.Clear();
        foreach (var operation in tweak.Definition.Operations)
        {
            SelectedOperations.Add(operation.Describe());
        }
    }

    public void Log(string message)
    {
        ActivityLog.Add($"{DateTimeOffset.Now:HH:mm:ss}  {message}");
        while (ActivityLog.Count > 300)
        {
            ActivityLog.RemoveAt(0);
        }

        StatusMessage = message;
    }

    public void RefreshAll()
    {
        IsBusy = true;
        try
        {
            Dashboard.Refresh();
            Startup.Refresh();
            ControlCenter.Refresh();
            Journal.Refresh();
            Report.Generate();
            RefreshExplorer();
            RefreshTimeline();
            CatalogStatus = ControlCenter.HasCatalogWarning ? "Catalog warning" : "Catalog valid";
            Log("Audit and catalog refreshed.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void RefreshTimeline()
    {
        Timeline.Clear();
        foreach (var row in Journal.Rows)
        {
            Timeline.Add(row);
        }
    }

    /// <summary>Refreshes navigation and journal after one change, without a full audit.</summary>
    public void RefreshShellData()
    {
        Journal.Refresh();
        RefreshTimeline();
        RefreshExplorer();
        CatalogStatus = ControlCenter.HasCatalogWarning ? "Catalog warning" : "Catalog valid";
    }

    private void RefreshExplorer()
    {
        ExplorerRoots.Clear();
        var startupChildren = Startup.AllItems
            .GroupBy(item => item.MechanismLabel)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ExplorerNode(
                $"{group.Key} ({group.Count()})",
                "startup",
                group.Select(item => new ExplorerNode(item.Name, "startup", payload: item))))
            .ToArray();
        ExplorerRoots.Add(new ExplorerNode("Startup", "startup", startupChildren));

        var catalogChildren = ControlCenter.SelectableTweaks
            .GroupBy(item => item.Category)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ExplorerNode(
                $"{group.Key} ({group.Count()})",
                "tweaks",
                group.Select(item => new ExplorerNode(item.Title, "tweaks", payload: item))))
            .ToArray();
        ExplorerRoots.Add(new ExplorerNode("Tweak catalog", "tweaks", catalogChildren));
        ExplorerRoots.Add(new ExplorerNode("Journal", "journal"));
        ExplorerRoots.Add(new ExplorerNode("Report", "report"));
    }

    private void Navigate(string tag)
    {
        NavigateRequested?.Invoke(tag);
    }

    [RelayCommand]
    private void Refresh() => RefreshAll();

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
