using System.ComponentModel;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Timing;

namespace Lemon.Explore.Book.Chapters.BlueSky;

/// <summary>
/// The animated diagram of the blue sky page. Sky mode: the sky worked out from how sunlight scatters, over a sea
/// horizon, facing the Sun or with it behind you (<c>SkyDiagram.Sky.cs</c>). Scattering mode: a beam of sunlight through
/// a box of air, its colours bounced aside (<c>SkyDiagram.Scattering.cs</c>). Path mode: the way sunlight comes through
/// the air to you, from noon to after sunset (<c>SkyDiagram.Path.cs</c>).
/// </summary>
/// <remarks>
/// Time is <see cref="Hour"/>, local solar time on an equinox day (see <see cref="SkyOptics"/>). The diagram owns the
/// clock: playing, the day runs from dawn to night, slower while the Sun is low, holds a moment and starts again.
/// Dragging in the sky (sky mode) or round the observer (path mode) moves the Sun and pauses; clicking a thing explains
/// it (<c>SkyDiagram.Objects.cs</c>).
/// </remarks>
public sealed partial class SkyDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HourProperty)
        {
            OnHourChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>Seconds playback holds on the night before the next dawn.</summary>
    private const double EndPause = 2.5;

    private const double Deg = Math.PI / 180;

    #region dependency properties

    public static readonly StyledProperty<SkyMode> ModeProperty =
        AvaloniaProperty.Register<SkyDiagram, SkyMode>(nameof(Mode), SkyMode.Sky);

    /// <summary>Local solar time, <see cref="SkyOptics.FirstHour"/>…<see cref="SkyOptics.LastHour"/>.</summary>
    public static readonly StyledProperty<double> HourProperty =
        AvaloniaProperty.Register<SkyDiagram, double>(nameof(Hour), 16.5, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<SkyDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Haze, dust and water droplets in the air, from very clean (0) to thick haze (1).</summary>
    public static readonly StyledProperty<double> HazeProperty =
        AvaloniaProperty.Register<SkyDiagram, double>(nameof(Haze), 0.3);

    /// <summary>Sky mode: face away from the Sun, to see the Earth's shadow rise after sunset.</summary>
    public static readonly StyledProperty<bool> LookAwayProperty =
        AvaloniaProperty.Register<SkyDiagram, bool>(nameof(LookAway), false);

    /// <summary>The latitude of the day (the shared observer location's), degrees north; an equinox there.</summary>
    public static readonly StyledProperty<double> LatitudeProperty =
        AvaloniaProperty.Register<SkyDiagram, double>(nameof(Latitude), SkyOptics.DefaultLatitude);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<SkyDiagram>(LatitudeProperty);
        return true;
    }

    public double Latitude
    {
        get => GetValue(LatitudeProperty);
        set => SetValue(LatitudeProperty, value);
    }

    public SkyMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double Hour
    {
        get => GetValue(HourProperty);
        set => SetValue(HourProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public double Haze
    {
        get => GetValue(HazeProperty);
        set => SetValue(HazeProperty, value);
    }

    public bool LookAway
    {
        get => GetValue(LookAwayProperty);
        set => SetValue(LookAwayProperty, value);
    }

    #endregion

    private readonly BookStopwatch _clock = new();
    private readonly StarBackdrop _stars = new();
    private readonly LabelCache _labels;
    private readonly Random _random = new(7);

    private double _lastSeconds;
    private bool _pushing;
    private double _hour = 16.5;

    /// <summary>Time held at the end of the day before the next dawn.</summary>
    private double _overrun;

    private bool _dragging;

    public SkyDiagram()
    {
        _labels = new LabelCache(this);
        ClipToBounds = true;
    }

    private double Altitude => SkyOptics.SunAltitude(_hour, Latitude);

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

    private void OnHourChanged(double value)
    {
        if (_pushing) return;
        _hour = Math.Clamp(value, SkyOptics.FirstHour, SkyOptics.LastHour);
        _overrun = 0;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _stars.OnSizeChanged(_clock.Elapsed.TotalSeconds);
    }

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.1);
        _lastSeconds = now;
        _reserve.Step(IsLessonActive, dt);

        if (IsPlaying && !_dragging && dt > 0)
        {
            if (_hour >= SkyOptics.LastHour)
            {
                _overrun += dt;
                if (_overrun > EndPause) PushHour(SkyOptics.FirstHour);
            }
            else
            {
                PushHour(Math.Min(SkyOptics.LastHour, _hour + (HoursPerSecond(Altitude) * dt)));
            }
        }

        if (Mode == SkyMode.Scattering) StepPhotons(dt);
        if (Mode == SkyMode.Path) StepSparkles(dt);
        InvalidateVisual();
    }

    /// <summary>The day goes by fast at noon and slowly round sunrise and sunset, when the colours change.</summary>
    internal static double HoursPerSecond(double altitude) => 0.07 + (0.45 * Math.Min(1, Math.Abs(altitude) / 40));

    private void PushHour(double hour)
    {
        _hour = Math.Clamp(hour, SkyOptics.FirstHour, SkyOptics.LastHour);
        _overrun = 0;
        _pushing = true;
        try
        {
            SetCurrentValue(HourProperty, _hour);
        }
        finally
        {
            _pushing = false;
        }
    }

    /// <summary>Moves the Sun to an altitude, keeping to the morning or afternoon side it is on.</summary>
    private void PushAltitude(double altitude, bool? afternoon = null) =>
        PushHour(SkyOptics.HourAt(Math.Clamp(altitude, SkyOptics.SunAltitude(SkyOptics.LastHour, Latitude), SkyOptics.NoonAltitude(Latitude)),
            afternoon ?? _hour >= 12, Latitude));

    #endregion

    #region dragging the Sun

    private void DragTo(Point point)
    {
        if (Mode == SkyMode.Path)
        {
            var observer = PathObserver();
            var altitude = Math.Atan2(observer.Y - point.Y, Math.Abs(point.X - observer.X)) / Deg;
            PushAltitude(altitude, point.X >= observer.X);
            return;
        }

        PushAltitude((SkyHorizonY() - point.Y) / SkyScale());
    }

    #endregion

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        _targets.Clear();
        switch (Mode)
        {
            case SkyMode.Scattering:
                DrawScattering(dc, now);
                break;

            case SkyMode.Path:
                DrawPath(dc, now);
                break;

            default:
                DrawSky(dc, now);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, now, width);
    }

    /// <summary>The colour of the Sun's light: its hue, paler the more of it gets through.</summary>
    internal static Color SunColour(LinearRgb light)
    {
        var hue = SkyOptics.Hue(light);
        return Lerp(hue, Colors.White, Math.Clamp(light.Luminance, 0, 0.85));
    }

    /// <summary>The light left of sunlight after a column of air, as a colour (see <see cref="SunColour"/>).</summary>
    internal static LinearRgb Through(AirColumn column, double haze)
    {
        if (column.Blocked) return default;
        var bands = new double[SkyOptics.Bands];
        for (var b = 0; b < bands.Length; b++) bands[b] = Math.Exp(-SkyOptics.OpticalDepth(SkyOptics.Wavelengths[b], column, haze));
        return SkyOptics.ToRgb(bands);
    }

    private void DrawCenteredPill(PaintContext dc, string text, Point center, double size, Color color)
    {
        var formatted = _labels.Text(text, size, color);
        _labels.DrawTextPill(dc, text, new Point(center.X - (formatted.Width / 2) - 6, center.Y - (formatted.Height / 2) - 2), size, color);
    }

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    private static class Ink
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Dim = Color.FromRgb(0xB4, 0xBE, 0xCC);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);

        /// <summary>A dark wash behind the title, so it reads over a bright sky.</summary>
        public static readonly IBrush TitleScrim = SpaceDrawing.LinearGradient(90,
            (Color.FromArgb(120, 0, 0, 0), 0),
            (Color.FromArgb(0, 0, 0, 0), 1));
    }
}
