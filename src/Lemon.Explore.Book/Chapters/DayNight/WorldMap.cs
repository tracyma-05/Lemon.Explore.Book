using System.Globalization;
using System.IO;
using System.Reflection;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.DayNight;

/// <summary>
/// The world's coastlines (Natural Earth 1:110m land, embedded as <c>Assets/Maps/land-110m.txt</c>) and a land
/// raster made from them for drawing Earth pixel by pixel.
/// </summary>
public static class WorldMap
{
    private const string ResourceName = "Lemon.Explore.Book.Maps.Land110m";

    private static readonly Lazy<IReadOnlyList<GeoPoint[]>> LandRings = new(Load);
    private static readonly Lazy<LandMask> Mask = new(() => LandMask.Rasterize(LandRings.Value, 2048, 1024));

    /// <summary>Land outlines; each ring is closed (its last point repeats the first).</summary>
    public static IReadOnlyList<GeoPoint[]> Land => LandRings.Value;

    /// <summary>The land raster, made on first use (a few tens of milliseconds).</summary>
    public static LandMask LandMask => Mask.Value;

    /// <summary>The land raster if it is ready; otherwise starts making it in the background and returns null.</summary>
    public static LandMask? LandMaskIfReady
    {
        get
        {
            if (Mask.IsValueCreated) return Mask.Value;
            WarmUp();
            return null;
        }
    }

    private static int _warming;

    public static void WarmUp()
    {
        if (Interlocked.Exchange(ref _warming, 1) == 0) _ = Task.Run(() => Mask.Value);
    }

    private static IReadOnlyList<GeoPoint[]> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);

        var rings = new List<GeoPoint[]>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#') continue;

            var values = line.Split(',');
            var ring = new GeoPoint[values.Length / 2];
            for (var i = 0; i < ring.Length; i++)
            {
                var lon = int.Parse(values[2 * i], CultureInfo.InvariantCulture) / 100.0;
                var lat = int.Parse(values[(2 * i) + 1], CultureInfo.InvariantCulture) / 100.0;
                ring[i] = new GeoPoint(lat, lon);
            }

            rings.Add(ring);
        }

        return rings;
    }
}

/// <summary>How much of each cell of an equirectangular grid is land, 0…255, with bilinear lookup.</summary>
public sealed class LandMask
{
    private readonly byte[] _coverage;

    private LandMask(int width, int height, byte[] coverage)
    {
        Width = width;
        Height = height;
        _coverage = coverage;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Land share at a point, 0 (sea) … 1 (land), smooth along the coasts.</summary>
    public double Sample(double latitude, double longitude)
    {
        var x = ((longitude + 180) / 360 * Width) - 0.5;
        var y = Math.Clamp(((90 - latitude) / 180 * Height) - 0.5, 0, Height - 1.001);
        var x0 = (int)Math.Floor(x);
        var y0 = (int)y;
        var fx = x - x0;
        var fy = y - y0;
        x0 %= Width;
        if (x0 < 0) x0 += Width;
        var x1 = x0 + 1 == Width ? 0 : x0 + 1;
        var row0 = y0 * Width;
        var row1 = Math.Min(y0 + 1, Height - 1) * Width;

        var top = (_coverage[row0 + x0] * (1 - fx)) + (_coverage[row0 + x1] * fx);
        var bottom = (_coverage[row1 + x0] * (1 - fx)) + (_coverage[row1 + x1] * fx);
        return ((top * (1 - fy)) + (bottom * fy)) / 255;
    }

    /// <summary>
    /// Fills the rings even-odd into a <paramref name="width"/> × <paramref name="height"/> grid, with four scanlines per
    /// row and exact coverage along each scanline, so the coasts come out anti-aliased.
    /// </summary>
    public static LandMask Rasterize(IReadOnlyList<GeoPoint[]> rings, int width, int height)
    {
        const int subRows = 4;

        // Edges in grid coordinates, bucketed by the rows they touch.
        var buckets = new List<(double X0, double Y0, double X1, double Y1)>[height];
        foreach (var ring in rings)
        {
            for (var i = 0; i < ring.Length; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Length];
                var edge = (X0: (a.Longitude + 180) / 360 * width, Y0: (90 - a.Latitude) / 180 * height,
                    X1: (b.Longitude + 180) / 360 * width, Y1: (90 - b.Latitude) / 180 * height);
                if (edge.Y0 == edge.Y1) continue;

                var first = Math.Clamp((int)Math.Floor(Math.Min(edge.Y0, edge.Y1)), 0, height - 1);
                var last = Math.Clamp((int)Math.Floor(Math.Max(edge.Y0, edge.Y1)), 0, height - 1);
                for (var row = first; row <= last; row++) (buckets[row] ??= []).Add(edge);
            }
        }

        var coverage = new byte[width * height];
        Parallel.For(0, height, row =>
        {
            var edges = buckets[row];
            if (edges is null) return;

            var cover = new double[width];
            var crossings = new List<double>();
            for (var s = 0; s < subRows; s++)
            {
                var y = row + ((s + 0.5) / subRows);
                crossings.Clear();
                foreach (var (x0, y0, x1, y1) in edges)
                {
                    if ((y0 <= y && y < y1) || (y1 <= y && y < y0))
                    {
                        crossings.Add(x0 + ((y - y0) / (y1 - y0) * (x1 - x0)));
                    }
                }

                crossings.Sort();
                for (var i = 0; i + 1 < crossings.Count; i += 2)
                {
                    AddSpan(cover, crossings[i], crossings[i + 1], 1.0 / subRows);
                }
            }

            for (var x = 0; x < width; x++)
            {
                coverage[(row * width) + x] = (byte)Math.Clamp(Math.Round(cover[x] * 255), 0, 255);
            }
        });

        return new LandMask(width, height, coverage);
    }

    private static void AddSpan(double[] cover, double from, double to, double weight)
    {
        from = Math.Clamp(from, 0, cover.Length);
        to = Math.Clamp(to, 0, cover.Length);
        if (to <= from) return;

        var first = (int)from;
        var last = (int)to;
        if (first == last)
        {
            cover[Math.Min(first, cover.Length - 1)] += (to - from) * weight;
            return;
        }

        cover[first] += (first + 1 - from) * weight;
        for (var i = first + 1; i < last; i++) cover[i] += weight;
        if (last < cover.Length) cover[last] += (to - last) * weight;
    }
}
