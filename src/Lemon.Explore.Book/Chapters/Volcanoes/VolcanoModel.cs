namespace Lemon.Explore.Book.Chapters.Volcanoes;

/// <summary>The views of the volcanoes and rocks page.</summary>
public enum VolcanoMode
{
    /// <summary>A volcano in cross-section: the magma chamber, gas coming out of the magma as it rises, and the eruption.</summary>
    Eruption,

    /// <summary>A landscape from the sea to a volcano with the rock cycle running through it, and a grain to follow.</summary>
    Cycle,

    /// <summary>A magnifier on rock being made: magma crystallising, mud settling and hardening, rock squeezed and heated.</summary>
    Magnifier,
}

/// <summary>How a volcano erupts, from the runniest, least gassy magma to the stickiest, gassiest.</summary>
public enum EruptionStyle
{
    /// <summary>Runny basalt with little gas: lava fountains and long lava flows (Kīlauea).</summary>
    Hawaiian,

    /// <summary>Runny magma with more gas: big bubbles bursting at the top, throwing out lumps of lava (Stromboli).</summary>
    Strombolian,

    /// <summary>Sticky magma with little gas: it squeezes out as a lava dome that can crumble into glowing avalanches (Merapi).</summary>
    Dome,

    /// <summary>Sticky magma with some gas: short cannon-like blasts of ash and lava bombs.</summary>
    Vulcanian,

    /// <summary>Sticky magma with a lot of gas: a column of ash tens of kilometres high (Pinatubo).</summary>
    Plinian,
}

/// <summary>The shape a volcano builds.</summary>
public enum ConeShape
{
    /// <summary>A wide, gentle shield of thin lava flows.</summary>
    Shield,

    /// <summary>A steep cone of layers of lava and ash.</summary>
    Strato,

    /// <summary>A cone whose top has fallen into the emptied chamber, leaving a caldera with a dome in it.</summary>
    Caldera,
}

/// <summary>Where a volcano is in its round of filling up and erupting.</summary>
public enum EruptionPhase
{
    /// <summary>Magma flows into the chamber from below: the pressure rises, the ground swells, small earthquakes.</summary>
    Recharge,

    /// <summary>Magma forces its way up the conduit; bubbles appear as it rises.</summary>
    Rising,

    /// <summary>The eruption.</summary>
    Erupting,

    /// <summary>The eruption has stopped; gas still steams from the vent and the new layer cools.</summary>
    Calm,
}

/// <summary>What the magnifier view is watching.</summary>
public enum MagnifierProcess
{
    /// <summary>Magma cooling and crystallising: igneous rock.</summary>
    Cooling,

    /// <summary>Grains settling out of water, then buried and cemented: sedimentary rock.</summary>
    Settling,

    /// <summary>Rock buried deep, heated and squeezed: metamorphic rock.</summary>
    Squeezing,
}

/// <summary>The three families of rock.</summary>
public enum RockFamily
{
    Igneous,
    Sedimentary,
    Metamorphic,
}

/// <summary>The steps of the rock cycle, as the labels in the landscape name them.</summary>
public enum RockProcess
{
    /// <summary>Where a followed grain starts.</summary>
    Start,

    /// <summary>Rock broken and rotted at the surface by frost, rain, plants and air.</summary>
    Weathering,

    /// <summary>The bits carried away by rivers, wind and ice.</summary>
    Erosion,

    /// <summary>The bits laid down where the water slows, in layers.</summary>
    Deposition,

    /// <summary>Buried layers pressed together and glued by minerals into rock.</summary>
    Lithification,

    /// <summary>Rock pushed up into mountains when plates collide.</summary>
    Uplift,

    /// <summary>Rock changed by heat and pressure deep down without melting.</summary>
    Metamorphism,

    /// <summary>Rock melted into magma.</summary>
    Melting,

    /// <summary>Magma cooling into crystals, deep down or at the surface.</summary>
    Crystallization,

    /// <summary>Magma reaching the surface as lava and ash.</summary>
    Eruption,
}

