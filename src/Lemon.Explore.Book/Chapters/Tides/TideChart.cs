using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Tides;

/// <summary>
/// The water at the observer from nine hours ago to a day and a bit ahead: night shaded, each high and low tide with
/// its time, the time between the next two high tides, and now.
/// </summary>
public sealed class TideChart : Control
{
    static TideChart()
    {
        AffectsRender<TideChart>(DayProperty, IncludeSunProperty, ShowMetresProperty);
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
        AvaloniaProperty.Register<TideChart, double>(nameof(Day), 0.0);

    /// <summary>Add the Sun's tide to the Moon's; off, the chart shows the Moon's tide alone.</summary>
    public static readonly StyledProperty<bool> IncludeSunProperty =
        AvaloniaProperty.Register<TideChart, bool>(nameof(IncludeSun), true);

    /// <summary>Metres above chart datum (with the Sun) instead of just high and low.</summary>
    public static readonly StyledProperty<bool> ShowMetresProperty =
        AvaloniaProperty.Register<TideChart, bool>(nameof(ShowMetres), true);

    public double Day
    {
        get => GetValue(DayProperty);
        set => SetValue(DayProperty, value);
    }

    public bool IncludeSun
    {
        get => GetValue(IncludeSunProperty);
        set => SetValue(IncludeSunProperty, value);
    }

    public bool ShowMetres
    {
        get => GetValue(ShowMetresProperty);
        set => SetValue(ShowMetresProperty, value);
    }

    private const double Left = 40;
    private const double Right = 12;
    private const double Top = 24;
    private const double Bottom = 22;
    private const double HoursBefore = 9;
    private const double HoursAfter = 27;

    private readonly LabelCache _labels;

    public TideChart()
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
        if (width < Left + Right + 40 || height < Top + Bottom + 30) return;

        var day = Day;
        var includeSun = IncludeSun;
        var metres = ShowMetres && IncludeSun;
        var from = day - (HoursBefore / 24);
        var to = day + (HoursAfter / 24);
        var plot = new Rect(Left, Top, width - Left - Right, height - Top - Bottom);
        var (min, max) = metres ? (-0.2, 3.2) : (-1.35, 1.35);
        double Value(double d) => metres ? TideGeometry.CoastHeight(d) : TideGeometry.Height(d, includeSun);
        double ValueOf(TideTurn t) => metres ? TideGeometry.ToCoastMetres(t.Height) : t.Height;
        double X(double d) => plot.X + (plot.Width * (d - from) / (to - from));
        double Y(double v) => plot.Bottom - (plot.Height * (v - min) / (max - min));

        // Night, from 18:00 to 06:00.
        for (var midnight = Math.Floor(from); midnight <= to; midnight++)
        {
            var left = Math.Max(X(midnight - 0.25), plot.Left);
            var right = Math.Min(X(midnight + 0.25), plot.Right);
            if (right > left) dc.DrawRectangle(Paint.Night, null, new Rect(left, plot.Top, right - left, plot.Height));
        }

        // The height axis.
        if (metres)
        {
            for (var m = 0; m <= 3; m++)
            {
                var y = Y(m);
                dc.DrawLine(Paint.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
                var text = _labels.Text(m.ToString("0 m", CultureInfo.InvariantCulture), 9.5, Paint.Axis);
                dc.DrawText(text, new Point(plot.Left - text.Width - 6, y - (text.Height / 2)));
            }
        }
        else
        {
            foreach (var (v, key) in new[] { (1.0, "Tides_Chart_High"), (0.0, "Tides_Chart_Mean"), (-1.0, "Tides_Chart_Low") })
            {
                var y = Y(v);
                dc.DrawLine(v == 0 ? Paint.Midline : Paint.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
                var text = _labels.Get(key, 9.5, Paint.Axis);
                dc.DrawText(text, new Point(plot.Left - text.Width - 6, y - (text.Height / 2)));
            }
        }

        // Every six hours along the bottom, the day at midnight.
        for (var tick = Math.Ceiling(from * 4) / 4; tick <= to; tick += 0.25)
        {
            var x = X(tick);
            var hour = (int)Math.Round(TideGeometry.HourOfDay(tick)) % 24;
            dc.DrawLine(Paint.Tick, new Point(x, plot.Bottom), new Point(x, plot.Bottom + 4));
            var label = hour == 0
                ? _labels.Text(LocalizationService.Instance.Format("Tides_Chart_Day", (int)Math.Round(tick) + 1), 9.5, TidesDrawing.Text)
                : _labels.Text($"{hour:00}:00", 9.5, Paint.Axis);
            var left = Math.Clamp(x - (label.Width / 2), 0, width - label.Width);
            dc.DrawText(label, new Point(left, plot.Bottom + 5));
        }

        var curve = new StreamGeometry();
        using (var ctx = curve.Open())
        {
            const int steps = 240;
            for (var i = 0; i <= steps; i++)
            {
                var d = from + ((to - from) * i / steps);
                var point = new Point(X(d), Y(Value(d)));
                if (i == 0) ctx.BeginFigure(point, false);
                else ctx.LineTo(point);
            }

            ctx.EndFigure(false);
        }
        dc.DrawGeometry(null, TidesDrawing.CurvePen, curve);

        // High and low tides with their times.
        var turns = TideGeometry.Turns(from, to, includeSun);
        foreach (var turn in turns)
        {
            var at = new Point(X(turn.Day), Y(ValueOf(turn)));
            dc.DrawEllipse(turn.IsHigh ? TidesDrawing.HighBrush : TidesDrawing.LowBrush, TidesDrawing.DotOutline, at, 3.5, 3.5);
            var text = _labels.Text(SeasonsDiagram.ClockText(TideGeometry.HourOfDay(turn.Day)), 9.5, turn.IsHigh ? TidesDrawing.High : TidesDrawing.Low);
            var left = Math.Clamp(at.X - (text.Width / 2), plot.Left, plot.Right - text.Width);
            dc.DrawText(text, new Point(left, turn.IsHigh ? at.Y - text.Height - 3 : at.Y + 4));
        }

        // The time between the next two high tides, along the top.
        var highs = turns.Where(t => t.IsHigh && t.Day > day).Take(2).ToList();
        if (highs.Count == 2)
        {
            var (h, m) = TidesDrawing.HoursMinutes((highs[1].Day - highs[0].Day) * 24);
            var a = X(highs[0].Day);
            var b = X(highs[1].Day);
            var y = Top / 2;
            dc.DrawLine(Paint.Gap, new Point(a, y), new Point(b, y));
            DrawArrowHead(dc, Paint.GapBrush, new Point(a, y), new Vector(-1, 0), 5);
            DrawArrowHead(dc, Paint.GapBrush, new Point(b, y), new Vector(1, 0), 5);
            dc.DrawLine(Paint.GapTick, new Point(a, y), new Point(a, Y(ValueOf(highs[0])) - 16));
            dc.DrawLine(Paint.GapTick, new Point(b, y), new Point(b, Y(ValueOf(highs[1])) - 16));
            var text = _labels.Text(LocalizationService.Instance.Format("Tides_Chart_Gap", h, m), 9.5, TidesDrawing.High);
            var rect = new Rect(((a + b) / 2) - (text.Width / 2) - 4, y - (text.Height / 2), text.Width + 8, text.Height);
            dc.DrawRectangle(Paint.GapBack, null, rect);
            dc.DrawText(text, new Point(rect.X + 4, rect.Y));
        }

        var nowX = X(day);
        dc.DrawLine(Paint.Now, new Point(nowX, plot.Top), new Point(nowX, plot.Bottom));
        dc.DrawEllipse(Paint.Dot, TidesDrawing.DotOutline, new Point(nowX, Y(Value(day))), 4.5, 4.5);
    }

    private static class Paint
    {
        public static readonly Color Axis = Color.FromArgb(0xC0, 0xC9, 0xD1, 0xDC);

        public static readonly IBrush Night = Solid(70, 0x00, 0x04, 0x14);
        public static readonly IPen Tick = MakePen(Solid(90, 255, 255, 255), 1);
        public static readonly IPen Grid = MakePen(Solid(26, 255, 255, 255), 1);
        public static readonly IPen Midline = MakePen(Solid(60, 255, 255, 255), 1, dashed: true);
        public static readonly IPen Now = MakePen(Solid(150, 255, 255, 255), 1);
        public static readonly IBrush Dot = Solid(255, 0xFF, 0xD3, 0x6B);
        public static readonly IPen Gap = MakePen(Solid(170, 0x7F, 0xE0, 0xFF), 1);
        public static readonly IPen GapTick = MakePen(Solid(70, 0x7F, 0xE0, 0xFF), 1, dashed: true);
        public static readonly IBrush GapBrush = Solid(200, 0x7F, 0xE0, 0xFF);
        public static readonly IBrush GapBack = Solid(255, 6, 8, 15);
    }
}
