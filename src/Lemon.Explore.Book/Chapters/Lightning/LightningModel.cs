namespace Lemon.Explore.Book.Chapters.Lightning;

/// <summary>The views of the lightning and static page.</summary>
public enum LightningMode
{
    /// <summary>A rod rubbed with a cloth: electrons move, the rod lifts paper, bends water and sparks to metal.</summary>
    Static,

    /// <summary>A thundercloud in slice: ice and graupel collide in the updraft and the cloud's charges pull apart.</summary>
    Cloud,

    /// <summary>A strike in slow motion: stepped leader, streamers, return strokes, and the thunder on its way to you.</summary>
    Strike,
}

/// <summary>Things that are rubbed together on the page, from the triboelectric series.</summary>
public enum Material
{
    Hair,
    Fur,
    Glass,
    Wool,
    Silk,
    Cotton,
    Rubber,
    Balloon,
    Pvc,
}

/// <summary>What the rubbed rod is doing, for the page's card.</summary>
public enum StaticState
{
    /// <summary>Neutral, nothing going on.</summary>
    Neutral,

    /// <summary>Being rubbed on the cloth: electrons are moving.</summary>
    Rubbing,

    /// <summary>Charged, held in the air.</summary>
    Charged,

    /// <summary>Near the paper bits, lifting some.</summary>
    Lifting,

    /// <summary>Near the stream of water, bending it.</summary>
    Bending,

    /// <summary>Just sparked to the metal ball.</summary>
    Spark,

    /// <summary>Just touched the metal ball, too weakly charged to spark.</summary>
    Touched,
}

/// <summary>The part of its round the demonstrating hand keeps repeating.</summary>
public enum HandScene
{
    /// <summary>The whole round: rub, paper, water, ball.</summary>
    Round,

    /// <summary>Rubbing on the cloth.</summary>
    Rub,

    /// <summary>Holding it over the bits of paper.</summary>
    Paper,

    /// <summary>Holding it beside the stream of water.</summary>
    Stream,

    /// <summary>Bringing it up to the metal ball until it sparks.</summary>
    Ball,
}

/// <summary>The kind of the latest flash in the thundercloud.</summary>
public enum FlashKind
{
    None,

    /// <summary>Inside the cloud, between its negative middle and positive top.</summary>
    Intracloud,

    /// <summary>From the cloud's negative middle down to the ground.</summary>
    CloudToGround,
}

/// <summary>What the lightning hit.</summary>
public enum StrikeTarget
{
    Ground,
    Tree,
    House,
    Rod,
    Person,
}

/// <summary>
/// Charging by rubbing: where materials sit in the triboelectric series, how much charge a rod picks up, how fast damp
/// air lets it leak away, how big a spark the voltage makes (Paschen's law for air), and what the charge pulls on.
/// </summary>
public static class StaticModel
{
    /// <summary>The charge of one electron, C.</summary>
    public const double ElectronCharge = 1.602176634e-19;

    /// <summary>A rod held in the air holds about this much charge per volt, F (a 12 cm ball is about 13 pF).</summary>
    public const double RodCapacitance = 12e-12;

    /// <summary>
    /// The most a rubbed rod holds, C: past this the field at its surface breaks the air down and the extra leaks off
    /// as corona (ε₀ × 3 MV/m over ~100 cm² is about 0.27 µC).
    /// </summary>
    public const double MaxCharge = 250e-9;

    /// <summary>Metres of rubbing that take the charge about 63% of the way to its most.</summary>
    public const double RubLength = 0.6;

    /// <summary>A rod discharging into metal keeps this share of its charge: it is an insulator, so only the charge near the tip can get out.</summary>
    public const double KeptAfterSpark = 0.4;

    /// <summary>The smallest voltage anything sparks across in air, V (the bottom of the Paschen curve).</summary>
    public static double MinimumSparkVoltage => BreakdownVoltage(MinimumGap);

    /// <summary>
    /// How keen a material is to take electrons when rubbed: lower takes electrons (ends up negative), higher gives them
    /// (ends up positive). The order is the usual triboelectric series; the numbers are only illustrative.
    /// </summary>
    public static double Affinity(Material material) => material switch
    {
        Material.Hair => 45,
        Material.Fur => 40,
        Material.Glass => 25,
        Material.Wool => 15,
        Material.Silk => 10,
        Material.Cotton => 5,
        Material.Rubber => -50,
        Material.Balloon => -70,
        Material.Pvc => -100,
        _ => 0,
    };

