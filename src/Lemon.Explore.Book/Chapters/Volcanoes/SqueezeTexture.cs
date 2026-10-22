namespace Lemon.Explore.Book.Chapters.Volcanoes;

/// <summary>A flake or grain in a squeezed rock: mica (shiny, platy), quartz and feldspar (light), or dark minerals.</summary>
public enum FlakeKind
{
    Mica,
    Light,
    Dark,
}

/// <summary>Where a flake is drawn at a grade: its centre in the 0–1 square, its angle (0 = lying flat), its length and width.</summary>
public readonly record struct Flake(double X, double Y, double Angle, double Length, double Width, FlakeKind Kind);

/// <summary>
/// What a buried rock looks like under the magnifier as heat and pressure rise (grade 0 at 200 °C, 1 at 700 °C). Shale's
/// tiny clay flakes lie every which way; squeezed, they turn to lie across the push (slate, which splits into sheets),
/// grow into shiny mica (phyllite, schist), garnets grow in them, and at the highest grade the light and dark minerals
/// gather into bands (gneiss) and the first melt appears (migmatite). Sandstone's and limestone's grains grow into each
/// other and the gaps between them close (quartzite, marble).
/// </summary>
/// <remarks>Positions come from a hash of the flake's number, so the same flake moves smoothly as the grade changes.</remarks>
public static class SqueezeTexture
{
    /// <summary>Flakes in the shale picture.</summary>
    public const int FlakeCount = 900;

    /// <summary>The distance between dark bands in gneiss, as a share of the picture's height.</summary>
    public const double BandSpacing = 0.18;

    /// <summary>Grains of sandstone or limestone before they grow, and the fewest left when fully grown.</summary>
    public const int MostGrains = 260;

    public const int FewestGrains = 34;

    /// <summary>How fully the flakes have turned to lie across the push: done by slate.</summary>
    public static double Alignment(double grade) => VolcanoModel.SmoothStep(0.0, 0.3, grade);

    /// <summary>How far the light and dark minerals have gathered into bands: from schist to gneiss.</summary>
    public static double Banding(double grade) => VolcanoModel.SmoothStep(0.62, 0.92, grade);

    /// <summary>How far garnets have grown (none below about 450 °C).</summary>
    public static double GarnetGrowth(double grade) => VolcanoModel.SmoothStep(0.48, 0.85, grade);

    /// <summary>How much of the rock has melted (from about 680 °C).</summary>
    public static double Melt(double grade) => VolcanoModel.SmoothStep(0.94, 1.0, grade);

    /// <summary>Flake <paramref name="index"/> at <paramref name="grade"/>.</summary>
    public static Flake FlakeAt(int index, double grade)
    {
        var x = Hash(index, 1);
        var y0 = Hash(index, 2);
        var kind = Hash(index, 3) switch
        {
            < 0.55 => FlakeKind.Mica,
            < 0.88 => FlakeKind.Light,
            _ => FlakeKind.Dark,
        };

        // At first every way; squeezed, turned to lie across the push with a little scatter left.
        var start = (Hash(index, 4) - 0.5) * Math.PI;
        var scatter = (Hash(index, 5) - 0.5) * 0.18;
        var angle = start + ((scatter - start) * Alignment(grade));

        // The flakes grow: clay specks, then mica you can see.
        var growth = VolcanoModel.SmoothStep(0.2, 0.8, grade);
        var size = 0.6 + (Hash(index, 6) * 0.8);
        var length = (kind == FlakeKind.Mica ? 0.012 + (0.05 * growth) : 0.008 + (0.018 * growth)) * size;
        var width = kind == FlakeKind.Mica ? 0.0035 + (0.004 * growth) : length * 0.75;

        // Dark minerals and mica gather on the dark bands, the light ones between them.
        var dark = kind != FlakeKind.Light;
        var band = Math.Round((y0 / BandSpacing) - (dark ? 0 : 0.5)) + (dark ? 0 : 0.5);
        var target = (band * BandSpacing) + ((Hash(index, 7) - 0.5) * BandSpacing * (dark ? 0.32 : 0.5));
        var y = y0 + ((target - y0) * Banding(grade));

        // Squeezed from above, the picture is a little flatter.
        var squash = 1 - (0.06 * grade);
        y = 0.5 + ((y - 0.5) * squash);
        return new Flake(x, y, angle, length, width, kind);
    }

    /// <summary>The garnets: centre and radius (0 until they start to grow).</summary>
    public static IEnumerable<(double X, double Y, double Radius)> Garnets(double grade)
    {
        var growth = GarnetGrowth(grade);
        if (growth <= 0) yield break;
        for (var i = 0; i < 11; i++)
        {
            var own = Math.Clamp((growth * 1.4) - (Hash(i, 21) * 0.4), 0, 1);
            if (own <= 0) continue;
            yield return (0.05 + (Hash(i, 22) * 0.9), 0.08 + (Hash(i, 23) * 0.84), 0.012 + (0.03 * own * (0.6 + (Hash(i, 24) * 0.6))));
        }
    }

    /// <summary>The pockets of melt in migmatite: centre and radius.</summary>
    public static IEnumerable<(double X, double Y, double Radius)> MeltPockets(double grade)
    {
        var melt = Melt(grade);
        if (melt <= 0) yield break;
        for (var i = 0; i < 9; i++)
        {
            var band = Math.Round(Hash(i, 31) / BandSpacing) - 0.5;
            yield return (Hash(i, 32), (band + 1) * BandSpacing, (0.015 + (Hash(i, 33) * 0.035)) * melt);
        }
    }

    /// <summary>How many grains sandstone or limestone has at <paramref name="grade"/>: they grow, so there are fewer.</summary>
    public static int GrainCount(double grade) =>
        (int)Math.Round(MostGrains - ((MostGrains - FewestGrains) * VolcanoModel.SmoothStep(0.05, 0.85, grade)));

    /// <summary>Grain <paramref name="index"/>'s seed point (the first <see cref="GrainCount"/> are used).</summary>
    public static (double X, double Y) GrainSeed(int index) => (Hash(index, 41), Hash(index, 42));

    /// <summary>The share of sandstone that is open pore space between grains: about a fifth, gone by quartzite.</summary>
    public static double PoreShare(double grade) => 0.2 * (1 - VolcanoModel.SmoothStep(0.0, 0.45, grade));

    /// <summary>How visible the shell bits in limestone still are (they vanish as calcite recrystallises into marble).</summary>
    public static double Fossils(double grade) => 1 - VolcanoModel.SmoothStep(0.05, 0.4, grade);

    /// <summary>A number in [0, 1) from a flake's number and a channel; the same every time.</summary>
    internal static double Hash(int index, int channel)
    {
        unchecked
        {
            var h = (uint)((index * 73856093) ^ (channel * 19349663) ^ 0x5bd1e995);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            h *= 0x27d4eb2d;
            h ^= h >> 16;
            return h / 4294967296.0;
        }
    }
}