/// <summary>A real volcano and its magma: silica (% by weight) and dissolved water (% by weight) before it rises.</summary>
public sealed record VolcanoPreset(string Id, double Silica, double Water);

/// <summary>A stop on a followed grain's journey: where it goes, by which process, what it is then and how long the step takes.</summary>
public sealed record JourneyStep(string Place, RockProcess Process, string State, double Years);

/// <summary>A grain to follow round the rock cycle.</summary>
public sealed record Journey(string Id, IReadOnlyList<JourneyStep> Steps)
{
    /// <summary>Years from the start to the end of step <paramref name="index"/>.</summary>
    public double YearsTo(int index) => Steps.Take(Math.Clamp(index + 1, 0, Steps.Count)).Sum(s => s.Years);
}

/// <summary>
/// The science of the volcanoes and rocks page. Magma: its temperature and stickiness from its silica, how much water it can
/// hold at each depth, the share of its volume that is gas as it rises, the depth where the froth tears apart, and the style
/// of eruption. The rock cycle: the journeys a grain can make. Sediment: how fast grains settle and how burial squeezes the
/// water out. Metamorphism: the heat and pressure at a depth and the rock they make.
/// </summary>
/// <remarks>
/// Typical values, simplified for teaching: viscosity is a Vogel–Fulcher–Tammann curve fitted to three typical magmas at
/// their usual temperatures (basalt 1200 °C ≈ 10² Pa·s, andesite ≈ 10⁴·³, rhyolite 800 °C ≈ 10⁷·⁵); water solubility is
/// Henry's-law-like, s·√P (rhyolite ≈ 5.8 % at 200 MPa); gas is an ideal gas that stays with the magma (closed system);
/// the froth fragments at 75 % gas (Sparks 1978). Column heights from Mastin et al. 2009, H = 2.00·V̇^0.241 km. Settling from
/// Ferguson &amp; Church 2004; porosity from Sclater &amp; Christie 1980.
/// </remarks>
public static class VolcanoModel
{
    /// <summary>Silica of the slider's ends, % by weight.</summary>
    public const double MinSilica = 48;

    public const double MaxSilica = 76;

    /// <summary>Dissolved water of the slider's ends, % by weight.</summary>
    public const double MinWater = 0.2;

    public const double MaxWater = 7;

    /// <summary>Density of the crust above the magma, kg/m³ (sets the pressure at a depth).</summary>
    public const double CrustDensity = 2600;

    /// <summary>Density of the melt, kg/m³.</summary>
    public const double MeltDensity = 2500;

    /// <summary>The gas share of the volume at which a froth of bubbles tears apart into ash and gas.</summary>
    public const double FragmentationFraction = 0.75;

    private const double G = 9.81;
    private const double GasConstant = 8.314;
    private const double WaterMolarMass = 0.018;

    public static readonly IReadOnlyList<VolcanoPreset> Presets =
    [
        new("Kilauea", 50, 0.5),
        new("Wudalianchi", 50.5, 1.5),
        new("Stromboli", 51, 2.0),
        new("Merapi", 56, 1.2),
        new("StHelens", 63, 4.6),
        new("Pinatubo", 64, 6.0),
        new("Changbaishan", 72, 5.0),
    ];

    public static VolcanoPreset Preset(string id) => Presets.FirstOrDefault(p => p.Id == id) ?? Presets[0];

    #region magma

    /// <summary>The usual eruption temperature of a magma, °C: hot basalt, cooler rhyolite.</summary>
    public static double Temperature(double silica) => 1200 - ((silica - 50) * 400 / 22);

    /// <summary>log₁₀ of the viscosity, Pa·s, at the magma's own temperature (water 10⁻³, honey 10¹, peanut butter 10²·⁵).</summary>
    public static double LogViscosity(double silica)
    {
        var (b, c) = VftConstants(silica);
        var kelvin = Temperature(silica) + 273.15;
        return -4.55 + (b / (kelvin - c));
    }

