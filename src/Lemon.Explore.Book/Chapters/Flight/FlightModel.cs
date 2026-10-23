using System.Numerics;
using Vector = Avalonia.Vector;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Flight;

/// <summary>The three views of the flight page.</summary>
public enum FlightMode
{
    /// <summary>A wing's cross-section in a wind tunnel: the air flowing round it, the pressure and the lift.</summary>
    Wing,

    /// <summary>A small plane flying: lift, weight, thrust and drag, take-off, climb, cruise, glide and stall.</summary>
    Forces,

    /// <summary>A plane in 3D: ailerons, elevator and rudder, and banking to turn.</summary>
    Turn,
}

/// <summary>The wing sections the wind tunnel can hold.</summary>
public enum WingShape
{
    /// <summary>Curved on top, flatter underneath, about 12 % thick: a light aircraft's wing.</summary>
    Cambered,

    /// <summary>The same top and bottom, 12 % thick: an aerobatic plane's or a tail's.</summary>
    Symmetric,

    /// <summary>A thin flat plate (a sheet of card).</summary>
    Flat,

    /// <summary>Thin and strongly curved, like a bird's wing or a sail.</summary>
    Bird,
}

/// <summary>What the turn view demonstrates: a banked turn, or one control at a time.</summary>
public enum TurnDemo
{
    Turn,
    Roll,
    Pitch,
    Yaw,
}

/// <summary>
/// A Joukowski wing section in an ideal (inviscid) stream, the classic exact solution: the flow round a circle, with just
/// enough circulation that the air leaves the sharp trailing edge smoothly (the Kutta condition), mapped onto the wing by
/// z = ζ + 1/ζ. Speeds are in units of the free stream.
/// </summary>
/// <remarks>
/// The display frame has the wind blowing along +x, y up, lengths in chords and the origin at the quarter-chord point, so
/// the wing is turned nose-up by the angle of attack. The model frame is the textbook one: the wing on the x axis
/// (trailing edge at z = 2) and the stream coming in at an angle.
/// </remarks>
public sealed class Airfoil
{
    /// <param name="thickness">The circle's offset along −x (about 0.77 × thickness / chord).</param>
    /// <param name="camber">The circle's offset along +y (about 2 × camber / chord).</param>
    /// <param name="alphaDegrees">The angle between the wind and the chord line (nose up positive).</param>
    public Airfoil(double thickness, double camber, double alphaDegrees)
    {
        Centre = new Complex(-thickness, camber);
        Radius = Complex.Abs(1 - Centre);
        Beta = Math.Atan2(camber, 1 + thickness);

        // The leading edge is the point of the section farthest from the trailing edge.
        var best = 0.0;
        var leading = Complex.Zero;
        for (var i = 0; i < 1440; i++)
        {
            var z = Map(Centre + (Radius * Complex.FromPolarCoordinates(1, i * Math.PI / 720)));
            var d = Complex.Abs(z - TrailingEdgeModel);
            if (d > best)
            {
                best = d;
                leading = z;
            }
        }

        LeadingEdgeModel = leading;
        ChordModel = best;
        var chordAngle = (TrailingEdgeModel - leading).Phase;
        AlphaDegrees = alphaDegrees;
        StreamAngle = (alphaDegrees * Math.PI / 180) + chordAngle;
        Pivot = leading + (0.25 * (TrailingEdgeModel - leading));
        Circulation = 4 * Math.PI * Radius * Math.Sin(StreamAngle + Beta);
        _toDisplay = Complex.FromPolarCoordinates(1 / ChordModel, -StreamAngle);
        _streamIn = Complex.FromPolarCoordinates(1, -StreamAngle);
        _streamOut = Complex.FromPolarCoordinates(1, StreamAngle);
    }

    private static readonly Complex TrailingEdgeModel = new(2, 0);

    private readonly Complex _toDisplay;
    private readonly Complex _streamIn;
    private readonly Complex _streamOut;

