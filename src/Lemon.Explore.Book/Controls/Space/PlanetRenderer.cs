using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Lemon.Explore.Book.Controls.Space;

/// <summary>
/// Software ray caster for one body: an orthographic view of a textured, lit sphere with an atmosphere rim and,
/// for Saturn and Uranus, a ring plane that passes in front of and behind the planet and casts/receives shadow.
/// </summary>
/// <remarks>
/// View space: x right, y up, z towards the viewer; the planet is the unit sphere. Everything that only depends on
/// the image size and the pole (normals, latitude, ring hits, rim terms) is cached, so a frame only re-does texture
/// lookups and lighting, split across cores for large images. Must be called on the UI thread (it owns a
/// <see cref="WriteableBitmap"/>). Ported from the WPF original with the shading unchanged.
/// </remarks>
public sealed class PlanetRenderer
{
    private const double ViewInclination = 18 * Math.PI / 180;
    private const int ParallelThreshold = 4_096;

    private readonly SpaceBody _body;
    private readonly double _extent;
    private readonly V3 _pole;
    private readonly V3 _e1;
    private readonly V3 _e2;

    private int _size;
    private int _activePixels;
    private uint[] _pixels = [];
    private int[] _rowStart = [], _rowEnd = [];
    private float[] _nx = [], _ny = [], _nz = [], _coverage = [], _lonTurns = [], _v = [], _fresnel = [], _halo = [];
    private float[] _ringT = [], _ringZ = [], _ringShadow = [];
    private LightVector _lastShadowLight;

    public PlanetRenderer(SpaceBody body)
        : this(body, DefaultPole(body))
    {
    }

    /// <summary>A renderer whose north pole points along <paramref name="pole"/> in view space.</summary>
    public PlanetRenderer(SpaceBody body, LightVector pole)
    {
        _body = body;
        var atmosphere = body.AtmosphereColor is null ? 0.03 : 0.12;
        _extent = Math.Max(body.Rings?.Outer * 1.01 ?? 0, 1 + atmosphere);

        _pole = new V3(pole.X, pole.Y, pole.Z).Normalized();
        // Longitude 0 faces the viewer; with the pole pointing straight at the viewer any direction will do.
        var facing = Math.Abs(_pole.Z) > 0.999 ? new V3(1, 0, 0) : new V3(0, 0, 1);
        _e1 = (facing - (_pole * V3.Dot(facing, _pole))).Normalized();
        _e2 = V3.Cross(_pole, _e1);
    }

    /// <summary>The pole leans towards the viewer a little (so rings open up) and rolls by the axial tilt.</summary>
    private static LightVector DefaultPole(SpaceBody body)
    {
        var roll = -body.AxialTiltDegrees * Math.PI / 180;
        var (sr, cr) = Math.SinCos(roll);
        var (si, ci) = Math.SinCos(ViewInclination);
        return LightVector.Normalize(-sr * ci, cr * ci, si);
    }

    public WriteableBitmap? Bitmap { get; private set; }

    /// <summary>Image half-size in planet radii: the bitmap spans [-Extent, Extent] around the centre.</summary>
    public double Extent => _extent;

    /// <summary>Bitmap edge in device pixels for a planet of this radius, rounded so zooming does not reallocate every frame.</summary>
    public int SizeFor(double radiusPixels)
    {
        var size = (int)Math.Ceiling(2 * radiusPixels * _extent);
        var step = size <= 64 ? 2 : size <= 128 ? 4 : size <= 256 ? 8 : 16;
        return Math.Clamp(((size + step - 1) / step) * step, 4, 720);
    }

