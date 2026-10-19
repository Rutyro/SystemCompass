using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using SystemCompass.App.ViewModels;
using SystemCompass.App.Views;
using MessageBox = Xceed.Wpf.Toolkit.MessageBox;

namespace SystemCompass.App;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, object> _contentById = new(StringComparer.OrdinalIgnoreCase);
    private bool _layoutReady;

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel(App.Services, App.IsDemo);
        DataContext = ViewModel;
        ViewModel.NavigateRequested += ShowDocument;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    public MainViewModel ViewModel { get; }

    private string LayoutPath => Path.Combine(App.Services.DataDirectory, "layout.xml");

    private string DefaultLayoutPath => Path.Combine(App.Services.DataDirectory, "layout.default.xml");

    public void ShowDocument(string tag)
    {
        var document = FindContents().OfType<LayoutDocument>()
            .FirstOrDefault(candidate =>
                string.Equals(candidate.ContentId, tag, StringComparison.OrdinalIgnoreCase));
        if (document is null)
        {
            return;
        }

        document.IsActive = true;
        document.IsSelected = true;
        DockManager.ActiveContent = document.Content;
    }

    public void ResetLayout()
    {
        if (File.Exists(DefaultLayoutPath))
        {
            DeserializeLayout(DefaultLayoutPath);
        }

        ViewModel.Log("Layout reset to the default.");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildContentMap();
        Directory.CreateDirectory(App.Services.DataDirectory);

        if (File.Exists(LayoutPath))
        {
            DeserializeLayout(LayoutPath);
        }
        else
        {
            SaveLayout(DefaultLayoutPath);
        }

        _layoutReady = true;
        ShowDocument(App.StartPage ?? "overview");

        if (!string.IsNullOrWhiteSpace(App.ScreenshotPath))
        {
            Window? dialog = App.ScreenshotDialog?.ToLowerInvariant() switch
            {
                "settings" => new SettingsWindow { Owner = this },
                "cleanup" => new GuidedCleanupWindow(ViewModel.ControlCenter) { Owner = this },
                _ => null,
            };
            dialog?.Show();
            var target = dialog ?? (Window)this;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                RenderScreenshot(target, App.ScreenshotPath!);
                Application.Current.Shutdown();
            };
            timer.Start();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_layoutReady)
        {
            SaveLayout(LayoutPath);
        }
    }

    private void BuildContentMap()
    {
        _contentById.Clear();
        foreach (var content in FindContents())
        {
            if (!string.IsNullOrWhiteSpace(content.ContentId) && content.Content is not null)
            {
                _contentById[content.ContentId] = content.Content;
            }
        }
    }

    private IEnumerable<LayoutContent> FindContents()
    {
        if (DockManager.Layout is not { } layout || layout.RootPanel is null)
        {
            yield break;
        }

        foreach (var content in Walk(layout.RootPanel))
        {
            yield return content;
        }

        foreach (var content in layout.Hidden)
        {
            yield return content;
        }

        foreach (var floating in layout.FloatingWindows)
        {
            foreach (var content in Walk(floating))
            {
                yield return content;
            }
        }
    }

    private static IEnumerable<LayoutContent> Walk(ILayoutContainer? container)
    {
        if (container?.Children is null)
        {
            yield break;
        }

        foreach (var child in container.Children)
        {
            if (child is LayoutContent content)
            {
                yield return content;
            }

            if (child is ILayoutContainer nested)
            {
                foreach (var descendant in Walk(nested))
                {
                    yield return descendant;
                }
            }
        }
    }

    private void DeserializeLayout(string path)
    {
        try
        {
            var serializer = new XmlLayoutSerializer(DockManager);
            serializer.LayoutSerializationCallback += (_, args) =>
            {
                if (args.Model.ContentId is { } id
                    && _contentById.TryGetValue(id, out var content))
                {
                    args.Content = content;
                }
            };
            serializer.Deserialize(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Xml.XmlException)
        {
            ViewModel.Log($"Layout could not be restored: {ex.Message}");
        }
    }

    private void SaveLayout(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            new XmlLayoutSerializer(DockManager).Serialize(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ViewModel.Log($"Layout could not be saved: {ex.Message}");
        }
    }

    private void OnExplorerSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ExplorerNode node)
        {
            ViewModel.SelectedExplorerNode = node;
        }
    }

    private void OnShowDocument(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string tag })
        {
            ShowDocument(tag);
        }
    }

    private void OnToggleExplorer(object sender, RoutedEventArgs e) => ToggleAnchorable("explorer");

    private void OnToggleProperties(object sender, RoutedEventArgs e)
    {
        ToggleAnchorable("properties");
        DetailsToggle.IsChecked = FindAnchorable("properties")?.IsVisible == true;
    }

    private void OnToggleBottom(object sender, RoutedEventArgs e)
    {
        var visible = false;
        foreach (var id in new[] { "operations", "activity", "timeline" })
        {
            var anchorable = FindAnchorable(id);
            if (anchorable is null)
            {
                continue;
            }

            if (anchorable.IsVisible)
            {
                anchorable.Hide();
            }
            else
            {
                anchorable.Show();
                visible = true;
            }
        }

        ViewModel.Log(visible ? "Bottom pane shown." : "Bottom pane hidden.");
    }

    private void ToggleAnchorable(string contentId)
    {
        var anchorable = FindAnchorable(contentId);
        if (anchorable is null)
        {
            return;
        }

        if (anchorable.IsVisible)
        {
            anchorable.Hide();
        }
        else
        {
            anchorable.Show();
        }
    }

    private LayoutAnchorable? FindAnchorable(string contentId) =>
        FindContents().OfType<LayoutAnchorable>().FirstOrDefault(candidate =>
            string.Equals(candidate.ContentId, contentId, StringComparison.OrdinalIgnoreCase));

    private void OnToggleDetails(object sender, RoutedEventArgs e)
    {
        if (!_layoutReady)
        {
            return;
        }

        var anchorable = FindAnchorable("properties");
        if (anchorable is null)
        {
            return;
        }

        if (DetailsToggle.IsChecked == true)
        {
            anchorable.Show();
        }
        else
        {
            anchorable.Hide();
        }
    }

    private void OnScrollActivity(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ActivityLog.LastOrDefault() is { } last)
        {
            ActivityList.ScrollIntoView(last);
        }
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        ShowDocument("report");
        ViewModel.Report.SaveCommand.Execute(null);
    }

    private void OnCopyReport(object sender, RoutedEventArgs e)
    {
        ViewModel.Report.CopyCommand.Execute(null);
        ViewModel.Log(ViewModel.Report.StatusMessage);
    }

    private void OnPrintReport(object sender, RoutedEventArgs e)
    {
        ShowDocument("report");
        var view = FindContents()
            .OfType<LayoutDocument>()
            .FirstOrDefault(document => document.ContentId == "report")?
            .Content as ReportView;
        view?.PrintReport();
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        new SettingsWindow { Owner = this }.ShowDialog();
    }

    private async void OnGuidedCleanup(object sender, RoutedEventArgs e)
    {
        var window = new GuidedCleanupWindow(ViewModel.ControlCenter) { Owner = this };
        if (window.ShowDialog() != true || window.Selected.Count == 0)
        {
            return;
        }

        var applied = 0;
        foreach (var item in window.Selected)
        {
            var result = await ViewModel.ControlCenter.ApplyAsync(item);
            ViewModel.Log(result.Message);
            if (result.Success)
            {
                applied++;
            }
        }

        ViewModel.RefreshShellData();
        MessageBox.Show(
            this,
            $"{applied} of {window.Selected.Count} selected tweaks were applied and verified. Revert any of them from the Control Center or the journal.",
            "Guided cleanup",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnAbout(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            "SystemCompass 0.1.0\n\nRead-only-first startup auditing with verified, reversible changes.\n\n"
            + "Independent project. Not affiliated with Microsoft.\n"
            + "MIT licensed. No network access, no telemetry.",
            "About SystemCompass",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnExit(object sender, RoutedEventArgs e) => Close();

    private void OnResetLayout(object sender, RoutedEventArgs e) => ResetLayout();

    private void OnSaveLayout(object sender, RoutedEventArgs e)
    {
        SaveLayout(LayoutPath);
        ViewModel.Log("Layout saved.");
    }

    private static void RenderScreenshot(Window window, string path)
    {
        var width = (int)Math.Max(1, window.ActualWidth);
        var height = (int)Math.Max(1, window.ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
