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
using Lemon.Explore.Book.Infrastructures.Timing;

namespace Lemon.Explore.Book.Chapters.IceAges;

/// <summary>
/// The animated diagram of the ice-age page. Orbit: Earth going round the Sun on an orbit whose stretch, tilt and wobble
/// change over tens of thousands of years, with a gauge for each (<c>IceAgeDiagram.Orbit.cs</c>). Sunshine: the northern
/// half of the globe seen from above the pole with its ice sheets, beside the midsummer sunshine at 65° N and the ice it
/// grows over 800,000 years (<c>IceAgeDiagram.Sunshine.cs</c>). Ice sheet: a slice through North America from the Gulf of
/// Mexico to the Arctic with the ice sheet, its snow line, the ground it presses down and the sea it lowers
/// (<c>IceAgeDiagram.Sheet.cs</c>).
/// </summary>
/// <remarks>
/// One time (<see cref="TimeKa"/>, thousand years from 1950) drives every view; playing runs it forward at
/// <see cref="Speed"/>. The orbit comes from <see cref="IceAgeModel"/>, the ice from <see cref="IceRecord"/>. The mouse and
/// the clickable things are in <c>IceAgeDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class IceAgeDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    /// <summary>Seconds Earth takes to go round once in the orbit view.</summary>
    private const double YearSeconds = 7;

    #region dependency properties

    public static readonly StyledProperty<IceAgeMode> ModeProperty =
        AvaloniaProperty.Register<IceAgeDiagram, IceAgeMode>(nameof(Mode), IceAgeMode.Orbit);

    /// <summary>The time shown, thousand years from 1950 (negative: ago), from −800 to +100. Played and dragged here.</summary>
    public static readonly StyledProperty<double> TimeKaProperty =
        AvaloniaProperty.Register<IceAgeDiagram, double>(nameof(TimeKa), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Thousand years per second while playing.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<IceAgeDiagram, double>(nameof(Speed), 2.0);

    /// <summary>Orbit: the cycle shown changing (the others held at today's values).</summary>
    public static readonly StyledProperty<IceAgeCycle> CycleProperty =
        AvaloniaProperty.Register<IceAgeDiagram, IceAgeCycle>(nameof(Cycle), IceAgeCycle.All, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Orbit: the stretch drawn 5× and the tilt's changes 3× so they can be seen.</summary>
    public static readonly StyledProperty<bool> ExaggerateProperty =
        AvaloniaProperty.Register<IceAgeDiagram, bool>(nameof(Exaggerate), true);

    /// <summary>Sunshine: the ends of the model's full glacials marked on the ice chart.</summary>
    public static readonly StyledProperty<bool> ShowTerminationsProperty =
        AvaloniaProperty.Register<IceAgeDiagram, bool>(nameof(ShowTerminations), true);

    /// <summary>Ice sheet: arrows for the snow, the melt and the sunlight.</summary>
    public static readonly StyledProperty<bool> ShowArrowsProperty =
        AvaloniaProperty.Register<IceAgeDiagram, bool>(nameof(ShowArrows), true);

    /// <summary>Ice sheet: the ground pressed down under the ice.</summary>
    public static readonly StyledProperty<bool> ShowGroundProperty =
        AvaloniaProperty.Register<IceAgeDiagram, bool>(nameof(ShowGround), true);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<IceAgeDiagram, bool>(nameof(ShowLabels), true);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<IceAgeDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<IceAgeDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<IceAgeDiagram>(ModeProperty, TimeKaProperty, SpeedProperty, CycleProperty, ExaggerateProperty, ShowTerminationsProperty, ShowArrowsProperty, ShowGroundProperty, ShowLabelsProperty, ToolbarHeightProperty);
        return true;
    }


    public IceAgeMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double TimeKa
    {
        get => GetValue(TimeKaProperty);
        set => SetValue(TimeKaProperty, value);
    }

    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public IceAgeCycle Cycle
    {
        get => GetValue(CycleProperty);
        set => SetValue(CycleProperty, value);
    }

    public bool Exaggerate
    {
        get => GetValue(ExaggerateProperty);
        set => SetValue(ExaggerateProperty, value);
    }

    public bool ShowTerminations
    {
        get => GetValue(ShowTerminationsProperty);
        set => SetValue(ShowTerminationsProperty, value);
    }

    public bool ShowArrows
    {
        get => GetValue(ShowArrowsProperty);
        set => SetValue(ShowArrowsProperty, value);
    }

    public bool ShowGround
    {
        get => GetValue(ShowGroundProperty);
        set => SetValue(ShowGroundProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public double ToolbarHeight
    {
        get => GetValue(ToolbarHeightProperty);
        set => SetValue(ToolbarHeightProperty, value);
    }

    #endregion

    private readonly BookStopwatch _clock = new();
    private readonly LabelCache _labels;
    private double _lastSeconds;

    /// <summary>Where Earth is in its year in the orbit view, 0 to 1 from perihelion; runs whether or not time plays.</summary>
    private double _year;

    /// <summary>Seconds since the page started, for snow, drips and shimmer that move even when time is paused.</summary>
    private double _drift;

    public IceAgeDiagram()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
    }

    #region lifecycle

    private bool _attached;

    private void RequestFrame()
    {
        if (_attached)
        {
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
        }
    }

    private void OnFrame(TimeSpan time)
    {
        if (!_attached)
        {
            return;
        }

        OnRendering();
        RequestFrame();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _clock.Start();
        _lastSeconds = _clock.Elapsed.TotalSeconds;
        RequestFrame();
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        _clock.Stop();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => _labels.Clear();

    private void OnModeChanged() => _drag = DragKind.None;

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);
        _drift += dt;
        _year = (_year + (dt / YearSeconds)) % 1;
        if (IsPlaying && _drag is DragKind.None or DragKind.Pending)
        {
            var next = TimeKa + (Speed * dt);
            if (next > IceRecord.End)
            {
                // At the end of the story, stop on it; playing again starts from the beginning.
                next = IceRecord.End;
                SetCurrentValue(IsPlayingProperty, false);
            }

            SetCurrentValue(TimeKaProperty, next);
        }

        InvalidateVisual();
    }

    /// <summary>Called when play is pressed at the end of the story: back to the start.</summary>
    internal static double PlayFrom(double timeKa) => timeKa >= IceRecord.End - 0.01 ? IceRecord.Start : timeKa;

    #endregion

    /// <summary>The room for the picture: below the title, above the toolbar (and a walkthrough's subtitles).</summary>
    private Rect Stage()
    {
        var top = TitleSpace;
        var bottom = Math.Max(ToolbarSpace, ToolbarHeight + 18 + 16) + _reserve.Current;
        return new Rect(0, top, Math.Max(100, ActualWidth), Math.Max(200, ActualHeight - top - bottom));
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        _targets.Clear();
        var now = _clock.Elapsed.TotalSeconds;
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        switch (Mode)
        {
            case IceAgeMode.Sunshine:
                DrawSunshineMode(dc, now);
                break;

            case IceAgeMode.Sheet:
                DrawSheetMode(dc, now);
                break;

            default:
                DrawOrbitMode(dc, now);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, now, width);
    }

    #region the time strip

    private readonly Dictionary<(string Curve, Rect Strip), StreamGeometry> _stripCurves = [];

    /// <summary>The strip along the bottom of the orbit and ice-sheet views: the whole story, a curve, and where we are.</summary>
    private Rect StripRect(Rect stage) => new(stage.Left + 40, stage.Bottom - 54, Math.Max(100, stage.Width - 80), 40);

    /// <summary>The x of a time on a strip or chart from <see cref="IceRecord.Start"/> to <see cref="IceRecord.End"/>.</summary>
    private static double TimeX(Rect plot, double kyr) =>
        plot.Left + ((kyr - IceRecord.Start) / (IceRecord.End - IceRecord.Start) * plot.Width);

    private static double XTime(Rect plot, double x) =>
        Math.Clamp(IceRecord.Start + ((x - plot.Left) / plot.Width * (IceRecord.End - IceRecord.Start)), IceRecord.Start, IceRecord.End);

    /// <summary>
    /// The strip: <paramref name="value"/> over the whole story scaled between <paramref name="low"/> and <paramref name="high"/>,
    /// ticks every 100,000 years, today, the future shaded, and the time now with its label.
    /// </summary>
    private void DrawStrip(PaintContext dc, Rect strip, string curve, Func<double, double> value, double low, double high, Color color, string captionKey)
    {
        dc.DrawRoundedRectangle(Ink.PlotBack, Ink.Frame, Inflated(strip, 6, 4), 8, 8);
        var future = new Rect(TimeX(strip, 0), strip.Top, TimeX(strip, IceRecord.End) - TimeX(strip, 0), strip.Height);
        dc.DrawRectangle(Ink.Future, null, future);

        for (var k = -800; k <= 100; k += 100)
        {
            var x = TimeX(strip, k);
            dc.DrawLine(Ink.Grid, new Point(x, strip.Top), new Point(x, strip.Bottom));
            if (k % 200 == 0) DrawText(dc, TickText(k), new Point(x, strip.Bottom + 9), Ink.Dim, 9.5, TextAnchor.Center);
        }

        // The curve is the same until the strip moves or another curve is shown: worked out once and kept.
        if (!_stripCurves.TryGetValue((curve, strip), out var geometry))
        {
            var points = new List<Point>();
            for (var k = IceRecord.Start; k <= IceRecord.End + 1e-9; k += 1)
            {
                var f = Math.Clamp((value(k) - low) / (high - low), 0, 1);
                points.Add(new Point(TimeX(strip, k), strip.Bottom - (f * strip.Height)));
            }

            if (_stripCurves.Count > 16) _stripCurves.Clear();
            geometry = Line(points);
            _stripCurves[(curve, strip)] = geometry;
        }

        dc.DrawGeometry(null, FadedPen(color, 0.95, 1.4, flat: true), geometry);
        DrawText(dc, Loc(captionKey), new Point(strip.Left + 4, strip.Top + 7), color, 10);

        var cursor = TimeX(strip, Math.Clamp(TimeKa, IceRecord.Start, IceRecord.End));
        dc.DrawLine(FadedPen(Ink.Text, 0.9, 1.5), new Point(cursor, strip.Top - 4), new Point(cursor, strip.Bottom + 4));
        var label = TimeText(TimeKa);
        var half = (_labels.Text(label, 11, Ink.Text).Width / 2) + 6;
        DrawPill(dc, label, new Point(Math.Clamp(cursor, strip.Left + half, strip.Right - half), strip.Top - 14), Ink.Text, 11);
        AddBoxTarget("strip", strip);
    }

    #endregion

    #region drawing helpers

    /// <summary>"21,000 years ago", "today" or "in 50,000 years".</summary>
    internal static string TimeText(double kyr)
    {
        var years = Math.Round(kyr * 1000 / 100) * 100;
        if (Math.Abs(years) < 50) return Loc("Ice_Time_Today");
        return LocFormat(years < 0 ? "Ice_Time_Ago" : "Ice_Time_Ahead", YearsText(Math.Abs(years)));
    }

    /// <summary>A number of years: "21,000" in English, "2.1万" in Chinese.</summary>
    internal static string YearsText(double years)
    {
        var unit = double.Parse(Loc("Ice_Years_Unit"), CultureInfo.InvariantCulture);
        return years >= unit
            ? Number(years / unit, "0.#") + Loc("Ice_Years_UnitName")
            : Number(years, Loc("Ice_Years_Digits"));
    }

    /// <summary>A time axis tick: "−400k" / "−40万", "now", "+100k".</summary>
    internal static string TickText(double kyr)
    {
        if (Math.Abs(kyr) < 0.5) return Loc("Ice_Chart_Now");
        var unit = double.Parse(Loc("Ice_Chart_TickUnit"), CultureInfo.InvariantCulture);
        return LocFormat("Ice_Chart_Tick", (kyr < 0 ? "−" : "+") + Number(Math.Abs(kyr) / unit, "0"));
    }

    private static Rect Inflated(Rect rect, double x, double y)
    {
        rect = rect.Inflate(new Thickness(x, y));
        return rect;
    }

    /// <summary>Already localized text on a dark pill, centred on <paramref name="center"/> and kept inside the control.</summary>
    private Rect DrawPill(PaintContext dc, string text, Point center, Color color, double size = 11.5)
    {
        var formatted = _labels.Text(text, size, color);
        var x = Math.Clamp(center.X - (formatted.Width / 2) - 6, 2, Math.Max(2, ActualWidth - formatted.Width - 14));
        var y = Math.Clamp(center.Y - (formatted.Height / 2) - 2, 2, Math.Max(2, ActualHeight - formatted.Height - 6));
        _labels.DrawTextPill(dc, text, new Point(x, y), size, color);
        return new Rect(x, y, formatted.Width + 12, formatted.Height + 4);
    }

    /// <summary>A pill that is also a target for <paramref name="id"/>.</summary>
    private Rect DrawTargetPill(PaintContext dc, string text, Point center, Color color, string id, double size = 11.5)
    {
        var box = DrawPill(dc, text, center, color, size);
        AddBoxTarget(id, box);
        return box;
    }

    /// <summary>Text with its left edge (or its centre, or its right edge) at a point, its middle at the point's height.</summary>
    private Rect DrawText(PaintContext dc, string text, Point at, Color color, double size = 11, TextAnchor anchor = TextAnchor.Left, bool bold = false)
    {
        var formatted = _labels.Text(text, size, color, bold);
        var x = anchor switch
        {
            TextAnchor.Center => at.X - (formatted.Width / 2),
            TextAnchor.Right => at.X - formatted.Width,
            _ => at.X,
        };
        var topLeft = new Point(x, at.Y - (formatted.Height / 2));
        dc.DrawText(formatted, topLeft);
        return new Rect(topLeft, new Size(formatted.Width, formatted.Height));
    }

    private enum TextAnchor
    {
        Left,
        Center,
        Right,
    }

    /// <summary>A framed panel with a caption at its top left; returns the room inside it below the caption.</summary>
    private Rect DrawPanel(PaintContext dc, Rect box, string caption, Color color)
    {
        dc.DrawRoundedRectangle(Ink.Panel, Ink.Frame, box, 10, 10);
        DrawText(dc, caption, new Point(box.Left + 12, box.Top + 14), color, 11.5, bold: true);
        return new Rect(box.Left + 10, box.Top + 26, Math.Max(10, box.Width - 20), Math.Max(10, box.Height - 34));
    }

    private readonly Dictionary<(Color Color, int Alpha, double Thickness, bool Dashed, bool Flat), IPen> _fadedPens = [];
    private readonly Dictionary<(Color Color, int Alpha), IBrush> _fadedBrushes = [];

    /// <summary>A frozen pen of <paramref name="color"/> at <paramref name="alpha"/> (rounded to twentieths), made once and kept.</summary>
    private IPen FadedPen(Color color, double alpha, double thickness, bool dashed = false, bool flat = false)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20), Math.Round(thickness, 1), dashed, flat);
        if (_fadedPens.TryGetValue(key, out var pen)) return pen;
        var brush = Solid((byte)(key.Item2 * 12.75), color.R, color.G, color.B);
        var cap = flat ? PenLineCap.Flat : PenLineCap.Round;
        pen = new ImmutablePen(((IBrush)brush).ToImmutable(), key.Item3, dashed ? new ImmutableDashStyle([4, 3], 0) : null, cap, PenLineJoin.Round);
        _fadedPens[key] = pen;
        return pen;
    }

    /// <summary>A frozen brush of <paramref name="color"/> at <paramref name="alpha"/> (rounded to twentieths), made once and kept.</summary>
    private IBrush FadedBrush(Color color, double alpha)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 20));
        if (_fadedBrushes.TryGetValue(key, out var brush)) return brush;
        brush = Solid((byte)(key.Item2 * 12.75), color.R, color.G, color.B);
        _fadedBrushes[key] = brush;
        return brush;
    }

    /// <summary>A line through the points, as one figure.</summary>
    private static StreamGeometry Line(IEnumerable<Point> points, bool closed = false, bool filled = false)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var first = true;
            foreach (var point in points)
            {
                if (first) context.BeginFigure(point, filled);
                else context.LineTo(point);
                first = false;
            }

            if (!first) context.EndFigure(closed);
        }
        return geometry;
    }

    /// <summary>An arrow from <paramref name="from"/> to <paramref name="to"/> with its head at the end.</summary>
    private void DrawArrow(PaintContext dc, Point from, Point to, Color color, double thickness = 2.5, double alpha = 1, bool dashed = false)
    {
        Vector v = to - from;
        if (v.Length < 2) return;
        var direction = v / v.Length;
        var head = Math.Min(Math.Max(7, thickness * 3.4), v.Length * 0.6);
        dc.DrawLine(FadedPen(color, alpha, thickness, dashed), from, to - (direction * head * 0.7));
        DrawArrowHead(dc, FadedBrush(color, alpha), to, direction, head);
    }

    /// <summary>The Sun: a soft glow and a bright disc.</summary>
    private void DrawSun(PaintContext dc, Point at, double radius)
    {
        dc.DrawEllipse(Ink.SunGlow, null, at, radius * 3.2, radius * 3.2);
        dc.DrawEllipse(Ink.SunDisc, null, at, radius, radius);
    }

    internal static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    #endregion

    private static class Ink
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
        public static readonly Color Green = Color.FromRgb(0x7C, 0xFF, 0x9E);
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x9A, 0x4A);
        public static readonly Color Red = Color.FromRgb(0xFF, 0x5A, 0x5F);
        public static readonly Color Blue = Color.FromRgb(0x5A, 0xA8, 0xFF);
        public static readonly Color Violet = Color.FromRgb(0xB0, 0x8C, 0xFF);
        public static readonly Color Ice = Color.FromRgb(0xDD, 0xEE, 0xFA);
        public static readonly Color IceEdge = Color.FromRgb(0x9C, 0xC8, 0xE8);
        public static readonly Color Sea = Color.FromRgb(0x1C, 0x4E, 0x7A);
        public static readonly Color Earthy = Color.FromRgb(0x8A, 0x6E, 0x4E);
        public static readonly Color Steel = Color.FromRgb(0xC9, 0xD2, 0xDE);

        public static readonly IBrush Backdrop = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromRgb(0x0D, 0x11, 0x22)), new ImmutableGradientStop(1, Color.FromRgb(0x03, 0x04, 0x09))],
            center: new RelativePoint(0.45, 0.45, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.45, 0.45, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.9, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.9, RelativeUnit.Relative));

        public static readonly IBrush Panel = Solid(185, 8, 11, 20);
        public static readonly IBrush PlotBack = Solid(150, 6, 8, 16);
        public static readonly IBrush Future = Solid(28, 0xB0, 0x8C, 0xFF);
        public static readonly IPen Frame = MakePen(Solid(46, 255, 255, 255), 1);
        public static readonly IPen Faint = MakePen(Solid(34, 255, 255, 255), 1);
        public static readonly IPen Grid = MakePen(Solid(22, 255, 255, 255), 1);

        public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient((Color.FromArgb(120, 0xFF, 0xC8, 0x5A), 0), (Color.FromArgb(0, 0xFF, 0x9A, 0x2A), 1));
        public static readonly IBrush SunDisc = SpaceDrawing.RadialGradient((Color.FromRgb(0xFF, 0xF6, 0xD0), 0), (Color.FromRgb(0xFF, 0xB8, 0x3A), 1));
        public static readonly IBrush EarthBall = new ImmutableRadialGradientBrush(
        [
            new ImmutableGradientStop(0, Color.FromRgb(0x6C, 0xB8, 0xFF)),
            new ImmutableGradientStop(1, Color.FromRgb(0x1E, 0x4A, 0x6A)),
        ],
            gradientOrigin: new RelativePoint(0.35, 0.35, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.5, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.5, RelativeUnit.Relative));

        public static readonly IBrush Sky = SpaceDrawing.LinearGradient(Color.FromRgb(0x0E, 0x1E, 0x3A), Color.FromRgb(0x2A, 0x4A, 0x6E), 90);
        public static readonly IBrush Rock = SpaceDrawing.LinearGradient(Color.FromRgb(0x5A, 0x48, 0x36), Color.FromRgb(0x2A, 0x22, 0x1C), 90);
        public static readonly IBrush Soil = Solid(255, 0x4E, 0x6A, 0x3E);
        public static readonly IBrush Water = SpaceDrawing.LinearGradient(Color.FromArgb(235, 0x2A, 0x6A, 0x9E), Color.FromArgb(235, 0x0C, 0x2A, 0x48), 90);
        public static readonly IBrush IceFill = SpaceDrawing.LinearGradient(Color.FromRgb(0xF4, 0xFA, 0xFF), Color.FromRgb(0xA8, 0xCC, 0xE6), 90);
        public static readonly IBrush Pressed = Solid(62, 0xFF, 0x8A, 0x4A);
        public static readonly IBrush StateWarm = Solid(70, 0x7C, 0xFF, 0x9E);
        public static readonly IBrush StateGlacial = Solid(70, 0x5A, 0xA8, 0xFF);
        public static readonly IBrush StateFull = Solid(105, 0xDD, 0xEE, 0xFA);
    }
}
