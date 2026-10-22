using System.Collections.Concurrent;

namespace Lemon.Explore.Book.Controls.Space;

/// <summary>
/// An equirectangular surface map (longitude across, latitude down) with mip levels, plus the radial profile of
/// the ring system when the planet has one. Texels are packed BGRA; alpha holds the specular mask (Earth's oceans).
/// </summary>
public sealed class PlanetTexture
{
    private const int RingSamples = 1024;

    private readonly int[] _widths;
    private readonly int[] _heights;
    private readonly uint[][] _levels;

    private PlanetTexture(uint[] baseLevel, int width, int height)
    {
        var widths = new List<int> { width };
        var heights = new List<int> { height };
        var levels = new List<uint[]> { baseLevel };
        while (width > 32)
        {
            baseLevel = Downsample(baseLevel, width, height);
            width /= 2;
            height /= 2;
            widths.Add(width);
            heights.Add(height);
            levels.Add(baseLevel);
        }

        _widths = [.. widths];
        _heights = [.. heights];
        _levels = [.. levels];
    }

    /// <summary>
    /// Texels around the equator for a planet. The browser runs this on its only thread, so its head lowers it to
    /// keep the first frames responsive.
    /// </summary>
    public static int PlanetWidth { get; set; } = 1024;

    public int LevelCount => _levels.Length;

    /// <summary>Ring colour and opacity across <see cref="RingSystem.Inner"/>..Outer.</summary>
    public float[]? RingProfile { get; private set; }

    /// <summary>Mip level whose width best matches a sphere drawn with this pixel radius.</summary>
    public int LevelFor(double pixelRadius)
    {
        // Half the circumference (the visible hemisphere) spans the diameter: width / 2 texels over 2R pixels.
        var wanted = Math.Max(1, pixelRadius * 4);
        var level = 0;
        while (level < _levels.Length - 1 && _widths[level] / 2 >= wanted)
        {
            level++;
        }

        return level;
    }

    /// <summary>Bilinear sample; <paramref name="u"/> wraps, <paramref name="v"/> clamps. Channels in 0..255.</summary>
    public void Sample(int level, double u, double v, out float r, out float g, out float b, out float spec)
    {
        var w = _widths[level];
        var h = _heights[level];
        var data = _levels[level];

        var x = (u * w) - 0.5;
        var y = Math.Clamp((v * h) - 0.5, 0, h - 1.001);
        var x0 = (int)Math.Floor(x);
        var y0 = (int)y;
        var fx = (float)(x - x0);
        var fy = (float)(y - y0);
        x0 %= w;
        if (x0 < 0)
        {
            x0 += w;
        }

        var x1 = x0 + 1 == w ? 0 : x0 + 1;
        var y1 = Math.Min(y0 + 1, h - 1);

        var c00 = data[(y0 * w) + x0];
        var c10 = data[(y0 * w) + x1];
        var c01 = data[(y1 * w) + x0];
        var c11 = data[(y1 * w) + x1];

        var w00 = (1 - fx) * (1 - fy);
        var w10 = fx * (1 - fy);
        var w01 = (1 - fx) * fy;
        var w11 = fx * fy;

        b = (w00 * (c00 & 0xFF)) + (w10 * (c10 & 0xFF)) + (w01 * (c01 & 0xFF)) + (w11 * (c11 & 0xFF));
        g = (w00 * ((c00 >> 8) & 0xFF)) + (w10 * ((c10 >> 8) & 0xFF)) + (w01 * ((c01 >> 8) & 0xFF)) + (w11 * ((c11 >> 8) & 0xFF));
        r = (w00 * ((c00 >> 16) & 0xFF)) + (w10 * ((c10 >> 16) & 0xFF)) + (w01 * ((c01 >> 16) & 0xFF)) + (w11 * ((c11 >> 16) & 0xFF));
        spec = (w00 * (c00 >> 24)) + (w10 * (c10 >> 24)) + (w01 * (c01 >> 24)) + (w11 * (c11 >> 24));
    }

    #region cache

    private static readonly ConcurrentDictionary<string, PlanetTexture> Cache = new();
    private static readonly ConcurrentDictionary<string, byte> Requested = new();
    private static readonly Queue<SpaceBody> Pending = new();
    private static readonly Lock PendingLock = new();
    private static bool _working;

