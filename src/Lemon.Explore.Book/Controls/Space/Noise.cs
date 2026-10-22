namespace Lemon.Explore.Book.Controls.Space;

/// <summary>Small double-precision 3D vector, enough for the sphere ray casting and procedural textures.</summary>
internal readonly record struct V3(double X, double Y, double Z)
{
    public static V3 operator +(V3 a, V3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static V3 operator -(V3 a, V3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static V3 operator *(V3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);

    public double Length => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

    public static double Dot(V3 a, V3 b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    public static V3 Cross(V3 a, V3 b) =>
        new((a.Y * b.Z) - (a.Z * b.Y), (a.Z * b.X) - (a.X * b.Z), (a.X * b.Y) - (a.Y * b.X));

    public V3 Normalized()
    {
        var length = Length;
        return length < 1e-12 ? this : this * (1 / length);
    }
}

/// <summary>
/// Deterministic gradient (Perlin) and cellular (Worley) noise. Textures are sampled on the unit sphere in 3D,
/// so they wrap without a seam at the date line and do not pinch at the poles.
/// </summary>
internal static class Noise
{
    private static readonly int[] Perm = BuildPermutation(20191214);

    public static double Perlin(V3 p) => Perlin(p.X, p.Y, p.Z);

    public static double Perlin(double x, double y, double z)
    {
        var xi = (int)Math.Floor(x);
        var yi = (int)Math.Floor(y);
        var zi = (int)Math.Floor(z);
        x -= xi;
        y -= yi;
        z -= zi;
        xi &= 255;
        yi &= 255;
        zi &= 255;

        var u = Fade(x);
        var v = Fade(y);
        var w = Fade(z);

        var a = Perm[xi] + yi;
        var aa = Perm[a] + zi;
        var ab = Perm[a + 1] + zi;
        var b = Perm[xi + 1] + yi;
        var ba = Perm[b] + zi;
        var bb = Perm[b + 1] + zi;

        return Lerp(w,
            Lerp(v,
                Lerp(u, Grad(Perm[aa], x, y, z), Grad(Perm[ba], x - 1, y, z)),
                Lerp(u, Grad(Perm[ab], x, y - 1, z), Grad(Perm[bb], x - 1, y - 1, z))),
            Lerp(v,
                Lerp(u, Grad(Perm[aa + 1], x, y, z - 1), Grad(Perm[ba + 1], x - 1, y, z - 1)),
                Lerp(u, Grad(Perm[ab + 1], x, y - 1, z - 1), Grad(Perm[bb + 1], x - 1, y - 1, z - 1))));
    }

    /// <summary>Fractal Brownian motion: roughly in [-1, 1], most values within ±0.5.</summary>
    public static double Fbm(V3 p, int octaves, double lacunarity = 2.0, double gain = 0.5)
    {
        double sum = 0, amplitude = 0.5;
        for (var i = 0; i < octaves; i++)
        {
            sum += amplitude * Perlin(p);
            p = (p * lacunarity) + new V3(17.3, -9.1, 4.7);
            amplitude *= gain;
        }

        return sum;
    }

    /// <summary>Distance to the nearest jittered feature point (cell units) and a random value for that cell.</summary>
    public static (double Distance, double CellRandom) Worley(V3 p)
    {
        var cx = (int)Math.Floor(p.X);
        var cy = (int)Math.Floor(p.Y);
        var cz = (int)Math.Floor(p.Z);
        var best = double.MaxValue;
        var bestHash = 0.0;

        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var x = cx + dx;
                    var y = cy + dy;
                    var z = cz + dz;
                    var fx = x + Hash(x, y, z, 1) - p.X;
                    var fy = y + Hash(x, y, z, 2) - p.Y;
                    var fz = z + Hash(x, y, z, 3) - p.Z;
                    var d = (fx * fx) + (fy * fy) + (fz * fz);
                    if (d < best)
                    {
                        best = d;
                        bestHash = Hash(x, y, z, 4);
                    }
                }
            }
        }

        return (Math.Sqrt(best), bestHash);
    }

    /// <summary>Stable pseudo-random value in [0, 1) for an integer lattice point.</summary>
    public static double Hash(int x, int y, int z, int salt)
    {
        unchecked
        {
            var h = (uint)((x * 73856093) ^ (y * 19349663) ^ (z * 83492791) ^ (salt * 2654435761u));
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / (double)0x1000000;
        }
    }

    public static double SmoothStep(double edge0, double edge1, double x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - (2 * t));
    }

    private static double Fade(double t) => t * t * t * ((t * ((t * 6) - 15)) + 10);

    private static double Lerp(double t, double a, double b) => a + (t * (b - a));

    private static double Grad(int hash, double x, double y, double z)
    {
        var h = hash & 15;
        var u = h < 8 ? x : y;
        var v = h < 4 ? y : h is 12 or 14 ? x : z;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    private static int[] BuildPermutation(int seed)
    {
        var random = new Random(seed);
        var p = Enumerable.Range(0, 256).ToArray();
        for (var i = 255; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        var perm = new int[512];
        for (var i = 0; i < 512; i++)
        {
            perm[i] = p[i & 255];
        }

        return perm;
    }
}
