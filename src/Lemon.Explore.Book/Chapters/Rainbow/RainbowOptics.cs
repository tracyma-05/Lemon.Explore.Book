using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Lemon.Explore.Book.Chapters.BlueSky;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Rainbow;

/// <summary>The three views of the rainbow page.</summary>
public enum RainbowMode
{
    /// <summary>Light bending at a surface between air and water, glass or diamond, or through a prism.</summary>
    Refraction,

    /// <summary>Sunlight through one raindrop: in, reflected once (or twice) inside, and out at about 42° (or 51°).</summary>
    Drop,

    /// <summary>The rainbow in the sky, opposite the Sun, by the Sun's height and the size of the drops.</summary>
    Sky,
}

/// <summary>Refraction view: a flat surface, or a prism.</summary>
public enum RefractionScene
{
    Surface,
    Prism,
}

public enum OpticalMedium
{
    Water,
    Glass,
    Diamond,
}

/// <summary>The light shone in: white (all colours) or one colour.</summary>
public enum LightColour
{
    White,
    Red,
    Green,
    Violet,
}

/// <summary>Which bow the drop view follows: once reflected inside (primary), twice (secondary), or both.</summary>
public enum BowOrder
{
    Primary,
    Secondary,
    Both,
}

/// <summary>How big the drops are: the smaller, the more their colours run together (a fog bow is white).</summary>
public enum DropSize
{
    Large,
    Rain,
    Drizzle,
    Fog,
}

/// <summary>A straight part of a ray, with the share of the light that is left in it.</summary>
public readonly record struct RaySegment(Point From, Point To, double Power, bool Inside);

/// <summary>Light leaving a drop (or a surface) on the way: where, which way and how much.</summary>
public readonly record struct RayLeak(Point At, Vector Direction, double Power);

/// <summary>
/// A ray through a drop of radius 1 centred on the origin, the sunlight coming along +x (y is up): where it enters, where
/// it meets the surface inside, which way it leaves, how much light is in each part, and what leaks out on the way.
/// </summary>
public sealed record DropPath(IReadOnlyList<Point> Points, IReadOnlyList<double> Powers, Vector Exit, double ExitPower,
    IReadOnlyList<RayLeak> Leaks)
{
    public Point Entry => Points[0];

    public Point ExitPoint => Points[^1];
}

/// <summary>
/// The optics of the rainbow page: refractive indices by colour, Snell's law and how much light a surface reflects
/// (Fresnel), rays through a prism, rays through a raindrop, and the brightness of the sky round the point opposite the
/// Sun that the rain sends back.
/// </summary>
public static class RainbowOptics
{
    public const double Deg = Math.PI / 180;

    /// <summary>Light's speed in empty space (and very nearly in air), km/s.</summary>
    public const double SpeedOfLight = 299_792.458;

    public const double RedNm = 650;
    public const double GreenNm = 530;
    public const double VioletNm = 410;

    /// <summary>The yellow sodium line, where a material's "refractive index" is usually quoted.</summary>
    public const double YellowNm = 589.3;

    /// <summary>Colours a white beam is split into when drawn: red, orange, yellow, green, blue, indigo, violet.</summary>
    public static readonly double[] Fan = [660, 615, 585, 545, 495, 455, 415];

    /// <summary>The Sun's angular radius in the sky, degrees.</summary>
    public const double SunRadius = 0.266;

    #region refractive index

    /// <summary>
    /// The refractive index at a wavelength, from a two-term Cauchy fit (n = A + B/λ²) through the blue (486 nm) and red
    /// (656 nm) Fraunhofer lines: water at 20 °C, a crown glass (BK7) and diamond.
    /// </summary>
    public static double Index(OpticalMedium medium, double nm)
    {
        var (a, b) = medium switch
        {
            OpticalMedium.Glass => (1.50446, 4240.0),
            OpticalMedium.Diamond => (2.37913, 13296.0),
            _ => (1.32369, 3193.0),
        };
        return a + (b / (nm * nm));
    }

