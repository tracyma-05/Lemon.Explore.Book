using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;

namespace Lemon.Explore.Book.Chapters.SolarSystem;

/// <summary>
/// Animated top-down view of the Solar System: real planet positions from Kepler orbits, ray-cast planets lit by
/// the Sun, a star field, the asteroid belt, orbit trails, mouse-wheel zoom, drag to pan and click to focus a body.
/// Moons circle their planet once zoomed in (always for the selected planet) and can be clicked as well.
/// </summary>
/// <remarks>
/// Two layouts, blended smoothly: evenly spaced rings (like the iPhone astronomy wallpaper, every planet visible)
/// and true distances in AU. Angles are always true. Body sizes are never to scale.
/// The animation runs on an animation frame per screen refresh only while the control is on screen and visible.
/// </remarks>
public sealed class SolarSystemCanvas : Control
{
    private const double WorldRadius = 1000;
    private const double InnerRing = 160;
    private const double RingSpacing = 118;
    private const double NeptuneAu = 30.07;
    private const double StylizedOuter = InnerRing + (7 * RingSpacing);
    private const double WorldPerAu = StylizedOuter / NeptuneAu;
    private const double J2000 = 2451545.0;
    private const double MinZoom = 0.4;
    private const double MaxZoom = 90;
    private const int TrailSegments = 22;

    /// <summary>Fastest a moon is drawn to go round, in turns per real second, so Io does not strobe at "1 year / s".</summary>
    private const double MaxMoonTurnsPerSecond = 0.4;

    #region properties

    public static readonly StyledProperty<IEnumerable<CelestialBody>?> BodiesProperty =
        AvaloniaProperty.Register<SolarSystemCanvas, IEnumerable<CelestialBody>?>(nameof(Bodies));