    public Complex Centre { get; }

    public double Radius { get; }

    /// <summary>The angle at the circle's centre from the x axis down to the trailing edge, rad.</summary>
    public double Beta { get; }

    public double AlphaDegrees { get; }

    /// <summary>The stream's angle to the model's x axis, rad.</summary>
    public double StreamAngle { get; }

    public Complex LeadingEdgeModel { get; }

    public double ChordModel { get; }

    /// <summary>The quarter-chord point, about which the wing is turned (and the lift acts).</summary>
    public Complex Pivot { get; }

    /// <summary>The circulation round the wing (clockwise positive), free-stream speed × model length.</summary>
    public double Circulation { get; }

    /// <summary>The ideal-flow lift coefficient (Kutta–Joukowski: L = ρ U Γ).</summary>
    public double LiftCoefficient => 2 * Circulation / ChordModel;

    /// <summary>The angle of attack at which this section gives no lift, degrees.</summary>
    public double ZeroLiftAlpha => (-Beta - (StreamAngle - (AlphaDegrees * Math.PI / 180))) * 180 / Math.PI;

    public static Complex Map(Complex zeta) => zeta + (1 / zeta);

    public Point ToDisplay(Complex model) => AsPoint((model - Pivot) * _toDisplay);

    public Complex ToModel(Point display) => Pivot + (new Complex(display.X, display.Y) / _toDisplay);

    public Point LeadingEdge => ToDisplay(LeadingEdgeModel);

    public Point TrailingEdge => ToDisplay(TrailingEdgeModel);

    /// <summary>The point of the circle plane that maps to <paramref name="model"/>, outside the circle (two do).</summary>
    public Complex Zeta(Complex model)
    {
        var root = Complex.Sqrt((model * model) - 4);
        var a = (model + root) / 2;
        var b = (model - root) / 2;
        return Complex.Abs(a - Centre) >= Complex.Abs(b - Centre) ? a : b;
    }

    /// <summary>Whether a display point is inside the wing.</summary>
    public bool Inside(Point display) => Complex.Abs(Zeta(ToModel(display)) - Centre) < Radius;

    /// <summary>The air's velocity at a display point (free stream = (1, 0)); zero inside the wing.</summary>
    public Vector Velocity(Point display)
    {
        var zeta = Zeta(ToModel(display));
        var offset = zeta - Centre;
        if (offset.Magnitude < Radius) return default;
        return VelocityAtZeta(zeta);
    }

    private Vector VelocityAtZeta(Complex zeta)
    {
        var dz = 1 - (1 / (zeta * zeta));
        if (dz.Magnitude < 1e-4)
        {
            // At the trailing edge both sides of the quotient vanish; step a hair outwards for the limit.
            zeta = Centre + ((zeta - Centre) * 1.0005);
            dz = 1 - (1 / (zeta * zeta));
        }

        var offset = zeta - Centre;
        var dw = _streamIn - (Radius * Radius * _streamOut / (offset * offset)) + (Complex.ImaginaryOne * Circulation / (2 * Math.PI * offset));

        // dw/dz is u − iv in the model frame; turn the velocity into the display frame.
        var velocity = Complex.Conjugate(dw / dz) * _streamIn;
        return new Vector(velocity.Real, velocity.Imaginary);
    }

    /// <summary>The pressure coefficient at a display point: 1 − (V/U)² (Bernoulli), 0 in the free stream.</summary>
    public double PressureCoefficient(Point display) => 1 - Velocity(display).SquaredLength;

    /// <summary>The wing's outline in display coordinates, going round from the trailing edge over the top.</summary>
    public IReadOnlyList<Point> Outline(int count = 240)
    {
        var points = new List<Point>(count + 1);
        for (var i = 0; i <= count; i++)
        {
            var theta = -Beta + (2 * Math.PI * i / count);
            points.Add(ToDisplay(Map(Centre + Complex.FromPolarCoordinates(Radius, theta))));
        }

        return points;
    }

