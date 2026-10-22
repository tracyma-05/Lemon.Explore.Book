namespace Lemon.Explore.Book.Chapters.Greenhouse;

/// <summary>The views of the greenhouse-effect page.</summary>
public enum GreenhouseMode
{
    /// <summary>A slice of ground, air and space: sunlight in, the ground's heat caught and sent back by the air.</summary>
    Planet,

    /// <summary>One molecule at a time: how it can wiggle, and which of the ground's heat rays it catches.</summary>
    Molecules,

    /// <summary>A glass greenhouse and a parked car through a sunny day.</summary>
    GlassHouse,
}

/// <summary>
/// The planet as a whole: the temperature it would have with no greenhouse gases, how the surface warms with more CO₂,
/// and how high up the heat leaves for space.
/// </summary>
public static class PlanetModel
{
    /// <summary>Stefan–Boltzmann constant, W/m²/K⁴.</summary>
    public const double Sigma = 5.670374419e-8;

    /// <summary>Sunlight at Earth's distance, W/m².</summary>
    public const double SolarConstant = 1361;

    /// <summary>The share of sunlight Earth sends straight back (clouds, ice, bright ground).</summary>
    public const double Albedo = 0.3;

    /// <summary>CO₂ before industry (around 1750), ppm.</summary>
    public const double PreindustrialCo2 = 280;

    /// <summary>CO₂ in 2024 (Mauna Loa yearly mean, rounded), ppm.</summary>
    public const double TodayCo2 = 425;

    public const double MinCo2 = 150;

    public const double MaxCo2 = 1200;

    /// <summary>The world's average surface temperature before industry (1850–1900, about), °C.</summary>
    public const double PreindustrialSurface = 13.8;

    /// <summary>Warming once everything has caught up, per doubling of CO₂, K (IPCC AR6 best estimate; likely 2.5–4).</summary>
    public const double Sensitivity = 3.0;

    /// <summary>How fast the air cools with height on average, K/km.</summary>
    public const double LapseRate = 6.5;

    /// <summary>
    /// The temperature a planet must have to send out as much heat as the sunlight it keeps: σT⁴ = S(1 − albedo)/4
    /// (the 4: a sphere's surface is four times the disc that catches the light).
    /// </summary>
    public static double EffectiveTemperature(double solar = SolarConstant, double albedo = Albedo) =>
        Math.Pow(solar * (1 - albedo) / (4 * Sigma), 0.25);

    /// <summary>Earth with no greenhouse gases at all (albedo kept), °C: about −18.</summary>
    public static double BareCelsius => EffectiveTemperature() - 273.15;

    /// <summary>The extra push of more CO₂ than before industry, W/m² (Myhre et al. 1998: 5.35 ln C/C₀).</summary>
    public static double Forcing(double ppm) => 5.35 * Math.Log(Math.Max(1, ppm) / PreindustrialCo2);

    /// <summary>The average surface temperature this much CO₂ settles at (other gases and the ice as before industry), °C.</summary>
    public static double SurfaceCelsius(double ppm) => PreindustrialSurface + (Sensitivity * Math.Log2(Math.Max(1, ppm) / PreindustrialCo2));

    /// <summary>
    /// How high up, on average, the heat that reaches space leaves from, km: the height where the air is at the effective
    /// temperature, going up from the surface at the lapse rate. More CO₂ raises it; up there it is colder, so less heat
    /// gets out until the whole column has warmed.
    /// </summary>
    public static double EmissionHeight(double ppm) => (SurfaceCelsius(ppm) + 273.15 - EffectiveTemperature()) / LapseRate;

    /// <summary>The air's temperature at a height over the surface (up to the tropopause, about 11 km), °C.</summary>
    public static double AirCelsius(double ppm, double km) => SurfaceCelsius(ppm) - (LapseRate * Math.Min(km, 11));

    /// <summary>Heat the surface sends out as infrared, W/m² (σT⁴).</summary>
    public static double SurfaceEmission(double celsius) => Sigma * Math.Pow(celsius + 273.15, 4);

