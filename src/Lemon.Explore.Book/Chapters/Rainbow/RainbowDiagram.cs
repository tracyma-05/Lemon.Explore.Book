using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.Eclipses;
using Lemon.Explore.Book.Chapters.BlueSky;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Rainbow;

/// <summary>
/// The animated diagram of the rainbow page. Refraction mode: a beam meeting a flat surface, its wave crests closing up
/// as it slows, part reflected and the rest bent, or a white beam split by a prism (<c>RainbowDiagram.Refraction.cs</c>).
/// Drop mode: sunlight through one raindrop, in, reflected inside and out, the rays crowding at 42°
/// (<c>RainbowDiagram.Drop.cs</c>). Sky mode: the bows round the point opposite the Sun, worked out pixel by pixel from
/// the drops (<c>RainbowDiagram.Sky.cs</c>).
/// </summary>
/// <remarks>
/// While playing, the crests, light pulses and rain move; while sweeping, the view's main value (the beam's angle, where
/// the ray enters the drop, or the Sun's height) runs back and forth and is pushed back to the page. Dragging sets it.
/// </remarks>
public sealed partial class RainbowDiagram : Control
{
    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>The book puts the title on the left page: only a margin at the top (the original kept 164 for its title).</summary>
    private const double TitleSpace = 34;

    /// <summary>Room for the toolbar at the bottom (its margin and a gap included).</summary>
    private const double ToolbarSpace = 86;

    private const double Deg = Math.PI / 180;

    #region dependency properties

    public static readonly StyledProperty<RainbowMode> ModeProperty =
        AvaloniaProperty.Register<RainbowDiagram, RainbowMode>(nameof(Mode), RainbowMode.Refraction);
    public static readonly StyledProperty<RefractionScene> SceneProperty =
        AvaloniaProperty.Register<RainbowDiagram, RefractionScene>(nameof(Scene), RefractionScene.Surface);
    public static readonly StyledProperty<OpticalMedium> MediumProperty =
        AvaloniaProperty.Register<RainbowDiagram, OpticalMedium>(nameof(Medium), OpticalMedium.Water);
    public static readonly StyledProperty<LightColour> ColourProperty =
        AvaloniaProperty.Register<RainbowDiagram, LightColour>(nameof(Colour), LightColour.White);

    /// <summary>Surface: the light comes from inside the water (or glass) out into the air.</summary>
    public static readonly StyledProperty<bool> FromInsideProperty =
        AvaloniaProperty.Register<RainbowDiagram, bool>(nameof(FromInside), false);

    /// <summary>Surface: wave crests across the beams.</summary>
    public static readonly StyledProperty<bool> ShowWavesProperty =
        AvaloniaProperty.Register<RainbowDiagram, bool>(nameof(ShowWaves), true);

    /// <summary>Refraction: the colours of white light spread five times wider than they really are, to be seen.</summary>
    public static readonly StyledProperty<bool> ExaggerateProperty =
        AvaloniaProperty.Register<RainbowDiagram, bool>(nameof(Exaggerate), true);

    public static readonly StyledProperty<BowOrder> OrderProperty =
        AvaloniaProperty.Register<RainbowDiagram, BowOrder>(nameof(Order), BowOrder.Primary);

    /// <summary>Drop: a sheet of rays across the whole drop, to see them crowd at the rainbow angle.</summary>
    public static readonly StyledProperty<bool> ShowSheetProperty =
        AvaloniaProperty.Register<RainbowDiagram, bool>(nameof(ShowSheet), true);

    /// <summary>Drop: the light that leaves the drop on the way, with how much.</summary>
    public static readonly StyledProperty<bool> ShowLossesProperty =
        AvaloniaProperty.Register<RainbowDiagram, bool>(nameof(ShowLosses), true);

    public static readonly StyledProperty<DropSize> DropSizeProperty =
        AvaloniaProperty.Register<RainbowDiagram, DropSize>(nameof(DropSize), DropSize.Rain);

    /// <summary>Sky: the secondary bow (and in the drop's brightness chart).</summary>
    public static readonly StyledProperty<bool> ShowSecondaryProperty =
        AvaloniaProperty.Register<RainbowDiagram, bool>(nameof(ShowSecondary), true);

    /// <summary>Sky: the point opposite the Sun, the 42° and 51° circles and labels.</summary>
    public static readonly StyledProperty<bool> ShowGuidesProperty =
        AvaloniaProperty.Register<RainbowDiagram, bool>(nameof(ShowGuides), true);