    public static double Wavelength(LightColour colour) => colour switch
    {
        LightColour.Red => RedNm,
        LightColour.Violet => VioletNm,
        _ => GreenNm,
    };

    /// <summary>Light's speed in a material of index <paramref name="n"/>, km/s.</summary>
    public static double SpeedIn(double n) => SpeedOfLight / n;

    #endregion

    #region a flat surface

    /// <summary>
    /// Snell's law: the angle from the normal on the far side, degrees, for light meeting the surface at
    /// <paramref name="incidence"/> from the normal; NaN when none gets through (total internal reflection).
    /// </summary>
    public static double Refraction(double n1, double n2, double incidence)
    {
        var sine = n1 / n2 * Math.Sin(incidence * Deg);
        return Math.Abs(sine) > 1 ? double.NaN : Math.Asin(sine) / Deg;
    }

    /// <summary>The angle past which light coming from the denser side is all reflected, degrees; NaN going into denser.</summary>
    public static double CriticalAngle(double n1, double n2) => n1 > n2 ? Math.Asin(n2 / n1) / Deg : double.NaN;

    /// <summary>
    /// The share of light a surface reflects (Fresnel), for light polarised across the plane of incidence (s) and in it
    /// (p); both 1 past the critical angle.
    /// </summary>
    public static (double S, double P) Reflectances(double n1, double n2, double incidence)
    {
        var refraction = Refraction(n1, n2, incidence);
        if (double.IsNaN(refraction)) return (1, 1);
        var ci = Math.Cos(incidence * Deg);
        var ct = Math.Cos(refraction * Deg);
        var s = ((n1 * ci) - (n2 * ct)) / ((n1 * ci) + (n2 * ct));
        var p = ((n1 * ct) - (n2 * ci)) / ((n1 * ct) + (n2 * ci));
        return (s * s, p * p);
    }

    /// <summary>The share of ordinary (unpolarised) light a surface reflects.</summary>
    public static double Reflectance(double n1, double n2, double incidence)
    {
        var (s, p) = Reflectances(n1, n2, incidence);
        return (s + p) / 2;
    }

    /// <summary>The angle at which reflected light is all polarised one way (Brewster's angle), degrees.</summary>
    public static double BrewsterAngle(double n1, double n2) => Math.Atan(n2 / n1) / Deg;

    #endregion

    #region vectors

    /// <summary>
    /// The direction light takes through a surface (Snell's law in vector form), or null if it is all reflected.
    /// <paramref name="normal"/> is the surface's unit normal on the side the light comes from.
    /// </summary>
    public static Vector? Refract(Vector direction, Vector normal, double n1, double n2)
    {
        direction = direction.Normalize();
        var eta = n1 / n2;
        var cosi = -Dot(normal, direction);
        var k = 1 - (eta * eta * (1 - (cosi * cosi)));
        if (k < 0) return null;
        var refracted = (direction * eta) + (normal * ((eta * cosi) - Math.Sqrt(k)));
        refracted = refracted.Normalize();
        return refracted;
    }

    public static Vector Reflect(Vector direction, Vector normal) => direction - (normal * (2 * Dot(direction, normal)));

    /// <summary>The angle between a ray and a surface's normal, degrees (0 straight on).</summary>
    public static double IncidenceAngle(Vector direction, Vector normal)
    {
        direction = direction.Normalize();
        return Math.Acos(Math.Clamp(Math.Abs(Dot(direction, normal)), 0, 1)) / Deg;
    }

    private static double Dot(Vector a, Vector b) => (a.X * b.X) + (a.Y * b.Y);

    private static double Cross(Vector a, Vector b) => (a.X * b.Y) - (a.Y * b.X);

    #endregion

    #region a prism

