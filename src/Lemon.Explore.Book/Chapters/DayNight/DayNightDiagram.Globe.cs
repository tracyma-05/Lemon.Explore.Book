using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.DayNight;

/// <summary>
/// Earth as a globe with sunlight from the left: the line between day and night is the edge of the lit half, a
/// great circle; it passes through the poles only at the equinoxes, and leans away from the axis with the seasons.
/// </summary>
public sealed partial class DayNightDiagram
{
    private (Point Center, double Radius) GlobeLayout()
    {
        var top = TitleSpace;
        var bottom = Math.Max(top + 40, ActualHeight - ToolbarSpace);
        var centerY = (top + bottom) / 2;

        // The axis and its labels reach 1.32 radii above the centre; keep them clear of the title.
        var radius = Math.Max(20, Math.Min(Math.Min(ActualWidth * 0.28, ((bottom - top) / 2) - 24), (centerY - 112) / 1.38));
        return (new Point((ActualWidth / 2) + (ActualWidth * 0.05), centerY), radius);
    }

    private GlobeCamera GlobeCameraFor(SunPosition sun) => GlobeCamera.SunFixed(sun, ViewTowardSun, ViewElevation);

    private static Point OnScreen(Point center, double radius, (double X, double Y, bool Front) p) =>
        new(center.X + (p.X * radius), center.Y - (p.Y * radius));

    private Point? GlobeScreenPoint(GeoPoint place)
    {
        var (center, radius) = GlobeLayout();
        var projected = GlobeCameraFor(DayNightGeometry.Sun(Utc)).Project(place);
        return projected.Front ? OnScreen(center, radius, projected) : null;
    }

    private GeoPoint? GlobePointAt(Point point)
    {
        var (center, radius) = GlobeLayout();
        var x = (point.X - center.X) / radius;
        var y = (center.Y - point.Y) / radius;
        var r2 = (x * x) + (y * y);
        if (r2 > 1) return null;

        var camera = GlobeCameraFor(DayNightGeometry.Sun(Utc));
        var p = (camera.Right * x) + (camera.Up * y) + (camera.Toward * Math.Sqrt(1 - r2));
        return new GeoPoint(
            DayNightGeometry.ToDegrees(Math.Asin(Math.Clamp(p.Z, -1, 1))),
            DayNightGeometry.Normalize180(DayNightGeometry.ToDegrees(Math.Atan2(p.Y, p.X)) + camera.ReferenceLongitude));
    }

    private void DrawGlobe(PaintContext dc, double dpi, DateTime utc, SunPosition sun, double now)
    {
        var (center, radius) = GlobeLayout();
        var camera = GlobeCameraFor(sun);

        DrawSunlight(dc, center, radius);

        _globe.Render((int)Math.Ceiling(2 * radius * dpi), camera, sun);
        if (_globe.Bitmap is { } bitmap)
        {
            dc.DrawImage(bitmap, new Rect(center.X - radius, center.Y - radius, 2 * radius, 2 * radius));
        }

        dc.DrawEllipse(GlobePaint.Atmosphere, null, center, radius * 1.06, radius * 1.06);
        dc.DrawEllipse(null, GlobePaint.Rim, center, radius, radius);

        // The equator, the tropics and the polar circles, on the near side only.
        foreach (var (latitude, pen) in new[]
                 {
                     (0.0, GlobePaint.Equator), (SeasonsTilt, GlobePaint.Tropic), (-SeasonsTilt, GlobePaint.Tropic),
                     (90 - SeasonsTilt, GlobePaint.Polar), (-(90 - SeasonsTilt), GlobePaint.Polar),
                 })
        {
            var points = Enumerable.Range(0, 120).Select(i => new GeoPoint(latitude, -180 + (3.0 * i))).ToArray();
            DrawOnSphere(dc, camera, center, radius, points, pen, null);
        }

        var line = DayNightGeometry.Circle(sun, 0, 240);
        if (Highlight == "terminator") DrawOnSphere(dc, camera, center, radius, line, ExploreTargets.GlowPen(now, 10), null);
        DrawOnSphere(dc, camera, center, radius, line, DayNightPaint.DayNightLine, GlobePaint.HiddenLine);

        DrawAxis(dc, camera, center, radius, sun);
        DrawGlobeCities(dc, camera, center, radius, sun);

        var place = camera.Project(new GeoPoint(PlaceLatitude, PlaceLongitude));
        if (place.Front)
        {
            var at = OnScreen(center, radius, place);
            dc.DrawEllipse(null, DayNightPaint.PlaceOutline, at, 7, 7);
            dc.DrawEllipse(null, DayNightPaint.PlaceRing, at, 6, 6);
            dc.DrawEllipse(DayNightPaint.PlaceFill, DayNightPaint.PlaceOutline, at, 2.8, 2.8);
            var time = TimeZoneInfo.ConvertTimeFromUtc(utc, ZoneOf(PlaceCity, PlaceLongitude));
            _labels.DrawTextPill(dc, $"{PlaceName()} {ClockText(time)}", new Point(at.X + 10, at.Y - 24), 12, DayNightPaint.Gold);
        }

        DrawGlobeLabels(dc, camera, center, radius, sun, line);
    }

