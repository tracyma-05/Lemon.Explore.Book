using static Lemon.Explore.Book.Controls.Space.Noise;

namespace Lemon.Explore.Book.Controls.Space;

/// <summary>
/// Procedural surface colours, one function per body. Each returns linear-ish RGB in 0..1 and a specular mask,
/// evaluated at a point <paramref name="p"/> on the unit sphere (y = north pole).
/// </summary>
internal static class PlanetSurfaces
{
    private const double Deg = Math.PI / 180;

    public static (double R, double G, double B, double Spec) Shade(CelestialBody body, V3 p, double lat, double lon) =>
        body.Surface switch
        {
            SurfaceStyle.Sun => Sun(p, lat),
            SurfaceStyle.Mercury => Mercury(p),
            SurfaceStyle.Venus => Venus(p, lat),
            SurfaceStyle.Earth => Earth(p, lat),
            SurfaceStyle.Mars => Mars(p, lat, lon),
            SurfaceStyle.Jupiter => Jupiter(p, lat, lon),
            SurfaceStyle.Saturn => Saturn(p, lat),
            SurfaceStyle.Uranus => Uranus(p, lat),
            SurfaceStyle.Neptune => Neptune(p, lat, lon),
            SurfaceStyle.Moon => Moon(p),
            SurfaceStyle.Io => Io(p, lat),
            SurfaceStyle.Europa => Europa(p),
            SurfaceStyle.Ganymede => Ganymede(p, lat),
            SurfaceStyle.Callisto => Callisto(p),
            SurfaceStyle.Titan => Titan(p, lat),
            SurfaceStyle.Enceladus => Enceladus(p, lat),
            SurfaceStyle.Iapetus => Iapetus(p, lat),
            SurfaceStyle.Triton => Triton(p, lat),
            SurfaceStyle.IcyMoon => IcyMoon(body, p),
            SurfaceStyle.RockyMoon => RockyMoon(body, p),
            _ => (0.5, 0.5, 0.5, 0),
        };

    public static (double R, double G, double B, double A) Ring(SurfaceStyle surface, double radius) =>
        surface == SurfaceStyle.Saturn ? SaturnRing(radius) : UranusRing(radius);

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

    private static (double, double, double, double) Mercury(V3 p)
    {
        var n = Fbm(p * 3, 7);
        var tone = 0.46 + (0.38 * n);
        tone += Craters(p, 4, 0.16, 0.85) + Craters(p * 1.7, 9, 0.11, 0.8) + Craters(p * 2.3, 21, 0.07, 0.75);

        // A few young craters with bright rays, as on Mercury and the Moon.
        var (d, h) = Worley(p * 3.2);
        if (h < 0.12)
        {
            tone += 0.22 * Math.Exp(-d * 4) * (0.6 + (0.4 * Perlin(p * 40)));
        }

        return (tone * 1.04, tone * 0.99, tone * 0.92, 0);
    }

    private static (double, double, double, double) Venus(V3 p, double lat)
    {
        var warp = new V3(Fbm(p * 1.6, 4), Fbm(p * 1.6 + new V3(5.2, 1.3, 2.8), 4), Fbm(p * 1.6 + new V3(-3.1, 7.7, 0.4), 4));
        var w = p + (warp * 1.1);
        var c = Fbm(new V3(w.X * 1.6, w.Y * 4.5, w.Z * 1.6), 6);
        var t = Math.Clamp(0.55 + (c * 1.3), 0, 1);
        var (r, g, b) = Mix((0.74, 0.54, 0.30), (0.99, 0.91, 0.72), t);
        var band = 0.035 * Math.Sin((lat * 6) + (c * 5));
        return (r + band, g + band, b + (band * 0.6), 0);
    }

    private static (double, double, double, double) Earth(V3 p, double lat)
    {
        const double sea = 0.07;
        var latDeg = Math.Abs(lat) / Deg;
        var h = Fbm(p * 1.7 + new V3(3.1, 0.2, -1.3), 8);

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
            var moisture = Fbm(p * 2.4 + new V3(-7.3, 2.2, 5.5), 5);
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
        var warp = new V3(Fbm(p * 2, 3), Fbm(p * 2 + new V3(5, 5, 5), 3), Fbm(p * 2 + new V3(9, -3, 1), 3));
        var c = Fbm((p + (warp * 0.9)) * 3.2, 6);
        var tracks = (0.06 * Math.Cos(lat * 6)) + 0.02;
        var cover = SmoothStep(0.02, 0.34, c + tracks) * 0.92;
        color = Mix(color, (0.97, 0.98, 1.0), cover);
        spec *= 1 - cover;

        return (color.R, color.G, color.B, spec);
    }

