using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SystemCompass.Core.Changes;
using SystemCompass.Core.Demo;
using SystemCompass.Core.Reporting;
using SystemCompass.Windows;
using Windows.ApplicationModel.DataTransfer;

namespace SystemCompass.App.ViewModels;

public sealed partial class ReportViewModel : ObservableObject
{
    private readonly SystemServices _services;

    public ReportViewModel(SystemServices services, bool demo = false)
    {
        _services = services;
        ReportText = string.Empty;
        StatusMessage = "Reports are always redacted before they are shown or saved.";
        UseDemo = demo;
        Generate();
    }

    [ObservableProperty]
    public partial string ReportText { get; set; }

    [ObservableProperty]
    public partial bool UseDemo { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [RelayCommand]
    public void Generate()
    {
        IsBusy = true;
        try
        {
            if (UseDemo)
            {
                var demo = DemoDataSet.CreateReport(DateTimeOffset.UtcNow, AppVersion);
                ReportText = new ReportBuilder(DemoDataSet.CreateRedactor()).BuildMarkdown(demo);
                StatusMessage = "Demo report generated from fictional data.";
                return;
            }

            var report = _services.Diagnostics.Create(
                _services.Audit.EnumerateAll(),
                JournalView.LatestPerChange(_services.Journal.ReadAll()),
                AppVersion);
            ReportText = _services.Reports.BuildMarkdown(report);
            StatusMessage = "Redacted report generated from this machine.";
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or UnauthorizedAccessException
            or IOException
            or System.ComponentModel.Win32Exception)
        {
            StatusMessage = $"Report failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void Save()
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SystemCompass");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(
                directory,
                string.Create(CultureInfo.InvariantCulture,
                    $"report-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.md"));
            File.WriteAllText(path, ReportText);
            StatusMessage = $"Saved redacted report to {path}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Copy()
    {
        try
        {
            var package = new DataPackage();
            package.SetText(ReportText);
            Clipboard.SetContent(package);
            StatusMessage = "Report copied to the clipboard.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            StatusMessage = $"Copy failed: {ex.Message}";
        }
    }

    private static string AppVersion =>
        typeof(ReportViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
}
