using System.Globalization;
using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Infrastructures.Localization;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Explore.Places;

/// <summary>
/// The place the Explore pages are watched from: one of <see cref="ExplorePlaces.All"/>, or a custom point
/// (<see cref="ExplorePlaces.CustomId"/>). <see cref="TimeZone"/> is the city's real zone (IANA id), or for a custom
/// point the ideal zone of its meridian.
/// </summary>
public sealed record ExplorePlace(string Id, double Latitude, double Longitude, double Elevation, string TimeZoneId, double StandardOffset)
{
    public bool IsCustom => Id == ExplorePlaces.CustomId;

    /// <summary>The localized name's resource key.</summary>
    public string NameKey => $"Location_City_{Id}";

    /// <summary>The localized name, e.g. 北京 or Beijing; a custom point is called "custom location".</summary>
    public string Name => LocalizationService.Instance.GetString(NameKey);

    /// <remarks>Not cached in a field, so two places with the same values stay equal records.</remarks>
    public TimeZoneInfo TimeZone => IsCustom
        ? WorldCities.Fixed(TimeSpan.FromHours(StandardOffset))
        : WorldCities.FindZone(TimeZoneId, StandardOffset);

    /// <summary>Where it is, e.g. 39.9° N, 116.4° E.</summary>
    public string CoordinatesText => LocalizationService.Instance.Format(
        "Location_Coordinates",
        LatitudeText(Latitude),
        LongitudeText(Longitude));

    /// <summary>Gravity here: the WGS 84 normal gravity at the latitude, less 3.086 µm/s² per metre of height.</summary>
    public double Gravity => ExplorePlaces.Gravity(Latitude, Elevation);

    /// <summary>The same place as a city on the day-and-night map; a custom point has none.</summary>
    public WorldCity? AsWorldCity => IsCustom ? null : WorldCities.Find(Id);

    /// <summary>e.g. 39.9° N, 北纬 39.9°.</summary>
    public static string LatitudeText(double latitude) => Math.Abs(latitude) < 0.05
        ? LocalizationService.Instance.GetString("Location_Equator")
        : LocalizationService.Instance.Format(latitude > 0 ? "Location_North" : "Location_South", Number(Math.Abs(latitude)));

    /// <summary>e.g. 116.4° E, 东经 116.4°.</summary>
    public static string LongitudeText(double longitude) => Math.Abs(longitude) < 0.05
        ? Number(0) + "°"
        : LocalizationService.Instance.Format(longitude > 0 ? "Location_East" : "Location_West", Number(Math.Abs(longitude)));

    private static string Number(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
}

/// <summary>The preset places of the location picker, Beijing first (the default).</summary>
public static class ExplorePlaces
{
    public const string CustomId = "Custom";

    public const string DefaultId = "Beijing";

    public static IReadOnlyList<ExplorePlace> All { get; } =
    [
        new("Beijing", 39.90, 116.40, 44, "Asia/Shanghai", 8),
        new("Changsha", 28.23, 112.94, 45, "Asia/Shanghai", 8),
        new("Shanghai", 31.23, 121.47, 4, "Asia/Shanghai", 8),
        new("Guangzhou", 23.13, 113.26, 21, "Asia/Shanghai", 8),
        new("Wuhan", 30.59, 114.31, 23, "Asia/Shanghai", 8),
        new("Chengdu", 30.66, 104.07, 500, "Asia/Shanghai", 8),
        new("Kunming", 25.04, 102.71, 1890, "Asia/Shanghai", 8),
        new("Harbin", 45.80, 126.53, 140, "Asia/Shanghai", 8),
        new("HongKong", 22.32, 114.17, 30, "Asia/Hong_Kong", 8),
        new("Singapore", 1.35, 103.82, 15, "Asia/Singapore", 8),
        new("Tokyo", 35.68, 139.69, 40, "Asia/Tokyo", 9),
        new("Sydney", -33.87, 151.21, 30, "Australia/Sydney", 10),
        new("London", 51.51, -0.13, 20, "Europe/London", 0),
        new("NewYork", 40.71, -74.01, 10, "America/New_York", -5),
    ];

    public static ExplorePlace Default => All[0];

    public static ExplorePlace? Find(string? id) => All.FirstOrDefault(p => p.Id == id);

    /// <summary>A point of the user's own, on the ideal time zone of its meridian.</summary>
    public static ExplorePlace Custom(double latitude, double longitude)
    {
        latitude = Math.Round(Math.Clamp(latitude, -90, 90), 2);
        longitude = Math.Round(NormalizeLongitude(longitude), 2);
        return new ExplorePlace(CustomId, latitude, longitude, 0, string.Empty, Math.Round(longitude / 15));
    }

    /// <summary>
    /// Normal gravity on the WGS 84 ellipsoid (Somigliana's formula) at <paramref name="latitude"/>, less the
    /// free-air drop of 3.086 µm/s² per metre of <paramref name="elevation"/>: 9.780 m/s² at the equator, 9.832 at a pole.
    /// </summary>
    public static double Gravity(double latitude, double elevation = 0)
    {
        var sin = Math.Sin(Math.Clamp(latitude, -90, 90) * Math.PI / 180);
        var s2 = sin * sin;
        return (9.7803253359 * (1 + (0.00193185265241 * s2)) / Math.Sqrt(1 - (0.00669437999013 * s2))) - (3.086e-6 * elevation);
    }

    internal static double NormalizeLongitude(double longitude)
    {
        longitude %= 360;
        if (longitude > 180) longitude -= 360;
        if (longitude < -180) longitude += 360;
        return longitude;
    }
}
