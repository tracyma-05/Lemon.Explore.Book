using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.IceAges;

/// <summary>
/// The northern half of the globe seen from above the North Pole (azimuthal equidistant, out to 30° N), drawn per pixel
/// with the real land (<see cref="WorldMap.LandMask"/>), the ice sheets for a given amount of ice and the summer sea ice.
/// Longitude 0 points down, 90° E right, North America on the left. The pixels' places are worked out once per size;
/// the ice is redrawn only when it has changed enough to show.
/// </summary>
internal sealed class PolarIceMap
{
    /// <summary>The map's edge latitude.</summary>
    public const double EdgeLatitude = 30;

    /// <summary>An ice sheet: where its dome sits when small and when at its biggest, its radius then (degrees), and how far over sea it reaches.</summary>
    private readonly record struct Dome(double SmallLat, double SmallLon, double FullLat, double FullLon, double Radius, double OverSea, double Floor);

    /// <summary>
    /// The northern ice sheets at the last glacial maximum, roughly: Laurentide (it starts over Labrador and Baffin Island and
    /// spreads to Hudson Bay), Cordilleran, Greenland (there now too), Fennoscandian, Barents–Kara and the British–Irish.
    /// </summary>
    private static readonly Dome[] Domes =
    [
        new(66, -68, 60, -86, 22, 0.8, 0),
        new(58, -128, 55, -123, 9, 0.3, 0),
        new(72.5, -40, 72.5, -40, 10.5, 0.15, 0.82),
        new(66, 15, 63, 18, 15, 0.45, 0),
        new(78, 55, 76, 50, 10, 0.75, 0),
        new(57, -5, 56, -3, 4.5, 0.3, 0),
    ];

    private int _size;
    private float[] _x = [], _y = [], _z = [];
    private byte[] _land = [];
    private float[] _lat = [];
    private float[] _jitter = [];
    private int[] _base = [];
    private int[] _pixels = [];
    private double _lastShare = double.NaN;

    public WriteableBitmap? Bitmap { get; private set; }

    /// <summary>The ice sheets' growth from 0 (today's: Greenland only) to about 1 (the last glacial maximum's).</summary>
    public static double Growth(double share) => Math.Sqrt(Math.Clamp(share, 0, 1.15));

    /// <summary>The summer sea ice's edge latitude for a given amount of ice.</summary>
    public static double SeaIceEdge(double share) => 82 - (18 * Growth(share));

    /// <summary>Where a dome sits and how big it is (centre latitude, longitude, radius in degrees) for a given amount of ice.</summary>
    private static (double Lat, double Lon, double Radius) Place(Dome dome, double share)
    {
        var g = Growth(share);
        var radius = dome.Floor > 0 ? dome.Radius * (dome.Floor + ((1 - dome.Floor) * g)) : dome.Radius * g;
        return (dome.SmallLat + ((dome.FullLat - dome.SmallLat) * g), dome.SmallLon + ((dome.FullLon - dome.SmallLon) * g), radius);
    }

    /// <summary>The centre of a named ice sheet for labels: 0 Laurentide, 2 Greenland, 3 Fennoscandian.</summary>
    public static (double Lat, double Lon, double Radius) SheetCentre(int index, double share) => Place(Domes[index], share);

    /// <summary>A map point (as fractions of the radius from the centre, x right and y down) for a latitude and longitude.</summary>
    public static Vector Project(double latitude, double longitude)
    {
        var rho = (90 - latitude) / (90 - EdgeLatitude);
        var a = IceAgeModel.Rad(longitude);
        return new Vector(rho * Math.Sin(a), rho * Math.Cos(a));
    }

    /// <summary>The map at <paramref name="size"/>² pixels with the ice for <paramref name="share"/> (0 today, 1 at the last maximum).</summary>
    public WriteableBitmap Render(int size, double share)
    {
        size = Math.Clamp(size, 32, 1400);
        if (size != _size || Bitmap is null)
        {
            Prepare(size);
            _lastShare = double.NaN;
        }

        if (double.IsNaN(_lastShare) || Math.Abs(share - _lastShare) > 0.002)
        {
            _lastShare = share;
            Paint(share);
            SpaceDrawing.WritePixels(Bitmap!, _pixels, size, size);
        }

        return Bitmap!;
    }

