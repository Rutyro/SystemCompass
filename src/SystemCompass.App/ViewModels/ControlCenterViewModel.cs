using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SystemCompass.Core.Tweaks;
using SystemCompass.Windows;
using WinColor = System.Windows.Media.Color;

namespace SystemCompass.App.ViewModels;

public sealed partial class TweakItemViewModel : ObservableObject
{
    private static readonly SolidColorBrush LowBrush = new(WinColor.FromRgb(0x16, 0xA3, 0x4A));
    private static readonly SolidColorBrush MediumBrush = new(WinColor.FromRgb(0xD9, 0x77, 0x06));
    private static readonly SolidColorBrush HighBrush = new(WinColor.FromRgb(0xDC, 0x26, 0x26));
    private static readonly SolidColorBrush AppliedBrush = new(WinColor.FromRgb(0x34, 0xD3, 0x99));
    private static readonly SolidColorBrush NotAppliedBrush = new(WinColor.FromRgb(0x94, 0xA3, 0xB8));
    private static readonly SolidColorBrush PartialBrush = new(WinColor.FromRgb(0xFB, 0xBF, 0x24));
    private static readonly SolidColorBrush BlueBrush = new(WinColor.FromRgb(0x60, 0xA5, 0xFA));
    private static readonly SolidColorBrush VioletBrush = new(WinColor.FromRgb(0xA7, 0x8B, 0xFA));
    private static readonly SolidColorBrush GreenBrush = new(WinColor.FromRgb(0x34, 0xD3, 0x99));
    private static readonly SolidColorBrush AmberBrush = new(WinColor.FromRgb(0xFB, 0xBF, 0x24));
    private static readonly SolidColorBrush CyanBrush = new(WinColor.FromRgb(0x22, 0xD3, 0xEE));
    private static readonly SolidColorBrush PinkBrush = new(WinColor.FromRgb(0xF4, 0x72, 0xB6));
    private static readonly SolidColorBrush TealBrush = new(WinColor.FromRgb(0x2D, 0xD4, 0xBF));
    private static readonly SolidColorBrush SlateBrush = new(WinColor.FromRgb(0x94, 0xA3, 0xB8));

    private TweakStatus _status;
    private bool _selected;

    public TweakItemViewModel(TweakDefinition definition, TweakStatus status)
    {
        Definition = definition;
        _status = status;
    }

    public TweakDefinition Definition { get; }
    public TweakStatus Status => _status;
    public string Id => Definition.Id;
    public string Title => Definition.Title;
    public string Description => Definition.Description;
    public string Category => Definition.Category;

    public bool Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    public string EvidenceLabel => Definition.Evidence switch
    {
        TweakEvidence.MicrosoftDocumented => "Microsoft documented",
        TweakEvidence.Community => "Community",
        _ => "Unverified",
    };

    public string RiskLabel => Definition.Risk.ToString();

    public string StatusLabel => _status switch
    {
        TweakStatus.Applied => "Applied",
        TweakStatus.NotApplied => "Not applied",
        TweakStatus.Partial => "Partial",
        _ => "Not applicable",
    };

