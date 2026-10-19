using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using SystemCompass.Core.Tweaks;
using SystemCompass.Windows;
using Windows.UI;

namespace SystemCompass.App.ViewModels;

public sealed partial class TweakItemViewModel : ObservableObject
{
    private static readonly SolidColorBrush LowBrush = new(Color.FromArgb(255, 21, 128, 61));
    private static readonly SolidColorBrush MediumBrush = new(Color.FromArgb(255, 180, 83, 9));
    private static readonly SolidColorBrush HighBrush = new(Color.FromArgb(255, 185, 28, 28));

    private TweakStatus _status;

    public TweakItemViewModel(TweakDefinition definition, TweakStatus status)
    {
        Definition = definition;
        _status = status;
    }

    public TweakDefinition Definition { get; }

    public string Id => Definition.Id;

    public string Title => Definition.Title;

    public string Description => Definition.Description;

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

    public string OperationSummary =>
        string.Join(Environment.NewLine, Definition.Operations.Select(operation => "• " + operation.Describe()));

    public string BadgeLabel => $"{EvidenceLabel} · {RiskLabel}";

    public Brush RiskBrush => Definition.Risk switch
    {
        TweakRisk.Low => LowBrush,
        TweakRisk.Medium => MediumBrush,
        _ => HighBrush,
    };

    public string DialogText =>
        $"{Title}{Environment.NewLine}{Environment.NewLine}"
        + $"{Description}{Environment.NewLine}{Environment.NewLine}"
        + OperationSummary + Environment.NewLine + Environment.NewLine
        + (Definition.RequiresElevation
            ? "This tweak needs administrator rights. Windows will show one confirmation prompt."
            : "The previous value is captured and can be reverted.");

    public void SetStatus(TweakStatus status)
    {
        _status = status;
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(CanRevert));
    }
}

public sealed partial class ControlCenterViewModel : ObservableObject
{
    private readonly SystemServices _services;
    private readonly List<TweakItemViewModel> _all = [];

    public ControlCenterViewModel(SystemServices services)
    {
        _services = services;
        SearchText = string.Empty;
        StatusMessage = string.Empty;
        CatalogWarning = string.Empty;
        Refresh();
    }

    public ObservableCollection<TweakItemViewModel> Tweaks { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool HasCatalogWarning { get; set; }

    [ObservableProperty]
    public partial string CatalogWarning { get; set; }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    public void Refresh()
    {
        IsBusy = true;
        try
        {
            var problems = _services.Catalog.Validate();
            HasCatalogWarning = problems.Count > 0;
            CatalogWarning = string.Join(Environment.NewLine, problems);

            _all.Clear();
            foreach (var tweak in _services.Catalog.Tweaks)
            {
                _all.Add(new TweakItemViewModel(
                    tweak,
                    _services.Tweaks.GetStatus(tweak, Environment.OSVersion.Version.Build)));
            }

            ApplyFilter();
            StatusMessage = $"{_all.Count} tweaks · {_all.Count(tweak => tweak.Definition.Evidence == TweakEvidence.MicrosoftDocumented)} documented by Microsoft";
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or UnauthorizedAccessException
            or IOException
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
            or UnauthorizedAccessException
            or IOException
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
            or UnauthorizedAccessException
            or IOException
            or System.ComponentModel.Win32Exception)
        {
            return (false, ex.Message);
        }
    }

    private void ApplyFilter()
    {
        Tweaks.Clear();
        var query = _all.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(tweak =>
                tweak.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || tweak.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || tweak.Definition.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var tweak in query.OrderBy(item => item.Definition.Category)
                     .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase))
        {
            Tweaks.Add(tweak);
        }
    }
}