    /// <summary>The series from the most positive to the most negative.</summary>
    public static IReadOnlyList<Material> Series { get; } = Enum.GetValues<Material>().OrderByDescending(Affinity).ToArray();

    /// <summary>The charge the rod ends up with after long rubbing, C (negative when it takes electrons).</summary>
    public static double SaturationCharge(Material rod, Material cloth)
    {
        var gap = Affinity(cloth) - Affinity(rod);
        if (gap == 0) return 0;
        return -Math.Sign(gap) * MaxCharge * Math.Sqrt(Math.Min(1, Math.Abs(gap) / 120));
    }

    /// <summary>The rod's charge after rubbing it <paramref name="distance"/> metres more.</summary>
    public static double Rub(double charge, double distance, Material rod, Material cloth)
    {
        var target = SaturationCharge(rod, cloth);
        return target + ((charge - target) * Math.Exp(-Math.Max(0, distance) / RubLength));
    }

    /// <summary>
    /// How long charge on an insulator lasts in air of a relative humidity, s (the e-folding time): minutes when the air is
    /// dry, seconds when it is damp, because a film of water on the surface conducts.
    /// </summary>
    public static double LeakTime(double humidity) => 600 * Math.Pow(10, -(Math.Clamp(humidity, 0, 100) - 20) / 20);

    /// <summary>The time for half the charge to leak away, s.</summary>
    public static double HalfLife(double humidity) => LeakTime(humidity) * Math.Log(2);

    /// <summary>The charge left after <paramref name="seconds"/>.</summary>
    public static double Leak(double charge, double seconds, double humidity) => charge * Math.Exp(-Math.Max(0, seconds) / LeakTime(humidity));

    /// <summary>The rod's voltage, V.</summary>
    public static double Voltage(double charge) => charge / RodCapacitance;

    /// <summary>The number of electrons moved, for a charge.</summary>
    public static double Electrons(double charge) => Math.Abs(charge) / ElectronCharge;

    // Paschen's law for air at sea level: V = B·pd / (ln(A·pd) − ln ln(1 + 1/γ)), pd in Torr·cm.
    private const double PaschenA = 15;
    private const double PaschenB = 365;
    private const double Secondary = 0.01;
    private const double Torr = 760;
    private static readonly double PaschenC = Math.Log(Math.Log(1 + (1 / Secondary)));

    /// <summary>The gap, m, at the bottom of the Paschen curve: no smaller voltage sparks across any gap.</summary>
    public static double MinimumGap => Math.Exp(1 + PaschenC) / PaschenA / Torr / 100;

    /// <summary>The voltage that sparks across an air gap, V (Paschen's law; ~3 kV a millimetre for gaps of centimetres).</summary>
    public static double BreakdownVoltage(double gap)
    {
        var pd = Torr * Math.Max(1e-7, gap) * 100;
        var denominator = Math.Log(PaschenA * pd) - PaschenC;
        return denominator <= 0.05 ? double.PositiveInfinity : PaschenB * pd / denominator;
    }

    /// <summary>The widest gap a voltage can spark across, m (0 below the Paschen minimum).</summary>
    public static double SparkGap(double voltage)
    {
        voltage = Math.Abs(voltage);
        var low = MinimumGap;
        if (voltage <= BreakdownVoltage(low)) return 0;
        var high = 1.0;
        for (var i = 0; i < 80; i++)
        {
            var mid = Math.Sqrt(low * high);
            if (BreakdownVoltage(mid) < voltage) low = mid;
            else high = mid;
        }

        return low;
    }

    /// <summary>
    /// How far from a bit of paper the rod lifts it, m. The pull on something polarised goes as q²/r⁵, so the reach goes
    /// as q^0.4; calibrated so that 100 nC lifts bits from 4 cm.
    /// </summary>
    public static double LiftReach(double charge) => 0.04 * Math.Pow(Math.Abs(charge) / 100e-9, 0.4);

    /// <summary>The pull on a thin stream of water toward the rod, m/s² (as q²/r⁵, capped close in).</summary>
    public static double StreamPull(double charge, double distance)
    {
        var r = Math.Max(0.012, distance);
        return Math.Min(80, 4.4e7 * charge * charge / Math.Pow(r, 5));
    }

