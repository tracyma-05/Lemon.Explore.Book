namespace Lemon.Explore.Book.Chapters.Projectile;

/// <summary>Which view the projectile page shows.</summary>
public enum ProjectileMode
{
    /// <summary>One shot from a cannon: aim it, watch the ball fly, its speed split into across and up.</summary>
    Launch,

    /// <summary>A fan of shots at the same speed, from 15° to 75°: which goes the farthest.</summary>
    BestAngle,

    /// <summary>Falling does not care about moving sideways: a dropped and a thrown ball, the monkey and the hunter.</summary>
    Together,
}

/// <summary>Where the ball is thrown, which sets the pull of gravity and how thick the air is.</summary>
public enum ProjectilePlace
{
    Earth,
    Moon,
    Mars,
}

/// <summary>What is thrown: from a heavy shot that hardly notices the air to a shuttlecock that is all drag.</summary>
public enum ProjectileBall
{
    ShotPut,
    Soccer,
    Baseball,
    PingPong,
    Shuttlecock,
}

/// <summary>Where in its flight the ball is, for the "what is happening" card.</summary>
public enum FlightStage
{
    /// <summary>Going up and slowing down.</summary>
    Rising,

    /// <summary>At the top: not going up any more, still moving across.</summary>
    Top,

    /// <summary>Coming down and speeding up.</summary>
    Falling,

    /// <summary>On the ground.</summary>
    Landed,
}

/// <summary>The scene of the "fall together" view.</summary>
public enum TogetherScene
{
    /// <summary>One ball dropped, one thrown sideways from the same height at the same moment.</summary>
    DropAndThrow,

    /// <summary>A dart aimed straight at a monkey, who lets go of the branch the moment it is fired.</summary>
    MonkeyHunter,
}

/// <summary>How the monkey and the hunter's dart end up.</summary>
public enum MonkeyOutcome
{
    /// <summary>The dart is still on its way.</summary>
    Flying,

    /// <summary>The dart reaches the monkey: both fell the same distance.</summary>
    Hit,

    /// <summary>Too slow: the dart lands before it gets there.</summary>
    Short,

    /// <summary>The monkey held on: the dart passes below by the distance it fell.</summary>
    Below,
}

/// <summary>A ball: mass in kilograms, diameter in metres and its drag coefficient.</summary>
public readonly record struct BallSpec(double Mass, double Diameter, double DragCoefficient)
{
    /// <summary>Cross-section facing the air, square metres.</summary>
    public double Area => Math.PI * Diameter * Diameter / 4;
}

/// <summary>A moment of a flight: time, position (y up, from the ground) and velocity.</summary>
public readonly record struct FlightPoint(double Time, double X, double Y, double Vx, double Vy)
{
    public double Speed => Math.Sqrt((Vx * Vx) + (Vy * Vy));
}

/// <summary>
/// A whole flight from launch to landing, integrated in a few hundred Runge–Kutta steps, with where it peaks and lands.
/// </summary>
public sealed class Flight
{
    internal Flight(IReadOnlyList<FlightPoint> points, FlightPoint peak, double gravity, double drag)
    {
        Points = points;
        PeakPoint = peak;
        Gravity = gravity;
        Drag = drag;
    }

    /// <summary>The states from launch to landing (the last one exactly on the ground).</summary>
    public IReadOnlyList<FlightPoint> Points { get; }

    /// <summary>The highest point (the launch point when it is thrown level or downwards).</summary>
    public FlightPoint PeakPoint { get; }

    public double Gravity { get; }

    /// <summary>The drag factor k: the air slows it by k·v² (per metre).</summary>
    public double Drag { get; }

    public FlightPoint Launch => Points[0];

    public FlightPoint Landing => Points[^1];

    public double Duration => Landing.Time;

    public double Range => Landing.X;

    public double Peak => PeakPoint.Y;

    /// <summary>Degrees below level at which it lands.</summary>
    public double LandingAngle => Math.Atan2(-Landing.Vy, Math.Max(1e-9, Landing.Vx)) * 180 / Math.PI;

    /// <summary>The state at <paramref name="time"/> (clamped to the flight), between steps by cubic Hermite.</summary>
    public FlightPoint At(double time)
    {
        if (Points.Count == 1 || time <= 0) return Points[0];
        if (time >= Duration) return Landing;

        var low = 0;
        var high = Points.Count - 1;
        while (high - low > 1)
        {
            var mid = (low + high) / 2;
            if (Points[mid].Time <= time) low = mid;
            else high = mid;
        }

        var a = Points[low];
        var b = Points[high];
        var h = b.Time - a.Time;
        if (h <= 0) return a;
        var s = (time - a.Time) / h;
        return new FlightPoint(time,
            Hermite(a.X, a.Vx * h, b.X, b.Vx * h, s),
            Hermite(a.Y, a.Vy * h, b.Y, b.Vy * h, s),
            a.Vx + ((b.Vx - a.Vx) * s),
            a.Vy + ((b.Vy - a.Vy) * s));
    }

