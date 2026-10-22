namespace Lemon.Explore.Book.Chapters.WaterCycle;

/// <summary>A point of the cycle landscape: <see cref="X"/> across it (0 at the left, the open sea; 1 at the right, behind the mountain) and <see cref="Z"/> metres above sea level.</summary>
public readonly record struct ScenePoint(double X, double Z);

/// <summary>A step of a followed drop's journey: where it goes, and for how many seconds of playback.</summary>
public sealed record DropStep(DropLeg Leg, IReadOnlyList<ScenePoint> Path, double Seconds);

/// <summary>
/// The landscape of the cycle view, as a cross-section from the sea to over a mountain: the ground's height, the water
/// table under it, where the coast and the clouds are, the river down to the sea, and the paths a followed drop takes.
/// </summary>
/// <remarks>Heights are real metres, but across is squeezed: the whole picture is some hundred kilometres wide.</remarks>
public static class CycleScene
{
    /// <summary>The top of the sky drawn, metres.</summary>
    public const double SkyTop = 7000;

    /// <summary>The bottom of the ground drawn, metres (below sea level).</summary>
    public const double Bottom = -1900;

    /// <summary>Where the land meets the sea.</summary>
    public const double Coast = 0.31;

    /// <summary>The mountain's top.</summary>
    public const double PeakX = 0.81;

    /// <summary>Sea-level air temperature of today's world in the picture, °C.</summary>
    public const double SeaLevelTemperature = 15;

    /// <summary>The base of the clouds over the sea and of the mountain's cloud, metres.</summary>
    public const double CloudBase = 1700;

    /// <summary>The windward slope's rain falls between these.</summary>
    public const double RainFrom = 0.56;
    public const double RainTo = 0.79;

    /// <summary>The trees of the plain stand between these.</summary>
    public const double ForestFrom = 0.36;
    public const double ForestTo = 0.56;

    // The ground's profile: the sea floor rising to the coast, a low plain, the mountain, its far side.
    private static readonly double[] GroundX = [0, 0.16, 0.25, Coast, 0.34, 0.42, 0.5, 0.56, 0.63, 0.71, 0.78, PeakX, 0.845, 0.9, 0.96, 1];
    private static readonly double[] GroundZ = [-1350, -1180, -420, 0, 55, 110, 170, 300, 1100, 2600, 4150, 4450, 4150, 2700, 1500, 1150];
    private static readonly double[] GroundSlopes = Slopes(GroundX, GroundZ);

    /// <summary>The ground's (or the sea floor's) height, metres.</summary>
    public static double Ground(double x) => Hermite(GroundX, GroundZ, GroundSlopes, Math.Clamp(x, 0, 1));

    /// <summary>
    /// The top of the water-soaked rock under the land, metres: a subdued copy of the ground, meeting the sea at the coast,
    /// so groundwater flows from under the hills toward the sea.
    /// </summary>
    public static double WaterTable(double x)
    {
        if (x <= Coast) return 0;
        var ground = Math.Max(0, Ground(x));
        return (0.4 * ground) - (60 * (1 - Math.Exp(-ground / 150)));
    }

    /// <summary>The height of the freezing level (0 °C) in a world <paramref name="warming"/> degrees warmer than today, metres.</summary>
    public static double FreezingLevel(double warming) => (SeaLevelTemperature + warming) / WaterCycleModel.StandardLapseRate;

    /// <summary>The lowest snow that lies all year: a little above the freezing level of the air.</summary>
    public static double SnowLine(double warming) => FreezingLevel(warming) + 500;

    /// <summary>The windward side's highest point under a height, on the way up the mountain; NaN when the peak is lower.</summary>
    public static double WindwardAt(double height)
    {
        if (height > Ground(PeakX)) return double.NaN;
        if (height <= 0) return Coast;
        double low = Coast, high = PeakX;
        for (var i = 0; i < 50; i++)
        {
            var mid = (low + high) / 2;
            if (Ground(mid) < height) low = mid;
            else high = mid;
        }

        return (low + high) / 2;
    }

    /// <summary>Where the river rises: where the snow line meets the windward slope (or near the top in a world too warm for snow).</summary>
    public static double RiverSource(double warming)
    {
        var source = WindwardAt(SnowLine(warming));
        return double.IsNaN(source) ? 0.77 : Math.Min(source, 0.77);
    }