    /// <summary>
    /// A thin stream of water falling from a tap at <paramref name="tapX"/>, <paramref name="tapY"/> (m, y up) to
    /// <paramref name="floorY"/>, pulled sideways toward a charged rod's tip; the points are a few millimetres apart.
    /// </summary>
    public static List<(double X, double Y)> Stream(double tapX, double tapY, double floorY, double charge, double tipX, double tipY)
    {
        var points = new List<(double X, double Y)>();
        double x = tapX, y = tapY, vx = 0, vy = -0.3;
        const double dt = 0.002;
        points.Add((x, y));
        for (var i = 0; i < 2000 && y > floorY; i++)
        {
            var dx = tipX - x;
            var dy = tipY - y;
            var r = Math.Sqrt((dx * dx) + (dy * dy));
            if (charge != 0 && r > 1e-6)
            {
                var pull = StreamPull(charge, r);
                vx += pull * dx / r * dt;
                vy += pull * dy / r * dt * 0.3;
            }

            vy -= 9.81 * dt;
            x += vx * dt;
            y += vy * dt;
            points.Add((x, Math.Max(floorY, y)));
        }

        return points;
    }
}

/// <summary>
/// A thundercloud as a slice: temperatures by height, where the charges sit (a tripole: positive on top, negative in the
/// middle at about −15 °C, a little positive below), the field they make at the ground, how often it flashes (Price and
/// Rind 1992) and how many flashes stay in the cloud (Prentice and Mackerras 1977).
/// </summary>
public static class CloudModel
{
    /// <summary>The air at the ground, °C.</summary>
    public const double SurfaceTemperature = 30;

    /// <summary>The cooling with height inside the storm, °C per km.</summary>
    public const double LapseRate = 6.5;

    /// <summary>
    /// Graupel hit by ice crystals takes negative charge where it is colder than this, positive where it is warmer
    /// (Takahashi 1978; the reversal is somewhere about −10 to −20 °C).
    /// </summary>
    public const double ReversalTemperature = -15;

    /// <summary>The fair-weather field at the ground, kV/m (pointing down: the sky is positive).</summary>
    public const double FairWeatherField = -0.13;

    /// <summary>The main negative charge when the cloud lets go, C.</summary>
    public const double TriggerCharge = 50;

    /// <summary>A flash carries away this share of the charge it taps.</summary>
    public const double FlashShare = 0.4;

    /// <summary>The cloud's base, km.</summary>
    public const double CloudBase = 1.5;

    private const double CoulombK = 8.9875517923e9;

    public static double Temperature(double km) => SurfaceTemperature - (LapseRate * km);

    public static double HeightOf(double celsius) => (SurfaceTemperature - celsius) / LapseRate;

    /// <summary>Flashes a minute for a storm whose top is at <paramref name="topKm"/> (Price and Rind 1992, over land: 3.44 × 10⁻⁵ H^4.9).</summary>
    public static double FlashRate(double topKm) => 3.44e-5 * Math.Pow(Math.Max(0, topKm), 4.9);

    /// <summary>Flashes in the cloud for each one to the ground at a latitude (Prentice and Mackerras 1977: 4.16 + 2.16 cos 3λ, up to 60°).</summary>
    public static double IntracloudRatio(double latitude)
    {
        var lat = Math.Abs(latitude);
        return lat >= 60 ? 2 : 4.16 + (2.16 * Math.Cos(3 * lat * Math.PI / 180));
    }

    /// <summary>The share of flashes that reach the ground at a latitude.</summary>
    public static double GroundShare(double latitude) => 1 / (1 + IntracloudRatio(latitude));

    /// <summary>A charge centre: its height, km, its charge at full strength, C, and its horizontal place, km.</summary>
    public readonly record struct ChargeCentre(double X, double Height, double Charge);

    /// <summary>
    /// The storm's charges at full strength for a top at <paramref name="topKm"/>: the main negative at the reversal
    /// temperature, the positive between it and the top, a small positive near the 0 °C level, all leaning downwind.
    /// </summary>
    public static ChargeCentre[] Charges(double topKm)
    {
        var main = HeightOf(ReversalTemperature);
        var upper = (main + topKm) / 2 + 0.5;
        var lower = HeightOf(0);
        return
        [
            new ChargeCentre(1.5, upper, 0.8 * TriggerCharge),
            new ChargeCentre(0.5, main, -TriggerCharge),
            new ChargeCentre(-0.5, lower, 0.06 * TriggerCharge),
        ];
    }