    /// <summary>A point on the surface (angle round the circle, from the trailing edge over the top) and the air's speed there.</summary>
    public (Point Point, Vector Velocity) Surface(double angleFromTrailingEdge)
    {
        var zeta = Centre + Complex.FromPolarCoordinates(Radius, -Beta + angleFromTrailingEdge);
        return (ToDisplay(Map(zeta)), VelocityAtZeta(Centre + ((zeta - Centre) * 1.000001)));
    }

    /// <summary>The front stagnation point, where the air splits and stands still.</summary>
    public Point Stagnation => ToDisplay(Map(Centre + Complex.FromPolarCoordinates(Radius, Math.PI + (2 * StreamAngle) + Beta)));

    /// <summary>The camber line (half-way between top and bottom) in display coordinates, leading edge to trailing edge.</summary>
    public IReadOnlyList<Point> CamberLine(int count = 40)
    {
        var outline = Outline(720);
        var leadIndex = 0;
        var best = double.MaxValue;
        var le = LeadingEdge;
        for (var i = 0; i < outline.Count; i++)
        {
            var d = ((Vector)(outline[i] - le)).SquaredLength;
            if (d < best)
            {
                best = d;
                leadIndex = i;
            }
        }

        var top = outline.Take(leadIndex + 1).ToList();
        var bottom = outline.Skip(leadIndex).ToList();
        var te = TrailingEdge;
        Vector axis = te - le;
        var length = axis.Length;
        axis /= length;
        var line = new List<Point>(count + 1);
        for (var i = 0; i <= count; i++)
        {
            var s = length * (1 - Math.Cos(Math.PI * i / count)) / 2;
            line.Add(Midpoint(At(top, le, axis, s), At(bottom, le, axis, s)));
        }

        return line;
    }

    /// <summary>The point of a surface whose distance along the chord from the leading edge is <paramref name="s"/>.</summary>
    private static Point At(List<Point> surface, Point le, Vector axis, double s)
    {
        for (var i = 1; i < surface.Count; i++)
        {
            var a = (surface[i - 1] - le) * axis;
            var b = (surface[i] - le) * axis;
            if ((a - s) * (b - s) <= 0 && Math.Abs(b - a) > 1e-12)
            {
                var t = (s - a) / (b - a);
                return surface[i - 1] + ((surface[i] - surface[i - 1]) * t);
            }
        }

        return le + (axis * s);
    }

    private static Point Midpoint(Point a, Point b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    private static Point AsPoint(Complex c) => new(c.Real, c.Imaginary);
}

/// <summary>
/// A real wing section: the ideal-flow lift up to the stall, then the flow tearing off the top. Lift coefficients are
/// per unit span, at the chord's angle of attack in degrees.
/// </summary>
public static class WingSection
{
    /// <summary>The Joukowski offsets, the stall (degrees past the zero-lift angle) and the drag at no lift.</summary>
    public static (double Thickness, double Camber, double Stall, double Cd0) Parameters(WingShape shape, bool inverted)
    {
        var (thickness, camber, stall, cd0) = shape switch
        {
            WingShape.Symmetric => (0.092, 0.0, 15.0, 0.008),
            WingShape.Flat => (0.012, 0.0, 9.0, 0.02),
            WingShape.Bird => (0.022, 0.14, 17.0, 0.016),
            _ => (0.092, 0.08, 18.0, 0.009),
        };
        return (thickness, inverted ? -camber : camber, stall, cd0);
    }

    public static Airfoil Airfoil(WingShape shape, bool inverted, double alphaDegrees)
    {
        var (thickness, camber, _, _) = Parameters(shape, inverted);
        return new Airfoil(thickness, camber, alphaDegrees);
    }

    /// <summary>The ideal-flow lift coefficient (no stall).</summary>
    public static double IdealLift(WingShape shape, bool inverted, double alphaDegrees) => Airfoil(shape, inverted, alphaDegrees).LiftCoefficient;

