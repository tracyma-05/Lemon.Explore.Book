using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.BlackHole;

/// <summary>
/// The black hole up close, ray traced: every pixel's light is followed back from the camera along its real path past
/// the hole (<see cref="BlackHoleModel.TraceLight"/>), to the glowing disk, into the hole or out to the stars. The paths
/// are worked out once per tilt and size (<see cref="Build"/>, a table of rays by impact parameter); each frame only
/// shades the disk as it turns (<see cref="Shade"/>).
/// </summary>
/// <remarks>
/// The camera is 40 M from the hole; the disk is thin and flat, from the innermost stable orbit (6 M) to 20 M, and glows by
/// <see cref="BlackHoleModel.DiskGlow"/>. Its colours are those of a blackbody whose temperature is shifted into visible
/// light (real disks glow in ultraviolet and X-rays); the shift by <see cref="BlackHoleModel.DiskShift"/> is real.
/// </remarks>
internal sealed class ShadowRenderer
{
    /// <summary>The camera's distance from the hole, M.</summary>
    public const double CameraDistance = 40;

    /// <summary>The disk's outer edge, M.</summary>
    public const double DiskOuter = 20;

    /// <summary>How fast the disk turns: M of the hole's time per second of the animation.</summary>
    public const double TimeScale = 30;

    private const double ImpactStep = 0.01;
    private const double MaxImpact = 34;

    private static readonly double Lapse = Math.Sqrt(1 - (2 / CameraDistance));

    /// <summary>Rays by impact parameter, bent by gravity, every 0.01 M.</summary>
    private static readonly Lazy<LightPath[]> Curved = new(() => BuildRays(bent: true), isThreadSafe: true);

    /// <summary>The same rays if light went straight (a hole that does not bend light).</summary>
    private static readonly Lazy<LightPath[]> Straight = new(() => BuildRays(bent: false), isThreadSafe: true);

    private WriteableBitmap? _bitmap;
    private int _w;
    private int _h;
    private object? _key;

    // The paths: per pixel, what its light reaches. Kind 0 = sky, 1 = disk, 2 = the hole.
    private byte[] _kind = [];
    private byte[] _order = [];
    private float[] _r = [];
    private float[] _psi = [];
    private float[] _lz = [];
    private float[] _phi = [];
    private int[] _sky = [];
    private int[] _pixels = [];

    /// <summary>Render pixels per M at the hole's distance, and the hole's centre in render pixels.</summary>
    public double Scale { get; private set; }

    public Point Centre { get; private set; }

    /// <summary>Where the labelled parts of the picture are, in render pixels (null if not in view).</summary>
    public ShadowAnchors Anchors { get; private set; } = new(null, null, null, null, null);

    /// <summary>The rays bent by gravity (or straight), for the chart and tests.</summary>
    public static IReadOnlyList<LightPath> Rays(bool bent) => bent ? Curved.Value : Straight.Value;

    /// <summary>The impact parameter of the ray through a point x M from the centre at the hole's distance.</summary>
    public static double ImpactAt(double x)
    {
        var delta = Math.Atan(x / CameraDistance);
        return CameraDistance * Math.Sin(delta) / Lapse;
    }

    /// <summary>The distance from the centre (M, at the hole's distance) that rays of impact parameter b pass through.</summary>
    public static double ScreenRadiusOf(double b)
    {
        var s = Math.Clamp(b * Lapse / CameraDistance, -0.999, 0.999);
        return CameraDistance * Math.Tan(Math.Asin(s));
    }

    /// <summary>The shadow's edge on the screen (M at the hole's distance): bent, the critical ray; straight, the horizon's outline.</summary>
    public static double ShadowRadius(bool bent) => bent
        ? ScreenRadiusOf(BlackHoleModel.CriticalImpact)
        : CameraDistance * Math.Tan(Math.Asin(BlackHoleModel.Horizon / CameraDistance));

    private static LightPath[] BuildRays(bool bent)
    {
        var count = (int)(MaxImpact / ImpactStep) + 2;
        var rays = new LightPath[count];
        Parallel.For(0, count, i =>
        {
            var b = Math.Max(i * ImpactStep, 1e-3);
            rays[i] = bent ? BlackHoleModel.TraceLight(b, CameraDistance, 0.01) : StraightRay(b);
        });
        return rays;
    }

