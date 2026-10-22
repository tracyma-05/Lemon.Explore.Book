namespace Lemon.Explore.Book.Chapters.SeaBreeze;

/// <summary>The three views of the sea-and-land-breeze page.</summary>
public enum BreezeMode
{
    /// <summary>A coast through a whole day: the sea breeze by day, the land breeze by night.</summary>
    Coast,

    /// <summary>The thermal circulation built up step by step: heating, pressure surfaces, wind.</summary>
    Cell,

    /// <summary>The same thing over a continent and a year: the Asian monsoon.</summary>
    Monsoon,
}

/// <summary>The coast's weather: a clear summer day, or an overcast one that hardly heats the land.</summary>
public enum BreezeSky
{
    Clear,
    Overcast,
}

/// <summary>What the coast's wind is doing at a time of day.</summary>
public enum CoastPhase
{
    LandBreeze,
    MorningCalm,
    SeaBreezeBuilding,
    SeaBreeze,
    SeaBreezeFading,
    EveningCalm,
    Overcast,
}

/// <summary>The steps of the thermal circulation, in the order the page builds them up.</summary>
public enum CellStage
{
    /// <summary>Land and sea equally warm: the pressure surfaces lie flat.</summary>
    Even,

    /// <summary>The land warms its air, which expands: the pressure surfaces rise over the land.</summary>
    Heating,

    /// <summary>Aloft the pressure over the land is now higher: air flows out toward the sea.</summary>
    AloftFlow,

    /// <summary>The land's column has lost air, the sea's gained it: low pressure at the ground over land, high over sea.</summary>
    SurfacePressure,

    /// <summary>At the ground air flows from the sea's high to the land's low: the sea breeze.</summary>
    SurfaceWind,

    /// <summary>Rising over the land, sinking over the sea: one closed loop.</summary>
    Circulation,
}

/// <summary>Whether the cell is shown by day (land warmer) or by night (land colder).</summary>
public enum CellTime
{
    Day,
    Night,
}

/// <summary>The cities whose rainfall the monsoon view shows.</summary>
public enum MonsoonCity
{
    Mumbai,
    Guangzhou,
    Shanghai,
    Beijing,
    Darwin,
    Changsha,
}

/// <summary>A city of the monsoon view, its place and its typical rainfall in each month (mm, rounded).</summary>
public sealed record MonsoonCityInfo(MonsoonCity Id, double Latitude, double Longitude, IReadOnlyList<double> Rain)
{
    public double YearRain => Rain.Sum();
}

/// <summary>
/// A pressure system of the monsoon map: where it sits in mid-January and mid-July, its size in degrees, and how far
/// above (+) or below (−) the background pressure it is in each of those months, hPa.
/// </summary>
public sealed record PressureSystem(
    string Key,
    double LongitudeJan, double LatitudeJan,
    double LongitudeJul, double LatitudeJul,
    double SpreadLongitude, double SpreadLatitude,
    double AnomalyJan, double AnomalyJul,
    bool Labelled = true)
{
    public double Longitude(double summer) => LongitudeJan + ((LongitudeJul - LongitudeJan) * summer);

    public double Latitude(double summer) => LatitudeJan + ((LatitudeJul - LatitudeJan) * summer);

    public double Anomaly(double summer) => AnomalyJan + ((AnomalyJul - AnomalyJan) * summer);
}

/// <summary>
/// The physics of the sea-and-land-breeze page, kept apart from the drawing so it can be tested: the land's and the sea's
/// temperature through a day and the wind they drive, the hydrostatic pressure of a warmed column of air, and a simple
/// pressure map of Asia through the year with the surface wind it drives.
/// </summary>
public static class BreezeModel
{
    #region the coast through a day

    /// <summary>Hour of sunrise and sunset on the coast (a summer day, local solar time).</summary>
    public const double Sunrise = 5.5;

    public const double Sunset = 18.5;

    /// <summary>
    /// How Parton and Logan's daytime curve lags the Sun, hours: the warmest moment comes about two hours after noon.
    /// </summary>
    private const double PeakLag = 1.86;

    /// <summary>The breeze answers the land-sea contrast some time later, hours.</summary>
    public const double Lag = 0.75;

