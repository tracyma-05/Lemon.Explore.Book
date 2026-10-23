using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.AcidsBases;

/// <summary>The acids and bases page's three views.</summary>
public enum AcidBaseMode
{
    /// <summary>A beaker of water, bottles of acid and base to drip in, and a magnifier on the particles in it.</summary>
    Particles,

    /// <summary>The pH scale with everyday things on it, and what each indicator turns in them.</summary>
    Scale,

    /// <summary>Neutralising an acid with a base from a burette, with its titration curve.</summary>
    Neutralize,
}

/// <summary>What the beaker of the particles view can be given.</summary>
public enum Reagent
{
    /// <summary>Hydrochloric acid, 0.1 mol/L: a strong acid, every molecule gives its proton away.</summary>
    StrongAcid,

    /// <summary>Acetic acid (vinegar's acid), 0.1 mol/L: a weak acid, only about one molecule in a hundred does.</summary>
    WeakAcid,

    /// <summary>Sodium hydroxide, 0.1 mol/L: a strong base, it brings its hydroxide ions with it.</summary>
    StrongBase,

    /// <summary>Ammonia, 0.1 mol/L: a weak base, a few of its molecules take a proton from water.</summary>
    WeakBase,

    /// <summary>Pure water.</summary>
    Water,
}

/// <summary>The dyes whose colour tells the pH.</summary>
public enum Indicator
{
    None,
    Universal,
    Cabbage,
    Litmus,
    Phenolphthalein,
    MethylOrange,
    Bromothymol,
}

/// <summary>The acid in the flask of the neutralising view.</summary>
public enum TitrationAcid
{
    /// <summary>Hydrochloric acid.</summary>
    Strong,

    /// <summary>Acetic acid.</summary>
    Weak,
}

/// <summary>
/// What is dissolved, as total amounts per litre (mol/L): a strong acid (all of it gives up its proton), a weak acid
/// (with its acid constant), a strong base (all of it hydroxide) and a weak base (with its base constant). A weak acid's
/// salt is the weak acid plus as much strong base, which is also how a buffer is made.
/// </summary>
public readonly record struct Solution(
    double StrongAcid,
    double WeakAcid,
    double StrongBase,
    double WeakBase,
    double AcidKa = AcidBaseModel.AceticKa,
    double BaseKb = AcidBaseModel.AmmoniaKb)
{
    public static Solution Water => new(0, 0, 0, 0);

    /// <summary>The same solution with <paramref name="factor"/> times as much water (every amount per litre divided by it).</summary>
    public Solution Diluted(double factor) =>
        this with { StrongAcid = StrongAcid / factor, WeakAcid = WeakAcid / factor, StrongBase = StrongBase / factor, WeakBase = WeakBase / factor };
}

/// <summary>Every particle's amount per litre (mol/L) in a solution, once water has taken and given its protons.</summary>
public readonly record struct Species(
    double Hydronium,
    double Hydroxide,
    double Chloride,
    double Sodium,
    double AcidMolecules,
    double AcidIons,
    double BaseMolecules,
    double BaseIons)
{
    /// <summary>pH = −log₁₀[H₃O⁺].</summary>
    public double Ph => -Math.Log10(Hydronium);
}

/// <summary>An everyday liquid on the pH scale, made as a simple solution that has its typical pH.</summary>
public sealed record Substance(string Id, double Ph, SubstanceKind Kind, Solution Recipe)
{
    /// <summary>The resource key of its name: <c>Acid_Sub_Lemon</c>.</summary>
    public string NameKey => "Acid_Sub_" + Id;
}

/// <summary>What makes a substance acidic or basic, which decides what watering it down does.</summary>
public enum SubstanceKind
{
    StrongAcid,
    WeakAcid,
    Neutral,
    Buffer,
    WeakBase,
    StrongBase,
}

/// <summary>
/// The chemistry of the acids and bases page, at 25 °C: the pH of any mix of strong and weak acids and bases (from the
/// charge balance, solved exactly), the particles in it, a titration of 25 mL of acid with sodium hydroxide, the heat it
/// gives off, the colours of six indicators, and everyday liquids on the pH scale.
/// </summary>
public static class AcidBaseModel
{
    /// <summary>Water's ion product at 25 °C: [H₃O⁺][OH⁻].</summary>
    public const double Kw = 1e-14;