    /// <summary>The planets compared (NASA fact sheets): sunlight W/m², Bond albedo, average surface temperature K.</summary>
    public static IReadOnlyList<World> Worlds { get; } =
    [
        new("Venus", 2601.3, 0.77, 737),
        new("Earth", SolarConstant, 0.306, 288),

        // Mars' thin CO₂ air warms it only a few kelvin; estimates are around 5 K.
        new("Mars", 586.2, 0.25, 215),
    ];
}

/// <summary>A planet: its sunlight, how much it reflects and its real average surface temperature.</summary>
public sealed record World(string Id, double Solar, double Albedo, double Surface)
{
    /// <summary>Without greenhouse gases, K.</summary>
    public double Bare => PlanetModel.EffectiveTemperature(Solar, Albedo);

    /// <summary>What the greenhouse gases add, K.</summary>
    public double Warming => Surface - Bare;
}

/// <summary>
/// Earth's average energy flows, W/m² (Trenberth, Fasullo and Kiehl 2009, for March 2000 – May 2004). Sunlight in equals
/// sunlight reflected plus heat out, give or take the 0.9 the planet keeps.
/// </summary>
public static class EnergyBudget
{
    public const double Incoming = 341;
    public const double ReflectedByAir = 79;
    public const double ReflectedBySurface = 23;
    public const double AbsorbedByAir = 78;
    public const double AbsorbedBySurface = 161;
    public const double Thermals = 17;
    public const double Evaporation = 80;
    public const double SurfaceRadiation = 396;
    public const double Window = 40;
    public const double BackRadiation = 333;
    public const double OutgoingFromAir = 199;
    public const double Kept = 0.9;

    public static double Reflected => ReflectedByAir + ReflectedBySurface;

    public static double Outgoing => OutgoingFromAir + Window;

    /// <summary>The share of the ground's infrared that goes straight out to space.</summary>
    public static double WindowShare => Window / SurfaceRadiation;
}

/// <summary>A gas of the air.</summary>
public enum Gas
{
    N2,
    O2,
    Ar,
    CO2,
    H2O,
    CH4,
    O3,
}

/// <summary>
/// Infrared light: the glow of a body at a temperature (Planck), and a schematic of which wavelengths each gas in a
/// whole column of air catches. The bands are the real ones; their shapes are simplified (a real band is thousands of
/// lines).
/// </summary>
public static class InfraredModel
{
    private const double H = 6.62607015e-34;
    private const double C = 2.99792458e8;
    private const double K = 1.380649e-23;

    /// <summary>Wien: the wavelength of brightest glow, µm (about 10 µm for the ground, 0.5 µm for the Sun).</summary>
    public static double PeakWavelength(double kelvin) => 2897.77 / kelvin;

    /// <summary>Spectral radiance of a black body, W/m²/sr/µm.</summary>
    public static double Planck(double micrometres, double kelvin)
    {
        var lambda = micrometres * 1e-6;
        var x = H * C / (lambda * K * kelvin);
        if (x > 700) return 0;
        return 2 * H * C * C / Math.Pow(lambda, 5) / (Math.Exp(x) - 1) * 1e-6;
    }

    /// <summary>The glow per step of log wavelength (λ·B), the right curve to draw on a log axis, so equal areas are equal power.</summary>
    public static double PerLog(double micrometres, double kelvin) => micrometres * Planck(micrometres, kelvin);

    /// <summary>The share of a black body's power between two wavelengths, µm.</summary>
    public static double Share(double from, double to, double kelvin, int steps = 2000)
    {
        double Integrate(double a, double b)
        {
            var la = Math.Log(a);
            var lb = Math.Log(b);
            var sum = 0.0;
            for (var i = 0; i < steps; i++)
            {
                var l = la + ((lb - la) * (i + 0.5) / steps);
                sum += PerLog(Math.Exp(l), kelvin);
            }

            return sum * (lb - la) / steps;
        }

        return Integrate(from, to) / Integrate(0.05, 2000);
    }

    /// <summary>A band with a flat top and steep sides.</summary>
    private static double Band(double micrometres, double centre, double halfWidth, double strength) =>
        strength * Math.Exp(-Math.Pow((micrometres - centre) / halfWidth, 4));