    /// <summary>A contrast of about this many degrees is needed before a breeze gets going.</summary>
    public const double Threshold = 1.5;

    /// <summary>Wind at the beach per degree of contrast above the threshold, by day and by night, m/s.</summary>
    public const double DayGain = 1.0;

    public const double NightGain = 0.7;

    /// <summary>Below this the wind at the beach counts as calm, m/s.</summary>
    public const double Calm = 0.5;

    /// <summary>Depth of the onshore sea breeze and of the offshore land breeze, metres.</summary>
    public const double SeaBreezeDepth = 900;

    public const double LandBreezeDepth = 300;

    /// <summary>Coldest and warmest air over the land on a clear and on an overcast day, °C.</summary>
    public static (double Min, double Max) LandRange(BreezeSky sky) => sky == BreezeSky.Clear ? (20, 31) : (22.5, 25.5);

    /// <summary>The sea's surface: about 24 °C, warmer by a few tenths in the afternoon.</summary>
    public const double SeaMean = 24;

    public static double SeaSwing(BreezeSky sky) => sky == BreezeSky.Clear ? 0.4 : 0.15;

    /// <summary>
    /// The air over the land (2 m up) at <paramref name="hour"/>: a sine from the minimum at sunrise to the maximum about
    /// two hours after noon, then after sunset an exponential fall back to the minimum at the next sunrise (Parton and
    /// Logan, 1981).
    /// </summary>
    public static double LandTemperature(double hour, BreezeSky sky = BreezeSky.Clear)
    {
        var (min, max) = LandRange(sky);
        hour = Wrap(hour);
        var day = Sunset - Sunrise;
        if (hour >= Sunrise && hour <= Sunset) return min + ((max - min) * Math.Sin(Math.PI * (hour - Sunrise) / (day + (2 * PeakLag))));

        var atSunset = min + ((max - min) * Math.Sin(Math.PI * day / (day + (2 * PeakLag))));
        var night = 24 - day;
        var since = hour > Sunset ? hour - Sunset : hour + 24 - Sunset;
        const double Tau = 3;
        return min + ((atSunset - min) * (Math.Exp(-since / Tau) - Math.Exp(-night / Tau)) / (1 - Math.Exp(-night / Tau)));
    }

    /// <summary>The sea's surface at <paramref name="hour"/>: it barely changes, warmest at about three in the afternoon.</summary>
    public static double SeaTemperature(double hour, BreezeSky sky = BreezeSky.Clear) =>
        SeaMean + (SeaSwing(sky) * Math.Sin(2 * Math.PI * (Wrap(hour) - 9) / 24));

    /// <summary>How much warmer the land's air is than the sea at <paramref name="hour"/>, °C (negative when colder).</summary>
    public static double Contrast(double hour, BreezeSky sky = BreezeSky.Clear) => LandTemperature(hour, sky) - SeaTemperature(hour, sky);

    /// <summary>
    /// The wind at the beach, m/s: positive onshore (the sea breeze), negative offshore (the land breeze). It follows the
    /// contrast <see cref="Lag"/> hours earlier, starts only once that is beyond about <see cref="Threshold"/> degrees, and
    /// the land breeze is weaker for the same contrast because the cold night air lies shallow and still.
    /// </summary>
    public static double ShoreWind(double hour, BreezeSky sky = BreezeSky.Clear)
    {
        var contrast = Contrast(hour - Lag, sky);
        var drive = contrast - (Threshold * Math.Tanh(contrast / Threshold));
        return drive * (drive > 0 ? DayGain : NightGain);
    }

    /// <summary>How fast the shore wind is changing at <paramref name="hour"/>, m/s per hour.</summary>
    public static double ShoreWindTrend(double hour, BreezeSky sky = BreezeSky.Clear) =>
        (ShoreWind(hour + 0.05, sky) - ShoreWind(hour - 0.05, sky)) / 0.1;

    /// <summary>
    /// How far inland the sea breeze's front has pushed, km: it sets off from the coast when the breeze starts and moves at
    /// about a third of the breeze's speed. Zero when there is no sea breeze.
    /// </summary>
    public static double FrontDistance(double hour, BreezeSky sky = BreezeSky.Clear)
    {
        hour = Wrap(hour);
        if (ShoreWind(hour, sky) < Calm || hour < Sunrise) return 0;
        return 2 + Integrate(Sunrise, hour, h => Math.Max(0, ShoreWind(h, sky) - Calm) * 0.3 * 3.6);
    }

