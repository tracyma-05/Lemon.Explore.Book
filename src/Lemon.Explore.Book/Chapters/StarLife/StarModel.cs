using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.StarLife;

/// <summary>What the life of a star page shows.</summary>
public enum StarMode
{
    /// <summary>The star itself through its life, drawn big, with the Sun for scale.</summary>
    Life,

    /// <summary>Its track on the Hertzsprung–Russell diagram (temperature across, luminosity up), with real stars.</summary>
    Diagram,

    /// <summary>The four ways a star's life can go, by its mass, and what is left at the end.</summary>
    Fates,
}

/// <summary>Which of the four life stories a star of a given mass follows.</summary>
public enum StarClass
{
    /// <summary>Below half the Sun's mass: burns for trillions of years, then fades into a helium white dwarf.</summary>
    RedDwarf,

    /// <summary>Half to eight times the Sun's mass: red giant, planetary nebula, white dwarf.</summary>
    SunLike,

    /// <summary>Eight to about twenty: red supergiant, supernova, neutron star.</summary>
    Massive,

    /// <summary>Above about twenty: red supergiant, supernova, black hole.</summary>
    VeryMassive,
}

/// <summary>A stage of a star's life.</summary>
public enum StarStage
{
    Protostar,
    MainSequence,

    /// <summary>A red dwarf at the end of its hydrogen: smaller, hotter and bluer (none has got there yet).</summary>
    BlueDwarf,
    Subgiant,
    RedGiant,

    /// <summary>Helium burning in the core after the red giant (the horizontal branch or red clump).</summary>
    HeliumBurning,

    /// <summary>The asymptotic giant branch: a second, bigger red giant, shedding its outer layers.</summary>
    Agb,
    PlanetaryNebula,
    WhiteDwarf,

    /// <summary>A massive star swelling from blue to red across the diagram.</summary>
    Supergiant,
    RedSupergiant,

    /// <summary>Carbon, neon, oxygen and silicon burning in shells like an onion, the core turning to iron.</summary>
    LateBurning,
    Supernova,
    NeutronStar,
    BlackHole,
}

/// <summary>One stage of a star's track: how long it really lasts, how long playback spends on it, and where it goes on the diagram.</summary>
/// <param name="Stage">Which stage.</param>
/// <param name="Years">Real duration; infinity for a remnant that lasts for ever.</param>
/// <param name="Seconds">Playback seconds: every stage gets its turn, however short.</param>
/// <param name="LogL0">log₁₀ luminosity (Sun = 0) at the start.</param>
/// <param name="LogT0">log₁₀ surface temperature (K) at the start.</param>
/// <param name="LogL1">At the end.</param>
/// <param name="LogT1">At the end.</param>
public sealed record StarStageSpan(StarStage Stage, double Years, double Seconds, double LogL0, double LogT0, double LogL1, double LogT1);

/// <summary>The star at a moment of its life.</summary>
/// <param name="Stage">Which stage.</param>
/// <param name="Index">The stage's position in the track.</param>
/// <param name="Progress">How far through the stage, 0…1.</param>
/// <param name="AgeYears">Years since the cloud began to collapse.</param>
/// <param name="Luminosity">Times the Sun's.</param>
/// <param name="Temperature">Surface temperature, K.</param>
/// <param name="Radius">Times the Sun's.</param>
public readonly record struct StarState(StarStage Stage, int Index, double Progress, double AgeYears, double Luminosity, double Temperature, double Radius);

/// <summary>A well-known star for the diagram: surface temperature (K) and luminosity (Sun = 1).</summary>
public readonly record struct KnownStar(string Id, double Temperature, double Luminosity)
{
    public double Radius => StarModel.RadiusOf(Luminosity, Temperature);
}

/// <summary>
/// The life of a star from its mass: a simple model for the page, not a stellar-evolution code. The main sequence comes
/// from a table of zero-age stars (luminosity, temperature, lifetime); the later stages follow the shape of published
/// tracks, scaled by mass. Radius follows from luminosity and temperature (Stefan–Boltzmann), so size, colour and
/// brightness always agree.
/// </summary>
/// <remarks>
/// Masses are initial masses in Suns, 0.1…40; the boundaries between the fates (0.5, 8 and about 20) are for single stars
/// like the Sun and are themselves approximate, the last one especially.
/// </remarks>
public static class StarModel
{
    public const double MinMass = 0.1;
    public const double MaxMass = 40;

    /// <summary>The Sun's effective temperature, K.</summary>
    public const double SunTemperature = 5772;

    public const double SunRadiusKm = 695_700;
    public const double EarthRadiusKm = 6_371;