    /// <summary>How much of the light at this wavelength one gas in the whole air column catches, 0…1 (schematic).</summary>
    public static double Absorption(Gas gas, double micrometres, double co2 = PlanetModel.TodayCo2)
    {
        var l = micrometres;
        var a = gas switch
        {
            // The 15 µm band is so strong that its middle is black; more CO₂ widens its edges (by ln C).
            Gas.CO2 => Math.Max(Band(l, 15, Co2HalfWidth(co2), 1), Math.Max(Band(l, 4.3, 0.18, 1), Band(l, 2.7, 0.08, 0.6))),

            // Water: the 6.3 µm band, the stretches near 2.7 µm, its spinning beyond about 18 µm, and a weak continuum.
            Gas.H2O => Math.Max(Math.Max(Band(l, 6.3, 1.3, 1), Band(l, 2.75, 0.3, 1)),
                Math.Max(Math.Clamp((l - 15) / 7, 0, 1), 0.08 + (0.06 * Math.Clamp((l - 8) / 5, 0, 1)))),
            Gas.CH4 => Math.Max(Band(l, 7.7, 0.4, 0.75), Band(l, 3.3, 0.15, 0.6)),
            Gas.O3 => Math.Max(Band(l, 9.6, 0.3, 0.7), Band(l, 14.3, 0.4, 0.2)),
            _ => 0,
        };
        return Math.Clamp(a, 0, 1);
    }

    /// <summary>Half the width of the CO₂ 15 µm band, µm: wider with more CO₂, about 0.25 µm per doubling.</summary>
    public static double Co2HalfWidth(double co2) => 1.75 + (0.36 * Math.Log(Math.Max(10, co2) / PlanetModel.PreindustrialCo2));

    /// <summary>The whole air (CO₂, water, methane, ozone) together.</summary>
    public static double AirAbsorption(double micrometres, double co2 = PlanetModel.TodayCo2)
    {
        var through = 1.0;
        foreach (var gas in new[] { Gas.CO2, Gas.H2O, Gas.CH4, Gas.O3 }) through *= 1 - Absorption(gas, micrometres, co2);
        return 1 - through;
    }

    /// <summary>The share of the ground's glow (at <paramref name="kelvin"/>) that this gas alone would catch.</summary>
    public static double CaughtShare(Gas gas, double kelvin = 288, double co2 = PlanetModel.TodayCo2, int steps = 1200) =>
        CaughtShare(l => Absorption(gas, l, co2), kelvin, steps);

    /// <summary>The share of the ground's glow the whole air catches.</summary>
    public static double AirCaughtShare(double kelvin = 288, double co2 = PlanetModel.TodayCo2, int steps = 1200) =>
        CaughtShare(l => AirAbsorption(l, co2), kelvin, steps);

    private static double CaughtShare(Func<double, double> absorption, double kelvin, int steps)
    {
        double caught = 0, total = 0;
        var la = Math.Log(1.0);
        var lb = Math.Log(200.0);
        for (var i = 0; i < steps; i++)
        {
            var l = Math.Exp(la + ((lb - la) * (i + 0.5) / steps));
            var glow = PerLog(l, kelvin);
            total += glow;
            caught += glow * absorption(l);
        }

        return caught / total;
    }

    /// <summary>
    /// A wavelength drawn at random from the ground's glow between 3 and 40 µm (for the photons of the pictures);
    /// <paramref name="u"/> is uniform in 0…1.
    /// </summary>
    public static double SampleGround(double u, double kelvin = 288) => GroundTable.Sample(u, kelvin);

    private static class GroundTable
    {
        private const int Steps = 400;
        private static readonly Dictionary<int, double[]> Tables = [];

        public static double Sample(double u, double kelvin)
        {
            var key = (int)Math.Round(kelvin);
            double[] cdf;
            lock (Tables)
            {
                if (!Tables.TryGetValue(key, out cdf!)) Tables[key] = cdf = Build(key);
            }

            u = Math.Clamp(u, 0, 1);
            var index = Array.BinarySearch(cdf, u);
            if (index < 0) index = Math.Max(1, ~index);
            index = Math.Clamp(index, 1, Steps);
            var t = (u - cdf[index - 1]) / Math.Max(1e-12, cdf[index] - cdf[index - 1]);
            return Math.Exp(Math.Log(3) + ((Math.Log(40) - Math.Log(3)) * (index - 1 + t) / Steps));
        }