    /// <summary>The fitted B and C (K) of log η = A + B / (T − C), piecewise linear in silica between basalt, andesite and rhyolite.</summary>
    private static (double B, double C) VftConstants(double silica)
    {
        if (silica <= 60)
        {
            var t = Math.Clamp((silica - 50) / 10, 0, 1);
            return (5718 + (t * (6543 - 5718)), 600 - (t * 50));
        }

        var u = Math.Clamp((silica - 60) / 12, 0, 1);
        return (6543 + (u * (6905 - 6543)), 550 - (u * 50));
    }

    /// <summary>The rock the magma makes, by silica: basalt, basaltic andesite, andesite, dacite or rhyolite.</summary>
    public static string RockName(double silica) => silica switch
    {
        < 52 => "Basalt",
        < 57 => "BasalticAndesite",
        < 63 => "Andesite",
        < 69 => "Dacite",
        _ => "Rhyolite",
    };

    /// <summary>A thing as sticky as the magma, for the card: water, honey, ketchup … asphalt, glass.</summary>
    public static string ViscosityLike(double logViscosity) => logViscosity switch
    {
        < 1.5 => "Honey",
        < 3 => "PeanutButter",
        < 4.5 => "Toothpaste",
        < 6 => "Putty",
        < 8 => "Asphalt",
        _ => "Glacier",
    };

    /// <summary>Water the melt can hold dissolved at <paramref name="pressureMpa"/>, % by weight (s·√P; more in silica-rich melt).</summary>
    public static double Solubility(double silica, double pressureMpa)
    {
        var s = 0.32 + (Math.Clamp((silica - 50) / 22, 0, 1.2) * 0.09);
        return s * Math.Sqrt(Math.Max(0, pressureMpa));
    }

    /// <summary>The pressure of the rock above at <paramref name="depthKm"/>, MPa (about 25.5 MPa a kilometre, plus the air).</summary>
    public static double Pressure(double depthKm) => 0.1 + (CrustDensity * G * Math.Max(0, depthKm) * 1000 / 1e6);

    /// <summary>The depth where the rising magma can no longer hold its water and bubbles start, km (0 if it never could).</summary>
    public static double SaturationDepth(double silica, double water)
    {
        var s = Solubility(silica, 1);
        var pressure = (water / s) * (water / s);
        return Math.Max(0, (pressure - 0.1) / (CrustDensity * G * 1000 / 1e6));
    }

    /// <summary>
    /// The share of the magma's volume that is gas bubbles at <paramref name="depthKm"/>, if all the gas that has come out
    /// of the melt stays in it as it rises (closed system).
    /// </summary>
    public static double GasFraction(double silica, double water, double depthKm)
    {
        var pressure = Pressure(depthKm);
        var exsolved = Math.Max(0, water - Solubility(silica, pressure)) / 100;
        if (exsolved <= 0) return 0;
        var kelvin = Temperature(silica) + 273.15;
        var gas = exsolved * GasConstant * kelvin / (WaterMolarMass * pressure * 1e6);
        var melt = (1 - exsolved) / MeltDensity;
        return gas / (gas + melt);
    }

    /// <summary>The depth where the froth reaches 75 % gas and tears apart into ash and gas, km; null if it never does before the vent.</summary>
    public static double? FragmentationDepth(double silica, double water)
    {
        if (GasFraction(silica, water, 0) < FragmentationFraction) return null;
        double shallow = 0, deep = SaturationDepth(silica, water);
        if (GasFraction(silica, water, deep) >= FragmentationFraction) return deep;
        for (var i = 0; i < 60; i++)
        {
            var mid = (shallow + deep) / 2;
            if (GasFraction(silica, water, mid) >= FragmentationFraction) shallow = mid;
            else deep = mid;
        }

        return (shallow + deep) / 2;
    }

    /// <summary>The style of eruption: runny magma lets its bubbles out; sticky magma traps them, and with much gas it explodes.</summary>
    public static EruptionStyle Style(double logViscosity, double water)
    {
        if (logViscosity < 3) return water < 1.2 ? EruptionStyle.Hawaiian : EruptionStyle.Strombolian;
        if (water < 1.5) return EruptionStyle.Dome;
        return water < PlinianWater(logViscosity) ? EruptionStyle.Vulcanian : EruptionStyle.Plinian;
    }

