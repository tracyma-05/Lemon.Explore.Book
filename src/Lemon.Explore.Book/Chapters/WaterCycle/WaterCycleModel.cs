namespace Lemon.Explore.Book.Chapters.WaterCycle;

/// <summary>The three views of the water-cycle page.</summary>
public enum WaterMode
{
    /// <summary>A landscape from the sea to over a mountain: every way water moves, and one drop to follow.</summary>
    Cycle,

    /// <summary>A parcel of air lifted until it cools to its dew point and a cloud forms, then rain.</summary>
    Cloud,

    /// <summary>Where Earth's water is, and how much of it moves each year.</summary>
    Stores,
}

/// <summary>The ways water moves in the cycle view.</summary>
public enum WaterProcess
{
    None,
    Evaporation,
    Transpiration,
    Sublimation,
    Transport,
    Condensation,
    Precipitation,
    Snowmelt,
    Runoff,
    Infiltration,
    Groundwater,
}

/// <summary>The journeys a followed drop can take from the sea back to the sea.</summary>
public enum DropRoute
{
    /// <summary>Rain on the mountain, down a river.</summary>
    River,

    /// <summary>Snow on the peak, kept until it melts.</summary>
    Snow,

    /// <summary>Rain soaking into the ground, slowly back through the rock.</summary>
    Ground,

    /// <summary>Rain taken up by a tree and breathed out again.</summary>
    Tree,

    /// <summary>Rain falling straight back into the sea (most of it does).</summary>
    Sea,
}

/// <summary>Where a followed drop is on its journey.</summary>
public enum DropLeg
{
    Ocean,
    Evaporation,
    Transport,
    Condensation,
    Rain,
    Snowfall,
    Snowpack,
    Snowmelt,
    River,
    Infiltration,
    Groundwater,
    Uptake,
    Transpiration,
}

/// <summary>What lifts the air in the cloud view.</summary>
public enum CloudLift
{
    /// <summary>A bubble of air warmed by sunny ground rises on its own.</summary>
    Thermal,

    /// <summary>Wind pushes the air up over a mountain, and down the other side.</summary>
    Mountain,

    /// <summary>Warm air slides up over a wedge of cold air.</summary>
    Front,
}

/// <summary>The two pictures of the stores view.</summary>
public enum StoresView
{
    Spheres,
    Flows,
}

/// <summary>Where Earth's water is (Shiklomanov 1993, as tabled by the USGS).</summary>
public enum WaterStore
{
    Oceans,
    IceAndSnow,
    Groundwater,
    GroundIce,
    Lakes,
    SoilMoisture,
    Atmosphere,
    Swamps,
    Rivers,
    Living,
}

/// <summary>One step of a lifted parcel of air.</summary>
/// <param name="Height">Metres above sea level.</param>
/// <param name="Temperature">The parcel's temperature, °C.</param>
/// <param name="DewPoint">The parcel's dew point, °C (equal to its temperature inside the cloud).</param>
/// <param name="Pressure">hPa.</param>
/// <param name="Condensed">Water turned to cloud droplets so far, grams per kilogram of air.</param>
/// <param name="Saturated">Whether the parcel is in cloud.</param>
public readonly record struct ParcelPoint(double Height, double Temperature, double DewPoint, double Pressure, double Condensed, bool Saturated)
{
    /// <summary>Relative humidity, 0–1.</summary>
    public double Humidity => Math.Clamp(WaterCycleModel.SaturationPressure(DewPoint) / WaterCycleModel.SaturationPressure(Temperature), 0, 1);
}

/// <summary>A parcel lifted from the ground to a top height, step by step.</summary>
public sealed class ParcelAscent
{
    internal ParcelAscent(IReadOnlyList<ParcelPoint> points, double cloudBase, double freezingLevel)
    {
        Points = points;
        CloudBase = cloudBase;
        FreezingLevel = freezingLevel;
    }

    /// <summary>Every 20 m from the ground to the top.</summary>
    public IReadOnlyList<ParcelPoint> Points { get; }

    /// <summary>Where it first reaches its dew point, metres; NaN when it never does below the top.</summary>
    public double CloudBase { get; }

    /// <summary>Where the parcel cools to 0 °C, metres; NaN when it stays warmer.</summary>
    public double FreezingLevel { get; }

    public ParcelPoint Ground => Points[0];