        private static double[] Build(double kelvin)
        {
            var cdf = new double[Steps + 1];
            var la = Math.Log(3);
            var lb = Math.Log(40);
            for (var i = 0; i < Steps; i++)
            {
                var l = Math.Exp(la + ((lb - la) * (i + 0.5) / Steps));
                cdf[i + 1] = cdf[i] + PerLog(l, kelvin);
            }

            for (var i = 0; i <= Steps; i++) cdf[i] /= cdf[Steps];
            return cdf;
        }
    }
}

/// <summary>A point or a shift in a molecule, ångström; x right, y down, z towards the viewer.</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vec3 operator *(Vec3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);

    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);

    public double Length => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

    public Vec3 Unit => Length < 1e-12 ? this : this * (1 / Length);

    public double Dot(Vec3 b) => (X * b.X) + (Y * b.Y) + (Z * b.Z);
}

/// <summary>An atom of a molecule: its element symbol, place, mass (u) and small electric charge (in parts of an electron's).</summary>
public sealed record Atom(string Element, Vec3 Position, double Mass, double Charge);

/// <summary>
/// One way a molecule can wiggle (a normal mode): its wavenumber (cm⁻¹), whether it changes the molecule's dipole, so
/// that infrared light can drive it, the band of wavelengths it catches in the air, and each atom's shift at its
/// largest (ångström, centre of mass kept still). A spinning water molecule is a "mode" too.
/// </summary>
public sealed record Vibration(string Id, double Wavenumber, bool Active, double CatchFrom, double CatchTo, Vec3[] Shifts, bool Spin = false)
{
    /// <summary>The wavelength of light with the same frequency, µm.</summary>
    public double Wavelength => Spin ? 50 : 10000 / Wavenumber;

    public bool Catches(double micrometres) => Active && micrometres >= CatchFrom && micrometres <= CatchTo;
}

/// <summary>A molecule of the air: its atoms and its ways to wiggle.</summary>
public sealed record Molecule(Gas Gas, Atom[] Atoms, Vibration[] Modes, double AirShare)
{
    /// <summary>The atoms where they are at this point of a vibration (<paramref name="swing"/> −1…1), or turned by a spin.</summary>
    public Vec3[] Positions(Vibration? mode, double swing, double spinAngle = 0)
    {
        var result = new Vec3[Atoms.Length];
        for (var i = 0; i < Atoms.Length; i++)
        {
            var p = Atoms[i].Position;
            if (mode is not null && !mode.Spin) p += mode.Shifts[i] * swing;
            result[i] = p;
        }

        if (mode is { Spin: true })
        {
            var (s, c) = Math.SinCos(spinAngle);
            for (var i = 0; i < result.Length; i++) result[i] = new Vec3((result[i].X * c) - (result[i].Y * s), (result[i].X * s) + (result[i].Y * c), result[i].Z);
        }

        return result;
    }

    /// <summary>The dipole: Σ charge × place, e·Å (zero for a molecule whose charges sit symmetrically).</summary>
    public Vec3 Dipole(IReadOnlyList<Vec3> positions)
    {
        var d = new Vec3(0, 0, 0);
        for (var i = 0; i < Atoms.Length; i++) d += positions[i] * Atoms[i].Charge;
        return d;
    }

    /// <summary>Does this molecule catch light of this wavelength with any of its active modes? The mode, or null.</summary>
    public Vibration? Catcher(double micrometres) => Modes.FirstOrDefault(m => m.Catches(micrometres));
}

/// <summary>
/// The molecules of the page with their real shapes, wavenumbers and the share of the air they make up. The atoms'
/// charges are rough partial charges, enough to show whether a wiggle shifts the charge; the shifts are drawn large.
/// </summary>
public static class Molecules
{
    public static IReadOnlyList<Gas> Shown { get; } = [Gas.N2, Gas.O2, Gas.Ar, Gas.CO2, Gas.H2O, Gas.CH4];

