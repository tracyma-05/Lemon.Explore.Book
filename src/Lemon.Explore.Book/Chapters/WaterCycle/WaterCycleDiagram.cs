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

namespace Lemon.Explore.Book.Chapters.WaterCycle;

/// <summary>
/// The animated diagram of the water-cycle page. Cycle mode: a landscape from the sea over a mountain with every way water
/// moves, and a drop to follow (<c>WaterCycleDiagram.Cycle.cs</c>). Cloud mode: a parcel of air lifted until it cools to
/// its dew point, the cloud and the rain (<c>WaterCycleDiagram.Cloud.cs</c>). Stores mode: all of Earth's water as balls
/// beside Earth, and the yearly flows between sea, air and land (<c>WaterCycleDiagram.Stores.cs</c>).
/// </summary>
/// <remarks>
/// The diagram owns the clock. In cycle mode it moves the followed drop and pushes back <see cref="LegIndex"/>; clicking a
/// label picks <see cref="SelectedProcess"/>. In cloud mode it moves <see cref="Time"/> along the lift and holds a moment
/// at the end (rain), then starts again.
/// </remarks>
public sealed partial class WaterCycleDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            OnModeChanged();
        }
        else if (change.Property == WarmingProperty)
        {
            OnJourneyChanged();
        }
        else if (change.Property == FollowDropProperty)
        {
            OnJourneyChanged();
        }
        else if (change.Property == RouteProperty)
        {
            OnJourneyChanged();
        }
        else if (change.Property == LiftProperty)
        {
            OnCloudChanged();
        }
        else if (change.Property == GroundTemperatureProperty)
        {
            OnCloudChanged();
        }
        else if (change.Property == HumidityProperty)
        {
            OnCloudChanged();
        }
        else if (change.Property == StoresViewProperty)
        {
            _storesSeconds = 0;
        }
        else if (change.Property == TimeProperty)
        {
            OnTimeChanged(change.GetNewValue<double>());
        }
        else if (change.Property == RestartCountProperty)
        {
            Restart();
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Room for the title and the notes under it.</summary>
    private const double TitleSpace = 132;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    #region dependency properties

    public static readonly StyledProperty<WaterMode> ModeProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, WaterMode>(nameof(Mode), WaterMode.Cycle);

    /// <summary>Cycle mode: how much warmer (or colder) than today's world, °C.</summary>
    public static readonly StyledProperty<double> WarmingProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, double>(nameof(Warming), 0.0);

    /// <summary>Cycle mode: the way of moving to light up; clicking a label sets it.</summary>
    public static readonly StyledProperty<WaterProcess> SelectedProcessProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, WaterProcess>(nameof(SelectedProcess), WaterProcess.None, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, bool>(nameof(ShowLabels), true);

    /// <summary>Cycle mode: a drop is followed from the sea back to the sea.</summary>
    public static readonly StyledProperty<bool> FollowDropProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, bool>(nameof(FollowDrop), false);

    public static readonly StyledProperty<DropRoute> RouteProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, DropRoute>(nameof(Route), DropRoute.River);

    /// <summary>The step of the followed drop's journey it is on (pushed back to the page).</summary>
    public static readonly StyledProperty<int> LegIndexProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, int>(nameof(LegIndex), 0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<CloudLift> LiftProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, CloudLift>(nameof(Lift), CloudLift.Thermal);

    /// <summary>Cloud mode: the air's temperature at the ground, °C.</summary>
    public static readonly StyledProperty<double> GroundTemperatureProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, double>(nameof(GroundTemperature), 20.0);

    /// <summary>Cloud mode: the air's relative humidity at the ground, 0–1.</summary>
    public static readonly StyledProperty<double> HumidityProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, double>(nameof(Humidity), 0.6);

    public static readonly StyledProperty<StoresView> StoresViewProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, StoresView>(nameof(StoresView), StoresView.Spheres);

    /// <summary>Playback speed: 1 is the normal pace.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, double>(nameof(Speed), 1.0);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Cloud mode: seconds since the parcel set off.</summary>
    public static readonly StyledProperty<double> TimeProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, double>(nameof(Time), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Goes up each time the page asks to start again from the beginning.</summary>
    public static readonly StyledProperty<int> RestartCountProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, int>(nameof(RestartCount), 0);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<WaterCycleDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<WaterCycleDiagram>(ModeProperty, WarmingProperty, SelectedProcessProperty, ShowLabelsProperty, FollowDropProperty, RouteProperty, LiftProperty, GroundTemperatureProperty, HumidityProperty, StoresViewProperty, SpeedProperty, RestartCountProperty, ToolbarHeightProperty);
        return true;
    }


    public WaterMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double Warming
    {
        get => GetValue(WarmingProperty);
        set => SetValue(WarmingProperty, value);
    }

    public WaterProcess SelectedProcess
    {
        get => GetValue(SelectedProcessProperty);
        set => SetValue(SelectedProcessProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public bool FollowDrop
    {
        get => GetValue(FollowDropProperty);
        set => SetValue(FollowDropProperty, value);
    }

    public DropRoute Route
    {
        get => GetValue(RouteProperty);
        set => SetValue(RouteProperty, value);
    }

    public int LegIndex
    {
        get => GetValue(LegIndexProperty);
        set => SetValue(LegIndexProperty, value);
    }

    public CloudLift Lift
    {
        get => GetValue(LiftProperty);
        set => SetValue(LiftProperty, value);
    }

    public double GroundTemperature
    {
        get => GetValue(GroundTemperatureProperty);
        set => SetValue(GroundTemperatureProperty, value);
    }

    public double Humidity
    {
        get => GetValue(HumidityProperty);
        set => SetValue(HumidityProperty, value);
    }

    public StoresView StoresView
    {
        get => GetValue(StoresViewProperty);
        set => SetValue(StoresViewProperty, value);
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

    public double Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    public int RestartCount
    {
        get => GetValue(RestartCountProperty);
        set => SetValue(RestartCountProperty, value);
    }

    public double ToolbarHeight
    {
        get => GetValue(ToolbarHeightProperty);
        set => SetValue(ToolbarHeightProperty, value);
    }

    #endregion

    private readonly Stopwatch _clock = new();
    private readonly StarBackdrop _stars = new();
    private readonly LabelCache _labels;

    private double _lastSeconds;
    private bool _pushing;

    /// <summary>Animation seconds (scaled by the speed, stopped while paused): moves the vapour, clouds and rain.</summary>
    private double _t;

    /// <summary>Seconds the stores view has been shown, for its balls growing in.</summary>
    private double _storesSeconds;

    public WaterCycleDiagram()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
        OnJourneyChanged();
        OnCloudChanged();
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
        _storesSeconds = 0;
        Restart();
    }

    /// <summary>From the beginning: the drop back in the sea, the parcel back on the ground.</summary>
    private void Restart()
    {
        _dropTime = 0;
        Push(LegIndexProperty, 0);
        _cloudTime = 0;
        _held = 0;
        Push(TimeProperty, 0.0);
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;
        _storesSeconds += dt;
        _reserve.Step(IsLessonActive, dt);

        if (IsPlaying && dt > 0)
        {
            var step = dt * Math.Clamp(Speed, 0.1, 8);
            _t += step;
            switch (Mode)
            {
                case WaterMode.Cycle:
                    AdvanceDrop(step);
                    break;

                case WaterMode.Cloud:
                    AdvanceCloud(step, dt);
                    break;
            }
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

    /// <summary>The room for the picture: below the title, above the toolbar and, while a walkthrough runs, its subtitles.</summary>
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
            case WaterMode.Cloud:
                DrawCloudMode(dc, width, height);
                break;

            case WaterMode.Stores:
                DrawStoresMode(dc, width, height);
                break;

            default:
                DrawCycle(dc, width, height);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, _clock.Elapsed.TotalSeconds, width);
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

    /// <summary>Text whose right edge is at <paramref name="at"/>, its middle at the point's height.</summary>
    private void DrawTextRight(PaintContext dc, string text, Point at, Color color, double size = 11)
    {
        var formatted = _labels.Text(text, size, color);
        dc.DrawText(formatted, new Point(at.X - formatted.Width, at.Y - (formatted.Height / 2)));
    }

    /// <summary>One ball of a cloud: its centre and radii.</summary>
    private readonly record struct Puff(Point Center, double RadiusX, double RadiusY);

    /// <summary>A soft puffy cloud: overlapping balls round <paramref name="center"/>, flat and greyer underneath.</summary>
    private static void DrawCloud(PaintContext dc, Point center, double width, double height, double opacity, int seed, bool dark = false, bool withBase = true)
    {
        if (opacity <= 0.01 || width < 2) return;
        var puffs = new List<Puff>();
        AddCloudPuffs(puffs, center, width, height, seed);
        dc.PushOpacity(Math.Clamp(opacity, 0, 1));
        if (withBase) dc.DrawEllipse(dark ? Ink.CloudBaseDark : Ink.CloudBase, null, new Point(center.X, center.Y + (height * 0.32)), width * 0.46, height * 0.16);
        DrawPuffs(dc, puffs, dark);
        dc.Pop();
    }

    /// <summary>The balls of a cloud round <paramref name="center"/>: a row, higher in the middle.</summary>
    private static void AddCloudPuffs(List<Puff> puffs, Point center, double width, double height, int seed)
    {
        var random = new Random(seed);
        var count = 5 + (int)Math.Clamp(width / 40, 0, 7);
        for (var i = 0; i < count; i++)
        {
            var f = count == 1 ? 0.5 : i / (double)(count - 1);
            var x = center.X + ((f - 0.5) * width * 0.78) + ((random.NextDouble() - 0.5) * width * 0.08);
            var lift = Math.Sin(f * Math.PI);
            var r = height * (0.32 + (0.3 * lift) + (0.12 * random.NextDouble()));
            var y = center.Y + (height * 0.18) - (lift * height * 0.32) - (r * 0.25);
            puffs.Add(new Puff(new Point(x, y), r * 1.15, r));
        }
    }

    /// <summary>
    /// Draws a cloud's balls in two passes, the soft edges of all of them first and then their solid cores, so where balls
    /// overlap they merge into one cloud instead of showing every edge.
    /// </summary>
    private static void DrawPuffs(PaintContext dc, IReadOnlyList<Puff> puffs, bool dark)
    {
        if (puffs.Count == 0) return;

        // One light for the whole cloud: white where the Sun catches its top, greyer toward its base.
        var top = puffs.Min(p => p.Center.Y - p.RadiusY);
        var bottom = puffs.Max(p => p.Center.Y + p.RadiusY);
        var core = new ImmutableLinearGradientBrush(
        [
            new ImmutableGradientStop(0, dark ? Color.FromRgb(0xD4, 0xDA, 0xE2) : Color.FromRgb(0xFA, 0xFB, 0xFD)),
            new ImmutableGradientStop(0.55, dark ? Color.FromRgb(0xB0, 0xB9, 0xC5) : Color.FromRgb(0xE6, 0xEB, 0xF1)),
            new ImmutableGradientStop(1, dark ? Color.FromRgb(0x84, 0x8F, 0x9E) : Color.FromRgb(0xB8, 0xC1, 0xCD)),
        ],
            startPoint: new RelativePoint(0, top, RelativeUnit.Absolute),
            endPoint: new RelativePoint(0, Math.Max(top + 1, bottom), RelativeUnit.Absolute));
        var rim = dark ? Ink.CloudRimDark : Ink.CloudRim;
        foreach (var puff in puffs) dc.DrawEllipse(rim, null, puff.Center, puff.RadiusX * 1.08, puff.RadiusY * 1.08);
        foreach (var puff in puffs) dc.DrawEllipse(core, null, puff.Center, puff.RadiusX * 0.86, puff.RadiusY * 0.86);
    }

    /// <summary>
    /// A cumulus from its flat base up to <paramref name="topY"/>: a wide low heap at first, growing into a tower with a
    /// cauliflower top. The base is cut flat where the air reached its dew point.
    /// </summary>
    private static void DrawCumulus(PaintContext dc, double centerX, double baseY, double topY, double unit, int seed, bool dark)
    {
        var height = baseY - topY;
        if (height < 4) return;
        var random = new Random(seed);
        var width = CumulusWidth(unit, height);
        var puffs = new List<Puff>();
        var rows = Math.Max(1, (int)Math.Ceiling(height / 30));
        for (var k = 0; k <= rows; k++)
        {
            var f = k / (double)rows;
            var r = Math.Clamp(height / rows, 12, 34) * (0.95 + (0.45 * f));
            var half = Math.Max(r * 0.4, (width / 2 * (1 - (0.5 * f * f))) - (r * 0.55));
            var y = baseY - (f * (height - r)) - (r * 0.55);
            var n = Math.Max(1, (int)Math.Round(half * 2 / (r * 1.1)));
            for (var j = 0; j <= n; j++)
            {
                var x = centerX + ((((double)j / n * 2) - 1) * half) + ((random.NextDouble() - 0.5) * r * 0.4);
                puffs.Add(new Puff(new Point(x, y + ((random.NextDouble() - 0.5) * r * 0.3)), r * (0.9 + (0.25 * random.NextDouble())), r));
            }
        }

        // Flat underneath: nothing below the cloud base.
        dc.PushClip(new RectangleGeometry(new Rect(centerX - width, topY - 60, width * 2, height + 60 + 1)));
        DrawPuffs(dc, puffs, dark);
        dc.Pop();
        dc.DrawRectangle(dark ? Ink.CumulusBaseDark : Ink.CumulusBase, null, new Rect(centerX - (width * 0.5), baseY - 7, width, 8));
    }

    /// <summary>How wide a cumulus <paramref name="height"/> pixels tall is drawn.</summary>
    private static double CumulusWidth(double unit, double height) => Math.Clamp((unit * 0.1) + (height * 0.5), 50, unit * 0.42);

    /// <summary>A small six-armed snowflake.</summary>
    private static void DrawFlake(PaintContext dc, Point at, double size)
    {
        for (var k = 0; k < 3; k++)
        {
            var a = k * Math.PI / 3;
            var v = new Vector(Math.Cos(a) * size, Math.Sin(a) * size);
            dc.DrawLine(Ink.Flake, at - v, at + v);
        }
    }

    /// <summary>A falling raindrop's streak.</summary>
    private static void DrawRainStreak(PaintContext dc, Point at, double length) => dc.DrawLine(Ink.Rain, at, at + new Vector(-length * 0.12, length));

    /// <summary>An arrow from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static void DrawArrow(PaintContext dc, Point from, Point to, IBrush brush, double thickness = 2, double head = 9)
    {
        Vector v = to - from;
        if (v.Length < 3) return;
        var direction = v / v.Length;
        dc.DrawLine(MakePen(brush, thickness), from, to - (direction * head * 0.6));
        DrawArrowHead(dc, brush, to, direction, head);
    }

    /// <summary>The fractional part, for looping particles.</summary>
    private static double Frac(double value) => value - Math.Floor(value);

    /// <summary>0 below <paramref name="a"/>, 1 above <paramref name="b"/>, smooth between.</summary>
    private static double Smooth(double a, double b, double value)
    {
        var t = Math.Clamp((value - a) / (b - a), 0, 1);
        return t * t * (3 - (2 * t));
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
        public static readonly Color Green = Color.FromRgb(0x8B, 0xE0, 0x8F);
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x9F, 0x5A);
        public static readonly Color Red = Color.FromRgb(0xFF, 0x7A, 0x6B);
        public static readonly Color Blue = Color.FromRgb(0x5A, 0xA8, 0xFF);
        public static readonly Color Violet = Color.FromRgb(0xB3, 0x9D, 0xFF);
        public static readonly Color Snow = Color.FromRgb(0xF2, 0xF6, 0xFB);

        public static readonly IBrush Sky = SpaceDrawing.LinearGradient(90,
            (Color.FromRgb(0x07, 0x10, 0x22), 0),
            (Color.FromRgb(0x14, 0x2A, 0x4A), 0.62),
            (Color.FromRgb(0x2B, 0x4A, 0x6E), 1));

        public static readonly IBrush Sea = SpaceDrawing.LinearGradient(Color.FromRgb(0x1D, 0x5E, 0x96), Color.FromRgb(0x06, 0x1A, 0x33), 90);
        public static readonly IBrush Rock = SpaceDrawing.LinearGradient(Color.FromRgb(0x4A, 0x3A, 0x2C), Color.FromRgb(0x24, 0x1C, 0x16), 90);
        public static readonly IBrush SeaFloor = SpaceDrawing.LinearGradient(Color.FromRgb(0x3A, 0x33, 0x2A), Color.FromRgb(0x1E, 0x1A, 0x16), 90);
        public static readonly IBrush Saturated = Solid(105, 0x2A, 0x7E, 0xE0);
        public static readonly IBrush Snowcap = Solid(250, 0xF2, 0xF6, 0xFB);
        public static readonly IPen SnowEdge = MakePen(Solid(120, 0xB8, 0xC8, 0xDA), 1);
        public static readonly IPen Grass = MakePen(Solid(255, 0x4E, 0x8A, 0x3E), 3);
        public static readonly IPen Bare = MakePen(Solid(200, 0x8A, 0x7A, 0x66), 2);
        public static readonly IPen GroundLine = MakePen(Solid(120, 0, 0, 0), 1);
        public static readonly IPen WaterTable = MakePen(Solid(200, 0x6F, 0xC0, 0xFF), 1.4, dashed: true);
        public static readonly IPen Wave = MakePen(Solid(170, 0xB8, 0xE2, 0xFF), 1.4);
        public static readonly IPen Freezing = MakePen(Solid(120, 0x9F, 0xD8, 0xFF), 1, dashed: true);
        public static readonly IPen SnowLinePen = MakePen(Solid(110, 0xF2, 0xF6, 0xFB), 1, dashed: true);

        public static readonly IBrush CloudRim = SpaceDrawing.RadialGradient(
            (Color.FromArgb(150, 0xDC, 0xE3, 0xEB), 0.75),
            (Color.FromArgb(0, 0xDC, 0xE3, 0xEB), 1));

        public static readonly IBrush CloudRimDark = SpaceDrawing.RadialGradient(
            (Color.FromArgb(150, 0x9C, 0xA6, 0xB3), 0.75),
            (Color.FromArgb(0, 0x9C, 0xA6, 0xB3), 1));

        public static readonly IBrush CumulusBase = SpaceDrawing.LinearGradient(Color.FromArgb(0, 0xA8, 0xB2, 0xBF), Color.FromArgb(170, 0xA8, 0xB2, 0xBF), 90);
        public static readonly IBrush CumulusBaseDark = SpaceDrawing.LinearGradient(Color.FromArgb(0, 0x6E, 0x78, 0x86), Color.FromArgb(200, 0x6E, 0x78, 0x86), 90);

        public static readonly IBrush CloudBase = SpaceDrawing.RadialGradient((Color.FromArgb(120, 0xA8, 0xB2, 0xBF), 0), (Color.FromArgb(0, 0xA8, 0xB2, 0xBF), 1));
        public static readonly IBrush CloudBaseDark = SpaceDrawing.RadialGradient((Color.FromArgb(160, 0x6E, 0x78, 0x86), 0), (Color.FromArgb(0, 0x6E, 0x78, 0x86), 1));

        public static readonly IPen Rain = MakePen(Solid(200, 0x9F, 0xD0, 0xFF), 1.3);
        public static readonly IPen Flake = MakePen(Solid(235, 0xF2, 0xF6, 0xFB), 1.1);
        public static readonly IBrush Vapour = Solid(150, 0xD8, 0xEE, 0xFF);
        public static readonly IBrush LeafVapour = Solid(150, 0xC8, 0xF0, 0xC8);
        public static readonly IBrush WaterDot = Solid(230, 0x6F, 0xC0, 0xFF);
        public static readonly IPen Seep = MakePen(Solid(230, 0x6F, 0xC0, 0xFF), 1.6);
        public static readonly IBrush Wind = Solid(170, 0xEE, 0xF1, 0xF6);
        public static readonly IBrush River = Solid(255, 0x4A, 0x9A, 0xE8);
        public static readonly IPen Flow = MakePen(Solid(220, 0xD8, 0xEE, 0xFF), 1.4);

        public static readonly IBrush Trunk = Solid(255, 0x6B, 0x4A, 0x2E);
        public static readonly IBrush Leaves = Solid(255, 0x2F, 0x6B, 0x38);
        public static readonly IBrush LeavesLight = Solid(255, 0x3F, 0x85, 0x45);

        public static readonly IBrush SunCore = Solid(255, 0xFF, 0xE6, 0x9A);
        public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient((Color.FromArgb(110, 0xFF, 0xD3, 0x6B), 0), (Color.FromArgb(0, 0xFF, 0xD3, 0x6B), 1));

        public static readonly IBrush DropGlow = SpaceDrawing.RadialGradient((Color.FromArgb(200, 0xFF, 0xD3, 0x6B), 0), (Color.FromArgb(0, 0xFF, 0xD3, 0x6B), 1));
        public static readonly IBrush DropCore = Solid(255, 0xFF, 0xF1, 0xC4);
        public static readonly IPen DropTrail = MakePen(Solid(150, 0xFF, 0xD3, 0x6B), 1.6, dashed: true);
        public static readonly IPen Leader = MakePen(Solid(130, 0xEE, 0xF1, 0xF6), 1);
        public static readonly IPen LeaderBright = MakePen(Solid(230, 0xFF, 0xD3, 0x6B), 1.4);
        public static readonly IPen Tick = MakePen(Solid(150, 0xC9, 0xD1, 0xDC), 1);
        public static readonly IPen Axis = MakePen(Solid(110, 0xC9, 0xD1, 0xDC), 1.2);
    }
}
