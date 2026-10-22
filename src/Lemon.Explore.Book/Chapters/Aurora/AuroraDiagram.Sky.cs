using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Aurora;

/// <summary>
/// Sky mode, three pictures of one moment for the shared location:
/// <list type="bullet">
/// <item>The globe from above the magnetic pole's side, with the real coastlines and night, the auroral oval for the Kp
/// (fixed towards the Sun while Earth turns under it), the viewer, and how far south of the oval it can still be seen.</item>
/// <item>A slice along the magnetic meridian from the viewer to the curtain, to scale with Earth's curve: the curtain
/// coloured by height, the line of sight and the horizon.</item>
/// <item>The sky the viewer sees facing the oval: the curtain where the slice puts it, folded and shimmering.</item>
/// </list>
/// </summary>
public sealed partial class AuroraDiagram
{
    /// <summary>Lowest and highest heights the curtain is drawn between, km.</summary>
    private const double CurtainBottom = 75;
    private const double CurtainTop = 600;

    private readonly GlobeRenderer _globe = new();
    private WriteableBitmap? _skyBitmap;
    private int[] _skyPixels = [];

    /// <summary>The curtain's colour by height in 2 km steps for the energy last used: premultiplied light, not yet tone-mapped.</summary>
    private (double Energy, float[] R, float[] G, float[] B) _lightTable = (double.NaN, [], [], []);

    private static readonly (double X, double Y, double Brightness)[] SkyStars = MakeStars();

    private static (double, double, double)[] MakeStars()
    {
        var rnd = new Random(11);
        return Enumerable.Range(0, 260).Select(_ => (rnd.NextDouble(), rnd.NextDouble(), Math.Pow(rnd.NextDouble(), 3))).ToArray();
    }

    private (double Energy, double Top) _visibleTop = (double.NaN, 0);

    /// <summary>The highest the curtain glows for the electrons' energy now, km (kept per energy).</summary>
    private double VisibleTop
    {
        get
        {
            if (_visibleTop.Energy != EnergyKeV) _visibleTop = (EnergyKeV, AuroraModel.VisibleTop(EnergyKeV));
            return _visibleTop.Top;
        }
    }

    /// <summary>The moment shown.</summary>
    private DateTime SkyUtc => DateTime.SpecifyKind(SkyBase, DateTimeKind.Utc).AddHours(_hour);

    /// <summary>
    /// Everything the three pictures share: the viewer's magnetic latitude and local time, the oval's edges there, the
    /// ground distance to the bright curtain (km) and whether it lies towards the pole (else the viewer is past the oval).
    /// </summary>
    internal readonly record struct SkyGeometry(
        double Mlat,
        double Mlt,
        double Equatorward,
        double Poleward,
        double CurtainKm,
        bool TowardPole,
        double SunAltitude,
        double Brightness);

    internal static SkyGeometry Geometry(DateTime utc, double latitude, double longitude, double kp)
    {
        var (mlat, _) = AuroraModel.ToMagnetic(latitude, longitude);
        var mlt = AuroraModel.MagneticLocalTime(utc, latitude, longitude);
        var eq = AuroraModel.OvalEquatorward(kp, mlt);
        var pw = AuroraModel.OvalPoleward(kp, mlt);
        // The bright arc sits in the oval's equatorward part.
        var arc = eq + ((pw - eq) * 0.25);
        var distance = (arc - Math.Abs(mlat)) * AuroraModel.KmPerDegree;
        var sun = DayNightGeometry.Altitude(utc, latitude, longitude);
        return new SkyGeometry(mlat, mlt, eq, pw, Math.Abs(distance), distance >= 0, sun, AuroraModel.OvalBrightness(kp, mlt));
    }

    private void DrawSkyMode(PaintContext dc)
    {
        var stage = Stage();
        var utc = SkyUtc;
        var geometry = Geometry(utc, Latitude, Longitude, Kp);
        var now = _clock.Elapsed.TotalSeconds;

        var globeSize = Math.Max(120, Math.Min(stage.Height - 10, stage.Width * 0.42));
        var globeRect = new Rect(stage.Left + 18, stage.Top + ((stage.Height - globeSize) / 2), globeSize, globeSize);
        var right = new Rect(globeRect.Right + 26, stage.Top + 4, Math.Max(120, stage.Right - globeRect.Right - 44), stage.Height - 8);
        var sliceRect = new Rect(right.Left, right.Top, right.Width, right.Height * 0.53);
        var viewRect = new Rect(right.Left, sliceRect.Bottom + 12, right.Width, right.Height - sliceRect.Height - 12);

        DrawGlobe(dc, globeRect, utc, geometry, now);
        DrawSlice(dc, sliceRect, geometry, now);
        DrawSkyView(dc, viewRect, geometry, now);
    }

