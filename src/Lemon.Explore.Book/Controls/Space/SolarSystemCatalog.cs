using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Lemon.Explore.Book.Controls.Space;

/// <summary>
/// The Sun, the eight planets and their major moons. Physical data: NASA planetary and satellite fact sheets;
/// planet orbits: JPL (Standish).
/// </summary>
public static class SolarSystemCatalog
{
    private static readonly Lazy<IReadOnlyList<CelestialBody>> LazyBodies = new(Build);

    /// <summary>Sun first, then the planets outward. Moons hang off their planet's <see cref="CelestialBody.Satellites"/>.</summary>
    public static IReadOnlyList<CelestialBody> Bodies => LazyBodies.Value;

    /// <summary>Every body including the moons, for work that has to cover all of them (textures).</summary>
    public static IEnumerable<CelestialBody> AllBodies => Bodies.SelectMany(b => b.Satellites.Prepend(b));

    /// <summary>A body (or moon) by its id, e.g. <c>Earth</c> or <c>Moon</c>.</summary>
    public static CelestialBody Body(string id) => AllBodies.First(b => b.Id == id);

    private static List<CelestialBody> Build()
    {
        var bodies = BuildSunAndPlanets();
        CelestialBody Planet(string id) => bodies.First(b => b.Id == id);

        // Display distance is in planet radii and starts outside the rings for Saturn and Uranus.
        Planet("Earth").WithSatellites(
            Moon("Moon", SurfaceStyle.Moon, Color.FromRgb(0xB4, 0xB1, 0xAA), 3.2, new(384_400, 27.321661, 2.6, 218.3165),
                3_475, 7.35, 22, 1.62, temperature: -20));
        Planet("Mars").WithSatellites(
            Moon("Phobos", SurfaceStyle.RockyMoon, Color.FromRgb(0x6E, 0x63, 0x5A), 1.6, new(9_376, 0.31891, 1.9, 40), 22.5, 1.07, 16, 0.0057),
            Moon("Deimos", SurfaceStyle.RockyMoon, Color.FromRgb(0x80, 0x75, 0x6A), 1.3, new(23_460, 1.26244, 2.6, 200), 12.4, 1.48, 15, 0.003));
        Planet("Jupiter").WithSatellites(
            Moon("Io", SurfaceStyle.Io, Color.FromRgb(0xE6, 0xD2, 0x6A), 2.9, new(421_700, 1.769138, 1.7, 110), 3_643, 8.93, 22, 1.80, temperature: -143),
            Moon("Europa", SurfaceStyle.Europa, Color.FromRgb(0xD8, 0xCC, 0xB4), 2.6, new(671_034, 3.551181, 2.15, 250), 3_122, 4.80, 22, 1.31, temperature: -160),
            Moon("Ganymede", SurfaceStyle.Ganymede, Color.FromRgb(0x9C, 0x93, 0x86), 3.6, new(1_070_412, 7.154553, 2.65, 15), 5_268, 1.48, 23, 1.43, temperature: -163),
            Moon("Callisto", SurfaceStyle.Callisto, Color.FromRgb(0x7A, 0x6C, 0x5D), 3.3, new(1_882_709, 16.689018, 3.2, 300), 4_821, 1.08, 23, 1.24, temperature: -139));
        Planet("Saturn").WithSatellites(
            Moon("Mimas", SurfaceStyle.IcyMoon, Color.FromRgb(0xC8, 0xC8, 0xC4), 1.6, new(185_539, 0.942422, 2.7, 60), 396, 3.75, 19, 0.064),
            Moon("Enceladus", SurfaceStyle.Enceladus, Color.FromRgb(0xF0, 0xF4, 0xF8), 1.7, new(237_948, 1.370218, 3.0, 170), 504, 1.08, 20, 0.113, temperature: -198),
            Moon("Tethys", SurfaceStyle.IcyMoon, Color.FromRgb(0xE2, 0xE2, 0xE0), 2.0, new(294_619, 1.887802, 3.3, 280), 1_062, 6.17, 20, 0.146),
            Moon("Dione", SurfaceStyle.IcyMoon, Color.FromRgb(0xD0, 0xCF, 0xCC), 2.1, new(377_396, 2.736915, 3.6, 30), 1_123, 1.10, 21, 0.232),
            Moon("Rhea", SurfaceStyle.IcyMoon, Color.FromRgb(0xCB, 0xC8, 0xC2), 2.4, new(527_108, 4.518212, 3.95, 135), 1_527, 2.31, 21, 0.264),
            Moon("Titan", SurfaceStyle.Titan, Color.FromRgb(0xE0, 0x9C, 0x45), 3.5, new(1_221_870, 15.945421, 4.5, 225), 5_150, 1.35, 23, 1.35,
                temperature: -179, atmosphere: Color.FromRgb(0xFF, 0xB8, 0x5C), atmosphereStrength: 0.9),
            Moon("Iapetus", SurfaceStyle.Iapetus, Color.FromRgb(0x9A, 0x8C, 0x7C), 2.3, new(3_560_820, 79.3215, 5.2, 320), 1_469, 1.81, 21, 0.223));
        Planet("Uranus").WithSatellites(
            Moon("Miranda", SurfaceStyle.IcyMoon, Color.FromRgb(0xB8, 0xB8, 0xB4), 1.6, new(129_390, 1.413479, 2.4, 80), 472, 6.59, 19, 0.079),
            Moon("Ariel", SurfaceStyle.IcyMoon, Color.FromRgb(0xC6, 0xC4, 0xBF), 2.0, new(191_020, 2.520379, 2.8, 190), 1_158, 1.25, 21, 0.269),
            Moon("Umbriel", SurfaceStyle.IcyMoon, Color.FromRgb(0x70, 0x6E, 0x6C), 2.0, new(266_300, 4.144177, 3.2, 290), 1_169, 1.28, 21, 0.2),
            Moon("Titania", SurfaceStyle.IcyMoon, Color.FromRgb(0xA4, 0x9A, 0x92), 2.4, new(435_910, 8.705872, 3.8, 20), 1_577, 3.40, 21, 0.379),
            Moon("Oberon", SurfaceStyle.IcyMoon, Color.FromRgb(0x96, 0x8A, 0x80), 2.3, new(583_520, 13.463239, 4.4, 140), 1_523, 3.08, 21, 0.346));
        Planet("Neptune").WithSatellites(
            Moon("Triton", SurfaceStyle.Triton, Color.FromRgb(0xDC, 0xC8, 0xBC), 3.0, new(354_759, 5.876854, 2.6, 260, Retrograde: true),
                2_707, 2.14, 22, 0.779, temperature: -235));

        return bodies;
    }