    /// <summary>
    /// The vertical field at the ground <paramref name="x"/> km from the storm, kV/m, positive pointing up, for charges at
    /// <paramref name="upper"/>, <paramref name="main"/> and <paramref name="lower"/> times their full strength (each
    /// charge and its image in the conducting ground).
    /// </summary>
    public static double GroundField(double x, double topKm, double upper, double main, double lower)
    {
        var centres = Charges(topKm);
        double[] scale = [upper, main, lower];
        var field = FairWeatherField;
        for (var i = 0; i < centres.Length; i++)
        {
            var c = centres[i];
            var h = c.Height * 1000;
            var d = (x - c.X) * 1000;
            var r3 = Math.Pow((h * h) + (d * d), 1.5);
            field -= 2 * CoulombK * c.Charge * scale[i] * h / r3 / 1000;
        }

        return field;
    }

    /// <summary>The charging current the storm needs to keep up its flash rate, in units of full strength per second.</summary>
    public static double ChargingRate(double topKm) => FlashShare * FlashRate(topKm) / 60;
}

/// <summary>
/// A lightning strike: how close the leader comes before something reaches up to it (striking distance, the rolling
/// sphere), the current of a return stroke (Heidler's function with the IEC 62305 shapes), the stepped leader as a
/// branching path, what it hits, and how long the flash and its thunder take.
/// </summary>
public static class StrikeModel
{
    /// <summary>The speed of sound at 20 °C, m/s.</summary>
    public const double SoundSpeed = 343;

    /// <summary>Where the leader starts, m (the cloud's base).</summary>
    public const double CloudBase = 2000;

    /// <summary>Above this height the leader steps 50 m at a time; below it the picture uses shorter steps.</summary>
    public const double NearGround = 150;

    /// <summary>The median peak current of a first stroke, kA (Berger et al. 1975).</summary>
    public const double MedianCurrent = 30;

    /// <summary>
    /// How close the leader's tip gets before an upward streamer from something reaches it, m: 10 I^0.65 (I in kA). The
    /// rolling sphere of lightning protection has this radius (IEC 62305: 20, 30, 45, 60 m for 3, 5, 10, 16 kA).
    /// </summary>
    public static double StrikingDistance(double kiloAmps) => 10 * Math.Pow(Math.Max(0.1, kiloAmps), 0.65);

    /// <summary>
    /// The current of a return stroke, kA, at <paramref name="microseconds"/> (Heidler 1985, IEC 62305-1 shapes):
    /// a first stroke rises in about 1 µs and halves in about 200 µs, a later stroke is faster and smaller.
    /// </summary>
    public static double Current(double microseconds, double peak, bool first)
    {
        if (microseconds <= 0) return 0;
        var (eta, tau1, tau2) = first ? (0.986, 1.82, 285.0) : (0.993, 0.454, 143.0);
        var x = Math.Pow(microseconds / tau1, 10);
        return peak / eta * x / (1 + x) * Math.Exp(-microseconds / tau2);
    }

    /// <summary>A later stroke's peak current: about 40% of the first (median 12 kA against 30 kA).</summary>
    public static double LaterPeak(double firstPeak) => 0.4 * firstPeak;

    /// <summary>The seconds between the flash and its thunder at a distance, km.</summary>
    public static double ThunderDelay(double km) => km * 1000 / SoundSpeed;

    /// <summary>Something standing on the ground that the lightning could hit: its middle, top, width, m.</summary>
    public readonly record struct Thing(StrikeTarget Kind, double X, double Top, double Width);

    /// <summary>The things in the picture: a tree, a house (with its rod if fitted) and a person out in the open.</summary>
    public static Thing[] Things(bool rod) => rod
        ? [new(StrikeTarget.Tree, -70, 16, 12), new(StrikeTarget.House, 15, 9, 16), new(StrikeTarget.Rod, 15, 14, 0.3), new(StrikeTarget.Person, 78, 1.7, 0.6)]
        : [new(StrikeTarget.Tree, -70, 16, 12), new(StrikeTarget.House, 15, 9, 16), new(StrikeTarget.Person, 78, 1.7, 0.6)];

    /// <summary>A point of the leader, m, and the step at which it is reached.</summary>
    public readonly record struct Node(double X, double H, int Step);

    /// <summary>A side branch of the leader: its nodes, the first being on the channel it grows from.</summary>
    public sealed record Branch(IReadOnlyList<Node> Nodes);

    /// <summary>
    /// A strike worked out: the main channel down to the junction, the branches, what was hit, where the upward streamer
    /// started and the step it all connected at.
    /// </summary>
    public sealed record Strike(IReadOnlyList<Node> Main, IReadOnlyList<Branch> Branches, StrikeTarget Target, double TargetX, double TargetTop, int Steps);