    /// <summary>
    /// How far a prism of apex angle <paramref name="apex"/> turns a ray that meets its first face at
    /// <paramref name="incidence"/>, degrees; NaN if the ray cannot get out of the second face.
    /// </summary>
    public static double PrismDeviation(double n, double apex, double incidence)
    {
        var r1 = Math.Asin(Math.Sin(incidence * Deg) / n) / Deg;
        var r2 = apex - r1;
        var sine = n * Math.Sin(r2 * Deg);
        if (Math.Abs(sine) > 1) return double.NaN;
        return incidence + (Math.Asin(sine) / Deg) - apex;
    }

    /// <summary>The smallest turn a prism gives, degrees: when the ray goes through it symmetrically.</summary>
    public static double MinimumDeviation(double n, double apex) => (2 * Math.Asin(n * Math.Sin(apex / 2 * Deg)) / Deg) - apex;

    /// <summary>The angle of incidence that gives the smallest turn, degrees.</summary>
    public static double MinimumDeviationIncidence(double n, double apex) => Math.Asin(n * Math.Sin(apex / 2 * Deg)) / Deg;

    /// <summary>
    /// Follows a ray through a convex polygon of index <paramref name="n"/> in air, taking the way most of the light goes
    /// at each surface (through it if it can, else reflected), for at most <paramref name="maxSegments"/> parts. The last
    /// part leaves the polygon and runs <paramref name="tail"/> further.
    /// </summary>
    public static IReadOnlyList<RaySegment> TracePolygon(IReadOnlyList<Point> polygon, Point origin, Vector direction, double n,
        double tail, int maxSegments = 6)
    {
        var segments = new List<RaySegment>();
        direction = direction.Normalize();
        var inside = false;
        var power = 1.0;
        var at = origin;
        for (var step = 0; step < maxSegments; step++)
        {
            var hit = Intersect(polygon, at, direction);
            if (hit is null)
            {
                segments.Add(new RaySegment(at, at + (direction * tail), power, inside));
                break;
            }

            var (point, edgeNormal) = hit.Value;
            segments.Add(new RaySegment(at, point, power, inside));

            // The normal on the side the light comes from.
            var normal = Dot(edgeNormal, direction) > 0 ? -edgeNormal : edgeNormal;
            var (n1, n2) = inside ? (n, 1.0) : (1.0, n);
            var refracted = Refract(direction, normal, n1, n2);
            if (refracted is { } through)
            {
                power *= 1 - Reflectance(n1, n2, IncidenceAngle(direction, normal));
                direction = through;
                inside = !inside;
            }
            else
            {
                direction = Reflect(direction, normal);
            }

            at = point + (direction * 1e-6);
        }

        return segments;
    }

    /// <summary>Where a ray from <paramref name="origin"/> first crosses the polygon's outline, and that edge's normal.</summary>
    private static (Point Point, Vector Normal)? Intersect(IReadOnlyList<Point> polygon, Point origin, Vector direction)
    {
        (Point, Vector)? best = null;
        var nearest = double.PositiveInfinity;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            var edge = b - a;
            var denominator = Cross(direction, edge);
            if (Math.Abs(denominator) < 1e-12) continue;
            var t = Cross(a - origin, edge) / denominator;
            var u = Cross(a - origin, direction) / denominator;
            if (t <= 1e-9 || u < 0 || u > 1 || t >= nearest) continue;
            nearest = t;
            var normal = new Vector(edge.Y, -edge.X);
            normal = normal.Normalize();
            best = (origin + (direction * t), normal);
        }

