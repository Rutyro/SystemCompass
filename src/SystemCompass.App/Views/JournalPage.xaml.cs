using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SystemCompass.App.ViewModels;

namespace SystemCompass.App.Views;

public sealed partial class JournalPage : Page
{
    public JournalPage()
    {
        ViewModel = new JournalViewModel(App.Services, App.IsDemo);
        InitializeComponent();
    }

    public JournalViewModel ViewModel { get; }

    private async void OnRollbackClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: JournalRowViewModel row })
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Roll back this change?",
            Content = new TextBlock
            {
                Text = $"{row.EntryName}: {row.ActionLabel}. The recorded previous state is restored.",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "Roll back",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.Rollback(row);
        }
    }
}