    /// <summary>
    /// Grows a stepped leader from the cloud base with seeded randomness, and stops it when its tip comes within striking
    /// distance of something (or the ground): the same seed always gives the same path.
    /// </summary>
    public static Strike Leader(int seed, double kiloAmps, bool rod)
    {
        var random = new Random(seed);
        var things = Things(rod);
        var reach = StrikingDistance(kiloAmps);
        var aim = (random.NextDouble() * 200) - 100;
        var main = new List<Node> { new((random.NextDouble() * 120) - 60, CloudBase, 0) };
        var branches = new List<Branch>();
        var step = 0;
        while (true)
        {
            var tip = main[^1];
            if (Hit(tip, things, reach) is { } hit)
            {
                // The upward streamer meets the leader a little above the thing it rises from.
                var junction = new Node(Mix(hit.X, tip.X, 0.65), Mix(hit.Top, tip.H, 0.65), step + 1);
                return new Strike([.. main, junction], branches, hit.Kind, hit.X, hit.Top, step + 1);
            }

            step++;
            var length = tip.H > NearGround ? 50 : 16;
            var angle = Gaussian(random) * 0.45;
            var pull = Math.Clamp((aim - tip.X) / 400, -0.35, 0.35);
            angle = Math.Clamp(angle + pull, -0.85, 0.85);
            var next = new Node(tip.X + (length * Math.Sin(angle)), Math.Max(0, tip.H - (length * Math.Cos(angle))), step);
            main.Add(next);

            if (tip.H > 60 && random.NextDouble() < (tip.H > NearGround ? 0.32 : 0.22)) branches.AddRange(Grow(random, tip, step, things, reach, 0));
        }
    }

    /// <summary>A side branch and its own little branches: a few steps, wandering further sideways, stopping short of the ground.</summary>
    private static IEnumerable<Branch> Grow(Random random, Node from, int step, Thing[] things, double reach, int depth)
    {
        var nodes = new List<Node> { from };
        var side = random.NextDouble() < 0.5 ? -1 : 1;
        var count = 2 + random.Next(depth == 0 ? 6 : 3);
        var found = new List<Branch>();
        var at = from;
        for (var i = 1; i <= count; i++)
        {
            var length = (at.H > NearGround ? 42 : 14) * (depth == 0 ? 1 : 0.7);
            var angle = Math.Clamp((side * (0.5 + (0.4 * random.NextDouble()))) + (Gaussian(random) * 0.35), -1.25, 1.25);
            var next = new Node(at.X + (length * Math.Sin(angle)), at.H - (length * Math.Cos(angle)), step + i);
            if (next.H < 25 || Hit(next, things, reach * 0.8) is not null) break;
            nodes.Add(next);
            if (depth == 0 && random.NextDouble() < 0.18) found.AddRange(Grow(random, next, step + i, things, reach, 1));
            at = next;
        }

        if (nodes.Count > 1) found.Insert(0, new Branch(nodes));
        return found;
    }

    /// <summary>The nearest thing (or the ground right below) within striking distance of the tip, if any.</summary>
    private static Thing? Hit(Node tip, Thing[] things, double reach)
    {
        Thing? best = null;
        var bestDistance = double.MaxValue;
        foreach (var thing in things)
        {
            // A thing is reached at its nearest point: the top of a pole, anywhere along a roof or a crown.
            var x = Math.Clamp(tip.X, thing.X - (thing.Width / 2), thing.X + (thing.Width / 2));
            var distance = Math.Sqrt(Math.Pow(tip.X - x, 2) + Math.Pow(tip.H - thing.Top, 2));
            if (distance <= reach && distance < bestDistance)
            {
                best = thing with { X = x };
                bestDistance = distance;
            }
        }

        if (best is null && tip.H <= reach) best = new Thing(StrikeTarget.Ground, tip.X, 0, 0);
        return best;
    }

