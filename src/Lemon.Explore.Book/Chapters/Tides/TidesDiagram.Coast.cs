using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Chapters.MoonPhases;
using Lemon.Explore.Book.Chapters.Seasons;
using Lemon.Explore.Book.Controls.Space;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Tides;

/// <summary>
/// The coast view: a harbour cut through from the sea (left) to the land (right), facing the equator, with the Sun and
/// the Moon crossing the sky above it. The water rises and falls along a tide staff on the pier, uncovering the beach
/// at low water; today's high and low water are marked. Heights are metres above the lowest low water (chart datum)
/// on a coast where the Moon alone would raise the water a metre above and below the mean.
/// </summary>
public sealed partial class TidesDiagram
{
    private const double CoastSkyHalfWidth = 110;

    private double _datumY;
    private double _metre;

    /// <summary>Screen y of a height in metres above chart datum.</summary>
    private double LevelY(double metres) => _datumY - (metres * _metre);

    private void DrawCoast(PaintContext dc, double dpi, double now)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        var day = _day;
        var hours = TideGeometry.HourOfDay(day);

        // The walkthrough's subtitles take the bottom of the view while one runs.
        var picture = height - _reserve.Current;
        _metre = Math.Clamp(picture * 0.068, 34, 76);
        _datumY = picture * 0.62;
        var level = TideGeometry.CoastHeight(day);
        var waterY = LevelY(level);
        var horizonY = LevelY(1 + TideGeometry.SolarRatio);

        // The sky, the stars at night, and the Sun and the Moon on their way across.
        var latitude = Math.Clamp(CoastLatitude, -89, 89);
        var sun = MoonPhaseGeometry.SunDirection(latitude, hours);
        var moon = MoonPhaseGeometry.MoonDirection(latitude, day, hours);
        var sunAltitude = MoonPhaseGeometry.Altitude(sun);
        var daylight = Noise.SmoothStep(-8, 8, sunAltitude);
        dc.DrawRectangle(SeasonsDiagram.SkyBrush(sunAltitude), null, new Rect(0, 0, width, height));
        _stars.Draw(dc, width, horizonY, dpi, now, 0.85 * (1 - daylight));

        var scaleX = width / (2 * CoastSkyHalfWidth);
        var scaleY = Math.Min(scaleX * 1.6, (horizonY - 120) / 72);
        Point SkyAt((double East, double North, double Up) d) => new(
            (width / 2) + (SeasonsGeometry.Normalize180(MoonPhaseGeometry.Azimuth(d) - 180) * scaleX),
            horizonY - (MoonPhaseGeometry.Altitude(d) * scaleY));

        var sunAt = SkyAt(sun);
        if (sunAltitude > -4)
        {
            var glow = 56 * Math.Max(0.3, daylight);
            dc.PushOpacity(Math.Max(0.2, daylight));
            dc.DrawEllipse(Coast.SunGlow, null, sunAt, glow, glow);
            dc.Pop();
            dc.DrawEllipse(sunAltitude > 3 ? Coast.SunDisc : Coast.LowSunDisc, null, sunAt, 13, 13);
            _targets.Add("sun", sunAt, 13);
        }

        var moonAt = SkyAt(moon);
        var moonUp = MoonPhaseGeometry.Altitude(moon) > -3;
        if (moonUp)
        {
            var elongation = MoonPhaseGeometry.Elongation(day);
            Vector lit = sunAt - moonAt;
            if (lit.Length < 1) lit = new Vector(-1, 0);
            dc.DrawEllipse(Coast.MoonGlow, null, moonAt, 34, 34);
            MoonDrawing.DrawPhaseIcon(dc, moonAt, 15, elongation, lit);
        }

        // The land, cut through: deep water, a drop-off, the beach, the sea wall and the promenade.
        var land = LandShape(width, height);
        dc.DrawGeometry(Coast.Sand, null, land);