    /// <summary>A straight line from the camera with impact parameter b, sampled like a bent one: u = sin(φ + δ)/p.</summary>
    private static LightPath StraightRay(double b)
    {
        const double step = 0.01;
        var p = b * Lapse;
        var delta = Math.Asin(p / CameraDistance);
        var captured = p < BlackHoleModel.Horizon;
        var end = captured ? Math.Asin(p / BlackHoleModel.Horizon) - delta : Math.PI - delta;
        var n = Math.Max(2, (int)Math.Ceiling(end / step) + 1);
        var u = new float[n];
        for (var i = 0; i < n; i++)
        {
            var phi = Math.Min(i * step, end);
            u[i] = (float)Math.Max(0, Math.Sin(phi + delta) / p);
        }

        return new LightPath(b, u, step, end, captured);
    }

    /// <summary>
    /// The picture for this size and tilt: <paramref name="width"/> × <paramref name="height"/> render pixels,
    /// <paramref name="scale"/> render pixels per M, the hole at <paramref name="centre"/>; <paramref name="inclination"/>
    /// degrees between the disk's axis and the line of sight (0 = from above, 90 = edge on).
    /// </summary>
    public IImage Render(int width, int height, double scale, Point centre, double inclination, bool bent, bool showDisk, bool doppler, bool showStars, double time)
    {
        var key = (width, height, Math.Round(scale, 3), Math.Round(centre.X, 2), Math.Round(centre.Y, 2), Math.Round(inclination, 2), bent);
        if (_bitmap is null || !Equals(key, _key))
        {
            _key = key;
            Build(width, height, scale, centre, inclination, bent);
        }

        Shade(showDisk, doppler, showStars, time);
        return _bitmap!;
    }

    /// <summary>Follows every pixel's light: where it crosses the disk first, or falls in, or which star it comes from.</summary>
    private void Build(int width, int height, double scale, Point centre, double inclination, bool bent)
    {
        if (_bitmap is null || _w != width || _h != height)
        {
            _w = width;
            _h = height;
            _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            var n = width * height;
            _kind = new byte[n];
            _order = new byte[n];
            _r = new float[n];
            _psi = new float[n];
            _lz = new float[n];
            _phi = new float[n];
            _sky = new int[n];
            _pixels = new int[n];
        }

        Scale = scale;
        Centre = centre;
        var rays = bent ? Curved.Value : Straight.Value;
        var footprint = 1 / (scale * CameraDistance);
        var inc = inclination * Math.PI / 180;
        var sinI = Math.Sin(inc);
        var cosI = Math.Cos(inc);

        Parallel.For(0, height, py =>
        {
            var y = (centre.Y - (py + 0.5)) / scale;
            for (var px = 0; px < width; px++)
            {
                var i = (py * width) + px;
                var x = (px + 0.5 - centre.X) / scale;
                var rho = Math.Sqrt((x * x) + (y * y));
                _kind[i] = 2;
                _sky[i] = 0;
                _order[i] = 0;
                if (rho < 1e-9) continue;

                var ex = x / rho;
                var ey = y / rho;
                var b = ImpactAt(rho);
                var at = b / ImpactStep;
                var row = Math.Min((int)at, rays.Length - 2);
                var f = at - row;
                var r0 = rays[row];
                var r1 = rays[row + 1];

                // Between two rays: their end angles blend unless one falls in and the other escapes.
                bool captured;
                double end;
                if (r0.Captured == r1.Captured)
                {
                    captured = r0.Captured;
                    end = r0.EndAngle + ((r1.EndAngle - r0.EndAngle) * f);
                }
                else
                {
                    var near = f < 0.5 ? r0 : r1;
                    captured = near.Captured;
                    end = near.EndAngle;
                }

                // The disk's plane crossings: (cos φ·e1 + sin φ·e2)·n = 0, every π from the first.
                var first = Math.Atan2(-cosI, ey * sinI);
                if (first < 0) first += Math.PI;
                for (var k = 0; k < 4; k++)
                {
                    var phi = first + (k * Math.PI);
                    if (phi >= end) break;
                    var u = r0.UAt(phi) + ((r1.UAt(phi) - r0.UAt(phi)) * f);
                    if (u <= 0) continue;
                    var r = 1 / u;
                    if (r < BlackHoleModel.Isco || r > DiskOuter) continue;

                    // Where on the disk: P = r(sin φ·ex, sin φ·ey, cos φ); disk axes a = x, c = (0, cos i, −sin i).
                    var s = Math.Sin(phi);
                    var pa = r * s * ex;
                    var pc = r * ((s * ey * cosI) - (Math.Cos(phi) * sinI));
                    _kind[i] = 1;
                    _order[i] = (byte)k;
                    _r[i] = (float)r;
                    _psi[i] = (float)Math.Atan2(pc, pa);
                    _lz[i] = (float)(-b * ex * sinI);
                    _phi[i] = (float)phi;
                    break;
                }

                if (captured) continue;

                // Out to the stars (also behind the disk, for when it is hidden): the direction the light came from, on a
                // sky fixed to the disk.
                var se = Math.Sin(end);
                var dx = se * ex;
                var dy = se * ey;
                var dz = Math.Cos(end);
                var da = dx;
                var dc = (dy * cosI) - (dz * sinI);
                var dn = (dy * sinI) + (dz * cosI);
                if (_kind[i] != 1) _kind[i] = 0;
                _sky[i] = StarSky.Colour(da, dc, dn, footprint);
            }
        });

        Anchors = FindAnchors();
    }

