using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SystemCompass.App.ViewModels;

namespace SystemCompass.App.Views;

public partial class StartupMapView : UserControl
{
    public StartupMapView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BuildMap();
        Loaded += (_, _) =>
        {
            BuildMap();
            FitMap();
        };
    }

    private void BuildMap()
    {
        MapCanvas.Children.Clear();
        if (DataContext is not StartupViewModel viewModel)
        {
            return;
        }

        var groups = viewModel.AllItems
            .GroupBy(item => item.MechanismLabel)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var maximum = Math.Max(1, groups.Select(group => group.Count()).DefaultIfEmpty(1).Max());
        MapCanvas.Width = Math.Max(700, 70 + groups.Length * 220 + 30);
        MapCanvas.Height = Math.Max(720, 130 + maximum * 88);

        for (var index = 0; index < groups.Length; index++)
        {
            var group = groups[index];
            var x = 70 + index * 220;
            var hub = new Border
            {
                Width = 190,
                Height = 44,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x2E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46)),
                BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = group.Key,
                            Foreground = Brushes.White,
                            FontWeight = FontWeights.SemiBold,
                            FontSize = 12,
                            HorizontalAlignment = HorizontalAlignment.Center,
                        },
                        new TextBlock
                        {
                            Text = $"{group.Count()} entries",
                            Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xAA)),
                            FontSize = 10,
                            HorizontalAlignment = HorizontalAlignment.Center,
                        },
                    },
                },
            };
            Canvas.SetLeft(hub, x);
            Canvas.SetTop(hub, 20);
            MapCanvas.Children.Add(hub);

            var row = 0;
            foreach (var item in group.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                var y = 102 + row * 84;
                var line = new Line
                {
                    X1 = x + 95,
                    Y1 = 64,
                    X2 = x + 95,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
                    StrokeThickness = 1,
                };
                MapCanvas.Children.Add(line);

                var node = CreateNode(item, x, y);
                MapCanvas.Children.Add(node);
                row++;
            }
        }
    }

    private Border CreateNode(StartupEntryViewModel item, double x, double y)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        if (item.Icon is { } icon)
        {
            header.Children.Add(new Image
            {
                Source = icon,
                Width = 15,
                Height = 15,
                Margin = new Thickness(0, 0, 6, 0),
            });
        }

        header.Children.Add(new TextBlock
        {
            Text = item.Name,
            Foreground = Brushes.White,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 150,
        });

        var border = new Border
        {
            Width = 190,
            Height = 66,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x22)),
            BorderBrush = item.CategoryBrush,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(9, 7, 9, 7),
            Cursor = Cursors.Hand,
            ToolTip = $"{item.Name}\n{item.MetaLabel}\n{item.Detail}",
            Child = new StackPanel
            {
                Children =
                {
                    header,
                    new TextBlock
                    {
                        Text = item.StateLabel,
                        Foreground = item.CategoryBrush,
                        FontSize = 10,
                        Margin = new Thickness(0, 4, 0, 0),
                    },
                },
            },
        };
        border.MouseLeftButtonUp += (_, _) =>
        {
            if (Window.GetWindow(this)?.DataContext is MainViewModel shell)
            {
                shell.SelectStartup(item);
                shell.Log($"Selected {item.Name} from the startup map.");
            }
        };
        Canvas.SetLeft(border, x);
        Canvas.SetTop(border, y);
        return border;
    }

    private void OnFit(object sender, RoutedEventArgs e) => FitMap();

    private void FitMap() => MapZoom.FitToBounds();
}
