namespace Lemon.Explore.Book.Chapters.Oscillations;

/// <summary>Which view the oscillations page shows.</summary>
public enum OscillationMode
{
    /// <summary>A pendulum on a light rod, with a pen tracing its swing on moving paper.</summary>
    Pendulum,

    /// <summary>A mass bouncing on a spring, beside the circle whose shadow moves the same way.</summary>
    Spring,

    /// <summary>Fifteen pendulums of slightly different lengths falling in and out of step.</summary>
    Wave,
}

/// <summary>Where the experiment is done, which sets the pull of gravity.</summary>
public enum OscillatorPlace
{
    Earth,
    Moon,
    Mars,
    Jupiter,
}

/// <summary>Where in its swing (or bounce) the oscillator is, for the "what is happening" card.</summary>
public enum SwingStage
{
    /// <summary>At an end of the swing: still for a moment, all its energy stored.</summary>
    Turning,

    /// <summary>Heading for the middle and speeding up.</summary>
    Falling,

    /// <summary>Passing the middle at its fastest, nothing pulling it either way.</summary>
    Middle,

    /// <summary>Moving away from the middle and slowing down.</summary>
    Rising,

    /// <summary>Hardly moving any more: the air has taken its energy.</summary>
    Resting,

    /// <summary>Being held and pulled aside with the mouse.</summary>
    Held,
}

/// <summary>A pattern the pendulum wave makes, by how far through its cycle it is.</summary>
public enum WavePattern
{
    /// <summary>All the pendulums together, as when they were let go.</summary>
    InLine,

    /// <summary>Each a little behind the next: a snake winding along the row.</summary>
    Snake,

    /// <summary>Sorted into a few rows (see <see cref="OscillatorPhysics.WaveRows"/>).</summary>
    Rows,

    /// <summary>No pattern to see: it looks random, but is not.</summary>
    Mixed,
}

/// <summary>How the energy of a swing is shared out, each as a share of the energy it was let go with.</summary>
public readonly record struct EnergySplit(double Kinetic, double Stored, double Lost)
{
    public static EnergySplit None => new(0, 0, 0);
}

/// <summary>
/// The physics of the oscillations page: the pendulum (its exact period for any swing, from the arithmetic–geometric mean,
/// and a Runge–Kutta step of its motion), the mass on a spring (period, stretch, a step with damping and a shaken top, and
/// the steady swing when shaken) and the pendulum wave (lengths for a whole number of swings in one cycle).
/// </summary>
/// <remarks>Angles are in radians, lengths in metres, masses in kilograms and times in seconds.</remarks>
public static class OscillatorPhysics
{
    /// <summary>Standard gravity on Earth.</summary>
    public const double EarthGravity = 9.81;

    /// <summary>How fast the air slows a swing, per second (its amplitude halves in about 23 s).</summary>
    public const double PendulumAirDamping = 0.06;

    /// <summary>How fast the air and the spring's own losses slow a bounce, per second.</summary>
    public const double SpringDamping = 0.22;

    /// <summary>
    /// Quality factor of the spring while it is shaken: its damping is at least ω₀ / Q, so a swing at resonance settles at
    /// Q times the shake (about 19 cm) instead of growing without end.
    /// </summary>
    public const double ShakenQuality = 16;

    /// <summary>How far the top of the spring is shaken up and down, in metres.</summary>
    public const double DriveAmplitude = 0.012;

    /// <summary>The length of the spring with nothing on it, as drawn, in metres.</summary>
    public const double SpringNaturalLength = 0.3;

    /// <summary>Pendulums in the pendulum wave.</summary>
    public const int WaveCount = 15;

    /// <summary>Seconds in which, on Earth, the pendulum wave comes back into line.</summary>
    public const double WaveCycleOnEarth = 60;

    /// <summary>Whole swings the longest pendulum of the wave makes in one cycle; each next one makes one more.</summary>
    public const int WaveFirstSwings = 51;

    /// <summary>How far each pendulum of the wave is let go, in radians.</summary>
    public const double WaveAmplitude = 18 * Math.PI / 180;

    /// <summary>
    /// Pull of gravity at the surface (for Jupiter: at the cloud tops), m/s². On Earth it is <paramref name="earth"/>:
    /// the page passes the local value at the shared observer location.
    /// </summary>
    public static double Gravity(OscillatorPlace place, double earth = EarthGravity) => place switch
    {
        OscillatorPlace.Moon => 1.62,
        OscillatorPlace.Mars => 3.71,
        OscillatorPlace.Jupiter => 24.79,
        _ => earth,
    };

    #region pendulum

    /// <summary>The textbook period 2π√(L/g), right only for small swings.</summary>
    public static double SmallAnglePeriod(double length, double gravity) => 2 * Math.PI * Math.Sqrt(length / gravity);

    /// <summary>
    /// The exact period of a pendulum let go at <paramref name="amplitude"/> radians: the textbook period divided by the
    /// arithmetic–geometric mean of 1 and cos(amplitude / 2). Grows without end as the amplitude nears 180°.
    /// </summary>
    public static double PendulumPeriod(double length, double gravity, double amplitude) =>
        SmallAnglePeriod(length, gravity) / ArithmeticGeometricMean(1, Math.Cos(Math.Abs(amplitude) / 2));