    public static Molecule Get(Gas gas) => All[gas];

    private static readonly Dictionary<Gas, Molecule> All = Build();

    private static Dictionary<Gas, Molecule> Build()
    {
        var x = new Vec3(1, 0, 0);
        var y = new Vec3(0, 1, 0);
        var zero = new Vec3(0, 0, 0);

        // Two of the same atom: the stretch keeps the charges symmetric (there are none), so light cannot drive it.
        Molecule Pair(Gas gas, string element, double mass, double bond, double wavenumber, double share)
        {
            var atoms = new[] { new Atom(element, x * (-bond / 2), mass, 0), new Atom(element, x * (bond / 2), mass, 0) };
            var stretch = new Vibration("Stretch", wavenumber, false, 0, 0, [x * -0.16, x * 0.16]);
            return new Molecule(gas, atoms, [stretch], share);
        }

        var co2Atoms = new[]
        {
            new Atom("O", x * -1.16, 16, -0.35),
            new Atom("C", zero, 12, 0.7),
            new Atom("O", x * 1.16, 16, -0.35),
        };

        // In each mode the carbon moves so the centre of mass stays put (16 + 16 = 12 × 2.67).
        var co2 = new Molecule(Gas.CO2, co2Atoms,
        [
            new Vibration("SymmetricStretch", 1333, false, 0, 0, [x * -0.2, zero, x * 0.2]),
            new Vibration("Bend", 667, true, 13.2, 17.0, [y * 0.16, y * (-0.32 * 16 / 12), y * 0.16]),
            new Vibration("AsymmetricStretch", 2349, true, 4.15, 4.45, [x * 0.12, x * -0.32, x * 0.12]),
        ], 425e-6);

        // Water: bent at 104.5°, 0.958 Å bonds; the hydrogens below the oxygen.
        var bond1 = new Vec3(-0.757, 0.586, 0);
        var bond2 = new Vec3(0.757, 0.586, 0);
        var u1 = bond1.Unit;
        var u2 = bond2.Unit;
        var t1 = new Vec3(0.612, 0.79, 0);
        var t2 = new Vec3(-0.612, 0.79, 0);
        var h2oAtoms = new[]
        {
            new Atom("O", zero, 16, -0.8),
            new Atom("H", bond1, 1, 0.4),
            new Atom("H", bond2, 1, 0.4),
        };
        var h2o = new Molecule(Gas.H2O, h2oAtoms,
        [
            new Vibration("SymmetricStretch", 3657, true, 2.6, 2.9, [-(u1 + u2) * (0.16 / 16), u1 * 0.16, u2 * 0.16]),
            new Vibration("Bend", 1595, true, 5.4, 7.6, [-(t1 + t2) * (0.3 / 16), t1 * 0.3, t2 * 0.3]),
            new Vibration("AsymmetricStretch", 3756, true, 2.55, 2.8, [-(u1 - u2) * (0.16 / 16), u1 * 0.16, -u2 * 0.16]),
            new Vibration("Spin", 200, true, 18, 1000, [zero, zero, zero], Spin: true),
        ], 0.004);

        // Methane: a tetrahedron with one hydrogen straight up, one in front below and two behind at the sides.
        var hs = Tetrahedron(1.09);
        var ch4Atoms = new Atom[5];
        ch4Atoms[0] = new Atom("C", zero, 12, -0.4);
        for (var i = 0; i < 4; i++) ch4Atoms[i + 1] = new Atom("H", hs[i], 1, 0.1);
        var hu = hs.Select(h => h.Unit).ToArray();

        Vec3[] WithCarbon(Vec3[] h)
        {
            var sum = h.Aggregate(zero, (a, b) => a + b);
            return [-sum * (1.0 / 12), .. h];
        }

        var symStretch = hu.Select(u => u * 0.16).ToArray();
        var symBend = new[] { (hu[1] - hu[0]) * 0.14, (hu[0] - hu[1]) * 0.14, (hu[2] - hu[3]) * 0.14, (hu[3] - hu[2]) * 0.14 };
        var asymStretch = hu.Select(u => x * (0.14 * Math.Abs(u.X) + 0.04)).ToArray();
        // Along the screen's vertical, so the dipole it makes shows in the picture.
        var axis = new Vec3(0, 1, 0);
        var asymBend = hu.Select(u => (axis - (u * axis.Dot(u))) * 0.22).ToArray();
        var ch4 = new Molecule(Gas.CH4, ch4Atoms,
        [
            new Vibration("SymmetricStretch", 2917, false, 0, 0, WithCarbon(symStretch)),
            new Vibration("SymmetricBend", 1534, false, 0, 0, WithCarbon(symBend)),
            new Vibration("AsymmetricStretch", 3019, true, 3.2, 3.45, WithCarbon(asymStretch)),
            new Vibration("AsymmetricBend", 1306, true, 7.3, 8.1, WithCarbon(asymBend)),
        ], 1.9e-6);

        return new Dictionary<Gas, Molecule>
        {
            [Gas.N2] = Pair(Gas.N2, "N", 14, 1.10, 2330, 0.7808),
            [Gas.O2] = Pair(Gas.O2, "O", 16, 1.21, 1556, 0.2095),
            [Gas.Ar] = new Molecule(Gas.Ar, [new Atom("Ar", zero, 40, 0)], [], 0.0093),
            [Gas.CO2] = co2,
            [Gas.H2O] = h2o,
            [Gas.CH4] = ch4,
        };
    }

