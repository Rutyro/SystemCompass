using System.Windows;
using System.Windows.Media;
using Xceed.Wpf.Toolkit;
using MessageBox = Xceed.Wpf.Toolkit.MessageBox;

namespace SystemCompass.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        AccentPicker.SelectedColor = ((SolidColorBrush)Application.Current.Resources["AccentBlueBrush"]).Color;
    }

    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        Application.Current.ThemeMode = DarkTheme.IsChecked == true
            ? ThemeMode.Dark
            : ThemeMode.Light;
    }

    private void OnAccentChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
    {
        if (e.NewValue is { } color)
        {
            Application.Current.Resources["AccentBlueBrush"] = new SolidColorBrush(color);
        }
    }

    private void OnApplyTerms(object sender, RoutedEventArgs e)
    {
        var terms = TermsBox.Text
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length > 0)
            .ToArray();
        foreach (var term in terms)
        {
            App.Services.Redactor.AddLiteral(term);
        }

        MessageBox.Show(
            this,
            terms.Length == 0
                ? "No terms were entered."
                : $"{terms.Length} term{(terms.Length == 1 ? string.Empty : "s")} added for this session.",
            "Redaction terms",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnResetLayout(object sender, RoutedEventArgs e)
    {
        if (Owner is MainWindow window)
        {
            window.ResetLayout();
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