    private static (double, double, double, double) Mars(V3 p, double lat, double lon)
    {
        var latDeg = Math.Abs(lat) / Deg;
        var n = Fbm(p * 2.2, 7);
        var color = Mix((0.52, 0.22, 0.11), (0.86, 0.52, 0.31), Math.Clamp(0.55 + (n * 1.5), 0, 1));

        // Dark albedo regions such as Syrtis Major.
        var dark = SmoothStep(0.04, 0.24, Fbm(p * 1.3 + new V3(4.4, -2.1, 0.9), 5));
        color = Mix(color, (0.33, 0.18, 0.12), dark * 0.65);

        var crater = Craters(p * 1.4, 8, 0.06, 0.7);
        color = (color.R + crater, color.G + (crater * 0.8), color.B + (crater * 0.6));

        // Valles Marineris: a long canyon just south of the equator.
        var canyonLon = SmoothStep(-1.6, -1.2, lon) * SmoothStep(-0.1, -0.5, lon);
        var canyon = Math.Exp(-Math.Pow((lat + (8 * Deg) + (0.02 * Perlin(p * 20))) / (1.2 * Deg), 2)) * canyonLon;
        color = Mix(color, (0.30, 0.14, 0.09), canyon * 0.7);

        var capLine = 80 + (5 * Fbm(p * 6, 3));
        color = Mix(color, (0.96, 0.92, 0.88), SmoothStep(capLine - 1, capLine + 1, latDeg));
        return (color.R, color.G, color.B, 0);
    }

    private static (double, double, double, double) Jupiter(V3 p, double lat, double lon)
    {
        // Jet streams: noise stretched along longitude, itself warped so the band edges billow and curl.
        var warp = Fbm(p * 2.5, 4);
        var turbulence = Fbm(new V3((p.X * 3) + warp, p.Y * 14, (p.Z * 3) - warp), 6);
        var y = Math.Sin(lat) + (0.045 * turbulence) + (0.015 * Fbm(p * 9, 4));
        var ay = Math.Abs(y);

        var t = Math.Clamp(0.5 + (0.36 * Math.Sin((y * Math.PI * 7) + 0.8)) + (0.16 * Math.Sin((y * Math.PI * 13) + 2.1)), 0, 1);
        var belt = Mix((0.60, 0.40, 0.28), (0.80, 0.55, 0.36), SmoothStep(-0.25, 0.25, Fbm(new V3(p.X * 1.5, p.Y * 6, p.Z * 1.5), 3)));
        var color = Mix(belt, (0.95, 0.89, 0.78), Math.Pow(t, 0.8));
        color = Mix(color, (0.50, 0.32, 0.22), Math.Exp(-Math.Pow((ay - 0.27) / 0.06, 2)) * 0.6);
        color = Mix(color, (0.94, 0.80, 0.60), Math.Exp(-Math.Pow(y / 0.08, 2)) * 0.45);
        color = Mix(color, (0.57, 0.53, 0.50), SmoothStep(0.72, 0.95, ay));
        var detail = 1 + (0.08 * Fbm(p * 24, 3));
        color = (color.R * detail, color.G * detail, color.B * detail);

        // "String of pearls": small white storms in the southern temperate belt.
        var (pearl, pearlHash) = Worley(new V3(p.X * 9, p.Y * 30, p.Z * 9));
        if (pearlHash < 0.35)
        {
            var pearlBand = Math.Exp(-Math.Pow((lat + (40 * Deg)) / (3 * Deg), 2));
            color = Mix(color, (0.97, 0.95, 0.92), SmoothStep(0.32, 0.12, pearl) * pearlBand * 0.9);
        }

        // The Great Red Spot at about 22°S.
        var lat0 = -22.5 * Deg;
        var dLon = KeplerOrbit.NormalizeAngle(lon - 1.2);
        var dx = dLon * Math.Cos(lat0) / 0.21;
        var dy = (lat - lat0 + (0.01 * turbulence)) / 0.095;
        var d = Math.Sqrt((dx * dx) + (dy * dy));
        if (d < 2)
        {
            var swirl = 0.06 * Perlin(new V3(dx * 3, dy * 3, d * 4));
            var spot = SmoothStep(1.0, 0.65, d + swirl);
            var rim = Math.Exp(-Math.Pow((d - 1.1) / 0.14, 2));
            color = Mix(color, (0.95, 0.88, 0.80), rim * 0.55);
            color = Mix(color, (0.76, 0.36, 0.22), spot * 0.85);
        }

        return (color.R, color.G, color.B, 0);
    }