    /// <summary>Refraction: the beam's angle from the normal, degrees. Dragging and sweeping set it.</summary>
    public static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<RainbowDiagram, double>(nameof(Angle), 40.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Drop: how far off the drop's centre the followed ray enters, in radii (0–1).</summary>
    public static readonly StyledProperty<double> ImpactProperty =
        AvaloniaProperty.Register<RainbowDiagram, double>(nameof(Impact), 0.5, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Sky: the Sun's height above the horizon, degrees.</summary>
    public static readonly StyledProperty<double> SunAltitudeProperty =
        AvaloniaProperty.Register<RainbowDiagram, double>(nameof(SunAltitude), 20.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<RainbowDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsSweepingProperty =
        AvaloniaProperty.Register<RainbowDiagram, bool>(nameof(IsSweeping), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Height of the toolbar over the bottom of the diagram, so a toolbar wrapped onto two rows covers nothing.</summary>
    public static readonly StyledProperty<double> ToolbarHeightProperty =
        AvaloniaProperty.Register<RainbowDiagram, double>(nameof(ToolbarHeight), 0.0);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnProperties = RedrawOnProperties();

    private static bool RedrawOnProperties()
    {
        AffectsRender<RainbowDiagram>(ModeProperty, SceneProperty, MediumProperty, ColourProperty, FromInsideProperty, ShowWavesProperty, ExaggerateProperty, OrderProperty, ShowSheetProperty, ShowLossesProperty, DropSizeProperty, ShowSecondaryProperty, ShowGuidesProperty, AngleProperty, ImpactProperty, SunAltitudeProperty, IsPlayingProperty, IsSweepingProperty, ToolbarHeightProperty);
        return true;
    }



    public RainbowMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public RefractionScene Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public OpticalMedium Medium
    {
        get => GetValue(MediumProperty);
        set => SetValue(MediumProperty, value);
    }

    public LightColour Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    public bool FromInside
    {
        get => GetValue(FromInsideProperty);
        set => SetValue(FromInsideProperty, value);
    }

    public bool ShowWaves
    {
        get => GetValue(ShowWavesProperty);
        set => SetValue(ShowWavesProperty, value);
    }

    public bool Exaggerate
    {
        get => GetValue(ExaggerateProperty);
        set => SetValue(ExaggerateProperty, value);
    }

    public BowOrder Order
    {
        get => GetValue(OrderProperty);
        set => SetValue(OrderProperty, value);
    }

    public bool ShowSheet
    {
        get => GetValue(ShowSheetProperty);
        set => SetValue(ShowSheetProperty, value);
    }

    public bool ShowLosses
    {
        get => GetValue(ShowLossesProperty);
        set => SetValue(ShowLossesProperty, value);
    }

    public DropSize DropSize
    {
        get => GetValue(DropSizeProperty);
        set => SetValue(DropSizeProperty, value);
    }

    public bool ShowSecondary
    {
        get => GetValue(ShowSecondaryProperty);
        set => SetValue(ShowSecondaryProperty, value);
    }

    public bool ShowGuides
    {
        get => GetValue(ShowGuidesProperty);
        set => SetValue(ShowGuidesProperty, value);
    }

    public double Angle
    {
        get => GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }

    public double Impact
    {
        get => GetValue(ImpactProperty);
        set => SetValue(ImpactProperty, value);
    }

    public double SunAltitude
    {
        get => GetValue(SunAltitudeProperty);
        set => SetValue(SunAltitudeProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public bool IsSweeping
    {
        get => GetValue(IsSweepingProperty);
        set => SetValue(IsSweepingProperty, value);
    }

    public double ToolbarHeight
    {
        get => GetValue(ToolbarHeightProperty);
        set => SetValue(ToolbarHeightProperty, value);
    }

    #endregion

    private readonly Stopwatch _clock = new();
    private readonly LabelCache _labels;

    private double _lastSeconds;

    /// <summary>Seconds of animation played (crests, pulses, rain); stands still while paused.</summary>
    private double _time;

    /// <summary>Which way the sweep runs now: +1 or −1.</summary>
    private int _sweepDirection = 1;

    /// <summary>While dragging: what is being dragged, and for the sky where the drag began.</summary>
    private bool _dragging;
    private double _dragStartY;
    private double _dragStartSun;

    public RainbowDiagram()
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

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.05);
        _lastSeconds = now;
        _reserve.Step(IsLessonActive, dt);

        if (IsPlaying && dt > 0)
        {
            _time += dt;
            if (IsSweeping && !_dragging) Sweep(dt);
        }

        InvalidateVisual();
    }

    /// <summary>Moves the view's main value on, turning back at the ends of its range.</summary>
    private void Sweep(double dt)
    {
        var (property, low, high, rate) = Mode switch
        {
            RainbowMode.Drop => (ImpactProperty, 0.0, 0.995, 0.11),
            RainbowMode.Sky => (SunAltitudeProperty, 0.0, 58.0, 4.0),
            _ when Scene == RefractionScene.Prism => (AngleProperty, 25.0, 89.0, 7.0),
            _ => (AngleProperty, 0.0, 89.0, 9.0),
        };

        var value = GetValue(property) + (_sweepDirection * rate * dt);
        if (value >= high)
        {
            value = high;
            _sweepDirection = -1;
        }
        else if (value <= low)
        {
            value = low;
            _sweepDirection = 1;
        }

        SetCurrentValue(property, value);
    }

    #endregion

    #region layout

    /// <summary>The room for the picture: below the title, above the toolbar and, while a walkthrough runs, its subtitles.</summary>
    private Rect Stage()
    {
        var top = Math.Min(TitleSpace, ActualHeight * 0.26);
        var bottom = Math.Max(ToolbarSpace, ToolbarHeight + 18 + 16) + _reserve.Current;
        return new Rect(24, top, Math.Max(100, ActualWidth - 48), Math.Max(120, ActualHeight - top - bottom));
    }

    #endregion

    #region dragging

    private void DragTo(Point point)
    {
        var stage = Stage();
        switch (Mode)
        {
            case RainbowMode.Drop:
            {
                var (center, radius) = DropLayout(stage);
                SetCurrentValue(ImpactProperty, Math.Round(Math.Clamp(Math.Abs(point.Y - center.Y) / radius, 0, 0.995), 3));
                break;
            }

            case RainbowMode.Sky:
            {
                // Pulling the bow down raises the Sun.
                var scale = SkyLayout(stage).Scale;
                SetCurrentValue(SunAltitudeProperty, Math.Round(Math.Clamp(_dragStartSun + ((point.Y - _dragStartY) / scale), 0, 60), 1));
                break;
            }

            default:
                SetCurrentValue(AngleProperty, Math.Round(AimAngle(stage, point), 1));
                break;
        }
    }

    #endregion

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        _targets.Clear();
        var stage = Stage();
        switch (Mode)
        {
            case RainbowMode.Drop:
                DrawDrop(dc, stage, width, height);
                break;

            case RainbowMode.Sky:
                DrawSky(dc, stage, width, height);
                break;

            default:
                DrawRefraction(dc, stage, width, height);
                break;
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, Now, width);
    }

    #region drawing helpers

    /// <summary>A part of a light path, for the pulses running along it: its ends, how fast light goes there, and how much is left.</summary>
    private readonly record struct Leg(Point From, Point To, double Speed, double Power);

    /// <summary>Pixels per second light pulses move in air (slower by the index inside).</summary>
    private const double PulseSpeed = 170;

    /// <summary>Seconds between pulses sent along a path.</summary>
    private const double PulseInterval = 0.42;

    /// <summary>Bright pulses of light running along a path, slower where light is slower, dimmer where less is left.</summary>
    private void DrawPulses(PaintContext dc, IReadOnlyList<Leg> legs, Color color, double radius = 3.2, double delay = 0)
    {
        if (legs.Count == 0) return;
        var durations = legs.Select(l => ((Vector)(l.To - l.From)).Length / (PulseSpeed * l.Speed)).ToArray();
        var total = durations.Sum();
        if (total <= 0) return;

        var glow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(255, 255, 255, 255), 0),
            (WithAlpha(color, 0.85), 0.35),
            (WithAlpha(color, 0), 1));
        // A path that starts later (a reflected part) carries on the pulses of the part before it.
        var phase = (_time - delay) % PulseInterval;
        if (phase < 0) phase += PulseInterval;
        for (var start = phase; start < total; start += PulseInterval)
        {
            var left = start;
            for (var i = 0; i < legs.Count; i++)
            {
                if (left > durations[i])
                {
                    left -= durations[i];
                    continue;
                }

                var leg = legs[i];
                var at = leg.From + ((leg.To - leg.From) * (durations[i] <= 0 ? 0 : left / durations[i]));
                var opacity = Math.Clamp(Math.Sqrt(leg.Power), 0.12, 1);
                dc.PushOpacity(opacity);
                dc.DrawEllipse(glow, null, at, radius * 2.4, radius * 2.4);
                dc.Pop();
                break;
            }
        }
    }

    /// <summary>A beam of light: a soft glow and a bright core, as bright as the share of light in it.</summary>
    private static void DrawRay(PaintContext dc, Point from, Point to, Color color, double power, double thickness = 2.2)
    {
        var alpha = Math.Clamp(0.15 + (0.85 * Math.Sqrt(Math.Max(0, power))), 0, 1);
        dc.DrawLine(MakePen(new SolidColorBrush(WithAlpha(color, alpha * 0.22)), thickness * 4), from, to);
        dc.DrawLine(MakePen(new SolidColorBrush(WithAlpha(color, alpha)), thickness), from, to);
    }

    /// <summary>
    /// An arc round <paramref name="center"/> from one direction to another (the shorter way), with a label; the label is
    /// a target named <paramref name="id"/> if given.
    /// </summary>
    private void DrawAngle(PaintContext dc, Point center, Vector from, Vector to, double radius, string label, Color color, string? id = null)
    {
        from = from.Normalize();
        to = to.Normalize();
        var sweep = SpaceDrawing.AngleBetween(from, to);
        if (Math.Abs(sweep) < 0.3) return;
        var start = center + (from * radius);
        var end = center + (to * radius);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, false);
            context.ArcTo(end, new Size(radius, radius), 0, Math.Abs(sweep) > 180, sweep > 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);

            context.EndFigure(false);
        }
        dc.DrawGeometry(null, MakePen(new SolidColorBrush(WithAlpha(color, 0.9)), 1.4), geometry);

