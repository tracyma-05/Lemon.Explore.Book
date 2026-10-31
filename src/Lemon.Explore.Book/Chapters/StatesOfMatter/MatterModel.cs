namespace Lemon.Explore.Book.Chapters.StatesOfMatter;

/// <summary>The states of matter page's three views.</summary>
public enum MatterMode
{
    /// <summary>A cylinder of molecules under a piston, warmed or cooled from below and squeezed from above.</summary>
    Particles,

    /// <summary>Ice (or dry ice) heated steadily, with its heating curve: the temperature stops while it melts and boils.</summary>
    Heating,

    /// <summary>The phase diagram: which state wins at each temperature and pressure.</summary>
    Diagram,
}

/// <summary>The two substances on the page.</summary>
public enum MatterSubstance
{
    Water,

    /// <summary>Carbon dioxide: at ordinary pressure its solid (dry ice) turns straight into gas.</summary>
    CarbonDioxide,
}

/// <summary>The state a substance is in at a temperature and pressure.</summary>
public enum MatterState
{
    Solid,
    Liquid,
    Gas,

    /// <summary>Beyond the critical point: neither liquid nor gas, a fluid with no surface.</summary>
    Supercritical,
}

/// <summary>How the burner under the beaker is set.</summary>
public enum HeatSetting
{
    Off,
    Low,
    High,
}

/// <summary>Where the heating happens: the air pressure sets the boiling point.</summary>
public enum HeatingPlace
{
    SeaLevel,

    /// <summary>Lhasa, 3,650 m up.</summary>
    Lhasa,

    /// <summary>The top of Everest, 8,849 m up.</summary>
    Everest,

    /// <summary>A pressure cooker, about two atmospheres.</summary>
    Cooker,
}

/// <summary>
/// The physics of the states of matter page: each substance's phase boundaries (melting, boiling and sublimation
/// curves, triple and critical points), the state at a temperature and pressure, and a heating curve at constant
/// pressure for 100 g of it. Temperatures are kelvin and pressures pascals unless a name says otherwise.
/// </summary>
/// <remarks>
/// Water's boiling curve is interpolated in (1/T, ln P) through steam-table points, so 100 °C at one atmosphere, about
/// 70 °C on Everest and 120 °C in a pressure cooker come out right. The sublimation curves follow Clausius–Clapeyron
/// from the triple point; the melting lines are straight (water's leans back: squeezing ice melts it a little).
/// Carbon dioxide's numbers are rounded; past about 100 MPa other kinds of ice appear, which the page leaves out.
/// </remarks>
public static class MatterModel
{
    public const double Atmosphere = 101_325;

    public const double ZeroCelsius = 273.15;

    /// <summary>The pressures the diagrams show: 1 Pa to 100 MPa.</summary>
    public const double MinPressure = 1;

    public const double MaxPressure = 1e8;

    /// <summary>Grams heated in the heating view.</summary>
    public const double Grams = 100;

    /// <summary>One substance's constants.</summary>
    public sealed record Substance(
        MatterSubstance Id,
        double TripleT,
        double TripleP,
        double CriticalT,
        double CriticalP,
        double SublimationSlope,
        double MeltingSlope,
        (double T, double P)[] Boiling,
        double MinCelsius,
        double MaxCelsius,
        double SolidHeat,
        double FusionHeat,
        double LiquidHeat,
        double VaporizationHeat,
        double SublimationHeat,
        double GasHeat,
        double StartCelsius,
        double EndCelsius)
    {
        public double MinT => MinCelsius + ZeroCelsius;

        public double MaxT => MaxCelsius + ZeroCelsius;
    }

