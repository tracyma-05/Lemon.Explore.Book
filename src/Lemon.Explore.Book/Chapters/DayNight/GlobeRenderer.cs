using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.DayNight;

internal readonly record struct Vec3(double X, double Y, double Z)
{
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);

    public double Dot(Vec3 b) => (X * b.X) + (Y * b.Y) + (Z * b.Z);

    public Vec3 Cross(Vec3 b) => new((Y * b.Z) - (Z * b.Y), (Z * b.X) - (X * b.Z), (X * b.Y) - (Y * b.X));

    public double Length => Math.Sqrt(Dot(this));

    public Vec3 Normalized() => this * (1 / Length);

    /// <summary>
    /// A point on the unit sphere, in a frame whose x axis points at latitude 0, longitude
    /// <paramref name="referenceLongitude"/>, y 90° east of it and z at the North Pole.
    /// </summary>
    public static Vec3 FromGeo(double latitude, double longitude, double referenceLongitude)
    {
        var (sinLat, cosLat) = Math.SinCos(DayNightGeometry.ToRadians(latitude));
        var (sinLon, cosLon) = Math.SinCos(DayNightGeometry.ToRadians(longitude - referenceLongitude));
        return new Vec3(cosLat * cosLon, cosLat * sinLon, sinLat);
    }
}

/// <summary>
/// How a globe is looked at: screen right, screen up and the direction towards the viewer, in the frame of
/// <see cref="Vec3.FromGeo"/> for <see cref="ReferenceLongitude"/>.
/// </summary>
internal readonly record struct GlobeCamera(Vec3 Right, Vec3 Up, Vec3 Toward, double ReferenceLongitude)
{
    /// <summary>
    /// A camera that keeps the Sun to the left: <paramref name="towardSun"/> turns the view towards the lit side
    /// (more day in sight), <paramref name="elevation"/> raises it above the equator.
    /// </summary>
    public static GlobeCamera SunFixed(SunPosition sun, double towardSun, double elevation)
    {
        // Rounded, so the view (and the renderer's per-pixel cache) changes only every few hours of playback.
        var declination = Math.Round(sun.Declination * 10) / 10;
        var (sinDec, cosDec) = Math.SinCos(DayNightGeometry.ToRadians(declination));
        var s = new Vec3(cosDec, 0, sinDec);

        // North at right angles to the sunlight, and east (90° from the Sun along the equator).
        var north = new Vec3(-sinDec, 0, cosDec);
        var east = new Vec3(0, 1, 0);
        var (sinE, cosE) = Math.SinCos(DayNightGeometry.ToRadians(elevation));
        var (sinT, cosT) = Math.SinCos(DayNightGeometry.ToRadians(towardSun));
        var toward = (((east * cosE) + (north * sinE)) * cosT) + (s * sinT);
        return Looking(toward.Normalized(), north, sun.SubsolarLongitude);
    }

    /// <summary>A camera straight above <paramref name="latitude"/>, <paramref name="longitude"/>, north up.</summary>
    public static GlobeCamera Above(double latitude, double longitude) =>
        Looking(Vec3.FromGeo(latitude, longitude, longitude), new Vec3(0, 0, 1), longitude);

    private static GlobeCamera Looking(Vec3 toward, Vec3 upHint, double referenceLongitude)
    {
        var up = (upHint - (toward * upHint.Dot(toward))).Normalized();
        return new GlobeCamera(up.Cross(toward), up, toward, referenceLongitude);
    }

    /// <summary>Where a point of the unit sphere lands, in globe radii from the centre (y up), and whether it faces us.</summary>
    public (double X, double Y, bool Front) Project(Vec3 p) => (p.Dot(Right), p.Dot(Up), p.Dot(Toward) >= 0);

    public (double X, double Y, bool Front) Project(GeoPoint point) => Project(Vec3.FromGeo(point.Latitude, point.Longitude, ReferenceLongitude));
}

/// <summary>
/// Earth as a globe, pixel by pixel: the real coastlines from <see cref="WorldMap.LandMask"/>, lit by the Sun at one
/// instant, with the same twilight shading as the flat map.
/// </summary>
internal sealed class GlobeRenderer
{
    private int _size;
    private GlobeCamera _camera;
    private float[] _px = [];
    private float[] _py = [];
    private float[] _pz = [];
    private float[] _lat = [];
    private float[] _lon = [];
    private float[] _edge = [];
    private int[] _pixels = [];

    private static readonly (float R, float G, float B)[] LandColors = LatitudeColors(DayNightPaint.LandStops);
    private static readonly (float R, float G, float B)[] OceanColors = LatitudeColors(DayNightPaint.OceanStops);

    public WriteableBitmap? Bitmap { get; private set; }