    public ParcelPoint Top => Points[^1];

    public bool HasCloud => !double.IsNaN(CloudBase);

    /// <summary>How deep the cloud is when the parcel reaches the top, metres.</summary>
    public double CloudDepth => HasCloud ? Top.Height - CloudBase : 0;

    /// <summary>The parcel at a height, interpolated between steps.</summary>
    public ParcelPoint At(double height) => WaterCycleModel.Interpolate(Points, height);
}

/// <summary>
/// The numbers behind the water-cycle page: how much water vapour air can hold (Magnus), a parcel of air lifted and
/// cooled until a cloud forms (dry and moist adiabatic lapse rates in a standard atmosphere), the warm dry wind down the
/// far side of a mountain, Earth's water stores and yearly flows, and the landscape of the cycle view.
/// </summary>
public static class WaterCycleModel
{
    private const double Gravity = 9.80665;

    /// <summary>Specific heat of dry air at constant pressure, J/(kg·K).</summary>
    private const double Cp = 1004.0;

    /// <summary>Gas constant of dry air, J/(kg·K).</summary>
    private const double Rd = 287.04;

    /// <summary>Latent heat of vaporisation, J/kg.</summary>
    private const double Lv = 2.501e6;

    /// <summary>Ratio of the molar masses of water and dry air.</summary>
    private const double Epsilon = 0.622;

    /// <summary>How fast unsaturated air cools as it rises, °C per metre (g / cp, about 9.8 °C per km).</summary>
    public const double DryLapseRate = Gravity / Cp;

    /// <summary>The standard atmosphere's fall of temperature with height, °C per metre.</summary>
    public const double StandardLapseRate = 0.0065;

    /// <summary>Sea-level pressure, hPa.</summary>
    public const double SeaLevelPressure = 1013.25;

    #region water vapour

    /// <summary>The most water vapour air can hold at a temperature, as its pressure in hPa (Magnus, over water).</summary>
    public static double SaturationPressure(double celsius) => 6.1094 * Math.Exp(17.625 * celsius / (celsius + 243.04));

    /// <summary>The temperature at which a vapour pressure (hPa) saturates the air, °C.</summary>
    public static double DewPointFromPressure(double vapourPressure)
    {
        var gamma = Math.Log(Math.Max(1e-9, vapourPressure) / 6.1094);
        return 243.04 * gamma / (17.625 - gamma);
    }

    /// <summary>The dew point of air at a temperature and relative humidity (0–1), °C.</summary>
    public static double DewPoint(double celsius, double humidity) =>
        DewPointFromPressure(Math.Clamp(humidity, 1e-4, 1) * SaturationPressure(celsius));

    /// <summary>Grams of water vapour per kilogram of dry air at a vapour pressure and air pressure (hPa).</summary>
    public static double MixingRatio(double vapourPressure, double pressure) =>
        1000 * Epsilon * vapourPressure / Math.Max(1e-6, pressure - vapourPressure);

    /// <summary>The most water vapour a cubic metre of air can hold at a temperature, grams.</summary>
    public static double SaturationDensity(double celsius) =>
        SaturationPressure(celsius) * 100 / (461.5 * (celsius + 273.15)) * 1000;

    /// <summary>How many times more water vapour air can hold <paramref name="warming"/> degrees warmer than at <paramref name="celsius"/>.</summary>
    public static double VapourFactor(double celsius, double warming) =>
        SaturationPressure(celsius + warming) / SaturationPressure(celsius);

    /// <summary>Air pressure at a height in the standard atmosphere, hPa.</summary>
    public static double Pressure(double height) =>
        SeaLevelPressure * Math.Pow(Math.Max(0.05, 1 - (2.25577e-5 * height)), 5.25588);

    /// <summary>
    /// How fast saturated air cools as it rises, °C per metre: slower than dry air, because water condensing in it gives
    /// back its latent heat (about 4 °C per km in warm air near the ground, near the dry rate in cold air high up).
    /// </summary>
    public static double MoistLapseRate(double celsius, double pressure)
    {
        var t = celsius + 273.15;
        var r = MixingRatio(SaturationPressure(celsius), pressure) / 1000;
        return Gravity * (1 + (Lv * r / (Rd * t))) / (Cp + (Lv * Lv * r * Epsilon / (Rd * t * t)));
    }