    public static readonly StyledProperty<CelestialBody?> SelectedBodyProperty =
        AvaloniaProperty.Register<SolarSystemCanvas, CelestialBody?>(nameof(SelectedBody), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<DateTime> SimulationTimeProperty =
        AvaloniaProperty.Register<SolarSystemCanvas, DateTime>(nameof(SimulationTime), DateTime.UtcNow, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> DaysPerSecondProperty =
        AvaloniaProperty.Register<SolarSystemCanvas, double>(nameof(DaysPerSecond), 7.0);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<SolarSystemCanvas, bool>(nameof(IsPlaying), true);

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<SolarSystemCanvas, bool>(nameof(ShowLabels), true);

    public static readonly StyledProperty<bool> ShowOrbitsProperty =
        AvaloniaProperty.Register<SolarSystemCanvas, bool>(nameof(ShowOrbits), true);

    public static readonly StyledProperty<bool> IsRealScaleProperty =
        AvaloniaProperty.Register<SolarSystemCanvas, bool>(nameof(IsRealScale));

    /// <summary>How far left of centre (DIPs) a focused body is placed, to clear a panel over the right side.</summary>
    public static readonly StyledProperty<double> FocusOffsetProperty =
        AvaloniaProperty.Register<SolarSystemCanvas, double>(nameof(FocusOffset));

    /// <summary>What the walkthrough points at: a body's <see cref="CelestialBody.Id"/>, or <c>belt</c> for the asteroid belt.</summary>
    public static readonly StyledProperty<string?> HighlightProperty =
        AvaloniaProperty.Register<SolarSystemCanvas, string?>(nameof(Highlight));

    /// <summary>A walkthrough is running: the view moves up to leave room for its subtitles (<see cref="LessonReserve"/>).</summary>
    public static readonly StyledProperty<bool> IsLessonActiveProperty =
        AvaloniaProperty.Register<SolarSystemCanvas, bool>(nameof(IsLessonActive));

    /// <summary>Run when the viewer starts dragging or clicks a body, before anything changes (pauses a walkthrough).</summary>
    public static readonly StyledProperty<ICommand?> InteractionCommandProperty =
        AvaloniaProperty.Register<SolarSystemCanvas, ICommand?>(nameof(InteractionCommand));

    /// <summary>
    /// Run with the body when the body already selected is clicked again (to explain it again); a click on another
    /// body changes <see cref="SelectedBody"/> instead.
    /// </summary>
    public static readonly StyledProperty<ICommand?> ReselectedCommandProperty =
        AvaloniaProperty.Register<SolarSystemCanvas, ICommand?>(nameof(ReselectedCommand));

    public IEnumerable<CelestialBody>? Bodies
    {
        get => GetValue(BodiesProperty);
        set => SetValue(BodiesProperty, value);
    }

    public CelestialBody? SelectedBody
    {
        get => GetValue(SelectedBodyProperty);
        set => SetValue(SelectedBodyProperty, value);
    }

    public DateTime SimulationTime
    {
        get => GetValue(SimulationTimeProperty);
        set => SetValue(SimulationTimeProperty, value);
    }

    /// <summary>Simulated days per real second; negative runs time backwards.</summary>
    public double DaysPerSecond
    {
        get => GetValue(DaysPerSecondProperty);
        set => SetValue(DaysPerSecondProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public bool ShowOrbits
    {
        get => GetValue(ShowOrbitsProperty);
        set => SetValue(ShowOrbitsProperty, value);
    }

    public bool IsRealScale
    {
        get => GetValue(IsRealScaleProperty);
        set => SetValue(IsRealScaleProperty, value);
    }

    public double FocusOffset
    {
        get => GetValue(FocusOffsetProperty);
        set => SetValue(FocusOffsetProperty, value);
    }

    public string? Highlight
    {
        get => GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    public bool IsLessonActive
    {
        get => GetValue(IsLessonActiveProperty);
        set => SetValue(IsLessonActiveProperty, value);
    }

    public ICommand? InteractionCommand
    {
        get => GetValue(InteractionCommandProperty);
        set => SetValue(InteractionCommandProperty, value);
    }

    public ICommand? ReselectedCommand
    {
        get => GetValue(ReselectedCommandProperty);
        set => SetValue(ReselectedCommandProperty, value);
    }

    #endregion

    private readonly Stopwatch _clock = new();
    private readonly Dictionary<CelestialBody, PlanetRenderer> _renderers = [];
    private readonly Dictionary<CelestialBody, BodyFrame> _frames = [];
    private readonly Dictionary<CelestialBody, FormattedText> _labels = [];
    private readonly Dictionary<CelestialBody, IPen[]> _trailPens = [];
    private readonly StarBackdrop _starfield = new();
    private readonly Asteroid[] _asteroids = CreateAsteroids();
    private readonly Random _random = new(7);
    private readonly LessonReserve _reserve = new();
    private List<CelestialBody> _bodies = [];

    private double _julianDay = KeplerOrbit.JulianDay(DateTime.UtcNow);
    private bool _pushingTime;
    private double _lastSeconds;
    private double _scaleBlend;

    // Camera: centre in world units (y up) and zoom; the targets are approached smoothly every frame.
    private Point _center;
    private double _zoom = 1;
    private Point _targetCenter;
    private double _targetZoom = 1;
    private Point? _zoomPivot;
    private CelestialBody? _follow;

    private Point _mouseDown;
    private Point _lastMouse;
    private bool _dragging;
    private CelestialBody? _hovered;
    private bool _captured;

    private readonly Dictionary<CelestialBody, double> _moonAngles = [];
    private readonly Dictionary<CelestialBody, double> _moonVisibility = [];
    private bool _resyncMoons = true;

    private bool _attached;
    private Meteor? _meteor;
    private double _nextMeteorAt = 6;

    public SolarSystemCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    /// <summary>Back to the whole system: zoom 1, centred on the Sun, no body followed.</summary>
    public void ResetView()
    {
        _follow = null;
        _zoomPivot = null;
        _targetZoom = 1;
        _targetCenter = default;
    }

    #region lifecycle

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _clock.Start();
        _lastSeconds = _clock.Elapsed.TotalSeconds;
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        RequestFrame();

        // Queue the textures of the Sun and the planets first; moons are queued when they first appear.
        foreach (var body in _bodies)
        {
            _ = PlanetTexture.TryGet(body);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        LocalizationService.Instance.PropertyChanged -= OnLanguageChanged;
        _clock.Stop();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BodiesProperty)
        {
            OnBodiesChanged();
        }
        else if (change.Property == SelectedBodyProperty)
        {
            OnSelectedBodyChanged(change.GetNewValue<CelestialBody?>());
        }
        else if (change.Property == SimulationTimeProperty)
        {
            OnSimulationTimeChanged(change.GetNewValue<DateTime>());
        }
        else if (change.Property == IsRealScaleProperty)
        {
            RefocusAfterLayoutChange();
        }
        else if (change.Property == BoundsProperty)
        {
            _starfield.OnSizeChanged(_clock.Elapsed.TotalSeconds);
        }
    }

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

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => _labels.Clear();

    private void OnBodiesChanged()
    {
        _bodies = Bodies?.ToList() ?? [];
        _renderers.Clear();
        _frames.Clear();
        _labels.Clear();
        _trailPens.Clear();
        _moonAngles.Clear();
        _moonVisibility.Clear();
        _resyncMoons = true;
        foreach (var body in _bodies.SelectMany(b => b.Satellites.Prepend(b)))
        {
            _renderers[body] = new PlanetRenderer(body);
        }
    }

    private void OnSelectedBodyChanged(CelestialBody? body)
    {
        if (body is null)
        {
            _follow = null;
            return;
        }

        // A moon moves too fast to chase with the camera: follow its planet, the moon gets the selection ring.
        _follow = body.Parent ?? body;
        _zoomPivot = null;
        _targetZoom = FocusZoom(_follow);
    }

    private void RefocusAfterLayoutChange()
    {
        if (_follow is not null)
        {
            _targetZoom = FocusZoom(_follow, IsRealScale ? 1 : 0);
        }
        else
        {
            ResetView();
        }
    }

    private void OnSimulationTimeChanged(DateTime value)
    {
        if (_pushingTime) return;
        _julianDay = KeplerOrbit.JulianDay(value);
        _resyncMoons = true;
    }


    #endregion

    #region frame update

    private void OnRendering()
    {
        if (!IsEffectivelyVisible || ActualWidth < 1 || ActualHeight < 1) return;

        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastSeconds, 0, 0.1);
        _lastSeconds = now;
        _reserve.Step(IsLessonActive, dt);

        if (IsPlaying && dt > 0)
        {
            // Keep to a range where DateTime and the orbital elements both stay meaningful.
            _julianDay = Math.Clamp(_julianDay + (dt * DaysPerSecond), 2305447.5, 2597640.5);
            _pushingTime = true;
            try
            {
                SetCurrentValue(SimulationTimeProperty, DateTime.UnixEpoch.AddDays(_julianDay - 2440587.5));
            }
            finally
            {
                _pushingTime = false;
            }
        }

        var blendTarget = IsRealScale ? 1.0 : 0.0;
        _scaleBlend += (blendTarget - _scaleBlend) * (1 - Math.Exp(-dt * 4));
        if (Math.Abs(blendTarget - _scaleBlend) < 0.001) _scaleBlend = blendTarget;

        UpdateBodyPositions();
        UpdateMoons(dt);
        UpdateCamera(dt);
        UpdateMeteor(now);
        InvalidateVisual();
    }

    private void UpdateBodyPositions()
    {
        foreach (var body in _bodies)
        {
            if (!_frames.TryGetValue(body, out var frame))
            {
                frame = new BodyFrame();
                _frames[body] = frame;
            }

            if (body.Orbit is null)
            {
                frame.World = default;
                continue;
            }

            frame.Orbit = body.Orbit.At(_julianDay);
            frame.Anomaly = frame.Orbit.EccentricAnomaly;
            frame.World = OrbitPoint(body, frame.Orbit, frame.Anomaly);
        }
    }

    /// <summary>
    /// Moves each moon towards its true position for the simulated date. When playback would spin a moon faster than
    /// <see cref="MaxMoonTurnsPerSecond"/>, it keeps turning at that rate instead and catches up once playback slows.
    /// </summary>
    private void UpdateMoons(double dt)
    {
        var cap = MaxMoonTurnsPerSecond * 2 * Math.PI;
        var selectedSystem = SelectedBody?.Parent ?? SelectedBody;
        foreach (var planet in _bodies)
        {
            var target = planet == selectedSystem ? 1 : Noise.SmoothStep(1.3, 1.9, _zoom);
            var visibility = _moonVisibility.GetValueOrDefault(planet);
            _moonVisibility[planet] = visibility + ((target - visibility) * (1 - Math.Exp(-dt * 5)));

            foreach (var moon in planet.Satellites)
            {
                var orbit = moon.MoonOrbit!;
                var trueAngle = orbit.LongitudeAt(_julianDay);
                if (_resyncMoons || !_moonAngles.TryGetValue(moon, out var angle))
                {
                    _moonAngles[moon] = trueAngle;
                    continue;
                }

                var rate = IsPlaying ? DaysPerSecond * 2 * Math.PI / orbit.PeriodDays * (orbit.Retrograde ? -1 : 1) : 0;
                if (Math.Abs(rate) <= cap)
                {
                    angle += rate * dt;
                    angle += KeplerOrbit.NormalizeAngle(trueAngle - angle) * (1 - Math.Exp(-dt * 3));
                }
                else
                {
                    angle += Math.Sign(rate) * cap * dt;
                }

                _moonAngles[moon] = KeplerOrbit.NormalizeAngle(angle);
            }
        }

        _resyncMoons = false;
    }

    private void UpdateCamera(double dt)
    {
        var k = 1 - Math.Exp(-dt * 5);
        var oldScale = PixelsPerWorld(_zoom);
        var newZoom = Math.Exp(Math.Log(_zoom) + ((Math.Log(_targetZoom) - Math.Log(_zoom)) * k));
        if (Math.Abs(newZoom - _targetZoom) / _targetZoom < 0.0005) newZoom = _targetZoom;

        if (_follow is not null && _frames.TryGetValue(_follow, out var frame))
        {
            var scale = PixelsPerWorld(newZoom);
            _targetCenter = new Point(frame.World.X + (FocusOffset / scale), frame.World.Y);
            _center += (_targetCenter - _center) * Math.Min(1, k * 1.4);
        }
        else if (_zoomPivot is { } pivot && newZoom != _zoom)
        {
            // Keep the world point under the mouse where it is while the zoom animates.
            var world = ScreenToWorld(pivot, _center, oldScale);
            var newScale = PixelsPerWorld(newZoom);
            _center = new Point(
                world.X - ((pivot.X - (ActualWidth / 2)) / newScale),
                world.Y + ((pivot.Y - (ViewHeight / 2)) / newScale));
            _targetCenter = _center;
        }
        else
        {
            _center += (_targetCenter - _center) * k;
        }

        _zoom = newZoom;
    }

    private void UpdateMeteor(double now)
    {
        if (_meteor is { } meteor && now - meteor.Start > meteor.Duration)
        {
            _meteor = null;
        }

        if (_meteor is null && now > _nextMeteorAt)
        {
            var angle = (0.55 + (_random.NextDouble() * 0.5)) * Math.PI;
            _meteor = new Meteor(
                new Point(_random.NextDouble() * ActualWidth, _random.NextDouble() * ActualHeight * 0.6),
                new Vector(Math.Cos(angle), Math.Abs(Math.Sin(angle))),
                now,
                0.7 + (_random.NextDouble() * 0.5));
            _nextMeteorAt = now + 7 + (_random.NextDouble() * 12);
        }
    }

    #endregion

    #region geometry

    private double ViewRadiusPixels => Math.Min(ActualWidth, ViewHeight) / 2 * 0.92;

    /// <summary>The height the view is centred in: less the walkthrough's subtitles while one runs.</summary>
    private double ViewHeight => Math.Max(100, ActualHeight - _reserve.Current);

    private double PixelsPerWorld(double zoom) => ViewRadiusPixels / WorldRadius * zoom;

    private double BodyScale
    {
        get
        {
            var sizeFactor = Math.Clamp(Math.Min(ActualWidth, ActualHeight) / 780, 0.6, 1.8);
            var exponent = 0.6 - (0.25 * _scaleBlend);
            return sizeFactor * Math.Pow(_zoom, exponent) * (1 - (0.42 * _scaleBlend));
        }
    }

    private Point WorldToScreen(Point world, double scale) =>
        new((ActualWidth / 2) + ((world.X - _center.X) * scale), (ViewHeight / 2) - ((world.Y - _center.Y) * scale));

    private Point ScreenToWorld(Point screen, Point center, double scale) =>
        new(center.X + ((screen.X - (ActualWidth / 2)) / scale), center.Y - ((screen.Y - (ViewHeight / 2)) / scale));

    /// <summary>A point on a body's orbit: true direction, distance blended between the ring layout and AU.</summary>
    private Point OrbitPoint(CelestialBody body, OrbitState orbit, double eccentricAnomaly)
    {
        var (x, y) = orbit.PositionAt(eccentricAnomaly);
        var real = new Point(x * WorldPerAu, y * WorldPerAu);
        if (_scaleBlend >= 1) return real;

        var ring = InnerRing + (body.OrbitIndex * RingSpacing);
        var angle = Math.Atan2(y, x);
        var stylized = new Point(ring * Math.Cos(angle), ring * Math.Sin(angle));
        return stylized + ((real - stylized) * _scaleBlend);
    }

    private double FocusZoom(CelestialBody body, double? blend = null)
    {
        if (body.Orbit is null) return 2.5;

        var b = blend ?? _scaleBlend;
        var stylized = InnerRing + (body.OrbitIndex * RingSpacing);
        var real = body.Orbit.SemiMajorAxis * WorldPerAu;
        var radius = stylized + ((real - stylized) * b);
        return Math.Clamp(780 / radius, 2.4, 70);
    }

    #endregion

    #region rendering

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        var dpi = RenderScale();
        var now = _clock.Elapsed.TotalSeconds;

        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
        DrawStarfield(dc, width, height, dpi, now);

        var scale = PixelsPerWorld(_zoom);
        var bodyScale = BodyScale;
        var sunScreen = WorldToScreen(default, scale);

        // Faintly lit orbital plane, like the wallpaper's glow around the Sun.
        // Neptune's ring and its true orbit (30 AU) have the same radius, so this fits both layouts.
        var discRadius = StylizedOuter * scale * 1.04;
        dc.DrawEllipse(Paint.PlaneGlow, null, sunScreen, discRadius, discRadius);

        if (ShowOrbits)
        {
            foreach (var body in _bodies)
            {
                if (body.Orbit is not null && _frames.TryGetValue(body, out var frame))
                {
                    DrawOrbit(dc, body, frame, scale);
                }
            }
        }

        DrawAsteroids(dc, scale);

        foreach (var body in _bodies)
        {
            if (!_frames.TryGetValue(body, out var frame)) continue;

            var screen = WorldToScreen(frame.World, scale);
            var radius = body.DisplayRadius * bodyScale;
            frame.Screen = screen;
            frame.Radius = radius;

            if (body.IsStar)
            {
                DrawSunGlow(dc, screen, radius, now);
            }

            DrawBody(dc, body, frame, sunScreen, dpi, now);
        }

        var visibleMoons = DrawMoons(dc, bodyScale, sunScreen, dpi, now);

        if (ShowLabels)
        {
            // Bodies crowd together when zoomed out in true scale: the selected and hovered body, then the Sun, then
            // the planets from the outside in, then the moons get their label, and any label that would overlap one
            // already placed is skipped.
            var placed = new List<Rect>();
            var order = _bodies
                .Concat(visibleMoons.Where(m => _frames[m].Visibility > 0.6))
                .OrderByDescending(b => b == SelectedBody)
                .ThenByDescending(b => b == _hovered)
                .ThenBy(b => b.IsMoon)
                .ThenByDescending(b => b.IsStar)
                .ThenByDescending(b => b.OrbitIndex);
            foreach (var body in order)
            {
                if (_frames.TryGetValue(body, out var frame)) DrawLabel(dc, body, frame, dpi, placed);
            }
        }

        DrawHighlight(dc, scale, sunScreen, now);
    }

    /// <summary>The walkthrough's pointer: a pulsing ring round a body, or a glow along the asteroid belt.</summary>
    private void DrawHighlight(PaintContext dc, double scale, Point sunScreen, double now)
    {
        if (Highlight is not { } id) return;
        if (id == "belt")
        {
            var stylized = InnerRing + (3.5 * RingSpacing);
            var radius = stylized + (((2.7 * WorldPerAu) - stylized) * _scaleBlend);
            var width = Math.Clamp(0.3 * RingSpacing * scale * (1 - (0.4 * _scaleBlend)), 6, 24);
            dc.DrawEllipse(null, ExploreTargets.GlowPen(now, width), sunScreen, radius * scale, radius * scale);
            return;
        }

        foreach (var (body, frame) in _frames)
        {
            if (body.Id != id || frame.Visibility < 0.5) continue;
            ExploreTargets.DrawHighlight(dc, new ExploreTarget(id, frame.Screen, Math.Max(frame.Radius, 6)), now);
            return;
        }
    }

    /// <summary>Moons around their planet, offset in screen space by planet radii. Returns the moons drawn.</summary>
    private List<CelestialBody> DrawMoons(PaintContext dc, double bodyScale, Point sunScreen, double dpi, double now)
    {
        var drawn = new List<CelestialBody>();
        foreach (var planet in _bodies)
        {
            var visibility = _moonVisibility.GetValueOrDefault(planet);
            if (planet.Satellites.Count == 0 || !_frames.TryGetValue(planet, out var planetFrame)) continue;

            foreach (var moon in planet.Satellites)
            {
                if (!_frames.TryGetValue(moon, out var frame))
                {
                    frame = new BodyFrame();
                    _frames[moon] = frame;
                }

                var angle = _moonAngles.GetValueOrDefault(moon);
                var distance = planetFrame.Radius * moon.MoonOrbit!.DisplayDistance;
                frame.Screen = planetFrame.Screen + new Vector(Math.Cos(angle) * distance, -Math.Sin(angle) * distance);
                frame.Radius = moon.DisplayRadius * bodyScale;
                frame.Visibility = visibility;
            }

            if (visibility < 0.02) continue;

            dc.PushOpacity(visibility);
            if (ShowOrbits)
            {
                foreach (var moon in planet.Satellites)
                {
                    var distance = planetFrame.Radius * moon.MoonOrbit!.DisplayDistance;
                    var pen = moon == SelectedBody || moon == _hovered ? Paint.OrbitHighlightPen(moon) : Paint.MoonOrbitPen;
                    dc.DrawEllipse(null, pen, planetFrame.Screen, distance, distance);
                }
            }

            foreach (var moon in planet.Satellites)
            {
                var frame = _frames[moon];
                if (frame.Radius < 0.6) continue;
                DrawBody(dc, moon, frame, sunScreen, dpi, now);
                drawn.Add(moon);
            }

            dc.Pop();
        }

        return drawn;
    }

    private void DrawOrbit(PaintContext dc, CelestialBody body, BodyFrame frame, double scale)
    {
        const int samples = 220;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i <= samples; i++)
            {
                var point = WorldToScreen(OrbitPoint(body, frame.Orbit, i * 2 * Math.PI / samples), scale);
                if (i == 0) ctx.BeginFigure(point, false);
                else ctx.LineTo(point);
            }

            ctx.EndFigure(true);
        }
        var highlighted = body == SelectedBody || body == _hovered;
        dc.DrawGeometry(null, highlighted ? Paint.OrbitHighlightPen(body) : Paint.OrbitPen, geometry);

        // A fading trail behind the planet, the article's "orbit trace" kept as a short comet tail.
        if (!_trailPens.TryGetValue(body, out var pens))
        {
            pens = Paint.TrailPens(body, TrailSegments);
            _trailPens[body] = pens;
        }

        const double trailLength = 0.85;
        var direction = DaysPerSecond >= 0 ? -1 : 1;
        var previous = WorldToScreen(frame.World, scale);
        for (var i = 1; i <= TrailSegments; i++)
        {
            var point = WorldToScreen(OrbitPoint(body, frame.Orbit, frame.Anomaly + (direction * trailLength * i / TrailSegments)), scale);
            dc.DrawLine(pens[i - 1], previous, point);
            previous = point;
        }
    }

