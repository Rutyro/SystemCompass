using System.Windows;
using System.Windows.Controls;
using SystemCompass.App.ViewModels;
using Xceed.Wpf.Toolkit;
using MessageBox = Xceed.Wpf.Toolkit.MessageBox;

namespace SystemCompass.App.Views;

public partial class ControlCenterView : UserControl
{
    public ControlCenterView()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ControlCenterViewModel viewModel
            && viewModel.SelectedTweak is { } tweak
            && Window.GetWindow(this)?.DataContext is MainViewModel shell)
        {
            shell.SelectTweak(tweak);
        }
    }

    private async void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: TweakItemViewModel item }
            || DataContext is not ControlCenterViewModel viewModel)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        var answer = MessageBox.Show(
            owner, item.DialogText, $"Apply '{item.Title}'?",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        var result = await viewModel.ApplyAsync(item);
        if (owner?.DataContext is MainViewModel shell)
        {
            shell.Log(result.Message);
            shell.RefreshShellData();
        }
    }

    private async void OnRevertClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: TweakItemViewModel item }
            || DataContext is not ControlCenterViewModel viewModel)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        var answer = MessageBox.Show(
            owner, "The previously recorded value is restored.", $"Revert '{item.Title}'?",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        var result = await viewModel.RevertAsync(item);
        if (owner?.DataContext is MainViewModel shell)
        {
            shell.Log(result.Message);
            shell.RefreshShellData();
        }
    }
}
