using Avalonia.Media;

namespace Lemon.Explore.Book.Controls.Space;

/// <summary>Which procedural surface a body gets (see <see cref="PlanetSurfaces"/>).</summary>
public enum SurfaceStyle
{
    Sun,
    Earth,
}

/// <summary>A ring system, radii in planet radii.</summary>
public sealed record RingSystem(double Inner, double Outer);

/// <summary>
/// What the sphere renderer needs to draw a body. Chapters that show more of the solar system add their bodies
/// to <see cref="SpaceBodies"/> (and a surface to <see cref="PlanetSurfaces"/>).
/// </summary>
public sealed record SpaceBody(string Id, SurfaceStyle Surface)
{
    public bool IsStar => Surface == SurfaceStyle.Sun;

    public bool IsMoon { get; init; }

    /// <summary>Flat colour drawn until the procedural texture has been generated.</summary>
    public Color BaseColor { get; init; }

    public Color? AtmosphereColor { get; init; }

    public double AtmosphereStrength { get; init; }

    public RingSystem? Rings { get; init; }

    public double AxialTiltDegrees { get; init; }

    /// <summary>Sidereal rotation in hours; negative for retrograde rotation.</summary>
    public double RotationHours { get; init; }

    /// <summary>A gentle on-screen spin: a body's day takes 5 to 80 seconds, depending on how fast it really turns.</summary>
    public double SpinAngle(double seconds)
    {
        var period = Math.Clamp(14 * Math.Abs(RotationHours) / 24, 5, 80);
        return Math.Sign(RotationHours) * 2 * Math.PI * seconds / period;
    }
}

/// <summary>The bodies the chapters draw (NASA planetary fact sheet values).</summary>
public static class SpaceBodies
{
    public static SpaceBody Sun { get; } = new("Sun", SurfaceStyle.Sun)
    {
        BaseColor = Color.FromRgb(0xFF, 0xD8, 0x80),
        AxialTiltDegrees = 7.25,
        RotationHours = 609.12,
    };

    public static SpaceBody Earth { get; } = new("Earth", SurfaceStyle.Earth)
    {
        BaseColor = Color.FromRgb(0x2E, 0x5E, 0xA8),
        AtmosphereColor = Color.FromRgb(0x6F, 0xB4, 0xFF),
        AtmosphereStrength = 0.75,
        AxialTiltDegrees = 23.44,
        RotationHours = 23.93,
    };
}