    private void DrawAsteroids(PaintContext dc, double scale)
    {
        var marsRing = InnerRing + (3 * RingSpacing);
        var size = Math.Clamp(0.9 * Math.Pow(_zoom, 0.3), 0.8, 2.2);
        var groups = new StreamGeometry[3];
        var contexts = new StreamGeometryContext[3];
        for (var g = 0; g < 3; g++)
        {
            groups[g] = new StreamGeometry();
            contexts[g] = groups[g].Open();
        }

        var width = ActualWidth;
        var height = ActualHeight;
        foreach (var asteroid in _asteroids)
        {
            var angle = asteroid.Phase + (2 * Math.PI * (_julianDay - J2000) / (365.25 * asteroid.Period));
            var stylized = marsRing + ((0.28 + (0.44 * asteroid.Spread)) * RingSpacing);
            var radius = stylized + (((asteroid.Au * WorldPerAu) - stylized) * _scaleBlend);
            var p = WorldToScreen(new Point(radius * Math.Cos(angle), radius * Math.Sin(angle)), scale);
            if (p.X < -4 || p.Y < -4 || p.X > width + 4 || p.Y > height + 4) continue;

            var s = size * asteroid.Size;
            var ctx = contexts[asteroid.Brightness];
            ctx.BeginFigure(new Point(p.X - s, p.Y - s), true);
            ctx.LineTo(new Point(p.X + s, p.Y - s));
            ctx.LineTo(new Point(p.X + s, p.Y + s));
            ctx.LineTo(new Point(p.X - s, p.Y + s));
            ctx.EndFigure(true);
        }

        for (var g = 0; g < 3; g++)
        {
            contexts[g].Dispose();
            dc.DrawGeometry(Paint.AsteroidBrushes[g], null, groups[g]);
        }
    }