    public static double ZeroLiftAlpha(WingShape shape, bool inverted) => Airfoil(shape, inverted, 0).ZeroLiftAlpha;

    /// <summary>How far the flow has torn off the top (0: attached, 1: fully stalled).</summary>
    public static double Separation(WingShape shape, bool inverted, double alphaDegrees)
    {
        var stall = Parameters(shape, inverted).Stall;
        var beyond = Math.Abs(alphaDegrees - ZeroLiftAlpha(shape, inverted)) - stall;
        return SmoothStep(-1, 3, beyond);
    }

    /// <summary>The angle of attack at which the wing stalls (nose up), degrees.</summary>
    public static double StallAlpha(WingShape shape, bool inverted) => ZeroLiftAlpha(shape, inverted) + Parameters(shape, inverted).Stall;

    /// <summary>The lift coefficient of the real section: ideal flow until the stall, a stalled plate's after it.</summary>
    public static double Lift(WingShape shape, bool inverted, double alphaDegrees)
    {
        var w = Separation(shape, inverted, alphaDegrees);
        var a = alphaDegrees * Math.PI / 180;
        return ((1 - w) * IdealLift(shape, inverted, alphaDegrees)) + (w * 1.05 * Math.Sin(2 * a));
    }

    public static double Drag(WingShape shape, bool inverted, double alphaDegrees)
    {
        var w = Separation(shape, inverted, alphaDegrees);
        var a = alphaDegrees * Math.PI / 180;
        var cl = IdealLift(shape, inverted, alphaDegrees);
        return Parameters(shape, inverted).Cd0 + (0.007 * cl * cl * (1 - w)) + (w * 1.9 * Math.Sin(a) * Math.Sin(a));
    }

    internal static double SmoothStep(double from, double to, double x)
    {
        var t = Math.Clamp((x - from) / (to - from), 0, 1);
        return t * t * (3 - (2 * t));
    }
}

/// <summary>Where the plane is and how it moves: horizontal distance and height (m), speed (m/s) and climb angle (rad).</summary>
public readonly record struct FlightState(double X, double Altitude, double Speed, double PathAngle, bool OnGround);

/// <summary>The four forces on the plane (N), the air's density and how the wing is doing.</summary>
public readonly record struct FlightForces(double Lift, double Drag, double Thrust, double Weight, double Density, double LiftCoefficient, double Separation, double Alpha);

/// <summary>
/// A small four-seat trainer (about a Cessna 172's size): its wing, propeller and engine, the standard atmosphere, and its
/// motion in the vertical plane as a point mass with the angle of attack and the throttle as the pilot's controls.
/// </summary>
public static class Trainer
{
    public const double G = 9.80665;

    public const double WingArea = 16;

    public const double Span = 11;

    public const double AspectRatio = Span * Span / WingArea;

    /// <summary>Oswald's efficiency: how close the wing comes to the ideal (elliptic) lift distribution.</summary>
    public const double Oswald = 0.8;

    /// <summary>Drag at no lift (the fuselage, wheels and skin friction).</summary>
    public const double Cd0 = 0.032;

    /// <summary>The wing's lift at no angle of attack (it is cambered and set at an angle on the fuselage).</summary>
    public const double Cl0 = 0.25;

    public const double StallDegrees = 15;

    /// <summary>The engine's power at sea level, W, and how much of it the propeller turns into thrust.</summary>
    public const double Power = 120_000;

    public const double PropEfficiency = 0.75;

    /// <summary>The propeller's thrust standing still, N.</summary>
    public const double StaticThrust = 3000;

    /// <summary>The steepest the plane can sit on its wheels before the tail hits the runway, degrees.</summary>
    public const double TailStrike = 12;

    public const double RollingFriction = 0.03;

    public const double BrakeFriction = 0.35;

    /// <summary>Lift per radian of angle of attack, for a wing of this aspect ratio.</summary>
    public static double LiftSlope => 2 * Math.PI * AspectRatio / (AspectRatio + 2);

