namespace Lemon.Explore.Book.Chapters.Earthquakes;

/// <summary>The cross-section shown in the boundaries view.</summary>
public enum SectionKind
{
    /// <summary>A mid-ocean ridge: plates move apart, magma fills the gap, the new floor records the magnetic field.</summary>
    Ridge,

    /// <summary>An ocean plate sinking under another plate: trench, quakes down the slab, a line of volcanoes.</summary>
    Subduction,

    /// <summary>India running into Asia: the ocean between closes, then the Himalaya and Tibet rise.</summary>
    Collision,

    /// <summary>Two plates sliding past each other, seen from above: the ground bends, then snaps back in a quake.</summary>
    Transform,
}

/// <summary>
/// The physics behind the boundary cross-sections: the magnetic field's flips recorded in new sea floor, how the floor
/// sinks as it ages, the shape of a sinking plate and where its volcanoes come up, India's journey north, and the
/// stick-and-snap cycle of a fault (Reid's elastic rebound).
/// </summary>
public static class BoundaryModel
{
    #region the magnetic record

    /// <summary>
    /// Times (millions of years ago) when the field was the way round it is now, back to 5.9 million years (the
    /// Brunhes, Jaramillo, Olduvai, Gauss and Gilbert-age normal chrons, rounded). Older flips are made up with similar
    /// spacing, so stripes keep appearing; they are not the real record.
    /// </summary>
    private static readonly (double From, double To)[] RealNormal =
    [
        (0, 0.78),
        (0.99, 1.07),
        (1.78, 1.95),
        (2.58, 3.04),
        (3.11, 3.22),
        (3.33, 3.58),
        (4.19, 4.30),
        (4.49, 4.63),
        (4.80, 4.90),
        (5.00, 5.24),
    ];

    private const double RealRecordEnd = 5.9;

    private static readonly Lazy<(double From, double To)[]> Normal = new(() =>
    {
        var list = RealNormal.ToList();
        var random = new Random(7);
        var t = RealRecordEnd;
        while (t < 60)
        {
            var reversed = 0.15 + (random.NextDouble() * 0.5);
            var normal = 0.1 + (random.NextDouble() * 0.45);
            list.Add((t + reversed, t + reversed + normal));
            t += reversed + normal;
        }

        return list.ToArray();
    });

    /// <summary>The field had today's direction <paramref name="ageMa"/> million years ago.</summary>
    public static bool IsNormal(double ageMa)
    {
        if (ageMa < 0) return true;
        foreach (var (from, to) in Normal.Value)
        {
            if (ageMa >= from && ageMa < to) return true;
            if (from > ageMa) break;
        }

        return false;
    }

    /// <summary>The age (Ma) the real record is known to here; beyond, the flips are invented.</summary>
    public static double KnownRecord => RealRecordEnd;

    /// <summary>
    /// What a ship's magnetometer reads across a ridge: +1 over floor magnetised today's way, −1 over reversed floor,
    /// smoothed over a few kilometres as the field from the rock spreads out by the time it reaches the sea surface.
    /// </summary>
    /// <param name="distanceKm">Distance from the ridge, either side.</param>
    /// <param name="halfRate">How fast each side moves away, cm a year.</param>
    /// <param name="ageAtRidge">How long ago "now" is, Ma (0 for today); the floor is that much younger.</param>
    public static double Anomaly(double distanceKm, double halfRate, double ageAtRidge = 0)
    {
        const double smooth = 4;
        var total = 0.0;
        var weight = 0.0;
        for (var k = -6; k <= 6; k++)
        {
            var x = Math.Abs(distanceKm + (k * smooth / 3));
            var w = Math.Exp(-0.5 * Math.Pow(k / 3.0, 2));
            total += w * (IsNormal(ageAtRidge + Age(x, halfRate)) ? 1 : -1);
            weight += w;
        }

        return total / weight;
    }

    #endregion

    #region the sea floor

    /// <summary>How old the floor is at a distance from the ridge, Ma: 1 cm a year is 10 km per million years.</summary>
    public static double Age(double distanceKm, double halfRate) => Math.Abs(distanceKm) / (halfRate * 10);

