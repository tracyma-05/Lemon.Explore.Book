using Avalonia;
using Lemon.Explore.Book.Controls.Explore;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.IceAges;

/// <summary>
/// The orbit view: Earth's orbit seen at a slant with the Sun at one focus, the four season points and the axis, Earth
/// going round (faster near the Sun); three gauges for the stretch (eccentricity), the tilt (obliquity) and the wobble
/// (where the axis points among the stars); and the strip of the chosen cycle over 900,000 years.
/// </summary>
public sealed partial class IceAgeDiagram
{
    /// <summary>The orbit is seen from this far above its plane: its height is squashed to this share.</summary>
    private const double OrbitSquash = 0.38;

    /// <summary>How much the stretch is drawn bigger when exaggerating (it is hardly visible as it is).</summary>
    internal const double StretchFactor = 5;

    /// <summary>How much the tilt's changes (about its mean) are drawn bigger when exaggerating.</summary>
    internal const double TiltFactor = 3;

    private const double MeanTilt = 23.3;

    /// <summary>The orbital elements the orbit view shows: only the chosen cycle changes.</summary>
    private OrbitalElements ShownOrbit => IceAgeModel.Elements(TimeKa, Cycle);

    private double ShownStretch(OrbitalElements orbit) => orbit.Eccentricity * (Exaggerate ? StretchFactor : 1);

    private double ShownTilt(double tilt) => Exaggerate ? MeanTilt + ((tilt - MeanTilt) * TiltFactor) : tilt;

    private void DrawOrbitMode(PaintContext dc, double now)
    {
        var stage = Stage();
        var strip = StripRect(stage);
        var area = new Rect(stage.Left, stage.Top, stage.Width, Math.Max(120, strip.Top - 30 - stage.Top));
        var column = Math.Clamp(area.Width * 0.3, 210, 300);
        var orbitArea = new Rect(area.Left + 20, area.Top, Math.Max(100, area.Width - column - 50), area.Height);
        var gauges = new Rect(orbitArea.Right + 10, area.Top, column, area.Height);
        var orbit = ShownOrbit;

        DrawOrbit(dc, orbitArea, orbit, now);
        DrawGauges(dc, gauges, orbit);

        var (curve, low, high, color, caption) = Cycle switch
        {
            IceAgeCycle.Eccentricity => ("e", 0.0, 0.06, Ink.Orange, "Ice_Strip_Eccentricity"),
            IceAgeCycle.Tilt => ("tilt", 22.0, 24.6, Ink.Green, "Ice_Strip_Tilt"),
            IceAgeCycle.Precession => ("wobble", -0.06, 0.06, Ink.Violet, "Ice_Strip_Precession"),
            _ => ("sun", 420.0, 570.0, Ink.Gold, "Ice_Strip_Sunshine"),
        };
        Func<double, double> value = Cycle switch
        {
            IceAgeCycle.Eccentricity => k => IceAgeModel.Elements(k).Eccentricity,
            IceAgeCycle.Tilt => k => IceAgeModel.Elements(k).Obliquity,
            IceAgeCycle.Precession => k => -IceAgeModel.Elements(k).PrecessionIndex,
            _ => k => IceRecord.Instance.At(k).Sunshine,
        };
        DrawStrip(dc, strip, curve, value, low, high, color, caption);
    }

    #region the orbit

    /// <summary>The orbit's layout: the Sun's place on screen and the semi-major axis in pixels.</summary>
    private readonly record struct OrbitLayout(Point Sun, double A, double E)
    {
        /// <summary>The screen point at true anomaly <paramref name="anomaly"/> (degrees; 0 = perihelion, to the right).</summary>
        public Point At(double anomaly)
        {
            var v = IceAgeModel.Rad(anomaly);
            var r = A * IceAgeModel.Distance(E, anomaly);
            return new Point(Sun.X + (r * Math.Cos(v)), Sun.Y - (r * Math.Sin(v) * OrbitSquash));
        }

        /// <summary>A direction in space (x along the long axis, y across it in the orbit's plane, z up) on screen.</summary>
        public static Vector Screen(double x, double y, double z) =>
            new(x, -((y * OrbitSquash) + (z * Math.Sqrt(1 - (OrbitSquash * OrbitSquash)))));
    }