    private static (double, double, double, double) Saturn(V3 p, double lat)
    {
        var turbulence = Fbm(new V3(p.X * 2, p.Y * 10, p.Z * 2), 5);
        var y = Math.Sin(lat) + (0.015 * turbulence);
        var t = 0.5 + (0.5 * Math.Sin((y * Math.PI * 6) + 0.3));
        var color = Mix((0.79, 0.65, 0.43), (0.95, 0.87, 0.66), t);
        color = Mix(color, (0.62, 0.62, 0.56), SmoothStep(0.80, 0.97, Math.Abs(y)));
        var detail = 1 + (0.04 * Fbm(p * 20, 3));
        return (color.R * detail, color.G * detail, color.B * detail, 0);
    }

    private static (double, double, double, double) Uranus(V3 p, double lat)
    {
        var y = Math.Sin(lat);
        var color = Mix((0.58, 0.82, 0.86), (0.74, 0.92, 0.94), SmoothStep(0.3, 0.95, y));
        var band = 0.015 * Math.Sin((y * 22) + (Fbm(p * 3, 3) * 3));
        var detail = 0.012 * Fbm(p * 12, 3);
        return (color.R + band + detail, color.G + band + detail, color.B + band + detail, 0);
    }

    private static (double, double, double, double) Neptune(V3 p, double lat, double lon)
    {
        var turbulence = Fbm(new V3(p.X * 2.5, p.Y * 9, p.Z * 2.5), 5);
        var y = Math.Sin(lat) + (0.03 * turbulence);
        var color = Mix((0.15, 0.27, 0.74), (0.27, 0.45, 0.93), 0.5 + (0.5 * Math.Sin(y * Math.PI * 4)));

        // The Great Dark Spot (as Voyager 2 saw it) with a white companion cloud.
        var lat0 = -20 * Deg;
        var dx = KeplerOrbit.NormalizeAngle(lon - 0.5) * Math.Cos(lat0) / 0.18;
        var dy = (lat - lat0) / 0.08;
        var d = Math.Sqrt((dx * dx) + (dy * dy));
        color = Mix(color, (0.08, 0.13, 0.42), SmoothStep(1.0, 0.6, d) * 0.85);

        var streaks = Fbm(new V3(p.X * 3, p.Y * 26, p.Z * 3), 4);
        var companion = Math.Exp(-Math.Pow((d - 1.4) / 0.25, 2)) * (dy > 0 ? 1 : 0);
        var cloud = Math.Clamp(SmoothStep(0.26, 0.42, streaks) + (companion * 0.8), 0, 1);
        color = Mix(color, (0.86, 0.91, 1.0), cloud * 0.8);
        return (color.R, color.G, color.B, 0);
    }

    private static (double, double, double, double) SaturnRing(double r)
    {
        (double R, double G, double B) color;
        double a;
        if (r < 1.527)
        {
            color = (0.60, 0.55, 0.49);
            a = 0.16 + (0.08 * SmoothStep(1.24, 1.52, r));
        }
        else if (r < 1.95)
        {
            color = (0.91, 0.84, 0.69);
            a = 0.80 + (0.18 * SmoothStep(1.53, 1.75, r));
        }
        else if (r < 2.025)
        {
            color = (0.55, 0.50, 0.45);
            a = 0.06;
        }
        else if (r < 2.27)
        {
            color = (0.83, 0.77, 0.65);
            a = r is > 2.212 and < 2.220 ? 0.05 : 0.58;
        }
        else
        {
            color = (0.85, 0.82, 0.78);
            a = Math.Exp(-Math.Pow((r - 2.325) / 0.004, 2)) * 0.45;
        }

        var ripple = 0.84 + (0.3 * Perlin(r * 90, 1.3, 0.7)) + (0.12 * Perlin(r * 420, 7.1, 2.2));
        return (color.R, color.G, color.B, Math.Clamp(a * ripple, 0, 1));
    }

