using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.DayNight;

/// <summary>
/// Draws the flat world map (equirectangular: longitude across, latitude up) with the night side shaded through the
/// three twilights. Shared by the page's main diagram and the small map in the side panel.
/// </summary>
internal sealed class MapPainter
{
    /// <summary>The night shade is computed on a half-degree grid and stretched smoothly over the map.</summary>
    private const int ShadeWidth = 720;

    private const int ShadeHeight = 360;

    private static readonly double[] SinLat = new double[ShadeHeight];
    private static readonly double[] CosLat = new double[ShadeHeight];

    private readonly int[] _shade = new int[ShadeWidth * ShadeHeight];
    private readonly double[] _cosH = new double[ShadeWidth];
    private WriteableBitmap? _shadeBitmap;

    private Rect _landRect;
    private StreamGeometry? _land;
    private IBrush? _landBrush;
    private IBrush? _oceanBrush;

    static MapPainter()
    {
        for (var row = 0; row < ShadeHeight; row++)
        {
            var latitude = 90 - ((row + 0.5) * 180.0 / ShadeHeight);
            (SinLat[row], CosLat[row]) = Math.SinCos(DayNightGeometry.ToRadians(latitude));
        }
    }

    public static Point ToScreen(Rect map, double latitude, double longitude) =>
        new(map.X + ((longitude + 180) / 360 * map.Width), map.Y + ((90 - latitude) / 180 * map.Height));

    public static Point ToScreen(Rect map, GeoPoint point) => ToScreen(map, point.Latitude, point.Longitude);

    public static GeoPoint ToGeo(Rect map, Point point) => new(
        Math.Clamp(90 - ((point.Y - map.Y) / map.Height * 180), -90, 90),
        Math.Clamp(((point.X - map.X) / map.Width * 360) - 180, -180, 180));

    /// <summary>Sea and land in daylight colours.</summary>
    public void DrawSurface(PaintContext dc, Rect map)
    {
        if (_land is null || _landRect != map)
        {
            _landRect = map;
            _land = LandGeometry(map);
            _landBrush = LatitudeBrush(map, DayNightPaint.LandStops);
            _oceanBrush = LatitudeBrush(map, DayNightPaint.OceanStops);
        }

        dc.DrawRectangle(_oceanBrush, null, map);
        dc.PushClip(_land);
        dc.DrawRectangle(_landBrush, null, map);
        dc.Pop();
        dc.DrawGeometry(null, DayNightPaint.Coast, _land);
    }

    /// <summary>Meridians every 15° (one hour of the Earth's turn), the equator, the tropics and the polar circles.</summary>
    public static void DrawGraticule(PaintContext dc, Rect map, double tilt = 23.44)
    {
        for (var lon = -165; lon <= 165; lon += 15)
        {
            var x = ToScreen(map, 0, lon).X;
            dc.DrawLine(lon == 0 ? DayNightPaint.Greenwich : DayNightPaint.Meridian, new Point(x, map.Top), new Point(x, map.Bottom));
        }

        void Parallel(double latitude, IPen pen)
        {
            var y = ToScreen(map, latitude, 0).Y;
            dc.DrawLine(pen, new Point(map.Left, y), new Point(map.Right, y));
        }

        Parallel(0, DayNightPaint.Equator);
        Parallel(tilt, DayNightPaint.Tropic);
        Parallel(-tilt, DayNightPaint.Tropic);
        Parallel(90 - tilt, DayNightPaint.PolarCircle);
        Parallel(-(90 - tilt), DayNightPaint.PolarCircle);
    }