        return best;
    }

    #endregion

    #region a raindrop

    /// <summary>
    /// The angle from the point opposite the Sun at which light comes out of a drop after <paramref name="order"/>
    /// reflections inside (1 the primary bow, 2 the secondary), for a ray entering <paramref name="impact"/> radii off
    /// centre, degrees. (Order 0 is light reflected off the outside.)
    /// </summary>
    public static double ExitAngle(int order, double n, double impact)
    {
        var i = Math.Asin(Math.Clamp(Math.Abs(impact), 0, 1));
        var deviation = order == 0
            ? Math.PI - (2 * i)
            : (2 * (i - Math.Asin(Math.Sin(i) / n))) + (order * (Math.PI - (2 * Math.Asin(Math.Sin(i) / n))));
        return Math.Acos(Math.Clamp(-Math.Cos(deviation), -1, 1)) / Deg;
    }

    /// <summary>The ray that turns furthest (primary) or least (secondary): how far off centre it enters, in radii.</summary>
    public static double RainbowImpact(int order, double n)
    {
        var k = order;
        var cos = Math.Sqrt(((n * n) - 1) / ((k * k) + (2 * k)));
        return Math.Sqrt(1 - (cos * cos));
    }

    /// <summary>Where the light crowds together: the rainbow's angle from the point opposite the Sun, degrees.</summary>
    public static double RainbowAngle(int order, double n) => ExitAngle(order, n, RainbowImpact(order, n));

    /// <summary>
    /// The share of the light entering <paramref name="impact"/> radii off centre that comes out after
    /// <paramref name="order"/> reflections inside: in, reflected, out (each polarisation on its own, then averaged).
    /// </summary>
    public static double Throughput(int order, double n, double impact)
    {
        var incidence = Math.Asin(Math.Clamp(Math.Abs(impact), 0, 1)) / Deg;
        var (s, p) = Reflectances(1, n, incidence);
        if (order == 0) return (s + p) / 2;
        return (((1 - s) * (1 - s) * Math.Pow(s, order)) + ((1 - p) * (1 - p) * Math.Pow(p, order))) / 2;
    }

    /// <summary>
    /// How strongly the light of a bow is polarised: (s − p)/(s + p) of what comes out, at the rainbow ray.
    /// </summary>
    public static double Polarisation(int order, double n)
    {
        var incidence = Math.Asin(RainbowImpact(order, n)) / Deg;
        var (s, p) = Reflectances(1, n, incidence);
        var outS = (1 - s) * (1 - s) * Math.Pow(s, order);
        var outP = (1 - p) * (1 - p) * Math.Pow(p, order);
        return (outS - outP) / (outS + outP);
    }

    /// <summary>
    /// Follows a ray through a drop of radius 1 (centre at the origin, y up) entering at height <paramref name="height"/>
    /// with the sunlight going along +x, reflected <paramref name="order"/> times inside before it leaves.
    /// </summary>
    public static DropPath TraceDrop(double height, double n, int order)
    {
        height = Math.Clamp(height, -0.99999, 0.99999);
        var direction = new Vector(1, 0);
        var entry = new Point(-Math.Sqrt(1 - (height * height)), height);
        var points = new List<Point> { entry };
        var powers = new List<double>();
        var leaks = new List<RayLeak>();

        var outward = (Vector)entry;
        var incidence = IncidenceAngle(direction, outward);
        var reflected = Reflectance(1, n, incidence);
        leaks.Add(new RayLeak(entry, Reflect(direction, outward), reflected));
        direction = Refract(direction, outward, 1, n) ?? direction;
        var power = 1 - reflected;
        var at = entry;

        for (var bounce = 0; bounce <= order; bounce++)
        {
            // Along a chord of the unit circle: from a point on it, the next is t = −2 (p·d) further on.
            var next = at + (direction * (-2 * Dot((Vector)at, direction)));
            points.Add(next);
            powers.Add(power);
            outward = (Vector)next;
            var inner = Reflectance(n, 1, IncidenceAngle(direction, -outward));
            var leaving = Refract(direction, -outward, n, 1) ?? Reflect(direction, -outward);
            if (bounce == order)
            {
                return new DropPath(points, powers, leaving, power * (1 - inner), leaks);
            }

            leaks.Add(new RayLeak(next, leaving, power * (1 - inner)));
            direction = Reflect(direction, -outward);
            power *= inner;
            at = next;
        }

        throw new InvalidOperationException("unreachable");
    }

    #endregion

    #region the bow in the sky

    /// <summary>Steps of a bow profile per degree from the point opposite the Sun.</summary>
    public const int ProfileSteps = 10;

    /// <summary>A bow profile runs from 0° to this many degrees from the point opposite the Sun (all the way round).</summary>
    public const int ProfileDegrees = 180;

    /// <summary>The drop's diameter, millimetres.</summary>
    public static double Diameter(DropSize size) => size switch
    {
        DropSize.Large => 2,
        DropSize.Drizzle => 0.2,
        DropSize.Fog => 0.02,
        _ => 1,
    };

    /// <summary>
    /// How much a bow is smeared out, degrees (one standard deviation): by the width of the Sun's disc, and – a rough
    /// stand-in for the wave theory of the bow (Airy) – by about 0.6 (λ/d)^⅔ radians, which small drops make wide.
    /// </summary>
    public static double Smearing(double nm, double diameterMm)
    {
        var sun = SunRadius / 2;
        var wave = 0.6 * Math.Pow(nm * 1e-6 / diameterMm, 2.0 / 3) / Deg;
        return Math.Sqrt((sun * sun) + (wave * wave));
    }

    /// <summary>
    /// The brightness of the rain round the point opposite the Sun, by angle (<see cref="ProfileSteps"/> per degree, 0 to
    /// <see cref="ProfileDegrees"/>), for light of one wavelength coming out after <paramref name="order"/> reflections
    /// inside the drops. Sunlight falling on the drops is spread by angle (rays through a drop's whole face, each with its
    /// Fresnel share), per unit of sky, then smeared by <see cref="Smearing"/>. Per unit of sunlight, so all bands add up
    /// the same way.
    /// </summary>
    public static double[] Profile(int order, double nm, double diameterMm, int rays = 20000)
    {
        var n = Index(OpticalMedium.Water, nm);
        var bins = (ProfileDegrees * ProfileSteps) + 1;
        var power = new double[bins];
        for (var j = 0; j < rays; j++)
        {
            // Equal shares of the drop's face (rings of equal area).
            var impact = Math.Sqrt((j + 0.5) / rays);
            var angle = ExitAngle(order, n, impact);
            if (angle > ProfileDegrees) continue;
            var x = angle * ProfileSteps;
            var low = (int)Math.Floor(x);
            var share = x - low;
            var light = Throughput(order, n, impact) / rays;
            power[low] += light * (1 - share);
            if (low + 1 < bins) power[low + 1] += light * share;
        }

        // Per unit of sky: a ring of the sky round the point opposite the Sun covers 2π sin θ dθ.
        var radiance = new double[bins];
        var step = Deg / ProfileSteps;
        for (var b = 0; b < bins; b++)
        {
            var theta = Math.Max(b, 0.5) * step;
            radiance[b] = power[b] / (2 * Math.PI * Math.Sin(theta) * step);
        }

        return Smooth(radiance, Smearing(nm, diameterMm) * ProfileSteps);
    }

    /// <summary>A Gaussian blur of <paramref name="sigma"/> steps, the ends mirrored.</summary>
    private static double[] Smooth(double[] values, double sigma)
    {
        var reach = (int)Math.Ceiling(sigma * 3.5);
        var kernel = Enumerable.Range(-reach, (2 * reach) + 1).Select(k => Math.Exp(-0.5 * k * k / (sigma * sigma))).ToArray();
        var total = kernel.Sum();
        var result = new double[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var sum = 0.0;
            for (var k = -reach; k <= reach; k++)
            {
                var j = i + k;
                if (j < 0) j = -j;
                if (j >= values.Length) j = (2 * (values.Length - 1)) - j;
                if (j < 0 || j >= values.Length) continue;
                sum += values[j] * kernel[k + reach];
            }

            result[i] = sum / total;
        }

        return result;
    }

    /// <summary>Per band of <see cref="SkyOptics.Wavelengths"/>: the primary bow with the light reflected off the outside, and the secondary.</summary>
    private sealed record BandProfiles(double[][] Primary, double[][] Secondary);

    private static readonly Dictionary<DropSize, BandProfiles> ProfileCache = [];

    private static BandProfiles ProfilesFor(DropSize size)
    {
        lock (ProfileCache)
        {
            if (ProfileCache.TryGetValue(size, out var cached)) return cached;
        }

        var diameter = Diameter(size);
        var primary = new double[SkyOptics.Bands][];
        var secondary = new double[SkyOptics.Bands][];
        Parallel.For(0, SkyOptics.Bands, band =>
        {
            var nm = SkyOptics.Wavelengths[band];
            var outside = Profile(0, nm, diameter, 4000);
            primary[band] = Profile(1, nm, diameter).Select((v, i) => v + outside[i]).ToArray();
            secondary[band] = Profile(2, nm, diameter);
        });

        var profiles = new BandProfiles(primary, secondary);
        lock (ProfileCache)
        {
            ProfileCache[size] = profiles;
        }

        return profiles;
    }

    /// <summary>
    /// The colour and brightness the rain sends back by angle from the point opposite the Sun (same steps as
    /// <see cref="Profile"/>): the primary bow, light reflected off the drops' outsides and, if asked, the secondary bow.
    /// <paramref name="sunlight"/> is the share of sunlight in each band reaching the rain (null: all of it, white).
    /// </summary>
    public static LinearRgb[] BowColours(DropSize size, bool secondary, IReadOnlyList<double>? sunlight = null)
    {
        var profiles = ProfilesFor(size);
        var bins = (ProfileDegrees * ProfileSteps) + 1;
        var colours = new LinearRgb[bins];
        for (var band = 0; band < SkyOptics.Bands; band++)
        {
            var colour = SkyOptics.BandColours[band] * (sunlight?[band] ?? 1);
            var first = profiles.Primary[band];
            var second = profiles.Secondary[band];
            for (var b = 0; b < bins; b++)
            {
                colours[b] += colour * (first[b] + (secondary ? second[b] : 0));
            }
        }

        return colours;
    }

    /// <summary>The angle from the point opposite the Sun of the sky at <paramref name="altitude"/>, <paramref name="azimuth"/>
    /// from the point's own azimuth (degrees), with the Sun at <paramref name="sunAltitude"/>.</summary>
    public static double AngleFromAntisolar(double altitude, double azimuth, double sunAltitude)
    {
        var cos = (Math.Cos(altitude * Deg) * Math.Cos(azimuth * Deg) * Math.Cos(sunAltitude * Deg))
            - (Math.Sin(altitude * Deg) * Math.Sin(sunAltitude * Deg));
        return Math.Acos(Math.Clamp(cos, -1, 1)) / Deg;
    }

    /// <summary>
    /// How much of a bow's circle of radius <paramref name="radius"/> (degrees) round the point opposite the Sun is above
    /// the horizon, 0–1.
    /// </summary>
    public static double ShareAboveHorizon(double radius, double sunAltitude)
    {
        // The circle's centre is sunAltitude below the horizon; a point at angle φ round it is at altitude
        // asin(sin r sin φ cos h − cos r sin h).
        var sine = Math.Tan(sunAltitude * Deg) / Math.Tan(radius * Deg);
        if (sine >= 1) return 0;
        if (sine <= -1) return 1;
        return 0.5 - (Math.Asin(sine) / Math.PI);
    }

    /// <summary>The highest point of a bow of <paramref name="radius"/> degrees, degrees above the horizon (negative: below).</summary>
    public static double TopAltitude(double radius, double sunAltitude) => radius - sunAltitude;

    #endregion
}