        // The strip of beach the water has left since today's high water is still wet.
        var today = Math.Floor(day);
        var turns = TideGeometry.Turns(today, today + 1);
        var highToday = turns.Where(t => t.IsHigh).Select(t => TideGeometry.ToCoastMetres(t.Height)).DefaultIfEmpty(level).Max();
        var lowToday = turns.Where(t => !t.IsHigh).Select(t => TideGeometry.ToCoastMetres(t.Height)).DefaultIfEmpty(level).Min();
        var shore = ShoreX(width, level);
        var wetTo = ShoreX(width, highToday);
        if (wetTo > shore + 1)
        {
            var top = new Point(wetTo, LevelY(highToday));
            var edge = new Point(shore, waterY);
            dc.DrawGeometry(Coast.WetSand, null, Polygon(edge, top, top + new Vector(0, 8), edge + new Vector(0, 8)));
        }

        // Everything in front of the sky is collected, so it can be dimmed at night in one go.
        var shade = new GeometryGroup { FillRule = FillRule.NonZero };
        shade.Children.Add(land);
        DrawPier(dc, width, shade);

        // The sea: below its rippling surface and above the ground, the pier's posts showing through.
        var water = new CombinedGeometry(GeometryCombineMode.Exclude, WaterSurface(width, height, waterY, now), land);
        var waterColor = Lerp(Color.FromArgb(215, 0x0A, 0x1E, 0x38), Color.FromArgb(200, 0x2A, 0x78, 0xC0), daylight);
        var deep = Lerp(waterColor, Color.FromArgb(240, 0x04, 0x10, 0x22), 0.7);
        var waterBrush = new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, waterColor), new ImmutableGradientStop(1, deep)],
            startPoint: new RelativePoint(0, waterY, RelativeUnit.Absolute),
            endPoint: new RelativePoint(0, LevelY(-3.2), RelativeUnit.Absolute));
        dc.DrawGeometry(waterBrush, null, water);
        var surface = SurfaceLine(shore, waterY, now);
        if (Glows("water")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now), surface);
        dc.DrawGeometry(null, Coast.SurfacePen, surface);

        DrawBoat(dc, width, waterY, now, shade);
        DrawLighthouse(dc, width, shade);

        // Dim the land, the pier, the boat and the lighthouse at night; the sky and the water have night colours.
        if (daylight < 1)
        {
            dc.PushOpacity(0.5 * (1 - daylight));
            dc.DrawGeometry(Brushes.Black, null, shade);
            dc.Pop();
        }

        DrawLamp(dc, width, daylight, now);
        DrawStaff(dc, width, level, highToday, lowToday, day);

        if (moonUp)
        {
            var name = _labels.Get("Tides_Label_Moon", 11.5, TidesDrawing.Text);
            _labels.DrawPill(dc, "Tides_Label_Moon", new Point(moonAt.X + 20, moonAt.Y - (name.Height / 2) - 2), 11.5, TidesDrawing.Text);
            _targets.Add("moon", moonAt, 15);
        }
    }

    /// <summary>The surface of the ground from the sea floor on the left up to the promenade on the right.</summary>
    private Point[] GroundProfile(double width) =>
    [
        new Point(0, LevelY(-3.2)),
        new Point(width * 0.30, LevelY(-3.0)),
        new Point(width * 0.42, LevelY(-0.9)),
        new Point(width * 0.72, LevelY(3.4)),
        new Point(width * 0.78, LevelY(3.6)),
        new Point(width * 0.785, LevelY(4.6)),
        new Point(width, LevelY(4.6)),
    ];

    /// <summary>The ground cut through, closed along the bottom of the view.</summary>
    private StreamGeometry LandShape(double width, double height) =>
        Polygon([.. GroundProfile(width), new Point(width, height), new Point(0, height)]);

    /// <summary>Screen y of the ground at <paramref name="x"/>.</summary>
    private double GroundY(double width, double x)
    {
        var profile = GroundProfile(width);
        for (var i = 1; i < profile.Length; i++)
        {
            if (x > profile[i].X) continue;
            var t = (x - profile[i - 1].X) / Math.Max(1e-9, profile[i].X - profile[i - 1].X);
            return profile[i - 1].Y + ((profile[i].Y - profile[i - 1].Y) * Math.Clamp(t, 0, 1));
        }

        return profile[^1].Y;
    }

    /// <summary>Where water <paramref name="metres"/> high meets the beach; the ground only rises to the right.</summary>
    private double ShoreX(double width, double metres)
    {
        var y = LevelY(metres);
        var profile = GroundProfile(width);
        for (var i = 1; i < profile.Length; i++)
        {
            if (profile[i].Y > y) continue;
            var t = (profile[i - 1].Y - y) / Math.Max(1e-9, profile[i - 1].Y - profile[i].Y);
            return profile[i - 1].X + ((profile[i].X - profile[i - 1].X) * Math.Clamp(t, 0, 1));
        }

        return width;
    }

    /// <summary>The sea from its rippling surface down to the bottom of the view.</summary>
    private static StreamGeometry WaterSurface(double width, double height, double y, double now)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(0, height), true);
            for (var x = 0.0; x <= width + 8; x += 8) ctx.LineTo(new Point(x, y + Ripple(x, now)));
            ctx.LineTo(new Point(width + 8, height));

            ctx.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>The bright line of the water's surface, from the left edge to the shore.</summary>
    private static StreamGeometry SurfaceLine(double shore, double y, double now)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(0, y + Ripple(0, now)), false);
            for (var x = 8.0; x < shore; x += 8) ctx.LineTo(new Point(x, y + Ripple(x, now)));
            ctx.LineTo(new Point(shore, y));

            ctx.EndFigure(false);
        }
        return geometry;
    }

    /// <summary>Small waves on the surface, moving with time.</summary>
    private static double Ripple(double x, double now) =>
        (2.2 * Math.Sin((x / 38) + (now * 1.6))) + (1.3 * Math.Sin((x / 15) - (now * 2.4)));

    /// <summary>A pier on posts out from the sea wall, the tide staff beside its last post.</summary>
    private void DrawPier(PaintContext dc, double width, GeometryGroup shade)
    {
        var deckY = LevelY(4.2);
        var left = width * 0.50;
        var right = width * 0.785;
        for (var x = left; x < right; x += width * 0.055)
        {
            var post = new Rect(x, deckY, 6, Math.Max(0, GroundY(width, x + 3) - deckY + 4));
            dc.DrawRectangle(Coast.Post, null, post);
            shade.Children.Add(new RectangleGeometry(post));
        }

        var deck = new Rect(left - 8, deckY - 7, right - left + 8, 8);
        dc.DrawRectangle(Coast.Deck, null, deck);
        shade.Children.Add(new RectangleGeometry(deck));
        dc.DrawLine(Coast.RailPen, new Point(left - 8, deckY - 22), new Point(right, deckY - 22));
        for (var x = left - 8; x < right; x += 26) dc.DrawLine(Coast.RailPen, new Point(x, deckY - 22), new Point(x, deckY - 7));
    }

    /// <summary>
    /// The tide staff: a board marked every half metre, today's high and low water, and the level now. Drawn last so
    /// it can be read through the water and at night.
    /// </summary>
    private void DrawStaff(PaintContext dc, double width, double level, double highToday, double lowToday, double day)
    {
        var x = (width * 0.50) - 26;
        var top = LevelY(3.6);
        var bottom = LevelY(-0.4);
        dc.DrawRectangle(Coast.Staff, Coast.StaffEdge, new Rect(x, top, 20, bottom - top));
        _targets.AddBox("staff", new Rect(x - 2, top - 2, 24, bottom - top + 4));
        for (var m = 0.0; m <= 3.5; m += 0.5)
        {
            var y = LevelY(m);
            var whole = m % 1 == 0;
            dc.DrawLine(Coast.TickPen, new Point(x + (whole ? 0 : 8), y), new Point(x + 20, y));
            if (!whole) continue;
            var text = _labels.Text(m.ToString("0 m", CultureInfo.InvariantCulture), 10.5, TidesDrawing.Text);
            dc.DrawText(text, new Point(x - text.Width - 5, y - (text.Height / 2)));
        }

        // Today's highest and lowest water, across the harbour to the staff, named just before it.
        foreach (var (metres, high) in new[] { (highToday, true), (lowToday, false) })
        {
            var y = LevelY(metres);
            dc.DrawLine(high ? Coast.HighPen : Coast.LowPen, new Point(width * 0.04, y), new Point(x, y));
            var text = LocFormat(high ? "Tides_Label_HighWater" : "Tides_Label_LowWater", metres);
            var formatted = _labels.Text(text, 11, high ? TidesDrawing.High : TidesDrawing.Low);
            dc.DrawText(formatted, new Point(x - 38 - formatted.Width, y - formatted.Height - 2));
            AddTextTarget(high ? "highwater" : "lowwater", formatted, new Point(x - 38 - formatted.Width, y - formatted.Height - 2));
        }

        // The level now, beside the staff.
        var now = LevelY(level);
        var reading = LocFormat("Tides_Label_Level", level, Loc(StageKey(TideGeometry.StageAt(day))));
        var readingText = _labels.Text(reading, 12.5, TidesDrawing.Gold);
        DrawArrowHead(dc, Coast.LevelBrush, new Point(x + 22, now), new Vector(-1, 0), 9);
        _labels.DrawTextPill(dc, reading, new Point(x + 34, now - (readingText.Height / 2) - 2), 12.5, TidesDrawing.Gold);
        AddPillTarget("level", reading, new Point(x + 34, now - (readingText.Height / 2) - 2), 12.5, TidesDrawing.Gold);
    }

    /// <summary>A small sailing boat riding on the water, anchored to the sea floor.</summary>
    private void DrawBoat(PaintContext dc, double width, double waterY, double now, GeometryGroup shade)
    {
        var x = width * 0.17;
        var bob = 2.2 * Math.Sin((now * 1.6) + (x / 38));
        var tilt = 3 * Math.Cos(now * 1.3);
        var y = waterY + bob;

        // The anchor line: slack at low water, nearly straight at high water.
        var seabed = new Point(width * 0.08, GroundY(width, width * 0.08));
        var bow = new Point(x - 30, y + 4);
        var sag = Math.Max(0, 60 - ((seabed.Y - bow.Y) * 0.25));
        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(bow, false);
            ctx.QuadraticBezierTo(new Point(((bow.X + seabed.X) / 2) + sag, (bow.Y + seabed.Y) / 2), seabed);
            ctx.EndFigure(false);
        }

        dc.DrawGeometry(null, Coast.RopePen, line);
        dc.DrawEllipse(Coast.Post, null, seabed, 4, 3);

        var turn = SpaceDrawing.Rotation(tilt, x, y);
        var hull = Polygon(new Point(x - 34, y - 8), new Point(x + 36, y - 8), new Point(x + 26, y + 9), new Point(x - 26, y + 9));
        var sail = Polygon(new Point(x + 3, y - 76), new Point(x + 3, y - 14), new Point(x + 38, y - 14));
        var jib = Polygon(new Point(x - 3, y - 70), new Point(x - 3, y - 14), new Point(x - 26, y - 14));
        dc.PushTransform(turn);
        dc.DrawGeometry(Coast.Hull, null, hull);
        dc.DrawLine(Coast.MastPen, new Point(x, y - 8), new Point(x, y - 78));
        dc.DrawGeometry(Coast.Sail, null, sail);
        dc.DrawGeometry(Coast.Jib, null, jib);
        dc.Pop();
        _targets.AddBox("boat", new Rect(new Point(x - 36, y - 80), new Point(x + 40, y + 10)));
        foreach (var part in new Geometry[] { hull, sail, jib })
        {
            var turned = part.Clone();
            turned.Transform = new MatrixTransform(turn);
            shade.Children.Add(turned);
        }
    }

    private double LighthouseTop => LevelY(4.6) - Math.Min(170, _metre * 3.4);

    /// <summary>A lighthouse on the promenade.</summary>
    private void DrawLighthouse(PaintContext dc, double width, GeometryGroup shade)
    {
        var x = width * 0.90;
        var baseY = LevelY(4.6);
        var topY = LighthouseTop;
        var towerHeight = baseY - topY;
        var tower = Polygon(new Point(x - 17, baseY), new Point(x + 17, baseY), new Point(x + 11, topY), new Point(x - 11, topY));
        dc.DrawGeometry(Coast.Tower, null, tower);
        shade.Children.Add(tower);
        for (var k = 1; k <= 2; k++)
        {
            var bandY = baseY - (towerHeight * k / 3);
            var half = 17 - (6.0 * k / 3);
            dc.DrawGeometry(Coast.Band, null, Polygon(new Point(x - half, bandY), new Point(x + half, bandY), new Point(x + half - 1.5, bandY - 12), new Point(x - half + 1.5, bandY - 12)));
        }

        dc.DrawRectangle(Coast.Lantern, null, new Rect(x - 9, topY - 16, 18, 16));
        var roof = Polygon(new Point(x - 12, topY - 16), new Point(x + 12, topY - 16), new Point(x, topY - 28));
        dc.DrawGeometry(Coast.Band, null, roof);
        shade.Children.Add(roof);
    }

    /// <summary>The lighthouse's lamp: lit at night, its beam sweeping round.</summary>
    private void DrawLamp(PaintContext dc, double width, double daylight, double now)
    {
        if (daylight >= 0.6) return;

        var strength = 1 - (daylight / 0.6);
        var lamp = new Point(width * 0.90, LighthouseTop - 8);
        var sweep = Math.Cos(now * 0.9);
        var end = new Point(lamp.X + (Math.Sign(sweep) * width * 0.5), lamp.Y + 18);
        dc.PushOpacity(0.35 * strength * Math.Abs(sweep));
        dc.DrawGeometry(Coast.Beam, null, Polygon(lamp, new Point(end.X, end.Y - 26), new Point(end.X, end.Y + 26)));
        dc.Pop();
        dc.PushOpacity(strength);
        dc.DrawEllipse(Coast.LampGlow, null, lamp, 22, 22);
        dc.Pop();
    }

    private static class Coast
    {
        public static readonly IBrush Sand = SpaceDrawing.LinearGradient(Color.FromRgb(0xC9, 0xA8, 0x72), Color.FromRgb(0x6E, 0x55, 0x38), 90);
        public static readonly IBrush WetSand = Solid(110, 0x3A, 0x2A, 0x18);
        public static readonly IPen SurfacePen = MakePen(Solid(200, 0xBF, 0xE6, 0xFF), 1.4);
        public static readonly IBrush Post = Solid(255, 0x4A, 0x3B, 0x2C);
        public static readonly IBrush Deck = Solid(255, 0x7A, 0x5E, 0x40);
        public static readonly IPen RailPen = MakePen(Solid(255, 0x7A, 0x5E, 0x40), 2);
        public static readonly IBrush Staff = Solid(235, 0xF4, 0xF1, 0xE8);
        public static readonly IPen StaffEdge = MakePen(Solid(255, 0x30, 0x34, 0x40), 1);
        public static readonly IPen TickPen = MakePen(Solid(255, 0xC6, 0x28, 0x28), 1.6);
        public static readonly IPen HighPen = MakePen(SpaceDrawing.Solid(TidesDrawing.High), 1.2, dashed: true);
        public static readonly IPen LowPen = MakePen(SpaceDrawing.Solid(TidesDrawing.Low), 1.2, dashed: true);
        public static readonly IBrush LevelBrush = SpaceDrawing.Solid(TidesDrawing.Gold);
        public static readonly IPen RopePen = MakePen(Solid(200, 0xD8, 0xCF, 0xB8), 1);
        public static readonly IBrush Hull = Solid(255, 0xB2, 0x3A, 0x3A);
        public static readonly IPen MastPen = MakePen(Solid(255, 0x5A, 0x46, 0x32), 2.4);
        public static readonly IBrush Sail = Solid(245, 0xF6, 0xF3, 0xEA);
        public static readonly IBrush Jib = Solid(230, 0xE6, 0xE1, 0xD4);
        public static readonly IBrush Tower = Solid(255, 0xF2, 0xEE, 0xE6);
        public static readonly IBrush Band = Solid(255, 0xC0, 0x34, 0x34);
        public static readonly IBrush Lantern = Solid(255, 0xFF, 0xE9, 0xA8);
        public static readonly IBrush Beam = Solid(255, 0xFF, 0xF1, 0xC0);

        public static readonly IBrush LampGlow = SpaceDrawing.RadialGradient((Color.FromArgb(230, 255, 240, 190), 0), (Color.FromArgb(0, 255, 220, 140), 1));
        public static readonly IBrush MoonGlow = SpaceDrawing.RadialGradient((Color.FromArgb(60, 220, 230, 255), 0), (Color.FromArgb(0, 220, 230, 255), 1));
        public static readonly IBrush SunDisc = Solid(255, 0xFF, 0xF4, 0xD6);
        public static readonly IBrush LowSunDisc = Solid(255, 0xFF, 0xB0, 0x60);

        public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(170, 255, 244, 214), 0),
            (Color.FromArgb(60, 255, 210, 140), 0.45),
            (Color.FromArgb(0, 255, 190, 110), 1));
    }
}
