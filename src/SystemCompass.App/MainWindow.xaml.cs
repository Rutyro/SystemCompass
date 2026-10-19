using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SystemCompass.App.Views;
using WinColor = global::Windows.UI.Color;

namespace SystemCompass.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop();
        }

        var titleBar = AppWindow.TitleBar;
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var background = WinColor.FromArgb(255, 26, 26, 28);
            var foreground = WinColor.FromArgb(255, 240, 240, 240);
            var inactiveForeground = WinColor.FromArgb(255, 190, 190, 190);
            titleBar.BackgroundColor = background;
            titleBar.InactiveBackgroundColor = background;
            titleBar.ForegroundColor = foreground;
            titleBar.InactiveForegroundColor = inactiveForeground;
            titleBar.ButtonBackgroundColor = background;
            titleBar.ButtonInactiveBackgroundColor = background;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonInactiveForegroundColor = inactiveForeground;
            titleBar.ButtonHoverBackgroundColor = WinColor.FromArgb(255, 45, 45, 48);
            titleBar.ButtonPressedBackgroundColor = WinColor.FromArgb(255, 60, 60, 64);
        }

        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1280, 840));
        var startItem = App.StartPage is { Length: > 0 }
            ? Nav.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(item => string.Equals((string?)item.Tag, App.StartPage, StringComparison.OrdinalIgnoreCase))
            : null;
        Nav.SelectedItem = startItem ?? Nav.MenuItems[0];
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string tag })
        {
            return;
        }

        var pageType = tag switch
        {
            "startup" => typeof(StartupPage),
            "tweaks" => typeof(ControlCenterPage),
            "report" => typeof(ReportPage),
            "journal" => typeof(JournalPage),
            _ => null,
        };

        if (pageType is not null && ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