    /// <summary>Acetic acid's acid constant (pKa 4.76).</summary>
    public const double AceticKa = 1.75e-5;

    /// <summary>Ammonia's base constant (pKb 4.74).</summary>
    public const double AmmoniaKb = 1.8e-5;

    /// <summary>Water's own amount per litre: 1000 g / 18.015 g/mol.</summary>
    public const double WaterMolar = 55.5;

    /// <summary>Strength of every bottle and of the titration's acid and base by default, mol/L.</summary>
    public const double BottleMolar = 0.1;

    /// <summary>One drop from a bottle or the burette, mL.</summary>
    public const double DropMl = 0.05;

    /// <summary>The acid in the titration flask, mL.</summary>
    public const double FlaskAcidMl = 25;

    /// <summary>What the burette holds, mL.</summary>
    public const double BuretteMl = 50;

    /// <summary>Heat given off per mole of H₃O⁺ + OH⁻ → 2 H₂O (strong acid), J.</summary>
    public const double NeutralizationHeat = 57_100;

    /// <summary>The same with acetic acid, a little less because the acid's molecules must first come apart, J.</summary>
    public const double WeakNeutralizationHeat = 55_900;

    /// <summary>Heat that warms one millilitre of the solution by a degree, J (water's 4.18 J/g·K, 1 g/mL).</summary>
    public const double HeatPerMl = 4.18;

    #region the pH of a solution

    /// <summary>
    /// [H₃O⁺] of <paramref name="s"/>: the root of the charge balance
    /// [H₃O⁺] + [Na⁺] + [BH⁺] = [OH⁻] + [Cl⁻] + [A⁻], which only rises with [H₃O⁺], found by bisection on its logarithm.
    /// </summary>
    public static double Hydronium(Solution s)
    {
        double Balance(double h) =>
            h + s.StrongBase + (s.WeakBase * h / (h + (Kw / s.BaseKb)))
            - (Kw / h) - s.StrongAcid - (s.WeakAcid * s.AcidKa / (s.AcidKa + h));

        double low = -16, high = 2;
        for (var i = 0; i < 80; i++)
        {
            var mid = (low + high) / 2;
            if (Balance(Math.Pow(10, mid)) > 0) high = mid;
            else low = mid;
        }

        return Math.Pow(10, (low + high) / 2);
    }

    public static double Ph(Solution s) => -Math.Log10(Hydronium(s));

    /// <summary>Every particle in <paramref name="s"/>.</summary>
    public static Species SpeciesOf(Solution s)
    {
        var h = Hydronium(s);
        var acidIons = s.WeakAcid * s.AcidKa / (s.AcidKa + h);
        var bh = Kw / s.BaseKb;
        var baseIons = s.WeakBase * h / (h + bh);
        return new Species(h, Kw / h, s.StrongAcid, s.StrongBase, s.WeakAcid - acidIons, acidIons, s.WeakBase - baseIons, baseIons);
    }

    /// <summary>"Acidic" below 6.95, "Basic" above 7.05, else "Neutral".</summary>
    public static string Nature(double ph) => ph < 6.95 ? "Acidic" : ph > 7.05 ? "Basic" : "Neutral";

    #endregion

    #region the beaker of the particles view

    /// <summary>A beaker's contents as amounts (mmol) and its volume (mL).</summary>
    public readonly record struct Beaker(double StrongAcid, double WeakAcid, double StrongBase, double WeakBase, double VolumeMl)
    {
        public static Beaker Water(double ml = 100) => new(0, 0, 0, 0, ml);

        public Solution Solution => new(StrongAcid / VolumeMl, WeakAcid / VolumeMl, StrongBase / VolumeMl, WeakBase / VolumeMl);

        /// <summary><paramref name="ml"/> of a 0.1 mol/L bottle (or water) poured in.</summary>
        public Beaker Add(Reagent reagent, double ml)
        {
            var mmol = ml * BottleMolar;
            return reagent switch
            {
                Reagent.StrongAcid => this with { StrongAcid = StrongAcid + mmol, VolumeMl = VolumeMl + ml },
                Reagent.WeakAcid => this with { WeakAcid = WeakAcid + mmol, VolumeMl = VolumeMl + ml },
                Reagent.StrongBase => this with { StrongBase = StrongBase + mmol, VolumeMl = VolumeMl + ml },
                Reagent.WeakBase => this with { WeakBase = WeakBase + mmol, VolumeMl = VolumeMl + ml },
                _ => this with { VolumeMl = VolumeMl + ml },
            };
        }

        /// <summary>A tenth kept and topped up with water to the same volume: ten times weaker.</summary>
        public Beaker DilutedTenTimes() => new(StrongAcid / 10, WeakAcid / 10, StrongBase / 10, WeakBase / 10, VolumeMl);
    }

