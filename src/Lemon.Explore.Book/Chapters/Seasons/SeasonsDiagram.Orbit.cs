using Avalonia;
using Avalonia.Media;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Seasons;

/// <summary>
/// The orbit view: the ecliptic plane seen from a little above, the Sun in the middle and Earth going round
/// counter-clockwise. World units: x to the right, y away from the viewer, z up (north of the ecliptic). Earth's axis
/// leans towards +x, so Earth is on the left at the June solstice (the north leaning towards the Sun) and on the right
/// at the December solstice. Not to scale: Earth and the Sun are drawn far too large for the orbit.
/// </summary>
public sealed partial class SeasonsDiagram
{
    private const double OrbitRadius = 330;
    private const double EarthRadius = 40;
    private const double GhostRadius = 15;
    private const double SunRadius = 44;
    private const double OrbitHalfWidth = 470;
    private const double OrbitHalfHeight = 250;
    private static readonly double Elevation = 24 * Math.PI / 180;
    private static readonly double SinE = Math.Sin(Elevation);
    private static readonly double CosE = Math.Cos(Elevation);

    private static readonly SeasonEvent[] Events =
        [SeasonEvent.MarchEquinox, SeasonEvent.JuneSolstice, SeasonEvent.SeptemberEquinox, SeasonEvent.DecemberSolstice];

    private readonly PlanetRenderer _sun;
    private PlanetRenderer? _earth;
    private PlanetRenderer[] _ghosts = [];
    private double _rendererTilt = double.NaN;
    private (double Tilt, int Size) _ghostsRendered;

    // View transform: screen point of the Sun and DIPs per world unit.
    private Point _origin;
    private double _scale = 1;

    /// <summary>Earth's labels this frame, laid out before the other labels so they can keep clear.</summary>
    private EarthLabels _earthLabels;

    private Point Screen(V3 w) => new(_origin.X + (w.X * _scale), _origin.Y - (((w.Z * CosE) + (w.Y * SinE)) * _scale));

    /// <summary>World direction in a renderer's view space (x right, y up, z towards the viewer).</summary>
    private static V3 ToView(V3 w) => new(w.X, (w.Z * CosE) + (w.Y * SinE), (w.Z * SinE) - (w.Y * CosE));

    /// <summary>Earth's position on <paramref name="day"/>: a quarter turn ahead of the Sun's longitude, at its true distance.</summary>
    private static V3 EarthAt(double day)
    {
        var (s, c) = Math.SinCos(SeasonsGeometry.ToRadians(SeasonsGeometry.SunLongitude(day) + 90));
        var r = OrbitRadius * SeasonsGeometry.SunDistance(day);
        return new V3(r * c, r * s, 0);
    }

    /// <summary>The day Earth is at the orbit point nearest to <paramref name="point"/> on screen.</summary>
    private double DayAt(Point point)
    {
        var x = (point.X - _origin.X) / _scale;
        var y = -(point.Y - _origin.Y) / (_scale * SinE);
        if (Math.Abs(x) + Math.Abs(y) < 1e-6)
        {
            return _day;
        }

        var theta = SeasonsGeometry.ToDegrees(Math.Atan2(y, x));
        return SeasonsGeometry.DayOfLongitude(theta - 90);
    }

    private static V3 Pole(double tilt)
    {
        var (s, c) = Math.SinCos(SeasonsGeometry.ToRadians(tilt));
        return new V3(s, 0, c);
    }

    /// <summary>Earth's renderers take the pole in view space, so they are made again when the tilt changes.</summary>
    private void EnsureEarthRenderers(double tilt)
    {
        if (_earth is not null && Math.Abs(tilt - _rendererTilt) < 0.01)
        {
            return;
        }

        var pole = ToView(Pole(tilt));
        var light = new LightVector(pole.X, pole.Y, pole.Z);
        _earth = new PlanetRenderer(SpaceBodies.Earth, light);
        _ghosts = Events.Select(_ => new PlanetRenderer(SpaceBodies.Earth, light)).ToArray();
        _rendererTilt = tilt;
        _ghostsRendered = default;
    }