    /// <summary>The river from its source down the slope and across the plain to the coast, a point every so often.</summary>
    public static IReadOnlyList<ScenePoint> River(double warming) => AlongGround(RiverSource(warming), Coast, 40);

    /// <summary>Points along the ground from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static IReadOnlyList<ScenePoint> AlongGround(double from, double to, int count)
    {
        var points = new List<ScenePoint>(count + 1);
        for (var i = 0; i <= count; i++)
        {
            var x = WaterCycleModel.Lerp(from, to, (double)i / count);
            points.Add(new ScenePoint(x, Math.Max(0, Ground(x))));
        }

        return points;
    }

    /// <summary>Points along the water table from <paramref name="from"/> to the coast, then out under the sea.</summary>
    private static IReadOnlyList<ScenePoint> AlongWaterTable(double from, double depth, int count)
    {
        var points = new List<ScenePoint>(count + 2);
        for (var i = 0; i <= count; i++)
        {
            var x = WaterCycleModel.Lerp(from, Coast, (double)i / count);
            points.Add(new ScenePoint(x, WaterTable(x) - (depth * Math.Min(1, (x - Coast) / 0.05))));
        }

        // Seeping out through the sea floor near the shore.
        points.Add(new ScenePoint(Coast - 0.03, Ground(Coast - 0.03) + 20));
        return points;
    }

    #region a followed drop