    /// <summary>
    /// The four corners of a regular tetrahedron at <paramref name="bond"/> from its centre: one straight up, three below
    /// in a ring (one towards the viewer), tilted 25° so none hides behind the centre.
    /// </summary>
    private static Vec3[] Tetrahedron(double bond)
    {
        var ring = Math.Sqrt(8) / 3;
        Vec3[] corners =
        [
            new(0, -1, 0),
            .. new[] { -30.0, 90, 210 }.Select(a => new Vec3(ring * Math.Cos(a * Math.PI / 180), 1.0 / 3, ring * Math.Sin(a * Math.PI / 180))),
        ];
        var (s, c) = Math.SinCos(25 * Math.PI / 180);
        return corners.Select(v => new Vec3(v.X, (v.Y * c) + (v.Z * s), (-v.Y * s) + (v.Z * c)) * bond).ToArray();
    }
}

/// <summary>The season of the glass-house day: where the Sun goes and how warm the outside air is.</summary>
public enum Season
{
    Summer,
    Equinox,
    Winter,
}

/// <summary>A moment of the glass-house day: the Sun, the sunlight on the ground (W/m²) and the temperatures (°C).</summary>
public readonly record struct DayState(double Hour, double SunAltitude, double Sunlight, double Outside, double Greenhouse, double Floor, double Car);

/// <summary>The options of the glass-house day.</summary>
public readonly record struct DayOptions(double Latitude, Season Season, bool VentsOpen, bool SaltPane, bool CarWindowOpen);

/// <summary>
/// A clear day for a small glass greenhouse and a car parked in the sun, worked out minute by minute (three days, the
/// last one kept, so the day starts as warm as it ends). Each is a lump model per square metre of floor:
/// <list type="bullet">
/// <item>Greenhouse: the floor (soil and plants) takes in the sunlight through the glass, warms the air, keeps some
/// heat for the night and sends its infrared to the glass (which sits between inside and outside); the air loses heat
/// through the glass and with the air that leaks out (1.5 changes an hour shut, 30 with the vents open). A rock-salt
/// pane lets the infrared through to the cold sky, as in R. W. Wood's 1909 experiment.</item>
/// <item>Car: one lump (air, seats, dashboard) warmed by the sun through the windows, about 17 minutes to settle.</item>
/// </list>
/// </summary>
public static class GlassHouseModel
{
    private const double Deg = Math.PI / 180;

    /// <summary>Glass lets through this much of the sunlight.</summary>
    public const double PaneTransmission = 0.85;

    /// <summary>Of the sunlight through the glass, the share the floor and plants keep as heat (some is reflected, some evaporates water).</summary>
    public const double FloorAbsorbed = 0.5;