    private static CelestialBody Moon(
        string id, SurfaceStyle surface, Color color, double displayRadius, MoonOrbit orbit,
        double diameterKm, double massMantissa, int massExponent, double gravity,
        double? temperature = null, Color? atmosphere = null, double atmosphereStrength = 0) =>
        new(id, surface)
        {
            MoonOrbit = orbit,
            DisplayRadius = displayRadius,
            BaseColor = color,
            AccentColor = Color.FromRgb((byte)((color.R + 255) / 2), (byte)((color.G + 255) / 2), (byte)((color.B + 255) / 2)),
            AtmosphereColor = atmosphere,
            AtmosphereStrength = atmosphereStrength,
            RotationHours = orbit.PeriodDays * 24 * (orbit.Retrograde ? -1 : 1),
            DiameterKm = diameterKm,
            MassMantissa = massMantissa,
            MassExponent = massExponent,
            SurfaceGravity = gravity,
            MeanTemperatureC = temperature,
        };

    private static List<CelestialBody> BuildSunAndPlanets() =>
    [
        new("Sun", SurfaceStyle.Sun)
        {
            DisplayRadius = 34,
            AccentColor = Color.FromRgb(0xFF, 0xC2, 0x4D),
            BaseColor = Color.FromRgb(0xFF, 0xD8, 0x80),
            AxialTiltDegrees = 7.25,
            RotationHours = 609.12,
            DiameterKm = 1_392_700,
            MassMantissa = 1.989,
            MassExponent = 30,
            MassEarths = 333_000,
            MeanTemperatureC = 5_500,
            SurfaceGravity = 274,
        },
        new("Mercury", SurfaceStyle.Mercury)
        {
            OrbitIndex = 0,
            Orbit = new(0.38709927, 0.00000037, 0.20563593, 0.00001906, 7.00497902, -0.00594749,
                252.25032350, 149472.67411175, 77.45779628, 0.16047689, 48.33076593, -0.12534081),
            DisplayRadius = 6.5,
            AccentColor = Color.FromRgb(0xB8, 0xB0, 0xA8),
            BaseColor = Color.FromRgb(0x8C, 0x86, 0x80),
            AxialTiltDegrees = 0.034,
            RotationHours = 1407.6,
            DiameterKm = 4_879,
            MassMantissa = 3.30,
            MassExponent = 23,
            MassEarths = 0.055,
            DistanceMillionKm = 57.9,
            DistanceAu = 0.387,
            OrbitalPeriodDays = 88.0,
            Moons = 0,
            MeanTemperatureC = 167,
            SurfaceGravity = 3.7,
        },
        new("Venus", SurfaceStyle.Venus)
        {
            OrbitIndex = 1,
            Orbit = new(0.72333566, 0.00000390, 0.00677672, -0.00004107, 3.39467605, -0.00078890,
                181.97909950, 58517.81538729, 131.60246718, 0.00268329, 76.67984255, -0.27769418),
            DisplayRadius = 10,
            AccentColor = Color.FromRgb(0xF2, 0xD3, 0x9B),
            BaseColor = Color.FromRgb(0xE3, 0xC1, 0x8A),
            AtmosphereColor = Color.FromRgb(0xFF, 0xE9, 0xB8),
            AtmosphereStrength = 0.55,
            AxialTiltDegrees = 177.4,
            RotationHours = -5832.5,
            DiameterKm = 12_104,
            MassMantissa = 4.87,
            MassExponent = 24,
            MassEarths = 0.815,
            DistanceMillionKm = 108.2,
            DistanceAu = 0.723,
            OrbitalPeriodDays = 224.7,
            Moons = 0,
            MeanTemperatureC = 464,
            SurfaceGravity = 8.9,
        },
        new("Earth", SurfaceStyle.Earth)
        {
            OrbitIndex = 2,
            Orbit = new(1.00000261, 0.00000562, 0.01671123, -0.00004392, -0.00001531, -0.01294668,
                100.46457166, 35999.37244981, 102.93768193, 0.32327364, 0.0, 0.0),
            DisplayRadius = 10.5,
            AccentColor = Color.FromRgb(0x5A, 0xA9, 0xFF),
            BaseColor = Color.FromRgb(0x2E, 0x5E, 0xA8),
            AtmosphereColor = Color.FromRgb(0x6F, 0xB4, 0xFF),
            AtmosphereStrength = 0.75,
            AxialTiltDegrees = 23.44,
            RotationHours = 23.93,
            DiameterKm = 12_756,
            MassMantissa = 5.97,
            MassExponent = 24,
            MassEarths = 1,
            DistanceMillionKm = 149.6,
            DistanceAu = 1.000,
            OrbitalPeriodDays = 365.2,
            Moons = 1,
            MeanTemperatureC = 15,
            SurfaceGravity = 9.8,
        },
        new("Mars", SurfaceStyle.Mars)
        {
            OrbitIndex = 3,
            Orbit = new(1.52371034, 0.00001847, 0.09339410, 0.00007882, 1.84969142, -0.00813131,
                -4.55343205, 19140.30268499, -23.94362959, 0.44441088, 49.55953891, -0.29257343),
            DisplayRadius = 8,
            AccentColor = Color.FromRgb(0xF0, 0x7A, 0x4A),
            BaseColor = Color.FromRgb(0xB5, 0x5A, 0x34),
            AtmosphereColor = Color.FromRgb(0xFF, 0xB0, 0x88),
            AtmosphereStrength = 0.25,
            AxialTiltDegrees = 25.19,
            RotationHours = 24.62,
            DiameterKm = 6_792,
            MassMantissa = 6.42,
            MassExponent = 23,
            MassEarths = 0.107,
            DistanceMillionKm = 228.0,
            DistanceAu = 1.524,
            OrbitalPeriodDays = 687.0,
            Moons = 2,
            MeanTemperatureC = -65,
            SurfaceGravity = 3.7,
        },
        new("Jupiter", SurfaceStyle.Jupiter)
        {
            OrbitIndex = 4,
            Orbit = new(5.20288700, -0.00011607, 0.04838624, -0.00013253, 1.30439695, -0.00183714,
                34.39644051, 3034.74612775, 14.72847983, 0.21252668, 100.47390909, 0.20469106),
            DisplayRadius = 20,
            AccentColor = Color.FromRgb(0xE8, 0xB9, 0x8A),
            BaseColor = Color.FromRgb(0xC9, 0xA2, 0x7C),
            AtmosphereColor = Color.FromRgb(0xF5, 0xDD, 0xC0),
            AtmosphereStrength = 0.25,
            AxialTiltDegrees = 3.13,
            RotationHours = 9.93,
            DiameterKm = 142_984,
            MassMantissa = 1.898,
            MassExponent = 27,
            MassEarths = 317.8,
            DistanceMillionKm = 778.5,
            DistanceAu = 5.203,
            OrbitalPeriodDays = 4_331,
            Moons = 95,
            MeanTemperatureC = -110,
            SurfaceGravity = 23.1,
        },
        new("Saturn", SurfaceStyle.Saturn)
        {
            OrbitIndex = 5,
            Orbit = new(9.53667594, -0.00125060, 0.05386179, -0.00050991, 2.48599187, 0.00193609,
                49.95424423, 1222.49362201, 92.59887831, -0.41897216, 113.66242448, -0.28867794),
            DisplayRadius = 15.5,
            AccentColor = Color.FromRgb(0xF1, 0xD8, 0x9E),
            BaseColor = Color.FromRgb(0xD9, 0xC0, 0x8C),
            AtmosphereColor = Color.FromRgb(0xFA, 0xE8, 0xC0),
            AtmosphereStrength = 0.2,
            Rings = new RingSystem(1.24, 2.34),
            AxialTiltDegrees = 26.73,
            RotationHours = 10.66,
            DiameterKm = 120_536,
            MassMantissa = 5.68,
            MassExponent = 26,
            MassEarths = 95.2,
            DistanceMillionKm = 1_432.0,
            DistanceAu = 9.537,
            OrbitalPeriodDays = 10_747,
            Moons = 274,
            MeanTemperatureC = -140,
            SurfaceGravity = 9.0,
        },
        new("Uranus", SurfaceStyle.Uranus)
        {
            OrbitIndex = 6,
            Orbit = new(19.18916464, -0.00196176, 0.04725744, -0.00004397, 0.77263783, -0.00242939,
                313.23810451, 428.48202785, 170.95427630, 0.40805281, 74.01692503, 0.04240589),
            DisplayRadius = 12.5,
            AccentColor = Color.FromRgb(0x9F, 0xE6, 0xEE),
            BaseColor = Color.FromRgb(0x9C, 0xD6, 0xDE),
            AtmosphereColor = Color.FromRgb(0xB8, 0xF2, 0xFA),
            AtmosphereStrength = 0.45,
            Rings = new RingSystem(1.60, 2.02),
            AxialTiltDegrees = 97.77,
            RotationHours = -17.24,
            DiameterKm = 51_118,
            MassMantissa = 8.68,
            MassExponent = 25,
            MassEarths = 14.5,
            DistanceMillionKm = 2_867.0,
            DistanceAu = 19.19,
            OrbitalPeriodDays = 30_589,
            Moons = 28,
            MeanTemperatureC = -195,
            SurfaceGravity = 8.7,
        },
        new("Neptune", SurfaceStyle.Neptune)
        {
            OrbitIndex = 7,
            Orbit = new(30.06992276, 0.00026291, 0.00859048, 0.00005105, 1.77004347, 0.00035372,
                -55.12002969, 218.45945325, 44.96476227, -0.32241464, 131.78422574, -0.01262724),
            DisplayRadius = 12,
            AccentColor = Color.FromRgb(0x5B, 0x86, 0xFF),
            BaseColor = Color.FromRgb(0x3A, 0x5C, 0xD8),
            AtmosphereColor = Color.FromRgb(0x7F, 0xA6, 0xFF),
            AtmosphereStrength = 0.5,
            AxialTiltDegrees = 28.32,
            RotationHours = 16.11,
            DiameterKm = 49_528,
            MassMantissa = 1.02,
            MassExponent = 26,
            MassEarths = 17.1,
            DistanceMillionKm = 4_515.0,
            DistanceAu = 30.07,
            OrbitalPeriodDays = 59_800,
            Moons = 16,
            MeanTemperatureC = -200,
            SurfaceGravity = 11.0,
        },
    ];
}