    /// <summary>How far out to sea the land breeze reaches, km; zero when there is none.</summary>
    public static double LandBreezeReach(double hour, BreezeSky sky = BreezeSky.Clear)
    {
        hour = Wrap(hour);
        if (ShoreWind(hour, sky) > -Calm) return 0;
        var end = hour < 12 ? hour + 24 : hour;
        return 2 + Integrate(Sunset, end, h => Math.Max(0, -ShoreWind(h, sky) - (Calm * 0.5)) * 0.5 * 3.6);
    }

    /// <summary>What the wind at the beach is doing at <paramref name="hour"/>.</summary>
    public static CoastPhase Phase(double hour, BreezeSky sky = BreezeSky.Clear)
    {
        hour = Wrap(hour);
        var wind = ShoreWind(hour, sky);
        if (sky == BreezeSky.Overcast && Math.Abs(wind) < 1.5) return CoastPhase.Overcast;
        if (wind <= -Calm) return CoastPhase.LandBreeze;
        if (wind < Calm) return hour >= Sunrise && hour < 14 ? CoastPhase.MorningCalm : CoastPhase.EveningCalm;

        var trend = ShoreWindTrend(hour, sky);
        if (trend > 0.15 && wind < 3.5) return CoastPhase.SeaBreezeBuilding;
        if (trend < -0.15 && wind < 3) return CoastPhase.SeaBreezeFading;
        return CoastPhase.SeaBreeze;
    }

    /// <summary>The Beaufort number of a wind of <paramref name="speed"/> m/s.</summary>
    public static int Beaufort(double speed)
    {
        double[] limits = [0.5, 1.6, 3.4, 5.5, 8.0, 10.8, 13.9, 17.2, 20.8, 24.5, 28.5, 32.7];
        var force = 0;
        while (force < limits.Length && Math.Abs(speed) >= limits[force]) force++;
        return force;
    }

    /// <summary>Hours wrapped into [0, 24).</summary>
    public static double Wrap(double hour)
    {
        hour %= 24;
        return hour < 0 ? hour + 24 : hour;
    }

    private static double Integrate(double from, double to, Func<double, double> f)
    {
        if (to <= from) return 0;
        var steps = Math.Max(4, (int)Math.Ceiling((to - from) / 0.05));
        var dh = (to - from) / steps;
        var sum = 0.0;
        for (var i = 0; i < steps; i++) sum += f(from + ((i + 0.5) * dh));
        return sum * dh;
    }

    #endregion

    #region a column of warm air

    /// <summary>Gas constant of dry air, J/(kg·K).</summary>
    public const double GasConstant = 287.05;

    public const double Gravity = 9.80665;

    /// <summary>Standard sea-level pressure, hPa.</summary>
    public const double SurfacePressure = 1013.25;

    /// <summary>How fast the air gets colder upward over the sea, K per metre.</summary>
    public const double LapseRate = 0.0065;

    /// <summary>How deep the land's heating reaches into the air, metres.</summary>
    public const double HeatedDepth = 1000;

    /// <summary>
    /// The height between two pressure levels in air of mean temperature <paramref name="kelvin"/> (the hypsometric
    /// equation): warmer air is thicker, about 3 m more per degree between 1000 and 900 hPa.
    /// </summary>
    public static double Thickness(double kelvin, double bottom, double top) => GasConstant * kelvin / Gravity * Math.Log(bottom / top);

    /// <summary>Temperature of the air over the sea at height <paramref name="z"/>, K.</summary>
    public static double SeaColumn(double seaCelsius, double z) => seaCelsius + 273.15 - (LapseRate * z);

    /// <summary>Over the land: the sea's column plus the land's extra warmth, fading to nothing at <see cref="HeatedDepth"/>.</summary>
    public static double LandColumn(double landCelsius, double seaCelsius, double z) =>
        SeaColumn(seaCelsius, z) + ((landCelsius - seaCelsius) * Math.Max(0, 1 - (z / HeatedDepth)));