    private static void DrawSunGlow(PaintContext dc, Point center, double radius, double now)
    {
        var pulse = 1 + (0.03 * Math.Sin(now * 0.9));
        var halo = radius * 6.5 * pulse;
        dc.DrawEllipse(Paint.SunHalo, null, center, halo, halo);
        var corona = radius * 1.75 * (1 + (0.02 * Math.Sin((now * 1.7) + 1)));
        dc.DrawEllipse(Paint.SunCorona, null, center, corona, corona);
    }

    private void DrawBody(PaintContext dc, CelestialBody body, BodyFrame frame, Point sunScreen, double dpi, double now)
    {
        var renderer = _renderers[body];
        var extentDip = frame.Radius * renderer.Extent;
        var screen = frame.Screen;
        if (screen.X + extentDip < 0 || screen.Y + extentDip < 0 || screen.X - extentDip > ActualWidth || screen.Y - extentDip > ActualHeight)
        {
            return;
        }

        // Light comes from the Sun, in the plane of the screen and slightly from the viewer's side.
        LightVector light;
        if (body.IsStar)
        {
            light = new LightVector(0, 0, 1);
        }
        else
        {
            Vector toSun = sunScreen - screen;
            toSun = toSun.Length < 1e-6 ? new Vector(-1, 0) : toSun / toSun.Length;
            light = LightVector.Normalize(toSun.X * 0.94, -toSun.Y * 0.94, 0.32);
        }

        var size = renderer.SizeFor(frame.Radius * dpi);
        renderer.Render(size, light, SpinAngle(body, now));

        var edge = size / dpi;
        dc.DrawImage(renderer.Bitmap, new Rect(screen.X - (edge / 2), screen.Y - (edge / 2), edge, edge));

        var selected = body == SelectedBody;
        if (selected || body == _hovered)
        {
            var ring = (frame.Radius * (body.Rings is null ? 1 : 1.12)) + 7 + (selected ? 1.5 * Math.Sin(now * 3) : 0);
            dc.DrawEllipse(null, Paint.SelectionPen(body, selected), screen, ring, ring);
            if (selected)
            {
                var outer = ring + 5 + (3 * ((now * 0.8) % 1));
                dc.PushOpacity(1 - ((now * 0.8) % 1));
                dc.DrawEllipse(null, Paint.SelectionPen(body, false), screen, outer, outer);
                dc.Pop();
            }
        }
    }