    public bool CanApply => _status is TweakStatus.NotApplied or TweakStatus.Partial;
    public bool CanRevert => _status is TweakStatus.Applied or TweakStatus.Partial;
    public System.Windows.Visibility ApplyVisibility =>
        CanApply ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public System.Windows.Visibility RevertVisibility =>
        CanRevert ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public string OperationSummary
    {
        get
        {
            var lines = Definition.Operations.Take(4)
                .Select(operation => "• " + operation.Describe()).ToList();
            if (Definition.Operations.Count > 4)
            {
                lines.Add($"• +{Definition.Operations.Count - 4} more operations");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    public string FullOperationSummary =>
        string.Join(Environment.NewLine, Definition.Operations.Select(operation => "• " + operation.Describe()));

    public IReadOnlyList<string> OperationLines =>
        Definition.Operations.Select(operation => operation.Describe()).ToArray();

    public string OperationCountLabel => Definition.Operations.Count == 1
        ? "1 operation" : $"{Definition.Operations.Count} operations";

    public string BadgeLabel => $"{EvidenceLabel} · {RiskLabel}";

    public Brush RiskBrush => Definition.Risk switch
    {
        TweakRisk.Low => LowBrush,
        TweakRisk.Medium => MediumBrush,
        _ => HighBrush,
    };

    public Brush StatusBrush => _status switch
    {
        TweakStatus.Applied => AppliedBrush,
        TweakStatus.Partial => PartialBrush,
        _ => NotAppliedBrush,
    };

    public Brush CategoryBrush => Definition.Category switch
    {
        "Privacy" => VioletBrush,
        "Security" => GreenBrush,
        "Network" => CyanBrush,
        "Power" => AmberBrush,
        "Gaming" => PinkBrush,
        "Visuals" => BlueBrush,
        "Explorer" => TealBrush,
        "Services" => SlateBrush,
        "Input" => PinkBrush,
        _ => BlueBrush,
    };

    public string DialogText =>
        $"{Title}{Environment.NewLine}{Environment.NewLine}"
        + $"{Description}{Environment.NewLine}{Environment.NewLine}"
        + FullOperationSummary + Environment.NewLine + Environment.NewLine
        + (Definition.RequiresElevation
            ? "This tweak needs administrator rights. Windows will show one confirmation prompt."
            : "The previous value is captured and can be reverted.");

    public void SetStatus(TweakStatus status)
    {
        _status = status;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(CanRevert));
        OnPropertyChanged(nameof(ApplyVisibility));
        OnPropertyChanged(nameof(RevertVisibility));
    }
}

public sealed partial class ControlCenterViewModel : ObservableObject
{
    private readonly SystemServices _services;
    private readonly List<TweakItemViewModel> _all = [];

    public ControlCenterViewModel(SystemServices services)
    {
        _services = services;
        Tweaks = [];
        Categories = [];
        RiskOptions = ["Low", "Medium", "High"];
        RiskFilters = ["Low", "Medium", "High"];
        RiskFilters.CollectionChanged += (_, _) => ApplyFilter();
        StatusOptions = ["All statuses", "Applied", "Not applied", "Partial"];
        SearchText = string.Empty;
        StatusMessage = string.Empty;
        CatalogWarning = string.Empty;
        FilteredCountLabel = string.Empty;
        AppliedCountLabel = "0";
        NotAppliedCountLabel = "0";
        PartialCountLabel = "0";
        Refresh();
    }

    public ObservableCollection<TweakItemViewModel> Tweaks { get; }
    public ObservableCollection<string> Categories { get; }
    public IReadOnlyList<string> StatusOptions { get; }
    public IReadOnlyList<string> RiskOptions { get; }
    public ObservableCollection<string> RiskFilters { get; }

    [ObservableProperty] public partial string SearchText { get; set; }
    [ObservableProperty] public partial int CategoryFilterIndex { get; set; }
    [ObservableProperty] public partial int StatusFilterIndex { get; set; }
    [ObservableProperty] public partial bool OnlySelected { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; }
    [ObservableProperty] public partial string FilteredCountLabel { get; set; }
    [ObservableProperty] public partial string AppliedCountLabel { get; set; }
    [ObservableProperty] public partial string NotAppliedCountLabel { get; set; }
    [ObservableProperty] public partial string PartialCountLabel { get; set; }
    [ObservableProperty] public partial double AppliedFraction { get; set; }
    [ObservableProperty] public partial bool HasCatalogWarning { get; set; }
    [ObservableProperty] public partial string CatalogWarning { get; set; }
    [ObservableProperty] public partial TweakItemViewModel? SelectedTweak { get; set; }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnCategoryFilterIndexChanged(int value) => ApplyFilter();
    partial void OnStatusFilterIndexChanged(int value) => ApplyFilter();
    partial void OnOnlySelectedChanged(bool value) => ApplyFilter();

    [RelayCommand]
    public void Refresh()
    {
        IsBusy = true;
        try
        {
            if (Categories.Count == 0)
            {
                Categories.Add("All categories");
                foreach (var category in _services.Catalog.Tweaks
                             .Select(tweak => tweak.Category)
                             .Distinct()
                             .OrderBy(category => category, StringComparer.OrdinalIgnoreCase))
                {
                    Categories.Add(category);
                }
            }

            _all.Clear();
            foreach (var tweak in _services.Catalog.Tweaks)
            {
                _all.Add(new TweakItemViewModel(
                    tweak, _services.Tweaks.GetStatus(tweak, Environment.OSVersion.Version.Build)));
            }

            var applied = _all.Count(item => item.Status == TweakStatus.Applied);
            var partial = _all.Count(item => item.Status == TweakStatus.Partial);
            AppliedCountLabel = applied.ToString();
            PartialCountLabel = partial.ToString();
            NotAppliedCountLabel = (_all.Count - applied - partial).ToString();
            AppliedFraction = _all.Count == 0 ? 0 : applied / (double)_all.Count;
            var problems = _services.Catalog.Validate();
            HasCatalogWarning = problems.Count > 0;
            CatalogWarning = string.Join(Environment.NewLine, problems);
            ApplyFilter();
            StatusMessage = $"{_all.Count} tweaks · "
                + $"{_all.Count(item => item.Definition.Evidence == TweakEvidence.MicrosoftDocumented)} documented by Microsoft";
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or UnauthorizedAccessException or IOException
            or System.ComponentModel.Win32Exception)
        {
            StatusMessage = $"Could not read tweak state: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<(bool Success, string Message)> ApplyAsync(TweakItemViewModel item)
    {
        try
        {
            if (item.Definition.RequiresElevation)
            {
                var elevated = await ElevationHelper.RunTweakAsync(item.Id, revert: false);
                Refresh();
                return elevated;
            }

            var result = _services.Tweaks.Apply(item.Definition, Environment.OSVersion.Version.Build);
            Refresh();
            return (result.Success, result.Success ? $"Applied '{item.Title}'." : result.Error ?? "Apply failed.");
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or UnauthorizedAccessException or IOException
            or System.ComponentModel.Win32Exception)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string Message)> RevertAsync(TweakItemViewModel item)
    {
        try
        {
            if (item.Definition.RequiresElevation)
            {
                var elevated = await ElevationHelper.RunTweakAsync(item.Id, revert: true);
                Refresh();
                return elevated;
            }

            var result = _services.Tweaks.Revert(item.Definition);
            Refresh();
            return (result.Success, result.Success ? $"Reverted '{item.Title}'." : result.Error ?? "Revert failed.");
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or UnauthorizedAccessException or IOException
            or System.ComponentModel.Win32Exception)
        {
            return (false, ex.Message);
        }
    }

    public IReadOnlyList<TweakItemViewModel> SelectableTweaks => _all;

    private void ApplyFilter()
    {
        Tweaks.Clear();
        IEnumerable<TweakItemViewModel> query = _all;
        if (CategoryFilterIndex > 0 && CategoryFilterIndex < Categories.Count)
        {
            var category = Categories[CategoryFilterIndex];
            query = query.Where(item =>
                item.Definition.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        query = StatusFilterIndex switch
        {
            1 => query.Where(item => item.Status == TweakStatus.Applied),
            2 => query.Where(item => item.Status == TweakStatus.NotApplied),
            3 => query.Where(item => item.Status == TweakStatus.Partial),
            _ => query,
        };

        if (RiskFilters.Count > 0)
        {
            query = query.Where(item => RiskFilters.Contains(item.RiskLabel));
        }

        if (OnlySelected)
        {
            query = query.Where(item => item.Selected);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(item =>
                item.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || item.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || item.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || item.Id.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var tweak in query.OrderBy(item => item.Definition.Category)
                     .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase))
        {
            Tweaks.Add(tweak);
        }

        FilteredCountLabel = $"{Tweaks.Count} shown of {_all.Count}";
    }
}
