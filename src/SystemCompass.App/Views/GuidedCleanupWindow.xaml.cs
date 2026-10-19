using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using SystemCompass.App.ViewModels;
using SystemCompass.Core.Tweaks;
using Xceed.Wpf.Toolkit.Core;

namespace SystemCompass.App.Views;

public partial class GuidedCleanupWindow : Window, INotifyPropertyChanged
{
    private readonly bool _readOnly;

    public GuidedCleanupWindow(ControlCenterViewModel controlCenter)
    {
        InitializeComponent();
        _readOnly = controlCenter.IsDemo;
        Candidates = controlCenter.SelectableTweaks
            .Where(item => item.Status is TweakStatus.NotApplied or TweakStatus.Partial)
            .Where(item => item.Definition.Risk != TweakRisk.High)
            .OrderBy(item => item.Definition.Risk)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Selected = [];
        Selected.CollectionChanged += (_, _) =>
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
            CleanupWizard.CanFinish = !_readOnly && Selected.Count > 0 && ConfirmBox.IsChecked == true;
        };
        DataContext = this;
        CleanupWizard.CanFinish = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<TweakItemViewModel> Candidates { get; }

    public ObservableCollection<TweakItemViewModel> Selected { get; }

    public string Summary => (Selected.Count == 0
        ? "No tweaks selected."
        : $"{Selected.Count} tweak{(Selected.Count == 1 ? string.Empty : "s")} selected · "
          + $"{Selected.Sum(item => item.Definition.Operations.Count)} operations")
        + (_readOnly ? " · Demo mode is read-only" : string.Empty);

    private void OnHelp(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            "Pick the Low or Medium risk tweaks you want. The next page shows every operation before anything is written. "
            + "Each applied tweak captures its previous value and can be reverted from the journal.",
            "Guided cleanup help",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnConfirmChanged(object sender, RoutedEventArgs e)
    {
        CleanupWizard.CanFinish = !_readOnly && Selected.Count > 0 && ConfirmBox.IsChecked == true;
    }

    private void OnFinish(object sender, CancelRoutedEventArgs e)
    {
        DialogResult = true;
    }
}
