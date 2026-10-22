namespace Lemon.Explore.Book.Chapters.WaterCycle;

/// <summary>Where the lifted parcel is: <see cref="X"/> km across the cloud view, <see cref="Height"/> metres up, and whether it is on its way down.</summary>
public readonly record struct ParcelPlace(double X, double Height, bool Descending);

/// <summary>
/// The cloud view: a slice of sky 20 km across and 8 km high (the heights are real, the width squeezed), and the way a
/// parcel of air is lifted through it: straight up as a warm bubble, up and over a mountain, or up a sloping front.
/// </summary>
public static class CloudScene
{
    /// <summary>Kilometres across the view.</summary>
    public const double Width = 20;

    /// <summary>The top of the view, metres.</summary>
    public const double SkyTop = 8000;

    /// <summary>Seconds of playback for the parcel's whole way, then for the rain after it.</summary>
    public const double MoveSeconds = 14;
    public const double HoldSeconds = 5;

    public static double Duration => MoveSeconds + HoldSeconds;

    /// <summary>Where the cold air's wedge begins along the ground, km, and how steeply its top rises, metres per km.</summary>
    public const double FrontStart = 6;
    public const double FrontSlope = 7000 / 12.0;

    /// <summary>Where the thermal rises from, km.</summary>
    public const double ThermalX = 9.4;

    /// <summary>The mountain's ground, metres: a ridge in the middle of the view.</summary>
    public static double MountainGround(double x) => WaterCycleModel.MountainHeight * Math.Exp(-Math.Pow((x - (Width / 2)) / 3.2, 2));

    /// <summary>The top of the wedge of cold air, metres (0 ahead of it).</summary>
    public static double FrontSurface(double x) => Math.Max(0, (x - FrontStart) * FrontSlope);

    /// <summary>The ground under a point of the view for a way of lifting, metres.</summary>
    public static double Ground(CloudLift lift, double x) => lift == CloudLift.Mountain ? MountainGround(x) : 0;

    /// <summary>The parcel's way, a point every so often: its height is what it has been lifted to.</summary>
    public static IReadOnlyList<ScenePoint> Path(CloudLift lift)
    {
        var points = new List<ScenePoint>();
        switch (lift)
        {
            case CloudLift.Mountain:
                for (var i = 0; i <= 120; i++)
                {
                    var x = 0.6 + ((Width - 1.2) * i / 120);
                    points.Add(new ScenePoint(x, MountainGround(x)));
                }

                break;

            case CloudLift.Front:
                for (var i = 0; i <= 20; i++) points.Add(new ScenePoint(0.6 + ((FrontStart - 0.6) * i / 20), 0));
                var end = FrontStart + (7000 / FrontSlope);
                for (var i = 1; i <= 80; i++)
                {
                    var x = FrontStart + ((end - FrontStart) * i / 80);
                    points.Add(new ScenePoint(x, FrontSurface(x)));
                }

                break;

            default:
                var top = WaterCycleModel.LiftTop(CloudLift.Thermal);
                for (var i = 0; i <= 80; i++)
                {
                    var t = i / 80.0;
                    points.Add(new ScenePoint(ThermalX + (1.2 * t) + (0.25 * Math.Sin(t * Math.PI * 3)), top * t));
                }

                break;
        }

        return points;
    }

    /// <summary>Where the parcel is after a share (0–1) of its way, by distance along it.</summary>
    public static ParcelPlace At(CloudLift lift, double progress)
    {
        var path = Path(lift);
        var lengths = new double[path.Count];
        for (var i = 1; i < path.Count; i++)
        {
            var dx = (path[i].X - path[i - 1].X) * 350;
            var dz = path[i].Z - path[i - 1].Z;
            lengths[i] = lengths[i - 1] + Math.Sqrt((dx * dx) + (dz * dz));
        }

        var target = Math.Clamp(progress, 0, 1) * lengths[^1];
        var point = path[^1];
        for (var i = 1; i < path.Count; i++)
        {
            if (lengths[i] < target) continue;
            var f = (target - lengths[i - 1]) / Math.Max(1e-12, lengths[i] - lengths[i - 1]);
            point = new ScenePoint(WaterCycleModel.Lerp(path[i - 1].X, path[i].X, f), WaterCycleModel.Lerp(path[i - 1].Z, path[i].Z, f));
            break;
        }

        return new ParcelPlace(point.X, point.Z, lift == CloudLift.Mountain && point.X > Width / 2);
    }

    /// <summary>Where the parcel is <paramref name="seconds"/> into the playback (it waits at the end while it rains).</summary>
    public static ParcelPlace AtTime(CloudLift lift, double seconds) => At(lift, seconds / MoveSeconds);

    /// <summary>The parcel's state at a place: up its ascent, or down the far side of the mountain.</summary>
    public static ParcelPoint State(ParcelAscent ascent, IReadOnlyList<ParcelPoint> descent, ParcelPlace place) =>
        place.Descending && descent.Count > 0 ? WaterCycleModel.Interpolate(descent, place.Height) : ascent.At(place.Height);
}
