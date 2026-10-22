using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Eclipses;

/// <summary>
/// The animated diagram of the eclipse page. Solar and lunar mode: the orbital plane seen from the north with the Sun
/// on the left, Earth on the right and the Moon going round it, each casting its umbra and penumbra. Seasons mode:
/// Earth's year round the Sun in perspective, with the Moon's tilted orbit and its line of nodes.
/// </summary>
/// <remarks>
/// The diagram owns the animation clock (an animation frame per screen refresh while it is on screen) and pushes
/// <see cref="MoonAngle"/> / <see cref="SeasonDay"/> back to their bindings, which the other eclipse views follow.
/// Playback slows down through an eclipse (<see cref="EclipseGeometry.PlaybackRate"/>). Clicking and dragging are in
/// <c>EclipseDiagram.Objects.cs</c>.
/// </remarks>
public sealed partial class EclipseDiagram : Control
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MoonAngleProperty)
        {
            OnMoonAngleChanged(change.GetNewValue<double>());
        }
        else if (change.Property == SeasonDayProperty)
        {
            OnSeasonDayChanged(change.GetNewValue<double>());
        }
    }

    private double ActualWidth => Bounds.Width;

    private double ActualHeight => Bounds.Height;

    private double RenderScale() => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    // Solar / lunar view: the part of the orbital plane shown, in diagram units (Earth at 0, the Sun at −750).
    private const double ViewLeft = -792;
    private const double ViewRight = 336;
    private const double ViewHalfHeight = 258;

    // Seasons view: world units, the Sun at 0.
    private const double SeasonOrbit = 330;
    private const double SeasonMoonOrbit = 76;
    private const double SeasonTilt = 24 * Math.PI / 180;
    private const double SeasonElevation = 28 * Math.PI / 180;
    private const double SeasonHalfWidth = 440;
    private const double SeasonHalfHeight = 236;

    #region dependency properties

    public static readonly StyledProperty<EclipseMode> ModeProperty =
        AvaloniaProperty.Register<EclipseDiagram, EclipseMode>(nameof(Mode), EclipseMode.Solar);

    /// <summary>The Moon's position along its orbit in degrees: 0 = new moon, 180 = full moon.</summary>
    public static readonly StyledProperty<double> MoonAngleProperty =
        AvaloniaProperty.Register<EclipseDiagram, double>(nameof(MoonAngle), 0.0, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Days since the start of the seasons view (see <see cref="EclipseSeasons"/>).</summary>
    public static readonly StyledProperty<double> SeasonDayProperty =
        AvaloniaProperty.Register<EclipseDiagram, double>(nameof(SeasonDay), 0.0, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<EclipseDiagram, bool>(nameof(IsPlaying), true, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsMoonNearProperty =
        AvaloniaProperty.Register<EclipseDiagram, bool>(nameof(IsMoonNear), true);

    /// <summary>Solar mode: the observer north (+) or south (−) of the orbital plane, −1…1.</summary>
    public static readonly StyledProperty<double> ObserverOffsetProperty =
        AvaloniaProperty.Register<EclipseDiagram, double>(nameof(ObserverOffset), 0.0);

    /// <summary>Lunar mode: the Moon's height above the orbital plane (see <see cref="EclipseGeometry.LunarPathHeight"/>).</summary>
    public static readonly StyledProperty<double> LunarHeightProperty =
        AvaloniaProperty.Register<EclipseDiagram, double>(nameof(LunarHeight), 0.0);

    public EclipseMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double MoonAngle
    {
        get => GetValue(MoonAngleProperty);
        set => SetValue(MoonAngleProperty, value);
    }

    public double SeasonDay
    {
        get => GetValue(SeasonDayProperty);
        set => SetValue(SeasonDayProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public bool IsMoonNear
    {
        get => GetValue(IsMoonNearProperty);
        set => SetValue(IsMoonNearProperty, value);
    }

    public double ObserverOffset
    {
        get => GetValue(ObserverOffsetProperty);
        set => SetValue(ObserverOffsetProperty, value);
    }

    public double LunarHeight
    {
        get => GetValue(LunarHeightProperty);
        set => SetValue(LunarHeightProperty, value);
    }

    #endregion

    private readonly Stopwatch _clock = new();
    private readonly StarBackdrop _stars = new();
    private readonly LabelCache _labels;
    private readonly CelestialBody _sunBody = Body("Sun");
    private readonly CelestialBody _earthBody = Body("Earth");
    private readonly PlanetRenderer _sun;
    private readonly PlanetRenderer _earth;
    private readonly PlanetRenderer _moon = new(Body("Moon"));

    private double _lastSeconds;
    private bool _pushing;
    private double _angle;
    private double _day;

    // Solar / lunar view transform: screen point of the world origin and DIPs per diagram unit.
    private Point _origin;
    private double _scale = 1;

    public EclipseDiagram()
    {
        _sun = new PlanetRenderer(_sunBody);
        _earth = new PlanetRenderer(_earthBody);
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

    private void OnMoonAngleChanged(double value)
    {
        if (!_pushing) _angle = value;
    }

    private void OnSeasonDayChanged(double value)
    {
        if (!_pushing) _day = value;
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
            _pushing = true;
            try
            {
                if (Mode == EclipseMode.Seasons)
                {
                    _day += dt * EclipseSeasons.PlaybackRate(_day);
                    SetCurrentValue(SeasonDayProperty, _day);
                }
                else
                {
                    _angle += dt * EclipseGeometry.PlaybackRate(Mode, _angle);
                    _angle -= 360 * Math.Floor(_angle / 360);
                    SetCurrentValue(MoonAngleProperty, _angle);
                }
            }
            finally
            {
                _pushing = false;
            }
        }

        InvalidateVisual();
    }

    #endregion

    public override void Render(DrawingContext context)
    {
        using var dc = new PaintContext(context);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        var dpi = RenderScale();
        var now = _clock.Elapsed.TotalSeconds;
        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
        _stars.Draw(dc, width, height, dpi, now, 0.8);
        _targets.Clear();

        if (Mode == EclipseMode.Seasons)
        {
            DrawSeasons(dc, dpi, now);
        }
        else
        {
            DrawPlane(dc, dpi, now);
        }

        _targets.DrawOverlays(dc, _labels, SelectedObject, Highlight, ObjectTitleKey, IsLineHighlight, now, width);
    }

    /// <summary>The height the picture is fitted into: less the walkthrough's subtitles while one runs.</summary>
    private double PictureHeight => Math.Max(120, ActualHeight - _reserve.Current);

    #region solar and lunar view

    private Point S(Vector world) => new(_origin.X + (world.X * _scale), _origin.Y - (world.Y * _scale));

    private Point S(double x, double y) => S(new Vector(x, y));

    private void DrawPlane(PaintContext dc, double dpi, double now)
    {
        _scale = Math.Min(ActualWidth / (ViewRight - ViewLeft), PictureHeight / (2 * ViewHalfHeight));
        _origin = new Point((ActualWidth / 2) - ((ViewLeft + ViewRight) / 2 * _scale), PictureHeight / 2);

        var solar = Mode == EclipseMode.Solar;
        var radians = _angle * Math.PI / 180;
        var orbit = solar ? EclipseGeometry.MoonOrbitRadius(IsMoonNear) : EclipseGeometry.MoonOrbitNear;
        var moon3 = EclipseGeometry.MoonPosition(radians, orbit, solar ? 0 : LunarHeight);
        var moon = new Vector(moon3.X, moon3.Y);
        var sun = new Vector(-EclipseGeometry.SunDistance, 0);
        var sunScreen = S(sun);
        var earthScreen = S(0, 0);
        var moonScreen = S(moon);

        // Sunlight fills the plane, so the shadows show as darker cones.
        dc.DrawRectangle(Paint.SunlightWash, null, new Rect(0, 0, ActualWidth, ActualHeight));
        var glow = EclipseGeometry.SunRadius * _scale * 3.4;
        dc.DrawEllipse(Paint.SunGlow, null, sunScreen, glow, glow);

        // Earth's shadow, behind Earth.
        DrawShadow(dc, default, EclipseGeometry.EarthRadius, solar ? 0.45 : 1, !solar);

        if (Glows("orbit")) dc.DrawEllipse(null, ExploreTargets.GlowPen(now), earthScreen, orbit * _scale, orbit * _scale);
        DrawMoonOrbit(dc, orbit);

        DrawSphere(dc, _sun, sunScreen, EclipseGeometry.SunRadius * _scale, new LightVector(0, 0, 1), dpi,
            _sunBody.SpinAngle(now));
        DrawSphere(dc, _earth, earthScreen, EclipseGeometry.EarthRadius * _scale, LightFrom(earthScreen, sunScreen, 0.12), dpi,
            _earthBody.SpinAngle(now) * 0.5);

        // The Moon's shadow lands on Earth's day side; what would lie behind Earth is cut away.
        var away = moon - sun;
        away /= away.Length;
        var side = new Vector(-away.Y, away.X) * EclipseGeometry.EarthRadius;
        var behindEarth = Polygon(S(side), S(side + (away * 2000)), S(-side + (away * 2000)), S(-side));
        dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)), behindEarth));
        DrawShadow(dc, moon, EclipseGeometry.MoonRadius, solar ? 1 : 0.35, solar);
        dc.Pop();

        DrawSphere(dc, _moon, moonScreen, EclipseGeometry.MoonRadius * _scale, LightFrom(moonScreen, sunScreen, 0.12), dpi);
        DrawMoonInEarthShadow(dc, radians, moonScreen);

        DrawBodyLabels(dc, sunScreen, earthScreen, moonScreen);
        _targets.Add("sun", sunScreen, EclipseGeometry.SunRadius * _scale);
        _targets.Add("earth", earthScreen, EclipseGeometry.EarthRadius * _scale);
        _targets.Add("moon", moonScreen, Math.Max(8, EclipseGeometry.MoonRadius * _scale));
        if (solar)
        {
            DrawMoonShadowLabels(dc, moon, sun);
            DrawObserver(dc);
        }
        else
        {
            DrawEarthShadowLabels(dc);
        }
    }

    /// <summary>
    /// Penumbra, umbra and (past the umbra's tip) antumbra of a sphere at <paramref name="occluder"/>; when emphasized,
    /// also the rays from the Sun's edges that bound them.
    /// </summary>
    private void DrawShadow(PaintContext dc, Vector occluder, double radius, double opacity, bool emphasized)
    {
        const double far = 1700;
        var sun = new Vector(-EclipseGeometry.SunDistance, 0);
        var axis = occluder - sun;
        var cone = new ShadowCone(radius, axis.Length);
        var a = axis / axis.Length;
        var n = new Vector(-a.Y, a.X);

        var top = occluder + (n * radius);
        var bottom = occluder - (n * radius);
        var farCentre = occluder + (a * far);
        var penumbra = cone.Penumbra(far);
        var tip = occluder + (a * cone.UmbraLength);

        dc.PushOpacity(opacity);
        dc.DrawGeometry(Paint.Penumbra, null, Polygon(S(top), S(farCentre + (n * penumbra)), S(farCentre - (n * penumbra)), S(bottom)));
        dc.DrawGeometry(Paint.Umbra, null, Polygon(S(top), S(tip), S(bottom)));
        if (cone.UmbraLength < far)
        {
            var spread = -cone.Umbra(far);
            dc.DrawGeometry(Paint.Antumbra, null, Polygon(S(tip), S(farCentre + (n * spread)), S(farCentre - (n * spread))));
        }

        if (emphasized)
        {
            // Rays grazing the Sun's edge and the sphere's: the umbra's edges cross at its tip, the penumbra's spread out.
            var sunTop = sun + (n * EclipseGeometry.SunRadius);
            var sunBottom = sun - (n * EclipseGeometry.SunRadius);
            dc.DrawLine(Paint.UmbraRay, S(sunTop), S(farCentre + (n * cone.Umbra(far))));
            dc.DrawLine(Paint.UmbraRay, S(sunBottom), S(farCentre - (n * cone.Umbra(far))));
            dc.DrawLine(Paint.PenumbraRay, S(sunBottom), S(farCentre + (n * penumbra)));
            dc.DrawLine(Paint.PenumbraRay, S(sunTop), S(farCentre - (n * penumbra)));
        }

        dc.Pop();
    }

    private void DrawMoonOrbit(PaintContext dc, double orbit)
    {
        dc.DrawEllipse(null, Paint.OrbitPen, S(0, 0), orbit * _scale, orbit * _scale);

        // The Moon goes round counter-clockwise: at the top of its orbit it heads towards the Sun.
        DrawArrowHead(dc, Paint.OrbitArrow, S(-12, orbit), new Vector(-1, 0), 9);

        foreach (var (x, key) in new[] { (-orbit, "Eclipse_Label_NewMoon"), (orbit, "Eclipse_Label_FullMoon") })
        {
            dc.DrawLine(Paint.OrbitTick, S(x * 0.94, 0), S(x * 1.06, 0));
            var text = _labels.Get(key, 11, Paint.DimText);
            var anchor = S(x, EclipseGeometry.MoonRadius + 30);
            var topLeft = new Point(anchor.X - (text.Width / 2), anchor.Y - text.Height);
            dc.DrawText(text, topLeft);
            AddTextTarget(key == "Eclipse_Label_NewMoon" ? "newmoon" : "fullmoon", text, topLeft);
        }
    }

    /// <summary>A Moon inside Earth's shadow dims, then turns copper red in the umbra.</summary>
    private void DrawMoonInEarthShadow(PaintContext dc, double radians, Point moonScreen)
    {
        var lunar = EclipseGeometry.Lunar(radians, Mode == EclipseMode.Lunar ? LunarHeight : 0);
        if (!lunar.BehindEarth || lunar.Light >= 1) return;

        var radius = (EclipseGeometry.MoonRadius * _scale) + 0.5;
        dc.PushOpacity(0.6 * (1 - lunar.Light) * (1 - lunar.UmbraCoverage));
        dc.DrawEllipse(Brushes.Black, null, moonScreen, radius, radius);
        dc.Pop();
        dc.PushOpacity(0.85 * lunar.UmbraCoverage);
        dc.DrawEllipse(Paint.Copper, null, moonScreen, radius, radius);
        dc.Pop();
    }

    private void DrawBodyLabels(PaintContext dc, Point sun, Point earth, Point moon)
    {
        _labels.DrawCentered(dc, "SolarSystem_Sun_Name", new Point(sun.X, sun.Y + (EclipseGeometry.SunRadius * _scale) + 8), 13, Paint.Text, true);
        _labels.DrawCentered(dc, "SolarSystem_Earth_Name", new Point(earth.X, earth.Y + (EclipseGeometry.EarthRadius * _scale) + 6), 13, Paint.Text, true);
        _labels.DrawCentered(dc, "SolarSystem_Moon_Name", new Point(moon.X, moon.Y + (EclipseGeometry.MoonRadius * _scale) + 4), 12, Paint.Text, true);
    }

    /// <summary>Callouts for the Moon's umbra, penumbra and antumbra while its shadow is on its way to Earth.</summary>
    private void DrawMoonShadowLabels(PaintContext dc, Vector moon, Vector sun)
    {
        if (Math.Abs(EclipseGeometry.NormalizeDegrees(_angle)) > 40) return;

        var a = moon - sun;
        var cone = new ShadowCone(EclipseGeometry.MoonRadius, a.Length);
        a /= a.Length;
        var n = new Vector(-a.Y, a.X);
        var toSurface = moon.Length - EclipseGeometry.EarthRadius;

        var umbraEnd = Math.Min(cone.UmbraLength, toSurface);
        var umbraAt = moon + (a * (umbraEnd * 0.5));
        Callout(dc, "Eclipse_Label_Umbra", "umbra", umbraAt, umbraAt - (n * 64));

        var penumbraX = toSurface * 0.55;
        var penumbraAt = moon + (a * penumbraX) + (n * ((cone.Umbra(penumbraX) + cone.Penumbra(penumbraX)) / 2));
        Callout(dc, "Eclipse_Label_Penumbra", "penumbra", penumbraAt, moon + (a * penumbraX) + (n * 70));

        if (cone.UmbraLength < toSurface)
        {
            var antumbraAt = moon + (a * ((cone.UmbraLength + toSurface) / 2));
            Callout(dc, "Eclipse_Label_Antumbra", "antumbra", antumbraAt, antumbraAt - (n * 96) - (a * 10));
        }
    }

    private void DrawEarthShadowLabels(PaintContext dc)
    {
        var cone = EclipseGeometry.EarthShadow;
        const double x = 105;
        var umbra = _labels.Get("Eclipse_Label_Umbra", 12, Paint.Text, true);
        var at = S(x, 0);
        var umbraAt = new Point(at.X - (umbra.Width / 2), at.Y - (umbra.Height / 2));
        dc.DrawText(umbra, umbraAt);
        AddTextTarget("umbra", umbra, umbraAt);

        var penumbra = _labels.Get("Eclipse_Label_Penumbra", 12, Paint.Text, true);
        at = S(x, (cone.Umbra(x) + cone.Penumbra(x)) / 2);
        var penumbraAt = new Point(at.X - (penumbra.Width / 2), at.Y - (penumbra.Height / 2));
        dc.DrawText(penumbra, penumbraAt);
        AddTextTarget("penumbra", penumbra, penumbraAt);
    }

    /// <summary>A label at <paramref name="labelAt"/> with a thin line to the point it names.</summary>
    private void Callout(PaintContext dc, string key, string id, Vector target, Vector labelAt)
    {
        var text = _labels.Get(key, 12, Paint.Text, true);
        var label = S(labelAt);
        var point = S(target);
        Vector towards = point - label;
        var gap = (text.Height / 2) + 3;
        if (towards.Length > gap)
        {
            dc.DrawLine(Paint.CalloutPen, label + (towards / towards.Length * gap), point);
        }

        dc.DrawEllipse(Paint.CalloutDot, null, point, 2, 2);
        var topLeft = new Point(label.X - (text.Width / 2), label.Y - (text.Height / 2));
        dc.DrawText(text, topLeft);
        AddTextTarget(id, text, topLeft);
    }

    /// <summary>The observer's spot on Earth's day side (north or south of the plane it sits at the same place here).</summary>
    private void DrawObserver(PaintContext dc)
    {
        var observer = EclipseGeometry.Observer(ObserverOffset);
        var at = S(observer.X, 0);
        dc.DrawEllipse(Paint.ObserverFill, Paint.ObserverPen, at, 4, 4);
        _labels.DrawPill(dc, "Eclipse_Label_Observer", new Point(at.X + 8, at.Y - 24), 11, Paint.Text);
        _targets.Add("observer", at, 5);
        AddPillTarget("observer", "Eclipse_Label_Observer", new Point(at.X + 8, at.Y - 24), 11, Paint.Text);
    }

    #endregion

    #region seasons view

    private Point P(V3 p, Point origin, double scale) =>
        new(origin.X + (p.X * scale), origin.Y - (((p.Y * Math.Sin(SeasonElevation)) + (p.Z * Math.Cos(SeasonElevation))) * scale));

    private static double Depth(V3 p) => (-p.Y * Math.Cos(SeasonElevation)) + (p.Z * Math.Sin(SeasonElevation));

    private static V3 OnOrbit(double longitudeDegrees, double radius)
    {
        var (s, c) = Math.SinCos(longitudeDegrees * Math.PI / 180);
        return new V3(radius * c, radius * s, 0);
    }

    private void DrawSeasons(PaintContext dc, double dpi, double now)
    {
        var scale = Math.Min(ActualWidth / (2 * SeasonHalfWidth), PictureHeight / (2 * SeasonHalfHeight));
        var origin = new Point(ActualWidth / 2, PictureHeight / 2);
        Point Project(V3 p) => P(p, origin, scale);

        var day = _day;
        var sunLongitude = EclipseSeasons.SunLongitude(day);
        var earth = OnOrbit(sunLongitude + 180, SeasonOrbit);
        var inSeason = EclipseSeasons.InSeason(day);
        var sunScreen = Project(default);
        var earthScreen = Project(earth);

        // The ecliptic plane and Earth's orbit.
        var plane = SeasonOrbit * 1.22 * scale;
        dc.DrawEllipse(Paint.EclipticPlane, null, sunScreen, plane, plane * Math.Sin(SeasonElevation));
        dc.DrawEllipse(null, Paint.OrbitPen, sunScreen, SeasonOrbit * scale, SeasonOrbit * scale * Math.Sin(SeasonElevation));
        // Inside the orbit, front left: out of the way of Earth and its Moon wherever they are.
        var planeLabel = Project(OnOrbit(232, SeasonOrbit * 0.55));
        _labels.DrawCentered(dc, "Eclipse_Label_Ecliptic", planeLabel, 11, Paint.DimText);
        var eclipticText = _labels.Get("Eclipse_Label_Ecliptic", 11, Paint.DimText);
        AddTextTarget("ecliptic", eclipticText, new Point(planeLabel.X - (eclipticText.Width / 2), planeLabel.Y));

        // Eclipse seasons: where Earth is when the line of nodes points at the Sun.
        var halfWidth = EclipseSeasons.SeasonHalfWidth;
        foreach (var centre in new[] { EclipseSeasons.NodeLongitude, EclipseSeasons.NodeLongitude + 180 })
        {
            var arc = new StreamGeometry();
            using (var ctx = arc.Open())
            {
                for (var i = 0; i <= 24; i++)
                {
                    var point = Project(OnOrbit(centre - halfWidth + (2 * halfWidth * i / 24), SeasonOrbit));
                    if (i == 0) ctx.BeginFigure(point, false);
                    else ctx.LineTo(point);
                }

                ctx.EndFigure(false);
            }
            if (Glows("season")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 14), arc);
            dc.DrawGeometry(null, Paint.SeasonArc, arc);
            // The label makes way while Earth (and its busy surroundings) pass along the arc.
            var gap = Math.Abs(EclipseGeometry.NormalizeDegrees(sunLongitude + 180 - centre));
            var visibility = Noise.SmoothStep(halfWidth + 4, halfWidth + 30, gap);
            if (visibility > 0.01)
            {
                var label = Project(OnOrbit(centre, SeasonOrbit + 40));
                var text = _labels.Get("Eclipse_Label_Season", 12, Paint.Gold, true);
                var topLeft = new Point(label.X - (text.Width / 2), label.Y - (text.Height / 2));
                dc.PushOpacity(visibility);
                dc.DrawText(text, topLeft);
                dc.Pop();
                if (visibility > 0.5) AddTextTarget("season", text, topLeft);
            }
        }

        // Eclipses of the past year, where Earth was at the time.
        foreach (var e in EclipseSeasons.Events(day - EclipseSeasons.Year, day))
        {
            if (e.Kind == SeasonEventKind.None) continue;
            var age = day - e.Day;
            var at = Project(OnOrbit(EclipseSeasons.SunLongitude(e.Day) + 180, SeasonOrbit));
            dc.PushOpacity(1 - (0.6 * age / EclipseSeasons.Year));
            dc.DrawEllipse(Paint.EventBrush(e.Kind), Paint.EventPen, at, 4.5, 4.5);
            dc.Pop();
        }

        // Sun, and the line from it to Earth (gold while it lies along the line of nodes).
        dc.DrawLine(inSeason ? Paint.SunLineInSeason : Paint.SunLine, sunScreen, earthScreen);
        var sunGlow = 26 * scale * 3.2;
        dc.DrawEllipse(Paint.SunGlow, null, sunScreen, sunGlow, sunGlow);
        DrawSphere(dc, _sun, sunScreen, 26 * scale, new LightVector(0, 0, 1), dpi, _sunBody.SpinAngle(now));
        _labels.DrawCentered(dc, "SolarSystem_Sun_Name", new Point(sunScreen.X, sunScreen.Y + (26 * scale) + 6), 13, Paint.Text, true);
        _targets.Add("sun", sunScreen, 26 * scale);

        // The Moon's tilted orbit round Earth, its line of nodes and the Moon itself.
        var nodeRadians = EclipseSeasons.NodeLongitude * Math.PI / 180;
        var nodeAxis = new V3(Math.Cos(nodeRadians), Math.Sin(nodeRadians), 0);
        var inPlane = new V3(-Math.Sin(nodeRadians) * Math.Cos(SeasonTilt), Math.Cos(nodeRadians) * Math.Cos(SeasonTilt), Math.Sin(SeasonTilt));
        V3 Ring(double u) => earth + (((nodeAxis * Math.Cos(u)) + (inPlane * Math.Sin(u))) * SeasonMoonOrbit);

        var u = (EclipseSeasons.MoonLongitude(day) - EclipseSeasons.NodeLongitude) * Math.PI / 180;
        var moon = Ring(u);
        var earthDepth = Depth(earth);

        if (Glows("moonorbit"))
        {
            var glowRing = new StreamGeometry();
            using (var ctx = glowRing.Open())
            {
                ctx.BeginFigure(Project(Ring(0)), false);
                for (var i = 1; i < 72; i++) ctx.LineTo(Project(Ring(i * Math.PI / 36)));

                ctx.EndFigure(true);
            }
            dc.DrawGeometry(null, ExploreTargets.GlowPen(now), glowRing);
        }

        DrawRing(dc, Ring, Project, earthDepth, behind: true);

        var nodeReach = SeasonMoonOrbit + 22;
        if (Glows("nodeline")) dc.DrawLine(ExploreTargets.GlowPen(now), Project(earth - (nodeAxis * nodeReach)), Project(earth + (nodeAxis * nodeReach)));
        dc.DrawLine(inSeason ? Paint.NodeLineInSeason : Paint.NodeLine, Project(earth - (nodeAxis * nodeReach)), Project(earth + (nodeAxis * nodeReach)));
        foreach (var sign in new[] { -1, 1 })
        {
            var node = Project(earth + (nodeAxis * SeasonMoonOrbit * sign));
            dc.DrawGeometry(inSeason ? Paint.GoldBrush : Paint.NodeBrush, null,
                Polygon(new Point(node.X, node.Y - 4), new Point(node.X + 4, node.Y), new Point(node.X, node.Y + 4), new Point(node.X - 4, node.Y)));
        }

        // Label the lower end of the line of nodes; the orbit's own label goes above the ring.
        var endA = Project(earth + (nodeAxis * nodeReach));
        var endB = Project(earth - (nodeAxis * nodeReach));
        var lowEnd = endA.Y > endB.Y ? endA : endB;
        _labels.DrawPill(dc, "Eclipse_Label_NodeLine", new Point(lowEnd.X + 6, lowEnd.Y - 8), 11, inSeason ? Paint.Gold : Paint.DimText);
        AddPillTarget("nodeline", "Eclipse_Label_NodeLine", new Point(lowEnd.X + 6, lowEnd.Y - 8), 11, inSeason ? Paint.Gold : Paint.DimText);

        // The Moon's drop line to the ecliptic shows how far above or below it is.
        var moonScreen = Project(moon);
        var foot = Project(new V3(moon.X, moon.Y, 0));
        dc.DrawLine(moon.Z >= 0 ? Paint.DropAbove : Paint.DropBelow, moonScreen, foot);
        dc.DrawEllipse(moon.Z >= 0 ? Paint.AboveBrush : Paint.BelowBrush, null, foot, 1.8, 1.8);

        var moonInFront = Depth(moon) > earthDepth;
        if (!moonInFront) DrawSphere(dc, _moon, moonScreen, 5.5 * scale, LightFrom(moonScreen, sunScreen), dpi);
        DrawSphere(dc, _earth, earthScreen, 12 * scale, LightFrom(earthScreen, sunScreen), dpi, _earthBody.SpinAngle(now) * 0.5);
        DrawRing(dc, Ring, Project, earthDepth, behind: false);
        if (moonInFront) DrawSphere(dc, _moon, moonScreen, 5.5 * scale, LightFrom(moonScreen, sunScreen), dpi);

        var ringTop = Enumerable.Range(0, 36).Select(i => Project(Ring(i * Math.PI / 18))).MinBy(p => p.Y);
        var ringLabel = _labels.Get("Eclipse_Label_MoonOrbit", 11, Paint.DimText);
        var ringLabelAt = new Point(ringTop.X - (ringLabel.Width / 2), ringTop.Y - ringLabel.Height - 6);
        dc.DrawText(ringLabel, ringLabelAt);
        AddTextTarget("moonorbit", ringLabel, ringLabelAt);
        var earthLabel = _labels.Get("SolarSystem_Earth_Name", 12, Paint.Text, true);
        dc.DrawText(earthLabel, new Point(earthScreen.X + (12 * scale) + 5, earthScreen.Y - (earthLabel.Height / 2)));
        _targets.Add("earth", earthScreen, 12 * scale);
        _targets.Add("moon", moonScreen, Math.Max(7, 5.5 * scale));

        // An eclipse that just happened.
        foreach (var e in EclipseSeasons.Events(day - 8, day))
        {
            if (e.Kind == SeasonEventKind.None) continue;
            var age = (day - e.Day) / 8;
            var pulse = (12 * scale) + 8 + (24 * age);
            dc.PushOpacity(1 - age);
            dc.DrawEllipse(null, Paint.EventRing(e.Kind), earthScreen, pulse, pulse);
            dc.Pop();
            var key = e.Kind switch
            {
                SeasonEventKind.SolarEclipse => "Eclipse_Label_SolarEclipse",
                SeasonEventKind.LunarEclipse => "Eclipse_Label_LunarEclipse",
                _ => "Eclipse_Label_PenumbralEclipse",
            };
            _labels.DrawPill(dc, key, new Point(earthScreen.X + (SeasonMoonOrbit * scale * 0.6), earthScreen.Y - (SeasonMoonOrbit * scale * 0.9)), 12, Paint.EventColor(e.Kind));
        }
    }

    /// <summary>The part of the Moon's orbit behind (or in front of) Earth: solid above the ecliptic, dashed below.</summary>
    private static void DrawRing(PaintContext dc, Func<double, V3> ring, Func<V3, Point> project, double earthDepth, bool behind)
    {
        const int segments = 120;
        var previous = ring(0);
        for (var i = 1; i <= segments; i++)
        {
            var next = ring(2 * Math.PI * i / segments);
            var mid = (previous + next) * 0.5;
            if ((Depth(mid) < earthDepth) == behind)
            {
                dc.DrawLine(mid.Z >= 0 ? Paint.RingAbove : Paint.RingBelow, project(previous), project(next));
            }

            previous = next;
        }
    }

    #endregion

    /// <summary>Frozen brushes and pens shared by every instance.</summary>
    private static class Paint
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color DimText = Color.FromArgb(0xC0, 0xC9, 0xD1, 0xDC);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);

        public static readonly IBrush SunlightWash = SpaceDrawing.LinearGradient(0,
            (Color.FromArgb(70, 255, 222, 165), 0),
            (Color.FromArgb(44, 255, 230, 190), 0.55),
            (Color.FromArgb(30, 255, 236, 205), 1));

        public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(200, 255, 236, 190), 0.25),
            (Color.FromArgb(70, 255, 200, 120), 0.4),
            (Color.FromArgb(0, 255, 170, 80), 1));

        public static readonly IBrush Penumbra = Solid(120, 2, 4, 12);
        public static readonly IBrush Umbra = Solid(185, 0, 0, 4);
        public static readonly IBrush Antumbra = Solid(95, 40, 20, 0);
        public static readonly IBrush Copper = Solid(255, 122, 40, 18);

        public static readonly IPen UmbraRay = MakePen(Solid(150, 255, 214, 130), 1, dashed: true);
        public static readonly IPen PenumbraRay = MakePen(Solid(95, 255, 214, 130), 1, dashed: true);
        public static readonly IPen OrbitPen = MakePen(Solid(70, 255, 255, 255), 1, dashed: true);
        public static readonly IPen OrbitTick = MakePen(Solid(140, 255, 255, 255), 1.2);
        public static readonly IBrush OrbitArrow = Solid(150, 255, 255, 255);
        public static readonly IPen CalloutPen = MakePen(Solid(130, 255, 255, 255), 0.8);
        public static readonly IBrush CalloutDot = Solid(200, 255, 255, 255);
        public static readonly IBrush ObserverFill = Solid(255, 255, 255, 255);
        public static readonly IPen ObserverPen = MakePen(Solid(255, 20, 26, 40), 1.5);

        public static readonly IBrush EclipticPlane = SpaceDrawing.RadialGradient(
            (Color.FromArgb(46, 140, 170, 230), 0),
            (Color.FromArgb(22, 120, 150, 220), 0.75),
            (Color.FromArgb(0, 120, 150, 220), 1));

        public static readonly IBrush GoldBrush = SpaceDrawing.Solid(Gold);
        public static readonly IPen SeasonArc = MakePen(Solid(110, 0xFF, 0xD3, 0x6B), 7);
        public static readonly IPen SunLine = MakePen(Solid(50, 255, 230, 190), 1, dashed: true);
        public static readonly IPen SunLineInSeason = MakePen(Solid(170, 0xFF, 0xD3, 0x6B), 1.4, dashed: true);
        public static readonly IPen NodeLine = MakePen(Solid(120, 200, 205, 215), 1.1, dashed: true);
        public static readonly IPen NodeLineInSeason = MakePen(Solid(230, 0xFF, 0xD3, 0x6B), 1.6, dashed: true);
        public static readonly IBrush NodeBrush = Solid(200, 200, 205, 215);
        public static readonly IBrush AboveBrush = Solid(255, 0x8E, 0xC5, 0xFF);
        public static readonly IBrush BelowBrush = Solid(255, 0xFF, 0x9F, 0x6B);
        public static readonly IPen RingAbove = MakePen(Solid(220, 0x8E, 0xC5, 0xFF), 1.5);
        public static readonly IPen RingBelow = MakePen(Solid(150, 0xFF, 0x9F, 0x6B), 1.2, dashed: true);
        public static readonly IPen DropAbove = MakePen(AboveBrush, 1);
        public static readonly IPen DropBelow = MakePen(BelowBrush, 1);
        public static readonly IPen EventPen = MakePen(Solid(200, 10, 14, 24), 1);

        private static readonly IBrush SolarEvent = SpaceDrawing.Solid(Gold);
        private static readonly IBrush LunarEvent = Solid(255, 0xE0, 0x70, 0x3C);
        private static readonly IBrush PenumbralEvent = Solid(255, 0xB9, 0xC2, 0xD0);
        private static readonly IPen SolarRing = MakePen(SolarEvent, 2);
        private static readonly IPen LunarRing = MakePen(LunarEvent, 2);
        private static readonly IPen PenumbralRing = MakePen(PenumbralEvent, 2);

        public static IBrush EventBrush(SeasonEventKind kind) => kind switch
        {
            SeasonEventKind.SolarEclipse => SolarEvent,
            SeasonEventKind.LunarEclipse => LunarEvent,
            _ => PenumbralEvent,
        };

        public static IPen EventRing(SeasonEventKind kind) => kind switch
        {
            SeasonEventKind.SolarEclipse => SolarRing,
            SeasonEventKind.LunarEclipse => LunarRing,
            _ => PenumbralRing,
        };

        public static Color EventColor(SeasonEventKind kind) => ((ISolidColorBrush)EventBrush(kind)).Color;
    }
}