    #endregion

    #region the lifted parcel

    /// <summary>Metres between the steps of a lifted parcel.</summary>
    private const double Step = 20;

    /// <summary>
    /// Lifts a parcel of air from <paramref name="groundHeight"/> to <paramref name="top"/>: it cools at the dry rate
    /// until it reaches its dew point (the cloud base), then at the slower moist rate as its vapour condenses.
    /// </summary>
    public static ParcelAscent Lift(double groundTemperature, double humidity, double top, double groundHeight = 0)
    {
        var height = groundHeight;
        var t = groundTemperature;
        var pressure = Pressure(height);
        var dewPoint = DewPoint(groundTemperature, humidity);
        var r = MixingRatio(SaturationPressure(dewPoint), pressure);
        var startR = r;
        var saturated = false;
        var cloudBase = double.NaN;
        var freezing = groundTemperature <= 0 ? groundHeight : double.NaN;
        var points = new List<ParcelPoint> { new(height, t, dewPoint, pressure, 0, false) };

        while (height < top - 1e-9)
        {
            var dz = Math.Min(Step, top - height);
            var lastT = t;
            var lastHeight = height;
            if (!saturated)
            {
                t -= DryLapseRate * dz;
                height += dz;
                pressure = Pressure(height);
                var rs = MixingRatio(SaturationPressure(t), pressure);
                if (rs <= r)
                {
                    // Saturated somewhere in this step: find where, then go on at the moist rate from there.
                    var (baseHeight, baseT) = FindSaturation(lastHeight, lastT, r, dz);
                    cloudBase = baseHeight;
                    saturated = true;
                    t = baseT;
                    var rest = height - baseHeight;
                    t -= MoistLapseRate(t, Pressure(baseHeight)) * rest;
                    r = MixingRatio(SaturationPressure(t), pressure);
                }
            }
            else
            {
                // Midpoint step of the moist rate.
                var mid = t - (MoistLapseRate(t, Pressure(height)) * dz / 2);
                t -= MoistLapseRate(mid, Pressure(height + (dz / 2))) * dz;
                height += dz;
                pressure = Pressure(height);
                r = Math.Min(r, MixingRatio(SaturationPressure(t), pressure));
            }

            if (double.IsNaN(freezing) && t <= 0) freezing = lastHeight + ((height - lastHeight) * lastT / Math.Max(1e-9, lastT - t));

            var parcelDew = saturated ? t : DewPointFromPressure(VapourPressure(r, pressure));
            points.Add(new ParcelPoint(height, t, parcelDew, pressure, Math.Max(0, startR - r), saturated));
        }

        return new ParcelAscent(points, cloudBase, freezing);
    }

    /// <summary>
    /// The air coming down the far side of a mountain from the crest: it has rained out what condensed, so it warms all
    /// the way at the dry rate and arrives warmer and drier than it set off (a foehn wind).
    /// </summary>
    public static IReadOnlyList<ParcelPoint> Descend(ParcelPoint crest, double groundHeight)
    {
        var r = MixingRatio(SaturationPressure(crest.DewPoint), crest.Pressure);
        var points = new List<ParcelPoint>();
        var height = crest.Height;
        var t = crest.Temperature;
        points.Add(crest with { Saturated = false });
        while (height > groundHeight + 1e-9)
        {
            var dz = Math.Min(Step, height - groundHeight);
            height -= dz;
            t += DryLapseRate * dz;
            var pressure = Pressure(height);
            points.Add(new ParcelPoint(height, t, DewPointFromPressure(VapourPressure(r, pressure)), pressure, crest.Condensed, false));
        }

        return points;
    }

    /// <summary>
    /// The way down the far side of the mountain: warmer and drier when the cloud on the way up rained (<see cref="Descend"/>),
    /// otherwise the way up backwards, its cloud droplets evaporating again as it sinks and warms.
    /// </summary>
    public static IReadOnlyList<ParcelPoint> OverMountain(ParcelAscent ascent) =>
        Rains(ascent) ? Descend(ascent.Top, ascent.Ground.Height) : ascent.Points.Reverse().ToList();

    /// <summary>Vapour pressure (hPa) of air with a mixing ratio (g/kg) at a pressure (hPa).</summary>
    private static double VapourPressure(double mixingRatio, double pressure)
    {
        var r = mixingRatio / 1000;
        return r * pressure / (Epsilon + r);
    }

