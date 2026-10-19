using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SystemCompass.Core.Demo;
using SystemCompass.Core.Startup;
using SystemCompass.Windows;
using WinColor = System.Windows.Media.Color;

namespace SystemCompass.App.ViewModels;

public sealed partial class StartupEntryViewModel : ObservableObject
{
    private static readonly SolidColorBrush RecommendBrush = new(WinColor.FromRgb(0xB4, 0x53, 0x09));
    private static readonly SolidColorBrush OptionalBrush = new(WinColor.FromRgb(0x25, 0x63, 0xEB));
    private static readonly SolidColorBrush KeepBrush = new(WinColor.FromRgb(0x15, 0x80, 0x3D));
    private static readonly SolidColorBrush SecurityBrush = new(WinColor.FromRgb(0x7C, 0x3A, 0xED));
    private static readonly SolidColorBrush NeutralBrush = new(WinColor.FromRgb(0x6B, 0x72, 0x80));
    private static readonly SolidColorBrush RiskHighBrush = new(WinColor.FromRgb(0xDC, 0x26, 0x26));

    private EntryState _state;
    private ImageSource? _icon;

    public StartupEntryViewModel(StartupEntry entry, bool canChange)
    {
        Entry = entry;
        _state = entry.State;
        CanToggle = canChange;
    }

    public StartupEntry Entry { get; }

    public string Id => Entry.Id;
    public string Name => Entry.Name;

    public string StateLabel => _state switch
    {
        EntryState.Enabled => "Enabled",
        EntryState.Disabled => "Disabled",
        _ => "Unknown",
    };

    public string ActionLabel => _state == EntryState.Enabled ? "Disable" : "Enable";

    public bool IsOn => _state == EntryState.Enabled;

    public bool CanToggle { get; }

    public Visibility ToggleVisibility => CanToggle ? Visibility.Visible : Visibility.Collapsed;

    public bool IsProtected => Entry.IsProtected;

    public ImageSource? Icon => _icon ??= IconLoader.Get(Entry.ResolvedPath);

    public string CategoryLabel => Entry.Assessment.Category switch
    {
        EntryCategory.DisableRecommended => "Disable recommended",
        EntryCategory.SystemCritical => "System critical",
        EntryCategory.SecurityRelevant => "Security relevant",
        EntryCategory.Optional => "Optional",
        EntryCategory.Keep => "Keep",
        _ => "Unclear",
    };

    public string RiskLabel => Entry.Assessment.Risk.ToString();

    public string MechanismLabel => Entry.Mechanism switch
    {
        StartupMechanism.RunKey => "Run key",
        StartupMechanism.RunOnceKey => "RunOnce",
        StartupMechanism.StartupFolder => "Startup folder",
        StartupMechanism.ScheduledTask => "Scheduled task",
        StartupMechanism.Winlogon => "Winlogon",
        _ => "Service",
    };

    public string SignatureLabel => Entry.Signature.Status switch
    {
        SignatureStatus.Valid => Entry.Signature.Signer ?? "Signed",
        SignatureStatus.ValidCatalog => Entry.Signature.Signer ?? "Catalog signed",
        SignatureStatus.Unsigned => "Unsigned",
        SignatureStatus.Invalid => "Invalid signature",
        _ => "Signature unknown",
    };

    public string Detail => string.IsNullOrWhiteSpace(Entry.CommandLine) ? Entry.Location : Entry.CommandLine;

    public string MetaLabel => $"{CategoryLabel} · {MechanismLabel} · {SignatureLabel}";

    public string ProtectionReason => Entry.IsProtected ? Entry.Assessment.Reason : string.Empty;

    public Visibility ProtectedVisibility => IsProtected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ReasonVisibility =>
        string.IsNullOrWhiteSpace(ProtectionReason) ? Visibility.Collapsed : Visibility.Visible;

    public Brush CategoryBrush => Entry.Assessment.Category switch
    {
        EntryCategory.DisableRecommended => RecommendBrush,
        EntryCategory.Optional => OptionalBrush,
        EntryCategory.Keep => KeepBrush,
        EntryCategory.SecurityRelevant => SecurityBrush,
        _ => NeutralBrush,
    };

    public Brush RiskBrush => Entry.Assessment.Risk is RiskLevel.High or RiskLevel.Critical
        ? RiskHighBrush
        : CategoryBrush;

    public string BuildDialogText(bool desiredState) =>
        $"Entry: {Name}{Environment.NewLine}"
        + $"Mechanism: {MechanismLabel} ({Entry.Scope}){Environment.NewLine}"
        + $"Current state: {StateLabel}{Environment.NewLine}"
        + $"Requested state: {(desiredState ? "Enabled" : "Disabled")}{Environment.NewLine}{Environment.NewLine}"
        + $"{Entry.Assessment.Reason}{Environment.NewLine}"
        + (Entry.RequiresElevation
            ? "This change needs administrator rights. Windows will show one confirmation prompt."
            : "This change is journaled and can be rolled back.");