    /// <summary>Parallel sunlight coming in from the left and stopping at the globe.</summary>
    private void DrawSunlight(PaintContext dc, Point center, double radius)
    {
        var start = Math.Max(24, center.X - radius - 220);
        dc.DrawEllipse(GlobePaint.SunGlow, null, new Point(start - 40, center.Y), radius * 0.9, radius * 0.9);

        for (var k = -3; k <= 3; k++)
        {
            var dy = k * radius * 0.28;
            var end = center.X - Math.Sqrt(Math.Max(0, (radius * radius) - (dy * dy))) - 6;
            var y = center.Y + dy;
            dc.DrawLine(GlobePaint.Ray, new Point(start, y), new Point(end, y));
            DrawArrowHead(dc, GlobePaint.RayArrow, new Point(end, y), new Vector(1, 0), 7);
        }

        var text = _labels.Get("DayNight_Label_Sunlight", 12, DayNightPaint.Gold, true);
        dc.DrawText(text, new Point(start, center.Y - (radius * 0.84) - text.Height - 4));
        AddLabelTarget("sunlight", new Point(start, center.Y - (radius * 0.84) - text.Height - 4), text.Width, text.Height);
    }

    /// <summary>A curve on the unit sphere: solid where it faces us, and with <paramref name="back"/> behind.</summary>
    private static void DrawOnSphere(PaintContext dc, GlobeCamera camera, Point center, double radius, IReadOnlyList<GeoPoint> points, IPen front, IPen? back)
    {
        var frontGeometry = new StreamGeometry();
        var backGeometry = new StreamGeometry();
        using (var frontCtx = frontGeometry.Open())
        using (var backCtx = backGeometry.Open())
        {
            var previous = camera.Project(points[^1]);
            bool? drawingFront = null;
            bool frontOpen = false, backOpen = false;
            for (var i = 0; i < points.Count; i++)
            {
                var p = camera.Project(points[i]);
                var from = OnScreen(center, radius, previous);
                var to = OnScreen(center, radius, p);
                var isFront = previous.Front && p.Front;
                var ctx = isFront ? frontCtx : backCtx;
                if (drawingFront != isFront || i == 0)
                {
                    // Each geometry ends its open stretch before starting another.
                    if (isFront ? frontOpen : backOpen) ctx.EndFigure(false);
                    ctx.BeginFigure(from, false);
                    if (isFront) frontOpen = true;
                    else backOpen = true;
                }

                ctx.LineTo(to);
                drawingFront = isFront;
                previous = p;
            }

            if (frontOpen) frontCtx.EndFigure(false);
            if (backOpen) backCtx.EndFigure(false);
        }
        dc.DrawGeometry(null, front, frontGeometry);
        if (back is null) return;
        dc.DrawGeometry(null, back, backGeometry);
    }