    /// <summary>The Sun's age today, years.</summary>
    public const double SunAgeYears = 4.6e9;

    /// <summary>The age of the universe, years: no red dwarf has had time to leave the main sequence.</summary>
    public const double UniverseAgeYears = 13.8e9;

    /// <summary>A neutron star's radius, km.</summary>
    public const double NeutronStarRadiusKm = 12;

    /// <summary>The Chandrasekhar limit, Suns: no white dwarf can be heavier.</summary>
    public const double ChandrasekharMass = 1.44;

    /// <summary>
    /// Zero-age main sequence: mass (Suns), main-sequence lifetime (years), luminosity (Suns), temperature (K). Rounded
    /// from standard stellar models (solar composition); the lowest masses from Laughlin et al. 1997.
    /// </summary>
    private static readonly (double Mass, double Years, double Luminosity, double Temperature)[] Zams =
    [
        (0.1, 4e12, 0.0008, 2800),
        (0.2, 1e12, 0.0045, 3200),
        (0.5, 7e10, 0.04, 3800),
        (0.8, 2.5e10, 0.3, 5000),
        (1.0, 1.0e10, 0.7, 5600),
        (1.5, 2.7e9, 4.5, 7000),
        (2.0, 1.2e9, 13, 8900),
        (3.0, 3.5e8, 60, 11500),
        (5.0, 1.0e8, 500, 16000),
        (8.0, 3.5e7, 2300, 21000),
        (12, 1.7e7, 9000, 26000),
        (20, 9e6, 4e4, 33000),
        (40, 5e6, 2e5, 42000),
    ];

    /// <summary>
    /// Stars for the diagram, values rounded from the literature: the Sun, some of the nearest and brightest stars, a
    /// red dwarf and two white dwarfs.
    /// </summary>
    public static IReadOnlyList<KnownStar> KnownStars { get; } =
    [
        new("Sun", SunTemperature, 1),
        new("Proxima", 3040, 0.0017),
        new("Barnard", 3130, 0.0035),
        new("SiriusA", 9940, 25.4),
        new("SiriusB", 25200, 0.056),
        new("ProcyonB", 7740, 0.00049),
        new("Vega", 9600, 40),
        new("Arcturus", 4290, 170),
        new("Aldebaran", 3910, 440),
        new("Polaris", 6020, 1260),
        new("Spica", 25300, 20500),
        new("Rigel", 12100, 120_000),
        new("Deneb", 8500, 196_000),
        new("Betelgeuse", 3600, 100_000),
    ];

    #region main sequence

    public static StarClass ClassOf(double mass) => mass switch
    {
        < 0.5 => StarClass.RedDwarf,
        < 8 => StarClass.SunLike,
        < 20 => StarClass.Massive,
        _ => StarClass.VeryMassive,
    };

    /// <summary>Years on the main sequence, burning hydrogen in the core: 10 billion for the Sun.</summary>
    public static double MainSequenceYears(double mass) => Math.Pow(10, Interpolate(mass, z => Math.Log10(z.Years)));

    /// <summary>Luminosity on arriving on the main sequence, Suns.</summary>
    public static double ZamsLuminosity(double mass) => Math.Pow(10, Interpolate(mass, z => Math.Log10(z.Luminosity)));

    /// <summary>Surface temperature on arriving on the main sequence, K.</summary>
    public static double ZamsTemperature(double mass) => Math.Pow(10, Interpolate(mass, z => Math.Log10(z.Temperature)));

    /// <summary>Radius in Suns from luminosity (Suns) and temperature (K): L = 4πR²σT⁴.</summary>
    public static double RadiusOf(double luminosity, double temperature) =>
        Math.Sqrt(luminosity) * Math.Pow(SunTemperature / temperature, 2);

    /// <summary>Luminosity of a star of <paramref name="radius"/> Suns at <paramref name="temperature"/> K.</summary>
    public static double LuminosityOf(double radius, double temperature) =>
        radius * radius * Math.Pow(temperature / SunTemperature, 4);

    #endregion

    #region remnants

    /// <summary>
    /// The white dwarf a star leaves, Suns: a red dwarf keeps nearly all its mass; Sun-like stars follow the measured
    /// initial–final mass relation (Kalirai et al. 2008), the Sun ending at about 0.5.
    /// </summary>
    public static double WhiteDwarfMass(double mass) => mass < 0.5
        ? mass * 0.95
        : Math.Min(1.35, (0.109 * mass) + 0.394);