    private void DrawOrbit(DrawingContext dc, double dpi, double now)
    {
        var height = Math.Max(1, ActualHeight - _reserve.Current);
        _scale = Math.Min(ActualWidth / (2 * OrbitHalfWidth), height / (2 * OrbitHalfHeight));
        _origin = new Point(ActualWidth / 2, (height / 2) + (12 * _scale));

        var tilt = Math.Clamp(Tilt, 0, 90);
        EnsureEarthRenderers(tilt);
        var distanceMode = Mode == SeasonsMode.Distance;
        var day = _day;
        var longitude = SeasonsGeometry.SunLongitude(day);
        var earth = EarthAt(day);
        var sunScreen = Screen(default);
        var earthScreen = Screen(earth);

        // The ecliptic plane and Earth's orbit, with an arrow at the front showing the direction of travel.
        var plane = OrbitRadius * 1.25 * _scale;
        dc.DrawEllipse(Paint.EclipticPlane, null, sunScreen, plane, plane * SinE);
        var orbit = OrbitPath();
        if (Highlight == "orbit")
        {
            dc.DrawGeometry(null, ExploreTargets.GlowPen(now), orbit);
        }

        dc.DrawGeometry(null, distanceMode ? Paint.OrbitTruePen : Paint.OrbitPen, orbit);
        var arrowDay = SeasonsGeometry.EventDay(SeasonEvent.SeptemberEquinox) + 40;
        var arrowAt = Screen(EarthAt(arrowDay));
        DrawArrowHead(dc, Paint.OrbitArrow, arrowAt, arrowAt - Screen(EarthAt(arrowDay - 2)), 9);

        _earthLabels = LayoutEarthLabels(earthScreen, tilt);
        foreach (var e in Events)
        {
            DrawEvent(dc, e, longitude, distanceMode, dpi);
        }

        if (distanceMode)
        {
            DrawApsis(dc, SeasonsGeometry.PerihelionDay(), "Seasons_Label_Perihelion", rightSide: true, longitude);
            DrawApsis(dc, SeasonsGeometry.AphelionDay(), "Seasons_Label_Aphelion", rightSide: false, longitude);
        }

        // Sunlight from the Sun to Earth; in distance mode, the distance itself.
        dc.DrawLine(distanceMode ? Paint.DistanceLine : Paint.SunLine, sunScreen, earthScreen);

        // Earth passes behind the Sun on the far side of its orbit.
        var earthBehind = earth.Y > 0;
        if (earthBehind)
        {
            DrawEarth(dc, earth, earthScreen, tilt, dpi, now);
        }

        var glow = SunRadius * _scale * 3.2;
        dc.DrawEllipse(Paint.SunGlow, null, sunScreen, glow, glow);
        DrawSphere(dc, _sun, sunScreen, SunRadius * _scale, new LightVector(0, 0, 1), dpi, SpaceBodies.Sun.SpinAngle(now));
        _targets.Add("sun", sunScreen, SunRadius * _scale);
        _labels.DrawCentered(dc, "SolarSystem_Sun_Name", new Point(sunScreen.X, sunScreen.Y + (SunRadius * _scale) + 6), 13, Paint.Text, true);
        if (!earthBehind)
        {
            DrawEarth(dc, earth, earthScreen, tilt, dpi, now);
        }

        if (distanceMode)
        {
            var km = SeasonsGeometry.SunDistance(day) * SeasonsGeometry.AstronomicalUnitKm / 1e6;
            var text = LocFormat("Seasons_Label_Distance", km);
            var middle = new Point((sunScreen.X + earthScreen.X) / 2, (sunScreen.Y + earthScreen.Y) / 2);
            var label = _labels.Text(text, 11.5, Paint.Text);
            _labels.DrawTextPill(dc, text, new Point(middle.X - (label.Width / 2) - 6, middle.Y - 10), 11.5, Paint.Text);
        }
    }

    private static string EventId(SeasonEvent e) => e switch
    {
        SeasonEvent.MarchEquinox => "march",
        SeasonEvent.JuneSolstice => "june",
        SeasonEvent.SeptemberEquinox => "september",
        _ => "december",
    };

    private StreamGeometry OrbitPath()
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            const int steps = 180;
            for (var i = 0; i < steps; i++)
            {
                var point = Screen(EarthAt(SeasonsGeometry.Year * i / steps));
                if (i == 0)
                {
                    ctx.BeginFigure(point, false);
                }
                else
                {
                    ctx.LineTo(point);
                }
            }