    /// <summary>The air's density in the standard atmosphere, kg/m³ (troposphere).</summary>
    public static double Density(double altitude)
    {
        var h = Math.Clamp(altitude, -500, 11_000);
        return 1.225 * Math.Pow(1 - (2.25577e-5 * h), 4.25588);
    }

    /// <summary>How far the flow has torn off the wing (0: attached, 1: stalled).</summary>
    public static double Separation(double alphaDegrees) =>
        WingSection.SmoothStep(-1, 3, alphaDegrees - StallDegrees) + WingSection.SmoothStep(-1, 3, -alphaDegrees - 12);

    public static double LiftCoefficient(double alphaDegrees)
    {
        var a = alphaDegrees * Math.PI / 180;
        var w = Math.Min(1, Separation(alphaDegrees));
        return ((1 - w) * (Cl0 + (LiftSlope * a))) + (w * 1.0 * Math.Sin(2 * a));
    }

    public static double DragCoefficient(double alphaDegrees)
    {
        var a = alphaDegrees * Math.PI / 180;
        var w = Math.Min(1, Separation(alphaDegrees));
        var cl = LiftCoefficient(alphaDegrees);
        return Cd0 + (cl * cl / (Math.PI * Oswald * AspectRatio)) + (w * 1.5 * Math.Sin(a) * Math.Sin(a));
    }

    /// <summary>The angle of attack that gives a lift coefficient (below the stall), degrees.</summary>
    public static double AlphaFor(double liftCoefficient) => (liftCoefficient - Cl0) / LiftSlope * 180 / Math.PI;

    /// <summary>The propeller's thrust, N: its static pull at low speed, the engine's power (thinner up high) at speed.</summary>
    public static double Thrust(double throttle, double speed, double altitude)
    {
        var sigma = Density(altitude) / 1.225;
        return Math.Clamp(throttle, 0, 1) * Math.Min(StaticThrust, PropEfficiency * Power * sigma / Math.Max(speed, 1));
    }

    public static FlightForces Forces(FlightState state, double alphaDegrees, double throttle, double mass)
    {
        var alpha = EffectiveAlpha(state, alphaDegrees);
        var rho = Density(state.Altitude);
        var q = 0.5 * rho * state.Speed * state.Speed;
        var cl = LiftCoefficient(alpha);
        return new FlightForces(q * WingArea * cl, q * WingArea * DragCoefficient(alpha), Thrust(throttle, state.Speed, state.Altitude),
            mass * G, rho, cl, Math.Min(1, Separation(alpha)), alpha);
    }

    /// <summary>On its wheels the plane cannot point below level or past its tail strike.</summary>
    public static double EffectiveAlpha(FlightState state, double alphaDegrees) =>
        state.OnGround ? Math.Clamp(alphaDegrees, 0, TailStrike) : Math.Clamp(alphaDegrees, -15, 30);

    /// <summary>
    /// Moves the plane on by <paramref name="dt"/> seconds. In the air: Newton's second law along and across the flight
    /// path. On the runway: thrust against drag and the wheels' friction until the lift carries the weight. The slow
    /// up-and-down swing a fixed-stick plane makes after a change (the phugoid) is damped towards the steady flight the
    /// controls ask for, as a pilot would.
    /// </summary>
    public static FlightState Step(FlightState state, double alphaDegrees, double throttle, double mass, double dt)
    {
        var steps = Math.Max(1, (int)Math.Ceiling(dt / 0.01));
        var h = dt / steps;
        var steady = state.OnGround ? null : Equilibrium(alphaDegrees, throttle, mass, state.Altitude, state.Speed);
        for (var i = 0; i < steps; i++)
        {
            state = Advance(state, alphaDegrees, throttle, mass, h);
            if (steady is { } eq && Separation(alphaDegrees) < 0.2 && state.Altitude > 3)
            {
                const double tau = 9;
                state = state with
                {
                    Speed = state.Speed + ((eq.Speed - state.Speed) * h / tau),
                    PathAngle = state.PathAngle + ((eq.PathAngle - state.PathAngle) * h / tau),
                };
            }
        }

        return state;
    }