    /// <summary>The drag on it at a moment, as a share of its weight (k·v²/g).</summary>
    public double DragShare(FlightPoint point) => Drag * ((point.Vx * point.Vx) + (point.Vy * point.Vy)) / Gravity;

    private static double Hermite(double p0, double m0, double p1, double m1, double s)
    {
        var s2 = s * s;
        var s3 = s2 * s;
        return (((2 * s3) - (3 * s2) + 1) * p0) + ((s3 - (2 * s2) + s) * m0) + ((-2 * s3 + (3 * s2)) * p1) + ((s3 - s2) * m1);
    }
}

/// <summary>
/// The physics of the projectile page: flights with and without air (drag growing as the square of the speed), the angle
/// that throws the farthest, and the falls behind the "fall together" scenes.
/// </summary>
/// <remarks>Angles are in degrees, lengths in metres, speeds in metres per second and times in seconds.</remarks>
public static class ProjectilePhysics
{
    /// <summary>Steps a flight is cut into, roughly (fewer when the air cuts it short).</summary>
    private const int FlightSteps = 800;

    private const double Deg = Math.PI / 180;

    /// <summary>The angles of the best-angle fan.</summary>
    public static IReadOnlyList<double> FanAngles { get; } = [15, 25, 35, 45, 55, 65, 75];

    /// <summary>The monkey's branch: how far from the hunter and how high, metres.</summary>
    public const double MonkeyDistance = 30;

    public const double MonkeyHeight = 15;

    /// <summary>The usual round value of Earth's gravity, m/s².</summary>
    public const double EarthGravity = 9.81;

    /// <summary>Pull of gravity at the surface, m/s²; on Earth <paramref name="earth"/> (the page passes the local value).</summary>
    public static double Gravity(ProjectilePlace place, double earth = EarthGravity) => place switch
    {
        ProjectilePlace.Moon => 1.62,
        ProjectilePlace.Mars => 3.71,
        _ => earth,
    };

    /// <summary>Density of the air at the ground, kg/m³: none on the Moon, a sixtieth of Earth's on Mars.</summary>
    public static double AirDensity(ProjectilePlace place) => place switch
    {
        ProjectilePlace.Moon => 0,
        ProjectilePlace.Mars => 0.020,
        _ => 1.225,
    };

    public static BallSpec Spec(ProjectileBall ball) => ball switch
    {
        ProjectileBall.ShotPut => new BallSpec(7.26, 0.12, 0.47),
        ProjectileBall.Soccer => new BallSpec(0.43, 0.22, 0.25),
        ProjectileBall.PingPong => new BallSpec(0.0027, 0.040, 0.50),
        ProjectileBall.Shuttlecock => new BallSpec(0.0051, 0.068, 0.60),
        _ => new BallSpec(0.145, 0.073, 0.35),
    };

    /// <summary>
    /// The drag factor k = ρ·Cd·A / 2m: the air slows the ball by k·v² (m/s² for v in m/s). Zero without air.
    /// </summary>
    public static double DragFactor(ProjectileBall ball, ProjectilePlace place, bool airDrag)
    {
        if (!airDrag) return 0;
        var spec = Spec(ball);
        return AirDensity(place) * spec.DragCoefficient * spec.Area / (2 * spec.Mass);
    }

    /// <summary>The speed at which the drag equals the weight: falling, it never goes faster. Infinite without air.</summary>
    public static double TerminalSpeed(double gravity, double drag) => drag > 0 ? Math.Sqrt(gravity / drag) : double.PositiveInfinity;

    #region without air

    public static double IdealTime(double speed, double angle, double height, double gravity)
    {
        var up = speed * Math.Sin(angle * Deg);
        return (up + Math.Sqrt(Math.Max(0, (up * up) + (2 * gravity * height)))) / gravity;
    }

    public static double IdealRange(double speed, double angle, double height, double gravity) =>
        speed * Math.Cos(angle * Deg) * IdealTime(speed, angle, height, gravity);

    public static double IdealPeak(double speed, double angle, double height, double gravity)
    {
        var up = Math.Max(0, speed * Math.Sin(angle * Deg));
        return height + (up * up / (2 * gravity));
    }

    /// <summary>The angle that throws the farthest without air: 45° from the ground, less from up high.</summary>
    public static double IdealBestAngle(double speed, double height, double gravity) =>
        Math.Atan(speed / Math.Sqrt((speed * speed) + (2 * gravity * Math.Max(0, height)))) / Deg;