    public void ApplyState(EntryState state)
    {
        _state = state;
        OnPropertyChanged(nameof(IsOn));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(ActionLabel));
    }
}

public sealed partial class StartupViewModel : ObservableObject
{
    private readonly SystemServices _services;
    private readonly bool _demo;
    private IReadOnlyList<StartupEntry> _all = [];
    private IReadOnlyList<StartupEntryViewModel> _items = [];

    public StartupViewModel(SystemServices services, bool demo = false)
    {
        _services = services;
        _demo = demo;
        Entries = [];
        SearchText = string.Empty;
        StatusMessage = string.Empty;
        TotalCountLabel = "0";
        EnabledCountLabel = "0";
        RecommendationCountLabel = "0";
        ProtectedCountLabel = "0";
        FilteredCountLabel = "0 shown";
        Refresh();
    }

    public ObservableCollection<StartupEntryViewModel> Entries { get; }

    public IReadOnlyList<StartupEntryViewModel> AllItems => _items;

    [ObservableProperty] public partial string SearchText { get; set; }
    [ObservableProperty] public partial int FilterIndex { get; set; }
    [ObservableProperty] public partial bool IncludeSystem { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; }
    [ObservableProperty] public partial string TotalCountLabel { get; set; }
    [ObservableProperty] public partial string EnabledCountLabel { get; set; }
    [ObservableProperty] public partial string RecommendationCountLabel { get; set; }
    [ObservableProperty] public partial string ProtectedCountLabel { get; set; }
    [ObservableProperty] public partial string FilteredCountLabel { get; set; }
    [ObservableProperty] public partial StartupEntryViewModel? SelectedEntry { get; set; }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnFilterIndexChanged(int value) => ApplyFilter();
    partial void OnIncludeSystemChanged(bool value) => ApplyFilter();

    [RelayCommand]
    public void Refresh()
    {
        IsBusy = true;
        try
        {
            _all = _demo
                ? DemoDataSet.CreateReport(DateTimeOffset.UtcNow, "0.1.0").StartupEntries
                : _services.Audit.EnumerateAll();
            _items = _all.Select(entry => new StartupEntryViewModel(
                entry, !_demo && _services.Audit.CanChange(entry))).ToArray();
            ApplyFilter();
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or UnauthorizedAccessException or IOException
            or System.ComponentModel.Win32Exception)
        {
            StatusMessage = $"Audit failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<(bool Success, string Message)> SetStateAsync(StartupEntryViewModel item, bool enabled)
    {
        if (!item.CanToggle)
        {
            return (false, item.ProtectionReason);
        }

        try
        {
            if (item.Entry.RequiresElevation)
            {
                var elevated = await ElevationHelper.ApplyAsync([(item.Entry.Id, enabled)]);
                if (elevated.Success)
                {
                    item.ApplyState(enabled ? EntryState.Enabled : EntryState.Disabled);
                }

                StatusMessage = elevated.Message;
                return elevated;
            }

            var result = _services.Changes.SetState(item.Entry, enabled);
            if (result.Success)
            {
                item.ApplyState(result.ObservedState ?? (enabled ? EntryState.Enabled : EntryState.Disabled));
            }

            StatusMessage = result.Message;
            return (result.Success, result.Message);
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or UnauthorizedAccessException or IOException
            or System.ComponentModel.Win32Exception)
        {
            StatusMessage = ex.Message;
            return (false, ex.Message);
        }
    }

    private void ApplyFilter()
    {
        Entries.Clear();
        IEnumerable<StartupEntryViewModel> query = _items;
        if (!IncludeSystem)
        {
            query = query.Where(item => item.Entry.Assessment.Category != EntryCategory.SystemCritical);
        }

        query = FilterIndex switch
        {
            1 => query.Where(item => item.Entry.Assessment.Category == EntryCategory.DisableRecommended),
            2 => query.Where(item => item.Entry.Assessment.Category == EntryCategory.Optional),
            3 => query.Where(item => item.Entry.Assessment.Category == EntryCategory.Unclear),
            4 => query.Where(item => item.Entry.Assessment.Category == EntryCategory.Keep),
            5 => query.Where(item => item.Entry.Assessment.Category == EntryCategory.SecurityRelevant),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(item =>
                item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || item.Detail.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || item.SignatureLabel.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || item.Entry.Location.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in query
                     .OrderBy(item => item.Entry.Assessment.Category)
                     .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            Entries.Add(item);
        }

        TotalCountLabel = _all.Count.ToString();
        EnabledCountLabel = _all.Count(entry => entry.State == EntryState.Enabled).ToString();
        RecommendationCountLabel = _all.Count(entry =>
            entry.State == EntryState.Enabled
            && entry.Assessment.Category == EntryCategory.DisableRecommended).ToString();
        ProtectedCountLabel = _all.Count(entry => entry.IsProtected).ToString();
        FilteredCountLabel = $"{Entries.Count} shown";
        StatusMessage = $"{Entries.Count} shown of {_all.Count} entries · {RecommendationCountLabel} recommendations";
    }
}