    /// <summary>
    /// How deep the sea is over floor of an age, km (Parsons and Sclater: 2.5 km at the ridge, sinking with the square
    /// root of age as the plate cools and shrinks; 4.6 km at 36 Ma).
    /// </summary>
    public static double SeaDepth(double ageMa) => 2.5 + (0.35 * Math.Sqrt(Math.Max(0, Math.Min(ageMa, 80))));

    /// <summary>How thick the cold, rigid plate is at an age, km: it thickens as it cools (about 11√age, up to 100).</summary>
    public static double LithosphereThickness(double ageMa) => Math.Min(100, 6 + (11 * Math.Sqrt(Math.Max(0, ageMa))));

    /// <summary>Ridges spreading slower than about 3 cm a year in all (each side 1.5) have a rift valley along the top; fast ones a smooth rise.</summary>
    public static bool HasRiftValley(double halfRate) => halfRate < 2.5;

    #endregion

    #region a sinking plate

    /// <summary>How sharply the plate bends down at the trench, km (radius of the curve).</summary>
    public const double BendRadius = 220;

    /// <summary>
    /// The top of the sinking plate <paramref name="s"/> km along it from the trench: across (km from the trench towards
    /// the upper plate) and down (km). It curves down to <paramref name="dip"/>° and then goes straight.
    /// </summary>
    public static (double X, double Depth) SlabTop(double s, double dip)
    {
        var a = dip * Math.PI / 180;
        var arc = BendRadius * a;
        if (s <= arc)
        {
            var phi = s / BendRadius;
            return (BendRadius * Math.Sin(phi), BendRadius * (1 - Math.Cos(phi)));
        }

        var x0 = BendRadius * Math.Sin(a);
        var z0 = BendRadius * (1 - Math.Cos(a));
        return (x0 + ((s - arc) * Math.Cos(a)), z0 + ((s - arc) * Math.Sin(a)));
    }

    /// <summary>How far from the trench the top of the sinking plate is at a depth, km.</summary>
    public static double SlabX(double depth, double dip)
    {
        double lo = 0;
        double hi = 3000;
        for (var i = 0; i < 60; i++)
        {
            var mid = (lo + hi) / 2;
            if (SlabTop(mid, dip).Depth < depth) lo = mid;
            else hi = mid;
        }

        return SlabTop(lo, dip).X;
    }

    /// <summary>
    /// Where the volcanoes come up, km from the trench: above where the sinking plate is about 100 km down, hot enough
    /// to give off its water, which melts the mantle above it.
    /// </summary>
    public static double ArcDistance(double dip) => SlabX(100, dip);

    /// <summary>
    /// Earthquakes in and on the sinking plate (the Wadati–Benioff zone): along the slab and up to 25 km into it, down
    /// to <paramref name="maxDepth"/>, fewer the deeper. As (km along the slab, km into it, magnitude).
    /// </summary>
    public static IReadOnlyList<(double S, double Into, double Magnitude)> SlabQuakes(double maxDepth, double dip, int count = 140, int seed = 3)
    {
        var random = new Random(seed);
        var list = new List<(double, double, double)>();
        while (list.Count < count)
        {
            var depth = random.NextDouble() < 0.6 ? 5 + (random.NextDouble() * 65) : 60 + (Math.Pow(random.NextDouble(), 1.4) * (maxDepth - 60));
            if (depth > maxDepth) continue;
            double lo = 0;
            double hi = 3000;
            for (var i = 0; i < 50; i++)
            {
                var mid = (lo + hi) / 2;
                if (SlabTop(mid, dip).Depth < depth) lo = mid;
                else hi = mid;
            }

            var into = depth < 60 ? random.NextDouble() * 12 : random.NextDouble() * 25;
            list.Add((lo, into, Math.Min(8.5, 4.5 - Math.Log10(1 - random.NextDouble()))));
        }

        return list;
    }