    /// <summary>Visual spin, decoupled from the playback speed so fast playback does not turn into a strobe.</summary>
    internal static double SpinAngle(CelestialBody body, double seconds) => body.SpinAngle(seconds);

    private void DrawLabel(PaintContext dc, CelestialBody body, BodyFrame frame, double dpi, List<Rect> placed)
    {
        if (!_labels.TryGetValue(body, out var text))
        {
            var typeface = new Typeface(GetValue(TextElement.FontFamilyProperty), FontStyle.Normal, FontWeight.Medium);
            text = new FormattedText(body.Name, LocalizationService.Instance.CurrentCulture, FlowDirection.LeftToRight,
                typeface, body.IsMoon ? 10.5 : 12, Paint.LabelBrush);
            _labels[body] = text;
        }

        var below = frame.Radius * (body.Rings is null ? 1 : 0.95) + (body.IsStar ? 12 : body.IsMoon ? 3 : 6);
        var origin = new Point(frame.Screen.X - (text.Width / 2), frame.Screen.Y + below);
        if (origin.X > ActualWidth || origin.Y > ActualHeight || origin.X + text.Width < 0 || origin.Y + text.Height < 0) return;

        var bounds = new Rect(origin.X - 2, origin.Y, text.Width + 4, text.Height);
        if (placed.Any(r => r.Intersects(bounds))) return;
        placed.Add(bounds);

        var emphasis = body == SelectedBody || body == _hovered ? 1 : body.IsMoon ? 0.55 : 0.72;
        dc.PushOpacity(emphasis * frame.Visibility);
        dc.DrawText(text, origin);
        dc.Pop();
    }

