using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SystemCompass.Core.Changes;
using SystemCompass.Core.Demo;
using SystemCompass.Core.Reporting;
using SystemCompass.Windows;

namespace SystemCompass.App.ViewModels;

public sealed partial class ReportViewModel : ObservableObject
{
    private readonly SystemServices _services;

    public ReportViewModel(SystemServices services, bool demo = false)
    {
        _services = services;
        PreviewDocument = new FlowDocument();
        ReportText = string.Empty;
        Notes = string.Empty;
        StatusMessage = "Reports are always redacted before they are shown or saved.";
        UseDemo = demo;
        Generate();
    }

    [ObservableProperty] public partial string ReportText { get; set; }
    [ObservableProperty] public partial FlowDocument PreviewDocument { get; set; }

    partial void OnReportTextChanged(string value) => PreviewDocument = BuildPreview(value);
    [ObservableProperty] public partial string Notes { get; set; }
    [ObservableProperty] public partial bool UseDemo { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; }

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
            or UnauthorizedAccessException or IOException
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
        var dialog = new SaveFileDialog
        {
            Title = "Save redacted report",
            Filter = "Markdown (*.md)|*.md|All files (*.*)|*.*",
            FileName = $"systemcompass-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.md",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var text = string.IsNullOrWhiteSpace(Notes)
                ? ReportText
                : ReportText + Environment.NewLine + Environment.NewLine
                  + "## Reviewer notes" + Environment.NewLine + Environment.NewLine
                  + _services.Redactor.Redact(Notes);
            File.WriteAllText(dialog.FileName, text);
            StatusMessage = $"Saved redacted report to {dialog.FileName}";
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
            Clipboard.SetText(ReportText);
            StatusMessage = "Report copied to the clipboard.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            StatusMessage = $"Copy failed: {ex.Message}";
        }
    }

    public void AppendNotes(string notes)
    {
        Notes = notes;
        StatusMessage = string.IsNullOrWhiteSpace(notes)
            ? "Reviewer notes cleared."
            : "Reviewer notes will be appended to the saved report.";
    }

    private static FlowDocument BuildPreview(string markdown)
    {
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            FontSize = 12,
            PagePadding = new Thickness(22),
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE4, 0xE4, 0xE7)),
        };

        foreach (var line in markdown.Split('\n').Take(500))
        {
            var text = line.TrimEnd();
            if (text.StartsWith("# ", StringComparison.Ordinal))
            {
                document.Blocks.Add(new Paragraph(new Run(text[2..]))
                {
                    FontSize = 22,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 4, 0, 10),
                });
            }
            else if (text.StartsWith("## ", StringComparison.Ordinal))
            {
                document.Blocks.Add(new Paragraph(new Run(text[3..]))
                {
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 12, 0, 6),
                });
            }
            else if (text.StartsWith("### ", StringComparison.Ordinal))
            {
                document.Blocks.Add(new Paragraph(new Run(text[4..]))
                {
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 10, 0, 4),
                });
            }
            else if (text.StartsWith('|') || text.StartsWith("```", StringComparison.Ordinal))
            {
                document.Blocks.Add(new Paragraph(new Run(text))
                {
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = 10.5,
                    Margin = new Thickness(0, 1, 0, 1),
                });
            }
            else if (string.IsNullOrWhiteSpace(text))
            {
                document.Blocks.Add(new Paragraph { Margin = new Thickness(0, 0, 0, 5) });
            }
            else
            {
                document.Blocks.Add(new Paragraph(new Run(text.Replace("- ", "• ", StringComparison.Ordinal)))
                {
                    Margin = new Thickness(0, 1, 0, 3),
                });
            }
        }

        return document;
    }

    private static string AppVersion =>
        typeof(ReportViewModel).Assembly.GetName().Version?.ToString(3)
        ?? "0.1.0";
}
