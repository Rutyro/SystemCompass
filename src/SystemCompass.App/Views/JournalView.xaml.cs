using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SystemCompass.App.ViewModels;
using Xceed.Wpf.Toolkit;
using MessageBox = Xceed.Wpf.Toolkit.MessageBox;

namespace SystemCompass.App.Views;

public partial class JournalView : UserControl
{
    private JournalViewModel? _viewModel;

    public JournalView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Rows.CollectionChanged -= OnRowsChanged;
        }

        _viewModel = DataContext as JournalViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Rows.CollectionChanged += OnRowsChanged;
            BuildTimeline();
        }
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e) => BuildTimeline();

    private void BuildTimeline()
    {
        if (_viewModel is null)
        {
            return;
        }

        TimelineHost.BeginDate = _viewModel.TimelineBegin;
        TimelineHost.EndDate = _viewModel.TimelineEnd;
        TimelineHost.Children.Clear();
        foreach (var row in _viewModel.Rows.Take(40))
        {
            var border = new Border
            {
                Width = 150,
                Margin = new Thickness(4),
                Padding = new Thickness(8, 5, 8, 5),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x2E)),
                BorderBrush = row.StatusBrush,
                BorderThickness = new Thickness(3, 0, 0, 0),
                ToolTip = $"{row.TimeLabel}\n{row.EntryName}\n{row.ActionLabel} · {row.StatusLabel}",
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = row.EntryName,
                            Foreground = Brushes.White,
                            FontSize = 11,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                        },
                        new TextBlock
                        {
                            Text = row.StatusLabel,
                            Foreground = row.StatusBrush,
                            FontSize = 10,
                        },
                    },
                },
            };
            TimelinePanel.SetDate(border, row.Date);
            TimelineHost.Children.Add(border);
        }
    }

    private void OnRollbackClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: JournalRowViewModel row })
        {
            Rollback(row);
        }
    }

    private void OnRollbackSelected(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedRow is { } row)
        {
            Rollback(row);
        }
    }

    private void Rollback(JournalRowViewModel row)
    {
        if (_viewModel is null)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        var answer = MessageBox.Show(
            owner,
            $"Restore the state recorded before '{row.EntryName}'?",
            "Roll back change",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        var result = _viewModel.Rollback(row);
        if (owner?.DataContext is MainViewModel shell)
        {
            shell.Log(result.Message);
            shell.RefreshShellData();
        }
    }
}