    #endregion

    #region titration

    /// <summary>The flask after <paramref name="baseMl"/> of sodium hydroxide went into 25 mL of acid, both at <paramref name="molar"/>.</summary>
    public static Solution Titration(TitrationAcid acid, double baseMl, double molar = BottleMolar)
    {
        var volume = FlaskAcidMl + baseMl;
        var acidAmount = FlaskAcidMl * molar / volume;
        var baseAmount = baseMl * molar / volume;
        return acid == TitrationAcid.Strong
            ? new Solution(acidAmount, 0, baseAmount, 0)
            : new Solution(0, acidAmount, baseAmount, 0);
    }

    public static double TitrationPh(TitrationAcid acid, double baseMl, double molar = BottleMolar) => Ph(Titration(acid, baseMl, molar));

    /// <summary>The base that exactly matches the acid, mL: as many OH⁻ as the acid had protons.</summary>
    public const double EquivalenceMl = FlaskAcidMl;

    /// <summary>
    /// How much warmer the flask is after <paramref name="baseMl"/>: the heat of the acid and base that met, spread through
    /// all the liquid (no heat lost, the burette's base at the flask's temperature).
    /// </summary>
    public static double TemperatureRise(TitrationAcid acid, double baseMl, double molar = BottleMolar)
    {
        var reacted = Math.Min(baseMl, FlaskAcidMl) * molar / 1000;
        var heat = reacted * (acid == TitrationAcid.Strong ? NeutralizationHeat : WeakNeutralizationHeat);
        return heat / ((FlaskAcidMl + baseMl) * HeatPerMl);
    }

    /// <summary>
    /// Where the titration is: "Start" (no base yet), "Before" (acid left), "Half" (the weak acid half used up, where
    /// pH = pKa), "Equivalence" (within half a millilitre of it), "Excess" (base left over).
    /// </summary>
    public static string TitrationStage(TitrationAcid acid, double baseMl)
    {
        if (baseMl < DropMl / 2) return "Start";
        if (Math.Abs(baseMl - EquivalenceMl) <= 0.5) return "Equivalence";
        if (baseMl > EquivalenceMl) return "Excess";
        return acid == TitrationAcid.Weak && Math.Abs(baseMl - (EquivalenceMl / 2)) <= 1 ? "Half" : "Before";
    }

    #endregion

    #region indicators