    #region the globe

    private void DrawGlobe(PaintContext dc, Rect rect, DateTime utc, SkyGeometry geometry, double now)
    {
        var hemisphere = geometry.Mlat >= 0 ? 1 : -1;
        var pole = AuroraModel.FromMagnetic(hemisphere * 90, 0);
        var you = new GeoPoint(Latitude, Longitude);

        // Looking down on a point between the magnetic pole and the viewer, so both (and the oval) are in sight.
        var mlon = AuroraModel.ToMagnetic(Latitude, Longitude).Longitude;
        var colatitude = 90 - Math.Abs(geometry.Mlat);
        var center = AuroraModel.FromMagnetic(hemisphere * (90 - (colatitude * 0.45)), mlon);
        var camera = GlobeCamera.Above(Math.Round(center.Latitude, 1), Math.Round(center.Longitude, 1));
        var sun = DayNightGeometry.Sun(utc);
        var dpi = RenderScale();
        var size = (int)Math.Round(rect.Width * dpi);
        _globe.Render(size, camera, sun);
        if (_globe.Bitmap is { } bitmap) dc.DrawImage(bitmap, rect);

        var c = new Point(rect.Left + (rect.Width / 2), rect.Top + (rect.Height / 2));
        var radius = rect.Width / 2;
        Point? Screen(GeoPoint point)
        {
            var (x, y, front) = camera.Project(point);
            return front ? new Point(c.X + (x * radius), c.Y - (y * radius)) : null;
        }

        // The oval, pixel by pixel over the globe; its equatorward edge as a line.
        var subsolar = AuroraModel.SubsolarMagneticLongitude(utc);
        var kp = Kp;
        if (UpdateOval(size, camera, utc, kp) is { } oval)
        {
            dc.PushOpacity(0.9 + (0.1 * Math.Sin(now * 2.3)));
            dc.DrawImage(oval, rect);
            dc.Pop();
        }

        var edge = new List<Point>();
        for (var j = 0; j < 96; j++)
        {
            var mlt = j / 4.0;
            if (Corner(mlt, AuroraModel.OvalEquatorward(kp, mlt)) is { } p) edge.Add(p);
        }

        Point? Corner(double mlt, double latitude) =>
            Screen(AuroraModel.FromMagnetic(hemisphere * latitude, AuroraModel.MagneticLongitudeAt(subsolar, mlt)));

        if (edge.Count > 2)
        {
            var line = Line(edge, closed: edge.Count == 96);
            if (Glows("oval")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 7), line);
            dc.DrawGeometry(null, FadedPen(Ink.Green, 0.55, 1), line);
        }

        // How far from the oval its visible top still stands 3° over the horizon.
        var reach = AuroraModel.SightDistance(VisibleTop) / AuroraModel.KmPerDegree;
        var seen = new List<Point>();
        for (var j = 0; j <= 96; j++)
        {
            var mlt = j / 4.0;
            if (Screen(AuroraModel.FromMagnetic(hemisphere * (AuroraModel.OvalEquatorward(kp, mlt) - reach), AuroraModel.MagneticLongitudeAt(subsolar, mlt))) is { } p)
            {
                seen.Add(p);
            }
        }