    private void DrawStarfield(PaintContext dc, double width, double height, double dpi, double now)
    {
        _starfield.Draw(dc, width, height, dpi, now);

        // A shooting star now and then: a bright head with a tail that fades out behind it.
        if (_meteor is { } meteor)
        {
            var t = (now - meteor.Start) / meteor.Duration;
            var head = meteor.Origin + (meteor.Direction * (t * 420));
            const int segments = 8;
            dc.PushOpacity(Math.Sin(Math.PI * t));
            for (var i = 0; i < segments; i++)
            {
                var from = head - (meteor.Direction * (110.0 * (i + 1) / segments));
                var to = head - (meteor.Direction * (110.0 * i / segments));
                dc.DrawLine(Paint.MeteorPens[i], from, to);
            }

            dc.Pop();
        }
    }

    #endregion

    #region pointer and keys

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var factor = Math.Pow(1.2, e.Delta.Y);
        _targetZoom = Math.Clamp(_targetZoom * factor, MinZoom, MaxZoom);
        _zoomPivot = _follow is null ? e.GetPosition(this) : null;
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Focus();
        if (e.ClickCount == 2 && HitTestBody(e.GetPosition(this)) is null)
        {
            ResetView();
            SetCurrentValue(SelectedBodyProperty, null);
            e.Handled = true;
            return;
        }