    /// <summary>
    /// The body's texture if it is ready. The first request queues it for generation on a background worker, in
    /// request order, so the bodies on screen are textured first.
    /// </summary>
    public static PlanetTexture? TryGet(SpaceBody body)
    {
        if (Cache.TryGetValue(body.Id, out var texture))
        {
            return texture;
        }

        if (Requested.TryAdd(body.Id, 0))
        {
            lock (PendingLock)
            {
                Pending.Enqueue(body);
                if (!_working)
                {
                    _working = true;
                    _ = Task.Run(GeneratePending);
                }
            }
        }

        return null;
    }

    private static void GeneratePending()
    {
        while (true)
        {
            SpaceBody body;
            lock (PendingLock)
            {
                if (!Pending.TryDequeue(out body!))
                {
                    _working = false;
                    return;
                }
            }

            try
            {
                Cache[body.Id] = Create(body);
            }
            catch (Exception ex) when (ex is OutOfMemoryException or InvalidOperationException or ArgumentException)
            {
                // The body keeps its flat colour; the page stays usable.
                System.Diagnostics.Trace.TraceWarning($"Generating the texture of {body.Id} failed: {ex.Message}");
            }
        }
    }

    internal static PlanetTexture Create(SpaceBody body)
    {
        // Stars and moons are never drawn large enough to need full-size maps.
        var width = body.IsStar || body.IsMoon ? Math.Min(512, PlanetWidth) : PlanetWidth;
        var height = width / 2;
        var pixels = new uint[width * height];

        Parallel.For(0, height, j =>
        {
            var lat = (Math.PI / 2) - ((j + 0.5) / height * Math.PI);
            var (sinLat, cosLat) = Math.SinCos(lat);
            for (var i = 0; i < width; i++)
            {
                var lon = ((i + 0.5) / width * 2 * Math.PI) - Math.PI;
                var (sinLon, cosLon) = Math.SinCos(lon);
                var p = new V3(cosLat * cosLon, sinLat, cosLat * sinLon);
                var (r, g, b, s) = PlanetSurfaces.Shade(body, p, lat);
                pixels[(j * width) + i] = Pack(r, g, b, s);
            }
        });

        var texture = new PlanetTexture(pixels, width, height);
        if (body.Rings is { } rings)
        {
            texture.RingProfile = BuildRingProfile(body.Surface, rings);
        }

        return texture;
    }

    private static float[] BuildRingProfile(SurfaceStyle surface, RingSystem rings)
    {
        var profile = new float[RingSamples * 4];
        for (var k = 0; k < RingSamples; k++)
        {
            var radius = rings.Inner + ((rings.Outer - rings.Inner) * (k + 0.5) / RingSamples);
            var (r, g, b, a) = PlanetSurfaces.Ring(surface, radius);
            profile[(k * 4) + 0] = (float)r;
            profile[(k * 4) + 1] = (float)g;
            profile[(k * 4) + 2] = (float)b;
            profile[(k * 4) + 3] = (float)a;
        }

        return profile;
    }

    private static uint Pack(double r, double g, double b, double s)
    {
        static uint C(double v) => (uint)Math.Clamp((int)Math.Round(v * 255), 0, 255);
        return (C(s) << 24) | (C(r) << 16) | (C(g) << 8) | C(b);
    }

    private static uint[] Downsample(uint[] src, int width, int height)
    {
        var w = width / 2;
        var h = height / 2;
        var dst = new uint[w * h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var a = src[(2 * y * width) + (2 * x)];
                var b = src[(2 * y * width) + (2 * x) + 1];
                var c = src[(((2 * y) + 1) * width) + (2 * x)];
                var d = src[(((2 * y) + 1) * width) + (2 * x) + 1];
                uint result = 0;
                for (var shift = 0; shift < 32; shift += 8)
                {
                    var sum = ((a >> shift) & 0xFF) + ((b >> shift) & 0xFF) + ((c >> shift) & 0xFF) + ((d >> shift) & 0xFF);
                    result |= ((sum + 2) / 4) << shift;
                }

                dst[(y * w) + x] = result;
            }
        }

        return dst;
    }

    #endregion
}