    /// <summary>
    /// The heights under which a sphere of radius <paramref name="reach"/> rolled over the ground and the things cannot
    /// reach, m, at each x in <paramref name="xs"/>: the protected zone of lightning protection (0 where unprotected).
    /// </summary>
    public static double[] Protected(IReadOnlyList<double> xs, Thing[] things, double reach)
    {
        // The sphere's centre rides at reach above the ground, or on circles round each thing's top corners.
        double Centre(double x)
        {
            var height = reach;
            foreach (var thing in things)
            {
                var near = Math.Clamp(x, thing.X - (thing.Width / 2), thing.X + (thing.Width / 2));
                var dx = Math.Abs(x - near);
                if (dx < reach) height = Math.Max(height, thing.Top + Math.Sqrt((reach * reach) - (dx * dx)));
            }

            return height;
        }

        var result = new double[xs.Count];
        var lo = xs.Min() - reach;
        var hi = xs.Max() + reach;
        var centres = new List<(double X, double H)>();
        for (var x = lo; x <= hi; x += reach / 60) centres.Add((x, Centre(x)));
        for (var i = 0; i < xs.Count; i++)
        {
            var x = xs[i];
            var lowest = double.MaxValue;
            foreach (var (cx, ch) in centres)
            {
                var dx = Math.Abs(x - cx);
                if (dx > reach) continue;
                lowest = Math.Min(lowest, ch - Math.Sqrt((reach * reach) - (dx * dx)));
            }

            result[i] = Math.Max(0, lowest);
        }

        return result;
    }

    /// <summary>A standard normal draw.</summary>
    private static double Gaussian(Random random)
    {
        var u = 1 - random.NextDouble();
        var v = random.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v);
    }

    private static double Mix(double a, double b, double t) => a + ((b - a) * t);
}

/// <summary>
/// The strike's timeline. The picture runs in "story" seconds; each part of the flash is slowed by a different amount
/// (the leader takes 20 ms, a return stroke 100 µs), and after the last stroke time runs at its real pace so the thunder
/// comes as late as it would.
/// </summary>
public static class StrikeTimeline
{
    public enum Phase
    {
        Leader,
        Attach,
        Stroke,
        Glow,
        Dart,
        Thunder,
    }

    /// <summary>One part of the flash: what it is, its story seconds, the real seconds it stands for, which stroke.</summary>
    public readonly record struct Part(Phase Phase, double Story, double Real, int Stroke);

    /// <summary>The parts of a flash with <paramref name="strokes"/> return strokes.</summary>
    public static IReadOnlyList<Part> Parts(int strokes)
    {
        var parts = new List<Part>
        {
            new(Phase.Leader, 6, 0.020, 0),
            new(Phase.Attach, 0.8, 0.0001, 0),
            new(Phase.Stroke, 1.4, 0.0001, 0),
            new(Phase.Glow, 1.2, 0.04, 0),
        };
        for (var k = 1; k < Math.Max(1, strokes); k++)
        {
            parts.Add(new(Phase.Dart, 0.8, 0.002, k));
            parts.Add(new(Phase.Stroke, 1.0, 0.00006, k));
            parts.Add(new(Phase.Glow, 1.0, 0.05, k));
        }

        return parts;
    }

    /// <summary>The story seconds of the flash itself, before the thunder's wait.</summary>
    public static double FlashStory(int strokes) => Parts(strokes).Sum(p => p.Story);

    /// <summary>The real seconds the flash takes.</summary>
    public static double FlashReal(int strokes) => Parts(strokes).Sum(p => p.Real);

    /// <summary>The real time of the first return stroke, s from the leader's start.</summary>
    public static double FirstStrokeReal => 0.020 + 0.0001;

    /// <summary>The whole story: the flash, then real time until the thunder has rolled past (its rumble lasts a few seconds).</summary>
    public static double Length(int strokes, double km) =>
        FlashStory(strokes) + Math.Max(0, FirstStrokeReal + StrikeModel.ThunderDelay(km) + Rumble(km) - FlashReal(strokes)) + 1.5;

    /// <summary>How long the thunder rumbles, s: sound from the far end of a ~2 km channel arrives later than from the near end.</summary>
    public static double Rumble(double km) => (Math.Sqrt((km * km) + 4) - km) * 1000 / StrikeModel.SoundSpeed + 1.5;

    /// <summary>Where a story time falls: the part, how far through it (0…1) and the real seconds since the leader started.</summary>
    public static (Part Part, double Fraction, double Real) At(double story, int strokes)
    {
        var parts = Parts(strokes);
        var real = 0.0;
        foreach (var part in parts)
        {
            if (story < part.Story)
            {
                var f = Math.Max(0, story / part.Story);
                return (part, f, real + (f * part.Real));
            }

            story -= part.Story;
            real += part.Real;
        }

        return (new Part(Phase.Thunder, double.PositiveInfinity, double.PositiveInfinity, parts[^1].Stroke), 0, real + story);
    }
}