    /// <summary>
    /// The upper plate's edge in a megathrust cycle: dragged down as the plates stay stuck (0 → 1 over the cycle), sprung
    /// back up in the quake. Returns how far down the edge is, as a share of the most it gets.
    /// </summary>
    public static double EdgeDrag(double cycle) => Math.Clamp(cycle, 0, 1);

    #endregion

    #region India and Asia

    /// <summary>When India's edge met Asia, Ma.</summary>
    public const double ContactAge = 50;

    /// <summary>
    /// How fast India moved north, cm a year, by age (Ma), rounded from the sea-floor record: about 15 before the
    /// collision (up to 18 near 65 Ma), slowing to about 5 after it met Asia, near 4–5 today.
    /// </summary>
    public static double IndiaSpeed(double ageMa) => ageMa switch
    {
        > 70 => 14,
        > 62 => 14 + ((70 - ageMa) / 8 * 4),
        > 52 => 18 - ((62 - ageMa) / 10 * 3),
        > 42 => 15 - ((52 - ageMa) / 10 * 10),
        _ => 5 - ((42 - ageMa) / 42 * 0.5),
    };

    /// <summary>How far India has come north since <paramref name="fromAge"/> to <paramref name="toAge"/> (Ma), km.</summary>
    public static double IndiaTravel(double fromAge, double toAge)
    {
        if (toAge > fromAge) return -IndiaTravel(toAge, fromAge);
        var km = 0.0;
        const double step = 0.25;
        for (var age = fromAge; age > toAge; age -= step)
        {
            var dt = Math.Min(step, age - toAge);
            km += IndiaSpeed(age - (dt / 2)) * 10 * dt;
        }

        return km;
    }

    /// <summary>How wide the ocean between India and Asia was at an age, km (0 after they met).</summary>
    public static double TethysWidth(double ageMa) => ageMa <= ContactAge ? 0 : IndiaTravel(ageMa, ContactAge);

    /// <summary>
    /// How high the mountains stand at an age, km: the Himalaya's peaks and the Tibetan Plateau, rising since the
    /// collision (simplified to a smooth rise; today 8.8 and about 5 km).
    /// </summary>
    public static (double Himalaya, double Tibet) Heights(double ageMa)
    {
        var f = ageMa >= ContactAge ? 0 : Smooth((ContactAge - ageMa) / ContactAge);
        return (0.5 + (8.3 * f), 0.3 + (4.7 * Smooth(Math.Min(1, f * 1.15))));
    }

    /// <summary>How thick the crust under Tibet is at an age, km: 35 before, about 70 now.</summary>
    public static double TibetCrust(double ageMa)
    {
        var f = ageMa >= ContactAge ? 0 : Smooth((ContactAge - ageMa) / ContactAge);
        return 35 + (35 * f);
    }

    private static double Smooth(double x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - (2 * x));
    }

    #endregion

    #region stick and snap

    /// <summary>How deep the fault is stuck, km.</summary>
    public const double LockingDepth = 15;

    /// <summary>
    /// How far the ground at <paramref name="x"/> km from a stuck fault has moved since the last quake, m, while the far
    /// sides move steadily apart at <paramref name="rate"/> cm a year: v·t/π·atan(x/D), smooth across the fault.
    /// </summary>
    public static double Bending(double x, double rate, double years) => rate / 100 * years / Math.PI * Math.Atan(x / LockingDepth);

    /// <summary>How far each side has slipped in one quake, m: everything the cycle stored.</summary>
    public static double SlipPerQuake(double rate, double recurrence) => rate / 100 * recurrence;

    /// <summary>
    /// Moment magnitude of a fault patch: Mw = (log₁₀M₀ − 9.1) / 1.5 with M₀ = rigidity × area × slip (30 GPa,
    /// <paramref name="lengthKm"/> long, <paramref name="depthKm"/> deep).
    /// </summary>
    public static double Magnitude(double slipM, double lengthKm, double depthKm = LockingDepth)
    {
        var moment = 3e10 * lengthKm * 1000 * depthKm * 1000 * Math.Max(1e-6, slipM);
        return (Math.Log10(moment) - 9.1) / 1.5;
    }

    #endregion
}