            ctx.EndFigure(true);
        }

        return geometry;
    }

    /// <summary>
    /// An equinox or solstice on the orbit: a tick, its name and date and, in tilt mode, a small Earth with its axis.
    /// They make way while Earth passes.
    /// </summary>
    private void DrawEvent(DrawingContext dc, SeasonEvent e, double longitude, bool distanceMode, double dpi)
    {
        var eventDay = SeasonsGeometry.EventDay(e);
        var position = EarthAt(eventDay);
        var at = Screen(position);
        var gap = Math.Abs(SeasonsGeometry.Normalize180(longitude - SeasonsGeometry.EventLongitude(e)));
        var visibility = Noise.SmoothStep(16, 36, gap);

        // A tick across the orbit, along the line to the Sun.
        Vector toSun = Screen(default) - at;
        toSun = toSun.Length < 1e-6 ? new Vector(0, 1) : toSun / toSun.Length;
        dc.DrawLine(Paint.OrbitTick, at + (toSun * 7), at - (toSun * 7));

        // Clickable while it shows; when Earth is on top of it, a click there is a click on Earth.
        if (visibility > 0.5)
        {
            _targets.Add(EventId(e), at, distanceMode ? 9 : (GhostRadius * _scale) + 3);
        }

        if (visibility < 0.01)
        {
            return;
        }

        using (dc.PushOpacity(visibility))
        {
            if (!distanceMode)
            {
                var ghost = _ghosts[(int)e];
                var radius = GhostRadius * _scale;
                var size = ghost.SizeFor(radius * dpi);
                if (_ghostsRendered != (_rendererTilt, size) || ghost.Bitmap is null)
                {
                    // Rendered once per tilt and size: the small Earths do not spin.
                    foreach (var (other, i) in _ghosts.Select((g, i) => (g, i)))
                    {
                        var p = EarthAt(SeasonsGeometry.EventDay(Events[i]));
                        var s = ToView(new V3(-p.X, -p.Y, 0) * (1 / p.Length));
                        other.Render(size, new LightVector(s.X, s.Y, s.Z), 0);
                    }

                    _ghostsRendered = (_rendererTilt, size);
                }

                using (dc.PushOpacity(0.7))
                {
                    var edge = size / dpi;
                    DrawAxis(dc, at, radius, 1.6, Paint.GhostAxis);
                    dc.DrawImage(ghost.Bitmap!, new Rect(at.X - (edge / 2), at.Y - (edge / 2), edge, edge));
                    DrawAxisTip(dc, at, radius, 1.6, Paint.GhostAxis);
                }
            }
        }

        var name = _labels.Get(EventKey(e), 12, Paint.Text, true);
        var date = _labels.Text(DateText(eventDay), 11, Paint.DimText);
        var reach = ((distanceMode ? 8 : GhostRadius) + 12) * _scale;
        var width = Math.Max(name.Width, date.Width);
        var height = name.Height + date.Height;
        var topLeft = e switch
        {
            SeasonEvent.JuneSolstice => new Point(at.X - reach - width, at.Y - (height / 2)),
            SeasonEvent.DecemberSolstice => new Point(at.X + reach, at.Y - (height / 2)),
            SeasonEvent.MarchEquinox => new Point(at.X - (width / 2), at.Y - (reach * 1.6) - height),
            _ => new Point(at.X - (width / 2), at.Y + (reach * 1.6)),
        };
        topLeft = topLeft.WithX(Math.Clamp(topLeft.X, 8, Math.Max(8, ActualWidth - width - 8)));
        var labelVisibility = visibility * Clearance(new Rect(topLeft, new Size(width, height)));
        if (labelVisibility < 0.01)
        {
            return;
        }

        using (dc.PushOpacity(labelVisibility))
        {
            var alignRight = e == SeasonEvent.JuneSolstice;
            var centered = e is SeasonEvent.MarchEquinox or SeasonEvent.SeptemberEquinox;
            double X(FormattedText t) => alignRight ? topLeft.X + width - t.Width : centered ? topLeft.X + ((width - t.Width) / 2) : topLeft.X;
            dc.DrawText(name, new Point(X(name), topLeft.Y));
            dc.DrawText(date, new Point(X(date), topLeft.Y + name.Height));
        }
    }

    /// <summary>
    /// Distance mode: where Earth is closest to or farthest from the Sun, with the date and distance. Perihelion comes two
    /// weeks after the December solstice, just above its tick, so its label goes above the diamond, clear of the
    /// solstice's; aphelion's goes below, clear of the June solstice's.
    /// </summary>
    private void DrawApsis(DrawingContext dc, double day, string key, bool rightSide, double longitude)
    {
        var at = Screen(EarthAt(day));
        _targets.Add(rightSide ? "perihelion" : "aphelion", at, 9);
        dc.DrawGeometry(Paint.ApsisBrush, null,
            Polygon(new Point(at.X, at.Y - 5), new Point(at.X + 5, at.Y), new Point(at.X, at.Y + 5), new Point(at.X - 5, at.Y)));

        var gap = Math.Abs(SeasonsGeometry.Normalize180(longitude - SeasonsGeometry.SunLongitude(day)));
        var visibility = Noise.SmoothStep(8, 24, gap);
        if (visibility < 0.01)
        {
            return;
        }

        var km = SeasonsGeometry.SunDistance(day) * SeasonsGeometry.AstronomicalUnitKm / 1e6;
        var title = _labels.Get(key, 12, Paint.Apsis, true);
        var detail = _labels.Text(LocFormat("Seasons_Label_ApsisDetail", DateText(day), km), 11, Paint.DimText);
        var width = Math.Max(title.Width, detail.Width);
        var x = Math.Clamp(rightSide ? at.X + (16 * _scale) : at.X - (16 * _scale) - width, 8, Math.Max(8, ActualWidth - width - 8));
        var y = rightSide ? at.Y - (14 * _scale) - title.Height - detail.Height : at.Y + (14 * _scale);
        visibility *= Clearance(new Rect(x, y, width, title.Height + detail.Height));
        if (visibility < 0.01)
        {
            return;
        }

        using (dc.PushOpacity(visibility))
        {
            dc.DrawText(title, new Point(rightSide ? x : x + width - title.Width, y));
            dc.DrawText(detail, new Point(rightSide ? x : x + width - detail.Width, y + title.Height));
        }
    }

    private void DrawEarth(DrawingContext dc, V3 earth, Point center, double tilt, double dpi, double now)
    {
        var radius = EarthRadius * _scale;
        var sun = ToView(new V3(-earth.X, -earth.Y, 0) * (1 / earth.Length));
        var light = new LightVector(sun.X, sun.Y, sun.Z);

        DrawAxis(dc, center, radius, 1.55, Paint.AxisPen);
        DrawSphere(dc, _earth!, center, radius, light, dpi, SpaceBodies.Earth.SpinAngle(now) * 0.5);

        // The equator and the observer's latitude, on the near side of the globe.
        var pole = ToView(Pole(tilt));
        DrawParallel(dc, center, radius, pole, 0, Paint.EquatorPen);
        var latitude = Math.Clamp(Latitude, -90, 90);
        DrawParallel(dc, center, radius, pole, latitude, Paint.LatitudePen);

        // The observer at local noon: on the meridian facing the Sun (hidden when that side faces away from us).
        var towardsSun = sun - (pole * V3.Dot(sun, pole));
        if (towardsSun.Length > 1e-6)
        {
            var (sinLat, cosLat) = Math.SinCos(SeasonsGeometry.ToRadians(latitude));
            var observer = (pole * sinLat) + (towardsSun.Normalized() * cosLat);
            if (observer.Z > 0.05)
            {
                var point = new Point(center.X + (observer.X * radius), center.Y - (observer.Y * radius));
                dc.DrawEllipse(Paint.ObserverFill, Paint.ObserverPen, point, 3.5, 3.5);
            }
        }

        DrawAxisTip(dc, center, radius, 1.55, Paint.AxisPen);
        _targets.Add("earth", center, radius);

        // Past the North Pole the axis keeps pointing at the North Star, wherever Earth is.
        var labels = LayoutEarthLabels(center, tilt);
        dc.DrawLine(Paint.PolarisPen, labels.Tip + (labels.Axis * 4), labels.Star);
        DrawStar(dc, labels.Star, 4.5);
        _targets.Add("axis", labels.Star, 10);
        if (Highlight == "axis")
        {
            dc.DrawLine(ExploreTargets.GlowPen(now), center - (labels.Axis * radius * 1.55), labels.Star);
        }

        dc.DrawText(_labels.Get("Seasons_Label_North", 11, Paint.Text, true), labels.North.TopLeft);
        dc.DrawText(_labels.Get("Seasons_Label_Polaris", 10.5, Paint.DimText), labels.Polaris.TopLeft);

        // Which season each hemisphere has right now.
        _labels.DrawTextPill(dc, labels.Seasons, labels.SeasonsPill.TopLeft, 11.5, Paint.Text);
    }

    /// <summary>
    /// Where Earth's own labels go: "North" by the pole, the North Star's name at the end of the axis, and which season
    /// each hemisphere has in a pill below. <see cref="Rects"/> are where they cover, for other labels to keep clear of.
    /// </summary>
    private readonly record struct EarthLabels(Point Tip, Vector Axis, Point Star, Rect North, Rect Polaris, string Seasons,
        Rect SeasonsPill)
    {
        public IEnumerable<Rect> Rects => [North, Polaris, SeasonsPill];
    }

    private EarthLabels LayoutEarthLabels(Point center, double tilt)
    {
        var radius = EarthRadius * _scale;
        var pole = ToView(Pole(tilt));
        var axis = new Vector(pole.X, -pole.Y);
        var tip = center + (axis * radius * 1.55);
        var star = tip + (axis * 34 * _scale);

        var north = _labels.Get("Seasons_Label_North", 11, Paint.Text, true);
        var polaris = _labels.Get("Seasons_Label_Polaris", 10.5, Paint.DimText);
        var longitude = SeasonsGeometry.SunLongitude(_day);
        var seasons = tilt < 0.5
            ? Loc("Seasons_Label_NoSeasons")
            : LocFormat("Seasons_Label_Hemispheres",
                Loc(SeasonKey(SeasonsGeometry.SeasonAt(longitude, north: true))),
                Loc(SeasonKey(SeasonsGeometry.SeasonAt(longitude, north: false))));
        var text = _labels.Text(seasons, 11.5, Paint.Text);

        return new EarthLabels(tip, axis, star,
            new Rect(tip.X + 6, tip.Y - north.Height + 2, north.Width, north.Height),
            new Rect(star.X - 5, star.Y - (polaris.Height / 2), polaris.Width + 13, polaris.Height),
            seasons,
            new Rect(center.X - (text.Width / 2) - 6, center.Y + (radius * 1.12) + 8, text.Width + 12, text.Height + 4));
    }

    /// <summary>
    /// How much of a label to show beside Earth's own labels: none where they touch, all of it from 18 DIPs clear, so
    /// the equinox, solstice and apsis labels make way as Earth comes past.
    /// </summary>
    private double Clearance(Rect label)
    {
        var nearest = double.PositiveInfinity;
        foreach (var other in _earthLabels.Rects)
        {
            var dx = Math.Max(0, Math.Max(other.Left - label.Right, label.Left - other.Right));
            var dy = Math.Max(0, Math.Max(other.Top - label.Bottom, label.Top - other.Bottom));
            nearest = Math.Min(nearest, Math.Sqrt((dx * dx) + (dy * dy)));
        }

        return Noise.SmoothStep(0, 18, nearest);
    }

    /// <summary>The axis through Earth; drawn behind the globe, the globe covers the middle and the far end.</summary>
    private void DrawAxis(DrawingContext dc, Point center, double radius, double reach, IPen pen)
    {
        var pole = ToView(Pole(Math.Clamp(Tilt, 0, 90)));
        var axis = new Vector(pole.X, -pole.Y) * radius * reach;
        dc.DrawLine(pen, center - axis, center + axis);
    }

    /// <summary>The axis from the North Pole (on the near side) outwards, drawn over the globe.</summary>
    private void DrawAxisTip(DrawingContext dc, Point center, double radius, double reach, IPen pen)
    {
        var pole = ToView(Pole(Math.Clamp(Tilt, 0, 90)));
        var axis = new Vector(pole.X, -pole.Y);
        dc.DrawLine(pen, center + (axis * radius), center + (axis * radius * reach));
    }

    /// <summary>A circle of latitude on the globe, only where it faces the viewer.</summary>
    private static void DrawParallel(DrawingContext dc, Point center, double radius, V3 pole, double latitude, IPen pen)
    {
        var reference = Math.Abs(pole.Z) > 0.99 ? new V3(1, 0, 0) : new V3(0, 0, 1);
        var u = V3.Cross(pole, reference).Normalized();
        var v = V3.Cross(pole, u);
        var (sinLat, cosLat) = Math.SinCos(SeasonsGeometry.ToRadians(latitude));
        var middle = pole * sinLat;

        const int segments = 72;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var open = false;
            for (var i = 0; i <= segments; i++)
            {
                var (s, c) = Math.SinCos(2 * Math.PI * i / segments);
                var p = middle + (((u * c) + (v * s)) * cosLat);
                var point = new Point(center.X + (p.X * radius), center.Y - (p.Y * radius));
                if (p.Z < 0)
                {
                    if (open)
                    {
                        ctx.EndFigure(false);
                    }

                    open = false;
                    continue;
                }

                if (!open)
                {
                    ctx.BeginFigure(point, false);
                    open = true;
                }
                else
                {
                    ctx.LineTo(point);
                }
            }

            if (open)
            {
                ctx.EndFigure(false);
            }
        }

        dc.DrawGeometry(null, pen, geometry);
    }

    private static void DrawStar(DrawingContext dc, Point at, double size)
    {
        var points = new Point[8];
        for (var i = 0; i < 8; i++)
        {
            var (s, c) = Math.SinCos((i * Math.PI / 4) - (Math.PI / 2));
            var r = i % 2 == 0 ? size : size * 0.38;
            points[i] = new Point(at.X + (c * r), at.Y + (s * r));
        }

        dc.DrawGeometry(Paint.StarBrush, null, Polygon(points));
    }

    /// <summary>Immutable brushes and pens shared by every instance.</summary>
    private static class Paint
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color DimText = Color.FromArgb(0xC0, 0xC9, 0xD1, 0xDC);
        public static readonly Color Apsis = Color.FromRgb(0x8E, 0xC5, 0xFF);

        public static readonly IBrush SunGlow = RadialGradient(
            (Color.FromArgb(200, 255, 236, 190), 0.25),
            (Color.FromArgb(70, 255, 200, 120), 0.4),
            (Color.FromArgb(0, 255, 170, 80), 1));

        public static readonly IBrush EclipticPlane = RadialGradient(
            (Color.FromArgb(46, 140, 170, 230), 0),
            (Color.FromArgb(22, 120, 150, 220), 0.75),
            (Color.FromArgb(0, 120, 150, 220), 1));

        public static readonly IPen OrbitPen = MakePen(Solid(80, 255, 255, 255), 1, dashed: true);
        public static readonly IPen OrbitTruePen = MakePen(Solid(150, 0x8E, 0xC5, 0xFF), 1.4);
        public static readonly IPen OrbitTick = MakePen(Solid(150, 255, 255, 255), 1.2);
        public static readonly IBrush OrbitArrow = Solid(150, 255, 255, 255);
        public static readonly IPen SunLine = MakePen(Solid(70, 255, 230, 190), 1, dashed: true);
        public static readonly IPen DistanceLine = MakePen(Solid(170, 0x8E, 0xC5, 0xFF), 1.2);
        public static readonly IBrush ApsisBrush = Solid(255, 0x8E, 0xC5, 0xFF);
        public static readonly IPen AxisPen = MakePen(Solid(235, 255, 255, 255), 1.6);
        public static readonly IPen GhostAxis = MakePen(Solid(200, 255, 255, 255), 1.1);
        public static readonly IPen PolarisPen = MakePen(Solid(110, 255, 255, 255), 1, dashed: true);
        public static readonly IBrush StarBrush = Solid(240, 255, 244, 214);
        public static readonly IPen EquatorPen = MakePen(Solid(110, 255, 255, 255), 1);
        public static readonly IPen LatitudePen = MakePen(Solid(240, 0xFF, 0xD3, 0x6B), 1.6);
        public static readonly IBrush ObserverFill = Solid(255, 0xFF, 0xD3, 0x6B);
        public static readonly IPen ObserverPen = MakePen(Solid(255, 20, 26, 40), 1.2);
    }
}