    public static double IdealMaxRange(double speed, double height, double gravity) =>
        speed / gravity * Math.Sqrt((speed * speed) + (2 * gravity * Math.Max(0, height)));

    /// <summary>
    /// The highest point any throw at this speed can reach above <paramref name="x"/> (the "safety parabola"): every
    /// path touches it once, nothing gets past it.
    /// </summary>
    public static double Envelope(double x, double speed, double height, double gravity) =>
        height + (speed * speed / (2 * gravity)) - (gravity * x * x / (2 * speed * speed));

    #endregion

    #region flights

    /// <summary>
    /// Flies a ball launched at <paramref name="speed"/> and <paramref name="angle"/> from <paramref name="height"/>
    /// metres up, slowed by k·v² against its motion, until it lands on the ground.
    /// </summary>
    public static Flight Fly(double speed, double angle, double height, double gravity, double drag)
    {
        speed = Math.Max(0, speed);
        height = Math.Max(0, height);
        var start = new FlightPoint(0, 0, height, speed * Math.Cos(angle * Deg), speed * Math.Sin(angle * Deg));
        var points = new List<FlightPoint>(FlightSteps + 8) { start };

        // Thrown level or downwards from the ground: it is down already.
        var ideal = IdealTime(speed, angle, height, gravity);
        if (ideal <= 1e-9) return new Flight(points, start, gravity, drag);

        var dt = Math.Clamp(ideal / FlightSteps, 1e-4, 0.25);
        var peak = start.Vy <= 0 ? start : (FlightPoint?)null;
        var state = start;
        for (var i = 0; i < 200_000; i++)
        {
            var next = Step(state, dt, gravity, drag);

            // Passing the top: home in on where the upward speed is zero.
            if (peak is null && next.Vy <= 0)
            {
                var top = state;
                for (var k = 0; k < 3; k++)
                {
                    var ay = Acceleration(top, gravity, drag).Ay;
                    top = Step(top, Math.Clamp(-top.Vy / ay, 0, dt), gravity, drag);
                }

                peak = top;
            }

            if (next.Y <= 0)
            {
                // Landing: home in on the ground from the last state above it.
                var land = state;
                for (var k = 0; k < 4 && land.Vy < 0; k++)
                {
                    land = Step(land, Math.Clamp(-land.Y / land.Vy, 0, dt), gravity, drag);
                }

                points.Add(land with { Y = 0 });
                break;
            }

            points.Add(next);
            state = next;
        }

        return new Flight(points, peak ?? start, gravity, drag);
    }

    /// <summary>How far the ball goes, metres.</summary>
    public static double Range(double speed, double angle, double height, double gravity, double drag) =>
        drag <= 0 ? IdealRange(speed, angle, height, gravity) : Fly(speed, angle, height, gravity, drag).Range;

    /// <summary>The angle that throws the farthest, by golden-section search (the range has a single maximum).</summary>
    public static double BestAngle(double speed, double height, double gravity, double drag)
    {
        if (drag <= 0) return IdealBestAngle(speed, height, gravity);

        var ratio = (Math.Sqrt(5) - 1) / 2;
        double a = 0;
        double b = 90;
        var c = b - (ratio * (b - a));
        var d = a + (ratio * (b - a));
        var fc = Range(speed, c, height, gravity, drag);
        var fd = Range(speed, d, height, gravity, drag);
        while (b - a > 0.01)
        {
            if (fc > fd)
            {
                b = d;
                d = c;
                fd = fc;
                c = b - (ratio * (b - a));
                fc = Range(speed, c, height, gravity, drag);
            }
            else
            {
                a = c;
                c = d;
                fc = fd;
                d = a + (ratio * (b - a));
                fd = Range(speed, d, height, gravity, drag);
            }
        }

        return (a + b) / 2;
    }

    /// <summary>Where in its flight the ball is at <paramref name="time"/>.</summary>
    public static FlightStage Stage(Flight flight, double time)
    {
        if (time >= flight.Duration - 1e-9) return FlightStage.Landed;
        var near = Math.Max(0.04, flight.Duration * 0.05);
        if (flight.PeakPoint.Time > 0 && Math.Abs(time - flight.PeakPoint.Time) <= near) return FlightStage.Top;
        return time < flight.PeakPoint.Time ? FlightStage.Rising : FlightStage.Falling;
    }

    /// <summary>A strobe interval that puts some 12–24 images on a flight of <paramref name="duration"/> seconds.</summary>
    public static double StrobeInterval(double duration)
    {
        foreach (var step in new[] { 0.02, 0.05, 0.1, 0.2, 0.25, 0.5, 1, 2, 2.5, 5, 10 })
        {
            if (duration / step <= 24) return step;
        }

        return 20;
    }

