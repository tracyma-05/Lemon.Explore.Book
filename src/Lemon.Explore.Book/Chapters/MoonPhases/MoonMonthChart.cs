using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.MoonPhases;

/// <summary>How much of the Moon is lit over one month, with the principal phases along the top and the current age.</summary>
public sealed class MoonMonthChart : Control
{
    static MoonMonthChart()
    {
        AffectsRender<MoonMonthChart>(AgeProperty, IsSouthernProperty);
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

    public static readonly StyledProperty<double> AgeProperty =
        AvaloniaProperty.Register<MoonMonthChart, double>(nameof(Age), 0.0);

    public static readonly StyledProperty<bool> IsSouthernProperty =
        AvaloniaProperty.Register<MoonMonthChart, bool>(nameof(IsSouthern), false);

    public double Age
    {
        get => GetValue(AgeProperty);
        set => SetValue(AgeProperty, value);
    }

    public bool IsSouthern
    {
        get => GetValue(IsSouthernProperty);
        set => SetValue(IsSouthernProperty, value);
    }

    private const double Left = 38;
    private const double Right = 12;
    private const double Top = 30;
    private const double Bottom = 22;

    private readonly LabelCache _labels;

    public MoonMonthChart()
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
        if (width < Left + Right + 20 || height < Top + Bottom + 20) return;

        var plot = new Rect(Left, Top, width - Left - Right, height - Top - Bottom);
        double X(double age) => plot.X + (plot.Width * age / MoonPhaseGeometry.SynodicMonth);
        double Y(double lit) => plot.Bottom - (plot.Height * lit);

        foreach (var lit in new[] { 0.0, 0.5, 1.0 })
        {
            var y = Y(lit);
            dc.DrawLine(lit == 0.5 ? Paint.Midline : Paint.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            var text = _labels.Text(lit.ToString("0%", CultureInfo.InvariantCulture), 9.5, Paint.Axis);
            dc.DrawText(text, new Point(plot.Left - text.Width - 6, y - (text.Height / 2)));
        }

        foreach (var day in new[] { 0, 7, 15, 22, 29 })
        {
            var x = X(day);
            dc.DrawLine(Paint.Tick, new Point(x, plot.Bottom), new Point(x, plot.Bottom + 4));
            var text = _labels.Text(LocalizationService.Instance.Format("MoonPhases_Chart_Day", day), 9.5, Paint.Axis);
            dc.DrawText(text, new Point(Math.Min(x - (text.Width / 2), width - text.Width - 2), plot.Bottom + 5));
        }

        // The principal phases: a line down the chart and the phase itself above it.
        for (var quarter = 0; quarter <= 4; quarter++)
        {
            var age = quarter * MoonPhaseGeometry.SynodicMonth / 4;
            var x = X(age);
            dc.DrawLine(Paint.PhaseLine, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var elongation = 90.0 * (quarter % 4);
            MoonDrawing.DrawPhaseIcon(dc, new Point(x, Top / 2), 8, elongation, MoonDrawing.LitSide(elongation, IsSouthern));
        }

        var curve = new StreamGeometry();
        using (var ctx = curve.Open())
        {
            const int steps = 120;
            for (var i = 0; i <= steps; i++)
            {
                var age = MoonPhaseGeometry.SynodicMonth * i / steps;
                var point = new Point(X(age), Y(MoonPhaseGeometry.Illumination(age)));
                if (i == 0) ctx.BeginFigure(point, false);
                else ctx.LineTo(point);
            }

            ctx.EndFigure(false);
        }
        dc.DrawGeometry(null, Paint.Curve, curve);

        var now = MoonPhaseGeometry.Wrap(Age);
        var nowX = X(now);
        dc.DrawLine(Paint.Today, new Point(nowX, plot.Top), new Point(nowX, plot.Bottom));
        dc.DrawEllipse(Paint.Dot, Paint.DotOutline, new Point(nowX, Y(MoonPhaseGeometry.Illumination(now))), 4, 4);
    }

    private static class Paint
    {
        public static readonly Color Axis = Color.FromArgb(0xC0, 0xC9, 0xD1, 0xDC);

        public static readonly IPen Tick = MakePen(Solid(90, 255, 255, 255), 1);
        public static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
        public static readonly IPen Midline = MakePen(Solid(60, 255, 255, 255), 1, dashed: true);
        public static readonly IPen PhaseLine = MakePen(Solid(40, 0xD9, 0xDC, 0xE3), 1);
        public static readonly IPen Today = MakePen(Solid(150, 255, 255, 255), 1);
        public static readonly IPen DotOutline = MakePen(Solid(255, 10, 14, 24), 1.2);
        public static readonly IBrush Dot = Solid(255, 0xD9, 0xDC, 0xE3);
        public static readonly IPen Curve = MakePen(Solid(255, 0xD9, 0xDC, 0xE3), 2);
    }
}
