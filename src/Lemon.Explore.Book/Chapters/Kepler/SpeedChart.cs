using System.ComponentModel;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Kepler;

/// <summary>
/// One round of the orbit from perihelion to perihelion: the speed (km/s, left) and the distance from the Sun (dashed),
/// one high where the other is low, and now.
/// </summary>
public sealed class SpeedChart : Control
{
    static SpeedChart()
    {
        AffectsRender<SpeedChart>(PhaseProperty, EccentricityProperty, SemiMajorAxisProperty);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    public static readonly StyledProperty<double> PhaseProperty =
        AvaloniaProperty.Register<SpeedChart, double>(nameof(Phase), 0.0);

    public static readonly StyledProperty<double> EccentricityProperty =
        AvaloniaProperty.Register<SpeedChart, double>(nameof(Eccentricity), 0.5);

    public static readonly StyledProperty<double> SemiMajorAxisProperty =
        AvaloniaProperty.Register<SpeedChart, double>(nameof(SemiMajorAxis), 1.0);

    public double Phase
    {
        get => GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

    public double Eccentricity
    {
        get => GetValue(EccentricityProperty);
        set => SetValue(EccentricityProperty, value);
    }

    /// <summary>AU, for the speeds in km/s.</summary>
    public double SemiMajorAxis
    {
        get => GetValue(SemiMajorAxisProperty);
        set => SetValue(SemiMajorAxisProperty, value);
    }

    private const double Left = 40;
    private const double Right = 12;
    private const double Top = 24;
    private const double Bottom = 22;

    private readonly LabelCache _labels;

    public SpeedChart()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        _labels.Clear();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < Left + Right + 60 || height < Top + Bottom + 40) return;

        var e = Math.Clamp(Eccentricity, 0, KeplerGeometry.MaxEccentricity);
        var a = SemiMajorAxis;
        var plot = new Rect(Left, Top, width - Left - Right, height - Top - Bottom);

        // Speeds from aphelion to perihelion, with a little room so a circle's flat line sits in the middle.
        var slowest = KeplerGeometry.SpeedKmS(a, 1 + e);
        var fastest = KeplerGeometry.SpeedKmS(a, 1 - e);
        var pad = Math.Max((fastest - slowest) * 0.08, fastest * 0.05);
        var (min, max) = (Math.Max(0, slowest - pad), fastest + pad);
        double X(double phase) => plot.X + (plot.Width * phase);
        double SpeedY(double v) => plot.Bottom - (plot.Height * (v - min) / (max - min));

        // Distance on the same height, scaled from perihelion (bottom) to aphelion (top) of the same band.
        double DistanceY(double r) => e < 1e-4 ? plot.Y + (plot.Height / 2) : plot.Bottom - (plot.Height * (0.08 + (0.84 * (r - (1 - e)) / (2 * e))));

        // The axis: slowest and fastest speed, and the middle of the round at aphelion.
        foreach (var v in new[] { slowest, fastest })
        {
            var y = SpeedY(v);
            dc.DrawLine(Paint.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            var text = _labels.Text(v.ToString(v < 10 ? "0.0" : "0"), 9.5, Paint.Axis);
            dc.DrawText(text, new Point(plot.Left - text.Width - 6, y - (text.Height / 2)));
        }

        var unit = _labels.Get("Kepler_Chart_Kms", 9.5, KeplerDrawing.Fast);
        dc.DrawText(unit, new Point(4, 4));

        foreach (var (phase, key) in new[] { (0.0, "Kepler_Chart_Perihelion"), (0.5, "Kepler_Chart_Aphelion"), (1.0, "Kepler_Chart_Perihelion") })
        {
            var x = X(phase);
            dc.DrawLine(Paint.Tick, new Point(x, plot.Bottom), new Point(x, plot.Bottom + 4));
            if (phase == 0.5) dc.DrawLine(Paint.Midline, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var label = _labels.Get(key, 9.5, Paint.Axis);
            var left = Math.Clamp(x - (label.Width / 2), 0, width - label.Width);
            dc.DrawText(label, new Point(left, plot.Bottom + 5));
        }

        dc.DrawGeometry(null, Paint.DistancePen, Curve(plot, p => DistanceY(KeplerGeometry.At(p, e).Radius)));
        dc.DrawGeometry(null, Paint.SpeedPen, Curve(plot, p => SpeedY(KeplerGeometry.SpeedKmS(a, KeplerGeometry.At(p, e).Radius))));

        // Legend along the top right.
        var speed = _labels.Get("Kepler_Chart_Speed", 9.5, KeplerDrawing.Fast);
        var distance = _labels.Get("Kepler_Chart_Distance", 9.5, KeplerDrawing.Slow);
        var legendX = plot.Right - distance.Width - speed.Width - 40;
        dc.DrawLine(Paint.SpeedPen, new Point(legendX, 10), new Point(legendX + 12, 10));
        dc.DrawText(speed, new Point(legendX + 16, 10 - (speed.Height / 2)));
        legendX += speed.Width + 24;
        dc.DrawLine(Paint.DistancePen, new Point(legendX, 10), new Point(legendX + 12, 10));
        dc.DrawText(distance, new Point(legendX + 16, 10 - (distance.Height / 2)));

        var phaseNow = KeplerGeometry.Wrap(Phase);
        var nowX = X(phaseNow);
        dc.DrawLine(Paint.Now, new Point(nowX, plot.Top), new Point(nowX, plot.Bottom));
        var point = KeplerGeometry.At(phaseNow, e);
        dc.DrawEllipse(Paint.DistanceDot, KeplerDrawing.DotOutline, new Point(nowX, DistanceY(point.Radius)), 3.5, 3.5);
        dc.DrawEllipse(KeplerDrawing.FastBrush, KeplerDrawing.DotOutline, new Point(nowX, SpeedY(KeplerGeometry.SpeedKmS(a, point.Radius))), 4.5, 4.5);
    }

    private static StreamGeometry Curve(Rect plot, Func<double, double> yAt)
    {
        const int steps = 240;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i <= steps; i++)
            {
                var phase = (double)i / steps;
                var point = new Point(plot.X + (plot.Width * phase), yAt(phase));
                if (i == 0) ctx.BeginFigure(point, false);
                else ctx.LineTo(point);
            }

            ctx.EndFigure(false);
        }
        return geometry;
    }

    private static class Paint
    {
        public static readonly Color Axis = Color.FromArgb(0xC0, 0xC9, 0xD1, 0xDC);

        public static readonly IPen Tick = MakePen(Solid(90, 255, 255, 255), 1);
        public static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
        public static readonly IPen Midline = MakePen(Solid(40, 255, 255, 255), 1, dashed: true);
        public static readonly IPen Now = MakePen(Solid(150, 255, 255, 255), 1);
        public static readonly IPen SpeedPen = MakePen(SpaceDrawing.Solid(KeplerDrawing.Fast), 2);
        public static readonly IPen DistancePen = MakePen(SpaceDrawing.Solid(KeplerDrawing.Slow), 1.6, dashed: true);
        public static readonly IBrush DistanceDot = SpaceDrawing.Solid(KeplerDrawing.Slow);
    }
}