    /// <summary>How much faster than real time a flight is played, so a long one takes some six seconds.</summary>
    public static int PlaybackBoost(double duration) => duration <= 7 ? 1 : (int)Math.Ceiling(duration / 6);

    /// <summary>One Runge–Kutta step: gravity down, drag k·v² against the motion.</summary>
    internal static FlightPoint Step(FlightPoint s, double dt, double gravity, double drag)
    {
        if (dt <= 0) return s;
        var (ax1, ay1) = Acceleration(s, gravity, drag);
        var s2 = new FlightPoint(0, 0, 0, s.Vx + (ax1 * dt / 2), s.Vy + (ay1 * dt / 2));
        var (ax2, ay2) = Acceleration(s2, gravity, drag);
        var s3 = new FlightPoint(0, 0, 0, s.Vx + (ax2 * dt / 2), s.Vy + (ay2 * dt / 2));
        var (ax3, ay3) = Acceleration(s3, gravity, drag);
        var s4 = new FlightPoint(0, 0, 0, s.Vx + (ax3 * dt), s.Vy + (ay3 * dt));
        var (ax4, ay4) = Acceleration(s4, gravity, drag);

        return new FlightPoint(
            s.Time + dt,
            s.X + (dt / 6 * (s.Vx + (2 * s2.Vx) + (2 * s3.Vx) + s4.Vx)),
            s.Y + (dt / 6 * (s.Vy + (2 * s2.Vy) + (2 * s3.Vy) + s4.Vy)),
            s.Vx + (dt / 6 * (ax1 + (2 * ax2) + (2 * ax3) + ax4)),
            s.Vy + (dt / 6 * (ay1 + (2 * ay2) + (2 * ay3) + ay4)));
    }

    private static (double Ax, double Ay) Acceleration(FlightPoint s, double gravity, double drag)
    {
        var speed = Math.Sqrt((s.Vx * s.Vx) + (s.Vy * s.Vy));
        return (-drag * speed * s.Vx, -gravity - (drag * speed * s.Vy));
    }

    #endregion

    #region falling together

    /// <summary>How far anything falls in <paramref name="time"/> seconds from rest (or below where it was heading).</summary>
    public static double FallDistance(double time, double gravity) => gravity * time * time / 2;

    /// <summary>Seconds to fall <paramref name="height"/> metres from rest.</summary>
    public static double FallTime(double height, double gravity) => Math.Sqrt(2 * Math.Max(0, height) / gravity);

    /// <summary>The hunter's aim, straight at the monkey, degrees.</summary>
    public static double MonkeyAim => Math.Atan2(MonkeyHeight, MonkeyDistance) / Deg;

    /// <summary>The slowest dart that reaches the monkey before it lands: √(g·(D² + H²) / 2H).</summary>
    public static double MonkeyMinSpeed(double gravity) =>
        Math.Sqrt(gravity * ((MonkeyDistance * MonkeyDistance) + (MonkeyHeight * MonkeyHeight)) / (2 * MonkeyHeight));

    /// <summary>
    /// When the monkey scene ends and how: the dart reaches the monkey's line (hit, or below it when the monkey holds
    /// on) or lands first. Without air the dart covers the aim line at its own speed, so it gets there at √(D²+H²)/v.
    /// </summary>
    public static (double Time, MonkeyOutcome Outcome) MonkeyEnd(double speed, double gravity, bool letsGo)
    {
        var aim = MonkeyAim * Deg;
        var reach = Math.Sqrt((MonkeyDistance * MonkeyDistance) + (MonkeyHeight * MonkeyHeight)) / Math.Max(0.1, speed);
        var landing = 2 * speed * Math.Sin(aim) / gravity;
        if (landing < reach) return (landing, MonkeyOutcome.Short);
        return (reach, letsGo ? MonkeyOutcome.Hit : MonkeyOutcome.Below);
    }

    /// <summary>The dart at <paramref name="time"/>: position and velocity, launched from the ground at the monkey.</summary>
    public static FlightPoint Dart(double speed, double gravity, double time)
    {
        var aim = MonkeyAim * Deg;
        var vx = speed * Math.Cos(aim);
        var vy = speed * Math.Sin(aim);
        return new FlightPoint(time, vx * time, (vy * time) - FallDistance(time, gravity), vx, vy - (gravity * time));
    }

    /// <summary>How high the monkey is at <paramref name="time"/> (it stops on the ground).</summary>
    public static double MonkeyY(double gravity, double time, bool letsGo) =>
        letsGo ? Math.Max(0, MonkeyHeight - FallDistance(time, gravity)) : MonkeyHeight;

    #endregion
}
