using Avalonia;
using Lemon.Explore.Book.Controls.Explore;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.IceAges;

/// <summary>
/// The ice-sheet view: a slice along about 88° W from the Gulf of Mexico (24° N) to the Arctic (82° N) with heights
/// drawn ~600× taller than they are. The Laurentide ice sheet's dome grows and shrinks with the model's ice; the summer
/// snow line rises and falls with the summer sunshine; the ground under the ice is pressed down, and catches up only
/// thousands of years later; the sea at the southern end falls as water is locked up in ice.
/// </summary>
public sealed partial class IceAgeDiagram
{
    private const double SliceSouth = 24;
    private const double SliceNorth = 82;

    /// <summary>The dome's height at the last glacial maximum, metres.</summary>
    internal const double DomeHeight = 3300;

    /// <summary>The ground sinks by about this share of the ice on it (ice is about a third as dense as rock).</summary>
    private const double SinkShare = 0.28;

    /// <summary>The ground along the slice today, metres above sea level (latitude, height), roughly.</summary>
    private static readonly (double Lat, double Height)[] Relief =
    [
        (24, -320), (26, -150), (27.5, -110), (29, -40), (29.9, 2), (31, 30), (35, 120), (39, 200), (42, 250), (45, 290),
        (50, 360), (53, 300), (55.5, 80), (56.5, -100), (61.5, -110), (63, 60), (65, 180), (69, 240), (70.5, -200),
        (72.5, -220), (74, 250), (78, 420), (82, 380),
    ];

    /// <summary>Places along the slice: latitude and the name's key.</summary>
    private static readonly (double Lat, string Key)[] SlicePlaces =
    [
        (30.5, "Ice_Place_Gulf"),
        (38.6, "Ice_Place_StLouis"),
        (41.9, "Ice_Place_Chicago"),
        (49.5, "Ice_Place_Shield"),
        (59, "Ice_Place_Hudson"),
        (76, "Ice_Place_Arctic"),
    ];

    /// <summary>The ice sheet along the slice: its dome's place, its southern and northern edges (latitudes) and its top (m).</summary>
    internal readonly record struct SheetShape(double Centre, double South, double North, double Height)
    {
        public bool Exists => Height > 50 && North - South > 0.4;

        /// <summary>
        /// The ice's thickness (m) at <paramref name="lat"/>: a dome h = H (1 − |x|^1.6)^0.6. Ice spreading under its own
        /// weight gives about (1 − |x|^(4/3))^(3/8) (Vialov), whose edges are near-vertical once heights are drawn 600×
        /// taller; this one is a little gentler at the edges.
        /// </summary>
        public double Thickness(double lat)
        {
            if (!Exists || lat <= South || lat >= North) return 0;
            var x = lat < Centre ? (Centre - lat) / (Centre - South) : (lat - Centre) / (North - Centre);
            return Height * Math.Pow(Math.Max(0, 1 - Math.Pow(Math.Clamp(x, 0, 1), 1.6)), 0.6);
        }
    }

    /// <summary>The sheet for a given amount of ice (share of the last maximum's).</summary>
    internal static SheetShape Sheet(double share)
    {
        var g = PolarIceMap.Growth(share);
        var centre = 62 - (2 * g);
        return new SheetShape(centre, centre - (21 * g), centre + (13 * g), DomeHeight * Math.Sqrt(g));
    }

    /// <summary>The summer snow line's height (m): above it the winter's snow outlasts the summer. Lower when summers are weak.</summary>
    internal static double SnowLine(double sunshine) => Math.Clamp(1800 + (28 * (sunshine - 470)), 300, 4800);

    /// <summary>The ground's height today along the slice, metres, read off <see cref="Relief"/>.</summary>
    internal static double GroundHeight(double lat)
    {
        if (lat <= Relief[0].Lat) return Relief[0].Height;
        for (var i = 1; i < Relief.Length; i++)
        {
            if (lat > Relief[i].Lat) continue;
            var (a, b) = (Relief[i - 1], Relief[i]);
            var f = (lat - a.Lat) / (b.Lat - a.Lat);
            f = f * f * (3 - (2 * f));
            return a.Height + ((b.Height - a.Height) * f);
        }

        return Relief[^1].Height;
    }

