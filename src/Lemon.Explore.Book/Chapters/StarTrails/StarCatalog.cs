namespace Lemon.Explore.Book.Chapters.StarTrails;

/// <summary>A star of the sky: where it is (J2000), how bright and what colour.</summary>
/// <param name="Id">Its name in the catalogue; named stars have a <c>Trails_Star_&lt;Id&gt;</c> string.</param>
/// <param name="Ra">Right ascension, hours.</param>
/// <param name="Dec">Declination, degrees.</param>
/// <param name="Magnitude">Visual magnitude (smaller is brighter).</param>
/// <param name="ColorIndex">B−V colour index: blue-white below 0, yellow near 0.6, orange-red above 1.2.</param>
/// <param name="Named">Shown with its name on the pages.</param>
public sealed record SkyStar(string Id, double Ra, double Dec, double Magnitude, double ColorIndex, bool Named = false);

/// <summary>A constellation (or asterism) drawn with lines between its stars.</summary>
/// <param name="Id">Its id; the name is <c>Trails_Con_&lt;Id&gt;</c>.</param>
/// <param name="Lines">Pairs of star ids joined by a line.</param>
public sealed record SkyFigure(string Id, IReadOnlyList<(string A, string B)> Lines);

/// <summary>
/// The bright stars of the whole sky with the figures of a few well-known constellations, plus a fixed scatter of
/// fainter stars (a seeded random fill, not real positions) so long exposures show as many trails as a photograph.
/// Positions of the bright stars are J2000 catalogue values typed in from memory, good to about a tenth of a degree.
/// </summary>
public static class StarCatalog
{
    /// <summary>The real stars, brightest first within each constellation.</summary>
    public static IReadOnlyList<SkyStar> Bright { get; } =
    [
        // Ursa Major (the Big Dipper)
        new("Dubhe", 11.0621, 61.751, 1.79, 1.07, true),
        new("Merak", 11.0307, 56.383, 2.37, -0.02),
        new("Phecda", 11.8972, 53.695, 2.44, 0.04),
        new("Megrez", 12.2571, 57.033, 3.31, 0.08),
        new("Alioth", 12.9005, 55.960, 1.77, -0.02),
        new("Mizar", 13.3988, 54.925, 2.27, 0.02),
        new("Alkaid", 13.7923, 49.313, 1.86, -0.19, true),

        // Ursa Minor
        new("Polaris", 2.5303, 89.264, 1.98, 0.60, true),
        new("Kochab", 14.8451, 74.156, 2.08, 1.47),
        new("Pherkad", 15.3455, 71.834, 3.05, 0.05),
        new("Yildun", 17.5369, 86.586, 4.36, 0.02),
        new("EpsUMi", 16.7662, 82.037, 4.21, 0.89),
        new("ZetUMi", 15.7343, 77.794, 4.32, 0.04),
        new("EtaUMi", 16.2918, 75.755, 4.95, 0.37),

        // Cassiopeia
        new("Schedar", 0.6751, 56.537, 2.24, 1.17),
        new("Caph", 0.1530, 59.150, 2.28, 0.34),
        new("GamCas", 0.9451, 60.717, 2.47, -0.15),
        new("Ruchbah", 1.4303, 60.235, 2.68, 0.13),
        new("Segin", 1.9066, 63.670, 3.37, -0.15),

        // Orion
        new("Rigel", 5.2423, -8.202, 0.13, -0.03, true),
        new("Betelgeuse", 5.9195, 7.407, 0.50, 1.85, true),
        new("Bellatrix", 5.4189, 6.350, 1.64, -0.22),
        new("Alnilam", 5.6036, -1.202, 1.69, -0.18),
        new("Alnitak", 5.6793, -1.943, 1.77, -0.21),
        new("Saiph", 5.7959, -9.670, 2.09, -0.17),
        new("Mintaka", 5.5334, -0.299, 2.23, -0.22),
        new("Meissa", 5.5856, 9.934, 3.39, -0.18),

        // Taurus, Auriga, Gemini, the dogs
        new("Aldebaran", 4.5987, 16.509, 0.86, 1.54, true),
        new("Elnath", 5.4382, 28.608, 1.65, -0.13),
        new("Alcyone", 3.7914, 24.105, 2.87, -0.09),
        new("Capella", 5.2782, 45.998, 0.08, 0.80, true),
        new("Menkalinan", 5.9921, 44.948, 1.90, 0.08),
        new("Pollux", 7.7553, 28.026, 1.14, 1.00, true),
        new("Castor", 7.5767, 31.888, 1.58, 0.03, true),
        new("Alhena", 6.6285, 16.399, 1.93, 0.00),
        new("Sirius", 6.7525, -16.716, -1.46, 0.00, true),
        new("Adhara", 6.9771, -28.972, 1.50, -0.21),
        new("Wezen", 7.1399, -26.393, 1.84, 0.68),
        new("Mirzam", 6.3783, -17.956, 1.98, -0.24),
        new("Aludra", 7.4016, -29.303, 2.45, -0.08),
        new("Procyon", 7.6550, 5.225, 0.34, 0.42, true),

        // Leo, Virgo, Bootes
        new("Regulus", 10.1395, 11.967, 1.35, -0.11, true),
        new("Denebola", 11.8177, 14.572, 2.14, 0.09),
        new("Algieba", 10.3329, 19.841, 2.08, 1.13),
        new("Zosma", 11.2351, 20.524, 2.56, 0.12),
        new("Chertan", 11.2373, 15.430, 3.33, 0.00),
        new("EtaLeo", 10.1222, 16.763, 3.48, -0.03),
        new("EpsLeo", 9.7642, 23.774, 2.98, 0.81),
        new("Rasalas", 9.8794, 26.007, 3.88, 1.22),
        new("Adhafera", 10.2782, 23.417, 3.44, 0.31),
        new("Spica", 13.4199, -11.161, 0.97, -0.23, true),
        new("Arcturus", 14.2610, 19.182, -0.05, 1.23, true),

        // The summer sky: Lyra, Cygnus, Aquila
        new("Vega", 18.6156, 38.784, 0.03, 0.00, true),
        new("Sheliak", 18.8347, 33.363, 3.52, 0.00),
        new("Sulafat", 18.9824, 32.690, 3.25, -0.05),
        new("ZetLyr", 18.7462, 37.605, 4.36, 0.19),
        new("DelLyr", 18.9084, 36.899, 4.30, 1.68),
        new("Deneb", 20.6905, 45.280, 1.25, 0.09, true),
        new("Sadr", 20.3705, 40.257, 2.23, 0.68),
        new("Gienah", 20.7702, 33.970, 2.48, 1.03),
        new("DelCyg", 19.7496, 45.131, 2.87, -0.03),
        new("Albireo", 19.5120, 27.960, 3.08, 1.13),
        new("Altair", 19.8464, 8.868, 0.76, 0.22, true),
        new("Tarazed", 19.7710, 10.613, 2.72, 1.52),
        new("Alshain", 19.9219, 6.407, 3.71, 0.86),

        // Scorpius
        new("Antares", 16.4901, -26.432, 1.06, 1.83, true),
        new("Acrab", 16.0906, -19.805, 2.62, -0.07),
        new("Dschubba", 16.0056, -22.622, 2.29, -0.12),
        new("PiSco", 15.9809, -26.114, 2.89, -0.19),
        new("SigSco", 16.3532, -25.593, 2.90, 0.13),
        new("TauSco", 16.5980, -28.216, 2.82, -0.25),
        new("EpsSco", 16.8361, -34.293, 2.29, 1.15),
        new("MuSco", 16.8645, -38.048, 3.04, -0.20),
        new("ZetSco", 16.9097, -42.362, 3.62, 1.37),
        new("EtaSco", 17.2026, -43.239, 3.33, 0.41),
        new("Sargas", 17.6220, -42.998, 1.86, 0.40),
        new("IotSco", 17.7931, -40.127, 2.99, 0.51),
        new("KapSco", 17.7081, -39.030, 2.39, -0.22),
        new("Shaula", 17.5601, -37.104, 1.62, -0.22),
        new("Lesath", 17.5127, -37.296, 2.70, -0.22),

        // Sagittarius (the Teapot)
        new("KausAustralis", 18.4029, -34.385, 1.85, -0.03),
        new("Nunki", 18.9211, -26.297, 2.05, -0.13),
        new("Ascella", 19.0435, -29.880, 2.60, 0.08),
        new("KausMedia", 18.3499, -29.828, 2.70, 1.38),
        new("KausBorealis", 18.4662, -25.422, 2.81, 1.04),
        new("Alnasl", 18.0968, -30.424, 2.99, 1.00),
        new("PhiSgr", 18.7609, -26.991, 3.17, -0.11),
        new("TauSgr", 19.1157, -27.670, 3.32, 1.19),

        // The far south
        new("Acrux", 12.4433, -63.099, 0.77, -0.24, true),
        new("Mimosa", 12.7954, -59.689, 1.25, -0.23),
        new("Gacrux", 12.5194, -57.113, 1.59, 1.60),
        new("DelCru", 12.2524, -58.749, 2.79, -0.23),
        new("RigilKentaurus", 14.6601, -60.834, -0.27, 0.71, true),
        new("Hadar", 14.0637, -60.373, 0.61, -0.23),
        new("Canopus", 6.3992, -52.696, -0.74, 0.15, true),
        new("Achernar", 1.6286, -57.237, 0.46, -0.16, true),
        new("Fomalhaut", 22.9608, -29.622, 1.16, 0.09, true),
        new("Miaplacidus", 9.2200, -69.717, 1.67, 0.00),
        new("Avior", 8.3752, -59.510, 1.86, 1.20),
        new("GamVel", 8.1589, -47.337, 1.78, -0.22),
        new("DelVel", 8.7451, -54.708, 1.96, 0.04),
        new("Suhail", 9.1333, -43.433, 2.21, 1.66),
        new("Alnair", 22.1372, -46.961, 1.74, -0.07),
        new("Peacock", 20.4275, -56.735, 1.94, -0.20),
        new("Atria", 16.8111, -69.028, 1.91, 1.44),
        new("SigOct", 21.1464, -88.956, 5.47, 0.27, true),
        new("Naos", 8.0597, -40.003, 2.25, -0.27),
        new("Phact", 5.6608, -34.074, 2.65, -0.12),

        // Pegasus, Andromeda, Perseus and the autumn sky
        new("Markab", 23.0794, 15.205, 2.49, -0.04),
        new("Scheat", 23.0629, 28.083, 2.42, 1.67),
        new("Algenib", 0.2206, 15.184, 2.83, -0.23),
        new("Alpheratz", 0.1398, 29.091, 2.06, -0.11),
        new("Enif", 21.7365, 9.875, 2.39, 1.53),
        new("Mirach", 1.1622, 35.621, 2.05, 1.58),
        new("Almach", 2.0650, 42.330, 2.10, 1.37),
        new("Mirfak", 3.4054, 49.861, 1.79, 0.48),
        new("Algol", 3.1361, 40.956, 2.12, -0.05),
        new("Hamal", 2.1196, 23.463, 2.01, 1.15),
        new("Diphda", 0.7265, -17.987, 2.04, 1.02),
        new("Menkar", 3.0380, 4.090, 2.54, 1.64),

        // And a few more bright ones
        new("Rasalhague", 17.5822, 12.560, 2.08, 0.16),
        new("Kornephoros", 16.5037, 21.490, 2.78, 0.95),
        new("Alphecca", 15.5781, 26.715, 2.23, -0.02),
        new("Eltanin", 17.9434, 51.489, 2.23, 1.52),
        new("Thuban", 14.0732, 64.376, 3.65, -0.05),
        new("Alphard", 9.4598, -8.659, 1.98, 1.44),
        new("Arneb", 5.5455, -17.822, 2.58, 0.21),
        new("Zubenelgenubi", 14.8480, -16.042, 2.75, 0.15),
    ];