    /// <summary>Renders a globe <paramref name="size"/> pixels across.</summary>
    public void Render(int size, GlobeCamera camera, SunPosition sun)
    {
        size = Math.Max(8, size);
        // The pixels' places on the sphere depend only on the view; the turning Earth just adds a longitude.
        if (size != _size || camera.Right != _camera.Right || camera.Up != _camera.Up || camera.Toward != _camera.Toward)
        {
            Prepare(size, camera);
        }

        var s = Vec3.FromGeo(sun.Declination, sun.SubsolarLongitude, camera.ReferenceLongitude);
        var mask = WorldMap.LandMaskIfReady;
        var referenceLongitude = (float)camera.ReferenceLongitude;

        Parallel.For(0, size, row =>
        {
            for (var col = 0; col < size; col++)
            {
                var i = (row * size) + col;
                var edge = _edge[i];
                if (edge <= 0)
                {
                    _pixels[i] = 0;
                    continue;
                }

                var latitude = _lat[i];
                var land = mask?.Sample(latitude, _lon[i] + referenceLongitude) ?? 0;
                var band = Math.Clamp((int)Math.Round(90 - latitude), 0, 180);
                var (lr, lg, lb) = LandColors[band];
                var (or, og, ob) = OceanColors[band];
                var r = or + ((lr - or) * land);
                var g = og + ((lg - og) * land);
                var b = ob + ((lb - ob) * land);

                // Soft daylight shading towards the edge of the lit side, then the night shade over it.
                var sine = (_px[i] * s.X) + (_py[i] * s.Y) + (_pz[i] * s.Z);
                var light = 0.55 + (0.45 * Math.Sqrt(Math.Max(0, sine)));
                var shade = DayNightPaint.NightShade(sine);
                var alpha = ((shade >> 24) & 0xFF) / 255.0;
                r = (r * light * (1 - alpha)) + ((shade >> 16) & 0xFF);
                g = (g * light * (1 - alpha)) + ((shade >> 8) & 0xFF);
                b = (b * light * (1 - alpha)) + (shade & 0xFF);

                _pixels[i] = Pack(r * edge, g * edge, b * edge, 255 * edge);
            }
        });

        SpaceDrawing.WritePixels(Bitmap!, _pixels, size, size);
    }

    private void Prepare(int size, GlobeCamera camera)
    {
        _size = size;
        _camera = camera;
        var count = size * size;
        if (_pixels.Length != count)
        {
            _px = new float[count];
            _py = new float[count];
            _pz = new float[count];
            _lat = new float[count];
            _lon = new float[count];
            _edge = new float[count];
            _pixels = new int[count];
            Bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        }

        var half = size / 2.0;
        Parallel.For(0, size, row =>
        {
            for (var col = 0; col < size; col++)
            {
                var i = (row * size) + col;
                var sx = (col + 0.5 - half) / half;
                var sy = (half - row - 0.5) / half;
                var r2 = (sx * sx) + (sy * sy);
                var edge = Math.Clamp(((1 - Math.Sqrt(r2)) * half) + 0.5, 0, 1);
                _edge[i] = (float)edge;
                if (edge <= 0) continue;

                var sz = Math.Sqrt(Math.Max(0, 1 - r2));
                var p = (camera.Right * sx) + (camera.Up * sy) + (camera.Toward * sz);
                _px[i] = (float)p.X;
                _py[i] = (float)p.Y;
                _pz[i] = (float)p.Z;
                _lat[i] = (float)DayNightGeometry.ToDegrees(Math.Asin(Math.Clamp(p.Z, -1, 1)));
                _lon[i] = (float)DayNightGeometry.ToDegrees(Math.Atan2(p.Y, p.X));
            }
        });
    }

    private static int Pack(double r, double g, double b, double a) =>
        ((int)Math.Clamp(a, 0, 255) << 24) | ((int)Math.Clamp(r, 0, 255) << 16) | ((int)Math.Clamp(g, 0, 255) << 8) | (int)Math.Clamp(b, 0, 255);

    /// <summary>The colour at every whole degree of latitude, 90° N first.</summary>
    private static (float R, float G, float B)[] LatitudeColors(IReadOnlyList<(double Latitude, Color Color)> stops)
    {
        var colors = new (float, float, float)[181];
        for (var i = 0; i <= 180; i++)
        {
            var latitude = 90.0 - i;
            var k = 0;
            while (k < stops.Count - 2 && stops[k + 1].Latitude > latitude) k++;
            var (lat0, c0) = stops[k];
            var (lat1, c1) = stops[k + 1];
            var t = (float)Math.Clamp((lat0 - latitude) / (lat0 - lat1), 0, 1);
            colors[i] = (c0.R + ((c1.R - c0.R) * t), c0.G + ((c1.G - c0.G) * t), c0.B + ((c1.B - c0.B) * t));
        }

        return colors;
    }
}
