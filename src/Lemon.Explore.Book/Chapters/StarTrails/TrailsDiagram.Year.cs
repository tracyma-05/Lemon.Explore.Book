using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.StarTrails;

/// <summary>
/// Year mode. Left: Earth going round the Sun seen from above the north pole, the constellations far away round the
/// edge; the night side of Earth looks out to the stars opposite the Sun. Right: the evening sky at 21:00 facing the
/// equator for the day, sliding west night by night; under it the sidereal day: one turn brings the stars back, a little
/// more the Sun.
/// </summary>
public sealed partial class TrailsDiagram
{
    /// <summary>The constellations placed round the orbit's edge (those not too far from the Sun's path).</summary>
    internal static readonly string[] RingFigures = ["Taurus", "Orion", "Gemini", "Leo", "Scorpius", "Sagittarius", "Aquila", "Pegasus"];

    /// <summary>The orbit's centre (the Sun) and radius on the picture, for dragging Earth.</summary>
    private Point _orbitCenter;

    private double _orbitRadius;

    /// <summary>The direction, degrees anticlockwise from the right, of right ascension <paramref name="ra"/> hours seen from above.</summary>
    private static double RaAngle(double ra) => ra * 15;

    /// <summary>Earth's direction from the Sun on <paramref name="day"/>, degrees: opposite the Sun's right ascension.</summary>
    internal static double EarthAngle(double day) => SkyModel.Wrap360(RaAngle(SkyModel.SunRa(day) + 12));

    /// <summary>The day on which Earth is at <paramref name="angle"/> (degrees) from the Sun, found by a few corrections.</summary>
    internal static double DayForEarthAngle(double angle)
    {
        var day = WrapDay(((angle - 180 - 280.46) / 0.9856474) + 1);
        for (var i = 0; i < 4; i++)
        {
            var miss = SkyModel.Wrap360(angle - EarthAngle(day) + 180) - 180;
            day = WrapDay(day + (miss / 0.9856474));
        }

        return day;
    }

    private void DrawYearMode(PaintContext dc, double width, double height)
    {
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        var stage = Stage();
        var size = Math.Min(stage.Width * 0.46, stage.Height);
        var orbitArea = new Rect(stage.Left + 20, stage.Top + ((stage.Height - size) / 2), size, size);
        var right = new Rect(orbitArea.Right + 24, stage.Top + 4, Math.Max(160, stage.Right - orbitArea.Right - 48), stage.Height - 8);
        var panorama = new Rect(right.Left, right.Top, right.Width, right.Height * 0.58);
        var inset = new Rect(right.Left, panorama.Bottom + 14, right.Width, Math.Max(60, right.Bottom - panorama.Bottom - 14));

        DrawOrbit(dc, orbitArea);
        DrawPanorama(dc, panorama);
        DrawSiderealDay(dc, inset);
    }

    #region the orbit

