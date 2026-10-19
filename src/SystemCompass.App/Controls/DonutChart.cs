using System.Windows;
using System.Windows.Media;
using SystemCompass.App.ViewModels;

namespace SystemCompass.App.Controls;

/// <summary>Dependency-free donut chart rendered directly with DrawingContext.</summary>
public sealed class DonutChart : FrameworkElement
{
    public static readonly DependencyProperty SlicesProperty = DependencyProperty.Register(
        nameof(Slices), typeof(IEnumerable<ChartSlice>), typeof(DonutChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty InnerRatioProperty = DependencyProperty.Register(
        nameof(InnerRatio), typeof(double), typeof(DonutChart),
        new FrameworkPropertyMetadata(0.64, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GapDegreesProperty = DependencyProperty.Register(
        nameof(GapDegrees), typeof(double), typeof(DonutChart),
        new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(DonutChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<ChartSlice>? Slices
    {
        get => (IEnumerable<ChartSlice>?)GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    public double InnerRatio
    {
        get => (double)GetValue(InnerRatioProperty);
        set => SetValue(InnerRatioProperty, value);
    }

    public double GapDegrees
    {
        get => (double)GetValue(GapDegreesProperty);
        set => SetValue(GapDegreesProperty, value);
    }

    public Brush? TrackBrush
    {
        get => (Brush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var slices = Slices?.Where(slice => slice.Value > 0).ToArray() ?? [];
        var total = slices.Sum(slice => slice.Value);
        if (total <= 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var size = Math.Min(ActualWidth, ActualHeight);
        var outer = size / 2 - 4;
        var inner = outer * Math.Clamp(InnerRatio, 0.25, 0.94);
        var center = new Point(ActualWidth / 2, ActualHeight / 2);

        if (TrackBrush is not null)
        {
            DrawRing(drawingContext, center, outer, inner, 0, 360, TrackBrush);
        }

        var angle = -90d;
        foreach (var slice in slices)
        {
            var sweep = slice.Value * 360d / total;
            var gap = Math.Min(Math.Max(GapDegrees, 0), sweep / 3);
            DrawRing(drawingContext, center, outer, inner, angle + gap / 2, sweep - gap, slice.Brush);
            angle += sweep;
        }
    }

    private static void DrawRing(
        DrawingContext context,
        Point center,
        double outer,
        double inner,
        double start,
        double sweep,
        Brush brush)
    {
        for (var offset = 0d; offset < sweep - 0.01;)
        {
            var chunk = Math.Min(179.0, sweep - offset);
            var geometry = BuildSegment(center, outer, inner, start + offset, start + offset + chunk);
            context.DrawGeometry(brush, null, geometry);
            offset += chunk;
        }
    }

    private static StreamGeometry BuildSegment(
        Point center, double outer, double inner, double startAngle, double endAngle)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(PointAt(center, outer, startAngle), isFilled: true, isClosed: true);
            context.ArcTo(
                PointAt(center, outer, endAngle),
                new Size(outer, outer), 0,
                endAngle - startAngle > 180,
                SweepDirection.Clockwise, true, false);
            context.LineTo(PointAt(center, inner, endAngle), true, false);
            context.ArcTo(
                PointAt(center, inner, startAngle),
                new Size(inner, inner), 0,
                endAngle - startAngle > 180,
                SweepDirection.Counterclockwise, true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Point PointAt(Point center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180d;
        return new Point(
            center.X + radius * Math.Cos(radians),
            center.Y + radius * Math.Sin(radians));
    }
}