    /// <summary>Earth's axis beyond the poles, and the angle between it and the edge of the lit half.</summary>
    private void DrawAxis(PaintContext dc, GlobeCamera camera, Point center, double radius, SunPosition sun)
    {
        var north = camera.Project(new Vec3(0, 0, 1));
        var length = Math.Sqrt((north.X * north.X) + (north.Y * north.Y));
        if (length < 0.15) return;

        foreach (var sign in new[] { 1, -1 })
        {
            // A pole behind the globe: the axis shows only once it clears the globe's outline.
            var poleFront = sign > 0 ? north.Front : !north.Front;
            var from = poleFront ? 1 : 1 / length;
            var direction = new Vector(north.X * sign, -north.Y * sign);

            // Long enough to read above the globe; below it, stop short of the toolbar.
            var to = 1.32;
            if (direction.Y > 0) to = Math.Min(to, (ActualHeight - ToolbarSpace + 6 - center.Y) / (direction.Y * radius));
            if (from < to)
            {
                dc.DrawLine(GlobePaint.Axis, center + (direction * radius * from), center + (direction * radius * to));
            }

            if (sign > 0)
            {
                var tip = center + (direction * radius * 1.32);
                var pole = _labels.Get("DayNight_Label_NorthPole", 11.5, DayNightPaint.Text, true);
                dc.DrawText(pole, new Point(tip.X - (pole.Width / 2), tip.Y - pole.Height - 3));
                _targets.Add("axis", new Point(tip.X, tip.Y - (pole.Height / 2)), Math.Max(14, (pole.Width / 2) + 2));
            }
        }

        // Straight up on screen lies in the plane of the day-night circle, at right angles to the sunlight.
        if (Math.Abs(sun.Declination) < 1) return;
        var up = new Vector(0, -1);
        var axis = new Vector(north.X, -north.Y);
        axis.Normalize();
        dc.DrawLine(GlobePaint.CircleEdge, center + (up * radius * 1.02), center + (up * radius * 1.3));

        var arcRadius = radius * 1.2;
        var start = center + (up * arcRadius);
        var end = center + (axis * arcRadius);
        var sweep = SpaceDrawing.AngleBetween(up, axis);
        var arc = new StreamGeometry();
        using (var ctx = arc.Open())
        {
            ctx.BeginFigure(start, false);
            ctx.ArcTo(end, new Size(arcRadius, arcRadius), 0, false, sweep > 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);

            ctx.EndFigure(false);
        }
        dc.DrawGeometry(null, GlobePaint.AngleArc, arc);

        var label = LocFormat("DayNight_Label_AxisAngle", Math.Abs(sun.Declination));
        var text = _labels.Text(label, 11, DayNightPaint.Gold);
        var middle = up + axis;
        if (middle.Length > 1e-6) middle.Normalize();
        var at = center + (middle * radius * 1.34);
        _labels.DrawTextPill(dc, label, new Point(at.X - ((text.Width + 12) / 2), at.Y - text.Height), 11, DayNightPaint.Gold);
        AddLabelTarget("angle", new Point(at.X - ((text.Width + 12) / 2), at.Y - text.Height), text.Width + 12, text.Height + 4);
    }

    private void DrawGlobeCities(PaintContext dc, GlobeCamera camera, Point center, double radius, SunPosition sun)
    {
        foreach (var city in WorldCities.All)
        {
            var projected = camera.Project(city.Location);
            if (!projected.Front) continue;

            var at = OnScreen(center, radius, projected);
            var altitude = DayNightGeometry.Altitude(sun, city.Latitude, city.Longitude);
            MapPainter.DrawCityLight(dc, at, DayNightPaint.Darkness(altitude), 0.8);
            dc.DrawEllipse(DayNightPaint.CityDot, DayNightPaint.CityOutline, at, 2.2, 2.2);
        }
    }