    /// <summary>
    /// The steps of a drop's journey from the open sea back to the sea. Each step starts where the last ended. The
    /// seconds are playback time, not real time: the real stays (<see cref="WaterCycleModel.TypicalDays"/>) run from
    /// minutes to centuries.
    /// </summary>
    public static IReadOnlyList<DropStep> Journey(DropRoute route, double warming)
    {
        var steps = new List<DropStep>();
        var sea = new ScenePoint(0.11, -520);
        var surface = new ScenePoint(0.15, 0);
        steps.Add(new DropStep(DropLeg.Ocean, [sea, new ScenePoint(0.13, -260), surface], 2.4));

        switch (route)
        {
            case DropRoute.Sea:
            {
                // Up into a cloud over the sea, and straight back down as rain.
                var cloud = new ScenePoint(0.2, CloudBase + 450);
                steps.Add(new DropStep(DropLeg.Evaporation, Rise(surface, new ScenePoint(0.18, CloudBase - 150), 0.012), 3.2));
                steps.Add(new DropStep(DropLeg.Condensation, [new ScenePoint(0.18, CloudBase - 150), new ScenePoint(0.19, CloudBase + 200), cloud], 2.2));
                steps.Add(new DropStep(DropLeg.Rain, [cloud, new ScenePoint(0.21, CloudBase), new ScenePoint(0.215, 0)], 1.6));
                steps.Add(new DropStep(DropLeg.Ocean, [new ScenePoint(0.215, 0), new ScenePoint(0.17, -300), sea], 2.4));
                break;
            }

            case DropRoute.Snow:
            {
                var landing = 0.79;
                var top = new ScenePoint(0.765, Ground(landing) + 600);
                AddUpAndAcross(steps, surface, top);
                var ground = new ScenePoint(landing, Ground(landing));
                steps.Add(new DropStep(DropLeg.Snowfall, [top, new ScenePoint(0.785, WaterCycleModel.Lerp(top.Z, ground.Z, 0.5)), ground], 2.6));
                steps.Add(new DropStep(DropLeg.Snowpack, [ground, ground], 3.0));

                // Melting at the snow's lower edge, trickling down to where the river rises.
                var source = RiverSource(warming);
                steps.Add(new DropStep(DropLeg.Snowmelt, AlongGround(landing, source, 10), 2.4));
                AddRiver(steps, source);
                break;
            }

            case DropRoute.Ground:
            {
                var cloud = new ScenePoint(0.5, CloudBase + 500);
                AddUpAndAcross(steps, surface, cloud);
                var land = new ScenePoint(0.5, Ground(0.5));
                steps.Add(new DropStep(DropLeg.Rain, [cloud, new ScenePoint(0.5, CloudBase), land], 1.6));
                var table = new ScenePoint(0.5, WaterTable(0.5) - 60);
                steps.Add(new DropStep(DropLeg.Infiltration, [land, new ScenePoint(0.502, WaterCycleModel.Lerp(land.Z, table.Z, 0.5)), table], 3.0));
                steps.Add(new DropStep(DropLeg.Groundwater, AlongWaterTable(0.5, 60, 20), 6.0));
                var seeped = new ScenePoint(Coast - 0.03, Ground(Coast - 0.03) + 20);
                steps.Add(new DropStep(DropLeg.Ocean, [seeped, new ScenePoint(0.2, -500), sea], 2.4));
                break;
            }

            case DropRoute.Tree:
            {
                var treeX = 0.465;
                var cloud = new ScenePoint(treeX, CloudBase + 500);
                AddUpAndAcross(steps, surface, cloud);
                var land = new ScenePoint(treeX + 0.012, Ground(treeX + 0.012));
                steps.Add(new DropStep(DropLeg.Rain, [cloud, new ScenePoint(treeX + 0.01, CloudBase), land], 1.6));

                // Into the soil, up through the roots and trunk to a leaf.
                var root = new ScenePoint(treeX + 0.006, Ground(treeX) - 90);
                var crown = new ScenePoint(treeX, Ground(treeX) + TreeHeight(treeX) * 0.75);
                steps.Add(new DropStep(DropLeg.Infiltration, [land, root], 1.8));
                steps.Add(new DropStep(DropLeg.Uptake, [root, new ScenePoint(treeX, Ground(treeX) - 40), new ScenePoint(treeX, Ground(treeX)), crown], 2.6));

                // Out of the leaf as vapour, carried on to the mountain's cloud, rained onto the slope and down the river.
                var air = new ScenePoint(treeX + 0.03, CloudBase - 200);
                steps.Add(new DropStep(DropLeg.Transpiration, Rise(crown, air, 0.008), 2.6));
                var mountainCloud = new ScenePoint(0.62, CloudBase + 700);
                steps.Add(new DropStep(DropLeg.Transport, [air, new ScenePoint(0.55, CloudBase + 150), new ScenePoint(0.6, CloudBase + 450)], 3.0));
                steps.Add(new DropStep(DropLeg.Condensation, [new ScenePoint(0.6, CloudBase + 450), mountainCloud], 1.8));
                var slope = 0.62;
                steps.Add(new DropStep(DropLeg.Rain, [mountainCloud, new ScenePoint(slope, Ground(slope))], 1.4));
                AddRiver(steps, slope);
                break;
            }

            default:
            {
                var cloud = new ScenePoint(0.64, CloudBase + 900);
                AddUpAndAcross(steps, surface, cloud);
                var slope = 0.645;
                steps.Add(new DropStep(DropLeg.Rain, [cloud, new ScenePoint(slope, Ground(slope))], 1.6));
                AddRiver(steps, slope);
                break;
            }
        }

        return steps;
    }

    /// <summary>Evaporating from the sea, carried inland on the wind, condensing into the cloud at <paramref name="cloud"/>.</summary>
    private static void AddUpAndAcross(List<DropStep> steps, ScenePoint surface, ScenePoint cloud)
    {
        var up = new ScenePoint(0.19, CloudBase - 250);
        steps.Add(new DropStep(DropLeg.Evaporation, Rise(surface, up, 0.012), 3.2));
        // Up the slope when the cloud is over the mountain, never through it.
        var near = new ScenePoint(cloud.X - 0.05, Math.Max(CloudBase + 100, Ground(cloud.X - 0.05) + 350));
        steps.Add(new DropStep(DropLeg.Transport, [up, new ScenePoint(WaterCycleModel.Lerp(up.X, near.X, 0.5), CloudBase + 50), near],
            Math.Max(2.4, (near.X - up.X) * 9)));
        steps.Add(new DropStep(DropLeg.Condensation, [near, new ScenePoint(cloud.X - 0.02, WaterCycleModel.Lerp(near.Z, cloud.Z, 0.6)), cloud], 2.0));
    }