    /// <summary>
    /// Pressure at height <paramref name="z"/> in a column whose temperature is <paramref name="kelvin"/>(z), starting from
    /// <paramref name="surface"/> hPa at the ground (hydrostatic: dp/dz = −p g / (R T)).
    /// </summary>
    public static double PressureAt(double z, double surface, Func<double, double> kelvin)
    {
        if (z <= 0) return surface;
        var steps = Math.Max(8, (int)Math.Ceiling(z / 20));
        var dz = z / steps;
        var sum = 0.0;
        for (var i = 0; i < steps; i++) sum += 1 / kelvin((i + 0.5) * dz);
        return surface * Math.Exp(-Gravity / GasConstant * sum * dz);
    }

    /// <summary>
    /// Land minus sea pressure at height <paramref name="z"/>, hPa. Just after heating (<paramref name="settled"/> false)
    /// both columns still have the same pressure at the ground, so the warmer land column has more pressure aloft. Once
    /// the air has moved (true) part of the land column's air has gone over the sea: low at the ground over the land,
    /// high over the sea, and the difference changes sign half way up.
    /// </summary>
    public static double PressureDifference(double z, double landCelsius, double seaCelsius, bool settled)
    {
        var shift = settled ? AloftDifference(landCelsius, seaCelsius) / 4 : 0;
        return PressureAt(z, SurfacePressure - shift, h => LandColumn(landCelsius, seaCelsius, h))
            - PressureAt(z, SurfacePressure + shift, h => SeaColumn(seaCelsius, h));
    }

    /// <summary>Land minus sea pressure at the top of the heated layer just after heating, hPa.</summary>
    public static double AloftDifference(double landCelsius, double seaCelsius) =>
        PressureDifference(HeatedDepth, landCelsius, seaCelsius, settled: false);

    /// <summary>Land minus sea pressure at the ground once the breeze is blowing, hPa (negative: lower over the land).</summary>
    public static double SurfaceDifference(double landCelsius, double seaCelsius) =>
        PressureDifference(0, landCelsius, seaCelsius, settled: true);

    /// <summary>The height (m) at which pressure <paramref name="hPa"/> is reached in the land's and the sea's column.</summary>
    public static double HeightOf(double hPa, double surface, Func<double, double> kelvin)
    {
        double low = 0, high = 12000;
        for (var i = 0; i < 50; i++)
        {
            var mid = (low + high) / 2;
            if (PressureAt(mid, surface, kelvin) > hPa) low = mid;
            else high = mid;
        }

        return (low + high) / 2;
    }

    /// <summary>The six stages of the cell take this many seconds each.</summary>
    public const double StageSeconds = 3.2;

    public const int StageCount = 6;

    /// <summary>The cell's whole story, seconds; it then holds the last stage a little and starts again.</summary>
    public const double CellDuration = StageSeconds * StageCount;

    public static CellStage StageAt(double seconds) => (CellStage)Math.Clamp((int)Math.Floor(seconds / StageSeconds), 0, StageCount - 1);

    /// <summary>
    /// How far <paramref name="stage"/> has got at <paramref name="seconds"/>, 0…1: it eases in during the first half of its
    /// own slot and then stays done.
    /// </summary>
    public static double Progress(double seconds, CellStage stage)
    {
        if (stage == CellStage.Even) return 1;
        var t = Math.Clamp((seconds - ((int)stage * StageSeconds)) / (StageSeconds * 0.55), 0, 1);
        return t * t * (3 - (2 * t));
    }

    #endregion

    #region the monsoon

    /// <summary>The background pressure of the monsoon map, hPa.</summary>
    public const double Background = 1012;

    /// <summary>The part of the map the monsoon view shows, degrees.</summary>
    public const double West = 40;

    public const double East = 160;

    public const double South = -36;

    public const double North = 60;