    /// <summary>Bisects the dry step from <paramref name="height"/> for the height where the parcel saturates.</summary>
    private static (double Height, double Temperature) FindSaturation(double height, double temperature, double r, double dz)
    {
        double low = 0, high = dz;
        for (var i = 0; i < 40; i++)
        {
            var mid = (low + high) / 2;
            var t = temperature - (DryLapseRate * mid);
            if (MixingRatio(SaturationPressure(t), Pressure(height + mid)) <= r) high = mid;
            else low = mid;
        }

        return (height + high, temperature - (DryLapseRate * high));
    }

    /// <summary>The well-known rule of thumb for the cloud base: 125 m for every degree between temperature and dew point.</summary>
    public static double CloudBaseRuleOfThumb(double temperature, double dewPoint) => 125 * (temperature - dewPoint);

    internal static ParcelPoint Interpolate(IReadOnlyList<ParcelPoint> points, double height)
    {
        if (points.Count == 0) return default;
        var ascending = points[^1].Height >= points[0].Height;
        if (ascending ? height <= points[0].Height : height >= points[0].Height) return points[0];
        if (ascending ? height >= points[^1].Height : height <= points[^1].Height) return points[^1];

        var low = 0;
        var high = points.Count - 1;
        while (high - low > 1)
        {
            var mid = (low + high) / 2;
            if ((points[mid].Height <= height) == ascending) low = mid;
            else high = mid;
        }

        var a = points[low];
        var b = points[high];
        var f = Math.Abs(b.Height - a.Height) < 1e-9 ? 0 : (height - a.Height) / (b.Height - a.Height);
        return new ParcelPoint(height, Lerp(a.Temperature, b.Temperature, f), Lerp(a.DewPoint, b.DewPoint, f), Lerp(a.Pressure, b.Pressure, f),
            Lerp(a.Condensed, b.Condensed, f), f < 0.5 ? a.Saturated : b.Saturated);
    }

    /// <summary>How much bigger a parcel has grown (by volume) at a point than at the ground: lower pressure, it swells.</summary>
    public static double Swelling(ParcelPoint ground, ParcelPoint now) =>
        (now.Temperature + 273.15) / (ground.Temperature + 273.15) * (ground.Pressure / now.Pressure);

    /// <summary>The top of the lift, metres: the cumulus's top, the mountain's crest, or as high as the front goes.</summary>
    public static double LiftTop(CloudLift lift) => lift switch
    {
        CloudLift.Mountain => MountainHeight,
        CloudLift.Front => 7000,
        _ => 6000,
    };

    /// <summary>The cloud view's mountain, metres.</summary>
    public const double MountainHeight = 3000;

    /// <summary>A cloud at least this deep (metres) grows drops big enough to fall.</summary>
    public const double RainingDepth = 1000;

    /// <summary>Whether the lifted parcel's cloud grows deep enough to rain or snow.</summary>
    public static bool Rains(ParcelAscent ascent) => ascent.CloudDepth >= RainingDepth;

    /// <summary>Whether what falls reaches the ground as snow: the ground is at most this many degrees above freezing.</summary>
    public static bool SnowsAtGround(double groundTemperature) => groundTemperature <= 2;

    #endregion

    #region where the water is

    /// <summary>All of Earth's water, km³.</summary>
    public const double TotalWater = 1_386_000_000;

    /// <summary>A store's water, km³ (USGS, after Shiklomanov 1993).</summary>
    public static double Volume(WaterStore store) => store switch
    {
        WaterStore.Oceans => 1_338_000_000,
        WaterStore.IceAndSnow => 24_064_000,
        WaterStore.Groundwater => 23_400_000,
        WaterStore.GroundIce => 300_000,
        WaterStore.Lakes => 176_400,
        WaterStore.SoilMoisture => 16_500,
        WaterStore.Atmosphere => 12_900,
        WaterStore.Swamps => 11_470,
        WaterStore.Rivers => 2_120,
        WaterStore.Living => 1_120,
        _ => 0,
    };

    /// <summary>Fresh groundwater, km³ (the rest is salty).</summary>
    public const double FreshGroundwater = 10_530_000;

    /// <summary>Fresh lakes, km³ (the rest are salt lakes such as the Caspian Sea).</summary>
    public const double FreshLakes = 91_000;