    /// <summary>Draws the body into <see cref="Bitmap"/>.</summary>
    /// <param name="size">Bitmap edge in pixels (see <see cref="SizeFor"/>).</param>
    /// <param name="light">Unit vector towards the light source in view space.</param>
    /// <param name="spin">Rotation about the pole in radians.</param>
    public void Render(int size, LightVector light, double spin)
    {
        EnsureGeometry(size);
        UpdateRingShadows(light);

        var texture = PlanetTexture.TryGet(_body);
        var half = new V3(light.X, light.Y, light.Z + 1).Normalized();
        var atmosphere = _body.AtmosphereColor ?? Colors.Transparent;
        var flat = _body.BaseColor;
        var spinTurns = spin / (2 * Math.PI);

        var frame = new Frame
        {
            Texture = texture,
            Level = texture?.LevelFor(size / (2 * _extent)) ?? 0,
            Ring = texture?.RingProfile,
            Lx = (float)light.X,
            Ly = (float)light.Y,
            Lz = (float)light.Z,
            Hx = (float)half.X,
            Hy = (float)half.Y,
            Hz = (float)half.Z,
            FlatR = flat.R / 255f,
            FlatG = flat.G / 255f,
            FlatB = flat.B / 255f,
            AtmosphereR = atmosphere.R / 255f,
            AtmosphereG = atmosphere.G / 255f,
            AtmosphereB = atmosphere.B / 255f,
            AtmosphereStrength = (float)_body.AtmosphereStrength,
            IsStar = _body.IsStar,
            SpinTurns = spinTurns - Math.Floor(spinTurns),
            PoleDotLight = (_pole.X * light.X) + (_pole.Y * light.Y) + (_pole.Z * light.Z),
        };

        if (_activePixels > ParallelThreshold)
        {
            Parallel.For(0, size, row => ShadeRow(row, frame));
        }
        else
        {
            for (var row = 0; row < size; row++) ShadeRow(row, frame);
        }

        SpaceDrawing.WritePixels(Bitmap!, _pixels, _size, _size);
    }

    private void ShadeRow(int row, Frame f)
    {
        var ring = f.Ring;
        var ringSamples = ring is null ? 0 : ring.Length / 4;
        var hasRing = ringSamples > 0 && _ringT.Length > 0;
        var rings = _body.Rings;
        var pole = _pole;
        var start = (row * _size) + _rowStart[row];
        var end = (row * _size) + _rowEnd[row];

        for (var i = start; i < end; i++)
        {
            float outR = 0, outG = 0, outB = 0, outA = 0;
            var coverage = _coverage[i];
            var hasSphere = coverage > 0;

            // Ring sample, lit, possibly in the planet's shadow; drawn under the sphere when it is behind it.
            float rr = 0, rg = 0, rb = 0, ra = 0;
            var ringInFront = false;
            if (hasRing && _ringT[i] >= 0)
            {
                var k = Math.Min((int)(_ringT[i] * ringSamples), ringSamples - 1) * 4;
                ra = ring![k + 3];
                if (ra > 0.002f)
                {
                    var shade = _ringShadow[i];
                    rr = ring[k] * shade;
                    rg = ring[k + 1] * shade;
                    rb = ring[k + 2] * shade;
                    ringInFront = !hasSphere || _ringZ[i] > _nz[i];
                    if (!ringInFront) Over(ref outR, ref outG, ref outB, ref outA, rr, rg, rb, ra);
                }
                else
                {
                    ra = 0;
                }
            }

            if (hasSphere)
            {
                var nx = _nx[i];
                var ny = _ny[i];
                var nz = _nz[i];
                float r, g, b, spec;
                if (f.Texture is { } texture)
                {
                    var u = _lonTurns[i] + f.SpinTurns;
                    if (u >= 1) u -= 1;
                    texture.Sample(f.Level, u, _v[i], out r, out g, out b, out spec);
                    r *= 1 / 255f;
                    g *= 1 / 255f;
                    b *= 1 / 255f;
                    spec *= 1 / 255f;
                }
                else
                {
                    (r, g, b, spec) = (f.FlatR, f.FlatG, f.FlatB, 0);
                }

                if (f.IsStar)
                {
                    // Limb darkening (the edge of the disc is cooler and dimmer); the centre burns towards white.
                    var mu = MathF.Sqrt(nz);
                    var dim = 0.42f + (0.58f * mu);
                    r = MathF.Min(1, r * (0.25f + (0.85f * dim)));
                    g *= dim * (0.82f + (0.18f * mu));
                    b *= dim * dim;
                    var core = 0.55f * nz * nz;
                    r += (1 - r) * core;
                    g += (1 - g) * core;
                    b += (1 - b) * core * 0.8f;
                }
                else
                {
                    var d = (nx * f.Lx) + (ny * f.Ly) + (nz * f.Lz);
                    var lit = MathF.Max(0, d);
                    var terminator = Math.Clamp((d + 0.06f) / 0.22f, 0f, 1f);

                    // ≈ lit^0.85: lifts the mid-tones so the lit half does not look flat.
                    var intensity = 0.03f + (1.08f * lit * (1.15f - (0.15f * lit)) * terminator);

                    // Shadow of the rings on the cloud tops.
                    if (hasRing && Math.Abs(f.PoleDotLight) > 1e-4)
                    {
                        var t = -((pole.X * nx) + (pole.Y * ny) + (pole.Z * nz)) / f.PoleDotLight;
                        if (t > 0)
                        {
                            var qx = nx + (t * f.Lx);
                            var qy = ny + (t * f.Ly);
                            var qz = nz + (t * f.Lz);
                            var radius = Math.Sqrt((qx * qx) + (qy * qy) + (qz * qz));
                            if (radius > rings!.Inner && radius < rings.Outer)
                            {
                                var k = Math.Min((int)((radius - rings.Inner) / (rings.Outer - rings.Inner) * ringSamples), ringSamples - 1);
                                intensity *= 1 - (0.75f * ring![(k * 4) + 3]);
                            }
                        }
                    }

                    r *= intensity;
                    g *= intensity;
                    b *= intensity;

                    if (spec > 0 && d > 0)
                    {
                        var nh = MathF.Max(0, (nx * f.Hx) + (ny * f.Hy) + (nz * f.Hz));
                        var highlight = spec * MathF.Pow(nh, 60) * 0.65f;
                        r += highlight * 0.9f;
                        g += highlight * 0.95f;
                        b += highlight;
                    }

                    if (f.AtmosphereStrength > 0)
                    {
                        var fresnel = _fresnel[i] * f.AtmosphereStrength * Math.Clamp(d + 0.35f, 0f, 1f);
                        r += f.AtmosphereR * fresnel;
                        g += f.AtmosphereG * fresnel;
                        b += f.AtmosphereB * fresnel;
                    }
                }

                Over(ref outR, ref outG, ref outB, ref outA, MathF.Min(r, 1), MathF.Min(g, 1), MathF.Min(b, 1), coverage);
            }

            // Glow of the atmosphere just outside the limb, brightest on the day side.
            var halo = _halo[i];
            if (halo > 0 && f.AtmosphereStrength > 0)
            {
                var day = Math.Clamp((_nx[i] * f.Lx) + (_ny[i] * f.Ly) + 0.35f, 0f, 1f);
                var a = halo * f.AtmosphereStrength * day * 0.85f * (1 - coverage);
                if (a > 0.002f) Over(ref outR, ref outG, ref outB, ref outA, f.AtmosphereR, f.AtmosphereG, f.AtmosphereB, a);
            }

            if (ringInFront) Over(ref outR, ref outG, ref outB, ref outA, rr, rg, rb, ra);

            _pixels[i] = outA <= 0
                ? 0
                : ((uint)((outA * 255) + 0.5f) << 24) | ((uint)((Math.Min(outR, outA) * 255) + 0.5f) << 16)
                  | ((uint)((Math.Min(outG, outA) * 255) + 0.5f) << 8) | (uint)((Math.Min(outB, outA) * 255) + 0.5f);
        }
    }