    /// <summary>Colours every pixel for the disk's turn at <paramref name="time"/> (seconds of animation).</summary>
    private void Shade(bool showDisk, bool doppler, bool showStars, double time)
    {
        var ramp = Ramp.Value.Value;
        var texture = DiskTexture.Value.Value;
        var t = time * TimeScale;
        Parallel.For(0, _h, py =>
        {
            for (var px = 0; px < _w; px++)
            {
                var i = (py * _w) + px;
                switch (_kind[i])
                {
                    case 1 when showDisk:
                    {
                        var r = _r[i];
                        var g = BlackHoleModel.DiskShift(r, _lz[i], doppler);
                        var psi = _psi[i] - (BlackHoleModel.DiskAngularSpeed(r) * t);
                        var light = BlackHoleModel.DiskGlow(r) * DiskTexture.At(texture, r, psi) * g * g * g * g;
                        light *= Math.Clamp((DiskOuter - r) / 3, 0, 1);
                        var temp = BlackHoleModel.DiskTemperature(r) * g;
                        _pixels[i] = Ramp.Colour(ramp, temp, 1 - Math.Exp(-1.15 * light));
                        break;
                    }

                    case 0 or 1:
                        _pixels[i] = showStars && _sky[i] != 0 ? _sky[i] : Black;
                        break;

                    default:
                        _pixels[i] = Black;
                        break;
                }
            }
        });

        SpaceDrawing.WritePixels(_bitmap!, _pixels, _w, _h);
    }

    private const int Black = unchecked((int)0xFF000000);

    /// <summary>
    /// The disk pixel brightest on the approaching side and dimmest on the other (both in front of the hole), the top of
    /// the far side's image arching over the top, the underside's image below the shadow, and the inner edge in front.
    /// </summary>
    /// <remarks>
    /// A ray's first crossing of the disk's plane (order 0) is in front of the hole (φ under 90°) or, for rays passing
    /// above it, behind it (φ near 180°: the far side bent over the top); the second crossing (order 1) of rays passing
    /// below is the underside.
    /// </remarks>
    private ShadowAnchors FindAnchors()
    {
        (int I, double Score)? hat = null, below = null, coming = null, going = null, inner = null;
        var cx = Centre.X;
        var cy = Centre.Y;
        for (var py = 0; py < _h; py += 2)
        {
            for (var px = 0; px < _w; px += 2)
            {
                var i = (py * _w) + px;
                if (_kind[i] != 1) continue;
                var x = px - cx;
                var y = cy - py;
                var front = _phi[i] <= (Math.PI / 2) + 0.01;
                if (_order[i] == 1)
                {
                    var score = -y - (2 * Math.Abs(x));
                    if (y < 0 && (below is null || score > below.Value.Score)) below = (i, score);
                    continue;
                }

                if (_order[i] != 0) continue;
                if (!front)
                {
                    var score = y - (2 * Math.Abs(x));
                    if (y > 0 && (hat is null || score > hat.Value.Score)) hat = (i, score);
                    continue;
                }

                var g = BlackHoleModel.DiskShift(_r[i], _lz[i]);
                if (x < 0 && (coming is null || g > coming.Value.Score)) coming = (i, g);
                if (x > 0 && (going is null || -g > going.Value.Score)) going = (i, -g);

                // The inner edge in front of the hole: its lowest point near the middle.
                if (_r[i] < BlackHoleModel.Isco + 0.6 && Math.Abs(x) < (0.25 * Math.Abs(y)) + 8)
                {
                    var score = -y;
                    if (inner is null || score > inner.Value.Score) inner = (i, score);
                }
            }
        }

        Point? At((int I, double Score)? hit) => hit is { } h ? new Point((h.I % _w) + 0.5, (h.I / _w) + 0.5) : null;
        return new ShadowAnchors(At(hat), At(below), At(coming), At(going), At(inner));
    }

