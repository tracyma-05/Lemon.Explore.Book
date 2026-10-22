using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using Lemon.Explore.Book.Infrastructures.Localization;

namespace Lemon.Explore.Book.Controls.Space;

public enum SurfaceStyle
{
    Sun,
    Mercury,
    Venus,
    Earth,
    Mars,
    Jupiter,
    Saturn,
    Uranus,
    Neptune,

    // Moons
    Moon,
    Io,
    Europa,
    Ganymede,
    Callisto,
    Titan,
    Enceladus,
    Iapetus,
    Triton,

    /// <summary>Generic cratered ice, coloured by <see cref="CelestialBody.BaseColor"/> (Saturn's and Uranus's mid-sized moons).</summary>
    IcyMoon,

    /// <summary>Small dark rock (Phobos, Deimos).</summary>
    RockyMoon,
}

/// <summary>A ring system, radii in planet radii.</summary>
public sealed record RingSystem(double Inner, double Outer);

/// <summary>
/// A moon's orbit around its planet, treated as circular in the ecliptic plane.
/// </summary>
/// <param name="DistanceKm">Mean distance from the planet's centre.</param>
/// <param name="PeriodDays">Sidereal orbital period; drives the motion at the simulated date.</param>
/// <param name="DisplayDistance">Drawn distance from the planet in planet radii (compressed, like the planet rings).</param>
/// <param name="LongitudeAtJ2000">
/// Orbital longitude at J2000 in degrees. For the Moon it is the real mean longitude, so its phase matches the sky;
/// for the other moons it is an illustrative starting point.
/// </param>
/// <param name="Retrograde">Orbits against the planet's rotation (Triton).</param>
public sealed record MoonOrbit(double DistanceKm, double PeriodDays, double DisplayDistance, double LongitudeAtJ2000, bool Retrograde = false)
{
    /// <summary>Ecliptic longitude of the moon around its planet, in radians.</summary>
    public double LongitudeAt(double julianDay)
    {
        var turns = (julianDay - 2451545.0) / PeriodDays;
        return (LongitudeAtJ2000 * Math.PI / 180) + ((Retrograde ? -1 : 1) * 2 * Math.PI * (turns - Math.Floor(turns)));
    }
}

/// <summary>
/// The Sun, a planet or a moon: what the scene needs to draw it and what the detail panel shows about it. Name, tagline
/// and description come from <c>SolarSystem_{Id}_*</c> resources and follow the interface language.
/// </summary>
public sealed class CelestialBody : ObservableObject
{
    public CelestialBody(string id, SurfaceStyle surface)
    {
        Id = id;
        Surface = surface;

        // Bodies come from a process-wide catalog, so this subscription lives as long as the app.
        LocalizationService.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Tagline));
            OnPropertyChanged(nameof(Description));
        };
    }

    public string Id { get; }

    public SurfaceStyle Surface { get; }

    public bool IsStar => Surface == SurfaceStyle.Sun;

    public bool IsMoon => Parent is not null;

    /// <summary>The planet a moon orbits; null for the Sun and the planets.</summary>
    public CelestialBody? Parent { get; private set; }

    /// <summary>Major moons, inner to outer.</summary>
    public IReadOnlyList<CelestialBody> Satellites => _satellites;

    private readonly List<CelestialBody> _satellites = [];

    public MoonOrbit? MoonOrbit { get; init; }

    /// <summary>Adds moons to this planet (catalog construction only).</summary>
    internal CelestialBody WithSatellites(params CelestialBody[] moons)
    {
        foreach (var moon in moons)
        {
            moon.Parent = this;
            _satellites.Add(moon);
        }

        return this;
    }

    public string Name => LocalizationService.Instance.GetString($"SolarSystem_{Id}_Name");

    public string Tagline => LocalizationService.Instance.GetString($"SolarSystem_{Id}_Tagline");

    public string Description => LocalizationService.Instance.GetString($"SolarSystem_{Id}_Description");

    /// <summary>Ring index in the stylized (evenly spaced) layout, 0 = Mercury; -1 for the Sun.</summary>
    public int OrbitIndex { get; init; } = -1;

    public KeplerOrbit? Orbit { get; init; }

    /// <summary>Radius in DIPs at zoom 1 on a ~900 px view. Not to scale: true sizes would make Mercury invisible.</summary>
    public double DisplayRadius { get; init; }

    public Color AccentColor { get; init; }

    /// <summary>Flat colour drawn until the procedural texture has been generated.</summary>
    public Color BaseColor { get; init; }

    public Color? AtmosphereColor { get; init; }

    public double AtmosphereStrength { get; init; }

    public RingSystem? Rings { get; init; }

    public double AxialTiltDegrees { get; init; }

    /// <summary>Sidereal rotation in hours; negative for retrograde rotation (Venus, Uranus).</summary>
    public double RotationHours { get; init; }

    /// <summary>Rotation equals the orbital period, so one face always points at the planet (every moon here).</summary>
    public bool IsTidallyLocked => IsMoon;

    public IBrush AccentBrush => _accentBrush ??= new ImmutableSolidColorBrush(AccentColor);

    private IBrush? _accentBrush;

    #region facts (NASA planetary fact sheet)

    public double DiameterKm { get; init; }

    /// <summary>Mass as mantissa × 10^exponent kg, e.g. 5.97 and 24 for Earth.</summary>
    public double MassMantissa { get; init; }

    public int MassExponent { get; init; }

    public double? MassEarths { get; init; }

    public double? DistanceMillionKm { get; init; }

    public double? DistanceAu { get; init; }

    public double? OrbitalPeriodDays { get; init; }

    public int? Moons { get; init; }

    public double? MeanTemperatureC { get; init; }

    public double SurfaceGravity { get; init; }

    #endregion

    /// <summary>A gentle on-screen spin: a body's day takes 5 to 80 seconds, so fast playback does not strobe.</summary>
    public double SpinAngle(double seconds)
    {
        var period = Math.Clamp(14 * Math.Abs(RotationHours) / 24, 5, 80);
        return Math.Sign(RotationHours) * 2 * Math.PI * seconds / period;
    }
}

/// <summary>Shortcuts to the bodies most chapters draw.</summary>
public static class SpaceBodies
{
    public static CelestialBody Sun => SolarSystemCatalog.Body("Sun");

    public static CelestialBody Earth => SolarSystemCatalog.Body("Earth");

    public static CelestialBody Moon => SolarSystemCatalog.Body("Moon");
}