    /// <summary>All fresh water, km³: about 2.5 % of the total, most of it frozen.</summary>
    public const double FreshWater = 35_030_000;

    /// <summary>Liquid fresh water: groundwater, swamps, rivers and fresh lakes, km³.</summary>
    public static double FreshLiquidWater => FreshGroundwater + Volume(WaterStore.Swamps) + Volume(WaterStore.Rivers) + FreshLakes;

    /// <summary>Fresh lakes and rivers: the water we can see and most easily use, km³.</summary>
    public static double LakesAndRivers => FreshLakes + Volume(WaterStore.Rivers);

    /// <summary>A store's share of all the water, 0–1.</summary>
    public static double Share(WaterStore store) => Volume(store) / TotalWater;

    /// <summary>The diameter of a ball holding a volume of water, km.</summary>
    public static double SphereDiameter(double cubicKilometres) => Math.Cbrt(6 * cubicKilometres / Math.PI);

    /// <summary>Earth's mean diameter, km.</summary>
    public const double EarthDiameter = 12_742;

    #endregion

    #region flows each year

    // km³ a year (Trenberth et al. 2007).
    public const double OceanEvaporation = 413_000;
    public const double OceanPrecipitation = 373_000;
    public const double LandPrecipitation = 113_000;
    public const double LandEvaporation = 73_000;

    /// <summary>Vapour carried from over the sea to over land; as much comes back by rivers and groundwater.</summary>
    public const double VapourToLand = OceanEvaporation - OceanPrecipitation;

    public const double Runoff = LandPrecipitation - LandEvaporation;

    /// <summary>All the water evaporating (and falling) in a year, km³: a metre of water over the whole Earth.</summary>
    public const double YearlyCycle = OceanEvaporation + LandEvaporation;

    /// <summary>How long water stays in the air on average, days: all of it rains out and is replaced about every 9 days.</summary>
    public static double AtmosphereDays => Volume(WaterStore.Atmosphere) / YearlyCycle * 365.25;

    /// <summary>How long water stays in the oceans on average, years.</summary>
    public static double OceanYears => Volume(WaterStore.Oceans) / OceanEvaporation;

    /// <summary>How long water stays in rivers on average, days.</summary>
    public static double RiverDays => Volume(WaterStore.Rivers) / Runoff * 365.25;

    /// <summary>The share of the sea's evaporation that rains straight back onto the sea.</summary>
    public static double FallsBackOnSea => OceanPrecipitation / OceanEvaporation;

    #endregion

    #region a followed drop

    /// <summary>A typical stay at a step of the journey, days (minutes for rain falling, centuries underground).</summary>
    public static double TypicalDays(DropLeg leg) => leg switch
    {
        DropLeg.Ocean => OceanYears * 365.25,
        DropLeg.Evaporation => 0.02,
        DropLeg.Transport => AtmosphereDays,
        DropLeg.Condensation => 0.04,
        DropLeg.Rain => 0.005,
        DropLeg.Snowfall => 0.02,
        DropLeg.Snowpack => 150,
        DropLeg.Snowmelt => 3,
        DropLeg.River => RiverDays,
        DropLeg.Infiltration => 60,
        DropLeg.Groundwater => 200 * 365.25,
        DropLeg.Uptake => 7,
        DropLeg.Transpiration => 0.02,
        _ => 0,
    };

    /// <summary>The way of moving a step of the journey belongs to, for lighting it up in the landscape.</summary>
    public static WaterProcess ProcessOf(DropLeg leg) => leg switch
    {
        DropLeg.Evaporation => WaterProcess.Evaporation,
        DropLeg.Transport => WaterProcess.Transport,
        DropLeg.Condensation => WaterProcess.Condensation,
        DropLeg.Rain or DropLeg.Snowfall => WaterProcess.Precipitation,
        DropLeg.Snowmelt => WaterProcess.Snowmelt,
        DropLeg.River => WaterProcess.Runoff,
        DropLeg.Infiltration => WaterProcess.Infiltration,
        DropLeg.Groundwater => WaterProcess.Groundwater,
        DropLeg.Uptake or DropLeg.Transpiration => WaterProcess.Transpiration,
        _ => WaterProcess.None,
    };

    #endregion

    internal static double Lerp(double a, double b, double t) => a + ((b - a) * t);
}
