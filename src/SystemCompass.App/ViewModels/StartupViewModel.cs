using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using SystemCompass.Core.Demo;
using SystemCompass.Core.Startup;
using SystemCompass.Windows;
using Windows.UI;

namespace SystemCompass.App.ViewModels;

public sealed partial class StartupEntryViewModel : ObservableObject
{
    private static readonly SolidColorBrush RecommendBrush = new(Color.FromArgb(255, 180, 83, 9));
    private static readonly SolidColorBrush OptionalBrush = new(Color.FromArgb(255, 37, 99, 235));
    private static readonly SolidColorBrush KeepBrush = new(Color.FromArgb(255, 21, 128, 61));
    private static readonly SolidColorBrush SecurityBrush = new(Color.FromArgb(255, 124, 58, 237));
    private static readonly SolidColorBrush NeutralBrush = new(Color.FromArgb(255, 107, 114, 128));

    private EntryState _state;
    private bool _isOn;

    public StartupEntryViewModel(StartupEntry entry, bool canChange)
    {
        Entry = entry;
        _state = entry.State;
        _isOn = entry.State == EntryState.Enabled;
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

    public bool IsOn
    {
        get => _isOn;
        set => SetProperty(ref _isOn, value);
    }

    public bool CanToggle { get; }

    public bool IsProtected => Entry.IsProtected;

    public string CategoryLabel => Entry.Assessment.Category switch
    {
        EntryCategory.DisableRecommended => "Disable recommended",
        EntryCategory.SystemCritical => "System critical",
        EntryCategory.SecurityRelevant => "Security relevant",
        EntryCategory.Optional => "Optional",
        EntryCategory.Keep => "Keep",
        _ => "Unclear",
    };

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

    public string Detail => string.IsNullOrWhiteSpace(Entry.CommandLine)
        ? Entry.Location
        : Entry.CommandLine;

    public string MetaLabel => $"{CategoryLabel} · {MechanismLabel} · {SignatureLabel}";

    public string ProtectionReason => Entry.IsProtected
        ? Entry.Assessment.Reason
        : string.Empty;

    public Microsoft.UI.Xaml.Visibility ProtectedVisibility =>
        IsProtected ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public Microsoft.UI.Xaml.Visibility ReasonVisibility =>
        string.IsNullOrWhiteSpace(ProtectionReason)
            ? Microsoft.UI.Xaml.Visibility.Collapsed
            : Microsoft.UI.Xaml.Visibility.Visible;

    public Brush CategoryBrush => Entry.Assessment.Category switch
    {
        EntryCategory.DisableRecommended => RecommendBrush,
        EntryCategory.Optional => OptionalBrush,
        EntryCategory.Keep => KeepBrush,
        EntryCategory.SecurityRelevant => SecurityBrush,
        _ => NeutralBrush,
    };

    public string BuildDialogText(bool desiredState) =>
        $"Entry: {Name}{Environment.NewLine}"
        + $"Mechanism: {MechanismLabel} ({Entry.Scope}){Environment.NewLine}"
        + $"Current state: {StateLabel}{Environment.NewLine}"
        + $"Requested state: {(desiredState ? "Enabled" : "Disabled")}{Environment.NewLine}"
        + $"{Environment.NewLine}{Entry.Assessment.Reason}{Environment.NewLine}"
        + (Entry.RequiresElevation
            ? "This change needs administrator rights. Windows will show one confirmation prompt."
            : "This change is journaled and can be rolled back.");

    public void ApplyState(EntryState state)
    {
        _state = state;
        IsOn = state == EntryState.Enabled;
        OnPropertyChanged(nameof(StateLabel));
    }
}

public sealed partial class StartupViewModel : ObservableObject
{
    private readonly SystemServices _services;
    private readonly bool _demo;
    private IReadOnlyList<StartupEntry> _all = [];

    public StartupViewModel(SystemServices services, bool demo = false)
    {
        _services = services;
        _demo = demo;
        SearchText = string.Empty;
        StatusMessage = string.Empty;
        Refresh();
    }

    public ObservableCollection<StartupEntryViewModel> Entries { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    public partial int FilterIndex { get; set; }

    [ObservableProperty]
    public partial bool IncludeSystem { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

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
            ApplyFilter();
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or UnauthorizedAccessException
            or IOException
            or System.ComponentModel.Win32Exception)
        {
            StatusMessage = $"Audit failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<(bool Success, string Message)> SetStateAsync(
        StartupEntryViewModel item,
        bool enabled)
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
                if (!elevated.Success)
                {
                    StatusMessage = elevated.Message;
                    return elevated;
                }

                item.ApplyState(enabled ? EntryState.Enabled : EntryState.Disabled);
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
            or UnauthorizedAccessException
            or IOException
            or System.ComponentModel.Win32Exception)
        {
            StatusMessage = ex.Message;
            return (false, ex.Message);
        }
    }

    private void ApplyFilter()
    {
        Entries.Clear();
        IEnumerable<StartupEntry> query = _all;

        if (!IncludeSystem)
        {
            query = query.Where(entry => entry.Assessment.Category != EntryCategory.SystemCritical);
        }

        query = FilterIndex switch
        {
            1 => query.Where(entry => entry.Assessment.Category == EntryCategory.DisableRecommended),
            2 => query.Where(entry => entry.Assessment.Category == EntryCategory.Optional),
            3 => query.Where(entry => entry.Assessment.Category == EntryCategory.Unclear),
            4 => query.Where(entry => entry.Assessment.Category == EntryCategory.Keep),
            5 => query.Where(entry => entry.Assessment.Category == EntryCategory.SecurityRelevant),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(entry =>
                entry.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || (entry.CommandLine?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                || (entry.Signature.Signer?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                || entry.Location.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var entry in query
                     .OrderBy(candidate => candidate.Assessment.Category)
                     .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase))
        {
            Entries.Add(new StartupEntryViewModel(
                entry,
                !_demo && _services.Audit.CanChange(entry)));
        }

        StatusMessage = $"{Entries.Count} shown of {_all.Count} entries · "
            + $"{_all.Count(entry => entry.Assessment.Category == EntryCategory.DisableRecommended)} recommendations";
    }
}