    private static (double, double, double, double) UranusRing(double r)
    {
        // Narrow rings 6, 5, 4, α, β, η, γ, δ and the brighter, wider ε ring (radii in Uranus radii).
        // Drawn wider and fainter than they are, so they read as rings rather than aliased hairlines.
        ReadOnlySpan<double> narrow = [1.637, 1.652, 1.666, 1.750, 1.786, 1.846, 1.863, 1.890];
        var a = 0.0;
        foreach (var ring in narrow)
        {
            a += 0.10 * Math.Exp(-Math.Pow((r - ring) / 0.007, 2));
        }

        a += 0.26 * Math.Exp(-Math.Pow((r - 2.001) / 0.013, 2));
        return (0.74, 0.79, 0.84, Math.Clamp(a, 0, 1));
    }

    #region moons

    private static (double, double, double, double) Moon(V3 p)
    {
        var tone = 0.58 + (0.14 * Fbm(p * 2.5, 6));

        // Maria: old lava plains, mostly on the hemisphere that faces Earth (longitude 0, +x).
        var maria = SmoothStep(0.02, 0.16, Fbm(p * 1.1 + new V3(2.7, -1.4, 0.6), 5)) * SmoothStep(-0.4, 0.5, p.X);
        tone += (Craters(p, 6, 0.10, 0.7) + Craters(p * 1.9, 14, 0.07, 0.6)) * (1 - (0.6 * maria));
        tone += (0.30 - tone) * maria * 0.85;

        // A few young, bright ray craters (Tycho, Copernicus).
        var (d, h) = Worley(p * 2.6);
        if (h < 0.1) tone += 0.25 * Math.Exp(-d * 4.5) * (0.6 + (0.4 * Perlin(p * 30)));
        return (tone * (1 - (0.03 * maria)), tone, tone * (1 + (0.04 * maria)), 0);
    }

    private static (double, double, double, double) Io(V3 p, double lat)
    {
        var f = Fbm(p * 3, 5);
        var color = Mix((0.93, 0.85, 0.42), (0.96, 0.94, 0.80), SmoothStep(0.08, 0.3, f));
        color = Mix(color, (0.86, 0.55, 0.24), SmoothStep(-0.08, -0.3, f));
        color = Mix(color, (0.58, 0.44, 0.30), SmoothStep(50, 75, Math.Abs(lat) / Deg));

        // Volcanoes: dark calderas, some ringed by red sulphur fall-out like Pele.
        var (d, h) = Worley(p * 6);
        if (h < 0.08)
        {
            color = Mix(color, (0.78, 0.33, 0.18), Math.Exp(-Math.Pow((d - 0.26) / 0.07, 2)) * 0.7);
        }

        if (h < 0.4)
        {
            color = Mix(color, (0.14, 0.10, 0.07), SmoothStep(0.11, 0.04, d));
        }

        return (color.R, color.G, color.B, 0);
    }

    private static (double, double, double, double) Europa(V3 p)
    {
        var color = Mix((0.84, 0.80, 0.72), (0.93, 0.91, 0.86), Math.Clamp(0.5 + (Fbm(p * 3, 4) * 1.5), 0, 1));

        // Chaos terrain: mottled brown patches.
        color = Mix(color, (0.68, 0.53, 0.40), SmoothStep(0.12, 0.3, Fbm(p * 2.2 + new V3(1, 4, 2), 5)) * 0.8);

        // Lineae: long reddish-brown cracks along the zero set of noise, at three scales.
        var lines = 0.0;
        lines += 0.75 * (1 - SmoothStep(0, 0.035, Math.Abs(Perlin(p * 2.5 + new V3(3, 1, 2)))));
        lines += 0.55 * (1 - SmoothStep(0, 0.03, Math.Abs(Perlin(p * 5 + new V3(-2, 7, 1)))));
        lines += 0.35 * (1 - SmoothStep(0, 0.025, Math.Abs(Perlin(p * 11 + new V3(5, -3, 9)))));
        color = Mix(color, (0.55, 0.34, 0.22), Math.Clamp(lines, 0, 1) * 0.75);
        return (color.R, color.G, color.B, 0);
    }

