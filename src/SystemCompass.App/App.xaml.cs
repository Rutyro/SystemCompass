using System.Globalization;
using System.Windows;
using SystemCompass.Windows;

namespace SystemCompass.App;

public partial class App : Application
{
    public App()
    {
        var arguments = Environment.GetCommandLineArgs();
        IsDemo = arguments.Any(argument => argument.Equals("--demo", StringComparison.OrdinalIgnoreCase));
        StartPage = arguments.FirstOrDefault(argument =>
                argument.StartsWith("--page=", StringComparison.OrdinalIgnoreCase))
            ?["--page=".Length..];
        ScreenshotPath = arguments.FirstOrDefault(argument =>
                argument.StartsWith("--screenshot=", StringComparison.OrdinalIgnoreCase))
            ?["--screenshot=".Length..];
        ScreenshotDialog = arguments.FirstOrDefault(argument =>
                argument.StartsWith("--dialog=", StringComparison.OrdinalIgnoreCase))
            ?["--dialog=".Length..];
        ScreenshotDelay = arguments.FirstOrDefault(argument =>
                argument.StartsWith("--screenshot-delay=", StringComparison.OrdinalIgnoreCase)) is { } delay
            && double.TryParse(delay["--screenshot-delay=".Length..], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var seconds)
                ? Math.Clamp(seconds, 0.5, 30)
                : 1.5;
        Services = SystemServices.Create();
    }

    public static SystemServices Services { get; private set; } = null!;

    /// <summary>Demo mode uses fictional data, never a real machine.</summary>
    public static bool IsDemo { get; private set; }

    public static string? StartPage { get; private set; }

    public static string? ScreenshotPath { get; private set; }

    /// <summary>With --screenshot, opens a dialog instead of only the main window.</summary>
    public static string? ScreenshotDialog { get; private set; }

    public static double ScreenshotDelay { get; private set; }

    public static MainWindow? Window { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Window = new MainWindow();
        MainWindow = Window;
        Window.Show();
    }
}