    /// <summary>The night side: darker through civil, nautical and astronomical twilight into full night.</summary>
    public void DrawNight(PaintContext dc, Rect map, SunPosition sun, double strength = 1)
    {
        var (sinDec, cosDec) = Math.SinCos(DayNightGeometry.ToRadians(sun.Declination));
        for (var col = 0; col < ShadeWidth; col++)
        {
            var longitude = -180 + ((col + 0.5) * 360.0 / ShadeWidth);
            _cosH[col] = Math.Cos(DayNightGeometry.ToRadians(longitude - sun.SubsolarLongitude));
        }

        for (var row = 0; row < ShadeHeight; row++)
        {
            var a = SinLat[row] * sinDec;
            var b = CosLat[row] * cosDec;
            var offset = row * ShadeWidth;
            for (var col = 0; col < ShadeWidth; col++)
            {
                _shade[offset + col] = DayNightPaint.NightShade(a + (b * _cosH[col]));
            }
        }

        _shadeBitmap ??= new WriteableBitmap(new PixelSize(ShadeWidth, ShadeHeight), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        SpaceDrawing.WritePixels(_shadeBitmap, _shade, ShadeWidth, ShadeHeight);

        if (strength < 1) dc.PushOpacity(strength);
        dc.DrawImage(_shadeBitmap, map);
        if (strength < 1) dc.Pop();
    }

    /// <summary>The circle where the Sun is at <paramref name="altitude"/>, broken where it crosses the map's edge.</summary>
    public static void DrawCircle(PaintContext dc, Rect map, SunPosition sun, double altitude, IPen pen, int count = 360)
    {
        dc.DrawGeometry(null, pen, CircleGeometry(map, DayNightGeometry.Circle(sun, altitude, count)));
    }

    /// <summary>Points as a polyline on the map, split where it jumps across the date line or over a pole.</summary>
    public static StreamGeometry CircleGeometry(Rect map, IReadOnlyList<GeoPoint> points)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var start = 0;

            // Start just after a jump, so the closed circle is drawn without a stray segment.
            for (var i = 0; i < points.Count; i++)
            {
                var previous = points[(i + points.Count - 1) % points.Count];
                if (Math.Abs(points[i].Longitude - previous.Longitude) > 90)
                {
                    start = i;
                    break;
                }
            }

            var open = false;

            for (var k = 0; k <= points.Count; k++)
            {
                var point = points[(start + k) % points.Count];
                var at = ToScreen(map, point);
                if (k == 0)
                {
                    if (open) ctx.EndFigure(false);
                    ctx.BeginFigure(at, false);
                    open = true;
                    continue;
                }

                var previous = points[(start + k - 1) % points.Count];
                if (Math.Abs(point.Longitude - previous.Longitude) > 90)
                {
                    if (open) ctx.EndFigure(false);
                    ctx.BeginFigure(at, false);
                    open = true;
                }
                else
                {
                    ctx.LineTo(at);
                }
            }
        }
        return geometry;
    }

    /// <summary>The Sun over the point where it is overhead.</summary>
    public static void DrawSun(PaintContext dc, Rect map, SunPosition sun, double radius)
    {
        var at = ToScreen(map, sun.Subsolar);
        dc.DrawEllipse(DayNightPaint.SunGlow, null, at, radius * 3.4, radius * 3.4);
        dc.DrawEllipse(DayNightPaint.SunDisc, DayNightPaint.SunRim, at, radius, radius);
    }

    /// <summary>A city's lights, brighter the darker it is there.</summary>
    public static void DrawCityLight(PaintContext dc, Point at, double darkness, double size = 1)
    {
        if (darkness <= 0.01) return;
        dc.PushOpacity(darkness);
        dc.DrawEllipse(DayNightPaint.CityGlow, null, at, 7 * size, 7 * size);
        dc.Pop();
    }

    private static StreamGeometry LandGeometry(Rect map)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.SetFillRule(FillRule.EvenOdd);
            foreach (var ring in WorldMap.Land)
            {
                ctx.BeginFigure(ToScreen(map, ring[0]), true);
                for (var i = 1; i < ring.Length; i++) ctx.LineTo(ToScreen(map, ring[i]));
                ctx.EndFigure(true);
            }
        }
        return geometry;
    }

    /// <summary>A top-to-bottom gradient with colour stops given by latitude.</summary>
    private static IBrush LatitudeBrush(Rect map, IReadOnlyList<(double Latitude, Color Color)> stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        };
        foreach (var (latitude, color) in stops) brush.GradientStops.Add(new GradientStop(color, (90 - latitude) / 180));
        return brush;
    }
}