    private static (double, double, double, double) Ganymede(V3 p, double lat)
    {
        var light = SmoothStep(-0.05, 0.12, Fbm(p * 1.8, 5));
        var grooves = 0.05 * Math.Sin(Perlin(p * 4) * 40);
        var color = Mix((0.40, 0.36, 0.31), (0.70 + grooves, 0.68 + grooves, 0.64 + grooves), light);
        var crater = Craters(p * 1.3, 9, 0.08, 0.6);
        color = (color.R + crater, color.G + crater, color.B + crater);

        var (d, h) = Worley(p * 3.5);
        if (h < 0.15) color = Mix(color, (0.92, 0.92, 0.92), Math.Exp(-d * 6) * 0.6);
        color = Mix(color, (0.82, 0.84, 0.86), SmoothStep(55, 78, Math.Abs(lat) / Deg) * 0.6);
        return (color.R, color.G, color.B, 0);
    }

    private static (double, double, double, double) Callisto(V3 p)
    {
        var tone = 0.34 + (0.08 * Fbm(p * 3, 5));
        (double R, double G, double B) color = (tone * 1.08, tone, tone * 0.86);

        // Bright, icy crater speckles all over the dark crust.
        foreach (var scale in (ReadOnlySpan<double>)[9, 18, 34])
        {
            var (d, h) = Worley(p * scale + new V3(scale, 0, 0));
            if (h < 0.6) color = Mix(color, (0.80, 0.78, 0.74), SmoothStep(0.22, 0.05, d) * 0.55);
        }

        // Valhalla: a bright multi-ring basin.
        var a = Math.Acos(Math.Clamp(V3.Dot(p, new V3(0.2, 0.3, 0.93).Normalized()), -1, 1));
        var rings = Math.Exp(-a * 4) * (0.5 + (0.5 * Math.Cos(a * 60)));
        color = Mix(color, (0.72, 0.68, 0.62), Math.Clamp((Math.Exp(-a * 14) * 0.8) + (rings * 0.25), 0, 1));
        return (color.R, color.G, color.B, 0);
    }

    private static (double, double, double, double) Titan(V3 p, double lat)
    {
        // An almost featureless orange haze; darker dune fields along the equator show faintly through it.
        var haze = 0.04 * Fbm(p * 3, 4);
        var dunes = SmoothStep(25, 5, Math.Abs(lat) / Deg) * SmoothStep(0, 0.2, Fbm(p * 2.5, 4)) * 0.12;
        var band = 0.03 * Math.Sin(lat * 3);
        return (0.88 + haze - dunes + band, 0.60 + haze - dunes + band, 0.27 + (haze * 0.5) - (dunes * 0.5), 0);
    }

    private static (double, double, double, double) Enceladus(V3 p, double lat)
    {
        var tone = 0.93 + (0.04 * Fbm(p * 4, 4));
        var crater = Craters(p, 10, 0.05, 0.6) * SmoothStep(-20, 30, lat / Deg);
        (double R, double G, double B) color = (tone + crater, tone + crater + 0.01, tone + crater + 0.03);

        // The "tiger stripes": four parallel fractures near the south pole.
        var q = (p.X * 0.8) + (p.Z * 0.6) + (0.04 * Perlin(p * 6));
        var stripe = Math.Exp(-Math.Pow(Math.Sin(q * Math.PI * 5) / 0.12, 2)) * SmoothStep(-55, -68, lat / Deg);
        color = Mix(color, (0.55, 0.68, 0.78), stripe * 0.85);
        return (color.R, color.G, color.B, 0);
    }

    private static (double, double, double, double) Iapetus(V3 p, double lat)
    {
        // Dark leading hemisphere (Cassini Regio), bright trailing hemisphere and poles.
        var dark = SmoothStep(-0.1, 0.25, p.Z + (0.15 * Fbm(p * 4, 4))) * (1 - SmoothStep(40, 70, Math.Abs(lat) / Deg));
        var color = Mix((0.86, 0.84, 0.80), (0.22, 0.16, 0.11), dark);
        var crater = Craters(p, 8, 0.07, 0.65);
        color = (color.R + crater, color.G + crater, color.B + crater);

        // The equatorial ridge.
        color = Mix(color, (0.5, 0.45, 0.4), Math.Exp(-Math.Pow(lat / (1.5 * Deg), 2)) * 0.3);
        return (color.R, color.G, color.B, 0);
    }

