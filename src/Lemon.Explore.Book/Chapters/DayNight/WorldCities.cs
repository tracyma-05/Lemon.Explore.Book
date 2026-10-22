using Lemon.Explore.Book.Explore.Places;

namespace Lemon.Explore.Book.Chapters.DayNight;

/// <summary>Which side of its dot a city's name is drawn on, so neighbours do not overlap.</summary>
public enum LabelSide
{
    Right,
    Left,
    Above,
    Below,
}

/// <summary>
/// A city on the map with its real time zone (IANA id). <see cref="StandardOffset"/> is only used when the system
/// does not know the zone.
/// </summary>
public sealed record WorldCity(string Id, double Latitude, double Longitude, string TimeZoneId, double StandardOffset, LabelSide Label = LabelSide.Right)
{
    private TimeZoneInfo? _zone;

    public GeoPoint Location => new(Latitude, Longitude);

    /// <summary>The localized name's resource key.</summary>
    public string NameKey { get; init; } = $"DayNight_City_{Id}";

    public TimeZoneInfo TimeZone => _zone ??= WorldCities.FindZone(TimeZoneId, StandardOffset);
}

/// <summary>The cities on the day-and-night map, chosen to show one thing each about clocks and the Sun.</summary>
public static class WorldCities
{
    public static IReadOnlyList<WorldCity> All { get; } =
    [
        new("Beijing", 39.90, 116.40, "Asia/Shanghai", 8),

        // Far west of China on Beijing time: solar noon comes close to 15:00 by the clock.
        new("Kashgar", 39.47, 75.99, "Asia/Shanghai", 8, LabelSide.Above),
        new("Tokyo", 35.68, 139.69, "Asia/Tokyo", 9),
        new("Singapore", 1.35, 103.82, "Asia/Singapore", 8),
        new("Sydney", -33.87, 151.21, "Australia/Sydney", 10, LabelSide.Left),

        // Among the first to see a new day, and with daylight saving in the southern summer.
        new("Auckland", -36.85, 174.76, "Pacific/Auckland", 12, LabelSide.Below),

        // UTC+14 at the same longitude as Honolulu (UTC−10): the same clock time, a day apart.
        new("Kiritimati", 1.87, -157.36, "Pacific/Kiritimati", 14),
        new("Honolulu", 21.31, -157.86, "Pacific/Honolulu", -10),
        new("LosAngeles", 34.05, -118.24, "America/Los_Angeles", -8, LabelSide.Left),
        new("NewYork", 40.71, -74.01, "America/New_York", -5),
        new("SaoPaulo", -23.55, -46.63, "America/Sao_Paulo", -3),
        new("Reykjavik", 64.15, -21.94, "Atlantic/Reykjavik", 0, LabelSide.Left),
        new("London", 51.51, -0.13, "Europe/London", 0, LabelSide.Left),

        // West of Greenwich but on Central European Time: late sunrises and late sunsets.
        new("Madrid", 40.42, -3.70, "Europe/Madrid", 1, LabelSide.Left),
        new("Tromso", 69.65, 18.96, "Europe/Oslo", 1, LabelSide.Above),
        new("Cairo", 30.04, 31.24, "Africa/Cairo", 2, LabelSide.Left),
        new("Moscow", 55.76, 37.62, "Europe/Moscow", 3),
        new("CapeTown", -33.92, 18.42, "Africa/Johannesburg", 2, LabelSide.Left),
        new("Dubai", 25.20, 55.27, "Asia/Dubai", 4, LabelSide.Below),

        // Half-hour and quarter-hour zones.
        new("NewDelhi", 28.61, 77.21, "Asia/Kolkata", 5.5, LabelSide.Below),
        new("Kathmandu", 27.72, 85.32, "Asia/Kathmandu", 5.75, LabelSide.Above),
    ];

    /// <summary>
    /// The location picker's places that are not on the map (e.g. Shanghai), so the page can start at the user's
    /// place. They are found by <see cref="Find"/> but not drawn as map cities or listed as clocks.
    /// </summary>
    private static readonly WorldCity[] PickerCities = ExplorePlaces.All
        .Where(p => All.All(c => c.Id != p.Id))
        .Select(p => new WorldCity(p.Id, p.Latitude, p.Longitude, p.TimeZoneId, p.StandardOffset) { NameKey = p.NameKey })
        .ToArray();

    public static WorldCity? Find(string? id) => All.FirstOrDefault(c => c.Id == id) ?? PickerCities.FirstOrDefault(c => c.Id == id);

    /// <summary>The system's zone for an IANA id, or a fixed offset when the system does not have it.</summary>
    internal static TimeZoneInfo FindZone(string id, double standardOffset)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone)) return zone;
        return Fixed(TimeSpan.FromHours(standardOffset));
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<TimeSpan, TimeZoneInfo> FixedZones = new();

    /// <summary>A zone without daylight saving, e.g. the ideal zone of a meridian.</summary>
    public static TimeZoneInfo Fixed(TimeSpan offset) => FixedZones.GetOrAdd(offset, o =>
    {
        var name = OffsetText(o);
        return TimeZoneInfo.CreateCustomTimeZone(name, o, name, name);
    });

    /// <summary>"UTC+8", "UTC+5:30", "UTC−3" or "UTC".</summary>
    public static string OffsetText(TimeSpan offset)
    {
        if (offset == TimeSpan.Zero) return "UTC";

        var sign = offset < TimeSpan.Zero ? "−" : "+";
        var abs = offset.Duration();
        return abs.Minutes == 0 ? $"UTC{sign}{(int)abs.TotalHours}" : $"UTC{sign}{(int)abs.TotalHours}:{abs.Minutes:00}";
    }
}