    /// <summary>The style of eruption of a magma of <paramref name="silica"/> % silica and <paramref name="water"/> % water.</summary>
    public static EruptionStyle StyleOf(double silica, double water) => Style(LogViscosity(silica), water);

    /// <summary>The water above which sticky magma of this viscosity erupts in a Plinian column (less for stickier magma).</summary>
    public static double PlinianWater(double logViscosity) => 4.5 - (0.25 * (logViscosity - 3));

    /// <summary>Whether this style blows the magma apart into ash (the froth tears apart before it reaches the vent).</summary>
    public static bool IsExplosive(EruptionStyle style) => style is EruptionStyle.Vulcanian or EruptionStyle.Plinian;

    /// <summary>
    /// The volcano's shape: a shield from runny basalt with little gas (thin lava flows), a steep cone of layers from
    /// gassier or stickier magma (lava and ash in turn), a caldera from rhyolite.
    /// </summary>
    public static ConeShape Shape(double silica, double water) => StyleOf(silica, water) == EruptionStyle.Hawaiian
        ? ConeShape.Shield
        : silica >= 69 ? ConeShape.Caldera : ConeShape.Strato;

    /// <summary>The magma erupted per second at the eruption's height, m³ of melt (only the explosive styles make a column).</summary>
    public static double EruptionRate(EruptionStyle style, double water) => style switch
    {
        EruptionStyle.Plinian => Math.Pow(10, 3.0 + (0.35 * water)),
        EruptionStyle.Vulcanian => Math.Pow(10, 1.5 + (0.3 * water)),
        EruptionStyle.Strombolian => 1,
        EruptionStyle.Hawaiian => 100,
        _ => 0.5,
    };

    /// <summary>The height of an eruption column above the vent for <paramref name="rate"/> m³ of melt a second, km (Mastin et al. 2009).</summary>
    public static double ColumnHeightKm(double rate) => rate <= 0 ? 0 : 2.00 * Math.Pow(rate, 0.241);

    /// <summary>
    /// How high the eruption reaches above the vent, km: a lava fountain (Hawaiian), lava lumps (Strombolian), the ash
    /// column (Vulcanian, Plinian); a dome only grows.
    /// </summary>
    public static double EruptionHeightKm(EruptionStyle style, double water) => style switch
    {
        EruptionStyle.Hawaiian => 0.12 + (water * 0.5),
        EruptionStyle.Strombolian => 0.1 + (water * 0.05),
        EruptionStyle.Dome => 0,
        _ => ColumnHeightKm(EruptionRate(style, water)),
    };

    /// <summary>The top of the magma chamber, km below the surface: shallow under basalt volcanoes, deeper under sticky ones.</summary>
    public static double ChamberTopKm(double silica) => 2 + (Math.Clamp((silica - 50) / 22, 0, 1.2) * 4);

    /// <summary>How far a lava flow runs, km: tens of kilometres for runny basalt, a few hundred metres for sticky lava.</summary>
    public static double LavaFlowLengthKm(double logViscosity) => Math.Clamp(60 * Math.Pow(10, -0.45 * (logViscosity - 1.5)), 0.2, 60);

    /// <summary>Seconds a whole round (recharge, rising, eruption, calm) takes on the screen.</summary>
    public const double RoundSeconds = 30;

    /// <summary>Where each phase starts in a round (a share of it).</summary>
    public const double RisingStart = 0.25;

    public const double EruptionStart = 0.38;

    public const double CalmStart = 0.84;

    /// <summary>The phase at <paramref name="progress"/> (0 to 1) through a round.</summary>
    public static EruptionPhase PhaseAt(double progress) => progress switch
    {
        < RisingStart => EruptionPhase.Recharge,
        < EruptionStart => EruptionPhase.Rising,
        < CalmStart => EruptionPhase.Erupting,
        _ => EruptionPhase.Calm,
    };

