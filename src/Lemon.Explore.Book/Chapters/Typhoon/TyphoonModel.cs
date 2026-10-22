using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Typhoon;

/// <summary>The views of the typhoon page.</summary>
public enum TyphoonMode
{
    /// <summary>The storm from above: eye, eyewall, spiral rain bands, the winds and the pressure.</summary>
    Storm,

    /// <summary>A slice through the storm: warm sea, inflow, the eyewall's towers, outflow: a heat engine.</summary>
    Engine,

    /// <summary>A map of the north-west Pacific: where a typhoon goes, steered by the subtropical high, and where it lands.</summary>
    Track,
}

/// <summary>China's grades of tropical cyclones by the strongest 2-minute mean wind near the centre (GB/T 19201-2006).</summary>
public enum StormGrade
{
    None,
    Depression,
    TropicalStorm,
    SevereTropicalStorm,
    Typhoon,
    SevereTyphoon,
    SuperTyphoon,
}

/// <summary>
/// One tropical cyclone's winds and pressure (Holland 1980 profile, with the strongest wind from the north-west Pacific
/// wind–pressure relation of Atkinson and Holliday 1977), its grade and Beaufort force, how strong the sea lets it get
/// (DeMaria and Kaplan 1994) and how it dies over land (Kaplan and DeMaria 1995).
/// </summary>
public static class StormModel
{
    /// <summary>The pressure around the storm, hPa.</summary>
    public const double AmbientPressure = 1010;

    public const double AirDensity = 1.15;

    /// <summary>The sea must be at least this warm for a typhoon to form, °C.</summary>
    public const double GenesisSea = 26.5;

    /// <summary>The air spirals in at about this angle across the isobars near the surface, degrees.</summary>
    public const double InflowAngle = 20;

    private const double Omega = 7.2921e-5;

    /// <summary>The strongest wind for a central pressure, m/s (Atkinson–Holliday: 6.7 kt × (1010 − p)^0.644).</summary>
    public static double MaxWind(double centralPressure) => 3.447 * Math.Pow(Math.Max(0, AmbientPressure - centralPressure), 0.644);

    /// <summary>The central pressure that goes with a strongest wind, hPa.</summary>
    public static double CentralPressure(double maxWind) => AmbientPressure - Math.Pow(Math.Max(0, maxWind) / 3.447, 1 / 0.644);

    /// <summary>
    /// The radius of the strongest winds, km (Vickery et al. 2000: ln R = 2.636 − 0.00005086 Δp² + 0.0394899 φ):
    /// deeper storms are tighter, storms further from the equator wider.
    /// </summary>
    public static double RadiusOfMaxWind(double centralPressure, double latitude = 20)
    {
        var dp = Math.Max(5, AmbientPressure - centralPressure);
        return Math.Clamp(Math.Exp(2.636 - (0.00005086 * dp * dp) + (0.0394899 * Math.Abs(latitude))), 10, 120);
    }

    /// <summary>The calm eye's radius, km: about two thirds of the way to the strongest winds.</summary>
    public static double EyeRadius(double centralPressure, double latitude = 20) => 0.65 * RadiusOfMaxWind(centralPressure, latitude);

    /// <summary>Holland's B, chosen so the profile's peak is <see cref="MaxWind"/>.</summary>
    public static double HollandB(double centralPressure)
    {
        var dp = Math.Max(1, AmbientPressure - centralPressure) * 100;
        var v = MaxWind(centralPressure);
        return Math.Clamp(AirDensity * Math.E * v * v / dp, 0.8, 2.5);
    }

    /// <summary>The Coriolis parameter, 1/s (2Ω sin φ).</summary>
    public static double Coriolis(double latitude) => 2 * Omega * Math.Sin(latitude * Math.PI / 180);