/// <summary>Colours of the day-and-night page.</summary>
internal static class DayNightPaint
{
    public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
    public static readonly Color DimText = Color.FromArgb(0xC0, 0xC9, 0xD1, 0xDC);
    public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
    public static readonly Color Blue = Color.FromRgb(0x8E, 0xC5, 0xFF);
    public static readonly Color Night = Color.FromRgb(0x03, 0x06, 0x14);

    /// <summary>Land colour by latitude: ice, tundra, forest, the dry belts and the tropics.</summary>
    public static readonly (double Latitude, Color Color)[] LandStops =
    [
        (90, Color.FromRgb(0xE2, 0xE8, 0xEE)),
        (72, Color.FromRgb(0xD6, 0xDE, 0xE6)),
        (63, Color.FromRgb(0x84, 0x91, 0x76)),
        (50, Color.FromRgb(0x50, 0x78, 0x46)),
        (33, Color.FromRgb(0xA9, 0x96, 0x66)),
        (20, Color.FromRgb(0xB8, 0x9F, 0x6C)),
        (8, Color.FromRgb(0x44, 0x7A, 0x40)),
        (-8, Color.FromRgb(0x44, 0x7A, 0x40)),
        (-24, Color.FromRgb(0xAE, 0x93, 0x62)),
        (-40, Color.FromRgb(0x5C, 0x82, 0x4E)),
        (-56, Color.FromRgb(0x7C, 0x8A, 0x70)),
        (-62, Color.FromRgb(0xD6, 0xDE, 0xE6)),
        (-90, Color.FromRgb(0xE2, 0xE8, 0xEE)),
    ];

    public static readonly (double Latitude, Color Color)[] OceanStops =
    [
        (90, Color.FromRgb(0x2A, 0x45, 0x60)),
        (60, Color.FromRgb(0x14, 0x3E, 0x66)),
        (0, Color.FromRgb(0x16, 0x5A, 0x8E)),
        (-60, Color.FromRgb(0x14, 0x3E, 0x66)),
        (-90, Color.FromRgb(0x2A, 0x45, 0x60)),
    ];

    public static readonly IPen Coast = MakePen(Solid(70, 255, 255, 255), 0.6);
    public static readonly IPen Meridian = MakePen(Solid(22, 255, 255, 255), 1);
    public static readonly IPen Greenwich = MakePen(Solid(60, 255, 255, 255), 1);
    public static readonly IPen Equator = MakePen(Solid(80, 255, 255, 255), 1);
    public static readonly IPen Tropic = MakePen(Solid(110, 0xFF, 0xD3, 0x6B), 1, dashed: true);
    public static readonly IPen PolarCircle = MakePen(Solid(110, 0x8E, 0xC5, 0xFF), 1, dashed: true);
    public static readonly IPen MapFrame = MakePen(Solid(60, 255, 255, 255), 1);

    public static readonly IPen DayNightLine = MakePen(Solid(255, 0xFF, 0xD3, 0x6B), 2.2);
    public static readonly IPen DayNightLineShadow = MakePen(Solid(120, 0, 0, 0), 4.5);
    public static readonly IPen TwilightLine = MakePen(Solid(120, 0x8E, 0xC5, 0xFF), 1, dashed: true);
    public static readonly IPen NoonLine = MakePen(Solid(150, 0xFF, 0xD3, 0x6B), 1.2, dashed: true);
    public static readonly IPen MidnightLine = MakePen(Solid(170, 0x8E, 0xC5, 0xFF), 1.2, dashed: true);
    public static readonly IPen DateLine = MakePen(Solid(200, 0xFF, 0x9F, 0x8A), 1.6, dashed: true);
    public static readonly IPen NewDayLine = MakePen(Solid(240, 0x8E, 0xC5, 0xFF), 2.2);

