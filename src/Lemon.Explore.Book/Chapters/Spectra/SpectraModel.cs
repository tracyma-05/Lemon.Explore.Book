using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.BlueSky;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Spectra;

/// <summary>The three views of the spectra page.</summary>
public enum SpectraMode
{
    /// <summary>A hydrogen atom: its energy levels, an electron jumping between them, and the light each jump gives.</summary>
    Atom,

    /// <summary>Lamps through a prism: the bright lines of each gas, a bulb's rainbow, the Sun's dark lines, light through a cool gas.</summary>
    Lamps,

    /// <summary>Starlight: a star's colour from its temperature, its dark lines, and lines shifted by its motion.</summary>
    Stars,
}

/// <summary>The gases in the lamps.</summary>
public enum Gas
{
    Hydrogen,
    Helium,
    Neon,
    Sodium,
    Mercury,
}

/// <summary>What shines into the spectroscope.</summary>
public enum LightSource
{
    /// <summary>A lamp of one glowing gas: bright lines.</summary>
    Lamp,

    /// <summary>A hot filament: every colour, a continuous rainbow.</summary>
    Bulb,

    /// <summary>Sunlight: a rainbow crossed by dark lines.</summary>
    Sun,

    /// <summary>A bulb's light through a cool gas: the rainbow with that gas's lines dark.</summary>
    ThroughGas,
}

/// <summary>Where an electron that falls goes: by chance, one level at a time, or straight to the bottom.</summary>
public enum FallMode
{
    Chance,
    OneStep,
    ToGround,
}

/// <summary>A line of a spectrum: its wavelength in air (nm) and how strong it is (0–1).</summary>
public readonly record struct SpectralLine(double Nm, double Strength);

/// <summary>A dark line or band in a star: where, how deep (0–1) and how wide (nm); a band fades out towards the red.</summary>
public readonly record struct Absorption(double Nm, double Depth, double Width, bool Band = false);

/// <summary>A star to look at: its name's resource key, surface temperature (K) and spectral type.</summary>
public sealed record StarPreset(string Key, double Kelvin, string Type);

/// <summary>
/// The physics of the spectra page. Hydrogen: Bohr's energy levels, the Rydberg formula for its lines (vacuum, turned into
/// air wavelengths above 200 nm), Kramers' approximation for which way an excited electron falls, and the electron clouds
/// of its orbitals. Gases: the strong visible lines of five lamps (typed from the NIST tables, strengths as seen in a
/// classroom spectroscope). Stars: Planck's law, Wien's peak, the colour of a black body, Saha–Boltzmann for how strong
/// hydrogen's lines are, simpler bumps for the other lines, and the Doppler shift.
/// </summary>
public static class SpectraModel
{
    /// <summary>The visible range drawn, nm.</summary>
    public const double VisibleFrom = 380;

    public const double VisibleTo = 750;

    /// <summary>Planck's constant × the speed of light, eV·nm: a photon's energy is this over its wavelength.</summary>
    public const double PhotonConstant = 1239.842;

    /// <summary>The speed of light, km/s.</summary>
    public const double LightSpeed = 299_792.458;

    #region hydrogen

    /// <summary>Hydrogen's ionisation energy, eV (Rydberg's constant for hydrogen's reduced mass).</summary>
    public const double Ionisation = 13.598;

    /// <summary>Rydberg's constant for hydrogen, per metre.</summary>
    public const double RydbergHydrogen = 10_967_758;

    /// <summary>The highest level drawn.</summary>
    public const int TopLevel = 6;

    /// <summary>A level's energy, eV (0 is the electron just set free).</summary>
    public static double Energy(int n) => -Ionisation / (n * n);

    /// <summary>The wavelength of the light given out (or taken in) between two levels, in air above 200 nm (in vacuum below), nm.</summary>
    public static double Wavelength(int lower, int upper)
    {
        var vacuum = 1e9 / (RydbergHydrogen * ((1.0 / (lower * lower)) - (1.0 / (upper * upper))));
        return ToAir(vacuum);
    }

    /// <summary>A vacuum wavelength in air (Ciddor's dispersion of standard air); unchanged below 200 nm, where air absorbs.</summary>
    public static double ToAir(double vacuumNm)
    {
        if (vacuumNm < 200) return vacuumNm;
        var s2 = Math.Pow(1000 / vacuumNm, 2);
        var n = 1 + (0.05792105 / (238.0185 - s2)) + (0.00167917 / (57.362 - s2));
        return vacuumNm / n;
    }