    private static FlightState Advance(FlightState s, double alphaDegrees, double throttle, double mass, double dt)
    {
        var f = Forces(s, alphaDegrees, throttle, mass);
        var a = f.Alpha * Math.PI / 180;
        if (s.OnGround)
        {
            var normal = f.Weight - f.Lift - (f.Thrust * Math.Sin(a));
            if (normal <= 0 && s.Speed > 5)
            {
                // Lift-off: the wheels leave the runway.
                return s with { OnGround = false, PathAngle = 0.002, Altitude = 0.01 };
            }

            var friction = (throttle < 0.05 && s.Speed > 0.1 ? BrakeFriction : RollingFriction) * Math.Max(0, normal);
            var accel = ((f.Thrust * Math.Cos(a)) - f.Drag - friction) / mass;
            var speed = Math.Max(0, s.Speed + (accel * dt));
            return s with { Speed = speed, X = s.X + (speed * dt), Altitude = 0, PathAngle = 0 };
        }

        var v = Math.Max(s.Speed, 4);
        var dv = (((f.Thrust * Math.Cos(a)) - f.Drag) / mass) - (G * Math.Sin(s.PathAngle));
        var dgamma = ((f.Lift + (f.Thrust * Math.Sin(a))) / (mass * v)) - (G * Math.Cos(s.PathAngle) / v);
        var next = s with
        {
            Speed = Math.Max(1, s.Speed + (dv * dt)),
            PathAngle = Math.Clamp(s.PathAngle + (dgamma * dt), -Math.PI / 2, Math.PI / 2),
            X = s.X + (s.Speed * Math.Cos(s.PathAngle) * dt),
            Altitude = s.Altitude + (s.Speed * Math.Sin(s.PathAngle) * dt),
        };

        if (next.Altitude <= 0)
        {
            // Touchdown: back on the wheels.
            next = next with { Altitude = 0, PathAngle = 0, OnGround = true, Speed = next.Speed * Math.Cos(s.PathAngle) };
        }

        return next;
    }

    /// <summary>
    /// The steady flight (speed and climb angle) these controls settle into: lift and thrust balance the weight across the
    /// path, thrust against drag and the slope along it. Of several, the one nearest <paramref name="near"/>; null if none.
    /// </summary>
    public static (double Speed, double PathAngle)? Equilibrium(double alphaDegrees, double throttle, double mass, double altitude, double near = 1000)
    {
        var weight = mass * G;
        var a = Math.Clamp(alphaDegrees, -15, 30) * Math.PI / 180;
        var cl = LiftCoefficient(alphaDegrees);
        var cd = DragCoefficient(alphaDegrees);
        var rho = Density(altitude);

        double Miss(double v)
        {
            var q = 0.5 * rho * v * v * WingArea;
            var t = Thrust(throttle, v, altitude);
            var along = ((t * Math.Cos(a)) - (q * cd)) / weight;
            var across = ((q * cl) + (t * Math.Sin(a))) / weight;
            return (along * along) + (across * across) - 1;
        }

        double? best = null;
        var previous = Miss(5);
        for (var v = 6.0; v <= 160; v += 1)
        {
            var miss = Miss(v);
            if (previous < 0 != miss < 0)
            {
                double lo = v - 1, hi = v;
                for (var i = 0; i < 40; i++)
                {
                    var mid = (lo + hi) / 2;
                    if (Miss(mid) < 0 == previous < 0) lo = mid;
                    else hi = mid;
                }

                var root = (lo + hi) / 2;
                if (best is null || Math.Abs(root - near) < Math.Abs(best.Value - near)) best = root;
            }

            previous = miss;
        }

        if (best is not { } speed) return null;
        var qq = 0.5 * rho * speed * speed * WingArea;
        var tt = Thrust(throttle, speed, altitude);
        var acrossForce = (qq * cl) + (tt * Math.Sin(a));
        if (acrossForce <= 0) return null;
        return (speed, Math.Atan2((tt * Math.Cos(a)) - (qq * cd), acrossForce));
    }