    /// <summary>
    /// A white dwarf's radius in km from its mass (Nauenberg 1972, for carbon and oxygen): the heavier, the smaller; none
    /// above 1.44 Suns. Sirius B (1.02 Suns) comes out at about 5,300 km against a measured 5,800.
    /// </summary>
    public static double WhiteDwarfRadiusKm(double mass)
    {
        var x = Math.Clamp(mass / ChandrasekharMass, 0.05, 0.999);
        return 0.0112 * SunRadiusKm * Math.Pow(x, -1.0 / 3) * Math.Sqrt(1 - Math.Pow(x, 4.0 / 3));
    }

    /// <summary>The black hole a very massive star leaves, Suns: roughly a third of the star, the rest blown off on the way.</summary>
    public static double BlackHoleMass(double mass) => Math.Max(5, 0.3 * mass);

    /// <summary>The event horizon's radius, km: 2GM/c², about 3 km per Sun.</summary>
    public static double SchwarzschildRadiusKm(double mass) => 2.953 * mass;

    /// <summary>What is left, Suns: a white dwarf, a neutron star (about 1.4) or a black hole.</summary>
    public static double RemnantMass(double mass) => ClassOf(mass) switch
    {
        StarClass.Massive => 1.4,
        StarClass.VeryMassive => BlackHoleMass(mass),
        _ => WhiteDwarfMass(mass),
    };

    /// <summary>The remnant's radius in km.</summary>
    public static double RemnantRadiusKm(double mass) => ClassOf(mass) switch
    {
        StarClass.Massive => NeutronStarRadiusKm,
        StarClass.VeryMassive => SchwarzschildRadiusKm(BlackHoleMass(mass)),
        _ => WhiteDwarfRadiusKm(WhiteDwarfMass(mass)),
    };

    #endregion

    #region the track

    /// <summary>
    /// The stages of a star of <paramref name="mass"/> Suns, in order, with their real lengths, their playback time and
    /// where each begins and ends on the diagram.
    /// </summary>
    public static IReadOnlyList<StarStageSpan> Track(double mass)
    {
        mass = Math.Clamp(mass, MinMass, MaxMass);
        var ms = MainSequenceYears(mass);
        var lz = Math.Log10(ZamsLuminosity(mass));
        var tz = Math.Log10(ZamsTemperature(mass));
        var track = new List<StarStageSpan>();

        // Every star starts as a cloud shrinking onto the main sequence, cool and bright (the Hayashi track); the
        // heavier, the faster.
        var protostarYears = Math.Clamp(5e7 * Math.Pow(mass, -2.5), 1e5, 1e9);
        var cool = Math.Log10(Math.Min(4000, Math.Pow(10, tz)));
        StarStageSpan Add(StarStage stage, double years, double seconds, double l0, double t0, double l1, double t1)
        {
            var span = new StarStageSpan(stage, years, seconds, l0, t0, l1, t1);
            track.Add(span);
            return span;
        }

        switch (ClassOf(mass))
        {
            case StarClass.RedDwarf:
            {
                Add(StarStage.Protostar, protostarYears, 5, lz + 1, cool, lz, tz);

                // Hydrogen mixed through the whole star, slowly brightening; then smaller and bluer (Laughlin 1997).
                var end = Add(StarStage.MainSequence, ms, 10, lz, tz, lz + 0.6, tz + 0.03);
                var blue = Add(StarStage.BlueDwarf, ms * 0.15, 5, end.LogL1, end.LogT1, lz + 1.2, Math.Log10(8000));
                Add(StarStage.WhiteDwarf, 1e11, 6, blue.LogL1 - 0.3, Math.Log10(9000), -5, Math.Log10(3000));
                break;
            }

            case StarClass.SunLike:
            {
                Add(StarStage.Protostar, protostarYears, 5, lz + 1, cool, lz, tz);
                var tEnd = tz + Math.Log10(mass < 1.5 ? 1.03 : 0.85);
                var ms1 = Add(StarStage.MainSequence, ms, 8, lz, tz, lz + Math.Log10(3), tEnd);

                // Heavier stars ignite helium quietly and loop back towards blue; Sun-like ones with a flash, to the
                // red clump.
                var low = mass < 2;
                var giantL = Math.Log10(low ? 2500 : 100 * Math.Pow(mass, 2.2));
                var giantT = Math.Log10(low ? 3200 : 4000);
                var sub = Add(StarStage.Subgiant, ms * 0.08, 3, ms1.LogL1, ms1.LogT1, ms1.LogL1 + 0.15, Math.Log10(low ? 4800 : 5200));
                var rg = Add(StarStage.RedGiant, ms * (low ? 0.1 : 0.02), 5, sub.LogL1, sub.LogT1, giantL, giantT);
                var he = low
                    ? Add(StarStage.HeliumBurning, ms * 0.012, 3, Math.Log10(50), Math.Log10(4900), Math.Log10(60), Math.Log10(5000))
                    : Add(StarStage.HeliumBurning, ms * 0.1, 3, rg.LogL1 - 0.3, Math.Log10(7000), rg.LogL1 - 0.2, Math.Log10(6000));
                var agbL = Math.Log10(Math.Min(5e4, low ? 4000 : 4000 * Math.Pow(mass, 1.2)));
                var agb = Add(StarStage.Agb, ms * 0.002, 4, he.LogL1, he.LogT1, agbL, Math.Log10(3100));

                // The outer layers drift off; the bare core heats up at the same brightness, then fades as it settles
                // to a white dwarf.
                var pn = Add(StarStage.PlanetaryNebula, 3e4, 5, agb.LogL1, agb.LogT1,
                    Math.Log10(LuminosityOf(WhiteDwarfRadiusKm(WhiteDwarfMass(mass)) / SunRadiusKm, 120_000)), Math.Log10(120_000));
                Add(StarStage.WhiteDwarf, 1e10, 6, pn.LogL1, pn.LogT1, -3.5, Math.Log10(5000));
                break;
            }

            default:
            {
                Add(StarStage.Protostar, 1e5, 4, lz - 0.5, Math.Log10(6000), lz, tz);
                var ms1 = Add(StarStage.MainSequence, ms, 8, lz, tz, lz + Math.Log10(1.8), tz + Math.Log10(0.75));

                // Across the diagram at nearly the same luminosity, swelling to a red supergiant.
                var cross = Add(StarStage.Supergiant, ms * 0.01, 3, ms1.LogL1, ms1.LogT1, ms1.LogL1 + 0.08, Math.Log10(4200));
                var rsg = Add(StarStage.RedSupergiant, ms * 0.1, 6, cross.LogL1, cross.LogT1, cross.LogL1 + 0.15, Math.Log10(3500));
                Add(StarStage.LateBurning, 1000, 4, rsg.LogL1, rsg.LogT1, rsg.LogL1, rsg.LogT1);

                // For a few weeks as bright as a whole galaxy, then gone; the remnant lasts for ever.
                Add(StarStage.Supernova, 0.5, 5, rsg.LogL1, rsg.LogT1, 9.3, Math.Log10(6000));
                Add(ClassOf(mass) == StarClass.Massive ? StarStage.NeutronStar : StarStage.BlackHole,
                    double.PositiveInfinity, 6, double.NaN, double.NaN, double.NaN, double.NaN);
                break;
            }
        }

        return track;
    }