    /// <summary>A photon's energy, eV.</summary>
    public static double PhotonEnergy(double nm) => PhotonConstant / nm;

    /// <summary>The series a line belongs to by its lower level: 1 Lyman, 2 Balmer, 3 Paschen, 4 Brackett, 5 Pfund.</summary>
    public static string SeriesKey(int lower) => lower switch
    {
        1 => "Lyman",
        2 => "Balmer",
        3 => "Paschen",
        4 => "Brackett",
        _ => "Pfund",
    };

    /// <summary>
    /// What a photon does to an electron on <paramref name="level"/>: lifts it to the level returned when its energy
    /// matches a jump to within <paramref name="tolerance"/> (a share), sets it free (0) when it has more than enough, or
    /// nothing (−1): it passes straight through.
    /// </summary>
    public static int Absorb(int level, double nm, double tolerance = 0.004)
    {
        var energy = PhotonEnergy(nm);
        for (var upper = level + 1; upper <= 12; upper++)
        {
            var gap = Energy(upper) - Energy(level);
            if (Math.Abs(energy - gap) <= gap * tolerance) return upper;
        }

        return energy >= -Energy(level) ? 0 : -1;
    }

    /// <summary>
    /// The chance of falling from <paramref name="upper"/> to each lower level (index 1 … upper − 1), from Kramers'
    /// semi-classical rates: A ∝ 1 / (m³ (1/m² − 1/n²)), so big jumps to the bottom are the commonest.
    /// </summary>
    public static double[] FallChances(int upper)
    {
        var chances = new double[upper];
        for (var m = 1; m < upper; m++) chances[m] = 1 / (m * m * m * ((1.0 / (m * m)) - (1.0 / (upper * upper))));
        var total = chances.Sum();
        for (var m = 1; m < upper; m++) chances[m] /= total;
        return chances;
    }

    /// <summary>Where an electron on <paramref name="upper"/> falls to, by <paramref name="mode"/> (<paramref name="draw"/> 0–1 picks by chance).</summary>
    public static int FallTo(int upper, FallMode mode, double draw)
    {
        if (upper <= 1) return 1;
        switch (mode)
        {
            case FallMode.OneStep:
                return upper - 1;

            case FallMode.ToGround:
                return 1;
        }

        var chances = FallChances(upper);
        var sum = 0.0;
        for (var m = 1; m < upper; m++)
        {
            sum += chances[m];
            if (draw < sum) return m;
        }

        return upper - 1;
    }

    /// <summary>
    /// The electron cloud of hydrogen's (n, l, m = 0) orbital: the chance of finding the electron at distance
    /// <paramref name="r"/> (Bohr radii) from the nucleus, at <paramref name="cosTheta"/> from the axis (unnormalised).
    /// </summary>
    public static double Cloud(int n, int l, double r, double cosTheta)
    {
        var rho = 2 * r / n;
        var radial = Math.Exp(-rho / 2) * Math.Pow(rho, l) * Laguerre(n - l - 1, (2 * l) + 1, rho);
        var angular = Legendre(l, cosTheta);
        return radial * radial * angular * angular;
    }

    /// <summary>The radial chance of finding the electron at distance r (r² R²), unnormalised: peaks at n² for l = n − 1.</summary>
    public static double RadialChance(int n, int l, double r)
    {
        var rho = 2 * r / n;
        var radial = Math.Exp(-rho / 2) * Math.Pow(rho, l) * Laguerre(n - l - 1, (2 * l) + 1, rho);
        return r * r * radial * radial;
    }

    private static double Laguerre(int k, double alpha, double x)
    {
        if (k <= 0) return 1;
        var previous = 1.0;
        var current = 1 + alpha - x;
        for (var i = 1; i < k; i++)
        {
            var next = ((((2 * i) + 1 + alpha - x) * current) - ((i + alpha) * previous)) / (i + 1);
            previous = current;
            current = next;
        }

        return current;
    }

    private static double Legendre(int l, double x) => l switch
    {
        0 => 1,
        1 => x,
        2 => ((3 * x * x) - 1) / 2,
        _ => ((5 * x * x * x) - (3 * x)) / 2,
    };

    #endregion

    #region the lamps

    /// <summary>The strong visible lines of a lamp of <paramref name="gas"/>, nm in air with classroom strengths.</summary>
    public static IReadOnlyList<SpectralLine> Lines(Gas gas) => gas switch
    {
        Gas.Hydrogen => HydrogenLines,
        Gas.Helium => HeliumLines,
        Gas.Neon => NeonLines,
        Gas.Sodium => SodiumLines,
        _ => MercuryLines,
    };