    /// <summary>Day and night on their halves, the sunrise and sunset halves of the circle, and the polar day and night.</summary>
    private void DrawGlobeLabels(PaintContext dc, GlobeCamera camera, Point center, double radius, SunPosition sun, GeoPoint[] line)
    {
        var s = Vec3.FromGeo(sun.Declination, sun.SubsolarLongitude, camera.ReferenceLongitude);
        foreach (var (direction, key, color) in new[] { (s, "DayNight_Label_Day", DayNightPaint.Gold), (s * -1, "DayNight_Label_Night", DayNightPaint.Blue) })
        {
            var middle = direction + camera.Toward;
            if (middle.Length < 0.2) continue;
            var projected = camera.Project(middle.Normalized());
            if (!projected.Front) continue;
            var at = OnScreen(center, radius, projected);
            var text = _labels.Get(key, 18, color, true);
            dc.DrawText(_labels.Get(key, 18, Shadow, true), new Point(at.X - (text.Width / 2) + 1, at.Y - (text.Height / 2) + 1));
            dc.DrawText(text, new Point(at.X - (text.Width / 2), at.Y - (text.Height / 2)));
        }

        // Each half of the circle is labelled where it faces us most.
        foreach (var (morning, key) in new[] { (true, "DayNight_Label_SunriseLine"), (false, "DayNight_Label_SunsetLine") })
        {
            var best = default(GeoPoint);
            var bestFacing = 0.25;
            var found = false;
            foreach (var point in line)
            {
                if ((DayNightGeometry.HourAngle(sun, point.Longitude) < 0) != morning) continue;
                var facing = Vec3.FromGeo(point.Latitude, point.Longitude, camera.ReferenceLongitude).Dot(camera.Toward);
                if (facing > bestFacing)
                {
                    best = point;
                    bestFacing = facing;
                    found = true;
                }
            }

            if (!found) continue;
            var at = OnScreen(center, radius, camera.Project(best));
            _labels.DrawPill(dc, key, new Point(at.X + 8, at.Y - 10), 11.5, DayNightPaint.Gold);
            var pill = _labels.Get(key, 11.5, DayNightPaint.Gold);
            AddLabelTarget(morning ? "sunriseline" : "sunsetline", new Point(at.X + 8, at.Y - 10), pill.Width + 12, pill.Height + 4);
        }

        if (Math.Abs(sun.Declination) < 1) return;
        foreach (var (latitude, key) in new[] { (90.0, sun.Declination > 0 ? "DayNight_Label_PolarDay" : "DayNight_Label_PolarNight"), (-90.0, sun.Declination > 0 ? "DayNight_Label_PolarNight" : "DayNight_Label_PolarDay") })
        {
            // Inside the polar circle, on the side facing us.
            var circleLatitude = Math.Sign(latitude) * (90 - (Math.Abs(sun.Declination) / 2));
            var facingLongitude = DayNightGeometry.ToDegrees(Math.Atan2(camera.Toward.Y, camera.Toward.X)) + camera.ReferenceLongitude;
            var projected = camera.Project(new GeoPoint(circleLatitude, facingLongitude));
            if (!projected.Front) continue;
            var at = OnScreen(center, radius, projected);
            var text = _labels.Get(key, 11, DayNightPaint.Text);
            _labels.DrawPill(dc, key, new Point(at.X - ((text.Width + 12) / 2), at.Y - (text.Height / 2)), 11, DayNightPaint.Text);
            AddLabelTarget(key == "DayNight_Label_PolarDay" ? "polarday" : "polarnight",
                new Point(at.X - ((text.Width + 12) / 2), at.Y - (text.Height / 2)), text.Width + 12, text.Height + 4);
        }
    }

    private static class GlobePaint
    {
        public static readonly IBrush Atmosphere = SpaceDrawing.RadialGradient(
            (Color.FromArgb(0, 0x8E, 0xC5, 0xFF), 0.92),
            (Color.FromArgb(110, 0x8E, 0xC5, 0xFF), 0.945),
            (Color.FromArgb(0, 0x8E, 0xC5, 0xFF), 1));

        public static readonly IPen Rim = MakePen(Solid(90, 0x8E, 0xC5, 0xFF), 1);
        public static readonly IPen Equator = MakePen(Solid(110, 255, 255, 255), 1);
        public static readonly IPen Tropic = MakePen(Solid(110, 0xFF, 0xD3, 0x6B), 1, dashed: true);
        public static readonly IPen Polar = MakePen(Solid(120, 0x8E, 0xC5, 0xFF), 1, dashed: true);
        public static readonly IPen HiddenLine = MakePen(Solid(70, 0xFF, 0xD3, 0x6B), 1, dashed: true);
        public static readonly IPen Axis = MakePen(Solid(235, 255, 255, 255), 1.6);
        public static readonly IPen CircleEdge = MakePen(Solid(170, 0xFF, 0xD3, 0x6B), 1.2, dashed: true);
        public static readonly IPen AngleArc = MakePen(Solid(220, 0xFF, 0xD3, 0x6B), 1.3);

        public static readonly IPen Ray = MakePen(SpaceDrawing.LinearGradient(Color.FromArgb(20, 255, 226, 150), Color.FromArgb(150, 255, 226, 150), 0), 1.2);

        public static readonly IBrush RayArrow = Solid(170, 255, 226, 150);

        public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(90, 255, 220, 140), 0),
            (Color.FromArgb(0, 255, 200, 110), 1));
    }
}