    /// <summary>Back-to-front "over" compositing into premultiplied colour.</summary>
    private static void Over(ref float r, ref float g, ref float b, ref float a, float sr, float sg, float sb, float sa)
    {
        r = (sr * sa) + (r * (1 - sa));
        g = (sg * sa) + (g * (1 - sa));
        b = (sb * sa) + (b * (1 - sa));
        a = sa + (a * (1 - sa));
    }

    private void EnsureGeometry(int size)
    {
        if (size == _size && Bitmap is not null) return;

        _size = size;
        var count = size * size;
        _pixels = new uint[count];
        _rowStart = new int[size];
        _rowEnd = new int[size];
        _nx = new float[count];
        _ny = new float[count];
        _nz = new float[count];
        _coverage = new float[count];
        _lonTurns = new float[count];
        _v = new float[count];
        _fresnel = new float[count];
        _halo = new float[count];
        var rings = _body.Rings;
        _ringT = rings is null ? [] : new float[count];
        _ringZ = rings is null ? [] : new float[count];
        _ringShadow = rings is null ? [] : new float[count];

        var pixelsPerRadius = size / (2 * _extent);
        var atmosphereHeight = _extent - 1;
        var active = new int[size];

        Parallel.For(0, size, j =>
        {
            int first = size, last = 0;
            for (var i = 0; i < size; i++)
            {
                var index = (j * size) + i;
                var x = (((i + 0.5) / size * 2) - 1) * _extent;
                var y = -(((j + 0.5) / size * 2) - 1) * _extent;
                var rr = (x * x) + (y * y);
                var r = Math.Sqrt(rr);
                var used = false;

                var coverage = Math.Clamp(((1 - r) * pixelsPerRadius) + 0.5, 0, 1);
                _coverage[index] = (float)coverage;

                var z = rr < 1 ? Math.Sqrt(1 - rr) : 0;
                var n = rr < 1 ? new V3(x, y, z) : new V3(x / r, y / r, 0);
                _nx[index] = (float)n.X;
                _ny[index] = (float)n.Y;
                _nz[index] = (float)n.Z;

                if (coverage > 0)
                {
                    used = true;
                    var lat = Math.Asin(Math.Clamp(V3.Dot(n, _pole), -1, 1));
                    var lon = Math.Atan2(V3.Dot(n, _e2), V3.Dot(n, _e1));
                    _lonTurns[index] = (float)((lon / (2 * Math.PI)) + 0.5);
                    _v[index] = (float)(0.5 - (lat / Math.PI));
                    _fresnel[index] = (float)Math.Pow(1 - z, 2.6);
                }

                if (r > 1 && atmosphereHeight > 0.05)
                {
                    var f = 1 - ((r - 1) / atmosphereHeight);
                    if (f > 0)
                    {
                        _halo[index] = (float)(f * f * f);
                        used = true;
                    }
                }
                else if (r > 0.97)
                {
                    _halo[index] = 1;
                }

                if (rings is not null)
                {
                    _ringT[index] = -1;
                    if (_pole.Z > 1e-3)
                    {
                        // Where the view ray through (x, y) crosses the ring plane.
                        var zr = -((_pole.X * x) + (_pole.Y * y)) / _pole.Z;
                        var radius = Math.Sqrt(rr + (zr * zr));
                        if (radius >= rings.Inner && radius <= rings.Outer)
                        {
                            _ringT[index] = (float)((radius - rings.Inner) / (rings.Outer - rings.Inner));
                            _ringZ[index] = (float)zr;
                            _ringShadow[index] = 1;
                            used = true;
                        }
                    }
                }

                if (used)
                {
                    first = Math.Min(first, i);
                    last = i + 1;
                }
            }

            // Only the span of each row that can hold something gets shaded; the rest stays transparent.
            _rowStart[j] = first < last ? first : 0;
            _rowEnd[j] = first < last ? last : 0;
            active[j] = Math.Max(0, last - first);
        });

        _activePixels = active.Sum();
        Bitmap?.Dispose();
        Bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        _lastShadowLight = default;
    }