    /// <summary>The pressure at a distance from the centre, hPa.</summary>
    public static double Pressure(double km, double centralPressure, double latitude = 20)
    {
        var r = Math.Max(0.01, km);
        var rmax = RadiusOfMaxWind(centralPressure, latitude);
        var b = HollandB(centralPressure);
        return centralPressure + ((AmbientPressure - centralPressure) * Math.Exp(-Math.Pow(rmax / r, b)));
    }

    /// <summary>How far out the pressure reaches <paramref name="hPa"/>, km (∞ if it never does).</summary>
    public static double RadiusOfPressure(double hPa, double centralPressure, double latitude = 20)
    {
        var dp = AmbientPressure - centralPressure;
        var share = (hPa - centralPressure) / dp;
        if (share <= 0) return 0;
        if (share >= 1) return double.PositiveInfinity;
        return RadiusOfMaxWind(centralPressure, latitude) / Math.Pow(-Math.Log(share), 1 / HollandB(centralPressure));
    }

    /// <summary>
    /// The wind going round the centre at a distance, m/s: the gradient wind of the Holland profile (pressure push
    /// balanced by the turning and the Coriolis force).
    /// </summary>
    public static double Wind(double km, double centralPressure, double latitude = 20)
    {
        var r = Math.Max(0.01, km) * 1000;
        var rmax = RadiusOfMaxWind(centralPressure, latitude) * 1000;
        var b = HollandB(centralPressure);
        var dp = Math.Max(0, AmbientPressure - centralPressure) * 100;
        var x = Math.Pow(rmax / r, b);
        var half = r * Math.Abs(Coriolis(latitude)) / 2;
        return Math.Sqrt((b / AirDensity * dp * x * Math.Exp(-x)) + (half * half)) - half;
    }

    /// <summary>The wind's angle in across the isobars at a distance, degrees: none in the eye, <see cref="InflowAngle"/> outside.</summary>
    public static double Inflow(double km, double centralPressure, double latitude = 20) =>
        InflowAngle * Smooth(0.8, 1.4, km / RadiusOfMaxWind(centralPressure, latitude));

    /// <summary>China's grade for a strongest wind, m/s.</summary>
    public static StormGrade Grade(double maxWind) => maxWind switch
    {
        >= 51.0 => StormGrade.SuperTyphoon,
        >= 41.5 => StormGrade.SevereTyphoon,
        >= 32.7 => StormGrade.Typhoon,
        >= 24.5 => StormGrade.SevereTropicalStorm,
        >= 17.2 => StormGrade.TropicalStorm,
        >= 10.8 => StormGrade.Depression,
        _ => StormGrade.None,
    };

    /// <summary>The lower edges of the Beaufort forces 1…17 as China uses them, m/s.</summary>
    private static readonly double[] BeaufortEdges = [0.3, 1.6, 3.4, 5.5, 8.0, 10.8, 13.9, 17.2, 20.8, 24.5, 28.5, 32.7, 37.0, 41.5, 46.2, 51.0, 56.1];

    /// <summary>The Beaufort force (0…17; 18 means beyond 17, over 61.2 m/s).</summary>
    public static int Beaufort(double speed)
    {
        if (speed >= 61.3) return 18;
        var force = 0;
        while (force < BeaufortEdges.Length && speed >= BeaufortEdges[force]) force++;
        return force;
    }

    /// <summary>
    /// The strongest wind the sea lets a storm reach, m/s (DeMaria and Kaplan 1994: 28.2 + 55.8 e^(0.1813 (T − 30)));
    /// none below <see cref="GenesisSea"/>.
    /// </summary>
    public static double PotentialIntensity(double seaCelsius) =>
        seaCelsius < GenesisSea ? 0 : 28.2 + (55.8 * Math.Exp(0.1813 * (seaCelsius - 30)));

    /// <summary>
    /// The wind <paramref name="hours"/> after landfall, m/s (Kaplan and DeMaria 1995: it falls towards 13.7 m/s by
    /// 9.5% an hour, after losing a tenth at the coast).
    /// </summary>
    public static double AfterLandfall(double landfallWind, double hours)
    {
        if (hours <= 0) return landfallWind;
        const double background = 13.7;
        var start = Math.Max(background, 0.9 * landfallWind);
        return Math.Min(landfallWind, background + ((start - background) * Math.Exp(-0.095 * hours)));
    }