    /// <summary>Colour stops (pH, colour) of each indicator; between stops the colour is blended, alpha included.</summary>
    private static readonly Dictionary<Indicator, (double Ph, Color Color)[]> Stops = new()
    {
        [Indicator.Universal] =
        [
            (0, Rgb(0xC8, 0x10, 0x24)), (1, Rgb(0xDC, 0x1E, 0x22)), (2, Rgb(0xEA, 0x3C, 0x1E)), (3, Rgb(0xF2, 0x66, 0x1C)),
            (4, Rgb(0xF6, 0x96, 0x1A)), (5, Rgb(0xF4, 0xC4, 0x1C)), (6, Rgb(0xD2, 0xD6, 0x28)), (7, Rgb(0x66, 0xBE, 0x3A)),
            (8, Rgb(0x2C, 0xA4, 0x6E)), (9, Rgb(0x20, 0x88, 0xB0)), (10, Rgb(0x2A, 0x58, 0xC0)), (11, Rgb(0x3E, 0x3A, 0xA8)),
            (12, Rgb(0x58, 0x2C, 0x9A)), (13, Rgb(0x68, 0x22, 0x88)), (14, Rgb(0x58, 0x18, 0x70)),
        ],
        [Indicator.Cabbage] =
        [
            (0, Rgb(0xD8, 0x14, 0x46)), (2, Rgb(0xE0, 0x24, 0x5E)), (4, Rgb(0xC8, 0x40, 0x92)), (6, Rgb(0x96, 0x48, 0xAC)),
            (7, Rgb(0x7A, 0x4E, 0xB0)), (8, Rgb(0x48, 0x68, 0xCE)), (9.5, Rgb(0x26, 0x98, 0xB0)), (11, Rgb(0x3A, 0xAE, 0x58)),
            (12.5, Rgb(0x9A, 0xC8, 0x30)), (14, Rgb(0xE6, 0xD6, 0x2E)),
        ],
        [Indicator.Litmus] =
        [
            (0, Rgb(0xD4, 0x2C, 0x3E)), (4.5, Rgb(0xD4, 0x2C, 0x3E)), (6.4, Rgb(0x8A, 0x46, 0xA6)), (8.3, Rgb(0x2C, 0x5A, 0xC8)),
            (14, Rgb(0x2C, 0x5A, 0xC8)),
        ],
        [Indicator.Phenolphthalein] =
        [
            (0, Clear), (8.2, Clear), (9, Color.FromArgb(150, 0xF2, 0x7C, 0xC4)), (10, Rgb(0xE0, 0x24, 0x96)),
            (12, Rgb(0xD8, 0x1E, 0x90)), (13.2, Color.FromArgb(120, 0xE8, 0x80, 0xC0)), (14, Clear),
        ],
        [Indicator.MethylOrange] =
        [
            (0, Rgb(0xD6, 0x24, 0x28)), (3.1, Rgb(0xD6, 0x24, 0x28)), (3.8, Rgb(0xEE, 0x78, 0x1E)), (4.4, Rgb(0xF0, 0xC4, 0x28)),
            (14, Rgb(0xF0, 0xC4, 0x28)),
        ],
        [Indicator.Bromothymol] =
        [
            (0, Rgb(0xE6, 0xCE, 0x2E)), (6.0, Rgb(0xE6, 0xCE, 0x2E)), (6.9, Rgb(0x56, 0xAA, 0x48)), (7.6, Rgb(0x28, 0x5E, 0xC0)),
            (14, Rgb(0x28, 0x5E, 0xC0)),
        ],
    };

    /// <summary>Clear water's faint tint, for no indicator and for an indicator with no colour at that pH.</summary>
    public static readonly Color Clear = Color.FromArgb(0, 0xB8, 0xDC, 0xF4);

    /// <summary>The indicator's colour at <paramref name="ph"/>; alpha 0 where it has none (phenolphthalein in acid).</summary>
    public static Color IndicatorColor(Indicator indicator, double ph)
    {
        if (!Stops.TryGetValue(indicator, out var stops)) return Clear;
        ph = Math.Clamp(ph, 0, 14);
        for (var i = 1; i < stops.Length; i++)
        {
            if (ph > stops[i].Ph) continue;
            var (p0, c0) = stops[i - 1];
            var (p1, c1) = stops[i];
            var t = p1 > p0 ? (ph - p0) / (p1 - p0) : 1;
            return Blend(c0, c1, t);
        }

        return stops[^1].Color;
    }

    /// <summary>The pH range over which an indicator changes colour (where a titration's end shows), or null for a ladder of colours.</summary>
    public static (double Low, double High)? ChangeRange(Indicator indicator) => indicator switch
    {
        Indicator.Litmus => (4.5, 8.3),
        Indicator.Phenolphthalein => (8.2, 10.0),
        Indicator.MethylOrange => (3.1, 4.4),
        Indicator.Bromothymol => (6.0, 7.6),
        _ => null,
    };

    /// <summary>The colour a solution shows, as a fill: the indicator's colour over water, alpha at least <paramref name="minAlpha"/>.</summary>
    public static Color LiquidColor(Indicator indicator, double ph, byte minAlpha = 70)
    {
        var dye = IndicatorColor(indicator, ph);
        var water = Color.FromArgb(minAlpha, 0xB8, 0xDC, 0xF4);
        return Blend(water, Color.FromArgb(255, dye.R, dye.G, dye.B), dye.A / 255.0 * 0.92);
    }

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    private static Color Blend(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        byte L(byte x, byte y) => (byte)Math.Round(x + ((y - x) * t));
        return Color.FromArgb(L(a.A, b.A), L(a.R, b.R), L(a.G, b.G), L(a.B, b.B));
    }