        if (seen.Count > 2)
        {
            var line = Line(seen);
            if (Glows("seen")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 6), line);
            dc.DrawGeometry(null, FadedPen(Ink.Gold, 0.6, 1.2, dashed: true), line);
            _targets.Add("seen", seen[seen.Count / 2], 10);
        }

        // The magnetic meridian from the viewer to the pole: the slice on the right.
        var meridian = new List<Point>();
        for (var i = 0; i <= 40; i++)
        {
            var m = geometry.Mlat + ((hemisphere * 90 - geometry.Mlat) * i / 40);
            if (Screen(AuroraModel.FromMagnetic(m, mlon)) is { } p) meridian.Add(p);
        }

        if (meridian.Count > 1)
        {
            var line = Line(meridian);
            if (Glows("meridian")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 7), line);
            dc.DrawGeometry(null, FadedPen(Ink.Cyan, 0.7, 1.4, dashed: true), line);
            _targets.Add("meridian", meridian[meridian.Count / 3], 10);
        }

        if (Screen(pole) is { } polePoint)
        {
            dc.DrawEllipse(null, FadedPen(Ink.Cyan, 0.95, 1.6), polePoint, 5, 5);
            dc.DrawLine(FadedPen(Ink.Cyan, 0.95, 1.4), polePoint + new Vector(-8, 0), polePoint + new Vector(8, 0));
            dc.DrawLine(FadedPen(Ink.Cyan, 0.95, 1.4), polePoint + new Vector(0, -8), polePoint + new Vector(0, 8));
            _targets.Add("pole", polePoint, 10);
            if (ShowLabels) DrawPill(dc, Loc("Aurora_Label_Pole"), polePoint + new Vector(0, -18), Ink.Cyan, 11);
        }

        // The night side's middle on the oval: midnight, where the aurora is brightest.
        if (Corner(23, AuroraModel.OvalEquatorward(kp, 23) - 1) is { } midnight)
        {
            _targets.Add("night", midnight, 10);
        }

        if (Screen(you) is { } here)
        {
            dc.DrawEllipse(Glow(Ink.Gold, 0.9), null, here, 12, 12);
            dc.DrawEllipse(Solid(255, 0xFF, 0xD3, 0x6B), MakePen(Solid(255, 20, 20, 30), 1.5), here, 4.5, 4.5);
            _targets.Add("you", here, 10);
            if (ShowLabels) DrawPill(dc, Loc("Aurora_Label_You"), here + new Vector(0, 18), Ink.Gold, 11);
        }

        if (ShowLabels && edge.Count > 0)
        {
            var at = edge.MinBy(p => p.Y);
            DrawTargetPill(dc, Loc("Aurora_Label_Oval"), at + new Vector(0, -14), Ink.Green, "oval");
        }
        else if (edge.Count > 0)
        {
            _targets.Add("oval", edge[edge.Count / 2], 10);
        }

        dc.DrawEllipse(null, Ink.Frame, c, radius, radius);
        DrawText(dc, Loc("Aurora_Globe_Caption"), new Point(rect.Left, rect.Top - 4), Ink.Dim, 11);
    }

    private WriteableBitmap? _ovalBitmap;
    private int[] _ovalPixels = [];
    private float[] _ovalX = [];
    private float[] _ovalY = [];
    private float[] _ovalZ = [];
    private float[] _ovalEdge = [];
    private (int Size, GlobeCamera Camera) _ovalGrid;
    private (long Minute, double Kp) _ovalKey = (-1, double.NaN);

    /// <summary>
    /// The auroral ovals (north and south) as a picture over the globe: for each pixel its magnetic latitude and local time
    /// put it inside the oval or not (soft edges, brightest on the equatorward side and a little before midnight), and the
    /// Sun's height there dims it by day. Redone when the minute, Kp, the size or the view changes.
    /// </summary>
    private WriteableBitmap? UpdateOval(int size, GlobeCamera camera, DateTime utc, double kp)
    {
        if (size < 8) return null;
        if (_ovalGrid.Size != size || _ovalGrid.Camera != camera)
        {
            PrepareOvalGrid(size, camera);
            _ovalKey = (-1, double.NaN);
        }

        var minute = utc.Ticks / TimeSpan.TicksPerMinute;
        if (_ovalKey == (minute, kp)) return _ovalBitmap;
        _ovalKey = (minute, kp);

        var (fx, fy, fz) = AuroraModel.MagneticFrame;
        var sun = DayNightGeometry.Sun(utc);
        var s = Vec3.FromGeo(sun.Declination, sun.SubsolarLongitude, 0);
        var subsolar = AuroraModel.SubsolarMagneticLongitude(utc);
        var pixels = _ovalPixels;
        Parallel.For(0, size, row =>
        {
            for (var col = 0; col < size; col++)
            {
                var i = (row * size) + col;
                var edge = _ovalEdge[i];
                pixels[i] = 0;
                if (edge <= 0) continue;
                var p = new Vec3(_ovalX[i], _ovalY[i], _ovalZ[i]);
                var mlat = Math.Abs(Math.Asin(Math.Clamp(p.Dot(fz), -1, 1)) * 180 / Math.PI);
                if (mlat < 44) continue;
                var mlon = Math.Atan2(p.Dot(fy), p.Dot(fx)) * 180 / Math.PI;
                var mlt = AuroraModel.Wrap24(12 + ((mlon - subsolar) / 15));
                var eq = AuroraModel.OvalEquatorward(kp, mlt);
                var pw = AuroraModel.OvalPoleward(kp, mlt);
                var inside = Smooth((mlat - eq + 1.5) / 1.5) * Smooth((pw + 1.5 - mlat) / 1.5);
                if (inside <= 0) continue;
                var across = Math.Clamp((mlat - eq) / Math.Max(0.5, pw - eq), 0, 1);
                var altitude = Math.Asin(Math.Clamp(p.Dot(s), -1, 1)) * 180 / Math.PI;
                var dark = 0.12 + (0.88 * Ramp(-altitude, 0, 12));
                var a = inside * (0.55 + (0.45 * (1 - across))) * AuroraModel.OvalBrightness(kp, mlt) * dark * edge;
                a *= 0.9;
                pixels[i] = Pack(0x7C * a, 0xFF * a, 0x9E * a, 0xFF * a);
            }
        });

        SpaceDrawing.WritePixels(_ovalBitmap!, pixels, size, size);
        return _ovalBitmap;
    }

    /// <summary>Each pixel's point on the globe in geographic coordinates, for the view.</summary>
    private void PrepareOvalGrid(int size, GlobeCamera camera)
    {
        var count = size * size;
        _ovalX = new float[count];
        _ovalY = new float[count];
        _ovalZ = new float[count];
        _ovalEdge = new float[count];
        _ovalPixels = new int[count];
        _ovalBitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        _ovalGrid = (size, camera);
        var (sl, cl) = Math.SinCos(camera.ReferenceLongitude * Math.PI / 180);
        var half = size / 2.0;
        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col < size; col++)
            {
                var i = (row * size) + col;
                var sx = (col + 0.5 - half) / half;
                var sy = (half - row - 0.5) / half;
                var r2 = (sx * sx) + (sy * sy);
                var edge = Math.Clamp(((1 - Math.Sqrt(r2)) * half) + 0.5, 0, 1);
                _ovalEdge[i] = (float)edge;
                if (edge <= 0) continue;
                var p = (camera.Right * sx) + (camera.Up * sy) + (camera.Toward * Math.Sqrt(Math.Max(0, 1 - r2)));
                // From the view's frame (x at the reference longitude) to the geographic one.
                _ovalX[i] = (float)((p.X * cl) - (p.Y * sl));
                _ovalY[i] = (float)((p.X * sl) + (p.Y * cl));
                _ovalZ[i] = (float)p.Z;
            }
        }
    }

    private static int Pack(double r, double g, double b, double a) =>
        ((int)Math.Clamp(a, 0, 255) << 24) | ((int)Math.Clamp(r, 0, 255) << 16) | ((int)Math.Clamp(g, 0, 255) << 8) | (int)Math.Clamp(b, 0, 255);

    #endregion

    #region the slice

    /// <summary>
    /// The slice along the magnetic meridian from the viewer to the curtain, to scale: Earth's curve, the air, the curtain
    /// coloured by height, the horizon and the line of sight to the curtain's top and bottom.
    /// </summary>
    private void DrawSlice(PaintContext dc, Rect rect, SkyGeometry geometry, double now)
    {
        dc.DrawRoundedRectangle(Solid(255, 4, 7, 16), Ink.Frame, rect, 12, 12);
        dc.PushClip(new RectangleGeometry(rect, 12, 12));
        const double r = AuroraModel.EarthRadiusKm;
        var distance = Math.Max(40, geometry.CurtainKm);
        var gamma = distance / r;
        var span = Math.Max(distance * 1.22 + 250, 1400) / r;

        // Points in km with the viewer at (0, 0), up being the viewer's up; the curtain stands along its own vertical.
        Point Ground(double angle, double height) => new((r + height) * Math.Sin(angle), ((r + height) * Math.Cos(angle)) - r);

        var topHeight = 520.0;
        var minX = -0.08 * span * r;
        var maxX = Math.Max(Ground(span, 0).X, Ground(gamma, topHeight).X) + 60;
        var maxY = Math.Max(Ground(gamma, topHeight).Y, 80) + 40;
        var minY = Math.Min(Ground(span, 0).Y, -60) - 40;
        var scale = Math.Min((rect.Width - 24) / (maxX - minX), (rect.Height - 60) / (maxY - minY));
        var origin = new Point(rect.Left + 12 - (minX * scale), rect.Top + 50 + (maxY * scale));
        Point Px(Point km) => new(origin.X + (km.X * scale), origin.Y - (km.Y * scale));

        // The ground, Earth below it and the air over it (100 km).
        var ground = new List<Point>();
        var air = new List<Point>();
        for (var i = -10; i <= 120; i++)
        {
            var a = span * i / 110;
            ground.Add(Px(Ground(a, 0)));
            air.Add(Px(Ground(a, 100)));
        }

        var body = new List<Point>(ground) { new(rect.Right + 20, rect.Bottom + 20), new(rect.Left - 20, rect.Bottom + 20) };
        var airBand = new List<Point>(air);
        airBand.AddRange(Enumerable.Reverse(ground));
        dc.DrawGeometry(FadedBrush(Ink.Blue, 0.16), null, Line(airBand, closed: true, filled: true));
        dc.DrawGeometry(Solid(255, 0x16, 0x22, 0x1C), FadedPen(Color.FromRgb(0x5E, 0x8A, 0x6A), 0.9, 1.4), Line(body, closed: true, filled: true));

        // Height rings over the curtain: 100, 200, 300, 400 km, named at their near end (only 400 km when it is small).
        var small = 400 * scale < 70;
        foreach (var height in new[] { 100.0, 200, 300, 400 })
        {
            var ring = Enumerable.Range(0, 21).Select(i => Px(Ground(gamma - (0.045 * span) + (0.09 * span * i / 20), height))).ToList();
            dc.DrawGeometry(null, FadedPen(Ink.Dim, 0.35, 1, dashed: true), Line(ring));
            if (small && height < 400) continue;
            var text = _labels.Text(LocFormat("Aurora_Slice_Km", Number(height, "0")), 9.5, Ink.Dim);
            dc.DrawText(text, new Point(ring[0].X - text.Width - 4, ring[0].Y - (text.Height / 2)));
        }

        // The curtain: a strip standing up from the ground, coloured by height.
        var foot = Px(Ground(gamma, CurtainBottom));
        var head = Px(Ground(gamma, CurtainTop));
        var axis = head - foot;
        var across = new Vector(-axis.Y, axis.X);
        across /= Math.Max(1e-6, across.Length);
        var width = Math.Clamp(rect.Width * 0.014, 4, 9);
        var brush = CurtainBrush(foot, head, geometry.Brightness);
        dc.DrawGeometry(brush, null, Polygon(foot - (across * width), foot + (across * width), head + (across * width), head - (across * width)));
        _targets.Add("curtain", Px(Ground(gamma, 160)), 12);

        // Which colours are where: the red on top, the green, the violet fringe at the bottom; labels stacked beside the
        // curtain (on the side with room), each with a leader to its height.
        var energy = EnergyKeV;
        var peak = AuroraModel.PeakHeight(energy);
        var marks = new (string Key, string Id, Color Color, double Height)[]
        {
            ("Aurora_Label_Red", "red", Ink.Red, Math.Max(peak + 120, 280)),
            ("Aurora_Label_Green", "green", Ink.Green, peak + 15),
            ("Aurora_Label_Violet", "violet", Ink.Violet, Math.Max(CurtainBottom + 4, peak - 6)),
        };
        var middle = Px(Ground(gamma, 200));
        var onLeft = middle.X - 190 > rect.Left + 10;
        var columnX = onLeft ? middle.X - 125 : Math.Min(rect.Right - 60, middle.X + 70);
        for (var k = 0; k < marks.Length; k++)
        {
            var (key, id, color, height) = marks[k];
            var anchor = Px(Ground(gamma, height));
            var y = Math.Clamp(middle.Y + ((k - 1) * 22) - 10, rect.Top + 44, rect.Bottom - 14);
            var at = new Point(columnX, y);
            dc.DrawLine(FadedPen(color, 0.6, 1), new Point(at.X + (onLeft ? 30 : -30), at.Y), anchor);
            if (ShowLabels) DrawTargetPill(dc, Loc(key), at, color, id, 10.5);
            else _targets.Add(id, anchor, 9);
        }

        // The viewer, the horizon and the lines of sight to the curtain's bottom and top.
        var eye = Px(new Point(0, 0));
        var horizonEnd = Px(new Point(maxX, 0));
        var horizon = Line([eye, horizonEnd]);
        if (Glows("horizon")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 6), horizon);
        dc.DrawGeometry(null, FadedPen(Ink.Gold, 0.55, 1.2, dashed: true), horizon);
        _targets.Add("horizon", Mix(eye, horizonEnd, 0.55), 9);

        var visibleTop = VisibleTop;
        var topVisible = AuroraModel.Elevation(visibleTop, distance);
        var bottomVisible = AuroraModel.Elevation(Math.Max(CurtainBottom, AuroraModel.PeakHeight(EnergyKeV) - 10), distance);
        var sight = new StreamGeometry();
        using (var context = sight.Open())
        {
            foreach (var height in new[] { Math.Max(CurtainBottom, AuroraModel.PeakHeight(EnergyKeV) - 10), visibleTop })
            {
                context.BeginFigure(eye, false);
                context.LineTo(Px(Ground(gamma, height)));
                context.EndFigure(false);
            }
        }
        if (Glows("sight")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 6), sight);
        dc.DrawGeometry(null, FadedPen(topVisible > 0 ? Ink.Text : Ink.Red, 0.7, 1.2), sight);
        _targets.Add("sight", Mix(eye, Px(Ground(gamma, visibleTop)), 0.35), 9);

        // A small person at the viewer's place.
        dc.DrawEllipse(Solid(255, 0xFF, 0xD3, 0x6B), null, eye + new Vector(0, -9), 3, 3);
        dc.DrawLine(MakePen(Solid(255, 0xFF, 0xD3, 0x6B), 2), eye + new Vector(0, -6), eye);
        _targets.Add("you", eye + new Vector(0, -6), 9);

        var caption = topVisible > 0
            ? LocFormat("Aurora_Slice_Seen", Number(distance, "0"), Number(Math.Max(0, bottomVisible), "0"), Number(topVisible, "0"))
            : LocFormat("Aurora_Slice_Hidden", Number(distance, "0"), Number(-topVisible, "0"));
        DrawText(dc, Loc("Aurora_Slice_Caption"), new Point(rect.Left + 12, rect.Top + 14), Ink.Text, 11.5, bold: true);
        DrawText(dc, caption, new Point(rect.Left + 12, rect.Top + 30), topVisible > 0 ? Ink.Dim : Ink.Red, 10.5);
        dc.Pop();
    }

    /// <summary>A gradient along the curtain from foot to head: its colour at each height for the electrons' energy.</summary>
    private IBrush CurtainBrush(Point foot, Point head, double brightness)
    {
        var brush = new LinearGradientBrush { StartPoint = new RelativePoint(foot, RelativeUnit.Absolute), EndPoint = new RelativePoint(head, RelativeUnit.Absolute) };
        for (var i = 0; i <= 24; i++)
        {
            var t = i / 24.0;
            var height = CurtainBottom + ((CurtainTop - CurtainBottom) * t);
            var (r, g, b, a) = CurtainColor(height, 0.55 + (0.45 * brightness));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(a, r, g, b), t));
        }
        return brush;
    }

    /// <summary>The curtain's colour at a height, tone-mapped, as straight (not premultiplied) colour and alpha.</summary>
    private (byte R, byte G, byte B, byte A) CurtainColor(double height, double gain)
    {
        var (lr, lg, lb) = LightAt(height);
        var r = 1 - Math.Exp(-lr * gain * 2.2);
        var g = 1 - Math.Exp(-lg * gain * 2.2);
        var b = 1 - Math.Exp(-lb * gain * 2.2);
        var a = Math.Max(r, Math.Max(g, b));
        if (a < 1e-4) return (0, 0, 0, 0);
        return ((byte)(255 * r / a), (byte)(255 * g / a), (byte)(255 * b / a), (byte)(255 * a));
    }

    /// <summary>The curtain's light at a height (linear RGB, unbounded), from the table for the energy now.</summary>
    private (float R, float G, float B) LightAt(double height)
    {
        EnsureLightTable();
        var i = Math.Clamp((int)Math.Round((height - CurtainBottom) / 2), 0, _lightTable.R.Length - 1);
        return (_lightTable.R[i], _lightTable.G[i], _lightTable.B[i]);
    }

    private void EnsureLightTable()
    {
        var energy = Math.Round(EnergyKeV, 2);
        if (_lightTable.Energy == energy) return;
        var count = (int)((CurtainTop - CurtainBottom) / 2) + 1;
        var red = new float[count];
        var green = new float[count];
        var blue = new float[count];
        for (var i = 0; i < count; i++)
        {
            var light = AuroraModel.Light(CurtainBottom + (2 * i), energy);
            // Oxygen green 557.7 nm, oxygen red 630 nm and nitrogen's 427.8 nm violet, as screen colours.
            red[i] = (float)((light.Green * 0.30) + (light.Red * 1.0) + (light.Violet * 0.45));
            green[i] = (float)((light.Green * 1.0) + (light.Red * 0.12) + (light.Violet * 0.20));
            blue[i] = (float)((light.Green * 0.45) + (light.Red * 0.18) + (light.Violet * 1.0));
        }

        _lightTable = (energy, red, green, blue);
    }

    #endregion

    #region the sky view

    /// <summary>
    /// The sky seen from the viewer's place facing the oval: 140° across, from the horizon up. Pixel by pixel: the sky
    /// (blue by day, dark with stars by night), the curtain where each line of sight passes over it (its height there gives
    /// its colour), folded and with rays moving along it; then a dark skyline of hills and trees.
    /// </summary>
    private void DrawSkyView(PaintContext dc, Rect rect, SkyGeometry geometry, double now)
    {
        dc.DrawRoundedRectangle(Solid(255, 4, 7, 16), Ink.Frame, rect, 12, 12);
        if (rect.Width < 40 || rect.Height < 40) return;
        var dpi = RenderScale();
        var w = Math.Max(16, (int)(rect.Width * dpi * 0.6));
        var h = Math.Max(16, (int)(rect.Height * dpi * 0.6));
        if (_skyBitmap is null || _skyBitmap.PixelSize.Width != w || _skyBitmap.PixelSize.Height != h)
        {
            _skyBitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            _skyPixels = new int[w * h];
        }

        EnsureLightTable();
        const double halfWidth = 70;
        var distance = Math.Max(60, geometry.CurtainKm);
        var topElevation = AuroraModel.Elevation(VisibleTop, distance);
        var maxElevation = Math.Clamp(Math.Max(topElevation, AuroraModel.Elevation(300, distance)) + 12, 18, 90);
        var day = Ramp(geometry.SunAltitude, -18, 2);
        var twilight = Ramp(geometry.SunAltitude, -18, -4);
        var gain = (0.35 + (0.65 * geometry.Brightness)) * (0.6 + (0.4 * Math.Clamp(Kp / 9, 0, 1))) * 1.6;
        var washed = 1 - (0.97 * Ramp(geometry.SunAltitude, -14, -4));
        var pixels = _skyPixels;
        var tableR = _lightTable.R;
        var tableG = _lightTable.G;
        var tableB = _lightTable.B;
        const double r = AuroraModel.EarthRadiusKm;
        var gamma0 = Math.Tan(distance / r);
        var t = now;

        // Per column: the curtain's distance along that bearing (folded and drifting) and the rays' brightness.
        var columnCos = new double[w];
        var columnSin = new double[w];
        var columnRay = new double[w];
        for (var x = 0; x < w; x++)
        {
            var a = (((x + 0.5) / w) - 0.5) * 2 * halfWidth * Math.PI / 180;
            var along = r * Math.Atan(gamma0 / Math.Cos(a));
            var fold = Math.Min(60, 12 + (0.1 * distance));
            along += fold * ((Math.Sin((a * 4.1) + (t * 0.23)) * 0.7) + (Math.Sin((a * 9.7) - (t * 0.41)) * 0.3));
            var g = Math.Max(5, along) / r;
            columnCos[x] = Math.Cos(g);
            columnSin[x] = Math.Sin(g);
            var rays = 0.55 + (0.45 * Math.Sin((a * 37) + (t * 1.9)) * Math.Sin((a * 13.3) - (t * 0.7)));
            columnRay[x] = Math.Clamp(rays, 0.12, 1) * (0.75 + (0.25 * Math.Sin((a * 2.3) - (t * 0.35))));
        }

        Parallel.For(0, h, y =>
        {
            var e = maxElevation * (1 - ((y + 0.5) / h)) * Math.PI / 180;
            var (se, ce) = Math.SinCos(e);
            var height01 = 1 - ((y + 0.5) / h);

            // The sky: day blue, twilight glow low down, night dark.
            var skyR = (0.02 + (0.05 * twilight * (1 - height01))) * 255;
            var skyG = (0.03 + (0.05 * twilight * (1 - height01))) * 255;
            var skyB = (0.07 + (0.08 * twilight)) * 255;
            skyR += day * ((0.35 + (0.25 * (1 - height01))) * 255 - skyR);
            skyG += day * ((0.55 + (0.2 * (1 - height01))) * 255 - skyG);
            skyB += day * (0.9 * 255 - skyB);

            for (var x = 0; x < w; x++)
            {
                double pr = skyR, pg = skyG, pb = skyB;
                // Where this line of sight passes over the curtain, and how high it is there: r cos e / cos(γ + e).
                var c = (columnCos[x] * ce) - (columnSin[x] * se);
                if (c > 1e-6)
                {
                    var height = (r * ce / c) - r;
                    if (height >= CurtainBottom && height <= CurtainTop)
                    {
                        var i = (int)((height - CurtainBottom) / 2);
                        var k = gain * columnRay[x] * washed;
                        pr += 255 * (1 - Math.Exp(-tableR[i] * k));
                        pg += 255 * (1 - Math.Exp(-tableG[i] * k));
                        pb += 255 * (1 - Math.Exp(-tableB[i] * k));
                    }
                }

                pixels[(y * w) + x] = (255 << 24) | ((int)Math.Min(255, pr) << 16) | ((int)Math.Min(255, pg) << 8) | (int)Math.Min(255, pb);
            }
        });

        // Stars, at night only.
        var starAlpha = 1 - Math.Max(day, twilight * 0.6);
        if (starAlpha > 0.05)
        {
            foreach (var (sx, sy, brightness) in SkyStars)
            {
                var x = (int)(sx * w);
                var y = (int)(sy * h * 0.9);
                var i = (y * w) + x;
                var add = (int)(starAlpha * (90 + (165 * brightness)));
                var p = pixels[i];
                var pr = Math.Min(255, ((p >> 16) & 0xFF) + add);
                var pg = Math.Min(255, ((p >> 8) & 0xFF) + add);
                var pb = Math.Min(255, (p & 0xFF) + add);
                pixels[i] = (255 << 24) | (pr << 16) | (pg << 8) | pb;
            }
        }

        SpaceDrawing.WritePixels(_skyBitmap, pixels, w, h);
        dc.PushClip(new RectangleGeometry(rect, 12, 12));
        SpaceDrawing.DrawSmooth(dc, _skyBitmap, rect);

        // The skyline: rolling hills with a few trees.
        var skyline = new List<Point> { new(rect.Left, rect.Bottom) };
        for (var i = 0; i <= 80; i++)
        {
            var f = i / 80.0;
            var hill = 0.035 + (0.018 * Math.Sin((f * 7.1) + 1.3)) + (0.01 * Math.Sin(f * 19.3));
            var tree = (i % 9 == 3 || i % 13 == 7) ? 0.03 : 0;
            skyline.Add(new Point(rect.Left + (f * rect.Width), rect.Bottom - ((hill + tree) * rect.Height)));
        }

        skyline.Add(new Point(rect.Right, rect.Bottom));
        dc.DrawGeometry(Solid(255, 3, 5, 8), null, Line(skyline, closed: true, filled: true));

        // Elevation marks on the left edge.
        foreach (var mark in new[] { 10.0, 30, 60 })
        {
            if (mark > maxElevation - 3) continue;
            var y = rect.Bottom - (rect.Height * mark / maxElevation);
            dc.DrawLine(FadedPen(Ink.Dim, 0.5, 1), new Point(rect.Left, y), new Point(rect.Left + 8, y));
            DrawText(dc, Number(mark, "0") + "°", new Point(rect.Left + 11, y), Ink.Dim, 9.5);
        }

        var title = LocFormat(geometry.TowardPole ? "Aurora_View_Facing" : "Aurora_View_FacingAway", Loc(geometry.Mlat >= 0 ? "Aurora_North" : "Aurora_South"));
        DrawPill(dc, title, new Point(rect.Left + (rect.Width / 2), rect.Top + 14), Ink.Text, 11);
        AddBoxTarget("view", new Rect(rect.Left + 10, rect.Top + 30, rect.Width - 20, rect.Height * 0.5));

        string? note = geometry.SunAltitude > -6 ? "Aurora_View_Daylight" : topElevation < 0 ? "Aurora_View_Hidden" : null;
        if (note is not null) DrawPill(dc, Loc(note), new Point(rect.Left + (rect.Width / 2), rect.Top + (rect.Height * 0.5)), Ink.Gold, 11.5);
        dc.Pop();
    }

    #endregion
}