    public const double FloorCapacity = 80e3;
    public const double AirCapacity = 20e3;
    public const double FloorToAir = 8;
    public const double FloorToDeep = 2;
    public const double FloorInfrared = 5;
    public const double GlassLoss = 7;
    public const double CarCapacity = 18e3;
    public const double CarLoss = 18;
    public const double CarLossWindowOpen = 20.5;

    /// <summary>Air changes per hour: shut, and with the roof vents open.</summary>
    public const double ShutAirChanges = 1.5;

    public const double OpenAirChanges = 30;

    /// <summary>The clear sky's infrared temperature is this far below the air's, K.</summary>
    public const double SkyColder = 20;

    /// <summary>The Sun's declination for the season on this side of the equator, degrees.</summary>
    public static double Declination(Season season, double latitude) => season switch
    {
        Season.Summer => 23.44 * (latitude >= 0 ? 1 : -1),
        Season.Winter => -23.44 * (latitude >= 0 ? 1 : -1),
        _ => 0,
    };

    /// <summary>The Sun's altitude at a local solar time, degrees.</summary>
    public static double SunAltitude(double hour, double latitude, double declination)
    {
        var h = (hour - 12) * 15 * Deg;
        var sin = (Math.Sin(latitude * Deg) * Math.Sin(declination * Deg)) + (Math.Cos(latitude * Deg) * Math.Cos(declination * Deg) * Math.Cos(h));
        return Math.Asin(Math.Clamp(sin, -1, 1)) / Deg;
    }

    /// <summary>The Sun's direction along the sky, −1 (east, morning) … 1 (west, evening), for drawing its arc.</summary>
    public static double SunAcross(double hour, double latitude, double declination)
    {
        var h = (hour - 12) * 15 * Deg;
        var east = Math.Sin(h) * Math.Cos(declination * Deg);
        return Math.Clamp(east, -1, 1);
    }

    /// <summary>The Sun's direct beam through a clear sky, W/m² facing the Sun (Meinel: 1361 × 0.7^(AM^0.678)).</summary>
    public static double DirectBeam(double altitude)
    {
        if (altitude <= 0) return 0;
        var h = altitude;
        var airMass = 1 / (Math.Sin(h * Deg) + (0.50572 * Math.Pow(h + 6.07995, -1.6364)));
        return PlanetModel.SolarConstant * Math.Pow(0.7, Math.Pow(airMass, 0.678));
    }

    /// <summary>Sunlight on level ground on a clear day, beam and sky together, W/m².</summary>
    public static double Sunlight(double altitude) => altitude <= 0 ? 0 : 1.1 * DirectBeam(altitude) * Math.Sin(altitude * Deg);

    /// <summary>A typical clear day's coolest (at dawn) and warmest (mid-afternoon) air, °C.</summary>
    public static (double Low, double High) OutsideRange(Season season) => season switch
    {
        Season.Summer => (25, 34),
        Season.Winter => (1, 10),
        _ => (13, 23),
    };

    /// <summary>The outside air: coolest at 5:00, warmest at 15:00.</summary>
    public static double Outside(double hour, Season season)
    {
        var (low, high) = OutsideRange(season);
        hour = ((hour % 24) + 24) % 24;
        if (hour >= 5 && hour <= 15) return low + ((high - low) * (1 - Math.Cos(Math.PI * (hour - 5) / 10)) / 2);
        var f = (((hour - 15) % 24) + 24) % 24 / 14;
        return high - ((high - low) * (1 - Math.Cos(Math.PI * f)) / 2);
    }

    private static readonly Dictionary<DayOptions, DayState[]> Days = [];

    /// <summary>The day minute by minute, 0:00 to 24:00 (1441 moments), worked out once per set of options.</summary>
    public static DayState[] Day(DayOptions options)
    {
        options = options with { Latitude = Math.Round(options.Latitude, 1) };
        lock (Days)
        {
            if (Days.TryGetValue(options, out var day)) return day;
            if (Days.Count > 64) Days.Clear();
            return Days[options] = Simulate(options);
        }
    }

