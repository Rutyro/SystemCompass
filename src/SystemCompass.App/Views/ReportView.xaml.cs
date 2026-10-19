using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SystemCompass.App.ViewModels;

namespace SystemCompass.App.Views;

public partial class ReportView : UserControl
{
    public ReportView()
    {
        InitializeComponent();
        NotesBox.Document.Background = (Brush)FindResource("SubtleBrush");
        NotesBox.Document.Foreground = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));
    }

    private void OnApplyNotes(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReportViewModel viewModel)
        {
            var text = new TextRange(NotesBox.Document.ContentStart, NotesBox.Document.ContentEnd).Text.Trim();
            viewModel.AppendNotes(text);
        }
    }

    private void OnClearNotes(object sender, RoutedEventArgs e)
    {
        NotesBox.Document.Blocks.Clear();
        if (DataContext is ReportViewModel viewModel)
        {
            viewModel.AppendNotes(string.Empty);
        }
    }

    private void OnDemoToggled(object sender, RoutedEventArgs e)
    {
        if (DataContext is ReportViewModel viewModel)
        {
            viewModel.Generate();
        }
    }

    private void OnPrint(object sender, RoutedEventArgs e) => PrintReport();

    public void PrintReport()
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() == true && Preview.Document is IDocumentPaginatorSource source)
        {
            dialog.PrintDocument(source.DocumentPaginator, "SystemCompass report");
        }
    }
}
