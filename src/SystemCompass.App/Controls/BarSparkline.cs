using System.Windows;
using System.Windows.Media;

namespace SystemCompass.App.Controls;

/// <summary>Compact activity bars rendered without a charting dependency.</summary>
public sealed class BarSparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IEnumerable<int>), typeof(BarSparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush), typeof(Brush), typeof(BarSparkline),
        new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(BarSparkline),
        new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<int>? Values
    {
        get => (IEnumerable<int>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush BarBrush
    {
        get => (Brush)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var values = Values?.ToArray() ?? [];
        if (values.Length == 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var maximum = Math.Max(1, values.Max());
        var width = ActualWidth;
        var height = ActualHeight;
        var gap = Math.Max(0, Gap);
        var barWidth = Math.Max(1, (width - gap * (values.Length - 1)) / values.Length);
        var brush = BarBrush;

        for (var index = 0; index < values.Length; index++)
        {
            var value = Math.Max(0, values[index]);
            var barHeight = value == 0 ? 2 : Math.Max(3, value / (double)maximum * (height - 4));
            var rect = new Rect(
                index * (barWidth + gap),
                height - barHeight,
                barWidth,
                barHeight);
            drawingContext.PushOpacity(value == 0 ? 0.16 : 0.42 + 0.58 * value / maximum);
            drawingContext.DrawRoundedRectangle(brush, null, rect, 2, 2);
            drawingContext.Pop();
        }
    }
}