    /// <summary>The figures drawn with lines.</summary>
    public static IReadOnlyList<SkyFigure> Figures { get; } =
    [
        new("BigDipper", [("Dubhe", "Merak"), ("Merak", "Phecda"), ("Phecda", "Megrez"), ("Megrez", "Dubhe"), ("Megrez", "Alioth"), ("Alioth", "Mizar"), ("Mizar", "Alkaid")]),
        new("LittleDipper", [("Polaris", "Yildun"), ("Yildun", "EpsUMi"), ("EpsUMi", "ZetUMi"), ("ZetUMi", "Kochab"), ("Kochab", "Pherkad"), ("Pherkad", "EtaUMi"), ("EtaUMi", "ZetUMi")]),
        new("Cassiopeia", [("Caph", "Schedar"), ("Schedar", "GamCas"), ("GamCas", "Ruchbah"), ("Ruchbah", "Segin")]),
        new("Orion", [("Betelgeuse", "Meissa"), ("Meissa", "Bellatrix"), ("Betelgeuse", "Bellatrix"), ("Betelgeuse", "Alnitak"), ("Bellatrix", "Mintaka"), ("Mintaka", "Alnilam"), ("Alnilam", "Alnitak"), ("Alnitak", "Saiph"), ("Mintaka", "Rigel")]),
        new("Taurus", [("Aldebaran", "Elnath"), ("Aldebaran", "Alcyone")]),
        new("Gemini", [("Castor", "Pollux"), ("Pollux", "Alhena")]),
        new("CanisMajor", [("Mirzam", "Sirius"), ("Sirius", "Wezen"), ("Wezen", "Adhara"), ("Wezen", "Aludra")]),
        new("Leo", [("Regulus", "EtaLeo"), ("EtaLeo", "Algieba"), ("Algieba", "Adhafera"), ("Adhafera", "Rasalas"), ("Rasalas", "EpsLeo"), ("Algieba", "Zosma"), ("Zosma", "Denebola"), ("Denebola", "Chertan"), ("Chertan", "Regulus"), ("Chertan", "Zosma")]),
        new("Lyra", [("Vega", "ZetLyr"), ("ZetLyr", "DelLyr"), ("DelLyr", "Sulafat"), ("Sulafat", "Sheliak"), ("Sheliak", "ZetLyr")]),
        new("Cygnus", [("Deneb", "Sadr"), ("Sadr", "Albireo"), ("Gienah", "Sadr"), ("Sadr", "DelCyg")]),
        new("Aquila", [("Tarazed", "Altair"), ("Altair", "Alshain")]),
        new("SummerTriangle", [("Vega", "Deneb"), ("Deneb", "Altair"), ("Altair", "Vega")]),
        new("Scorpius", [("Acrab", "Dschubba"), ("Dschubba", "PiSco"), ("Dschubba", "SigSco"), ("SigSco", "Antares"), ("Antares", "TauSco"), ("TauSco", "EpsSco"), ("EpsSco", "MuSco"), ("MuSco", "ZetSco"), ("ZetSco", "EtaSco"), ("EtaSco", "Sargas"), ("Sargas", "IotSco"), ("IotSco", "KapSco"), ("KapSco", "Shaula"), ("Shaula", "Lesath")]),
        new("Sagittarius", [("Alnasl", "KausMedia"), ("KausMedia", "KausAustralis"), ("KausAustralis", "Alnasl"), ("KausMedia", "KausBorealis"), ("KausBorealis", "PhiSgr"), ("PhiSgr", "KausMedia"), ("PhiSgr", "Nunki"), ("Nunki", "TauSgr"), ("TauSgr", "Ascella"), ("Ascella", "PhiSgr"), ("Ascella", "KausAustralis")]),
        new("Crux", [("Acrux", "Gacrux"), ("Mimosa", "DelCru")]),
        new("Pegasus", [("Markab", "Scheat"), ("Scheat", "Alpheratz"), ("Alpheratz", "Algenib"), ("Algenib", "Markab"), ("Markab", "Enif"), ("Alpheratz", "Mirach"), ("Mirach", "Almach")]),
    ];