    /// <summary>How far the ground is pressed down (m) at <paramref name="lat"/> by the ice it has caught up with.</summary>
    internal static double Sinking(IceMoment moment, double lat) =>
        SinkShare * Sheet(moment.GroundShare).Thickness(lat);

    private readonly record struct SheetLayout(Rect Section, double SeaY, double PerMetre, double LatLeft, double LatRight)
    {
        public double X(double lat) => Section.Left + ((lat - LatLeft) / (LatRight - LatLeft) * Section.Width);

        public double Y(double metres) => SeaY - (metres * PerMetre);

        public Point P(double lat, double metres) => new(X(lat), Y(metres));
    }

    private SheetLayout LayOutSheet(Rect stage, Rect strip)
    {
        var section = new Rect(stage.Left + 24, stage.Top + 6, Math.Max(120, stage.Width - 48), Math.Max(120, strip.Top - 34 - stage.Top - 6));
        var seaY = section.Top + (section.Height * 0.72);
        var perMetre = (seaY - section.Top - 40) / 4400;
        return new SheetLayout(section, seaY, perMetre, SliceSouth, SliceNorth);
    }

    private void DrawSheetMode(PaintContext dc, double now)
    {
        var stage = Stage();
        var strip = StripRect(stage);
        var layout = LayOutSheet(stage, strip);
        var section = layout.Section;
        var moment = IceRecord.Instance.At(TimeKa);
        var sheet = Sheet(moment.IceShare);
        var trend = IceRecord.Instance.IceTrend(TimeKa);

        dc.PushClip(new RectangleGeometry(section, 10, 10));
        dc.DrawRectangle(Ink.Sky, null, section);

        // The sea, at its level for the time, then the ground over it (so low ground below sea level is sea: Hudson Bay).
        var seaY = layout.Y(moment.SeaLevel);
        dc.DrawRectangle(Ink.Water, null, new Rect(section.Left, seaY, section.Width, Math.Max(0, section.Bottom - seaY)));
        dc.DrawLine(FadedPen(Ink.Dim, 0.5, 1, dashed: true), new Point(section.Left, layout.SeaY), new Point(section.Right, layout.SeaY));

        double Surface(double lat) => GroundHeight(lat) - (ShowGround ? Sinking(moment, lat) : 0);
        var ground = new List<Point>();
        for (var lat = SliceSouth; lat <= SliceNorth + 1e-9; lat += 0.25) ground.Add(layout.P(lat, Surface(lat)));
        ground.Add(new Point(section.Right, section.Bottom));
        ground.Add(new Point(section.Left, section.Bottom));
        dc.DrawGeometry(Ink.Rock, null, Line(ground, closed: true, filled: true));
        dc.DrawGeometry(null, FadedPen(Ink.Earthy, 0.9, 1.6), Line(ground.Take(ground.Count - 2)));

        // The ice sheet on the (sunk) ground.
        Point? top = null;
        if (sheet.Exists)
        {
            var ice = new List<Point>();
            var bed = new List<Point>();
            for (var lat = sheet.South; lat <= sheet.North + 1e-9; lat += 0.2)
            {
                var surface = Surface(lat);
                ice.Add(layout.P(lat, surface + sheet.Thickness(lat)));
                bed.Add(layout.P(lat, surface));
            }

            var shape = ice.Concat(Enumerable.Reverse(bed)).ToList();
            if (Glows("sheet")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 8), Line(ice));
            dc.DrawGeometry(Ink.IceFill, FadedPen(Ink.IceEdge, 0.9, 1.2), Line(shape, closed: true, filled: true));
            var summit = layout.P(sheet.Centre, Surface(sheet.Centre) + sheet.Height);
            top = summit;
            _targets.Add("sheet", summit + new Vector(0, 18), 16);
        }

