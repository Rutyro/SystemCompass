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
        Services = SystemServices.Create();
    }

    public static SystemServices Services { get; private set; } = null!;

    /// <summary>Demo mode uses fictional data, never a real machine.</summary>
    public static bool IsDemo { get; private set; }

    public static string? StartPage { get; private set; }

    public static string? ScreenshotPath { get; private set; }

    /// <summary>With --screenshot, opens a dialog instead of only the main window.</summary>
    public static string? ScreenshotDialog { get; private set; }

    public static MainWindow? Window { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Window = new MainWindow();
        MainWindow = Window;
        Window.Show();
    }
}