    private OrbitLayout LayOutOrbit(Rect area, OrbitalElements orbit)
    {
        var e = ShownStretch(orbit);

        // Room above for the far season's label, below for the near one's and the row of half-year pills.
        var h = area.Height;
        var a = Math.Max(60, Math.Min(area.Width * 0.40, Math.Min((h * 0.47) - 34, (h * 0.53) - 66) / OrbitSquash));

        // The orbit's centre a little above the middle of the area; the Sun at the focus on the perihelion side.
        var center = new Point(area.Left + (area.Width / 2), area.Top + (h * 0.47));
        return new OrbitLayout(new Point(center.X + (a * e), center.Y), a, e);
    }

    private void DrawOrbit(PaintContext dc, Rect area, OrbitalElements orbit, double now)
    {
        var layout = LayOutOrbit(area, orbit);
        var perihelionLongitude = orbit.PerihelionLongitude;
        double AnomalyOf(double longitude) => IceAgeModel.Wrap(longitude - perihelionLongitude);

        // The orbit in two halves: northern spring and summer (equinox to equinox through the June side), and the rest.
        var summer = new List<Point>();
        var winter = new List<Point>();
        for (var l = 0.0; l <= 180.001; l += 2) summer.Add(layout.At(AnomalyOf(l)));
        for (var l = 180.0; l <= 360.001; l += 2) winter.Add(layout.At(AnomalyOf(l)));
        var orbitGlow = Glows("orbit") || Glows("summerhalf");
        if (orbitGlow) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 8), Line(summer.Concat(winter)));
        dc.DrawGeometry(null, FadedPen(Ink.Gold, 0.8, 2.2), Line(summer));
        dc.DrawGeometry(null, FadedPen(Ink.Cyan, 0.7, 2.2), Line(winter));
        foreach (var v in new[] { 45.0, 135, 225, 315 }) _targets.Add("orbit", layout.At(v), 10);

        // The long axis through perihelion and aphelion.
        var perihelion = layout.At(0);
        var aphelion = layout.At(180);
        dc.DrawLine(FadedPen(Ink.Dim, 0.35, 1, dashed: true), perihelion, aphelion);

        DrawSun(dc, layout.Sun, 15);
        _targets.Add("sun", layout.Sun, 18);

        // Earth at the four season points, its axis fixed in space: tilted towards the Sun at the northern summer solstice.
        var tilt = IceAgeModel.Rad(ShownTilt(orbit.Obliquity));
        var summerAnomaly = IceAgeModel.Rad(AnomalyOf(90));
        var axis = OrbitLayout.Screen(-Math.Sin(tilt) * Math.Cos(summerAnomaly), -Math.Sin(tilt) * Math.Sin(summerAnomaly), Math.Cos(tilt));
        var seasons = new (double Longitude, string Key, Color Color)[]
        {
            (0, "Ice_Season_Spring", Ink.Green),
            (90, "Ice_Season_Summer", Ink.Gold),
            (180, "Ice_Season_Autumn", Ink.Orange),
            (270, "Ice_Season_Winter", Ink.Cyan),
        };
        var center = new Point((layout.At(0).X + layout.At(180).X) / 2, layout.Sun.Y);
        foreach (var (longitude, key, color) in seasons)
        {
            var at = layout.At(AnomalyOf(longitude));
            DrawEarth(dc, at, 8, layout.Sun, axis, 0.75);
            if (!ShowLabels) continue;

            // Above the marker on the far half and near the ends (perihelion and aphelion have their pills below), else below.
            var nearEnd = Math.Abs(at.X - center.X) > layout.A * 0.75;
            var below = !nearEnd && at.Y > center.Y;
            var text = Loc(key);
            var half = _labels.Text(text, 10.5, color).Width / 2;
            // (on a phone the area can be narrower than the label: then it is centred, not clamped)
            var x = area.Width > half * 2 ? Math.Clamp(at.X, area.Left + half, area.Right - half) : area.Center.X;
            DrawText(dc, text, new Point(x, at.Y + (below ? 24 : -26)), color, 10.5, TextAnchor.Center);
        }

        // Perihelion and aphelion: the real dates and distances (not the drawn stretch).
        var perihelionDay = IceAgeModel.PerihelionDay(orbit);
        var aphelionDay = (perihelionDay + (IceAgeModel.YearDays / 2)) % 365.25;
        dc.DrawEllipse(FadedBrush(Ink.Gold, 1), null, perihelion, 3.5, 3.5);
        dc.DrawEllipse(FadedBrush(Ink.Cyan, 1), null, aphelion, 3.5, 3.5);
        var nearText = ShowLabels
            ? LocFormat("Ice_Orbit_Perihelion", DateText(perihelionDay), DistanceText(1 - orbit.Eccentricity))
            : Loc("Ice_Orbit_PerihelionShort");
        var farText = ShowLabels
            ? LocFormat("Ice_Orbit_Aphelion", DateText(aphelionDay), DistanceText(1 + orbit.Eccentricity))
            : Loc("Ice_Orbit_AphelionShort");
        PillInside(dc, nearText, perihelion + new Vector(0, 26), Ink.Gold, "perihelion", area);
        PillInside(dc, farText, aphelion + new Vector(0, 26), Ink.Cyan, "aphelion", area);
        _targets.Add("perihelion", perihelion, 9);
        _targets.Add("aphelion", aphelion, 9);

        // The two halves of the year: the northern summer half is the longer one now, Earth being far and slow then.
        if (ShowLabels)
        {
            // A row under the orbit: the gold (spring to autumn) half and the blue one.
            var summerDays = IceAgeModel.NorthernSummerDays(orbit);
            var summerPill = LocFormat("Ice_Orbit_SummerHalf", Number(summerDays, "0.0"));
            var winterPill = LocFormat("Ice_Orbit_WinterHalf", Number(IceAgeModel.YearDays - summerDays, "0.0"));
            var y = Math.Min(area.Bottom - 10, center.Y + (layout.A * OrbitSquash) + 54);
            var gap = 14.0;
            var w1 = _labels.Text(summerPill, 11, Ink.Gold).Width + 12;
            var w2 = _labels.Text(winterPill, 11, Ink.Cyan).Width + 12;
            var left = center.X - ((w1 + w2 + gap) / 2);
            DrawTargetPill(dc, summerPill, new Point(left + (w1 / 2), y), Ink.Gold, "summerhalf", 11);
            DrawTargetPill(dc, winterPill, new Point(left + w1 + gap + (w2 / 2), y), Ink.Cyan, "summerhalf", 11);
        }

        // Earth going round: equal areas in equal times, so it hurries near the Sun.
        var anomaly = IceAgeModel.AnomalyAfterPerihelion(layout.E, _year);
        var earth = layout.At(anomaly);
        dc.DrawLine(FadedPen(Ink.Gold, 0.25, 1), layout.Sun, earth);
        if (Glows("axis")) dc.DrawLine(ExploreTargets.GlowPen(now, 7), earth - (axis * 20), earth + (axis * 20));
        DrawEarth(dc, earth, 12, layout.Sun, axis, 1);
        _targets.Add("earth", earth, 15);
        _targets.Add("axis", earth + (axis * 19), 7);
    }

    /// <summary>A target pill centred on <paramref name="at"/> but kept inside <paramref name="area"/> sideways.</summary>
    private void PillInside(PaintContext dc, string text, Point at, Color color, string id, Rect area)
    {
        var half = (_labels.Text(text, 11, color).Width / 2) + 6;
        DrawTargetPill(dc, text, new Point(Math.Clamp(at.X, area.Left + half, Math.Max(area.Left + half, area.Right - half)), at.Y), color, id, 11);
    }

    /// <summary>A small Earth lit from the Sun's side, with its axis (north end dotted) through it.</summary>
    private void DrawEarth(PaintContext dc, Point at, double radius, Point sun, Vector axis, double alpha)
    {
        dc.DrawLine(FadedPen(Ink.Text, 0.55 * alpha, 1.3), at - (axis * radius * 1.7), at + (axis * radius * 1.7));
        dc.DrawEllipse(Ink.EarthBall, null, at, radius, radius);
        Vector away = at - sun;
        if (away.Length > 1)
        {
            away /= away.Length;
            dc.PushClip(SpaceDrawing.Ellipse(at, radius, radius));
            dc.DrawEllipse(FadedBrush(Color.FromRgb(2, 4, 10), 0.72), null, at + (away * radius * 1.05), radius * 1.05, radius * 1.05);
            dc.Pop();
        }

        dc.DrawEllipse(FadedBrush(Ink.Text, alpha), null, at + (axis * radius * 1.7), 2, 2);
    }

    #endregion

    #region the gauges

    private void DrawGauges(PaintContext dc, Rect column, OrbitalElements orbit)
    {
        var gap = 10.0;
        var h = Math.Max(60, (column.Height - (2 * gap)) / 3);
        var boxes = Enumerable.Range(0, 3).Select(i => new Rect(column.Left, column.Top + (i * (h + gap)), column.Width, h)).ToArray();
        DrawStretchGauge(dc, boxes[0], orbit);
        DrawTiltGauge(dc, boxes[1], orbit);
        DrawWobbleGauge(dc, boxes[2], orbit);
    }

    /// <summary>Whether a gauge's cycle is held at today's value (another cycle is chosen).</summary>
    private bool Held(IceAgeCycle cycle) => Cycle != IceAgeCycle.All && Cycle != cycle;

    private Rect GaugePanel(PaintContext dc, Rect box, string captionKey, Color color, IceAgeCycle cycle, string id)
    {
        var inside = DrawPanel(dc, box, Loc(captionKey), color);
        if (Cycle == cycle) dc.DrawRoundedRectangle(null, FadedPen(color, 0.9, 1.6), box, 10, 10);
        if (Held(cycle)) DrawText(dc, Loc("Ice_Gauge_Held"), new Point(box.Right - 12, box.Top + 14), Ink.Dim, 10, TextAnchor.Right);
        AddBoxTarget(id, box);
        return inside;
    }

    /// <summary>The stretch: a bar from a circle (0) to the most stretched (0.06), and the orbit's shape beside it.</summary>
    private void DrawStretchGauge(PaintContext dc, Rect box, OrbitalElements orbit)
    {
        var inside = GaugePanel(dc, box, "Ice_Gauge_Stretch", Ink.Orange, IceAgeCycle.Eccentricity, "eccentricity");
        var e = orbit.Eccentricity;
        DrawText(dc, LocFormat("Ice_Gauge_StretchValue", Number(e, "0.0000")), new Point(inside.Left + 2, inside.Top + 10), Ink.Text, 13, bold: true);

        var shape = Math.Min(inside.Height * 0.42, 36);
        var shapeCenter = new Point(inside.Right - shape - 4, inside.Top + 14);
        var shown = ShownStretch(orbit);
        var b = shape * Math.Sqrt(Math.Max(0, 1 - (shown * shown)));
        dc.DrawEllipse(null, FadedPen(Ink.Orange, 0.9, 1.4), shapeCenter, shape, b * 0.6);
        dc.DrawEllipse(Ink.SunDisc, null, shapeCenter + new Vector(shape * shown, 0), 2.6, 2.6);

        var bar = new Rect(inside.Left + 4, inside.Top + 34, inside.Width - 8, 6);
        if (bar.Bottom > inside.Bottom - 12) bar = bar.WithY(inside.Bottom - 20);
        dc.DrawRoundedRectangle(FadedBrush(Ink.Dim, 0.25), null, bar, 3, 3);
        dc.DrawRoundedRectangle(FadedBrush(Ink.Orange, 0.9), null, new Rect(bar.Left, bar.Top, bar.Width * Math.Clamp(e / 0.06, 0, 1), bar.Height), 3, 3);
        var today = bar.Left + (bar.Width * IceAgeModel.Today1950.Eccentricity / 0.06);
        dc.DrawLine(FadedPen(Ink.Text, 0.8, 1.2), new Point(today, bar.Top - 3), new Point(today, bar.Bottom + 3));
        DrawText(dc, "0", new Point(bar.Left, bar.Bottom + 8), Ink.Dim, 9.5);
        DrawText(dc, "0.06", new Point(bar.Right, bar.Bottom + 8), Ink.Dim, 9.5, TextAnchor.Right);
        DrawText(dc, Loc("Ice_Gauge_Today"), new Point(today, bar.Bottom + 8), Ink.Dim, 9.5, TextAnchor.Center);
        if (inside.Height > 70) DrawText(dc, Loc("Ice_Gauge_StretchPeriod"), new Point(inside.Left + 2, inside.Bottom - 6), Ink.Dim, 10);
    }

    /// <summary>The tilt: Earth with its axis against the upright, the range it swings through, and the value.</summary>
    private void DrawTiltGauge(PaintContext dc, Rect box, OrbitalElements orbit)
    {
        var inside = GaugePanel(dc, box, "Ice_Gauge_Tilt", Ink.Green, IceAgeCycle.Tilt, "tilt");
        DrawText(dc, LocFormat("Ice_Gauge_TiltValue", Number(orbit.Obliquity, "0.00")), new Point(inside.Left + 2, inside.Top + 10), Ink.Text, 13, bold: true);
        if (inside.Height > 70) DrawText(dc, Loc("Ice_Gauge_TiltPeriod"), new Point(inside.Left + 2, inside.Bottom - 6), Ink.Dim, 10);

        var radius = Math.Max(8, Math.Min(inside.Height * 0.3, 24));
        var center = new Point(inside.Right - radius - 22, inside.Top + (inside.Height * 0.52));
        var length = radius * 1.75;
        dc.DrawLine(FadedPen(Ink.Dim, 0.6, 1, dashed: true), center - new Vector(0, length), center + new Vector(0, length));

        // The range the tilt swings through over the cycles (22.1° to 24.5°), drawn as an arc above Earth.
        var arc = new List<Point>();
        for (var t = 22.1; t <= 24.5001; t += 0.1)
        {
            var a = IceAgeModel.Rad(ShownTilt(t));
            arc.Add(center + new Vector(Math.Sin(a) * length, -Math.Cos(a) * length));
        }

        dc.DrawGeometry(null, FadedPen(Ink.Green, 0.5, 4, flat: true), Line(arc));
        dc.DrawEllipse(Ink.EarthBall, null, center, radius, radius);
        var tilt = IceAgeModel.Rad(ShownTilt(orbit.Obliquity));
        var direction = new Vector(Math.Sin(tilt), -Math.Cos(tilt));
        dc.DrawLine(FadedPen(Ink.Text, 0.95, 2), center - (direction * length), center + (direction * length));
        dc.DrawEllipse(FadedBrush(Ink.Text, 1), null, center + (direction * length), 2.5, 2.5);

        // The Arctic Circle moves with the tilt (90° − tilt).
        var arcticY = center.Y - (radius * Math.Sin(IceAgeModel.Rad(90 - ShownTilt(orbit.Obliquity))));
        var half = Math.Sqrt(Math.Max(0, (radius * radius) - ((arcticY - center.Y) * (arcticY - center.Y))));
        dc.DrawLine(FadedPen(Ink.Cyan, 0.8, 1), new Point(center.X - half, arcticY), new Point(center.X + half, arcticY));
        if (Exaggerate) DrawText(dc, LocFormat("Ice_Gauge_Times", Number(TiltFactor, "0")), new Point(center.X, center.Y + length + 8), Ink.Dim, 9.5, TextAnchor.Center);
    }

    /// <summary>The wobble: the sky round the pole of Earth's orbit, the circle the axis traces, the stars on it and where it points.</summary>
    private void DrawWobbleGauge(PaintContext dc, Rect box, OrbitalElements orbit)
    {
        var inside = GaugePanel(dc, box, "Ice_Gauge_Wobble", Ink.Violet, IceAgeCycle.Precession, "polestar");
        var dateText = LocFormat("Ice_Gauge_PerihelionDate", DateText(IceAgeModel.PerihelionDay(orbit)));
        DrawText(dc, dateText, new Point(inside.Left + 2, inside.Top + 10), Ink.Text, 12, bold: true);

        var radius = Math.Max(16, Math.Min((inside.Height - 26) / 2, inside.Width / 2.6));
        var center = new Point(inside.Right - radius - 6, inside.Bottom - radius - 2);
        Point Sky((double Longitude, double Latitude) p)
        {
            var rho = (90 - p.Latitude) / 30 * radius;
            var a = IceAgeModel.Rad(p.Longitude);
            return center + new Vector(rho * Math.Cos(a), -rho * Math.Sin(a));
        }

        dc.DrawEllipse(FadedBrush(Color.FromRgb(4, 6, 14), 1), Ink.Faint, center, radius, radius);
        var circle = new List<Point>();
        for (var l = 0.0; l <= 360.001; l += 5) circle.Add(Sky((l, 90 - orbit.Obliquity)));
        dc.DrawGeometry(null, FadedPen(Ink.Violet, 0.55, 1.2, dashed: true), Line(circle));

        foreach (var (id, ra, dec) in IceAgeModel.PoleStars)
        {
            var at = Sky(IceAgeModel.Ecliptic(ra, dec));
            if (((Vector)(at - center)).Length > radius - 2) continue;
            var big = id is "polaris" or "vega" or "thuban" or "deneb";
            dc.DrawEllipse(Brushes.White, null, at, big ? 2.2 : 1.4, big ? 2.2 : 1.4);
            if (big && ShowLabels) DrawText(dc, Loc(StarKey(id)), at + new Vector(4, -7), Ink.Dim, 9.5);
        }

        var pole = Sky((IceAgeModel.Wrap(90 - orbit.Precession), 90 - orbit.Obliquity));
        dc.DrawEllipse(null, FadedPen(Ink.Violet, 1, 1.8), pole, 5, 5);
        dc.DrawLine(FadedPen(Ink.Violet, 0.6, 1), center, pole);
        dc.DrawEllipse(FadedBrush(Ink.Dim, 0.8), null, center, 1.6, 1.6);
        if (inside.Width - (2 * radius) > 70) DrawText(dc, Loc("Ice_Gauge_WobblePeriod"), new Point(inside.Left + 2, inside.Bottom - 6), Ink.Dim, 10);
    }

    private static string StarKey(string id) => id switch
    {
        "polaris" => "Ice_Star_Polaris",
        "vega" => "Ice_Star_Vega",
        "thuban" => "Ice_Star_Thuban",
        _ => "Ice_Star_Deneb",
    };

    #endregion

    #region dates and distances

    /// <summary>A day of the year (0 = 1 January) as a date: "Jan 3" / "1月3日".</summary>
    internal static string DateText(double dayOfYear)
    {
        var date = new DateTime(2001, 1, 1).AddDays(Math.Floor(((dayOfYear % 365) + 365) % 365));
        return date.ToString(Loc("Ice_Date_Format"), LocalizationService.Instance.CurrentCulture);
    }

    /// <summary>A distance from the Sun given in units of the semi-major axis: "147.1 million km" / "1.471亿千米".</summary>
    internal static string DistanceText(double au)
    {
        var millions = au * 149.598;
        var unit = double.Parse(Loc("Ice_Distance_Unit"), System.Globalization.CultureInfo.InvariantCulture);
        return LocFormat("Ice_Value_Distance", Number(millions / unit, Loc("Ice_Distance_Digits")));
    }

    #endregion
}