    /// <summary>Total playback seconds of a track.</summary>
    public static double StoryLength(IReadOnlyList<StarStageSpan> track) => track.Sum(s => s.Seconds);

    /// <summary>The star <paramref name="story"/> playback seconds into its track.</summary>
    public static StarState At(IReadOnlyList<StarStageSpan> track, double mass, double story)
    {
        var age = 0.0;
        var start = 0.0;
        for (var i = 0; i < track.Count; i++)
        {
            var span = track[i];
            if (story < start + span.Seconds || i == track.Count - 1)
            {
                var p = Math.Clamp((story - start) / span.Seconds, 0, 1);
                return StateIn(span, i, p, double.IsInfinity(span.Years) ? age : age + (p * span.Years), mass);
            }

            age += span.Years;
            start += span.Seconds;
        }

        return StateIn(track[0], 0, 0, 0, mass);
    }

    /// <summary>The story time at which stage <paramref name="index"/> begins.</summary>
    public static double StoryStart(IReadOnlyList<StarStageSpan> track, int index) => track.Take(index).Sum(s => s.Seconds);

    /// <summary>The story time at which a star is <paramref name="ageYears"/> old (the age falls in a stage of finite length).</summary>
    public static double StoryAtAge(IReadOnlyList<StarStageSpan> track, double ageYears)
    {
        var age = 0.0;
        var story = 0.0;
        foreach (var span in track)
        {
            if (double.IsInfinity(span.Years) || ageYears < age + span.Years)
            {
                var p = double.IsInfinity(span.Years) ? 0 : (ageYears - age) / span.Years;
                return story + (Math.Clamp(p, 0, 1) * span.Seconds);
            }

            age += span.Years;
            story += span.Seconds;
        }

        return story;
    }