    public static readonly IBrush SunDisc = SpaceDrawing.RadialGradient((Color.FromRgb(0xFF, 0xFC, 0xF0), 0), (Color.FromRgb(0xFF, 0xD0, 0x60), 1));
    public static readonly IPen SunRim = MakePen(Solid(255, 0xFF, 0xF4, 0xD0), 1);

    public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(150, 255, 230, 160), 0),
            (Color.FromArgb(50, 255, 210, 120), 0.45),
            (Color.FromArgb(0, 255, 200, 110), 1));

    public static readonly IBrush CityGlow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(255, 255, 236, 170), 0),
            (Color.FromArgb(200, 255, 196, 90), 0.25),
            (Color.FromArgb(60, 255, 170, 60), 0.55),
            (Color.FromArgb(0, 255, 160, 60), 1));

    public static readonly IBrush GoldBrush = Solid(255, 0xFF, 0xD3, 0x6B);
    public static readonly IBrush BlueBrush = Solid(255, 0x8E, 0xC5, 0xFF);
    public static readonly IBrush CityDot = Solid(255, 0xF2, 0xF4, 0xF8);
    public static readonly IPen CityOutline = MakePen(Solid(220, 8, 12, 22), 1);
    public static readonly IBrush PlaceFill = Solid(255, 0xFF, 0xD3, 0x6B);
    public static readonly IPen PlaceRing = MakePen(Solid(255, 0xFF, 0xD3, 0x6B), 1.6);
    public static readonly IPen PlaceOutline = MakePen(Solid(255, 10, 14, 24), 1.4);

    public static readonly IBrush ZoneBand = Solid(16, 255, 255, 255);
    public static readonly IBrush LaterDate = Solid(34, 0x8E, 0xC5, 0xFF);
    public static readonly IPen ZoneOutline = MakePen(Solid(200, 0xFF, 0xD3, 0x6B), 1.4);

    /// <summary>
    /// Premultiplied night colour for a point where the Sun's altitude has sine <paramref name="sinAltitude"/>:
    /// clear by day, then darker through the twilights.
    /// </summary>
    public static int NightShade(double sinAltitude)
    {
        var index = (int)((Math.Clamp(sinAltitude, -1, 1) + 1) * (ShadeTable.Length - 1) / 2);
        return ShadeTable[index];
    }

    /// <summary>How dark the night side gets, 0…1, for a Sun at <paramref name="altitude"/>.</summary>
    public static double Darkness(double altitude) => altitude switch
    {
        >= 0 => 0,
        >= DayNightGeometry.SunriseAltitude => Lerp(0, 0.08, altitude / DayNightGeometry.SunriseAltitude),
        >= DayNightGeometry.CivilTwilight => Lerp(0.08, 0.45, (altitude - DayNightGeometry.SunriseAltitude) / (DayNightGeometry.CivilTwilight - DayNightGeometry.SunriseAltitude)),
        >= DayNightGeometry.NauticalTwilight => Lerp(0.45, 0.72, (altitude + 6) / -6),
        >= DayNightGeometry.AstronomicalTwilight => Lerp(0.72, 0.9, (altitude + 12) / -6),
        _ => Lerp(0.9, 1, Math.Clamp((altitude + 18) / -8, 0, 1)),
    };

    /// <summary>Strongest night shade (over full night).</summary>
    public const double MaxShade = 0.86;

    private static readonly int[] ShadeTable = BuildShadeTable(4097);

    private static int[] BuildShadeTable(int size)
    {
        var table = new int[size];
        for (var i = 0; i < size; i++)
        {
            var sine = (2.0 * i / (size - 1)) - 1;
            var altitude = DayNightGeometry.ToDegrees(Math.Asin(sine));
            var alpha = Darkness(altitude) * MaxShade;
            var a = (int)Math.Round(alpha * 255);
            table[i] = (a << 24) | ((int)(Night.R * alpha) << 16) | ((int)(Night.G * alpha) << 8) | (int)(Night.B * alpha);
        }

        return table;
    }

    private static double Lerp(double a, double b, double t) => a + ((b - a) * Math.Clamp(t, 0, 1));
}