    private void DrawOrbit(PaintContext dc, Rect area)
    {
        var center = new Point(area.Left + (area.Width / 2), area.Top + (area.Height / 2));
        var ring = area.Width * 0.4;
        var orbit = ring * 0.6;
        _orbitCenter = center;
        _orbitRadius = orbit;
        Point At(double degrees, double radius) => center + new Vector(Math.Cos(degrees * Math.PI / 180) * radius, -Math.Sin(degrees * Math.PI / 180) * radius);

        // The far stars round the edge, and the constellations among them.
        dc.DrawEllipse(null, FadedPen(Ink.Figure, 0.25, 1, dashed: true), center, ring, ring);
        for (var i = 0; i < RingFigures.Length; i++)
        {
            // Neighbours' names alternate in and out so close ones do not overlap.
            var id = RingFigures[i];
            var figure = StarCatalog.Figures.First(f => f.Id == id);
            var (ra, _) = StarCatalog.Center(figure);
            var p = At(RaAngle(ra), ring);
            dc.DrawEllipse(FadedBrush(Ink.Figure, 0.8), null, p, 2.5, 2.5);
            if (ShowLabels) DrawTargetPill(dc, Loc(ObjectTitleKey(FigureId(id))!), At(RaAngle(ra), ring + (i % 2 == 0 ? 16 : 36)), Ink.Figure, FigureId(id), 10.5);
            else _targets.Add(FigureId(id), p, 10);
        }

        // The orbit with the months, and which way Earth goes.
        dc.DrawEllipse(null, FadedPen(Ink.Text, 0.3, 1.2), center, orbit, orbit);
        for (var month = 1; month <= 12; month++)
        {
            var day = new DateTime(2025, month, 1).DayOfYear;
            var angle = EarthAngle(day);
            dc.DrawLine(FadedPen(Ink.Text, 0.4, 1), At(angle, orbit - 5), At(angle, orbit + 5));
            if (ShowLabels) DrawText(dc, Loc($"Trails_Month_{month}"), At(angle, orbit - 16), Ink.Dim, 10, center: true);
        }

        var travel = EarthAngle(_day) + 40;
        DrawArrowHead(dc, FadedBrush(Ink.Text, 0.5), At(travel, orbit), new Vector(-Math.Sin(travel * Math.PI / 180), -Math.Cos(travel * Math.PI / 180)), 9);

        // The Sun.
        var glow = SpaceDrawing.RadialGradient((Color.FromArgb(150, 0xFF, 0xD8, 0x6A), 0), (Color.FromArgb(0, 0xFF, 0xA8, 0x3A), 1));
        dc.DrawEllipse(glow, null, center, orbit * 0.4, orbit * 0.4);
        dc.DrawEllipse(FadedBrush(Color.FromRgb(0xFF, 0xD8, 0x6A), 1), null, center, orbit * 0.13, orbit * 0.13);
        if (Glows("sun")) dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), center, orbit * 0.17, orbit * 0.17);
        _targets.Add("sun", center, orbit * 0.15);

        // Earth: lit towards the Sun, the night side looking out to the stars opposite.
        var earthAngle = EarthAngle(_day);
        var earth = At(earthAngle, orbit);
        var r = Math.Max(9, orbit * 0.08);
        var outward = new Vector(Math.Cos(earthAngle * Math.PI / 180), -Math.Sin(earthAngle * Math.PI / 180));
        var midnight = At(earthAngle, ring - 10);
        dc.DrawLine(FadedPen(Ink.Gold, 0.75, 1.6, dashed: true), earth + (outward * r), midnight);
        DrawArrowHead(dc, FadedBrush(Ink.Gold, 0.9), midnight, outward, 10);
        if (Glows("night")) dc.DrawLine(ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 8), earth + (outward * r), midnight);
        var nightLabel = DrawPill(dc, Loc("Trails_Label_Midnight"), Mix(earth, midnight, 0.55) + (new Vector(-outward.Y, outward.X) * 14), Ink.Gold, 10.5);
        AddBoxTarget("night", nightLabel);

        dc.DrawEllipse(FadedBrush(Color.FromRgb(0x3A, 0x7A, 0xD8), 1), null, earth, r, r);
        var side = new Vector(-outward.Y, outward.X) * r;
        var night = Enumerable.Range(0, 25).Select(i => earth + (side * Math.Cos(i * Math.PI / 24)) + (outward * r * Math.Sin(i * Math.PI / 24)));
        dc.DrawGeometry(FadedBrush(Colors.Black, 0.6), null, Line(night, closed: true, filled: true));
        if (Glows("earth")) dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), earth, r + 4, r + 4);
        _targets.Add("earth", earth, r + 2);
    }

    #endregion

    #region the evening sky

    /// <summary>The sky at 21:00 on the day, facing the equator, from the horizon up to 75°.</summary>
    private void DrawPanorama(PaintContext dc, Rect area)
    {
        dc.PushClip(new RectangleGeometry(area, 10, 10));
        dc.DrawRectangle(Ink.NightSky, null, area);
        var ground = area.Height * 0.12;
        var sky = new Rect(area.Left, area.Top, area.Width, area.Height - ground);
        var facing = Latitude >= 0 ? 180.0 : 0.0;
        var left = facing - 90;
        var lst = SkyModel.SiderealTime(_day, 21);
        Point? Map(Horizontal p)
        {
            var x = SkyModel.Wrap360(p.Azimuth - left);
            if (x > 180 || p.Altitude < 0) return null;
            return new Point(sky.Left + (x / 180 * sky.Width), sky.Bottom - (p.Altitude / 75 * sky.Height));
        }

        foreach (var star in FillStars.Where(s => s.Magnitude < 5.3).Concat(StarCatalog.Bright))
        {
            if (Map(SkyModel.ToHorizontal(star.Ra, star.Dec, lst, Latitude)) is not { } p) continue;
            var size = Math.Clamp(2.8 - (0.45 * star.Magnitude), 0.6, 3.8);
            dc.DrawEllipse(FadedBrush(StarColor(star.ColorIndex), Math.Clamp(1.1 - (0.14 * star.Magnitude), 0.25, 1)), null, p, size, size);
        }

        foreach (var figure in StarCatalog.Figures)
        {
            var any = false;
            foreach (var (a, b) in figure.Lines)
            {
                var sa = StarCatalog.Get(a);
                var sb = StarCatalog.Get(b);
                if (Map(SkyModel.ToHorizontal(sa.Ra, sa.Dec, lst, Latitude)) is not { } pa || Map(SkyModel.ToHorizontal(sb.Ra, sb.Dec, lst, Latitude)) is not { } pb) continue;
                if (Math.Abs(pa.X - pb.X) > sky.Width / 2) continue;
                dc.DrawLine(FadedPen(Ink.Figure, 0.5, 1), pa, pb);
                any = true;
            }

            if (!any || !ShowLabels) continue;
            var (ra, dec) = StarCatalog.Center(figure);
            if (Map(SkyModel.ToHorizontal(ra, dec, lst, Latitude)) is not { } c) continue;
            DrawTargetPill(dc, Loc(ObjectTitleKey(FigureId(figure.Id))!), c + new Vector(0, 16), Ink.Figure, FigureId(figure.Id), 10);
        }

        dc.DrawRectangle(Ink.Ground, Ink.GroundEdge, new Rect(area.Left, sky.Bottom, area.Width, ground));
        string[] keys = ["Trails_Dir_N", "Trails_Dir_NE", "Trails_Dir_E", "Trails_Dir_SE", "Trails_Dir_S", "Trails_Dir_SW", "Trails_Dir_W", "Trails_Dir_NW"];
        for (var i = 0; i < 8; i++)
        {
            var x = SkyModel.Wrap360((i * 45) - left);
            if (x > 180.5) continue;
            DrawText(dc, Loc(keys[i]), new Point(sky.Left + (x / 180 * sky.Width), sky.Bottom + (ground / 2)), Ink.Dim, 11, center: true, bold: i % 2 == 0);
        }

        dc.Pop();
        dc.DrawRoundedRectangle(null, Ink.Frame, area, 10, 10);
        var header = LocFormat("Trails_Year_Panorama", DateText(_day), Loc(Latitude >= 0 ? "Trails_Dir_S" : "Trails_Dir_N"));
        var box = DrawPill(dc, header, new Point(area.Left + (area.Width / 2), area.Top + 14), Ink.Text, 11.5);
        if (Glows("panorama")) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), area, 10, 10);
        AddBoxTarget("panorama", box);
    }

    /// <summary>A day of the year as a short date in the page's language.</summary>
    internal static string DateText(double day)
    {
        var date = new DateTime(2025, 1, 1).AddDays(Math.Floor(WrapDay(day)) - 1);
        return LocFormat("Trails_Date", Loc($"Trails_Month_{date.Month}"), date.Day);
    }

    #endregion

    #region the sidereal day

    /// <summary>
    /// One day of Earth turning while it moves along its orbit (the step drawn 15 times too big): after one turn the far
    /// star is back overhead, the Sun only after a little more.
    /// </summary>
    private void DrawSiderealDay(PaintContext dc, Rect area)
    {
        dc.DrawRoundedRectangle(FadedBrush(Color.FromRgb(0x08, 0x0C, 0x14), 0.85), Ink.Frame, area, 10, 10);
        DrawText(dc, Loc("Trails_Year_SiderealCaption"), new Point(area.Left + 12, area.Top + 14), Ink.Dim, 11);
        if (Glows("sidereal")) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), area, 10, 10);
        AddBoxTarget("sidereal", new Rect(area.Left, area.Top, area.Width, 26));
        if (area.Height < 90) return;

        const double step = 15;
        var cycle = Frac(_t / 11);
        var p = Math.Min(1, cycle / 0.82);
        var sun = new Point(area.Left + (area.Width * 0.12), area.Top + (area.Height * 0.52));
        var radius = Math.Min(area.Width * 0.62, (area.Height * 0.36) / Math.Sin(step / 2 * Math.PI / 180) / 2);
        var start = -step / 2;
        Point Orbit(double degrees) => sun + new Vector(Math.Cos(degrees * Math.PI / 180) * radius, -Math.Sin(degrees * Math.PI / 180) * radius);

        // A piece of the orbit, the Sun, and Earth where the day began (faint) and now.
        var arc = Enumerable.Range(0, 21).Select(i => Orbit(start - 8 + ((step + 16) * i / 20))).ToList();
        dc.DrawGeometry(null, FadedPen(Ink.Text, 0.25, 1, dashed: true), Line(arc));
        dc.DrawEllipse(FadedBrush(Color.FromRgb(0xFF, 0xD8, 0x6A), 1), null, sun, 9, 9);

        var startEarth = Orbit(start);
        var earth = Orbit(start + (step * p));
        var r = Math.Clamp(area.Height * 0.11, 8, 18);
        dc.DrawEllipse(FadedBrush(Color.FromRgb(0x3A, 0x7A, 0xD8), 0.3), null, startEarth, r, r);

        // The far star lies straight on from where the day began, beyond the Sun: arrows to it stay parallel.
        Vector toStar = sun - startEarth;
        toStar = toStar.Normalize();
        dc.DrawLine(FadedPen(Ink.Cyan, 0.6, 1.2, dashed: true), earth, earth + (toStar * radius * 0.5));
        DrawArrowHead(dc, FadedBrush(Ink.Cyan, 0.8), earth + (toStar * radius * 0.5), toStar, 8);
        DrawText(dc, Loc("Trails_Year_FarStar"), earth + (toStar * radius * 0.5) + new Vector(-10, -12), Ink.Cyan, 10.5);

        // Earth turning: the marker starts pointing at the Sun and the star, and turns one whole turn plus the step.
        dc.DrawEllipse(FadedBrush(Color.FromRgb(0x3A, 0x7A, 0xD8), 1), null, earth, r, r);
        var startDirection = Math.Atan2(-toStar.Y, toStar.X) * 180 / Math.PI;
        var turned = p * (360 + step);
        var marker = (startDirection + turned) * Math.PI / 180;
        var tip = earth + new Vector(Math.Cos(marker), -Math.Sin(marker)) * (r + 12);
        dc.DrawLine(FadedPen(Ink.Orange, 1, 2.4), earth, tip);
        dc.DrawEllipse(FadedBrush(Ink.Orange, 1), null, tip, 3, 3);

        // When the marker lines up again: first with the star, then with the Sun.
        var hours = p * 24;
        var text = Math.Abs(turned - 360) < 6 ? Loc("Trails_Year_StarBack")
            : p >= 0.999 ? Loc("Trails_Year_SunBack")
            : LocFormat("Trails_Year_Turned", Number(hours, "0"));
        DrawText(dc, text, new Point(area.Left + (area.Width * 0.5), area.Bottom - 30), Math.Abs(turned - 360) < 6 ? Ink.Cyan : p >= 0.999 ? Ink.Gold : Ink.Text, 12, bold: true);
        DrawText(dc, Loc("Trails_Year_Exaggerated"), new Point(area.Left + 12, area.Bottom - 12), Ink.Dim, 10);
    }

    #endregion
}