        // Where the ground is pressed down, over the ice: the old surface dashed, the sunk part tinted.
        var deepest = 0.0;
        var deepestLat = sheet.Centre;
        if (ShowGround)
        {
            var sunk = new List<Point>();
            var old = new List<Point>();
            for (var lat = SliceSouth; lat <= SliceNorth + 1e-9; lat += 0.25)
            {
                var s = Sinking(moment, lat);
                if (s > deepest)
                {
                    deepest = s;
                    deepestLat = lat;
                }

                if (s < 5) continue;
                old.Add(layout.P(lat, GroundHeight(lat)));
                sunk.Add(layout.P(lat, GroundHeight(lat) - s));
            }

            if (old.Count > 2)
            {
                var band = old.Concat(Enumerable.Reverse(sunk)).ToList();
                dc.DrawGeometry(Ink.Pressed, null, Line(band, closed: true, filled: true));
                dc.DrawGeometry(null, FadedPen(Ink.Orange, 0.8, 1.2, dashed: true), Line(old));
                if (deepest > 40) _targets.Add("ground", layout.P(deepestLat, GroundHeight(deepestLat) - (deepest * 0.5)), 12);
            }
        }

        // The summer snow line: where it lies below the ice's top, snow outlasts the summer and the ice grows.
        var snowLine = SnowLine(moment.Sunshine);
        var snowY = layout.Y(snowLine);
        if (Glows("snowline")) dc.DrawLine(ExploreTargets.GlowPen(now, 7), new Point(section.Left, snowY), new Point(section.Right, snowY));
        dc.DrawLine(FadedPen(Ink.Cyan, 0.85, 1.4, dashed: true), new Point(section.Left, snowY), new Point(section.Right, snowY));
        var snowLabel = LocFormat("Ice_Sheet_SnowLine", Number(Math.Round(snowLine / 10) * 10, "#,##0"));
        DrawTargetPill(dc, snowLabel, new Point(section.Right - 120, snowY - 12), Ink.Cyan, "snowline", 10.5);

        if (ShowArrows) DrawSheetArrows(dc, layout, sheet, moment, trend, Surface);

        // Places along the slice and the coast.
        if (ShowLabels)
        {
            foreach (var (lat, key) in SlicePlaces)
            {
                var at = layout.P(lat, Surface(lat));
                if (key == "Ice_Place_Hudson")
                {
                    DrawTargetPill(dc, Loc(key), new Point(at.X, Math.Min(section.Bottom - 12, layout.SeaY + 34)), Ink.Steel, "hudson", 10);
                    continue;
                }

                dc.DrawLine(FadedPen(Ink.Dim, 0.6, 1), at, at + new Vector(0, 8));
                DrawText(dc, Loc(key), at + new Vector(0, 18), Ink.Dim, 9.5, TextAnchor.Center);
            }
        }
        else
        {
            _targets.Add("hudson", layout.P(59, Surface(59) - 40), 10);
        }

        var coast = SliceSouth;
        for (var lat = SliceSouth; lat < 40; lat += 0.05)
        {
            if (Surface(lat) >= moment.SeaLevel)
            {
                coast = lat;
                break;
            }
        }

        var coastAt = layout.P(coast, moment.SeaLevel);
        dc.DrawEllipse(FadedBrush(Ink.Cyan, 1), null, coastAt, 3.5, 3.5);
        DrawTargetPill(dc, LocFormat("Ice_Sheet_Coast", Number(moment.SeaLevel, "0")), coastAt + new Vector(36, -22), Ink.Cyan, "coast", 10.5);
        _targets.Add("coast", coastAt, 9);

        // Latitudes along the bottom.
        for (var lat = 30; lat <= 80; lat += 10)
        {
            var x = layout.X(lat);
            DrawText(dc, LocFormat("Ice_Value_Lat", lat), new Point(x, section.Bottom - 9), Ink.Dim, 9.5, TextAnchor.Center);
        }

        dc.Pop();
        dc.DrawRoundedRectangle(null, Ink.Frame, section, 10, 10);

        // The air's carbon dioxide and the world's temperature in this model.
        var gauges = LocFormat("Ice_Sheet_Co2", Number(moment.Co2, "0"), (moment.Temperature >= 0 ? "+" : "−") + Number(Math.Abs(moment.Temperature), "0.0"));
        var width = _labels.Text(gauges, 11.5, Ink.Text).Width + 12;
        DrawTargetPill(dc, gauges, new Point(section.Right - 14 - (width / 2), section.Top + 18), moment.Co2 < 230 ? Ink.Cyan : Ink.Gold, "co2", 11.5);