    #endregion

    #region everyday things

    /// <summary>
    /// Everyday liquids at their typical pH, lowest first. Each is a simple solution of the kind that makes it acidic or
    /// basic (citric acid for lemon juice, a bicarbonate buffer for blood, sodium hydroxide for drain cleaner …), its
    /// strength chosen so it has that pH; watering it down then moves its pH the way the real thing's would, roughly.
    /// </summary>
    public static IReadOnlyList<Substance> Substances { get; } =
    [
        Make("Stomach", 1.5, SubstanceKind.StrongAcid),
        Make("Lemon", 2.3, SubstanceKind.WeakAcid, 7.4e-4),
        Make("Cola", 2.5, SubstanceKind.WeakAcid, 7.1e-3),
        Make("Vinegar", 2.9, SubstanceKind.WeakAcid, AceticKa),
        Make("Orange", 3.5, SubstanceKind.WeakAcid, 7.4e-4),
        Make("Coffee", 5.0, SubstanceKind.WeakAcid, 4e-4),
        Make("Rain", 5.6, SubstanceKind.WeakAcid, 4.3e-7),
        Make("Milk", 6.6, SubstanceKind.Buffer, 1.6e-7),
        Make("Water", 7.0, SubstanceKind.Neutral),
        Make("Blood", 7.4, SubstanceKind.Buffer, 7.9e-7),
        Make("Seawater", 8.1, SubstanceKind.Buffer, 1.3e-9),
        Make("BakingSoda", 8.3, SubstanceKind.Buffer, 5e-9),
        Make("Soap", 10.0, SubstanceKind.WeakBase, 1e-5),
        Make("Antacid", 10.5, SubstanceKind.StrongBase),
        Make("Ammonia", 11.6, SubstanceKind.WeakBase, AmmoniaKb),
        Make("Bleach", 12.5, SubstanceKind.StrongBase),
        Make("Drain", 13.5, SubstanceKind.StrongBase),
    ];

    public static Substance Substance(string? id) => Substances.FirstOrDefault(s => s.Id == id) ?? Substances.First(s => s.Id == "Water");

    /// <summary>A substance of <paramref name="kind"/> whose strength is chosen (bisection on its logarithm) to give <paramref name="ph"/>.</summary>
    private static Substance Make(string id, double ph, SubstanceKind kind, double constant = 0)
    {
        Solution Recipe(double c) => kind switch
        {
            SubstanceKind.StrongAcid => new Solution(c, 0, 0, 0),
            SubstanceKind.WeakAcid => new Solution(0, c, 0, 0, AcidKa: constant),

            // A buffer: 0.05 mol/L of a weak acid, c of it turned into its salt by a strong base.
            SubstanceKind.Buffer => new Solution(0, 0.05, c, 0, AcidKa: constant),
            SubstanceKind.WeakBase => new Solution(0, 0, 0, c, BaseKb: constant),
            SubstanceKind.StrongBase => new Solution(0, 0, c, 0),
            _ => Solution.Water,
        };

        if (kind == SubstanceKind.Neutral) return new Substance(id, ph, kind, Solution.Water);

        // pH falls as an acid's strength rises and rises with a base's (or a buffer's salt).
        var rising = kind is SubstanceKind.Buffer or SubstanceKind.WeakBase or SubstanceKind.StrongBase;
        double low = -12, high = kind == SubstanceKind.Buffer ? Math.Log10(0.05) : 1.5;
        for (var i = 0; i < 80; i++)
        {
            var mid = (low + high) / 2;
            var tooHigh = Ph(Recipe(Math.Pow(10, mid))) > ph;
            if (tooHigh == rising) high = mid;
            else low = mid;
        }

        return new Substance(id, ph, kind, Recipe(Math.Pow(10, (low + high) / 2)));
    }

    /// <summary>The pH of <paramref name="substance"/> watered down <paramref name="times"/> times, ten-fold each time.</summary>
    public static double DilutedPh(Substance substance, int times) => Ph(substance.Recipe.Diluted(Math.Pow(10, times)));

    #endregion
}