    public static double Smooth(double a, double b, double value)
    {
        var t = Math.Clamp((value - a) / (b - a), 0, 1);
        return t * t * (3 - (2 * t));
    }
}

/// <summary>Where a typhoon starts.</summary>
public readonly record struct TrackOptions(double StartLatitude, double StartLongitude, double RidgeWest, int Month);

/// <summary>A moment of a storm's life: hours since it formed, where it is, its strongest wind (m/s), over land or sea.</summary>
public readonly record struct TrackPoint(double Hour, double Latitude, double Longitude, double Wind, bool OverLand);

/// <summary>A coast the storm reached: the point's index on the track, and the nearest city of the list.</summary>
public readonly record struct Landfall(int Index, CoastCity City);

/// <summary>A city near the coast, for naming landfalls (its localized name is <c>Typhoon_City_{Id}</c>).</summary>
public sealed record CoastCity(string Id, double Latitude, double Longitude);

/// <summary>
/// The track of a typhoon over the north-west Pacific. The storm is carried by the wind around the subtropical high
/// (easterlies on its south side, southerlies at its west end, westerlies north of it), drifts a little north-west by
/// itself (the "beta drift"), grows over sea warmer than 26.5 °C towards what the sea allows, and dies over land.
/// The high's ridge is a long band at the month's latitude whose west end is set by <see cref="TrackOptions.RidgeWest"/>.
/// </summary>
public static class TrackModel
{
    public const double West = 100;
    public const double East = 160;
    public const double South = 0;
    public const double North = 45;

    /// <summary>How strong the subtropical high is over its surroundings, hPa (as drawn), and its north–south half-width, degrees.</summary>
    private const double HighStrength = 12;

    private const double HighWidth = 11;

    /// <summary>Steering wind per hPa per degree of the high, m/s.</summary>
    private const double SteeringGain = 7;

    public static IReadOnlyList<int> Months { get; } = [6, 7, 8, 9, 10];

    /// <summary>The latitude of the subtropical high's ridge in a month (June about 22° N, August about 29° N).</summary>
    public static double RidgeLatitude(int month) => month switch
    {
        6 => 22,
        7 => 26,
        8 => 29,
        9 => 27,
        _ => 23,
    };

    /// <summary>The sea's surface temperature, °C: about 29.6 in the tropics, cooling north of 18–22° N (later in summer further north).</summary>
    public static double SeaTemperature(double latitude, int month)
    {
        var shift = month switch
        {
            7 => 2,
            8 => 4,
            9 => 3,
            _ => 0,
        };
        return 29.6 - (0.32 * Math.Max(0, latitude - (18 + shift)));
    }

    /// <summary>The latitude where the sea is 26.5 °C, the edge of where typhoons can grow.</summary>
    public static double GenesisEdge(int month) =>
        18 + (month switch { 7 => 2, 8 => 4, 9 => 3, _ => 0 }) + ((29.6 - StormModel.GenesisSea) / 0.32);

    /// <summary>The subtropical high's pressure above its surroundings, hPa.</summary>
    public static double HighPressure(double latitude, double longitude, double ridgeWest, int month)
    {
        var y = latitude - RidgeLatitude(month);
        var tip = ridgeWest + 8;
        var along = longitude >= tip ? 1 : Math.Exp(-Math.Pow((longitude - tip) / 14, 2));
        return HighStrength * Math.Exp(-Math.Pow(y / HighWidth, 2)) * along;
    }