    /// <summary>How many times longer the real period is than the textbook one, for a swing of this amplitude.</summary>
    public static double PeriodStretch(double amplitude) => 1 / ArithmeticGeometricMean(1, Math.Cos(Math.Abs(amplitude) / 2));

    /// <summary>The arithmetic–geometric mean of two positive numbers.</summary>
    public static double ArithmeticGeometricMean(double a, double b)
    {
        if (a <= 0 || b <= 0) return 0;
        for (var i = 0; i < 40 && Math.Abs(a - b) > 1e-15 * a; i++)
        {
            (a, b) = ((a + b) / 2, Math.Sqrt(a * b));
        }

        return a;
    }

    /// <summary>Angular acceleration of the pendulum: gravity pulling it back (as sin θ) and the air slowing it.</summary>
    public static double PendulumAcceleration(double angle, double angularVelocity, double length, double gravity, double damping) =>
        (-gravity / length * Math.Sin(angle)) - (damping * angularVelocity);

    /// <summary>Moves the pendulum on by <paramref name="dt"/> seconds (one fourth-order Runge–Kutta step).</summary>
    public static (double Angle, double AngularVelocity) StepPendulum(double angle, double angularVelocity, double length,
        double gravity, double damping, double dt)
    {
        double A(double th, double om) => PendulumAcceleration(th, om, length, gravity, damping);

        var k1x = angularVelocity;
        var k1v = A(angle, angularVelocity);
        var k2x = angularVelocity + (k1v * dt / 2);
        var k2v = A(angle + (k1x * dt / 2), k2x);
        var k3x = angularVelocity + (k2v * dt / 2);
        var k3v = A(angle + (k2x * dt / 2), k3x);
        var k4x = angularVelocity + (k3v * dt);
        var k4v = A(angle + (k3x * dt), k4x);
        return (angle + ((k1x + (2 * k2x) + (2 * k3x) + k4x) * dt / 6),
            angularVelocity + ((k1v + (2 * k2v) + (2 * k3v) + k4v) * dt / 6));
    }

    /// <summary>How far the pendulum would swing out from where it is now, going as fast as it is (no air).</summary>
    public static double PendulumAmplitude(double angle, double angularVelocity, double length, double gravity)
    {
        var cosine = Math.Cos(angle) - (length * angularVelocity * angularVelocity / (2 * gravity));
        return Math.Acos(Math.Clamp(cosine, -1, 1));
    }

    /// <summary>Speed of the bob through the bottom of a swing of this amplitude, in m/s.</summary>
    public static double PendulumTopSpeed(double length, double gravity, double amplitude) =>
        Math.Sqrt(2 * gravity * length * (1 - Math.Cos(amplitude)));

    /// <summary>The pendulum's energy now as shares of what it was let go with.</summary>
    public static EnergySplit PendulumEnergy(double angle, double angularVelocity, double length, double gravity, double release)
    {
        var total = gravity * length * (1 - Math.Cos(release));
        if (total <= 0) return EnergySplit.None;
        var kinetic = 0.5 * length * length * angularVelocity * angularVelocity / total;
        var stored = gravity * length * (1 - Math.Cos(angle)) / total;
        return new EnergySplit(kinetic, stored, Math.Max(0, 1 - kinetic - stored));
    }

    #endregion

    #region spring

    /// <summary>The period 2π√(m/k): the same however far it is pulled, and wherever it is (gravity does not come in).</summary>
    public static double SpringPeriod(double mass, double stiffness) => 2 * Math.PI * Math.Sqrt(mass / stiffness);

    /// <summary>Angular frequency √(k/m), radians per second.</summary>
    public static double SpringOmega(double mass, double stiffness) => Math.Sqrt(stiffness / mass);

    /// <summary>How far the mass stretches the spring at rest: mg/k, in metres.</summary>
    public static double SpringStretch(double mass, double stiffness, double gravity) => mass * gravity / stiffness;

    /// <summary>
    /// Moves the mass on by <paramref name="dt"/> seconds (fourth-order Runge–Kutta). <paramref name="height"/> is the
    /// mass's height above where it rests; <paramref name="support"/> gives the height of the top of the spring above its
    /// usual place at a time, for when it is shaken.
    /// </summary>
    public static (double Height, double Velocity) StepSpring(double height, double velocity, double omega, double damping,
        Func<double, double> support, double time, double dt)
    {
        double A(double y, double v, double t) => (-omega * omega * (y - support(t))) - (damping * v);

        var k1x = velocity;
        var k1v = A(height, velocity, time);
        var k2x = velocity + (k1v * dt / 2);
        var k2v = A(height + (k1x * dt / 2), k2x, time + (dt / 2));
        var k3x = velocity + (k2v * dt / 2);
        var k3v = A(height + (k2x * dt / 2), k3x, time + (dt / 2));
        var k4x = velocity + (k3v * dt);
        var k4v = A(height + (k3x * dt), k4x, time + dt);
        return (height + ((k1x + (2 * k2x) + (2 * k3x) + k4x) * dt / 6),
            velocity + ((k1v + (2 * k2v) + (2 * k3v) + k4v) * dt / 6));
    }