    /// <summary>How far through the eruption itself <paramref name="progress"/> is, 0 to 1 (0 before, 1 after).</summary>
    public static double EruptionShare(double progress) => Math.Clamp((progress - EruptionStart) / (CalmStart - EruptionStart), 0, 1);

    /// <summary>The chamber's pressure above the rock's, MPa: it builds while magma flows in and falls when the eruption lets it out.</summary>
    public static double Overpressure(double progress)
    {
        const double peak = 18;
        if (progress < RisingStart) return peak * VolcanoModel.SmoothStep(0, RisingStart, progress);
        if (progress < EruptionStart) return peak;
        return peak * (1 - VolcanoModel.SmoothStep(EruptionStart, EruptionStart + ((CalmStart - EruptionStart) * 0.7), progress));
    }

    #endregion

    #region the rock cycle

    /// <summary>The family each rock (or state of a grain) belongs to; null for loose sediment, magma and the like.</summary>
    public static RockFamily? FamilyOf(string state) => state switch
    {
        "Granite" or "Basalt" or "Pumice" or "Obsidian" or "Andesite" or "Gabbro" or "Rhyolite" => RockFamily.Igneous,
        "Sandstone" or "Shale" or "Limestone" or "Conglomerate" or "Siltstone" => RockFamily.Sedimentary,
        "Slate" or "Phyllite" or "Schist" or "Gneiss" or "Marble" or "Quartzite" or "Migmatite" => RockFamily.Metamorphic,
        _ => null,
    };

    /// <summary>The grains one can follow round the cycle (each ends where its next round would start).</summary>
    public static readonly IReadOnlyList<Journey> Journeys =
    [
        new("Sand",
        [
            new("peak", RockProcess.Start, "Granite", 0),
            new("slope", RockProcess.Weathering, "Sand", 1e4),
            new("river", RockProcess.Erosion, "Sand", 1e3),
            new("beach", RockProcess.Deposition, "Sand", 1e2),
            new("sandlayer", RockProcess.Lithification, "Sandstone", 1e7),
            new("folds", RockProcess.Uplift, "Sandstone", 2e7),
            new("foothill", RockProcess.Weathering, "Sand", 1e5),
        ]),
        new("Mud",
        [
            new("lava", RockProcess.Start, "Basalt", 0),
            new("lavasoil", RockProcess.Weathering, "Clay", 1e4),
            new("river", RockProcess.Erosion, "Mud", 1e3),
            new("deepsea", RockProcess.Deposition, "Mud", 1e3),
            new("mudlayer", RockProcess.Lithification, "Shale", 1e7),
            new("metamorphic", RockProcess.Metamorphism, "Schist", 3e7),
            new("melt", RockProcess.Melting, "Magma", 1e7),
            new("pluton", RockProcess.Crystallization, "Granite", 1e6),
            new("peak", RockProcess.Uplift, "Granite", 5e7),
        ]),
        new("Shell",
        [
            new("reef", RockProcess.Start, "Shell", 0),
            new("reefbed", RockProcess.Deposition, "ShellSand", 1e3),
            new("limelayer", RockProcess.Lithification, "Limestone", 1e7),
            new("marble", RockProcess.Metamorphism, "Marble", 1e6),
            new("cliff", RockProcess.Uplift, "Marble", 3e7),
            new("river", RockProcess.Weathering, "Dissolved", 1e4),
            new("reef", RockProcess.Deposition, "Shell", 1e2),
        ]),
        new("Magma",
        [
            new("metamorphic", RockProcess.Start, "Gneiss", 0),
            new("melt", RockProcess.Melting, "Magma", 1e6),
            new("chamber", RockProcess.Melting, "Magma", 1e5),
            new("vent", RockProcess.Eruption, "Lava", 1e3),
            new("lava", RockProcess.Crystallization, "Basalt", 10),
            new("lavasoil", RockProcess.Weathering, "Clay", 1e4),
        ]),
    ];

    public static Journey JourneyOf(string id) => Journeys.FirstOrDefault(j => j.Id == id) ?? Journeys[0];

    #endregion

    #region sediment