    /// <summary>The angle of attack and throttle for level flight at this speed; null if the wing would stall or the engine is too weak.</summary>
    public static (double Alpha, double Throttle)? TrimLevel(double speed, double mass, double altitude)
    {
        var q = 0.5 * Density(altitude) * speed * speed * WingArea;
        var weight = mass * G;

        // Lift plus the thrust's upward part carries the weight; a couple of rounds settle the small thrust term.
        var alpha = AlphaFor(weight / q);
        var throttle = 0.0;
        for (var i = 0; i < 4; i++)
        {
            var a = alpha * Math.PI / 180;
            var full = Thrust(1, speed, altitude);
            var drag = q * DragCoefficient(alpha);
            throttle = drag / Math.Cos(a) / full;
            var thrust = throttle * full;
            alpha = AlphaFor((weight - (thrust * Math.Sin(a))) / q);
        }

        if (alpha > StallDegrees - 0.5 || throttle > 1 || throttle < 0) return null;
        return (alpha, throttle);
    }

    /// <summary>The slowest speed the wing can carry the weight (times the load factor), m/s.</summary>
    public static double StallSpeed(double mass, double altitude, double loadFactor = 1)
    {
        var clMax = MaxLift();
        return Math.Sqrt(2 * mass * G * loadFactor / (Density(altitude) * WingArea * clMax));
    }

    /// <summary>The wing's greatest lift coefficient, just before the stall.</summary>
    public static double MaxLift()
    {
        var best = 0.0;
        for (var a = 0.0; a <= 25; a += 0.05) best = Math.Max(best, LiftCoefficient(a));
        return best;
    }

    /// <summary>The lift coefficient for the best glide (least drag for the lift): induced drag equal to the drag at no lift.</summary>
    public static double BestGlideLift => Math.Sqrt(Cd0 * Math.PI * Oswald * AspectRatio);

    /// <summary>How far the plane glides for each metre it sinks, at its best.</summary>
    public static double BestGlideRatio => BestGlideLift / (2 * Cd0);

    /// <summary>The drag in level flight at a speed, split into the part from the air's friction and the part from making lift, N.</summary>
    public static (double Parasite, double Induced) LevelDrag(double speed, double mass, double altitude)
    {
        var q = 0.5 * Density(altitude) * speed * speed * WingArea;
        var cl = mass * G / q;
        return (q * Cd0, q * cl * cl / (Math.PI * Oswald * AspectRatio));
    }

    /// <summary>The speed of least drag in level flight (where the two parts are equal), m/s.</summary>
    public static double MinDragSpeed(double mass, double altitude) =>
        Math.Sqrt(2 * mass * G / (Density(altitude) * WingArea * BestGlideLift));

    /// <summary>How many times its weight the wing must lift in a level turn at this bank.</summary>
    public static double LoadFactor(double bankDegrees) => 1 / Math.Cos(Math.Clamp(Math.Abs(bankDegrees), 0, 89) * Math.PI / 180);

    /// <summary>The radius of a level turn, m (infinite with the wings level).</summary>
    public static double TurnRadius(double speed, double bankDegrees)
    {
        var tan = Math.Tan(Math.Abs(bankDegrees) * Math.PI / 180);
        return tan < 1e-6 ? double.PositiveInfinity : speed * speed / (G * tan);
    }

    /// <summary>How fast the heading changes in a level turn, degrees per second (signed like the bank).</summary>
    public static double TurnRate(double speed, double bankDegrees) =>
        G * Math.Tan(Math.Clamp(bankDegrees, -89, 89) * Math.PI / 180) / Math.Max(speed, 1) * 180 / Math.PI;
}
