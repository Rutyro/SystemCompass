using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SystemCompass.App.ViewModels;

namespace SystemCompass.App.Views;

public sealed partial class ControlCenterPage : Page
{
    public ControlCenterPage()
    {
        ViewModel = new ControlCenterViewModel(App.Services);
        InitializeComponent();
    }

    public ControlCenterViewModel ViewModel { get; }

    private async void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: TweakItemViewModel item })
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = $"Apply '{item.Title}'?",
            Content = new TextBlock { Text = item.DialogText, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.ApplyAsync(item);
        }
    }

    private async void OnRevertClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: TweakItemViewModel item })
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = $"Revert '{item.Title}'?",
            Content = new TextBlock
            {
                Text = "The previously recorded value is restored.",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "Revert",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.RevertAsync(item);
        }
    }
}
