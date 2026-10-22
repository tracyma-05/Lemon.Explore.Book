using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Chapters.MoonPhases;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Tides;

/// <summary>
/// The day's range between high and low water over a month: spring tides at new and full moon, neap tides at the
/// quarters, the Moon's tide alone as a dashed line, the principal phases along the top and the current day.
/// </summary>
public sealed class TideRangeChart : Control
{
    static TideRangeChart()
    {
        AffectsRender<TideRangeChart>(DayProperty);
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

    public static readonly StyledProperty<double> DayProperty =
        AvaloniaProperty.Register<TideRangeChart, double>(nameof(Day), 0.0);

    public double Day
    {
        get => GetValue(DayProperty);
        set => SetValue(DayProperty, value);
    }

    private const double Left = 40;
    private const double Right = 12;
    private const double Top = 30;
    private const double Bottom = 22;
    private const double MaxMetres = 3.4;

    private readonly LabelCache _labels;

    public TideRangeChart()
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

        const double month = MoonPhaseGeometry.SynodicMonth;
        var plot = new Rect(Left, Top, width - Left - Right, height - Top - Bottom);
        double X(double day) => plot.X + (plot.Width * day / month);
        double Y(double metres) => plot.Bottom - (plot.Height * metres / MaxMetres);

        for (var m = 0; m <= 3; m++)
        {
            var y = Y(m);
            dc.DrawLine(Paint.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
            var text = _labels.Text(m.ToString("0 m", CultureInfo.InvariantCulture), 9.5, Paint.Axis);
            dc.DrawText(text, new Point(plot.Left - text.Width - 6, y - (text.Height / 2)));
        }

        foreach (var day in new[] { 0, 7, 15, 22, 29 })
        {
            var x = X(day);
            dc.DrawLine(Paint.Tick, new Point(x, plot.Bottom), new Point(x, plot.Bottom + 4));
            var text = _labels.Text(LocalizationService.Instance.Format("Tides_Chart_Day", day + 1), 9.5, Paint.Axis);
            dc.DrawText(text, new Point(Math.Min(x - (text.Width / 2), width - text.Width - 2), plot.Bottom + 5));
        }

        // The Moon alone would give the same range every day.
        var moonAlone = Y(2 * TideGeometry.CoastMetres);
        dc.DrawLine(TidesDrawing.LunarPen, new Point(plot.Left, moonAlone), new Point(plot.Right, moonAlone));

        // The principal phases: a line down the chart, the phase above it, and spring or neap beside the curve.
        for (var quarter = 0; quarter <= 4; quarter++)
        {
            var day = quarter * month / 4;
            var x = X(day);
            dc.DrawLine(Paint.PhaseLine, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var elongation = 90.0 * (quarter % 4);
            MoonDrawing.DrawPhaseIcon(dc, new Point(x, Top / 2), 8, elongation, MoonDrawing.LitSide(elongation, false));
        }

        var curve = new StreamGeometry();
        using (var ctx = curve.Open())
        {
            const int steps = 160;
            for (var i = 0; i <= steps; i++)
            {
                var day = month * i / steps;
                var point = new Point(X(day), Y(TideGeometry.Range(day) * TideGeometry.CoastMetres));
                if (i == 0) ctx.BeginFigure(point, false);
                else ctx.LineTo(point);
            }

            ctx.EndFigure(false);
        }
        dc.DrawGeometry(null, TidesDrawing.CurvePen, curve);

        foreach (var (quarter, key, color) in new[] { (2, "Tides_Label_Spring", TidesDrawing.High), (1, "Tides_Label_Neap", TidesDrawing.Low), (3, "Tides_Label_Neap", TidesDrawing.Low) })
        {
            var day = quarter * month / 4;
            var text = _labels.Get(key, 10, color, true);
            var y = Y(TideGeometry.Range(day) * TideGeometry.CoastMetres);
            dc.DrawText(text, new Point(X(day) - (text.Width / 2), quarter == 2 ? y + 4 : y - text.Height - 3));
        }

        var now = MoonPhaseGeometry.Wrap(Day);
        var nowX = X(now);
        dc.DrawLine(Paint.Today, new Point(nowX, plot.Top), new Point(nowX, plot.Bottom));
        dc.DrawEllipse(Paint.Dot, TidesDrawing.DotOutline, new Point(nowX, Y(TideGeometry.Range(now) * TideGeometry.CoastMetres)), 4.5, 4.5);
    }

    private static class Paint
    {
        public static readonly Color Axis = Color.FromArgb(0xC0, 0xC9, 0xD1, 0xDC);

        public static readonly IPen Tick = MakePen(Solid(90, 255, 255, 255), 1);
        public static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
        public static readonly IPen PhaseLine = MakePen(Solid(40, 0xD9, 0xDC, 0xE3), 1);
        public static readonly IPen Today = MakePen(Solid(150, 255, 255, 255), 1);
        public static readonly IBrush Dot = Solid(255, 0xFF, 0xD3, 0x6B);
    }
}
