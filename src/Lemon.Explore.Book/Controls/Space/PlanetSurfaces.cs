using static Lemon.Explore.Book.Controls.Space.Noise;

namespace Lemon.Explore.Book.Controls.Space;

/// <summary>
/// Procedural surface colours, one function per body. Each returns linear-ish RGB in 0..1 and a specular mask,
/// evaluated at a point <c>p</c> on the unit sphere (y = north pole).
/// </summary>
internal static class PlanetSurfaces
{
    private const double Deg = Math.PI / 180;

    public static (double R, double G, double B, double Spec) Shade(SpaceBody body, V3 p, double lat) =>
        body.Surface switch
        {
            SurfaceStyle.Sun => Sun(p, lat),
            SurfaceStyle.Earth => Earth(p, lat),
            _ => (0.5, 0.5, 0.5, 0),
        };

    /// <summary>Colour and opacity of a ring system at <paramref name="radius"/>; no body drawn so far has rings.</summary>
    public static (double R, double G, double B, double A) Ring(SurfaceStyle surface, double radius) => (0, 0, 0, 0);

    private static (double, double, double, double) Sun(V3 p, double lat)
    {
        var granules = Fbm(p * 42, 3);
        var cells = Fbm(p * 6, 4);
        var t = Math.Clamp(0.66 + (granules * 0.55) + (cells * 0.3), 0, 1);
        var (r, g, b) = Mix((1.0, 0.70, 0.26), (1.0, 0.98, 0.88), t);

        // Sunspots only appear in the activity belts, within ±35° of the equator.
        var (d, h) = Worley(p * 7);
        if (h < 0.06 && Math.Abs(lat) < 35 * Deg)
        {
            var penumbra = SmoothStep(0.30, 0.14, d);
            var umbra = SmoothStep(0.16, 0.07, d);
            var dim = 1 - (0.35 * penumbra) - (0.45 * umbra);
            (r, g, b) = (r * dim, g * dim * 0.95, b * dim * 0.9);
        }

        return (r, g, b, 0);
    }

    private static (double, double, double, double) Earth(V3 p, double lat)
    {
        const double sea = 0.07;
        var latDeg = Math.Abs(lat) / Deg;
        var h = Fbm((p * 1.7) + new V3(3.1, 0.2, -1.3), 8);

        (double R, double G, double B) color;
        double spec;
        if (h < sea)
        {
            var depth = Math.Clamp((sea - h) / 0.28, 0, 1);
            color = Mix((0.07, 0.33, 0.50), (0.01, 0.06, 0.20), Math.Pow(depth, 0.5));
            spec = 1;
        }
        else
        {
            var elevation = h - sea;
            var moisture = Fbm((p * 2.4) + new V3(-7.3, 2.2, 5.5), 5);
            var desertBelt = Math.Exp(-Math.Pow((latDeg - 24) / 11, 2));
            var desert = Math.Clamp((desertBelt * 1.5) - (moisture * 3) - 0.45, 0, 1);

            color = Mix((0.11, 0.27, 0.07), (0.30, 0.40, 0.15), Math.Clamp(0.5 + (moisture * 2.5), 0, 1));
            color = Mix(color, (0.80, 0.67, 0.45), desert);
            color = Mix(color, (0.42, 0.36, 0.29), SmoothStep(0.14, 0.32, elevation));
            color = Mix(color, (0.45, 0.45, 0.38), SmoothStep(52, 64, latDeg));
            color = Mix(color, (0.93, 0.95, 0.97), SmoothStep(0.36, 0.44, elevation));
            spec = 0;
        }

        // Polar ice: a ragged edge around 70°.
        var iceLine = 70 + (10 * Fbm(p * 4, 4));
        var ice = SmoothStep(iceLine - 1.5, iceLine + 1.5, latDeg);
        color = Mix(color, (0.93, 0.96, 0.99), ice);
        spec *= 1 - ice;

        // Clouds: domain-warped noise, denser along the equator and the mid-latitude storm tracks.
        var warp = new V3(Fbm(p * 2, 3), Fbm((p * 2) + new V3(5, 5, 5), 3), Fbm((p * 2) + new V3(9, -3, 1), 3));
        var c = Fbm((p + (warp * 0.9)) * 3.2, 6);
        var tracks = (0.06 * Math.Cos(lat * 6)) + 0.02;
        var cover = SmoothStep(0.02, 0.34, c + tracks) * 0.92;
        color = Mix(color, (0.97, 0.98, 1.0), cover);
        spec *= 1 - cover;

        return (color.R, color.G, color.B, spec);
    }

    private static (double R, double G, double B) Mix((double R, double G, double B) a, (double R, double G, double B) b, double t) =>
        (a.R + ((b.R - a.R) * t), a.G + ((b.G - a.G) * t), a.B + ((b.B - a.B) * t));
}
