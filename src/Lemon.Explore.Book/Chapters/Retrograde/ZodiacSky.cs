using Lemon.Explore.Book.Chapters.StarTrails;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Retrograde;

/// <summary>A star placed in ecliptic coordinates (degrees) for the retrograde page's strip of sky.</summary>
public sealed record EclipticStar(string Id, double Longitude, double Latitude, double Magnitude, double ColorIndex);

/// <summary>
/// The band of sky along the ecliptic that the planets wander through: the real bright stars of the star-trails catalogue
/// that lie within 25° of it, a few more stars of the fainter zodiac constellations, lines for the zodiac figures, and
/// where each constellation's stretch of the ecliptic begins.
/// </summary>
/// <remarks>
/// The extra stars are J2000 catalogue positions typed in from memory (good to about a tenth of a degree). The
/// boundaries are where the IAU constellation borders cross the ecliptic (J2000), rounded to a tenth of a degree.
/// </remarks>
public static class ZodiacSky
{
    /// <summary>How far from the ecliptic the strip keeps stars, degrees.</summary>
    public const double BandHalfWidth = 25;

    /// <summary>Zodiac stars the star-trails catalogue does not have: id, RA hours, Dec degrees, magnitude, B−V.</summary>
    private static readonly (string Id, double Ra, double Dec, double Magnitude, double ColorIndex)[] Extra =
    [
        // Aries
        ("Sheratan", 1.9107, 20.808, 2.64, 0.13),
        ("Mesarthim", 1.8925, 19.296, 3.88, -0.04),

        // Taurus
        ("AinTau", 4.4769, 19.180, 3.53, 1.01),
        ("ZetTau", 5.6274, 21.143, 3.00, -0.19),

        // Gemini
        ("Mebsuta", 6.7322, 25.131, 3.06, 1.38),
        ("Tejat", 6.3827, 22.514, 2.87, 1.64),

        // Cancer
        ("Altarf", 8.2753, 9.186, 3.52, 1.48),
        ("AsellusAustralis", 8.7448, 18.154, 3.94, 1.08),
        ("AsellusBorealis", 8.7213, 21.469, 4.66, 0.02),
        ("Acubens", 8.9748, 11.858, 4.25, 0.14),

        // Virgo
        ("Porrima", 12.6943, -1.449, 2.74, 0.36),
        ("Vindemiatrix", 13.0363, 10.959, 2.85, 0.94),
        ("Zavijava", 11.8449, 1.765, 3.61, 0.55),

        // Libra
        ("Zubeneschamali", 15.2835, -9.383, 2.61, -0.11),

        // Capricornus
        ("Algedi", 20.3001, -12.545, 3.57, 0.94),
        ("Dabih", 20.3502, -14.781, 3.05, 0.79),
        ("Nashira", 21.6681, -16.662, 3.68, 0.32),
        ("DenebAlgedi", 21.7840, -16.127, 2.85, 0.29),

        // Aquarius
        ("Sadalsuud", 21.5260, -5.571, 2.90, 0.83),
        ("Sadalmelik", 22.0964, -0.320, 2.95, 0.98),
        ("Sadachbia", 22.3609, -1.387, 3.84, -0.05),
        ("Skat", 22.9108, -15.821, 3.27, 0.07),

        // Pisces
        ("Alpherg", 1.5248, 15.346, 3.62, 0.97),
        ("Alrescha", 2.0340, 2.764, 3.82, 0.03),
        ("GamPsc", 23.2861, 3.282, 3.69, 0.92),
    ];