    private static (double, double, double, double) Triton(V3 p, double lat)
    {
        var color = Mix((0.82, 0.76, 0.72), (0.90, 0.84, 0.80), Math.Clamp(0.5 + Fbm(p * 3, 4), 0, 1));

        // "Cantaloupe" terrain: dimpled cells with darker rims.
        var (d, _) = Worley(p * 14);
        color = Mix(color, (0.66, 0.64, 0.64), SmoothStep(0.45, 0.75, d) * 0.6);

        // Pinkish nitrogen-ice polar cap with dark plume streaks.
        var cap = SmoothStep(-10, -25, lat / Deg);
        color = Mix(color, (0.94, 0.85, 0.80), cap);
        var streaks = SmoothStep(0.3, 0.45, Fbm(new V3(p.X * 10, p.Y * 3, p.Z * 10), 3)) * cap;
        color = Mix(color, (0.42, 0.36, 0.34), streaks * 0.7);
        return (color.R, color.G, color.B, 0);
    }

    private static (double, double, double, double) IcyMoon(CelestialBody body, V3 p)
    {
        var tint = body.BaseColor;
        var q = p + Seed(body);
        var tone = 1 + (0.25 * Fbm(q * 3, 5));
        tone += (Craters(q, 5, 0.18, 0.8) + Craters(q * 1.7, 11, 0.12, 0.7) + Craters(q * 2.3, 23, 0.07, 0.6)) * 1.4;

        // The giant Herschel crater on Mimas.
        if (body.Id == "Mimas") tone += BigCrater(p, new V3(0.9, 0.05, -0.4), 0.33) * 1.5;

        return (tint.R / 255.0 * tone, tint.G / 255.0 * tone, tint.B / 255.0 * tone, 0);
    }

    private static (double, double, double, double) RockyMoon(CelestialBody body, V3 p)
    {
        var tint = body.BaseColor;
        var q = p + Seed(body);
        var tone = 1 + (0.3 * Fbm(q * 2.5, 5)) + ((Craters(q, 4, 0.2, 0.8) + Craters(q * 1.6, 10, 0.12, 0.7)) * 1.6);

        // Stickney, the large crater on Phobos.
        if (body.Id == "Phobos") tone += BigCrater(p, new V3(0.85, 0.1, 0.5), 0.45) * 1.6;

        return (tint.R / 255.0 * tone, tint.G / 255.0 * tone, tint.B / 255.0 * tone, 0);
    }

    /// <summary>Per-body offset into the noise field, so moons sharing a surface style do not share a crater map.</summary>
    private static V3 Seed(CelestialBody body)
    {
        var h = body.Id.Aggregate(17, (acc, c) => unchecked((acc * 31) + c));
        return new V3(Noise.Hash(h, 1, 0, 1) * 50, Noise.Hash(h, 2, 0, 1) * 50, Noise.Hash(h, 3, 0, 1) * 50);
    }

    /// <summary>One large crater (radius in radians) centred on a direction: dark floor, bright rim, central peak.</summary>
    private static double BigCrater(V3 p, V3 centre, double radius)
    {
        var a = Math.Acos(Math.Clamp(V3.Dot(p, centre.Normalized()), -1, 1));
        var rim = Math.Exp(-Math.Pow((a - radius) / (radius * 0.15), 2)) * 0.12;
        var floor = a < radius ? -0.10 * (1 - Math.Pow(a / radius, 2)) : 0;
        var peak = Math.Exp(-Math.Pow(a / (radius * 0.12), 2)) * 0.1;
        return rim + floor + peak;
    }

    #endregion

    /// <summary>Crater field from cellular noise: a dark bowl inside a brighter rim. Returns a brightness offset.</summary>
    private static double Craters(V3 p, double scale, double amplitude, double density)
    {
        var (d, h) = Worley(p * scale);
        if (h > density) return 0;
        var radius = 0.16 + (0.24 * Noise.Hash((int)(h * 100000), 7, 3, 5));
        var rim = Math.Exp(-Math.Pow((d - radius) / (radius * 0.28), 2));
        var bowl = d < radius ? -(1 - ((d / radius) * (d / radius))) * 0.8 : 0;
        return amplitude * ((rim * 0.55) + bowl);
    }

    private static (double R, double G, double B) Mix((double R, double G, double B) a, (double R, double G, double B) b, double t) =>
        (a.R + ((b.R - a.R) * t), a.G + ((b.G - a.G) * t), a.B + ((b.B - a.B) * t));
}
