using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Expansion;

/// <summary>A cell of the endless galaxy lattice; (0, 0) holds the Milky Way.</summary>
public readonly record struct GalaxyCell(int I, int J)
{
    public static GalaxyCell MilkyWay => new(0, 0);

    public bool IsMilkyWay => I == 0 && J == 0;
}

/// <summary>A galaxy of the Hubble view: where it sits in comoving space (Mpc) and how it looks.</summary>
public sealed record FieldGalaxy(GalaxyCell Cell, Vector Position, int Look, double Size);

/// <summary>
/// An endless, even sprinkling of galaxies in comoving space: one per lattice cell, nudged by a fixed hash so the
/// pattern looks natural but is the same every time. Whichever galaxy is home, space looks the same all round it.
/// </summary>
public static class GalaxyField
{
    /// <summary>The lattice spacing, Mpc (each dot stands for a big cluster of galaxies).</summary>
    public const double Spacing = 80;

    /// <summary>How many sprite looks there are (spirals at different tilts, ellipticals).</summary>
    public const int Looks = 8;

    /// <summary>The galaxy of a cell.</summary>
    public static FieldGalaxy At(GalaxyCell cell)
    {
        if (cell.IsMilkyWay) return new FieldGalaxy(cell, new Vector(0, 0), 0, 1.25);
        var h = Hash(cell.I, cell.J);
        var dx = (Unit(h, 0) - 0.5) * 0.62;
        var dy = (Unit(h, 1) - 0.5) * 0.62;
        var look = (int)(Unit(h, 2) * Looks) % Looks;
        var size = 0.75 + (0.4 * Unit(h, 3));
        return new FieldGalaxy(cell, new Vector((cell.I + dx) * Spacing, (cell.J + dy) * Spacing), look, size);
    }

    /// <summary>The galaxies whose cells overlap a box in comoving space (Mpc).</summary>
    public static IEnumerable<FieldGalaxy> Within(Rect box)
    {
        var i0 = (int)Math.Floor(box.Left / Spacing) - 1;
        var i1 = (int)Math.Ceiling(box.Right / Spacing) + 1;
        var j0 = (int)Math.Floor(box.Top / Spacing) - 1;
        var j1 = (int)Math.Ceiling(box.Bottom / Spacing) + 1;
        for (var j = j0; j <= j1; j++)
        {
            for (var i = i0; i <= i1; i++)
            {
                var galaxy = At(new GalaxyCell(i, j));
                if (box.Contains((Point)galaxy.Position)) yield return galaxy;
            }
        }
    }

    private static ulong Hash(int i, int j)
    {
        // SplitMix64 of the two indices.
        var x = ((ulong)(uint)i * 0x9E3779B97F4A7C15UL) ^ ((ulong)(uint)j * 0xC2B2AE3D27D4EB4FUL) ^ 0x165667B19E3779F9UL;
        x ^= x >> 30;
        x *= 0xBF58476D1CE4E5B9UL;
        x ^= x >> 27;
        x *= 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return x;
    }

    /// <summary>The k-th number in [0, 1) drawn from a hash.</summary>
    private static double Unit(ulong hash, int k)
    {
        var x = hash + ((ulong)(k + 1) * 0x9E3779B97F4A7C15UL);
        x ^= x >> 33;
        x *= 0xFF51AFD7ED558CCDUL;
        x ^= x >> 33;
        return (x >> 11) * (1.0 / (1UL << 53));
    }
}

/// <summary>The stretch of time the Hubble view plays: from when the universe was 0.6 of today's size until now.</summary>
public static class HubbleSpan
{
    public const double StartScale = 0.6;

    /// <summary>The time (Gyr after the start) at a point of the span, 0 to 1, evenly in time.</summary>
    public static double Time(double progress)
    {
        var u = Universe.Ours;
        var start = u.TimeAt(StartScale);
        return start + (Math.Clamp(progress, 0, 1) * (u.Age - start));
    }

    public static double Scale(double progress) => Universe.Ours.ScaleAt(Time(progress));
}