    /// <summary>The grain sizes poured in, with their diameters, metres.</summary>
    public static readonly IReadOnlyList<(string Id, double Diameter)> GrainSizes =
    [
        ("Gravel", 4e-3),
        ("Sand", 2.5e-4),
        ("Silt", 3e-5),
        ("Clay", 2e-6),
    ];

    /// <summary>
    /// How fast a quartz grain of <paramref name="diameter"/> metres sinks through still water, m/s (Ferguson &amp; Church
    /// 2004: Stokes for fine grains, a drag limit for coarse ones).
    /// </summary>
    public static double SettlingSpeed(double diameter)
    {
        const double submerged = 1.65;
        const double viscosity = 1.0e-6;
        const double c1 = 18;
        const double c2 = 1.0;
        var rgd2 = submerged * G * diameter * diameter;
        return rgd2 / ((c1 * viscosity) + Math.Sqrt(0.75 * c2 * submerged * G * diameter * diameter * diameter));
    }

    /// <summary>Seconds for a grain to sink one metre through still water.</summary>
    public static double SecondsPerMetre(double diameter) => 1 / SettlingSpeed(diameter);

    /// <summary>The share of a buried layer that is pore water at <paramref name="depthKm"/> (sand from 49 %, mud from 63 %).</summary>
    public static double Porosity(bool muddy, double depthKm) =>
        muddy ? 0.63 * Math.Exp(-0.51 * Math.Max(0, depthKm)) : 0.49 * Math.Exp(-0.27 * Math.Max(0, depthKm));

    /// <summary>A layer's thickness at <paramref name="depthKm"/> compared with when it was laid down (the same grains, less water).</summary>
    public static double Thickness(bool muddy, double depthKm) =>
        (1 - Porosity(muddy, 0)) / (1 - Porosity(muddy, depthKm));

    /// <summary>How much of the pore space minerals have filled with cement at <paramref name="depthKm"/>, 0 to 1.</summary>
    public static double Cement(double depthKm) => SmoothStep(0.4, 2.5, depthKm);

    /// <summary>The rock a layer of loose grains of this size becomes once cemented.</summary>
    public static string SedimentaryRock(string grain) => grain switch
    {
        "Gravel" => "Conglomerate",
        "Sand" => "Sandstone",
        "Silt" => "Siltstone",
        _ => "Shale",
    };

    #endregion

    #region metamorphism

    /// <summary>The usual rise of temperature with depth inside a continent, °C a kilometre.</summary>
    public const double Geotherm = 25;

    /// <summary>The temperature at <paramref name="depthKm"/>, °C.</summary>
    public static double TemperatureAt(double depthKm) => 15 + (Geotherm * Math.Max(0, depthKm));

    /// <summary>The pressure at <paramref name="depthKm"/>, kilobars (about 0.26 kbar a kilometre; 1 kbar ≈ 1000 atmospheres).</summary>
    public static double KilobarsAt(double depthKm) => (Pressure(depthKm) - 0.1) / 100;

    /// <summary>What a buried rock becomes at <paramref name="depthKm"/>: shale → slate → phyllite → schist → gneiss → migmatite.</summary>
    public static string MetamorphicRock(string parent, double depthKm)
    {
        var t = TemperatureAt(depthKm);
        return parent switch
        {
            "Sandstone" => t < 250 ? "Sandstone" : "Quartzite",
            "Limestone" => t < 250 ? "Limestone" : "Marble",
            _ => t switch
            {
                < 200 => "Shale",
                < 330 => "Slate",
                < 420 => "Phyllite",
                < 600 => "Schist",
                < 700 => "Gneiss",
                _ => "Migmatite",
            },
        };
    }

    /// <summary>How far metamorphism has gone at <paramref name="depthKm"/>: 0 at 200 °C, 1 at 700 °C.</summary>
    public static double Grade(double depthKm) => Math.Clamp((TemperatureAt(depthKm) - 200) / 500, 0, 1);

    #endregion

    internal static double SmoothStep(double from, double to, double x)
    {
        var t = Math.Clamp((x - from) / (to - from), 0, 1);
        return t * t * (3 - (2 * t));
    }
}