    /// <summary>Lines of the zodiac figures, between ids of <see cref="Stars"/>.</summary>
    public static IReadOnlyList<(string A, string B)> Lines { get; } =
    [
        ("Hamal", "Sheratan"), ("Sheratan", "Mesarthim"),
        ("Aldebaran", "AinTau"), ("Aldebaran", "ZetTau"), ("AinTau", "Elnath"),
        ("Castor", "Pollux"), ("Castor", "Mebsuta"), ("Mebsuta", "Tejat"), ("Pollux", "Alhena"),
        ("Altarf", "AsellusAustralis"), ("AsellusAustralis", "Acubens"), ("AsellusAustralis", "AsellusBorealis"),
        ("Regulus", "EtaLeo"), ("EtaLeo", "Algieba"), ("Algieba", "Zosma"), ("Zosma", "Denebola"), ("Denebola", "Chertan"), ("Chertan", "Regulus"),
        ("Zavijava", "Porrima"), ("Porrima", "Spica"), ("Porrima", "Vindemiatrix"),
        ("Zubenelgenubi", "Zubeneschamali"),
        ("Acrab", "Dschubba"), ("Dschubba", "PiSco"), ("Dschubba", "SigSco"), ("SigSco", "Antares"), ("Antares", "TauSco"), ("TauSco", "EpsSco"),
        ("KausAustralis", "KausMedia"), ("KausMedia", "KausBorealis"), ("KausAustralis", "Ascella"), ("Ascella", "Nunki"), ("Nunki", "KausBorealis"), ("KausMedia", "Alnasl"),
        ("Algedi", "Dabih"), ("Dabih", "Nashira"), ("Nashira", "DenebAlgedi"), ("DenebAlgedi", "Algedi"),
        ("Sadalsuud", "Sadalmelik"), ("Sadalmelik", "Sadachbia"), ("Sadachbia", "Skat"),
        ("GamPsc", "Alrescha"), ("Alrescha", "Alpherg"),
    ];

    /// <summary>
    /// The zodiac constellations in order along the ecliptic, with the longitude (degrees, J2000) where the ecliptic enters
    /// each; the name is <c>Retro_Zodiac_&lt;Id&gt;</c>.
    /// </summary>
    public static IReadOnlyList<(string Id, double Start)> Constellations { get; } =
    [
        ("Pisces", 351.6),
        ("Aries", 28.7),
        ("Taurus", 53.4),
        ("Gemini", 90.1),
        ("Cancer", 118.0),
        ("Leo", 138.0),
        ("Virgo", 174.0),
        ("Libra", 217.8),
        ("Scorpius", 241.0),
        ("Ophiuchus", 248.0),
        ("Sagittarius", 266.3),
        ("Capricornus", 299.7),
        ("Aquarius", 327.5),
    ];

    /// <summary>The real stars within <see cref="BandHalfWidth"/> of the ecliptic, brightest first.</summary>
    public static IReadOnlyList<EclipticStar> Stars { get; } = Build();

    private static readonly Dictionary<string, EclipticStar> ById = Stars.ToDictionary(s => s.Id);

    public static EclipticStar? Find(string id) => ById.GetValueOrDefault(id);

    /// <summary>The zodiac constellation the ecliptic runs through at <paramref name="longitude"/>.</summary>
    public static string ConstellationAt(double longitude)
    {
        // Pisces is first and runs on past 0°, so anything before Aries' start is still Pisces.
        var lon = RetroModel.Wrap360(longitude);
        var found = Constellations[0].Id;
        for (var i = 1; i < Constellations.Count; i++)
        {
            if (Constellations[i].Start <= lon && lon < Constellations[0].Start) found = Constellations[i].Id;
        }

        return found;
    }

    /// <summary>The middle of a constellation's stretch of the ecliptic, degrees.</summary>
    public static double MiddleOf(string id)
    {
        var index = Constellations.ToList().FindIndex(c => c.Id == id);
        var start = Constellations[index].Start;
        var end = Constellations[(index + 1) % Constellations.Count].Start;
        var span = RetroModel.Wrap360(end - start);
        return RetroModel.Wrap360(start + (span / 2));
    }

    private static List<EclipticStar> Build()
    {
        var known = StarCatalog.Bright.Select(s => s.Id).ToHashSet();
        var all = StarCatalog.Bright.Select(s => (s.Id, s.Ra, s.Dec, s.Magnitude, s.ColorIndex))
            .Concat(Extra.Where(e => !known.Contains(e.Id)));
        return all
            .Select(s =>
            {
                var (lon, lat) = RetroModel.Ecliptic(s.Ra, s.Dec);
                return new EclipticStar(s.Id, lon, lat, s.Magnitude, s.ColorIndex);
            })
            .Where(s => Math.Abs(s.Latitude) <= BandHalfWidth)
            .OrderBy(s => s.Magnitude)
            .ToList();
    }
}