    /// <summary>
    /// The wind that steers the storm, m/s east and north: round the high (clockwise), the westerlies north of it, and
    /// the storm's own drift north-west.
    /// </summary>
    public static (double U, double V) Steering(double latitude, double longitude, double ridgeWest, int month)
    {
        const double h = 0.05;
        var dy = (HighPressure(latitude + h, longitude, ridgeWest, month) - HighPressure(latitude - h, longitude, ridgeWest, month)) / (2 * h);
        var dx = (HighPressure(latitude, longitude + h, ridgeWest, month) - HighPressure(latitude, longitude - h, ridgeWest, month)) / (2 * h);
        var u = -SteeringGain * dy;
        var v = SteeringGain * dx;

        // The westerlies, from about 6° north of the ridge.
        u += 12 * StormModel.Smooth(RidgeLatitude(month) + 6, RidgeLatitude(month) + 16, latitude);

        // Beta drift: a storm creeps north-west on its own.
        return (u - 1.0, v + 1.6);
    }

    public static IReadOnlyList<CoastCity> Cities { get; } =
    [
        new("Haikou", 20.04, 110.20),
        new("Zhanjiang", 21.27, 110.36),
        new("Yangjiang", 21.86, 111.98),
        new("HongKong", 22.32, 114.17),
        new("Shantou", 23.35, 116.68),
        new("Xiamen", 24.48, 118.09),
        new("Fuzhou", 26.07, 119.30),
        new("Wenzhou", 28.00, 120.67),
        new("Ningbo", 29.87, 121.55),
        new("Shanghai", 31.23, 121.47),
        new("Qingdao", 36.07, 120.38),
        new("Taipei", 25.03, 121.56),
        new("Hualien", 23.99, 121.60),
        new("Manila", 14.60, 120.98),
        new("Haiphong", 20.86, 106.68),
        new("DaNang", 16.05, 108.20),
        new("Naha", 26.21, 127.68),
        new("Kagoshima", 31.60, 130.56),
        new("Busan", 35.18, 129.08),
        new("Osaka", 34.69, 135.50),
        new("Tokyo", 35.68, 139.69),
    ];

    /// <summary>The ready-made places a storm can start: near Guam, east of the Philippines, in the South China Sea.</summary>
    public static IReadOnlyDictionary<string, (double Latitude, double Longitude)> Starts { get; } = new Dictionary<string, (double, double)>
    {
        ["Guam"] = (13.5, 144),
        ["Philippines"] = (12.5, 132),
        ["SouthChinaSea"] = (15.5, 116),
    };

    /// <summary>Is a point land (on the 1:110m map)?</summary>
    public static bool IsLand(double latitude, double longitude) => WorldMap.LandMask.Sample(latitude, longitude) > 0.5;

    private static readonly Dictionary<TrackOptions, TrackPoint[]> Tracks = [];

    /// <summary>The storm's life hour by hour, worked out once per set of options.</summary>
    public static TrackPoint[] Track(TrackOptions options)
    {
        options = options with
        {
            StartLatitude = Math.Round(options.StartLatitude, 1),
            StartLongitude = Math.Round(options.StartLongitude, 1),
            RidgeWest = Math.Round(options.RidgeWest, 1),
        };
        lock (Tracks)
        {
            if (Tracks.TryGetValue(options, out var track)) return track;
            if (Tracks.Count > 64) Tracks.Clear();
            return Tracks[options] = Simulate(options);
        }
    }