        _mouseDown = _lastMouse = e.GetPosition(this);
        _dragging = false;
        _captured = true;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);

        if (_captured)
        {
            if (!_dragging && ((Vector)(position - _mouseDown)).Length > 4)
            {
                Interacted();
                _dragging = true;
                _follow = null;
                _zoomPivot = null;
                Cursor = new Cursor(StandardCursorType.SizeAll);
            }

            if (_dragging)
            {
                var scale = PixelsPerWorld(_zoom);
                var delta = position - _lastMouse;
                _center = new Point(_center.X - (delta.X / scale), _center.Y + (delta.Y / scale));
                _targetCenter = _center;
            }

            _lastMouse = position;
            return;
        }

        _hovered = HitTestBody(position);
        Cursor = _hovered is null ? null : new Cursor(StandardCursorType.Hand);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_captured)
        {
            return;
        }

        _captured = false;
        e.Pointer.Capture(null);
        Cursor = null;
        if (!_dragging && HitTestBody(e.GetPosition(this)) is { } body)
        {
            Interacted();
            if (body == SelectedBody)
            {
                OnSelectedBodyChanged(body);
                if (ReselectedCommand is { } again && again.CanExecute(body))
                {
                    again.Execute(body);
                }
            }
            else
            {
                SetCurrentValue(SelectedBodyProperty, body);
            }
        }

        _dragging = false;
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _captured = false;
        _dragging = false;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = null;
    }

    private void Interacted()
    {
        if (InteractionCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Add or Key.OemPlus:
                _targetZoom = Math.Clamp(_targetZoom * 1.25, MinZoom, MaxZoom);
                e.Handled = true;
                break;
            case Key.Subtract or Key.OemMinus:
                _targetZoom = Math.Clamp(_targetZoom / 1.25, MinZoom, MaxZoom);
                e.Handled = true;
                break;
            case Key.Home:
                ResetView();
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// The body under a point: the one whose centre is nearest relative to its size, so a small moon next to its
    /// planet can still be picked. Planets and moons win over the Sun; small bodies get a few pixels of slack.
    /// </summary>
    private CelestialBody? HitTestBody(Point point)
    {
        CelestialBody? best = null;
        var bestDistance = double.MaxValue;
        foreach (var body in _bodies.SelectMany(b => b.Satellites.Prepend(b)))
        {
            if (!_frames.TryGetValue(body, out var frame) || frame.Visibility < 0.5) continue;
            var distance = ((Vector)(point - frame.Screen)).Length;
            var reach = Math.Max(frame.Radius, body.IsMoon ? 4 : 6) + (body.IsMoon ? 5 : 6);
            if (distance > reach) continue;

            var rank = (distance / reach) + (body.IsStar ? 1000 : 0);
            if (rank < bestDistance)
            {
                bestDistance = rank;
                best = body;
            }
        }

        return best;
    }

    #endregion

    #region data

    private sealed class BodyFrame
    {
        public OrbitState Orbit;
        public double Anomaly;
        public Point World;
        public Point Screen;
        public double Radius;

        /// <summary>Moons fade in with zoom; 1 for the Sun and the planets.</summary>
        public double Visibility = 1;
    }

    private readonly record struct Asteroid(double Au, double Period, double Phase, double Spread, double Size, int Brightness);

    private sealed record Meteor(Point Origin, Vector Direction, double Start, double Duration);

    private static Asteroid[] CreateAsteroids()
    {
        var random = new Random(1801);
        var asteroids = new Asteroid[900];
        for (var i = 0; i < asteroids.Length; i++)
        {
            // Main belt, 2.1–3.3 AU, densest around 2.7 AU.
            var spread = Math.Clamp(0.5 + ((random.NextDouble() + random.NextDouble() + random.NextDouble() - 1.5) * 0.55), 0, 1);
            var au = 2.1 + (spread * 1.2);
            asteroids[i] = new Asteroid(
                au,
                Math.Pow(au, 1.5),
                random.NextDouble() * 2 * Math.PI,
                spread,
                0.6 + (random.NextDouble() * 0.8),
                random.Next(10) < 6 ? 0 : random.Next(10) < 7 ? 1 : 2);
        }

        return asteroids;
    }

    /// <summary>Immutable brushes and pens shared by every instance.</summary>
    private static class Paint
    {
        public static readonly IBrush PlaneGlow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(46, 255, 214, 160), 0),
            (Color.FromArgb(22, 220, 180, 140), 0.45),
            (Color.FromArgb(10, 200, 200, 220), 0.9),
            (Color.FromArgb(0, 200, 200, 220), 1));

        public static readonly IBrush SunHalo = SpaceDrawing.RadialGradient(
            (Color.FromArgb(210, 255, 244, 214), 0),
            (Color.FromArgb(120, 255, 210, 120), 0.12),
            (Color.FromArgb(46, 255, 168, 70), 0.32),
            (Color.FromArgb(14, 255, 140, 60), 0.6),
            (Color.FromArgb(0, 255, 140, 60), 1));

        public static readonly IBrush SunCorona = SpaceDrawing.RadialGradient(
            (Color.FromArgb(255, 255, 252, 240), 0.45),
            (Color.FromArgb(150, 255, 236, 180), 0.6),
            (Color.FromArgb(0, 255, 210, 140), 1));

        public static readonly IBrush LabelBrush = SpaceDrawing.Solid(Color.FromRgb(0xEE, 0xF1, 0xF6));

        public static readonly IPen OrbitPen = Pen(Color.FromArgb(52, 255, 255, 255), 1);

        public static readonly IPen MoonOrbitPen = Pen(Color.FromArgb(34, 255, 255, 255), 0.8);

        public static readonly IBrush[] AsteroidBrushes =
        [
            SpaceDrawing.Solid(70, 200, 190, 175),
            SpaceDrawing.Solid(120, 215, 205, 190),
            SpaceDrawing.Solid(190, 230, 222, 210),
        ];

        /// <summary>The shooting star's tail, brightest at the head.</summary>
        public static readonly IPen[] MeteorPens = Enumerable.Range(0, 8)
            .Select(i => Pen(Color.FromArgb((byte)(220 * (1 - (i / 8.0))), 255, 255, 255), 1.4))
            .ToArray();

        private static readonly Dictionary<(CelestialBody, bool), IPen> SelectionPens = [];
        private static readonly Dictionary<CelestialBody, IPen> HighlightPens = [];

        public static IPen SelectionPen(CelestialBody body, bool selected)
        {
            if (!SelectionPens.TryGetValue((body, selected), out var pen))
            {
                var c = body.AccentColor;
                pen = Pen(Color.FromArgb(selected ? (byte)230 : (byte)140, c.R, c.G, c.B), selected ? 1.6 : 1.2);
                SelectionPens[(body, selected)] = pen;
            }

            return pen;
        }

        public static IPen OrbitHighlightPen(CelestialBody body)
        {
            if (!HighlightPens.TryGetValue(body, out var pen))
            {
                var c = body.AccentColor;
                pen = Pen(Color.FromArgb(150, c.R, c.G, c.B), 1.3);
                HighlightPens[body] = pen;
            }

            return pen;
        }

        public static IPen[] TrailPens(CelestialBody body, int count)
        {
            var c = body.AccentColor;
            var pens = new IPen[count];
            for (var i = 0; i < count; i++)
            {
                var fade = 1 - (i / (double)count);
                var alpha = (byte)(200 * fade * fade);
                pens[i] = Pen(Color.FromArgb(alpha, c.R, c.G, c.B), 1 + (1.4 * fade));
            }

            return pens;
        }

        private static IPen Pen(Color color, double thickness) => SpaceDrawing.MakePen(SpaceDrawing.Solid(color), thickness);
    }

    #endregion
}