    /// <summary>The day at an hour (0…24), between the minutes.</summary>
    public static DayState At(DayOptions options, double hour)
    {
        var day = Day(options);
        hour = Math.Clamp(hour, 0, 24);
        var i = Math.Min(1439, (int)Math.Floor(hour * 60));
        var t = (hour * 60) - i;
        var a = day[i];
        var b = day[i + 1];
        double L(double p, double q) => p + ((q - p) * t);
        return new DayState(hour, L(a.SunAltitude, b.SunAltitude), L(a.Sunlight, b.Sunlight), L(a.Outside, b.Outside),
            L(a.Greenhouse, b.Greenhouse), L(a.Floor, b.Floor), L(a.Car, b.Car));
    }

    /// <summary>The sunlight the car's cabin takes in, W per unit: through the roof's glass and the side windows.</summary>
    public static double CarSunlight(double altitude) =>
        altitude <= 0 ? 0 : (0.45 * Sunlight(altitude)) + (0.15 * DirectBeam(altitude) * Math.Cos(altitude * Deg));

    private static DayState[] Simulate(DayOptions o)
    {
        var declination = Declination(o.Season, o.Latitude);
        var (low, high) = OutsideRange(o.Season);
        var deep = (low + high) / 2;
        var vent = 1200 * 2.5 / 3600 * (o.VentsOpen ? OpenAirChanges : ShutAirChanges);
        var carLoss = o.CarWindowOpen ? CarLossWindowOpen : CarLoss;
        double floor = deep, air = deep, car = deep;
        var day = new DayState[1441];
        const double dt = 60;
        for (var n = 0; n <= 3 * 1440; n++)
        {
            var hour = (n % 1440) / 60.0;
            var altitude = SunAltitude(hour, o.Latitude, declination);
            var sun = Sunlight(altitude);
            var outside = Outside(hour, o.Season);
            if (n >= 2 * 1440)
            {
                var i = n - (2 * 1440);
                day[i] = new DayState(i / 60.0, altitude, sun, outside, air, floor, car);
            }

            // The pane's infrared: glass soaks it up and sits halfway between inside and out; salt lets it reach the sky.
            var target = o.SaltPane ? outside - SkyColder : (air + outside) / 2;
            var floorIn = (PaneTransmission * FloorAbsorbed * sun) - (FloorToAir * (floor - air)) - (FloorToDeep * (floor - deep)) - (FloorInfrared * (floor - target));
            var airIn = (FloorToAir * (floor - air)) - ((GlassLoss + vent) * (air - outside));
            var carIn = CarSunlight(altitude) - (carLoss * (car - outside));
            floor += floorIn * dt / FloorCapacity;
            air += airIn * dt / AirCapacity;
            car += carIn * dt / CarCapacity;
        }

        return day;
    }
}

/// <summary>CO₂ in the air through the years, ppm: Law Dome ice (rounded) before 1959, Mauna Loa yearly means after.</summary>
public static class Co2Record
{
    public static IReadOnlyList<(int Year, double Ppm)> Points { get; } = Build();

    private static List<(int Year, double Ppm)> Build()
    {
        var list = new List<(int, double)>
        {
            (1750, 277), (1800, 283), (1850, 285), (1880, 291), (1900, 296), (1920, 303), (1940, 310), (1950, 311),
        };
        double[] maunaLoa =
        [
            315.98, 316.91, 317.64, 318.45, 318.99, 319.62, 320.04, 321.37, 322.18, 323.05,
            324.62, 325.68, 326.32, 327.46, 329.68, 330.19, 331.12, 332.03, 333.84, 335.41,
            336.84, 338.76, 340.12, 341.48, 343.15, 344.87, 346.35, 347.61, 349.31, 351.69,
            353.20, 354.45, 355.70, 356.54, 357.21, 358.96, 360.97, 362.74, 363.88, 366.84,
            368.54, 369.71, 371.32, 373.45, 375.98, 377.70, 379.98, 382.09, 384.02, 385.83,
            387.64, 390.10, 391.85, 394.06, 396.74, 398.81, 401.01, 404.41, 406.76, 408.72,
            411.65, 414.21, 416.41, 418.53, 421.08, 424.61,
        ];
        for (var i = 0; i < maunaLoa.Length; i++) list.Add((1959 + i, maunaLoa[i]));
        return list;
    }
}
