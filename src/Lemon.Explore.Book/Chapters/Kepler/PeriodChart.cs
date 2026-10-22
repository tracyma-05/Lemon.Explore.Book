using System.ComponentModel;
using System.Globalization;
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
/// The third law on logarithmic scales: the size of each orbit (AU) across, its period (years) up. T² = a³ is a straight
/// line of slope 3/2, and the planets, Ceres, Pluto and Halley's comet all sit on it, with your planet.
/// </summary>
public sealed class PeriodChart : Control
{
    static PeriodChart()
    {
        AffectsRender<PeriodChart>(YourAxisProperty);
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

    public static readonly StyledProperty<double> YourAxisProperty =
        AvaloniaProperty.Register<PeriodChart, double>(nameof(YourAxis), 2.5);

    /// <summary>Your planet's distance from the Sun, AU.</summary>
    public double YourAxis
    {
        get => GetValue(YourAxisProperty);
        set => SetValue(YourAxisProperty, value);
    }

    private const double Left = 36;
    private const double Right = 12;
    private const double Top = 12;
    private const double Bottom = 34;
    private const double MinAu = 0.2;
    private const double MaxAu = 60;
    private const double MinYears = 0.06;
    private const double MaxYears = 600;

    private readonly LabelCache _labels;

    public PeriodChart()
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
        if (width < Left + Right + 80 || height < Top + Bottom + 60) return;

        var plot = new Rect(Left, Top, width - Left - Right, height - Top - Bottom);
        double X(double au) => plot.X + (plot.Width * Math.Log(au / MinAu) / Math.Log(MaxAu / MinAu));
        double Y(double years) => plot.Bottom - (plot.Height * Math.Log(years / MinYears) / Math.Log(MaxYears / MinYears));

        foreach (var au in new[] { 0.3, 1, 3, 10, 30 })
        {
            var x = X(au);
            dc.DrawLine(Paint.Grid, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var text = _labels.Text(au.ToString(CultureInfo.InvariantCulture), 9.5, Paint.Axis);
            dc.DrawText(text, new Point(x - (text.Width / 2), plot.Bottom + 3));
        }

        foreach (var years in new[] { 0.1, 1, 10, 100 })
        {
            var y = Y(years);
            dc.DrawLine(Paint.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            var text = _labels.Text(years.ToString(CultureInfo.InvariantCulture), 9.5, Paint.Axis);
            dc.DrawText(text, new Point(plot.Left - text.Width - 5, y - (text.Height / 2)));
        }

        var xAxis = _labels.Get("Kepler_Chart_AxisAu", 9.5, Paint.Axis);
        dc.DrawText(xAxis, new Point(plot.Right - xAxis.Width, plot.Bottom + 17));
        var yAxis = _labels.Get("Kepler_Chart_AxisYears", 9.5, Paint.Axis);
        dc.DrawText(yAxis, new Point(plot.Left + 4, plot.Top + 2));

        // T = a^1.5 across the whole plot.
        dc.DrawLine(Paint.Law, new Point(X(MinAu), Y(KeplerGeometry.PeriodYears(MinAu))), new Point(X(MaxAu), Y(KeplerGeometry.PeriodYears(MaxAu))));
        // Its name as a legend in the empty top left, under the axis title.
        var law = _labels.Get("Kepler_Chart_Law", 10.5, KeplerDrawing.Gold, true);
        var legend = new Point(plot.Left + 8, plot.Top + 22 + (law.Height / 2));
        dc.DrawLine(Paint.Law, legend, legend + new Vector(18, 0));
        dc.DrawText(law, new Point(legend.X + 24, legend.Y - (law.Height / 2)));

        // Measured periods; labels alternate sides of the line so neighbours stay apart.
        var index = 0;
        foreach (var point in KeplerGeometry.PeriodPoints)
        {
            var at = new Point(X(point.SemiMajorAxisAu), Y(point.PeriodYears));
            dc.DrawEllipse(Paint.Dot, KeplerDrawing.DotOutline, at, 3.5, 3.5);
            var name = _labels.Get(KeplerDiagram.BodyNameKey(point.Id), 9, KeplerDrawing.DimText);
            var below = index++ % 2 == 0;
            var left = below ? at.X + 5 : at.X - name.Width - 5;
            dc.DrawText(name, new Point(Math.Clamp(left, plot.Left, plot.Right - name.Width), below ? at.Y + 1 : at.Y - name.Height - 1));
        }

        var yours = Math.Clamp(YourAxis, MinAu, MaxAu);
        var you = new Point(X(yours), Y(KeplerGeometry.PeriodYears(yours)));
        dc.DrawEllipse(Paint.YouGlow, null, you, 10, 10);
        dc.DrawEllipse(KeplerDrawing.YouBrush, KeplerDrawing.DotOutline, you, 5, 5);
    }

    private static class Paint
    {
        public static readonly Color Axis = Color.FromArgb(0xC0, 0xC9, 0xD1, 0xDC);

        public static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
        public static readonly IPen Law = MakePen(Solid(150, 0xFF, 0xD3, 0x6B), 1.4, dashed: true);
        public static readonly IBrush Dot = Solid(255, 0xD9, 0xDC, 0xE3);
        public static readonly IBrush YouGlow = SpaceDrawing.RadialGradient((WithAlpha(KeplerDrawing.You, 0.6), 0), (WithAlpha(KeplerDrawing.You, 0), 1));
    }
}