        if (top is { } summit2 && ShowLabels)
        {
            var heightText = LocFormat("Ice_Sheet_Height", Number(sheet.Height / 1000, "0.0"));
            DrawPill(dc, heightText, summit2 + new Vector(0, -16), Ink.Ice, 10.5);
        }

        DrawStrip(dc, strip, "ice", k => IceRecord.Instance.At(k).IceShare, 0, 1.2, Ink.Ice, "Ice_Strip_Ice");
    }

    /// <summary>Sunlight coming in and bouncing off the ice, snow falling on the dome, and melt running off its southern edge.</summary>
    private void DrawSheetArrows(PaintContext dc, SheetLayout layout, SheetShape sheet, IceMoment moment, double trend, Func<double, double> surface)
    {
        var section = layout.Section;

        // Midsummer noon sunlight at 60° N comes in about 53° high, from the south; brighter when summers are strong.
        var strength = Math.Clamp((moment.Sunshine - 420) / 150, 0.15, 1);
        var direction = new Vector(Math.Cos(IceAgeModel.Rad(53)), Math.Sin(IceAgeModel.Rad(53)));
        var placed = false;
        foreach (var lat in new[] { 36.0, 44, 52, 60, 68 })
        {
            var h = surface(lat) + sheet.Thickness(lat);
            var hit = layout.P(lat, Math.Max(h, moment.SeaLevel));
            var from = hit - (direction * 90);
            if (from.Y < section.Top + 4) from = hit - (direction * ((hit.Y - section.Top - 4) / direction.Y));
            DrawArrow(dc, from, hit, Ink.Gold, 2.2, 0.35 + (0.6 * strength));
            if (!placed)
            {
                _targets.Add("sunrays", from + ((hit - from) * 0.4), 10);
                placed = true;
            }

            // Ice and snow send most of it back up; bare ground and sea keep most of it.
            var onIce = sheet.Thickness(lat) > 30;
            if (onIce)
            {
                var back = hit + new Vector(direction.X * 56, -direction.Y * 56);
                DrawArrow(dc, hit, back, Ink.Ice, 2, 0.85);
                _targets.Add("reflect", hit + ((back - hit) * 0.6), 9);
            }
            else
            {
                dc.DrawEllipse(FadedBrush(Ink.Orange, 0.35 * strength), null, hit, 7, 4);
            }
        }

        if (!sheet.Exists) return;

        // Snow over the dome's middle: more while the ice grows.
        var snow = trend > 0 ? 28 : 12;
        var span = Math.Max(1, sheet.North - sheet.South) * 0.7;
        var mid = sheet.Centre;
        var topY = layout.Y(surface(mid) + sheet.Height);
        for (var i = 0; i < snow; i++)
        {
            var lat = mid - (span / 2) + (span * Fraction(i * 0.6180339887));
            var fall = Fraction((i * 0.7548776662) + (_drift * 0.25));
            var groundY = layout.Y(surface(lat) + sheet.Thickness(lat));
            var y = section.Top + 28 + ((groundY - section.Top - 28) * fall);
            var x = layout.X(lat) + (Math.Sin((_drift * 1.3) + i) * 3);
            dc.DrawEllipse(FadedBrush(Ink.Ice, 0.85), null, new Point(x, y), 1.8, 1.8);
        }

        _targets.Add("snowfall", new Point(layout.X(mid - (span / 4)), (section.Top + 28 + topY) / 2), 14);

        // Melt at the southern edge: drips and a stream, stronger while the ice shrinks.
        var edge = layout.P(sheet.South + 0.15, surface(sheet.South + 0.15));
        var melt = trend < 0 ? Math.Clamp(-trend * 40, 0.4, 1) : 0.3;
        for (var i = 0; i < 6; i++)
        {
            var f = Fraction((i / 6.0) + (_drift * 0.6));
            var p = edge + new Vector(-6 - (f * 34 * melt), f * 6);
            dc.DrawEllipse(FadedBrush(Ink.Blue, 0.9 * (1 - f)), null, p, 2.2, 2.2);
        }

        dc.DrawLine(FadedPen(Ink.Blue, 0.7, 2 + (2 * melt)), edge, edge + new Vector(-44 * melt - 8, 7));
        _targets.Add("melt", edge + new Vector(-18, 2), 11);
    }

    private static double Fraction(double x) => x - Math.Floor(x);
}
