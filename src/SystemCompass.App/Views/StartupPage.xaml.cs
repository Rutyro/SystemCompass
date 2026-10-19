using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SystemCompass.App.ViewModels;

namespace SystemCompass.App.Views;

public sealed partial class StartupPage : Page
{
    private bool _syncing;

    public StartupPage()
    {
        ViewModel = new StartupViewModel(App.Services, App.IsDemo);
        InitializeComponent();
    }

    public StartupViewModel ViewModel { get; }

    private async void OnToggleToggled(object sender, RoutedEventArgs e)
    {
        if (_syncing
            || sender is not ToggleSwitch toggle
            || toggle.DataContext is not StartupEntryViewModel item)
        {
            return;
        }

        var desired = toggle.IsOn;
        if (desired == item.IsOn)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = desired ? "Enable startup entry?" : "Disable startup entry?",
            Content = new TextBlock
            {
                Text = item.BuildDialogText(desired),
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = desired ? "Enable" : "Disable",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            _syncing = true;
            toggle.IsOn = item.IsOn;
            _syncing = false;
            return;
        }

        await ViewModel.SetStateAsync(item, desired);
        _syncing = true;
        toggle.IsOn = item.IsOn;
        _syncing = false;
    }
}
