using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Waves;

/// <summary>
/// The animated diagram of the waves page. Ripple mode: two dippers in a ripple tank seen from above, the lines of still
/// water between them and a probe that shows how the two waves meet there (<c>WavesDiagram.Ripple.cs</c>). Slits mode:
/// light from a laser through two slits onto a screen (<c>WavesDiagram.Slits.cs</c>). Doppler mode: a source of sound
/// driving past a listener, its rings bunched up ahead and, faster than sound, piled into a cone
/// (<c>WavesDiagram.Doppler.cs</c>).
/// </summary>
/// <remarks>
/// The diagram owns the clock and pushes <see cref="Time"/> back: real seconds of the ripple tank (run
/// <see cref="WavePhysics.RippleSlowdown"/> times slower) and of the Doppler pass (run
/// <see cref="WavePhysics.DopplerSlowdown"/> times slower, starting again after each pass), seconds of the picture for
/// the light. Fronts spread from the sources when the page starts the waves again or a source is switched on. Dragging
/// moves the probe (ripple tank, slits) or the listener (Doppler).
/// </remarks>
public sealed partial class WavesDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModeProperty)
        {
            Restart();
        }
        else if (change.Property == SecondSourceProperty)
        {
            OnSecondSourceChanged(change.GetNewValue<bool>());
        }
        else if (change.Property == OneSlitProperty)
        {
            Restart();
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

    /// <summary>Space kept clear at the top for the page's title, DIPs.</summary>
    private const double TitleSpace = 164;

    /// <summary>Space kept clear at the bottom for the toolbar when it is one row high, DIPs.</summary>
    private const double ToolbarSpace = 86;

    #region dependency properties

    public static readonly StyledProperty<WaveMode> ModeProperty =
        AvaloniaProperty.Register<WavesDiagram, WaveMode>(nameof(Mode), WaveMode.Ripple);

    /// <summary>Ripple tank: wavelength, cm.</summary>
    public static readonly StyledProperty<double> WavelengthProperty =
        AvaloniaProperty.Register<WavesDiagram, double>(nameof(Wavelength), 2.5);

    /// <summary>Ripple tank: distance between the dippers, cm. Dragging a dipper sets it.</summary>
    public static readonly StyledProperty<double> SeparationProperty =
        AvaloniaProperty.Register<WavesDiagram, double>(nameof(Separation), 6.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Ripple tank: the dippers move opposite ways (B half a cycle behind A).</summary>
    public static readonly StyledProperty<bool> OppositeProperty =
        AvaloniaProperty.Register<WavesDiagram, bool>(nameof(Opposite), false);

    /// <summary>Ripple tank: whether dipper B is on.</summary>
    public static readonly StyledProperty<bool> SecondSourceProperty =
        AvaloniaProperty.Register<WavesDiagram, bool>(nameof(SecondSource), true);

    /// <summary>Ripple tank: draw the lines of still water and of the biggest waves.</summary>
    public static readonly StyledProperty<bool> ShowLinesProperty =
        AvaloniaProperty.Register<WavesDiagram, bool>(nameof(ShowLines), false);

    /// <summary>Ripple tank: show how far the water rises and falls at each point instead of the water now.</summary>
    public static readonly StyledProperty<bool> ShowAverageProperty =
        AvaloniaProperty.Register<WavesDiagram, bool>(nameof(ShowAverage), false);

    /// <summary>Ripple tank: the probe, cm from the midpoint between the dippers (y down). Dragging sets it.</summary>
    public static readonly StyledProperty<Point> ProbeProperty =
        AvaloniaProperty.Register<WavesDiagram, Point>(nameof(Probe), new Point(5, 9), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Slits: the light's wavelength, nm.</summary>
    public static readonly StyledProperty<double> LightWavelengthProperty =
        AvaloniaProperty.Register<WavesDiagram, double>(nameof(LightWavelength), 532.0);

    /// <summary>Slits: distance between the slits, mm.</summary>
    public static readonly StyledProperty<double> SlitSpacingProperty =
        AvaloniaProperty.Register<WavesDiagram, double>(nameof(SlitSpacing), 0.25);

    /// <summary>Slits: the lower slit is covered.</summary>
    public static readonly StyledProperty<bool> OneSlitProperty =
        AvaloniaProperty.Register<WavesDiagram, bool>(nameof(OneSlit), false);

    /// <summary>Slits: white light instead of a laser.</summary>
    public static readonly StyledProperty<bool> WhiteLightProperty =
        AvaloniaProperty.Register<WavesDiagram, bool>(nameof(WhiteLight), false);

    /// <summary>Slits: the probe, in picture units from the midpoint between the slits (y down). Dragging sets it.</summary>
    public static readonly StyledProperty<Point> SlitProbeProperty =
        AvaloniaProperty.Register<WavesDiagram, Point>(nameof(SlitProbe), new Point(SlitScreen, 0), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Doppler: the source's speed as a share of the speed of sound.</summary>
    public static readonly StyledProperty<double> MachProperty =
        AvaloniaProperty.Register<WavesDiagram, double>(nameof(Mach), 0.5);

    /// <summary>Doppler: where the listener stands, metres from where the source passes halfway (y down). Dragging sets it.</summary>
    public static readonly StyledProperty<Point> ListenerProperty =
        AvaloniaProperty.Register<WavesDiagram, Point>(nameof(Listener), new Point(60, 45), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Playback speed: 1 is the mode's own pace.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<WavesDiagram, double>(nameof(Speed), 1.0);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<WavesDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Seconds of the mode's clock (see the remarks). The page's timeline sets it for the Doppler pass.</summary>
    public static readonly StyledProperty<double> TimeProperty =
        AvaloniaProperty.Register<WavesDiagram, double>(nameof(Time), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Goes up each time the page asks to start the waves (or the pass) again.</summary>
    public static readonly StyledProperty<int> RestartCountProperty =
        AvaloniaProperty.Register<WavesDiagram, int>(nameof(RestartCount), 0);

    /// <summary>
    /// Height of the page's toolbar over the bottom of the diagram, DIPs: in a narrow window it wraps onto a second row,
    /// and the picture moves up to stay clear of it.
    /// </summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<WavesDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<WavesDiagram>(ToolbarHeightProperty);
        return true;
    }


    public WaveMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double Wavelength
    {
        get => GetValue(WavelengthProperty);
        set => SetValue(WavelengthProperty, value);
    }

    public double Separation
    {
        get => GetValue(SeparationProperty);
        set => SetValue(SeparationProperty, value);
    }

    public bool Opposite
    {
        get => GetValue(OppositeProperty);
        set => SetValue(OppositeProperty, value);
    }

    public bool SecondSource
    {
        get => GetValue(SecondSourceProperty);
        set => SetValue(SecondSourceProperty, value);
    }

    public bool ShowLines
    {
        get => GetValue(ShowLinesProperty);
        set => SetValue(ShowLinesProperty, value);
    }

    public bool ShowAverage
    {
        get => GetValue(ShowAverageProperty);
        set => SetValue(ShowAverageProperty, value);
    }

    public Point Probe
    {
        get => GetValue(ProbeProperty);
        set => SetValue(ProbeProperty, value);
    }

    public double LightWavelength
    {
        get => GetValue(LightWavelengthProperty);
        set => SetValue(LightWavelengthProperty, value);
    }

    public double SlitSpacing
    {
        get => GetValue(SlitSpacingProperty);
        set => SetValue(SlitSpacingProperty, value);
    }

    public bool OneSlit
    {
        get => GetValue(OneSlitProperty);
        set => SetValue(OneSlitProperty, value);
    }

    public bool WhiteLight
    {
        get => GetValue(WhiteLightProperty);
        set => SetValue(WhiteLightProperty, value);
    }

    public Point SlitProbe
    {
        get => GetValue(SlitProbeProperty);
        set => SetValue(SlitProbeProperty, value);
    }

    public double Mach
    {
        get => GetValue(MachProperty);
        set => SetValue(MachProperty, value);
    }

    public Point Listener
    {
        get => GetValue(ListenerProperty);
        set => SetValue(ListenerProperty, value);
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
    private readonly LabelCache _labels;
    private readonly WaveField _field = new();

    private double _lastSeconds;
    private bool _pushing;
    private bool _dragging;

    /// <summary>Seconds of the mode's clock.</summary>
    private double _time;

    /// <summary>When the waves of dipper A, of dipper B and of the laser started spreading.</summary>
    private double _startA;
    private double _startB;
    private double _startLight;

    public WavesDiagram()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.MediumQuality);
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

    /// <summary>Starts the waves spreading from their sources again; the Doppler pass from its start.</summary>
    private void Restart()
    {
        if (Mode == WaveMode.Doppler)
        {
            PushTime(0);
            return;
        }

        _startA = _time;
        _startB = _time;
        _startLight = _time;
        _slitScreen = null;
    }

    private void OnSecondSourceChanged(bool on)
    {
        // Dipper B's rings spread out from it when it is switched on.
        if (on) _startB = _time;
    }

    private void OnTimeChanged(double value)
    {
        if (_pushing) return;
        var shift = value - _time;
        _time = value;

        // A jump on the timeline keeps the fronts where they were.
        if (Mode != WaveMode.Doppler)
        {
            _startA += shift;
            _startB += shift;
            _startLight += shift;
        }
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;
        _reserve.Step(IsLessonActive, dt);

        if (IsPlaying && !(_dragging && Mode == WaveMode.Doppler) && dt > 0)
        {
            var step = dt * Math.Clamp(Speed, 0.05, 8);
            switch (Mode)
            {
                case WaveMode.Ripple:
                    PushTime(_time + (step / WavePhysics.RippleSlowdown));
                    break;

                case WaveMode.Slits:
                    PushTime(_time + step);
                    break;

                default:
                    var next = _time + (step / WavePhysics.DopplerSlowdown);
                    PushTime(next >= WavePhysics.LoopDuration(Mach, Listener) ? 0 : next);
                    break;
            }
        }

        InvalidateVisual();
    }

    private void PushTime(double value)
    {
        _time = value;
        Push(TimeProperty, value);
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
    /// The room for the picture: below the title, above the toolbar (its margin and a gap included) and, while a
    /// walkthrough runs, its subtitles.
    /// </summary>
    private Rect Stage()
    {
        var top = Math.Min(TitleSpace, ActualHeight * 0.26);
        var bottom = Math.Max(ToolbarSpace, ToolbarHeight + 18 + 16) + _reserve.Current;
        return new Rect(24, top, Math.Max(100, ActualWidth - 48), Math.Max(120, ActualHeight - top - bottom));
    }

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        _targets.Clear();
        _now = _clock.Elapsed.TotalSeconds;
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        switch (Mode)
        {
            case WaveMode.Slits:
                DrawSlits(dc);
                break;

            case WaveMode.Doppler:
                DrawDoppler(dc);
                break;

            default:
                DrawRipple(dc);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, _now, width);
    }

    #region drawing helpers

    /// <summary>Already localized text on a dark pill, centred on <paramref name="center"/>.</summary>
    private void DrawCenteredPill(PaintContext dc, string text, Point center, double size, Color color)
    {
        var formatted = _labels.Text(text, size, color);
        _labels.DrawTextPill(dc, text, new Point(center.X - (formatted.Width / 2) - 6, center.Y - (formatted.Height / 2) - 2), size, color);
    }

    /// <summary>
    /// A pill with its top-left corner moved so it stays inside <paramref name="bounds"/>; a target named
    /// <paramref name="id"/> if given.
    /// </summary>
    private void DrawPillInside(PaintContext dc, string text, Point topLeft, Rect bounds, double size, Color color, string? id = null)
    {
        var formatted = _labels.Text(text, size, color);
        var x = Math.Clamp(topLeft.X, bounds.Left + 4, Math.Max(bounds.Left + 4, bounds.Right - formatted.Width - 16));
        var y = Math.Clamp(topLeft.Y, bounds.Top + 4, Math.Max(bounds.Top + 4, bounds.Bottom - formatted.Height - 8));
        _labels.DrawTextPill(dc, text, new Point(x, y), size, color);
        if (id is not null) AddPillTarget(id, formatted, new Point(x, y));
    }

    /// <summary>A scale bar "<paramref name="label"/>" of <paramref name="length"/> DIPs from <paramref name="left"/>.</summary>
    private void DrawScaleBar(PaintContext dc, Point left, double length, string label)
    {
        dc.DrawLine(Ink.ScalePen, left, new Point(left.X + length, left.Y));
        dc.DrawLine(Ink.ScalePen, new Point(left.X, left.Y - 4), new Point(left.X, left.Y + 4));
        dc.DrawLine(Ink.ScalePen, new Point(left.X + length, left.Y - 4), new Point(left.X + length, left.Y + 4));
        var text = _labels.Text(label, 11, Ink.Dim);
        dc.DrawText(text, new Point(left.X + ((length - text.Width) / 2), left.Y - text.Height - 3));
    }

    /// <summary>A probe ring: a bright ring with a dark edge and a dot.</summary>
    private static void DrawProbeMark(PaintContext dc, Point at, IBrush brush)
    {
        dc.DrawEllipse(null, Ink.ProbeShadow, at, 9, 9);
        dc.DrawEllipse(null, MakePen(brush, 2), at, 9, 9);
        dc.DrawEllipse(brush, null, at, 2.5, 2.5);
    }

    /// <summary>"1½ λ", "½ λ", "2 λ", "0": a path difference in wavelengths, to the nearest half.</summary>
    internal static string Halves(double wavelengths)
    {
        var halves = (int)Math.Round(Math.Abs(wavelengths) * 2);
        if (halves == 0) return "0";
        var whole = halves / 2;
        var text = halves % 2 == 1 ? whole == 0 ? "½" : $"{whole}½" : $"{whole}";
        return $"{text} λ";
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
        public static readonly Color Pink = Color.FromRgb(0xFF, 0x8F, 0xB8);
        public static readonly Color Orange = Color.FromRgb(0xFF, 0x9F, 0x5A);

        public static readonly IBrush Backdrop = SpaceDrawing.LinearGradient(Color.FromRgb(0x05, 0x08, 0x10), Color.FromRgb(0x0B, 0x11, 0x1C), 90);
        public static readonly IBrush GoldBrush = Solid(255, 0xFF, 0xD3, 0x6B);
        public static readonly IBrush CyanBrush = Solid(255, 0x6F, 0xD8, 0xFF);
        public static readonly IBrush PinkBrush = Solid(255, 0xFF, 0x8F, 0xB8);
        public static readonly IBrush WhiteBrush = Solid(245, 0xEE, 0xF1, 0xF6);

        public static readonly IPen Frame = MakePen(Solid(90, 0xC9, 0xD1, 0xDC), 1.2);
        public static readonly IPen ScalePen = MakePen(Solid(170, 0xC9, 0xD1, 0xDC), 1.2);
        public static readonly IPen ProbeShadow = MakePen(Solid(150, 0, 0, 0), 4);
        public static readonly IPen QuietLine = MakePen(Solid(170, 0xEE, 0xF1, 0xF6), 1.4, dashed: true);
        public static readonly IPen LoudLine = MakePen(Solid(120, 0xFF, 0xD3, 0x6B), 1.1);
        public static readonly IPen PathA = MakePen(Solid(200, 0x6F, 0xD8, 0xFF), 1.3, dashed: true);
        public static readonly IPen PathB = MakePen(Solid(200, 0xFF, 0x8F, 0xB8), 1.3, dashed: true);
        public static readonly IPen DipperRim = MakePen(Solid(200, 0, 0, 0), 1);

        // Slits
        public static readonly IBrush SlitBackdrop = Solid(255, 0x02, 0x03, 0x06);
        public static readonly IBrush LaserBody = SpaceDrawing.LinearGradient(Color.FromRgb(0x3A, 0x42, 0x50), Color.FromRgb(0x1C, 0x21, 0x2A), 90);
        public static readonly IBrush BarrierFill = Solid(255, 0x5A, 0x63, 0x72);
        public static readonly IBrush TapeFill = Solid(255, 0x8A, 0x6A, 0x3A);
        public static readonly IPen TapePen = MakePen(Solid(255, 0xC9, 0xA2, 0x5E), 1);
        public static readonly IBrush ScreenBack = Solid(255, 0x10, 0x12, 0x16);

        // Doppler
        public static readonly IBrush Ground = SpaceDrawing.LinearGradient(Color.FromRgb(0x0E, 0x16, 0x14), Color.FromRgb(0x14, 0x1E, 0x1A), 90);
        public static readonly IBrush Road = Solid(255, 0x1D, 0x21, 0x28);
        public static readonly IPen RoadEdge = MakePen(Solid(110, 0xC9, 0xD1, 0xDC), 1);
        public static readonly IPen RoadMarking = MakePen(Solid(90, 0xFF, 0xD3, 0x6B), 1.4, dashed: true);
        public static readonly IBrush ConeFill = Solid(28, 0xFF, 0x9F, 0x5A);
        public static readonly IPen ConePen = MakePen(Solid(230, 0xFF, 0x9F, 0x5A), 2);
        public static readonly IBrush SourceGlow = SpaceDrawing.RadialGradient((Color.FromArgb(120, 0xFF, 0xD3, 0x6B), 0), (Color.FromArgb(0, 0xFF, 0xD3, 0x6B), 1));
        public static readonly IPen ArrivalPen = MakePen(Solid(255, 0xB8, 0xE6, 0xFF), 2);
        public static readonly IBrush BoomFill = SpaceDrawing.RadialGradient((Color.FromArgb(200, 0xFF, 0xB0, 0x6A), 0), (Color.FromArgb(0, 0xFF, 0x9F, 0x5A), 1));
        public static readonly IBrush Head = Solid(255, 0xE6, 0xE9, 0xEF);
        public static readonly IBrush Ear = Solid(255, 0xC9, 0xA0, 0x8A);
    }
}