    /// <summary>Down the river from <paramref name="from"/> to the coast, and out into the sea.</summary>
    private static void AddRiver(List<DropStep> steps, double from)
    {
        steps.Add(new DropStep(DropLeg.River, AlongGround(from, Coast, 30), Math.Max(2.5, (from - Coast) * 14)));
        steps.Add(new DropStep(DropLeg.Ocean, [new ScenePoint(Coast, 0), new ScenePoint(0.24, -260), new ScenePoint(0.11, -520)], 2.6));
    }

    /// <summary>A wavering climb from <paramref name="from"/> to <paramref name="to"/>, as vapour drifts up.</summary>
    private static List<ScenePoint> Rise(ScenePoint from, ScenePoint to, double wobble)
    {
        var points = new List<ScenePoint>();
        for (var i = 0; i <= 16; i++)
        {
            var t = i / 16.0;
            points.Add(new ScenePoint(WaterCycleModel.Lerp(from.X, to.X, t) + (wobble * Math.Sin(t * Math.PI * 3) * Math.Sin(t * Math.PI)),
                WaterCycleModel.Lerp(from.Z, to.Z, t)));
        }

        return points;
    }

    /// <summary>How tall the trees of the plain are drawn, metres (far taller than real, to be seen).</summary>
    public static double TreeHeight(double x) => 260 + (60 * Math.Sin(x * 97));

    /// <summary>Where a drop is after <paramref name="seconds"/> of its step.</summary>
    public static ScenePoint Along(DropStep step, double seconds)
    {
        var path = step.Path;
        if (path.Count == 1 || step.Seconds <= 0) return path[^1];
        var t = Math.Clamp(seconds / step.Seconds, 0, 1);

        // Ease in and out, so the drop rests a moment at each change.
        t = t * t * (3 - (2 * t));

        // By length along the path (x stretched to roughly match metres on screen).
        var lengths = new double[path.Count];
        for (var i = 1; i < path.Count; i++)
        {
            lengths[i] = lengths[i - 1] + Distance(path[i - 1], path[i]);
        }

        var target = t * lengths[^1];
        if (lengths[^1] <= 0) return path[^1];
        for (var i = 1; i < path.Count; i++)
        {
            if (lengths[i] < target) continue;
            var f = (target - lengths[i - 1]) / Math.Max(1e-12, lengths[i] - lengths[i - 1]);
            return new ScenePoint(WaterCycleModel.Lerp(path[i - 1].X, path[i].X, f), WaterCycleModel.Lerp(path[i - 1].Z, path[i].Z, f));
        }

        return path[^1];
    }

    private static double Distance(ScenePoint a, ScenePoint b)
    {
        var dx = (b.X - a.X) * 12000;
        var dz = b.Z - a.Z;
        return Math.Sqrt((dx * dx) + (dz * dz));
    }

    #endregion

    #region monotone cubic interpolation

    /// <summary>Fritsch–Carlson slopes, so the curve through the points never overshoots them.</summary>
    private static double[] Slopes(double[] x, double[] y)
    {
        var n = x.Length;
        var delta = new double[n - 1];
        for (var i = 0; i < n - 1; i++) delta[i] = (y[i + 1] - y[i]) / (x[i + 1] - x[i]);
        var m = new double[n];
        m[0] = delta[0];
        m[n - 1] = delta[n - 2];
        for (var i = 1; i < n - 1; i++)
        {
            if (delta[i - 1] * delta[i] <= 0)
            {
                m[i] = 0;
                continue;
            }

            var w1 = (2 * (x[i + 1] - x[i])) + (x[i] - x[i - 1]);
            var w2 = (x[i + 1] - x[i]) + (2 * (x[i] - x[i - 1]));
            m[i] = (w1 + w2) / ((w1 / delta[i - 1]) + (w2 / delta[i]));
        }

        return m;
    }

    private static double Hermite(double[] x, double[] y, double[] m, double at)
    {
        var i = Array.BinarySearch(x, at);
        if (i >= 0) return y[i];
        i = Math.Clamp(~i - 1, 0, x.Length - 2);
        var h = x[i + 1] - x[i];
        var t = (at - x[i]) / h;
        var t2 = t * t;
        var t3 = t2 * t;
        return (((2 * t3) - (3 * t2) + 1) * y[i]) + ((t3 - (2 * t2) + t) * h * m[i]) + (((-2 * t3) + (3 * t2)) * y[i + 1]) + ((t3 - t2) * h * m[i + 1]);
    }

    #endregion
}
