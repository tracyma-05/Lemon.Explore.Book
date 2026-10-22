using System.ComponentModel;
using System.Diagnostics;
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

namespace Lemon.Explore.Book.Chapters.SeaBreeze;

/// <summary>
/// The animated diagram of the sea-and-land-breeze page. Coast mode: a coast through a whole day, the sea breeze by day
/// and the land breeze by night (<c>BreezeDiagram.Coast.cs</c>). Cell mode: the thermal circulation built up step by step
/// with its pressure surfaces (<c>BreezeDiagram.Cell.cs</c>). Monsoon mode: the same over Asia and a year
/// (<c>BreezeDiagram.Monsoon.cs</c>).
/// </summary>
/// <remarks>
/// The diagram owns the clock and pushes back <see cref="Hour"/>, <see cref="StepTime"/> or <see cref="Month"/> as it
/// runs; clicking a city on the map picks <see cref="City"/>.
/// </remarks>
public sealed partial class BreezeDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == HourProperty)
        {
            OnHourChanged(change.GetNewValue<double>());
        }
        else if (change.Property == StepTimeProperty)
        {
            OnStepTimeChanged(change.GetNewValue<double>());
        }
        else if (change.Property == MonthProperty)
        {
            OnMonthChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    /// <summary>Coast hours per second of playback at 1×.</summary>
    private const double HoursPerSecond = 0.5;

    /// <summary>Monsoon months per second of playback at 1×.</summary>
    private const double MonthsPerSecond = 0.4;

    /// <summary>The cell holds its last stage this long before it starts again, seconds.</summary>
    private const double CellHold = 3;

    #region dependency properties

    public static readonly StyledProperty<BreezeMode> ModeProperty =
        AvaloniaProperty.Register<BreezeDiagram, BreezeMode>(nameof(Mode), BreezeMode.Coast);

    public static readonly StyledProperty<BreezeSky> SkyProperty =
        AvaloniaProperty.Register<BreezeDiagram, BreezeSky>(nameof(Sky), BreezeSky.Clear);

    /// <summary>The moving air: particles round the cell, or the winds over the map.</summary>
    public static readonly StyledProperty<bool> ShowFlowProperty =
        AvaloniaProperty.Register<BreezeDiagram, bool>(nameof(ShowFlow), true);

    /// <summary>Pressure surfaces over the coast, isobars and pressure centres on the map.</summary>
    public static readonly StyledProperty<bool> ShowPressureProperty =
        AvaloniaProperty.Register<BreezeDiagram, bool>(nameof(ShowPressure), false);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<BreezeDiagram, bool>(nameof(ShowLabels), true);

    /// <summary>Coast mode: the time of day, hours (pushed back as the day runs).</summary>
    public static readonly StyledProperty<double> HourProperty =
        AvaloniaProperty.Register<BreezeDiagram, double>(nameof(Hour), 14.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Cell mode: the land's air temperature, °C.</summary>
    public static readonly StyledProperty<double> LandTemperatureProperty =
        AvaloniaProperty.Register<BreezeDiagram, double>(nameof(LandTemperature), 32.0);

    /// <summary>Cell mode: the sea's air temperature, °C.</summary>
    public static readonly StyledProperty<double> SeaTemperatureProperty =
        AvaloniaProperty.Register<BreezeDiagram, double>(nameof(SeaTemperature), 24.0);

    /// <summary>Cell mode: seconds into the story of the cell (pushed back as it runs).</summary>
    public static readonly StyledProperty<double> StepTimeProperty =
        AvaloniaProperty.Register<BreezeDiagram, double>(nameof(StepTime), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Monsoon mode: the time of year, months since 1 January (pushed back as the year runs).</summary>
    public static readonly StyledProperty<double> MonthProperty =
        AvaloniaProperty.Register<BreezeDiagram, double>(nameof(Month), 6.5, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Monsoon mode: the city picked; clicking one on the map sets it.</summary>
    public static readonly StyledProperty<MonsoonCity> CityProperty =
        AvaloniaProperty.Register<BreezeDiagram, MonsoonCity>(nameof(City), MonsoonCity.Mumbai, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Playback speed: 1 is the normal pace.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<BreezeDiagram, double>(nameof(Speed), 1.0);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<BreezeDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<BreezeDiagram, double>(nameof(ToolbarHeight), 0.0);

    /// <summary>Height of the title block over the top of the diagram, so a title wrapped onto more lines covers nothing.</summary>
    public static readonly StyledProperty<double> TitleHeightProperty =
        AvaloniaProperty.Register<BreezeDiagram, double>(nameof(TitleHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<BreezeDiagram>(ModeProperty, SkyProperty, ShowFlowProperty, ShowPressureProperty, ShowLabelsProperty, HourProperty, LandTemperatureProperty, SeaTemperatureProperty, StepTimeProperty, MonthProperty, CityProperty, SpeedProperty, ToolbarHeightProperty, TitleHeightProperty);
        return true;
    }


    public BreezeMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public BreezeSky Sky
    {
        get => GetValue(SkyProperty);
        set => SetValue(SkyProperty, value);
    }

    public bool ShowFlow
    {
        get => GetValue(ShowFlowProperty);
        set => SetValue(ShowFlowProperty, value);
    }

    public bool ShowPressure
    {
        get => GetValue(ShowPressureProperty);
        set => SetValue(ShowPressureProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public double Hour
    {
        get => GetValue(HourProperty);
        set => SetValue(HourProperty, value);
    }

    public double LandTemperature
    {
        get => GetValue(LandTemperatureProperty);
        set => SetValue(LandTemperatureProperty, value);
    }

    public double SeaTemperature
    {
        get => GetValue(SeaTemperatureProperty);
        set => SetValue(SeaTemperatureProperty, value);
    }

    public double StepTime
    {
        get => GetValue(StepTimeProperty);
        set => SetValue(StepTimeProperty, value);
    }

    public double Month
    {
        get => GetValue(MonthProperty);
        set => SetValue(MonthProperty, value);
    }

    public MonsoonCity City
    {
        get => GetValue(CityProperty);
        set => SetValue(CityProperty, value);
    }

    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
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

    public double TitleHeight
    {
        get => GetValue(TitleHeightProperty);
        set => SetValue(TitleHeightProperty, value);
    }

    #endregion

    private readonly Stopwatch _clock = new();
    private readonly StarBackdrop _stars = new();
    private readonly LabelCache _labels;

    private double _lastSeconds;
    private bool _pushing;

    /// <summary>Animation seconds (scaled by the speed, stopped while paused): waves, smoke, particles.</summary>
    private double _t;

    /// <summary>The clocks of the three modes, kept here while they run and pushed back to the page.</summary>
    private double _hour = 14;

    private double _stepTime;
    private double _month = 6.5;

    public BreezeDiagram()
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

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _stars.OnSizeChanged(_clock.Elapsed.TotalSeconds);
    }

    private void OnModeChanged()
    {
        _cellParticles.Clear();
        _coastParticles.Clear();
        _windParticles.Clear();
    }

    private void OnHourChanged(double value)
    {
        if (!_pushing) _hour = BreezeModel.Wrap(value);
    }

    private void OnStepTimeChanged(double value)
    {
        if (!_pushing) _stepTime = Math.Clamp(value, 0, BreezeModel.CellDuration);
    }

    private void OnMonthChanged(double value)
    {
        if (_pushing) return;
        _month = ((value % 12) + 12) % 12;
        _fieldMonth = double.NaN;
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;

        _reserve.Step(IsLessonActive, dt);
        // A walkthrough holds the clock at the moment it talks about, but the air (and the sea, the smoke) keeps moving.
        var step = IsPlaying ? dt * Math.Clamp(Speed, 0.1, 8) : 0;
        var airStep = IsPlaying || IsLessonActive ? dt * Math.Clamp(Speed, 0.1, 8) : 0;
        _t += airStep;
        if (step > 0)
        {
            switch (Mode)
            {
                case BreezeMode.Coast:
                    _hour = BreezeModel.Wrap(_hour + (step * HoursPerSecond));
                    Push(HourProperty, Math.Round(_hour, 4));
                    break;

                case BreezeMode.Cell:
                    _stepTime += step;
                    if (_stepTime > BreezeModel.CellDuration + CellHold) _stepTime = 0;
                    Push(StepTimeProperty, Math.Round(Math.Min(_stepTime, BreezeModel.CellDuration), 3));
                    break;

                case BreezeMode.Monsoon:
                    _month = (_month + (step * MonthsPerSecond)) % 12;
                    Push(MonthProperty, Math.Round(_month, 4));
                    break;
            }
        }

        // Paused, nothing moves (but see airStep): particles use the scaled step.
        switch (Mode)
        {
            case BreezeMode.Coast:
                AdvanceCoastParticles(airStep);
                break;

            case BreezeMode.Cell:
                AdvanceCellParticles(airStep);
                break;

            case BreezeMode.Monsoon:
                AdvanceWindParticles(airStep);
                break;
        }

        InvalidateVisual();
    }

    private void Push(AvaloniaProperty property, object value)
    {
        if (Equals(GetValue(property), value)) return;
        _pushing = true;
        try
        {
            SetCurrentValue(property, value);
        }
        finally
        {
            _pushing = false;
        }
    }

    #endregion

    /// <summary>
    /// The title sits over the top of the picture; in a narrow window, where the title wraps across the whole width, the
    /// picture starts lower, and never above the title's last line (its top margin is 22).
    /// </summary>
    private double TitleSpace => Math.Max(Math.Clamp(118 + ((1050 - ActualWidth) * 0.3), 118, 190), 22 + TitleHeight + 12);

    /// <summary>The room for the picture: below the title, above the toolbar (and a walkthrough's subtitles).</summary>
    private Rect Stage(double top)
    {
        var bottom = Math.Max(ToolbarSpace, ToolbarHeight + 18 + 16) + _reserve.Current;
        return new Rect(0, top, Math.Max(100, ActualWidth), Math.Max(160, ActualHeight - top - bottom));
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        _targets.Clear();
        switch (Mode)
        {
            case BreezeMode.Cell:
                DrawCellMode(dc, width, height);
                break;

            case BreezeMode.Monsoon:
                DrawMonsoonMode(dc, width, height);
                break;

            default:
                DrawCoast(dc, width, height);
                break;
        }

        DrawObjectOverlays(dc, width);
    }

    #region drawing helpers

    /// <summary>Already localized text on a dark pill, centred on <paramref name="center"/> and kept inside the control.</summary>
    private Rect DrawPill(PaintContext dc, string text, Point center, Color color, double size = 11.5)
    {
        var formatted = _labels.Text(text, size, color);
        var x = Math.Clamp(center.X - (formatted.Width / 2) - 6, 2, Math.Max(2, ActualWidth - formatted.Width - 14));
        var y = Math.Clamp(center.Y - (formatted.Height / 2) - 2, 2, Math.Max(2, ActualHeight - formatted.Height - 6));
        _labels.DrawTextPill(dc, text, new Point(x, y), size, color);
        return new Rect(x, y, formatted.Width + 12, formatted.Height + 4);
    }

    /// <summary>Text with its left edge (or its centre) at a point, its middle at the point's height; where it went.</summary>
    private Rect DrawText(PaintContext dc, string text, Point at, Color color, double size = 11, bool center = false, bool bold = false)
    {
        var formatted = _labels.Text(text, size, color, bold);
        var topLeft = new Point(center ? at.X - (formatted.Width / 2) : at.X, at.Y - (formatted.Height / 2));
        dc.DrawText(formatted, topLeft);
        return new Rect(topLeft, new Size(formatted.Width, formatted.Height));
    }

    /// <summary>Text whose right edge is at <paramref name="at"/>, its middle at the point's height; where it went.</summary>
    private Rect DrawTextRight(PaintContext dc, string text, Point at, Color color, double size = 11, bool bold = false)
    {
        var formatted = _labels.Text(text, size, color, bold);
        var topLeft = new Point(at.X - formatted.Width, at.Y - (formatted.Height / 2));
        dc.DrawText(formatted, topLeft);
        return new Rect(topLeft, new Size(formatted.Width, formatted.Height));
    }

    /// <summary>
    /// A big "H" or "L" (localized) with a soft disc behind it; a target (<paramref name="id"/>, else "high" or "low")
    /// once it is plain to see.
    /// </summary>
    private void DrawPressureMark(PaintContext dc, Point at, bool high, double size = 22, double opacity = 1, string? id = null)
    {
        if (opacity <= 0.02) return;
        if (opacity > 0.5) _targets.Add(id ?? (high ? "high" : "low"), at, (size * 0.8) + 2);
        var color = high ? Ink.High : Ink.Low;
        dc.PushOpacity(opacity);
        dc.DrawEllipse(FadedBrush(Color.FromRgb(8, 12, 22), 0.6), FadedPen(color, 0.7, 1.4), at, size * 0.8, size * 0.8);
        DrawText(dc, Loc(high ? "Breeze_High" : "Breeze_Low"), at, color, size, center: true, bold: true);
        dc.Pop();
    }

    /// <summary>An arrow from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static void DrawArrow(PaintContext dc, Point from, Point to, IBrush brush, double thickness = 2, double head = 9)
    {
        Vector v = to - from;
        if (v.Length < 3) return;
        var direction = v / v.Length;
        dc.DrawLine(MakePen(brush, thickness), from, to - (direction * head * 0.6));
        DrawArrowHead(dc, brush, to, direction, head);
    }

    /// <summary>A thick arrow along a quadratic curve, its head at <paramref name="to"/>.</summary>
    private static void DrawCurvedArrow(PaintContext dc, Point from, Point control, Point to, Color color, double thickness, double opacity)
    {
        if (opacity <= 0.02) return;
        Vector tangent = to - control;
        if (tangent.Length < 1e-6) return;
        tangent = tangent.Normalize();
        var head = thickness * 3.2;
        var end = to - (tangent * head * 0.7);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(from, false);
            context.QuadraticBezierTo(control, end);

            context.EndFigure(false);
        }
        var brush = new SolidColorBrush(WithAlpha(color, Math.Clamp(opacity, 0, 1) * 0.9));
        var pen = new ImmutablePen(((IBrush)brush).ToImmutable(), thickness, null, PenLineCap.Round, PenLineJoin.Miter);
        dc.DrawGeometry(null, pen, geometry);
        DrawArrowHead(dc, brush, to, tangent, head);
    }

    /// <summary>A small thermometer whose red column stands at <paramref name="celsius"/> on a 10…40 °C scale.</summary>
    private static void DrawThermometer(PaintContext dc, Point bulb, double height, double celsius)
    {
        var tube = new Rect(bulb.X - 3, bulb.Y - height, 6, height);
        dc.DrawRoundedRectangle(Ink.Glass, Ink.GlassEdge, tube, 3, 3);
        var level = Math.Clamp((celsius - 10) / 30, 0.05, 1);
        dc.DrawRectangle(Ink.Mercury, null, new Rect(bulb.X - 1.6, bulb.Y - (height * level), 3.2, height * level));
        dc.DrawEllipse(Ink.Mercury, Ink.GlassEdge, bulb, 5, 5);
    }

    private readonly Dictionary<(Color Color, int Alpha, double Thickness, bool Flat), IPen> _fadedPens = [];
    private readonly Dictionary<(Color Color, int Alpha), IBrush> _fadedBrushes = [];

    /// <summary>
    /// A frozen pen of <paramref name="color"/> at <paramref name="alpha"/> (rounded to tenths), made once and kept. Round
    /// caps unless <paramref name="flat"/>: for many short joined segments round caps are costly to draw.
    /// </summary>
    private IPen FadedPen(Color color, double alpha, double thickness, bool flat = false)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 10), thickness, flat);
        if (_fadedPens.TryGetValue(key, out var pen)) return pen;
        var brush = Solid((byte)(key.Item2 * 25.5), color.R, color.G, color.B);
        pen = flat ? new ImmutablePen(((IBrush)brush).ToImmutable(), thickness) : MakePen(brush, thickness);
        _fadedPens[key] = pen;
        return pen;
    }

    /// <summary>A frozen brush of <paramref name="color"/> at <paramref name="alpha"/> (rounded to tenths), made once and kept.</summary>
    private IBrush FadedBrush(Color color, double alpha)
    {
        var key = (color, (int)Math.Round(Math.Clamp(alpha, 0, 1) * 10));
        if (_fadedBrushes.TryGetValue(key, out var brush)) return brush;
        brush = Solid((byte)(key.Item2 * 25.5), color.R, color.G, color.B);
        _fadedBrushes[key] = brush;
        return brush;
    }

    private readonly LineBatch _batch = new();

    /// <summary>
    /// Short lines gathered by pen and drawn as one geometry per pen: hundreds of separate translucent lines keep the render
    /// thread busy, a couple of dozen geometries do not. Particles follow the flow, so their streaks hardly ever cross.
    /// </summary>
    private sealed class LineBatch
    {
        /// <summary>Per pen: all the points, and where each line starts in them.</summary>
        private readonly Dictionary<IPen, (List<Point> Points, List<int> Starts)> _lines = [];

        public void Add(IPen pen, Point from, Point to)
        {
            var (points, starts) = Lists(pen);
            starts.Add(points.Count);
            points.Add(from);
            points.Add(to);
        }

        /// <summary>A line through several points, drawn as one figure.</summary>
        public void Add(IPen pen, ReadOnlySpan<Point> line)
        {
            if (line.Length < 2) return;
            var (points, starts) = Lists(pen);
            starts.Add(points.Count);
            foreach (var point in line) points.Add(point);
        }

        /// <summary>A round dot: a tiny line with the pen's round caps.</summary>
        public void Dot(IPen pen, Point at) => Add(pen, at, at + new Vector(0.01, 0));

        private (List<Point> Points, List<int> Starts) Lists(IPen pen)
        {
            if (!_lines.TryGetValue(pen, out var lists)) _lines[pen] = lists = ([], []);
            return lists;
        }

        public void Draw(PaintContext dc)
        {
            foreach (var (pen, (points, starts)) in _lines)
            {
                if (starts.Count == 0) continue;
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    for (var i = 0; i < starts.Count; i++)
                    {
                        var end = i + 1 < starts.Count ? starts[i + 1] : points.Count;
                        context.BeginFigure(points[starts[i]], false);
                        for (var k = starts[i] + 1; k < end; k++) context.LineTo(points[k]);
                        context.EndFigure(false);
                    }
                }
                dc.DrawGeometry(null, pen, geometry);
                points.Clear();
                starts.Clear();
            }
        }
    }

    /// <summary>The fractional part, for looping particles.</summary>
    private static double Frac(double value) => value - Math.Floor(value);

    /// <summary>0 below <paramref name="a"/>, 1 above <paramref name="b"/>, smooth between.</summary>
    private static double Smooth(double a, double b, double value) => BreezeModel.Smooth(a, b, value);

    private static double Mix(double a, double b, double t) => a + ((b - a) * t);

    internal static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>A time of day as hh:mm.</summary>
    internal static string Clock(double hour)
    {
        var minutes = (int)Math.Round(BreezeModel.Wrap(hour) * 60) % (24 * 60);
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    #endregion

    private static class Ink
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
        public static readonly Color Green = Color.FromRgb(0x8B, 0xE0, 0x8F);
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x9F, 0x5A);
        public static readonly Color Red = Color.FromRgb(0xFF, 0x7A, 0x6B);
        public static readonly Color Blue = Color.FromRgb(0x5A, 0xA8, 0xFF);
        public static readonly Color Violet = Color.FromRgb(0xB3, 0x9D, 0xFF);

        /// <summary>High pressure warm red, low pressure blue, as on most weather maps in China.</summary>
        public static readonly Color High = Color.FromRgb(0xFF, 0x8A, 0x70);

        public static readonly Color Low = Color.FromRgb(0x6F, 0xB8, 0xFF);

        public static readonly IBrush Sea = SpaceDrawing.LinearGradient(Color.FromRgb(0x1D, 0x5E, 0x96), Color.FromRgb(0x06, 0x1A, 0x33), 90);
        public static readonly IBrush SeaNight = SpaceDrawing.LinearGradient(Color.FromRgb(0x0E, 0x2C, 0x4C), Color.FromRgb(0x03, 0x0C, 0x1A), 90);
        public static readonly IBrush Soil = SpaceDrawing.LinearGradient(Color.FromRgb(0x5A, 0x46, 0x30), Color.FromRgb(0x24, 0x1C, 0x16), 90);
        public static readonly IBrush Sand = Solid(255, 0xD8, 0xC0, 0x8A);
        public static readonly IPen Grass = MakePen(Solid(255, 0x4E, 0x8A, 0x3E), 3);
        public static readonly IPen SandEdge = MakePen(Solid(255, 0xE6, 0xD2, 0x9E), 3);
        public static readonly IPen Wave = MakePen(Solid(170, 0xB8, 0xE2, 0xFF), 1.4);
        public static readonly IPen Foam = MakePen(Solid(220, 0xF2, 0xF8, 0xFF), 1.6);

        public static readonly IBrush Glass = Solid(150, 0xEE, 0xF1, 0xF6);
        public static readonly IPen GlassEdge = MakePen(Solid(200, 0x30, 0x38, 0x48), 1);
        public static readonly IBrush Mercury = Solid(255, 0xFF, 0x5A, 0x4A);

        public static readonly IBrush House = Solid(255, 0xD9, 0xD2, 0xC4);
        public static readonly IBrush HouseNight = Solid(255, 0x4A, 0x4E, 0x5A);
        public static readonly IBrush Roof = Solid(255, 0xA8, 0x4A, 0x3A);
        public static readonly IBrush Window = Solid(255, 0xFF, 0xD8, 0x7A);
        public static readonly IBrush Trunk = Solid(255, 0x6B, 0x4A, 0x2E);
        public static readonly IBrush Leaves = Solid(255, 0x2F, 0x6B, 0x38);
        public static readonly IBrush LeavesNight = Solid(255, 0x1B, 0x3A, 0x22);
        public static readonly IBrush Chimney = Solid(255, 0x8A, 0x84, 0x7C);
        public static readonly IBrush Smoke = Solid(110, 0xD0, 0xD4, 0xDA);
        public static readonly IBrush Hull = Solid(255, 0xF2, 0xF4, 0xF8);
        public static readonly IBrush Sail = Solid(240, 0xFF, 0xFF, 0xFF);
        public static readonly IPen Mast = MakePen(Solid(255, 0x30, 0x30, 0x30), 1.2);
        public static readonly IBrush Flag = Solid(255, 0xFF, 0x5A, 0x4A);
        public static readonly IPen Pole = MakePen(Solid(255, 0xC9, 0xD1, 0xDC), 1.6);

        public static readonly IBrush SunCore = Solid(255, 0xFF, 0xE6, 0x9A);
        public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient((Color.FromArgb(120, 0xFF, 0xD3, 0x6B), 0), (Color.FromArgb(0, 0xFF, 0xD3, 0x6B), 1));
        public static readonly IBrush MoonCore = Solid(255, 0xE8, 0xEC, 0xF2);
        public static readonly IBrush MoonGlow = SpaceDrawing.RadialGradient((Color.FromArgb(60, 0xC8, 0xD8, 0xFF), 0), (Color.FromArgb(0, 0xC8, 0xD8, 0xFF), 1));

        public static readonly IBrush CloudRim = SpaceDrawing.RadialGradient(
            (Color.FromArgb(150, 0xDC, 0xE3, 0xEB), 0.75),
            (Color.FromArgb(0, 0xDC, 0xE3, 0xEB), 1));

        public static readonly IBrush Particle = Solid(230, 0xEE, 0xF1, 0xF6);
        public static readonly IPen Axis = MakePen(Solid(110, 0xC9, 0xD1, 0xDC), 1.2);
        public static readonly IPen Tick = MakePen(Solid(150, 0xC9, 0xD1, 0xDC), 1);
        public static readonly IPen Front = MakePen(Solid(170, 0xFF, 0xD3, 0x6B), 1.4, dashed: true);
        public static readonly IPen Isobar = MakePen(Solid(200, 0xEE, 0xF1, 0xF6), 1.3, dashed: true);
        public static readonly IPen IsobarStrong = MakePen(Solid(235, 0xEE, 0xF1, 0xF6), 1.8);
        public static readonly IPen Frame = MakePen(Solid(60, 255, 255, 255), 1);
    }
}