    private static readonly Dictionary<string, SkyStar> ById = Bright.ToDictionary(s => s.Id);

    public static SkyStar Get(string id) => ById[id];

    public static SkyStar? Find(string? id) => id is not null && ById.TryGetValue(id, out var star) ? star : null;

    /// <summary>The named stars, which have labels and can be clicked.</summary>
    public static IReadOnlyList<SkyStar> Named { get; } = Bright.Where(s => s.Named).ToList();

    /// <summary>A figure's centre on the sky (the mean of its stars' directions), for its name.</summary>
    public static (double Ra, double Dec) Center(SkyFigure figure)
    {
        double x = 0, y = 0, z = 0;
        foreach (var id in figure.Lines.SelectMany(l => new[] { l.A, l.B }).Distinct())
        {
            var star = Get(id);
            var ra = star.Ra * 15 * Math.PI / 180;
            var dec = star.Dec * Math.PI / 180;
            x += Math.Cos(dec) * Math.Cos(ra);
            y += Math.Cos(dec) * Math.Sin(ra);
            z += Math.Sin(dec);
        }

        var length = Math.Sqrt((x * x) + (y * y) + (z * z));
        var centerRa = Math.Atan2(y, x) * 180 / Math.PI / 15;
        return (((centerRa % 24) + 24) % 24, Math.Asin(z / length) * 180 / Math.PI);
    }

    /// <summary>
    /// The faint fill: <paramref name="count"/> stars of magnitude 3.5–6 spread evenly over the sky from a fixed seed, so
    /// they are the same every time (they are not real stars).
    /// </summary>
    public static IReadOnlyList<SkyStar> Fill(int count = 900)
    {
        var random = new Random(20261010);
        var stars = new List<SkyStar>(count);
        for (var i = 0; i < count; i++)
        {
            var ra = random.NextDouble() * 24;
            var dec = Math.Asin((random.NextDouble() * 2) - 1) * 180 / Math.PI;

            // More faint stars than bright ones, as in the real sky.
            var magnitude = 6 - (2.5 * Math.Pow(random.NextDouble(), 2.2));
            var colour = (random.NextDouble() * 1.5) - 0.2;
            stars.Add(new SkyStar("fill" + i, ra, dec, magnitude, colour));
        }

        return stars;
    }
}