    /// <summary>
    /// Water. Triple point 0.01 °C at 611.657 Pa, critical point 373.946 °C at 22.064 MPa; the ice line falls 13.5 MPa
    /// per kelvin; heats in J/(g·K) and J/g.
    /// </summary>
    public static readonly Substance Water = new(
        MatterSubstance.Water,
        TripleT: 273.16, TripleP: 611.657,
        CriticalT: 647.096, CriticalP: 22.064e6,
        SublimationSlope: 6141,
        MeltingSlope: -13.5e6,
        Boiling:
        [
            (273.16, 611.657), (283.15, 1_228.1), (293.15, 2_339.3), (303.15, 4_246.9), (313.15, 7_384.9),
            (323.15, 12_352), (333.15, 19_946), (343.15, 31_201), (353.15, 47_414), (363.15, 70_182),
            (373.124, 101_325), (383.15, 143_380), (393.15, 198_670), (413.15, 361_540), (433.15, 618_230),
            (453.15, 1_002_800), (473.15, 1_554_900), (498.15, 2_550_800), (523.15, 3_976_200), (548.15, 5_942_000),
            (573.15, 8_587_900), (598.15, 12_046_000), (623.15, 16_529_000), (647.096, 22_064_000),
        ],
        MinCelsius: -80, MaxCelsius: 420,
        SolidHeat: 2.09, FusionHeat: 334, LiquidHeat: 4.18, VaporizationHeat: 2257, SublimationHeat: 2838, GasHeat: 2.01,
        StartCelsius: -40, EndCelsius: 150);

    /// <summary>
    /// Carbon dioxide. Triple point −56.6 °C at 518 kPa (about five atmospheres), so at one atmosphere dry ice turns
    /// straight to gas at −78.5 °C; critical point 31.0 °C at 7.38 MPa.
    /// </summary>
    public static readonly Substance CarbonDioxide = new(
        MatterSubstance.CarbonDioxide,
        TripleT: 216.58, TripleP: 518_000,
        CriticalT: 304.13, CriticalP: 7.3773e6,
        SublimationSlope: 3144,
        MeltingSlope: 5e6,
        Boiling: [(216.58, 518_000), (233.15, 1_005_000), (253.15, 1_969_000), (273.15, 3_485_000), (293.15, 5_729_000), (304.13, 7_377_300)],
        MinCelsius: -130, MaxCelsius: 60,
        SolidHeat: 1.0, FusionHeat: 196, LiquidHeat: 2.0, VaporizationHeat: 350, SublimationHeat: 571, GasHeat: 0.75,
        StartCelsius: -120, EndCelsius: 20);

    public static Substance Of(MatterSubstance id) => id == MatterSubstance.CarbonDioxide ? CarbonDioxide : Water;

    #region the phase boundaries

    /// <summary>
    /// The vapour pressure over the liquid at <paramref name="t"/> (the boiling curve), from the triple point to the
    /// critical point; beyond either end it carries on the nearest piece's slope.
    /// </summary>
    public static double BoilingPressure(Substance s, double t)
    {
        var points = s.Boiling;
        var i = 0;
        while (i < points.Length - 2 && t > points[i + 1].T) i++;
        var (t0, p0) = points[i];
        var (t1, p1) = points[i + 1];
        var f = ((1 / t) - (1 / t0)) / ((1 / t1) - (1 / t0));
        return Math.Exp(Math.Log(p0) + (f * (Math.Log(p1) - Math.Log(p0))));
    }

    /// <summary>The boiling temperature at <paramref name="p"/> (the inverse of <see cref="BoilingPressure"/>).</summary>
    public static double BoilingTemperature(Substance s, double p)
    {
        var points = s.Boiling;
        var i = 0;
        while (i < points.Length - 2 && p > points[i + 1].P) i++;
        var (t0, p0) = points[i];
        var (t1, p1) = points[i + 1];
        var f = (Math.Log(p) - Math.Log(p0)) / (Math.Log(p1) - Math.Log(p0));
        return 1 / ((1 / t0) + (f * ((1 / t1) - (1 / t0))));
    }

    /// <summary>The vapour pressure over the solid at <paramref name="t"/> (the sublimation curve), below the triple point.</summary>
    public static double SublimationPressure(Substance s, double t) =>
        s.TripleP * Math.Exp(-s.SublimationSlope * ((1 / t) - (1 / s.TripleT)));