    /// <summary>Balmer's lines: Hα to Hε.</summary>
    private static readonly SpectralLine[] HydrogenLines =
    [
        new(656.28, 1.0), new(486.13, 0.45), new(434.05, 0.25), new(410.17, 0.15), new(397.01, 0.08),
    ];

    private static readonly SpectralLine[] HeliumLines =
    [
        new(706.52, 0.5), new(667.82, 0.6), new(587.56, 1.0), new(501.57, 0.55), new(492.19, 0.3), new(471.31, 0.3),
        new(447.15, 0.6), new(438.79, 0.15), new(402.62, 0.2), new(388.86, 0.6),
    ];

    private static readonly SpectralLine[] NeonLines =
    [
        new(540.06, 0.25), new(585.25, 1.0), new(588.19, 0.35), new(594.48, 0.55), new(597.55, 0.2), new(603.00, 0.3),
        new(607.43, 0.45), new(609.62, 0.5), new(614.31, 0.65), new(616.36, 0.4), new(621.73, 0.35), new(626.65, 0.45),
        new(630.48, 0.3), new(633.44, 0.6), new(638.30, 0.6), new(640.22, 0.9), new(650.65, 0.6), new(659.90, 0.45),
        new(667.83, 0.35), new(692.95, 0.6), new(703.24, 0.75), new(717.39, 0.2), new(724.52, 0.3), new(743.89, 0.2),
    ];

    /// <summary>The D lines (a doublet 0.6 nm apart) and a few faint ones.</summary>
    private static readonly SpectralLine[] SodiumLines =
    [
        new(588.995, 1.0), new(589.592, 0.5), new(568.27, 0.05), new(568.82, 0.08), new(615.42, 0.06), new(616.07, 0.08),
        new(498.28, 0.04),
    ];

    private static readonly SpectralLine[] MercuryLines =
    [
        new(404.66, 0.55), new(407.78, 0.15), new(435.83, 1.0), new(491.60, 0.1), new(546.07, 0.9), new(576.96, 0.4),
        new(579.07, 0.45), new(623.44, 0.05), new(690.75, 0.05),
    ];

    /// <summary>The Sun's strongest dark lines (Fraunhofer's), depth 0–1; A and B are oxygen in our own air.</summary>
    public static readonly IReadOnlyList<Absorption> SunLines =
    [
        new(393.37, 0.9, 1.6), new(396.85, 0.85, 1.4), new(430.8, 0.5, 1.0), new(434.05, 0.4, 0.6), new(486.13, 0.55, 0.7),
        new(516.73, 0.35, 0.4), new(517.27, 0.4, 0.4), new(518.36, 0.45, 0.4), new(527.0, 0.35, 0.4), new(588.995, 0.7, 0.5),
        new(589.592, 0.6, 0.45), new(656.28, 0.6, 0.8), new(686.7, 0.5, 1.4), new(759.4, 0.7, 2.0),
    ];

    /// <summary>How hot a bulb's filament is, K.</summary>
    public const double BulbKelvin = 2800;

    /// <summary>The Sun's surface temperature, K.</summary>
    public const double SunKelvin = 5772;

    #endregion

    #region light and colour

    /// <summary>Planck's law: a black body's brightness at a wavelength, relative (W per m² per sr per m, without the constant).</summary>
    public static double Planck(double nm, double kelvin) => 1 / (Math.Pow(nm * 1e-3, 5) * (Math.Exp(1.438777e7 / (nm * kelvin)) - 1));

    /// <summary>Wien's law: where a black body is brightest, nm.</summary>
    public static double PeakWavelength(double kelvin) => 2_897_771.955 / kelvin;

    /// <summary>A black body's colour, at full brightness (its brightest channel at 1).</summary>
    public static Color BodyColour(double kelvin)
    {
        double x = 0, y = 0, z = 0;
        for (var nm = 380.0; nm <= 780; nm += 5)
        {
            var (cx, cy, cz) = SkyOptics.ColourMatch(nm);
            var p = Planck(nm, kelvin);
            x += cx * p;
            y += cy * p;
            z += cz * p;
        }

        return SkyOptics.Hue(SkyOptics.XyzToRgb(x / y, 1, z / y));
    }