    private static StarState StateIn(StarStageSpan span, int index, double p, double age, double mass)
    {
        switch (span.Stage)
        {
            case StarStage.NeutronStar:
                // Born at about a million degrees; tiny, so hardly any light.
                return new StarState(span.Stage, index, p, age, 0.01, 1e6, NeutronStarRadiusKm / SunRadiusKm);

            case StarStage.BlackHole:
                return new StarState(span.Stage, index, p, age, 0, 0, SchwarzschildRadiusKm(BlackHoleMass(mass)) / SunRadiusKm);

            case StarStage.Supernova:
            {
                // Up to its peak in about a tenth of the time, then fading.
                var rise = Math.Clamp(p / 0.1, 0, 1);
                var logL = p < 0.1 ? span.LogL0 + ((span.LogL1 - span.LogL0) * rise) : span.LogL1 - (2.5 * (p - 0.1) / 0.9);
                var temperature = Math.Pow(10, span.LogT0 + ((span.LogT1 - span.LogT0) * Math.Min(1, p * 3)));
                var luminosity = Math.Pow(10, logL);
                return new StarState(span.Stage, index, p, age, luminosity, temperature, RadiusOf(luminosity, temperature));
            }

            case StarStage.PlanetaryNebula:
            {
                // Across the diagram to the left first, then down.
                const double turn = 0.7;
                var logT = span.LogT0 + ((span.LogT1 - span.LogT0) * Smooth(Math.Min(1, p / turn)));
                var logL = p < turn
                    ? span.LogL0 - (0.1 * p / turn)
                    : span.LogL0 - 0.1 + ((span.LogL1 - span.LogL0 + 0.1) * Smooth((p - turn) / (1 - turn)));
                var luminosity = Math.Pow(10, logL);
                var temperature = Math.Pow(10, logT);
                return new StarState(span.Stage, index, p, age, luminosity, temperature, RadiusOf(luminosity, temperature));
            }

            case StarStage.WhiteDwarf:
            {
                // A white dwarf keeps its size and cools: fast at first, then ever more slowly.
                var eased = 1 - Math.Pow(1 - p, 2.2);
                var temperature = Math.Pow(10, span.LogT0 + ((span.LogT1 - span.LogT0) * eased));
                var radius = WhiteDwarfRadiusKm(WhiteDwarfMass(mass)) / SunRadiusKm;
                return new StarState(span.Stage, index, p, age, LuminosityOf(radius, temperature), temperature, radius);
            }

            default:
            {
                var eased = Smooth(p);
                var luminosity = Math.Pow(10, span.LogL0 + ((span.LogL1 - span.LogL0) * eased));
                var temperature = Math.Pow(10, span.LogT0 + ((span.LogT1 - span.LogT0) * eased));
                return new StarState(span.Stage, index, p, age, luminosity, temperature, RadiusOf(luminosity, temperature));
            }
        }
    }

    #endregion

    #region colour

    /// <summary>
    /// The colour of a glowing body at <paramref name="temperature"/> K as the eye sees it (a fit to the black-body
    /// colours by Tanner Helland): red below 3500 K, white near 6500 K, blue above 10,000 K.
    /// </summary>
    public static Color ColorOf(double temperature)
    {
        var t = Math.Clamp(temperature, 1000, 40000) / 100;
        double r, g, b;
        if (t <= 66)
        {
            r = 255;
            g = (99.4708025861 * Math.Log(t)) - 161.1195681661;
            b = t <= 19 ? 0 : (138.5177312231 * Math.Log(t - 10)) - 305.0447927307;
        }
        else
        {
            r = 329.698727446 * Math.Pow(t - 60, -0.1332047592);
            g = 288.1221695283 * Math.Pow(t - 60, -0.0755148492);
            b = 255;
        }

        return Color.FromRgb(Byte(r), Byte(g), Byte(b));
    }

    private static byte Byte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);

    #endregion

    private static double Smooth(double p) => p * p * (3 - (2 * p));

    /// <summary>Linear interpolation of the zero-age table in log mass.</summary>
    private static double Interpolate(double mass, Func<(double Mass, double Years, double Luminosity, double Temperature), double> value)
    {
        var x = Math.Log10(Math.Clamp(mass, Zams[0].Mass, Zams[^1].Mass));
        for (var i = 1; i < Zams.Length; i++)
        {
            var x1 = Math.Log10(Zams[i].Mass);
            if (x <= x1)
            {
                var x0 = Math.Log10(Zams[i - 1].Mass);
                var f = (x - x0) / (x1 - x0);
                return value(Zams[i - 1]) + ((value(Zams[i]) - value(Zams[i - 1])) * f);
            }
        }

        return value(Zams[^1]);
    }
}