    /// <summary>The temperature where the solid turns straight into gas at <paramref name="p"/> (below the triple point's pressure).</summary>
    public static double SublimationTemperature(Substance s, double p) =>
        1 / ((1 / s.TripleT) - (Math.Log(p / s.TripleP) / s.SublimationSlope));

    /// <summary>The melting temperature at <paramref name="p"/> (above the triple point's pressure).</summary>
    public static double MeltingTemperature(Substance s, double p) => s.TripleT + ((p - s.TripleP) / s.MeltingSlope);

    /// <summary>The state that wins at <paramref name="t"/> and <paramref name="p"/>.</summary>
    public static MatterState StateAt(Substance s, double t, double p)
    {
        if (p < s.TripleP) return t < SublimationTemperature(s, p) ? MatterState.Solid : MatterState.Gas;
        if (t < MeltingTemperature(s, p)) return MatterState.Solid;
        if (t >= s.CriticalT) return p >= s.CriticalP ? MatterState.Supercritical : MatterState.Gas;
        if (p >= s.CriticalP) return MatterState.Liquid;
        return p >= BoilingPressure(s, t) ? MatterState.Liquid : MatterState.Gas;
    }

    /// <summary>The temperature where the solid stops being solid at <paramref name="p"/>: it melts, or below the triple point sublimes.</summary>
    public static double SolidLimit(Substance s, double p) =>
        p < s.TripleP ? SublimationTemperature(s, p) : MeltingTemperature(s, p);

    #endregion

    #region places

    /// <summary>The air pressure at a place, Pa.</summary>
    public static double PressureAt(HeatingPlace place) => place switch
    {
        HeatingPlace.Lhasa => PressureAtHeight(3650),
        HeatingPlace.Everest => PressureAtHeight(8849),
        HeatingPlace.Cooker => 198_670,
        _ => Atmosphere,
    };

    /// <summary>The standard atmosphere's pressure <paramref name="metres"/> above sea level (good to 11 km).</summary>
    public static double PressureAtHeight(double metres) => Atmosphere * Math.Pow(1 - (2.25577e-5 * metres), 5.25588);

    /// <summary>Metres above sea level where the standard atmosphere has <paramref name="pressure"/>.</summary>
    public static double HeightAtPressure(double pressure) => (1 - Math.Pow(pressure / Atmosphere, 1 / 5.25588)) / 2.25577e-5;

    #endregion

    #region the heating curve

    /// <summary>One stretch of a heating curve: warming one state, or turning one into another at a fixed temperature.</summary>
    public sealed record Stretch(string Kind, MatterState From, MatterState To, double StartKj, double EndKj, double StartC, double EndC)
    {
        public bool IsChange => From != To;

        public double Kj => EndKj - StartKj;
    }

