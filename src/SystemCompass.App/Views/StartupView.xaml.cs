using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using SystemCompass.App.ViewModels;
using SystemCompass.Core.Startup;
using Xceed.Wpf.Toolkit;
using MessageBox = Xceed.Wpf.Toolkit.MessageBox;

namespace SystemCompass.App.Views;

public partial class StartupView : UserControl
{
    public StartupView()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is StartupViewModel viewModel
            && viewModel.SelectedEntry is { } entry
            && Window.GetWindow(this)?.DataContext is MainViewModel shell)
        {
            shell.SelectStartup(entry);
        }
    }

    private async void OnToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: StartupEntryViewModel entry }
            || DataContext is not StartupViewModel viewModel)
        {
            return;
        }

        var desired = entry.Entry.State != Core.Startup.EntryState.Enabled;
        var owner = Window.GetWindow(this);
        var answer = MessageBox.Show(
            owner,
            entry.BuildDialogText(desired),
            desired ? "Enable startup entry?" : "Disable startup entry?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        var result = await viewModel.SetStateAsync(entry, desired);
        if (owner?.DataContext is MainViewModel shell)
        {
            shell.Log(result.Message);
        }
    }

    private void OnCopyPath(object sender, RoutedEventArgs e)
    {
        if (DataContext is StartupViewModel { SelectedEntry: { } entry })
        {
            Clipboard.SetText(entry.Detail);
        }
    }

    private void OnOpenLocation(object sender, RoutedEventArgs e)
    {
        if (DataContext is not StartupViewModel { SelectedEntry: { } entry })
        {
            return;
        }

        var path = entry.Entry.ResolvedPath;
        if (!LocalPath.IsLocalFile(path) || !File.Exists(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
        {
            UseShellExecute = true,
        });
    }

    private void OnShowTweaks(object sender, RoutedEventArgs e)
    {
        App.Window?.ShowDocument("tweaks");
    }
}