    private static TrackPoint[] Simulate(TrackOptions o)
    {
        var points = new List<TrackPoint>();
        double latitude = o.StartLatitude, longitude = o.StartLongitude, wind = 15;
        double landedAt = -1, landfallWind = 0, hoursOnLand = 0;
        for (var hour = 0; hour <= 14 * 24; hour++)
        {
            var land = IsLand(latitude, longitude);
            points.Add(new TrackPoint(hour, latitude, longitude, wind, land));

            if (land)
            {
                if (landedAt < 0)
                {
                    landedAt = hour;
                    landfallWind = wind;
                }

                hoursOnLand++;
                wind = StormModel.AfterLandfall(landfallWind, hour + 1 - landedAt);
            }
            else
            {
                landedAt = -1;
                var ceiling = 0.85 * StormModel.PotentialIntensity(SeaTemperature(latitude, o.Month));
                if (ceiling > wind) wind += 0.03 * wind * (1 - (wind / ceiling));
                else wind += (Math.Max(ceiling, 8) - wind) / 24;
            }

            // It ends when it has died down, spent two days over land, or gone north into the westerlies' cold air.
            if (wind < 12 || hoursOnLand > 48 || latitude > 40) break;

            var (u, v) = Steering(latitude, longitude, o.RidgeWest, o.Month);
            latitude += v * 3600 / 111_195;
            longitude += u * 3600 / (111_195 * Math.Cos(latitude * Math.PI / 180));
            if (longitude < West || longitude > East || latitude < South || latitude > North) break;
        }

        return [.. points];
    }

    /// <summary>The storm at an hour, between the track's points.</summary>
    public static TrackPoint At(IReadOnlyList<TrackPoint> track, double hour)
    {
        if (track.Count == 0) return default;
        hour = Math.Clamp(hour, 0, track[^1].Hour);
        var i = Math.Min(track.Count - 2, (int)Math.Floor(hour));
        if (i < 0) return track[0];
        var a = track[i];
        var b = track[i + 1];
        var t = hour - a.Hour;
        return new TrackPoint(hour, a.Latitude + ((b.Latitude - a.Latitude) * t), a.Longitude + ((b.Longitude - a.Longitude) * t),
            a.Wind + ((b.Wind - a.Wind) * t), t < 0.5 ? a.OverLand : b.OverLand);
    }

    /// <summary>The coasts the storm reached as at least a tropical storm.</summary>
    public static IReadOnlyList<Landfall> Landfalls(IReadOnlyList<TrackPoint> track)
    {
        var result = new List<Landfall>();
        for (var i = 1; i < track.Count; i++)
        {
            if (!track[i].OverLand || track[i - 1].OverLand || track[i - 1].Wind < 17.2) continue;
            var city = Cities.MinBy(c => Distance(c.Latitude, c.Longitude, track[i].Latitude, track[i].Longitude))!;
            result.Add(new Landfall(i, city));
        }

        return result;
    }

    /// <summary>The storm's closest pass to a place: the track index, the distance (km) and the wind there then (m/s).</summary>
    public static (int Index, double Distance, double Wind) ClosestPass(IReadOnlyList<TrackPoint> track, double latitude, double longitude)
    {
        var best = (Index: 0, Distance: double.MaxValue, Wind: 0.0);
        for (var i = 0; i < track.Count; i++)
        {
            var d = Distance(latitude, longitude, track[i].Latitude, track[i].Longitude);
            if (d < best.Distance) best = (i, d, 0);
        }

        if (track.Count == 0) return best;
        var p = track[best.Index];
        var pressure = StormModel.CentralPressure(p.Wind);
        return (best.Index, best.Distance, StormModel.Wind(best.Distance, pressure, p.Latitude));
    }

    /// <summary>Great-circle distance, km.</summary>
    public static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        const double rad = Math.PI / 180;
        var dlat = (lat2 - lat1) * rad;
        var dlon = (lon2 - lon1) * rad;
        var a = (Math.Sin(dlat / 2) * Math.Sin(dlat / 2)) + (Math.Cos(lat1 * rad) * Math.Cos(lat2 * rad) * Math.Sin(dlon / 2) * Math.Sin(dlon / 2));
        return 2 * 6371 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>
    /// Average named storms per month over the north-west Pacific (Japan Meteorological Agency, 1991–2020, rounded):
    /// about 25 a year, most in August and September.
    /// </summary>
    public static IReadOnlyList<double> StormsPerMonth { get; } = [0.3, 0.3, 0.3, 0.6, 1.0, 1.7, 3.7, 5.7, 5.0, 3.4, 2.2, 1.0];
}