    /// <summary>
    /// The heating curve of <see cref="Grams"/> of <paramref name="s"/> at <paramref name="p"/> from its start to its end
    /// temperature: warming solid, melting, warming liquid, boiling, warming gas; or below the triple point's pressure,
    /// warming solid, subliming, warming gas. Kinds name the stretches: <c>WarmSolid</c>, <c>Melt</c>, <c>WarmLiquid</c>,
    /// <c>Boil</c>, <c>Sublime</c>, <c>WarmGas</c>.
    /// </summary>
    public static IReadOnlyList<Stretch> HeatingCurve(Substance s, double p)
    {
        var stretches = new List<Stretch>();
        var kj = 0.0;
        var c = s.StartCelsius;

        void Warm(string kind, MatterState state, double perGramKelvin, double toC)
        {
            var heat = Grams * perGramKelvin * (toC - c) / 1000;
            stretches.Add(new Stretch(kind, state, state, kj, kj + heat, c, toC));
            kj += heat;
            c = toC;
        }

        void Change(string kind, MatterState from, MatterState to, double perGram)
        {
            var heat = Grams * perGram / 1000;
            stretches.Add(new Stretch(kind, from, to, kj, kj + heat, c, c));
            kj += heat;
        }

        if (p < s.TripleP)
        {
            var sublime = SublimationTemperature(s, p) - ZeroCelsius;
            Warm("WarmSolid", MatterState.Solid, s.SolidHeat, sublime);
            Change("Sublime", MatterState.Solid, MatterState.Gas, s.SublimationHeat);
            Warm("WarmGas", MatterState.Gas, s.GasHeat, Math.Max(s.EndCelsius, sublime + 20));
            return stretches;
        }

        var melt = MeltingTemperature(s, p) - ZeroCelsius;
        var boil = BoilingTemperature(s, p) - ZeroCelsius;
        Warm("WarmSolid", MatterState.Solid, s.SolidHeat, melt);
        Change("Melt", MatterState.Solid, MatterState.Liquid, s.FusionHeat);
        Warm("WarmLiquid", MatterState.Liquid, s.LiquidHeat, boil);
        Change("Boil", MatterState.Liquid, MatterState.Gas, s.VaporizationHeat);
        Warm("WarmGas", MatterState.Gas, s.GasHeat, Math.Max(s.EndCelsius, boil + 20));
        return stretches;
    }

    /// <summary>All the heat the curve takes, kJ.</summary>
    public static double TotalKj(IReadOnlyList<Stretch> curve) => curve[^1].EndKj;

    /// <summary>The stretch the heat <paramref name="kj"/> is in.</summary>
    public static Stretch StretchAt(IReadOnlyList<Stretch> curve, double kj)
    {
        foreach (var stretch in curve)
        {
            if (kj < stretch.EndKj) return stretch;
        }

        return curve[^1];
    }

    /// <summary>The temperature after <paramref name="kj"/> of heat, °C.</summary>
    public static double CelsiusAt(IReadOnlyList<Stretch> curve, double kj)
    {
        var stretch = StretchAt(curve, kj);
        if (stretch.IsChange || stretch.Kj <= 0) return stretch.StartC;
        var f = Math.Clamp((kj - stretch.StartKj) / stretch.Kj, 0, 1);
        return stretch.StartC + (f * (stretch.EndC - stretch.StartC));
    }

    /// <summary>
    /// How much of the sample is solid, liquid and gas after <paramref name="kj"/> of heat: during a change the new
    /// state grows as the heat goes in.
    /// </summary>
    public static (double Solid, double Liquid, double Gas) FractionsAt(IReadOnlyList<Stretch> curve, double kj)
    {
        var stretch = StretchAt(curve, kj);
        var f = stretch.IsChange ? Math.Clamp((kj - stretch.StartKj) / stretch.Kj, 0, 1) : 0;
        double Share(MatterState state) => (stretch.From == state ? 1 - f : 0) + (stretch.To == state && stretch.IsChange ? f : 0);
        return stretch.IsChange
            ? (Share(MatterState.Solid), Share(MatterState.Liquid), Share(MatterState.Gas))
            : stretch.From switch
            {
                MatterState.Solid => (1, 0, 0),
                MatterState.Liquid => (0, 1, 0),
                _ => (0, 0, 1),
            };
    }

    /// <summary>A burner's power, kW: the low flame and the high one.</summary>
    public static double BurnerKw(HeatSetting heat) => heat switch
    {
        HeatSetting.Low => 1.5,
        HeatSetting.High => 6,
        _ => 0,
    };

    #endregion

    /// <summary>
    /// A gas's volume relative to the cylinder at its fullest, for the piston: an ideal gas, V ∝ T / P, squeezed into the
    /// picture on a log scale (0 smallest, 1 largest).
    /// </summary>
    public static double GasVolume(double t, double p)
    {
        var v = Math.Log10(t / p);
        return Math.Clamp((v - Math.Log10(200 / 2e7)) / (Math.Log10(700 / 30.0) - Math.Log10(200 / 2e7)), 0, 1);
    }
}