    /// <summary>
    /// The big pressure systems round Asia and the Indian Ocean in mid-January and mid-July, sized and placed after the
    /// usual climatological sea-level pressure charts (e.g. NCEP/NCAR reanalysis means), rounded.
    /// </summary>
    public static IReadOnlyList<PressureSystem> Systems { get; } =
    [
        new("Siberian", 98, 47, 98, 47, 26, 14, 24, -2),
        new("AsianLow", 75, 30, 70, 29, 20, 10, 4, -15),
        new("EastAsiaLow", 105, 40, 108, 40, 24, 12, 0, -5, Labelled: false),
        new("Aleutian", 178, 52, 180, 55, 22, 10, -14, -1),
        new("PacificHigh", 215, 30, 190, 36, 36, 11, 8, 14),
        new("PacificRidge", 150, 20, 138, 27, 18, 7, 2, 5),
        new("Mascarene", 75, -32, 60, -30, 26, 9, 4, 8),
        new("Australia", 133, -24, 133, -26, 16, 9, -7, 5),
    ];

    /// <summary>
    /// How far toward July the year is at <paramref name="month"/> (0 = 1 January, 12 = the next one): 0 in mid-January,
    /// 1 in mid-July. The big continent's heating and cooling lag the Sun by about a month.
    /// </summary>
    public static double Summer(double month) => (1 - Math.Cos(2 * Math.PI * (month - 0.5) / 12)) / 2;

    /// <summary>
    /// The latitude of the equatorial trough (the ITCZ) at <paramref name="longitude"/>: south of the equator in January,
    /// far north over India in July, where it merges with the heat low.
    /// </summary>
    public static double TroughLatitude(double longitude, double month)
    {
        var summer = Summer(month);
        var january = -6 - (7 * Smooth(90, 130, longitude));
        var july = 15 + (7 * Math.Exp(-Math.Pow((longitude - 82) / 22, 2)));
        return january + ((july - january) * summer);
    }

    /// <summary>Sea-level pressure at a place and time of year on the monsoon map, hPa.</summary>
    public static double Pressure(double latitude, double longitude, double month)
    {
        var summer = Summer(month);
        // The subtropical high belts either side of the equatorial trough drive the trade winds.
        var p = Background - (4 * Math.Exp(-Math.Pow((latitude - TroughLatitude(longitude, month)) / 8, 2)))
            + (3 * Math.Exp(-Math.Pow((latitude - 30) / 12, 2)))
            + ((4 + (2 * summer)) * Math.Exp(-Math.Pow((latitude + 30) / 12, 2)))
            // Across the equator the pressure falls toward the summer hemisphere, which pulls the air over it.
            - ((2 * summer) - 1) * 2 * Math.Tanh(latitude / 10);
        foreach (var system in Systems)
        {
            var dx = (longitude - system.Longitude(summer)) / system.SpreadLongitude;
            var dy = (latitude - system.Latitude(summer)) / system.SpreadLatitude;
            p += system.Anomaly(summer) * Math.Exp(-((dx * dx) + (dy * dy)));
        }

        return p;
    }

    /// <summary>The pressure at the centre of <paramref name="system"/> at <paramref name="month"/>, hPa.</summary>
    public static double CentralPressure(PressureSystem system, double month)
    {
        var summer = Summer(month);
        return Pressure(system.Latitude(summer), system.Longitude(summer), month);
    }

    /// <summary>
    /// The surface wind on the monsoon map, m/s east (<c>U</c>) and north (<c>V</c>). Air is pushed from high to low
    /// pressure; away from the equator Earth's turning bends it to the right in the north and to the left in the south,
    /// so it ends up blowing at an angle across the isobars (ground friction keeps it from going all the way round to
    /// along them). At the equator there is no bending and the wind runs straight down the pressure slope, which is why
    /// the south-east trades turn into the south-west monsoon as they cross into the north.
    /// </summary>
    public static (double U, double V) Wind(double latitude, double longitude, double month)
    {
        const double Step = 0.5;
        var kmPerDegree = 111.2;
        var cos = Math.Max(0.2, Math.Cos(latitude * Math.PI / 180));
        var dpdx = (Pressure(latitude, longitude + Step, month) - Pressure(latitude, longitude - Step, month)) / (2 * Step * kmPerDegree * cos);
        var dpdy = (Pressure(latitude + Step, longitude, month) - Pressure(latitude - Step, longitude, month)) / (2 * Step * kmPerDegree);

        // The push: down the pressure slope, hPa per 100 km.
        var gx = -dpdx * 100;
        var gy = -dpdy * 100;

        var turn = TurnAngle(latitude) * Math.PI / 180;
        var (sin, cosTurn) = Math.SinCos(turn);
        double u, v;
        if (latitude >= 0)
        {
            u = (gx * cosTurn) + (gy * sin);
            v = (-gx * sin) + (gy * cosTurn);
        }
        else
        {
            u = (gx * cosTurn) - (gy * sin);
            v = (gx * sin) + (gy * cosTurn);
        }

        // Geostrophic-like strength: weaker Coriolis near the equator lets the same slope drive a faster wind.
        var coriolis = Math.Max(Math.Sin(Math.Abs(latitude) * Math.PI / 180), 0.3);
        var scale = 3.2 / coriolis;
        return (u * scale, v * scale);
    }