    /// <summary>How far the mass would bounce out from where it is now, going as fast as it is.</summary>
    public static double SpringAmplitude(double height, double velocity, double omega) =>
        Math.Sqrt((height * height) + (velocity * velocity / (omega * omega)));

    /// <summary>The bounce's energy now as shares of what it was let go with (stored counts spring and height together).</summary>
    public static EnergySplit SpringEnergy(double height, double velocity, double omega, double release)
    {
        if (release == 0) return EnergySplit.None;
        var total = omega * omega * release * release;
        var kinetic = velocity * velocity / total;
        var stored = omega * omega * height * height / total;
        return new EnergySplit(kinetic, stored, Math.Max(0, 1 - kinetic - stored));
    }

    /// <summary>Damping of the spring while shaken: what it has anyway, but at least ω₀ / <see cref="ShakenQuality"/>.</summary>
    public static double ShakenDamping(double omega, double damping) => Math.Max(damping, omega / ShakenQuality);

    /// <summary>
    /// How many times further the mass swings than the top is shaken, once it has settled, when shaken at
    /// <paramref name="driveOmega"/>: ω₀² / √((ω₀² − ω²)² + (cω)²). Largest near the spring's own frequency.
    /// </summary>
    public static double ShakenResponse(double omega, double damping, double driveOmega)
    {
        var w0 = omega * omega;
        var w = driveOmega * driveOmega;
        return w0 / Math.Sqrt(((w0 - w) * (w0 - w)) + (damping * damping * w));
    }

    #endregion

    #region swing stages

    /// <summary>
    /// Where in its swing an oscillator is, from how far it is out (<paramref name="offset"/>, signed), how fast it moves
    /// (<paramref name="velocity"/>, signed alike) and how far its swing goes (<paramref name="amplitude"/>).
    /// </summary>
    public static SwingStage Stage(double offset, double velocity, double amplitude, double restingAmplitude)
    {
        if (amplitude < restingAmplitude) return SwingStage.Resting;
        var share = Math.Abs(offset) / amplitude;
        if (share > 0.93) return SwingStage.Turning;
        if (share < 0.18) return SwingStage.Middle;
        return offset * velocity < 0 ? SwingStage.Falling : SwingStage.Rising;
    }

    #endregion

    #region pendulum wave

    /// <summary>Period of pendulum <paramref name="index"/> of the wave on Earth: one cycle over its number of swings.</summary>
    public static double WaveEarthPeriod(int index) => WaveCycleOnEarth / (WaveFirstSwings + index);

    /// <summary>Length of pendulum <paramref name="index"/> of the wave (0 the longest), made for Earth.</summary>
    public static double WaveLength(int index)
    {
        var period = WaveEarthPeriod(index);
        return EarthGravity * period * period / (4 * Math.PI * Math.PI);
    }

    /// <summary>Period of pendulum <paramref name="index"/> of the wave where gravity is <paramref name="gravity"/>.</summary>
    public static double WavePeriod(int index, double gravity) => SmallAnglePeriod(WaveLength(index), gravity);

    /// <summary>Seconds until the wave is back in line: 60 on Earth, longer where gravity is weaker.</summary>
    public static double WaveCycle(double gravity) => WaveCycleOnEarth * Math.Sqrt(EarthGravity / gravity);

    /// <summary>Angle of pendulum <paramref name="index"/> at <paramref name="time"/> after all were let go together.</summary>
    public static double WaveAngle(int index, double time, double gravity) =>
        WaveAmplitude * Math.Cos(2 * Math.PI * time / WavePeriod(index, gravity));

    /// <summary>
    /// The pattern of the wave a <paramref name="fraction"/> of the way through its cycle. Each pendulum makes one swing
    /// more than the one before it, so neighbours are 360° × fraction apart: at ½ they alternate in two rows, at ⅓ three…
    /// </summary>
    public static WavePattern Pattern(double fraction, out int rows)
    {
        fraction -= Math.Floor(fraction);
        rows = 1;
        var fromLine = Math.Min(fraction, 1 - fraction);
        if (fromLine < 0.015) return WavePattern.InLine;
        if (fromLine < 0.11) return WavePattern.Snake;

        rows = WaveRows(fraction);
        return rows > 1 ? WavePattern.Rows : WavePattern.Mixed;
    }

    /// <summary>
    /// How many rows the wave sorts into a <paramref name="fraction"/> of the way through its cycle: q when the fraction
    /// is close to p/q (q from 2 to 5), else 0.
    /// </summary>
    public static int WaveRows(double fraction)
    {
        fraction -= Math.Floor(fraction);
        for (var q = 2; q <= 5; q++)
        {
            var nearest = Math.Round(fraction * q) / q;
            if (nearest is <= 0 or >= 1) continue;

            // Neighbours drift off the rows by 360° × the miss; allow a drift of about 70° across the whole row.
            if (Math.Abs(fraction - nearest) * (WaveCount - 1) < 0.2) return q;
        }

        return 0;
    }

    #endregion
}