    /// <summary>The disk's turbulent pattern: brighter and darker streaks round it, the same all the way round once.</summary>
    private static class DiskTexture
    {
        private const int Rings = 192;
        private const int Around = 512;

        public static readonly Lazy<float[]> Value = new(Make, isThreadSafe: true);

        public static double At(float[] texture, double r, double psi)
        {
            var ri = Math.Clamp((int)((r - BlackHoleModel.Isco) / (DiskOuter - BlackHoleModel.Isco) * (Rings - 1)), 0, Rings - 1);
            var a = psi / (2 * Math.PI);
            a -= Math.Floor(a);
            var ai = Math.Min(Around - 1, (int)(a * Around));
            return texture[(ri * Around) + ai];
        }

        private static float[] Make()
        {
            var texture = new float[Rings * Around];
            var random = new Random(31);
            var waves = Enumerable.Range(0, 26).Select(_ => (
                M: random.Next(1, 9),
                K: (random.NextDouble() * 9) + 1,
                Phase: random.NextDouble() * Math.PI * 2,
                A: 0.25 + random.NextDouble())).ToArray();
            var norm = waves.Sum(w => w.A);
            for (var ri = 0; ri < Rings; ri++)
            {
                var r = BlackHoleModel.Isco + ((DiskOuter - BlackHoleModel.Isco) * ri / (Rings - 1));

                // Fine rings across the disk, the gaps and lanes of the gas.
                var lanes = 0.75 + (0.25 * Math.Sin((r * 4.1) + (1.7 * Math.Sin(r * 1.3))));
                for (var ai = 0; ai < Around; ai++)
                {
                    var psi = 2 * Math.PI * ai / Around;
                    var sum = 0.0;
                    foreach (var w in waves) sum += w.A * Math.Sin((w.M * psi) + (w.K * r) + w.Phase);
                    var v = 0.62 + (0.38 * sum / norm * 2.2);
                    texture[(ri * Around) + ai] = (float)Math.Clamp(v * lanes, 0.15, 1.3);
                }
            }

            return texture;
        }
    }

    /// <summary>Blackbody colours by temperature, relative to the disk's hottest ring (1 ≈ 4200 K, a warm orange-white), a little more saturated than a real blackbody.</summary>
    private static class Ramp
    {
        private const int Steps = 256;
        private const double Top = 2.5;

        public static readonly Lazy<(double R, double G, double B)[]> Value = new(() =>
            Enumerable.Range(0, Steps).Select(i => Saturate(BlackHoleDrawing.Blackbody(4200 * Math.Max(0.12, i * Top / (Steps - 1))))).ToArray(), isThreadSafe: true);

        private static (double R, double G, double B) Saturate((double R, double G, double B) c) => (Math.Pow(c.R, 1.4), Math.Pow(c.G, 1.4), Math.Pow(c.B, 1.4));

        public static int Colour((double R, double G, double B)[] ramp, double temperature, double brightness)
        {
            var c = ramp[Math.Clamp((int)(temperature / Top * (Steps - 1)), 0, Steps - 1)];
            var k = Math.Clamp(brightness, 0, 1) * 255;
            var r = (int)Math.Min(255, c.R * k);
            var g = (int)Math.Min(255, c.G * k);
            var b = (int)Math.Min(255, c.B * k);
            return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
        }
    }
}