    /// <summary>The mixed colour a gas lamp glows with: its lines added together.</summary>
    public static Color GlowColour(Gas gas)
    {
        double x = 0, y = 0, z = 0;
        foreach (var line in Lines(gas))
        {
            var (cx, cy, cz) = SkyOptics.ColourMatch(line.Nm);
            x += cx * line.Strength;
            y += cy * line.Strength;
            z += cz * line.Strength;
        }

        var c = SkyOptics.XyzToRgb(x / y, 1, z / y);
        var lift = Math.Max(0, -Math.Min(c.R, Math.Min(c.G, c.B)));
        return SkyOptics.Hue(new LinearRgb(c.R + lift, c.G + lift, c.B + lift));
    }

    /// <summary>
    /// The colour of pure light of one wavelength as a spectrum is usually drawn (Bruton's piecewise ramps): violet, blue,
    /// cyan, green, yellow, orange, red, fading out at both ends where the eye loses it. Linear RGB, at most 1.
    /// </summary>
    public static LinearRgb SpectrumColour(double nm)
    {
        double r, g, b;
        if (nm < 440) (r, g, b) = (-(nm - 440) / 60, 0, 1);
        else if (nm < 490) (r, g, b) = (0, (nm - 440) / 50, 1);
        else if (nm < 510) (r, g, b) = (0, 1, -(nm - 510) / 20);
        else if (nm < 580) (r, g, b) = ((nm - 510) / 70, 1, 0);
        else if (nm < 645) (r, g, b) = (1, -(nm - 645) / 65, 0);
        else (r, g, b) = (1, 0, 0);

        // Dimmer towards the ends of what the eye sees.
        var fade = nm < 420 ? 0.3 + (0.7 * (nm - 380) / 40) : nm > 700 ? 0.3 + (0.7 * (780 - nm) / 80) : 1;
        fade = Math.Clamp(fade, 0.15, 1);
        return new LinearRgb(Linear(Math.Clamp(r, 0, 1) * fade), Linear(Math.Clamp(g, 0, 1) * fade), Linear(Math.Clamp(b, 0, 1) * fade));
    }

    /// <summary>
    /// The light of one wavelength at a brightness on screen (linear 0–1 and beyond): its spectrum colour, turning white in
    /// the middle when it is very bright.
    /// </summary>
    public static LinearRgb LineLight(double nm, double brightness)
    {
        var light = SpectrumColour(nm) * brightness;
        var over = Math.Max(0, brightness - 1);
        return over > 0 ? light + new LinearRgb(over * 0.5, over * 0.5, over * 0.5) : light;
    }

    private static double Linear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    #endregion

    #region stars

    /// <summary>Stars to look at, hottest first.</summary>
    public static readonly IReadOnlyList<StarPreset> Stars =
    [
        new("Spica", 22_400, "B1"),
        new("Sirius", 9_940, "A1"),
        new("Procyon", 6_530, "F5"),
        new("Sun", SunKelvin, "G2"),
        new("Arcturus", 4_290, "K1"),
        new("Betelgeuse", 3_600, "M2"),
    ];

    /// <summary>The letter of a star's spectral type by its temperature (O hottest … M coolest).</summary>
    public static char SpectralClass(double kelvin) => kelvin switch
    {
        >= 30_000 => 'O',
        >= 10_000 => 'B',
        >= 7_500 => 'A',
        >= 6_000 => 'F',
        >= 5_200 => 'G',
        >= 3_700 => 'K',
        _ => 'M',
    };

    /// <summary>
    /// The share of a star's hydrogen that can make Balmer's dark lines (neutral, its electron on level 2): Boltzmann
    /// for the level, Saha for how much is not ionised, with an electron pressure of 20 Pa as in a star's atmosphere.
    /// </summary>
    public static double BalmerShare(double kelvin)
    {
        const double K = 8.617333e-5;
        var excited = 4 * Math.Exp(-(Energy(2) - Energy(1)) / (K * kelvin));
        var electrons = 20 / (1.380649e-23 * kelvin);
        var saha = 2.4147e21 * Math.Pow(kelvin, 1.5) * Math.Exp(-Ionisation / (K * kelvin)) / electrons;
        return excited / (1 + excited) / (1 + saha);
    }

    private static readonly double BalmerPeak = Enumerable.Range(0, 400).Select(i => BalmerShare(3000 + (i * 100))).Max();

    /// <summary>How strong hydrogen's dark lines are at a temperature, 0–1 (strongest near 9,500 K, in A stars).</summary>
    public static double BalmerStrength(double kelvin) => BalmerShare(kelvin) / BalmerPeak;

    private static double Bump(double kelvin, double peak, double width) =>
        Math.Exp(-Math.Pow((Math.Log10(kelvin) - Math.Log10(peak)) / width, 2));