    private void Prepare(int size)
    {
        _size = size;
        var n = size * size;
        _x = new float[n];
        _y = new float[n];
        _z = new float[n];
        _land = new byte[n];
        _lat = new float[n];
        _jitter = new float[n];
        _base = new int[n];
        _pixels = new int[n];
        var mask = WorldMap.LandMask;
        var c = size / 2.0;
        for (var py = 0; py < size; py++)
        {
            for (var px = 0; px < size; px++)
            {
                var i = (py * size) + px;
                var dx = (px + 0.5 - c) / c;
                var dy = (py + 0.5 - c) / c;
                var rho = Math.Sqrt((dx * dx) + (dy * dy));
                if (rho > 1)
                {
                    _base[i] = 0;
                    _lat[i] = float.NaN;
                    continue;
                }

                var lat = 90 - (rho * (90 - EdgeLatitude));
                var lon = IceAgeModel.Deg(Math.Atan2(dx, dy));
                var (x, y, z) = Unit(lat, lon);
                _x[i] = (float)x;
                _y[i] = (float)y;
                _z[i] = (float)z;
                _lat[i] = (float)lat;

                // A smooth wobble (−1 to 1) by place, so the ice's edges are ragged like real ones, not circles.
                var l = IceAgeModel.Rad(lon);
                var f = IceAgeModel.Rad(lat) * 4;
                _jitter[i] = (float)((0.5 * Math.Sin((3 * l) + (2 * f))) + (0.3 * Math.Sin((7 * l) - (5 * f) + 1.3)) + (0.2 * Math.Sin((13 * l) + (11 * f) + 2.1)));
                var land = mask.Sample(lat, lon);
                _land[i] = (byte)Math.Round(Math.Clamp(land, 0, 1) * 255);

                // Sea darker towards the pole, land from green-brown to grey tundra; a soft rim at the map's edge.
                var t = (lat - EdgeLatitude) / (90 - EdgeLatitude);
                var sea = Mix(Rgb(0x16, 0x3E, 0x66), Rgb(0x0C, 0x26, 0x40), t);
                var ground = Mix(Rgb(0x4F, 0x6B, 0x3C), Rgb(0x7A, 0x74, 0x62), t);
                var colour = Mix(sea, ground, land);
                var rim = Math.Clamp((rho - 0.9) / 0.1, 0, 1);
                _base[i] = Mix(colour, Rgb(0x06, 0x0A, 0x14), rim * 0.45);
            }
        }

        Bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
    }

    private void Paint(double share)
    {
        var domes = Domes.Select(d =>
        {
            var (lat, lon, radius) = Place(d, share);
            var (x, y, z) = Unit(lat, lon);
            var cosR = Math.Cos(IceAgeModel.Rad(radius));
            var edge = Math.Max(1e-4, Math.Sin(IceAgeModel.Rad(Math.Max(radius, 0.5))) * IceAgeModel.Rad(0.8));
            var cosMarine = Math.Cos(IceAgeModel.Rad(radius * d.OverSea));

            // The edge wanders in and out by up to a fifth of the radius.
            var wander = Math.Sin(IceAgeModel.Rad(Math.Max(radius, 0.5))) * IceAgeModel.Rad(radius * 0.2);
            return (X: x, Y: y, Z: z, CosR: cosR, Edge: edge, CosMarine: cosMarine, Wander: wander, On: radius > 0.3);
        }).ToArray();
        var seaEdge = SeaIceEdge(share);
        var iceColour = Rgb(0xEC, 0xF5, 0xFC);
        var seaIce = Rgb(0xA9, 0xC2, 0xD6);

        for (var i = 0; i < _base.Length; i++)
        {
            var colour = _base[i];
            if (colour == 0)
            {
                _pixels[i] = 0;
                continue;
            }

            var land = _land[i] / 255.0;
            var lat = _lat[i];

            // Summer sea ice over the sea north of its edge, fading in over 2°.
            if (land < 0.5 && lat > seaEdge - 2) colour = Mix(colour, seaIce, Math.Clamp((lat - seaEdge + 2) / 2, 0, 1) * (1 - land) * 0.85);

            var ice = 0.0;
            foreach (var d in domes)
            {
                if (!d.On) continue;
                var dot = (_x[i] * d.X) + (_y[i] * d.Y) + (_z[i] * d.Z) + (_jitter[i] * d.Wander);
                if (dot < d.CosR - d.Edge) continue;
                var inside = Math.Clamp(((dot - d.CosR) / d.Edge) + 0.5, 0, 1);

                // Over the sea only the inner part (Hudson Bay, the Baltic, the Barents Sea) is covered.
                var cover = land >= 0.5 || dot >= d.CosMarine ? 1 : land * 2;
                ice = Math.Max(ice, inside * cover);
            }

            if (ice > 0) colour = Mix(colour, iceColour, ice * 0.95);
            _pixels[i] = colour;
        }
    }

    private static (double X, double Y, double Z) Unit(double lat, double lon)
    {
        var phi = IceAgeModel.Rad(lat);
        var lambda = IceAgeModel.Rad(lon);
        return (Math.Cos(phi) * Math.Cos(lambda), Math.Cos(phi) * Math.Sin(lambda), Math.Sin(phi));
    }

    private static int Rgb(int r, int g, int b) => unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;

    private static int Mix(int a, int b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        int Channel(int shift) => (int)Math.Round((((a >> shift) & 0xFF) * (1 - t)) + (((b >> shift) & 0xFF) * t));
        return unchecked((int)0xFF000000) | (Channel(16) << 16) | (Channel(8) << 8) | Channel(0);
    }
}