    /// <summary>Planet shadow on the rings; only recomputed when the light has moved noticeably.</summary>
    private void UpdateRingShadows(LightVector light)
    {
        if (_ringT.Length == 0) return;
        if (LightVector.DotProduct(light, _lastShadowLight) > 0.99995) return;
        _lastShadowLight = light;

        var size = _size;
        Parallel.For(0, size, j =>
        {
            for (var i = _rowStart[j]; i < _rowEnd[j]; i++)
            {
                var index = (j * size) + i;
                if (_ringT[index] < 0) continue;

                var x = (((i + 0.5) / size * 2) - 1) * _extent;
                var y = -(((j + 0.5) / size * 2) - 1) * _extent;
                var z = _ringZ[index];

                // Does the ray from the ring point towards the light hit the planet?
                var pDotL = (x * light.X) + (y * light.Y) + (z * light.Z);
                var pp = (x * x) + (y * y) + (z * z);
                var disc = (pDotL * pDotL) - (pp - 1);
                var inShadow = disc > 0 && -pDotL - Math.Sqrt(disc) > 0;
                _ringShadow[index] = inShadow ? 0.1f : 1f;
            }
        });
    }

    /// <summary>Per-frame constants shared by every row.</summary>
    private readonly struct Frame
    {
        public PlanetTexture? Texture { get; init; }
        public int Level { get; init; }
        public float[]? Ring { get; init; }
        public float Lx { get; init; }
        public float Ly { get; init; }
        public float Lz { get; init; }
        public float Hx { get; init; }
        public float Hy { get; init; }
        public float Hz { get; init; }
        public float FlatR { get; init; }
        public float FlatG { get; init; }
        public float FlatB { get; init; }
        public float AtmosphereR { get; init; }
        public float AtmosphereG { get; init; }
        public float AtmosphereB { get; init; }
        public float AtmosphereStrength { get; init; }
        public bool IsStar { get; init; }
        public double SpinTurns { get; init; }
        public double PoleDotLight { get; init; }
    }
}

/// <summary>Direction towards the light in a renderer's view space (x right, y up, z towards the viewer).</summary>
public readonly record struct LightVector(double X, double Y, double Z)
{
    public static LightVector Normalize(double x, double y, double z)
    {
        var length = Math.Sqrt((x * x) + (y * y) + (z * z));
        return length < 1e-12 ? new LightVector(0, 0, 1) : new LightVector(x / length, y / length, z / length);
    }

    public static double DotProduct(LightVector a, LightVector b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
}
