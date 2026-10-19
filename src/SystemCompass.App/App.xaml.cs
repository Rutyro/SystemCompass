using Microsoft.UI.Xaml;
using SystemCompass.Windows;

namespace SystemCompass.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        var arguments = Environment.GetCommandLineArgs();
        IsDemo = arguments.Any(argument => argument.Equals("--demo", StringComparison.OrdinalIgnoreCase));
        StartPage = arguments.FirstOrDefault(argument =>
                argument.StartsWith("--page=", StringComparison.OrdinalIgnoreCase))
            ?["--page=".Length..];
        Services = SystemServices.Create();
    }

    public static SystemServices Services { get; private set; } = null!;

    /// <summary>
    /// Demo mode shows fictional startup entries and a fictional report so
    /// screenshots never contain data from a real machine.
    /// </summary>
    public static bool IsDemo { get; private set; }

    /// <summary>Optional navigation target used for demo screenshots.</summary>
    public static string? StartPage { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