        var middle = from + to;
        if (middle.Length < 1e-6) middle = new Vector(-from.Y, from.X);
        middle = middle.Normalize();
        var text = _labels.Text(label, 12, color, true);
        var at = center + (middle * (radius + 10 + (text.Width / 2)));
        var topLeft = new Point(at.X - (text.Width / 2), at.Y - (text.Height / 2));
        dc.DrawText(text, topLeft);
        if (id is not null) AddTextTarget(id, text, topLeft);
    }

    /// <summary>
    /// Already localized text on a dark pill, centred on <paramref name="center"/> and kept inside <paramref name="bounds"/>;
    /// a target named <paramref name="id"/> if given.
    /// </summary>
    private void DrawPill(PaintContext dc, Rect bounds, string text, Point center, Color color, double size = 11.5, string? id = null)
    {
        var formatted = _labels.Text(text, size, color);
        var x = Math.Clamp(center.X - (formatted.Width / 2) - 6, bounds.Left + 2, Math.Max(bounds.Left + 2, bounds.Right - formatted.Width - 14));
        var y = Math.Clamp(center.Y - (formatted.Height / 2) - 2, bounds.Top + 2, Math.Max(bounds.Top + 2, bounds.Bottom - formatted.Height - 4));
        _labels.DrawTextPill(dc, text, new Point(x, y), size, color);
        if (id is not null) AddPillTarget(id, formatted, new Point(x, y));
    }

    /// <summary>The colour of light of one wavelength, or white for white light.</summary>
    internal static Color LightColor(double nm) => double.IsNaN(nm) ? Colors.White : SkyOptics.SpectralColour(nm);

    /// <summary>The wavelengths drawn for the page's light: the seven of the fan for white, else the one colour.</summary>
    private double[] Wavelengths => Colour == LightColour.White ? RainbowOptics.Fan : [RainbowOptics.Wavelength(Colour)];

    internal static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static string Loc(string key) => LocalizationService.Instance.GetString(key);

    private static string LocFormat(string key, params object?[] args) => LocalizationService.Instance.Format(key, args);

    private static Vector Rotate(Vector v, double degrees)
    {
        var c = Math.Cos(degrees * Deg);
        var s = Math.Sin(degrees * Deg);
        return new Vector((v.X * c) - (v.Y * s), (v.X * s) + (v.Y * c));
    }

    #endregion

    private static class Ink
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Dim = Color.FromRgb(0x9A, 0xA5, 0xB5);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
        public static readonly Color Cyan = Color.FromRgb(0x6F, 0xD8, 0xFF);
        public static readonly Color Green = Color.FromRgb(0x8B, 0xE0, 0x8F);
        public static readonly Color Red = Color.FromRgb(0xFF, 0x7A, 0x6B);
        public static readonly Color Violet = Color.FromRgb(0xB3, 0x9D, 0xFF);

        public static readonly IBrush AirBack = Vertical(Color.FromRgb(0x07, 0x0B, 0x14), Color.FromRgb(0x10, 0x18, 0x28));
        public static readonly IPen Normal = MakePen(Solid(150, 0xC9, 0xD1, 0xDC), 1.2, dashed: true);
        public static readonly IPen Guide = MakePen(Solid(110, 255, 255, 255), 1, dashed: true);
        public static readonly IPen Surface = MakePen(Solid(200, 0xC9, 0xE6, 0xFF), 1.6);
        public static readonly IBrush LampBody = Vertical(Color.FromRgb(0x5A, 0x63, 0x72), Color.FromRgb(0x2A, 0x2F, 0x38));
        public static readonly IPen LampEdge = MakePen(Solid(200, 0x9A, 0xA5, 0xB5), 1);
        public static readonly IBrush Screen = Solid(255, 0x1A, 0x1E, 0x26);
        public static readonly IPen ScreenEdge = MakePen(Solid(160, 0xC9, 0xD1, 0xDC), 1);

        public static IBrush Vertical(Color top, Color bottom) => SpaceDrawing.LinearGradient(top, bottom, 90);
    }
}