    /// <summary>How far the wind is turned from straight down the pressure slope, degrees: none at the equator, 55° beyond 15°.</summary>
    public static double TurnAngle(double latitude) => 55 * Smooth(0, 15, Math.Abs(latitude));

    /// <summary>Where a wind of <paramref name="u"/>, <paramref name="v"/> comes from: 0 = north, 1 = north-east, … 7 = north-west.</summary>
    public static int CompassFrom(double u, double v)
    {
        var bearing = Math.Atan2(-u, -v) * 180 / Math.PI;
        if (bearing < 0) bearing += 360;
        return (int)Math.Round(bearing / 45) % 8;
    }

    /// <summary>The compass points' keys in <see cref="CompassFrom"/> order.</summary>
    public static IReadOnlyList<string> CompassKeys { get; } = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];

    /// <summary>The month (1–12) that <paramref name="month"/> falls in.</summary>
    public static int MonthNumber(double month) => ((int)Math.Floor(((month % 12) + 12) % 12)) + 1;

    /// <summary>
    /// Typical monthly rainfall of the monsoon view's cities, mm (rounded from long-term climate normals; real years vary a
    /// lot). Mumbai is dry for half the year and gets most of its rain in four months; Darwin, south of the equator, has
    /// its wet season at the other end of the year.
    /// </summary>
    public static IReadOnlyList<MonsoonCityInfo> Cities { get; } =
    [
        new(MonsoonCity.Mumbai, 19.08, 72.88, [1, 1, 0, 1, 12, 520, 840, 560, 330, 80, 20, 5]),
        new(MonsoonCity.Guangzhou, 23.13, 113.26, [45, 65, 90, 190, 290, 320, 250, 260, 190, 70, 40, 35]),
        new(MonsoonCity.Shanghai, 31.23, 121.47, [75, 60, 100, 85, 100, 230, 150, 210, 110, 60, 55, 45]),
        new(MonsoonCity.Beijing, 39.90, 116.40, [3, 5, 9, 25, 35, 75, 190, 160, 50, 25, 10, 3]),
        new(MonsoonCity.Darwin, -12.46, 130.84, [420, 370, 320, 100, 20, 1, 1, 5, 15, 70, 140, 250]),

        // Inland, about 1450 mm a year: spring rains and the June plum rains, then a drier, hot late summer.
        new(MonsoonCity.Changsha, 28.23, 112.94, [80, 90, 150, 170, 200, 240, 160, 115, 75, 75, 75, 50]),
    ];

    /// <summary>The monsoon city at a place (within about 50 km), if it is one of them.</summary>
    public static MonsoonCity? CityNear(double latitude, double longitude) => Cities
        .Where(c => Math.Abs(c.Latitude - latitude) < 0.5 && Math.Abs(c.Longitude - longitude) < 0.5)
        .Select(c => (MonsoonCity?)c.Id)
        .FirstOrDefault();

    public static MonsoonCityInfo City(MonsoonCity id) => Cities.First(c => c.Id == id);

    /// <summary>A city's rainfall in the month that <paramref name="month"/> falls in, mm.</summary>
    public static double RainIn(MonsoonCity id, double month) => City(id).Rain[MonthNumber(month) - 1];

    #endregion

    /// <summary>0 below <paramref name="a"/>, 1 above <paramref name="b"/>, smooth between.</summary>
    public static double Smooth(double a, double b, double value)
    {
        var t = Math.Clamp((value - a) / (b - a), 0, 1);
        return t * t * (3 - (2 * t));
    }
}