    /// <summary>
    /// How deep hydrogen's dark lines look, 0–1: a line's depth grows only slowly once the absorbers are many (the curve of
    /// growth), so the share from Saha–Boltzmann is compressed logarithmically. The Sun, with a thousandth of the share an
    /// A star has, still shows clear hydrogen lines.
    /// </summary>
    public static double BalmerDepth(double kelvin) => Math.Log10(1 + (1000 * BalmerStrength(kelvin))) / 3;

    /// <summary>How strong each family of lines is at a temperature, 0–1: Saha–Boltzmann for hydrogen, smooth bumps for the rest.</summary>
    public static (double Hydrogen, double HeliumNeutral, double HeliumIon, double Calcium, double Metals, double Sodium, double Molecules) LineFamilies(double kelvin) =>
    (
        BalmerDepth(kelvin),
        Bump(kelvin, 20_000, 0.12),
        kelvin < 26_000 ? 0 : Math.Min(1, (kelvin - 26_000) / 14_000),
        Bump(kelvin, 4_800, 0.13),
        Bump(kelvin, 4_600, 0.12),
        Bump(kelvin, 3_900, 0.18),
        Math.Clamp((4_000 - kelvin) / 700, 0, 1)
    );

    /// <summary>A star's dark lines and bands at a temperature, in its own (unshifted) wavelengths.</summary>
    public static IReadOnlyList<Absorption> StarLines(double kelvin)
    {
        var (hydrogen, he, heIon, calcium, metals, sodium, molecules) = LineFamilies(kelvin);
        var lines = new List<Absorption>();
        void Add(double nm, double depth, double width, bool band = false)
        {
            if (depth > 0.02) lines.Add(new Absorption(nm, Math.Min(0.95, depth), width, band));
        }

        foreach (var line in HydrogenLines) Add(line.Nm, hydrogen * (0.55 + (0.35 * line.Strength)), 0.6 + (2.4 * hydrogen));
        foreach (var nm in new[] { 402.62, 447.15, 471.31, 492.19, 501.57, 587.56, 667.82 }) Add(nm, 0.5 * he, 0.6);
        foreach (var nm in new[] { 468.57, 541.15 }) Add(nm, 0.5 * heIon, 0.7);
        Add(393.37, 0.9 * calcium, 0.6 + (1.8 * calcium));
        Add(396.85, 0.85 * calcium, 0.6 + (1.6 * calcium));
        foreach (var nm in new[] { 430.8, 438.35, 495.76, 516.73, 517.27, 518.36, 527.0, 532.8 }) Add(nm, 0.45 * metals, 0.5);
        Add(588.995, 0.75 * sodium, 0.5 + sodium);
        Add(589.592, 0.65 * sodium, 0.45 + sodium);
        foreach (var nm in new[] { 476.2, 495.4, 516.7, 544.8, 586.2, 615.9, 662.6, 705.4 }) Add(nm, 0.6 * molecules, 14, band: true);
        return lines;
    }

    /// <summary>How much of the light at <paramref name="nm"/> gets through a set of dark lines, 0–1.</summary>
    public static double Transmission(IEnumerable<Absorption> lines, double nm, double shiftKms = 0)
    {
        var through = 1.0;
        foreach (var line in lines)
        {
            var centre = Shift(line.Nm, shiftKms);
            if (line.Band)
            {
                // A molecular band: sharp on its blue edge, fading out to the red.
                if (nm < centre || nm > centre + (line.Width * 3)) continue;
                through *= 1 - (line.Depth * Math.Exp(-(nm - centre) / line.Width));
            }
            else
            {
                var d = (nm - centre) / line.Width;
                if (Math.Abs(d) > 6) continue;
                through *= 1 - (line.Depth * Math.Exp(-0.5 * d * d));
            }
        }

        return through;
    }

    /// <summary>The wavelength seen from a source moving away at <paramref name="kms"/> (negative: coming closer), relativistic Doppler.</summary>
    public static double Shift(double nm, double kms)
    {
        if (kms == 0) return nm;
        var beta = kms / LightSpeed;
        return nm * Math.Sqrt((1 + beta) / (1 - beta));
    }

    /// <summary>The speed that shifts <paramref name="rest"/> to <paramref name="seen"/>, km/s (the inverse of <see cref="Shift"/>).</summary>
    public static double SpeedFromShift(double rest, double seen)
    {
        var r = (seen / rest) * (seen / rest);
        return LightSpeed * (r - 1) / (r + 1);
    }

    #endregion
}