/// <summary>Points of the up-close picture to hang labels on (render pixels), each null when it is not in view.</summary>
internal sealed record ShadowAnchors(Point? Hat, Point? Below, Point? Coming, Point? Going, Point? Inner);

/// <summary>
/// The sky round the hole: a soft band of the Milky Way and sharp stars, worked out for any direction, so stars stay
/// points however much the hole magnifies them. The stars are invented (one chance in 30 per cell of a cube-map grid),
/// not a real catalogue.
/// </summary>
internal static class StarSky
{
    /// <summary>Cells along each edge of each of the six cube faces (about 0.1° each).</summary>
    private const int Cells = 900;

    /// <summary>
    /// The sky's colour in direction (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) (a unit vector in
    /// the disk's frame, z along its axis); <paramref name="footprint"/> is a pixel's width on the sky, radians, which
    /// sets how wide a star is drawn.
    /// </summary>
    public static int Colour(double x, double y, double z, double footprint)
    {
        // The Milky Way: a band along a great circle tilted 60° to the disk, brighter towards +x, mottled.
        var off = (y * 0.866) + (z * 0.5);
        var band = Math.Exp(-(off * off) / 0.018) * (0.55 + (0.45 * x));
        var mottle = 0.65 + (0.35 * Math.Sin((x * 23) + (y * 17)) * Math.Sin((y * 29) - (z * 13)));
        var r = band * mottle * 0.07;
        var g = band * mottle * 0.065;
        var b = band * mottle * 0.085;

        // The cube face and the point on it.
        var ax = Math.Abs(x);
        var ay = Math.Abs(y);
        var az = Math.Abs(z);
        int face;
        double u, v, m;
        if (ax >= ay && ax >= az)
        {
            face = x > 0 ? 0 : 1;
            m = ax;
            u = y / m;
            v = z / m;
        }
        else if (ay >= az)
        {
            face = y > 0 ? 2 : 3;
            m = ay;
            u = x / m;
            v = z / m;
        }
        else
        {
            face = z > 0 ? 4 : 5;
            m = az;
            u = x / m;
            v = y / m;
        }

        // Face units per radian here (the face is flat, so cells shrink on the sky towards its corners).
        var stretch = 1 + (u * u) + (v * v);
        var sigma = Math.Max(0.00022, footprint * 0.55);
        var cu = (u + 1) / 2 * Cells;
        var cv = (v + 1) / 2 * Cells;
        var ci = (int)cu;
        var cj = (int)cv;
        for (var di = -1; di <= 1; di++)
        {
            for (var dj = -1; dj <= 1; dj++)
            {
                var i = ci + di;
                var j = cj + dj;
                var h = Hash(face, i, j);
                if ((h & 1023) >= 22) continue;
                var fx = 0.1 + (0.8 * ((h >> 10) & 255) / 255.0);
                var fy = 0.1 + (0.8 * ((h >> 18) & 255) / 255.0);
                var du = (i + fx - cu) * 2 / Cells;
                var dv = (j + fy - cv) * 2 / Cells;
                var angle2 = ((du * du) + (dv * dv)) / stretch;
                var w = Math.Exp(-angle2 / (2 * sigma * sigma));
                if (w < 0.004) continue;
                var h2 = Hash(face + 11, i, j);
                var bright = (Math.Pow((h2 & 4095) / 4095.0, 6) * 2.2) + 0.08;
                var tint = ((h2 >> 12) & 255) / 255.0;
                var (tr, tg, tb) = tint < 0.25 ? (1.0, 0.8, 0.6) : tint < 0.6 ? (1.0, 0.96, 0.9) : (0.78, 0.86, 1.0);
                r += bright * w * tr;
                g += bright * w * tg;
                b += bright * w * tb;
            }
        }

        static int Channel(double c) => (int)(255 * Math.Min(1, Math.Pow(Math.Max(0, c), 0.8)));
        return unchecked((int)0xFF000000) | (Channel(r) << 16) | (Channel(g) << 8) | Channel(b);
    }

    private static uint Hash(int face, int i, int j)
    {
        unchecked
        {
            var h = ((uint)face * 0x9E3779B1u) ^ ((uint)i * 0x85EBCA77u) ^ ((uint)j * 0xC2B2AE3Du);
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return h;
        }
    }
}
