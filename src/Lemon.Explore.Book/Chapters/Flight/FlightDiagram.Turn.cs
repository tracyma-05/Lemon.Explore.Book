using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Vector3D = Lemon.Explore.Book.Controls.Space.Vector3D;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Flight;

/// <summary>
/// The plane in 3D, followed from behind over a gridded landscape: its ailerons, elevator and rudder moving, the axes it
/// turns about, the banked turn with the lift tilted and split into the part that holds the weight up and the part that
/// pulls the plane round, a view from behind of that split and a map of the circle flown.
/// </summary>
/// <remarks>
/// World: x east, y north, z up, metres; the ground lies 60 m below the plane. Body: x forward, y left, z up. The plane is
/// a few dozen flat faces drawn back to front.
/// </remarks>
public sealed partial class FlightDiagram
{
    private const double GroundBelow = 60;

    /// <summary>The bank flown now (eases towards <see cref="Bank"/>), heading and pitch, degrees.</summary>
    private double _bankNow;

    private double _heading = 90;
    private double _cameraHeading = 90;
    private double _pitchNow;
    private Point _turnPosition;
    private double _demoTime;

    /// <summary>The control surfaces' deflections now, degrees (aileron: right one's trailing edge down; elevator: trailing edges down; rudder: trailing edge right).</summary>
    private double _aileron;

    private double _elevator;
    private double _rudder;
    private readonly List<Point> _trail = [];
    private double _trailClock;

    private void OnDemoChanged()
    {
        _demoTime = 0;
        if (Demo != TurnDemo.Turn) _trail.Clear();
    }

    private void StepTurn(double dt)
    {
        var speed = TurnSpeed / 3.6;
        _demoTime += dt;
        var oldBank = _bankNow;
        var course = _heading;
        switch (Demo)
        {
            case TurnDemo.Roll:
            {
                // Rolling side to side: the ailerons lead the roll.
                var phase = _demoTime * 2 * Math.PI / 5;
                _bankNow = 35 * Math.Sin(phase);
                _aileron = -14 * Math.Cos(phase);
                _elevator = 0;
                _rudder = 0;
                _pitchNow = 0;
                break;
            }

            case TurnDemo.Pitch:
            {
                var phase = _demoTime * 2 * Math.PI / 5;
                _bankNow = Approach(_bankNow, 0, 40 * dt);
                _pitchNow = 12 * Math.Sin(phase);
                _elevator = -16 * Math.Cos(phase);
                _aileron = 0;
                _rudder = 0;
                break;
            }

            case TurnDemo.Yaw:
            {
                var phase = _demoTime * 2 * Math.PI / 5;
                _bankNow = Approach(_bankNow, 0, 40 * dt);
                _pitchNow = 0;
                _rudder = -18 * Math.Cos(phase);
                _aileron = 0;
                _elevator = 0;
                break;
            }

            default:
            {
                // Roll towards the bank asked for at up to 30°/s; the ailerons are out only while rolling.
                var target = Math.Clamp(Bank, -75, 75);
                _bankNow = Approach(_bankNow, target, 30 * dt);
                var rollRate = (_bankNow - oldBank) / Math.Max(dt, 1e-6);
                _aileron = Approach(_aileron, Math.Clamp(-rollRate * 0.5, -15, 15), 80 * dt);
                var n = Trainer.LoadFactor(_bankNow);
                _elevator = Approach(_elevator, -Math.Clamp((n - 1) * 9, 0, 20), 40 * dt);
                _rudder = Approach(_rudder, Math.Clamp(rollRate * 0.12, -6, 6), 40 * dt);
                _pitchNow = Approach(_pitchNow, 2, 10 * dt);
                _heading -= Trainer.TurnRate(speed, _bankNow) * dt;
                course = _heading;
                break;
            }
        }

        var yawWiggle = Demo == TurnDemo.Yaw ? 12 * Math.Sin(_demoTime * 2 * Math.PI / 5) : 0;
        _yawOffset = yawWiggle;
        var rad = course * Math.PI / 180;
        _turnPosition += new Vector(Math.Cos(rad), Math.Sin(rad)) * speed * dt;
        _cameraHeading = ApproachAngle(_cameraHeading, _heading, dt / 0.9);

        _trailClock += dt;
        if (_trailClock > 0.25)
        {
            _trailClock = 0;
            _trail.Add(_turnPosition);
            if (_trail.Count > 480) _trail.RemoveAt(0);
        }
    }

    /// <summary>The nose's swing in the yaw demonstration, degrees.</summary>
    private double _yawOffset;

    private static double Approach(double value, double target, double step) =>
        Math.Abs(target - value) <= step ? target : value + (Math.Sign(target - value) * step);

    private static double ApproachAngle(double value, double target, double fraction)
    {
        var delta = ((target - value + 540) % 360) - 180;
        return value + (delta * Math.Clamp(fraction, 0, 1));
    }

    #region the camera

    private readonly record struct Camera(Point3D Position, Vector3D Forward, Vector3D Right, Vector3D Up, Point Center, double Focal)
    {
        public double Depth(Point3D p) => Vector3D.DotProduct(p - Position, Forward);

        public Point Project(Point3D p)
        {
            var d = p - Position;
            var z = Math.Max(0.05, Vector3D.DotProduct(d, Forward));
            return new Point(Center.X + (Focal * Vector3D.DotProduct(d, Right) / z), Center.Y - (Focal * Vector3D.DotProduct(d, Up) / z));
        }
    }

    private Camera MakeCamera(Rect stage)
    {
        var heading = _cameraHeading * Math.PI / 180;
        var back = new Vector3D(Math.Cos(heading), Math.Sin(heading), 0);
        var plane = new Point3D(_turnPosition.X, _turnPosition.Y, 0);
        var position = plane - (back * 22) + new Vector3D(0, 0, 4.6);
        var forward = (plane + new Vector3D(0, 0, 1.2)) - position;
        forward = forward.Normalize();
        var right = Vector3D.CrossProduct(forward, new Vector3D(0, 0, 1));
        right = right.Normalize();
        var up = Vector3D.CrossProduct(right, forward);
        // The plane is fitted to the room above the two insets (rear and top views) and centred in it, so a banked wing
        // on a tall, narrow page stays clear of them.
        var above = Math.Max(120, RearViewBox().Top - stage.Y - 8);
        var focal = Math.Min(stage.Width * 0.8, above * 1.9);
        return new Camera(position, forward, right, up, new Point(stage.X + (stage.Width * 0.47), stage.Y + (above * 0.5)), focal);
    }

    #endregion

    #region the plane's shape

    private readonly record struct Face(Point3D[] Points, Color Colour, bool TwoSided, string? Id);

    private static readonly Color BodyColour = Color.FromRgb(0xE9, 0xEE, 0xF4);
    private static readonly Color GlassColour = Color.FromRgb(0x2A, 0x3E, 0x5E);
    private static readonly Color AileronColour = Color.FromRgb(0x6F, 0xD8, 0xFF);
    private static readonly Color ElevatorColour = Color.FromRgb(0x7C, 0xFF, 0x9E);
    private static readonly Color RudderColour = Color.FromRgb(0xB0, 0x8C, 0xFF);
    private static readonly Color StripeColour = Color.FromRgb(0x2E, 0x6F, 0xD8);

    /// <summary>The fuselage's stations: x, half width, half height, centre height.</summary>
    private static readonly (double X, double W, double H, double Z)[] Stations =
    [
        (3.95, 0.12, 0.12, 0.05),
        (3.5, 0.42, 0.45, 0.0),
        (2.5, 0.58, 0.62, 0.06),
        (1.2, 0.6, 0.72, 0.12),
        (0.0, 0.55, 0.62, 0.1),
        (-1.5, 0.38, 0.42, 0.14),
        (-3.7, 0.1, 0.16, 0.32),
    ];

    /// <summary>The plane's faces in body coordinates, with the control surfaces turned by their deflections.</summary>
    private List<Face> PlaneFaces()
    {
        var faces = new List<Face>(96);

        // The fuselage: an octagonal loft, its cabin's upper sides glass.
        const int sides = 8;
        for (var s = 0; s < Stations.Length - 1; s++)
        {
            var a = Stations[s];
            var b = Stations[s + 1];
            for (var k = 0; k < sides; k++)
            {
                var t0 = ((k + 0.5) * 2 * Math.PI / sides);
                var t1 = ((k + 1.5) * 2 * Math.PI / sides);
                var mid = (t0 + t1) / 2;
                var cabin = s == 2 && Math.Sin(mid) > 0.3;
                var stripe = Math.Abs(Math.Sin(mid)) < 0.5 && s is >= 1 and <= 4;
                faces.Add(new Face(
                [
                    Ring(a, t0), Ring(b, t0), Ring(b, t1), Ring(a, t1),
                ], cabin ? GlassColour : stripe ? StripeColour : BodyColour, false, null));
            }
        }

        // Wing: low, with a little dihedral; the outer rear part is the aileron.
        foreach (var side in new[] { -1.0, 1.0 })
        {
            Point3D W(double x, double y) => new(x, side * y, -0.38 + (y * Math.Tan(5 * Math.PI / 180)));
            double Le(double y) => 1.3 - (0.25 * (y - 0.55) / 4.95);
            double Te(double y) => -0.3 + (0.15 * (y - 0.55) / 4.95);
            double Hinge(double y) => Te(y) + (0.3 * (Le(y) - Te(y)));
            faces.Add(new Face([W(Le(0.55), 0.55), W(Le(3.0), 3.0), W(Te(3.0), 3.0), W(Te(0.55), 0.55)], BodyColour, true, null));
            faces.Add(new Face([W(Le(3.0), 3.0), W(Le(5.3), 5.3), W(Hinge(5.3), 5.3), W(Hinge(3.0), 3.0)], BodyColour, true, null));
            faces.Add(new Face([W(Le(5.3), 5.3), W(Le(5.5) - 0.1, 5.5), W(Te(5.5) + 0.05, 5.5), W(Te(5.3), 5.3)], BodyColour, true, null));

            // The right aileron (y < 0) goes down for a positive deflection, the left one up (their hinges point opposite ways).
            var deflection = _aileron;
            var h1 = W(Hinge(3.0), 3.0);
            var h2 = W(Hinge(5.3), 5.3);
            faces.Add(new Face([h1, h2, Turn(W(Te(5.3), 5.3), h1, h2, deflection), Turn(W(Te(3.0), 3.0), h1, h2, deflection)], AileronColour, true, "aileron"));
        }

        // Horizontal tail and elevator (trailing edge up for a negative deflection: pulling back).
        foreach (var side in new[] { -1.0, 1.0 })
        {
            Point3D T(double x, double y) => new(x, side * y, 0.32);
            faces.Add(new Face([T(-2.7, 0.15), T(-2.95, 1.8), T(-3.25, 1.8), T(-3.25, 0.15)], BodyColour, true, null));
            var h1 = T(-3.25, 0.15);
            var h2 = T(-3.25, 1.8);
            var down = side < 0 ? _elevator : -_elevator;
            faces.Add(new Face([h1, h2, Turn(T(-3.65, 1.8), h1, h2, down), Turn(T(-3.7, 0.15), h1, h2, down)], ElevatorColour, true, "elevator"));
        }

        // Fin and rudder (trailing edge to the right for a positive deflection: nose right).
        var f1 = new Point3D(-3.35, 0, 0.4);
        var f2 = new Point3D(-3.45, 0, 1.75);
        faces.Add(new Face([new Point3D(-2.4, 0, 0.4), new Point3D(-3.15, 0, 1.75), f2, f1], BodyColour, true, null));
        faces.Add(new Face([f1, f2, Turn(new Point3D(-3.8, 0, 1.7), f1, f2, _rudder), Turn(new Point3D(-3.85, 0, 0.42), f1, f2, _rudder)], RudderColour, true, "rudder"));

        // Propeller: two blades turning in front of the nose.
        var spin = _time * 40;
        for (var b = 0; b < 2; b++)
        {
            var angle = spin + (b * Math.PI);
            var dir = new Vector3D(0, Math.Cos(angle), Math.Sin(angle));
            var across = new Vector3D(0, -Math.Sin(angle), Math.Cos(angle)) * 0.06;
            var hub = new Point3D(4.0, 0, 0.05);
            faces.Add(new Face([hub - across, hub + (dir * 0.95) - across, hub + (dir * 0.95) + across, hub + across], Color.FromRgb(0x30, 0x34, 0x3A), true, "propeller"));
        }

        return faces;
    }

    private static Point3D Ring((double X, double W, double H, double Z) station, double angle) =>
        new(station.X, station.W * Math.Cos(angle), station.Z + (station.H * Math.Sin(angle)));

    /// <summary><paramref name="p"/> turned by <paramref name="degrees"/> about the hinge line from <paramref name="a"/> to <paramref name="b"/>.</summary>
    private static Point3D Turn(Point3D p, Point3D a, Point3D b, double degrees)
    {
        var axis = b - a;
        axis = axis.Normalize();
        var v = p - a;
        var t = degrees * Math.PI / 180;
        var turned = (v * Math.Cos(t)) + (Vector3D.CrossProduct(axis, v) * Math.Sin(t)) + (axis * Vector3D.DotProduct(axis, v) * (1 - Math.Cos(t)));
        return a + turned;
    }

    /// <summary>Body to world: yaw (heading), pitch (nose up) and roll (right wing down), at the plane's place.</summary>
    private Point3D ToWorld(Point3D body)
    {
        var heading = (_heading + _yawOffset) * Math.PI / 180;
        var pitch = _pitchNow * Math.PI / 180;
        var roll = _bankNow * Math.PI / 180;

        // Roll about x: the right wing (y < 0) goes down for a positive bank.
        var y1 = (body.Y * Math.Cos(roll)) - (body.Z * Math.Sin(roll));
        var z1 = (body.Y * Math.Sin(roll)) + (body.Z * Math.Cos(roll));
        var x1 = body.X;

        // Pitch: the nose up.
        var x2 = (x1 * Math.Cos(pitch)) - (z1 * Math.Sin(pitch));
        var z2 = (x1 * Math.Sin(pitch)) + (z1 * Math.Cos(pitch));

        // Heading, anticlockwise from east.
        var x3 = (x2 * Math.Cos(heading)) - (y1 * Math.Sin(heading));
        var y3 = (x2 * Math.Sin(heading)) + (y1 * Math.Cos(heading));
        return new Point3D(x3 + _turnPosition.X, y3 + _turnPosition.Y, z2);
    }

    private Vector3D ToWorld(Vector3D body) => ToWorld(new Point3D(body.X, body.Y, body.Z)) - ToWorld(new Point3D(0, 0, 0));

    #endregion

    #region drawing

    private static readonly Vector3D Sun = Normalized(new Vector3D(0.35, 0.45, 0.85));

    private static Vector3D Normalized(Vector3D v)
    {
        v = v.Normalize();
        return v;
    }

    private void DrawTurnMode(PaintContext dc, double now)
    {
        var stage = Stage();
        var camera = MakeCamera(stage);
        DrawWorld(dc, camera);

        var faces = PlaneFaces()
            .Select(f => new Face(f.Points.Select(ToWorld).ToArray(), f.Colour, f.TwoSided, f.Id))
            .Select(f => (Face: f, Depth: f.Points.Average(p => camera.Depth(p))))
            .OrderByDescending(f => f.Depth)
            .ToList();

        // Axes behind the plane first where they pass behind it: drawn after it, faint, so they read through.
        var parts = new Dictionary<string, List<Point>>();
        foreach (var (face, _) in faces)
        {
            var centre = new Point3D(face.Points.Average(p => p.X), face.Points.Average(p => p.Y), face.Points.Average(p => p.Z));
            var normal = Vector3D.CrossProduct(face.Points[1] - face.Points[0], face.Points[^1] - face.Points[0]);
            if (normal.Length < 1e-9) continue;
            normal = normal.Normalize();
            var towardCamera = camera.Position - centre;
            if (Vector3D.DotProduct(normal, towardCamera) < 0)
            {
                if (!face.TwoSided) continue;
                normal = -normal;
            }

            var light = 0.42 + (0.58 * Math.Max(0, Vector3D.DotProduct(normal, Sun)));
            var colour = Color.FromRgb((byte)(face.Colour.R * light), (byte)(face.Colour.G * light), (byte)(face.Colour.B * light));
            var screen = face.Points.Select(camera.Project).ToArray();
            dc.DrawGeometry(FadedBrush(colour, 1), FadedPen(Color.FromRgb(0x1A, 0x22, 0x2E), 0.35, 0.7), Polygon(screen));
            if (face.Id is { } id)
            {
                if (!parts.TryGetValue(id, out var list)) parts[id] = list = [];
                list.AddRange(screen);
            }
        }

        foreach (var (id, points) in parts)
        {
            if (id == "aileron")
            {
                // Each aileron's own middle, so both can be clicked.
                var half = points.Count / 2;
                _targets.Add(id, Centre(points.Take(half)), 12);
                _targets.Add(id, Centre(points.Skip(half)), 12);
            }
            else
            {
                _targets.Add(id, Centre(points), id == "propeller" ? 14 : 12);
            }
        }

        if (Demo != TurnDemo.Turn) DrawAxes(dc, camera, now);
        else DrawTurnForces(dc, camera);
        if (ShowLabels) DrawSurfaceLabels(dc, parts);

        DrawRearView(dc);
        DrawTopView(dc);
        DrawTurnReadout(dc, stage);
    }

    private static Point Centre(IEnumerable<Point> points)
    {
        var list = points.ToList();
        return new Point(list.Average(p => p.X), list.Average(p => p.Y));
    }

    private static readonly IBrush TurnSky = SpaceDrawing.LinearGradient(Color.FromRgb(0x2A, 0x62, 0xB8), Color.FromRgb(0xB4, 0xD8, 0xF4), 90);
    private static readonly IBrush TurnGround = SpaceDrawing.LinearGradient(Color.FromRgb(0x6E, 0x96, 0x5A), Color.FromRgb(0x2E, 0x52, 0x2A), 90);

    /// <summary>The sky, the ground with a grid fixed to it, and the track flown traced on the ground.</summary>
    private void DrawWorld(PaintContext dc, Camera camera)
    {
        var flat = new Vector3D(camera.Forward.X, camera.Forward.Y, 0);
        flat = flat.Normalize();
        var horizon = camera.Project(camera.Position + (flat * 1e6)).Y;
        dc.DrawRectangle(TurnSky, null, new Rect(0, 0, ActualWidth, Math.Max(0, horizon)));
        dc.DrawRectangle(TurnGround, null, new Rect(0, horizon, ActualWidth, Math.Max(0, ActualHeight - horizon)));

        // Grid lines every 50 m, cut into pieces so the near ones bend correctly into perspective and the far ones fade.
        const double spacing = 50;
        const double reach = 1100;
        var cx = Math.Round(camera.Position.X / spacing) * spacing;
        var cy = Math.Round(camera.Position.Y / spacing) * spacing;
        var pens = new[] { FadedPen(Colors.White, 0.32, 1, flat: true), FadedPen(Colors.White, 0.18, 1, flat: true), FadedPen(Colors.White, 0.08, 1, flat: true) };
        for (var i = -reach; i <= reach; i += spacing)
        {
            GridLine(dc, camera, new Point3D(cx + i, cy - reach, -GroundBelow), new Point3D(cx + i, cy + reach, -GroundBelow), pens);
            GridLine(dc, camera, new Point3D(cx - reach, cy + i, -GroundBelow), new Point3D(cx + reach, cy + i, -GroundBelow), pens);
        }

        _turnBatch.Draw(dc);

        // The track: where the plane has been, on the ground below it.
        var trail = new List<Point>();
        foreach (var p in _trail)
        {
            var q = new Point3D(p.X, p.Y, -GroundBelow);
            if (camera.Depth(q) > 3) trail.Add(camera.Project(q));
            else if (trail.Count > 1)
            {
                dc.DrawGeometry(null, FadedPen(Ink.Gold, 0.55, 2), Line(trail));
                trail.Clear();
            }
        }

        if (trail.Count > 1) dc.DrawGeometry(null, FadedPen(Ink.Gold, 0.55, 2), Line(trail));

        // The plane's shadow straight below it.
        var below = camera.Project(new Point3D(_turnPosition.X, _turnPosition.Y, -GroundBelow));
        dc.DrawEllipse(Solid(60, 0, 0, 0), null, below, 26, 6);
    }

    private readonly LineBatch _turnBatch = new();

    private void GridLine(PaintContext dc, Camera camera, Point3D a, Point3D b, IPen[] pens)
    {
        const int pieces = 22;
        for (var i = 0; i < pieces; i++)
        {
            var p = a + ((b - a) * (i / (double)pieces));
            var q = a + ((b - a) * ((i + 1) / (double)pieces));
            var dp = camera.Depth(p);
            var dq = camera.Depth(q);
            if (dp < 4 || dq < 4) continue;
            var far = Math.Min(dp, dq);
            var pen = far < 300 ? pens[0] : far < 700 ? pens[1] : pens[2];
            _turnBatch.Add(pen, camera.Project(p), camera.Project(q));
        }
    }

    /// <summary>The axis the demonstration turns about, through the plane, with a curved arrow round it.</summary>
    private void DrawAxes(PaintContext dc, Camera camera, double now)
    {
        var (id, from, to, colour, around1, around2) = Demo switch
        {
            TurnDemo.Pitch => ("pitchaxis", new Vector3D(0, 6.5, 0), new Vector3D(0, -6.5, 0), Ink.Green, new Vector3D(1, 0, 0), new Vector3D(0, 0, 1)),
            TurnDemo.Yaw => ("yawaxis", new Vector3D(0, 0, -2.8), new Vector3D(0, 0, 3.6), Ink.Violet, new Vector3D(1, 0, 0), new Vector3D(0, 1, 0)),
            _ => ("rollaxis", new Vector3D(-6.5, 0, 0.1), new Vector3D(7.5, 0, 0.1), Ink.Cyan, new Vector3D(0, 1, 0), new Vector3D(0, 0, 1)),
        };

        // The demonstrations swing the plane but the axis stays fixed to it.
        var a = camera.Project(ToWorld(new Point3D(from.X, from.Y, from.Z)));
        var b = camera.Project(ToWorld(new Point3D(to.X, to.Y, to.Z)));
        if (Glows(id)) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 7), Line([a, b]));
        dc.DrawLine(FadedPen(colour, 0.95, 2.2, dashed: true), a, b);

        // A curved arrow round the axis near its far end.
        var end = to * 0.8;
        var ring = new List<Point>();
        for (var i = 0; i <= 30; i++)
        {
            var t = (i / 30.0 * 1.6 * Math.PI) + now;
            var p = new Point3D(end.X, end.Y, end.Z) + (around1 * Math.Cos(t) * 1.1) + (around2 * Math.Sin(t) * 1.1);
            ring.Add(camera.Project(ToWorld(p)));
        }

        dc.DrawGeometry(null, FadedPen(colour, 0.95, 2), Line(ring));
        DrawArrowHead(dc, FadedBrush(colour, 0.95), ring[^1], ring[^1] - ring[^3], 9);
        var label = Loc(Demo switch
        {
            TurnDemo.Pitch => "Flight_Label_PitchAxis",
            TurnDemo.Yaw => "Flight_Label_YawAxis",
            _ => "Flight_Label_RollAxis",
        });
        DrawTargetPill(dc, label, b + (b - a) / Math.Max(1, ((Vector)(b - a)).Length) * 30, colour, id);
    }

    /// <summary>In the turn: the lift along the plane's up, its two parts, and the weight.</summary>
    private void DrawTurnForces(PaintContext dc, Camera camera)
    {
        const double perG = 3.4;
        var n = Trainer.LoadFactor(_bankNow);
        var cg = ToWorld(new Point3D(0.4, 0, 0.1));
        var up = ToWorld(new Vector3D(0, 0, 1));
        var lift = cg + (up * perG * n);
        var vertical = cg + new Vector3D(0, 0, perG * up.Z * n);
        var weight = cg + new Vector3D(0, 0, -perG);
        var c = camera.Project(cg);
        DrawArrow(dc, c, camera.Project(weight), Ink.Orange, 3);
        if (Math.Abs(_bankNow) > 3)
        {
            DrawArrow(dc, c, camera.Project(vertical), Ink.Cyan, 2, 0.75, dashed: true);
            var side = cg + new Vector3D(up.X, up.Y, 0) * perG * n;
            DrawArrow(dc, c, camera.Project(side), Ink.Gold, 2.4, 0.95, dashed: true);
            if (ShowLabels) DrawTargetPill(dc, Loc("Flight_Label_Centripetal"), camera.Project(side) + new Vector(Math.Sign(_bankNow) * 60, 16), Ink.Gold, "centripetal");
        }

        var tip = camera.Project(lift);
        DrawArrow(dc, c, tip, Ink.Cyan, 3.4);
        _targets.Add("liftvector", tip, 10);
        if (ShowLabels) DrawTargetPill(dc, LocFormat("Flight_Label_LiftG", Number(n, "0.00")), tip + new Vector(0, -18), Ink.Cyan, "liftvector");
    }

    private void DrawSurfaceLabels(PaintContext dc, Dictionary<string, List<Point>> parts)
    {
        // The control surface the demonstration moves gets its name; in a turn, all three, small.
        var which = Demo switch
        {
            TurnDemo.Roll => new[] { "aileron" },
            TurnDemo.Pitch => new[] { "elevator" },
            TurnDemo.Yaw => new[] { "rudder" },
            _ => ["aileron", "elevator", "rudder"],
        };
        foreach (var id in which)
        {
            if (!parts.TryGetValue(id, out var points)) continue;
            var (key, colour) = id switch
            {
                "elevator" => ("Flight_Label_Elevator", Ink.Green),
                "rudder" => ("Flight_Label_Rudder", Ink.Violet),
                _ => ("Flight_Label_Aileron", Ink.Cyan),
            };
            if (id == "aileron")
            {
                // Over whichever aileron is further right on screen.
                var half = points.Count / 2;
                var right = Centre(points.Take(half));
                var left = Centre(points.Skip(half));
                var at = right.X > left.X ? right : left;
                DrawTargetPill(dc, Loc(key), at + new Vector(30, 28), colour, id);
            }
            else
            {
                var at = Centre(points);
                DrawTargetPill(dc, Loc(key), at + new Vector(id == "rudder" ? -56 : 56, id == "rudder" ? -30 : 26), colour, id);
            }
        }
    }

    /// <summary>The view from behind, bottom left.</summary>
    private Rect RearViewBox()
    {
        var stage = Stage();
        return new Rect(stage.X + 18, stage.Bottom - 206, 250, 194);
    }

    /// <summary>The map of the circle, bottom right.</summary>
    private Rect TopViewBox()
    {
        var stage = Stage();
        return new Rect(stage.Right - 214, stage.Bottom - 206, 196, 194);
    }

    /// <summary>
    /// From behind: the plane banked, its lift (n times the weight) along its up, split into the upward part that holds the
    /// weight and the sideways part that pulls it into the turn, and the weight.
    /// </summary>
    private void DrawRearView(PaintContext dc)
    {
        var box = RearViewBox();
        DrawFrame(dc, box);
        DrawText(dc, Loc("Flight_Rear_Title"), new Point(box.X + 14, box.Y + 14), Ink.Dim, 11, bold: true);
        AddBoxTarget("rearview", new Rect(box.X, box.Y, box.Width, 28));

        var bank = _bankNow * Math.PI / 180;
        var centre = new Point(box.X + (box.Width / 2), box.Y + 124);
        var up = new Vector(Math.Sin(bank), -Math.Cos(bank));
        var right = new Vector(Math.Cos(bank), Math.Sin(bank));

        // The plane from behind: the right wing (on the right) goes down for a positive bank.
        Point B(double x, double y) => centre + (right * x) + (up * y);
        var wing = MakePen(Solid(255, 0xE9, 0xEE, 0xF4), 3.2);
        dc.DrawLine(wing, B(-62, -6), B(-8, -12));
        dc.DrawLine(wing, B(8, -12), B(62, -6));
        dc.DrawLine(MakePen(Solid(255, 0xE9, 0xEE, 0xF4), 2.4), B(-20, 3), B(20, 3));
        dc.DrawLine(MakePen(Solid(255, 0xB0, 0x8C, 0xFF), 2.6), B(0, 4), B(0, 22));
        dc.DrawEllipse(Solid(255, 0xE9, 0xEE, 0xF4), Outline, centre, 9, 9);

        const double perG = 40;
        var n = Trainer.LoadFactor(_bankNow);
        var shown = Math.Min(n, 2.3);
        var liftTip = centre + (up * perG * shown);
        DrawArrow(dc, centre, centre + new Vector(0, perG), Ink.Orange, 2.6);
        if (Math.Abs(_bankNow) > 3)
        {
            DrawArrow(dc, centre, centre + new Vector(0, -perG * shown * Math.Cos(bank)), Ink.Cyan, 1.8, 0.75, dashed: true);
            DrawArrow(dc, centre, centre + new Vector(perG * shown * Math.Sin(bank), 0), Ink.Gold, 2, 0.95, dashed: true);
            dc.DrawLine(FadedPen(Colors.White, 0.3, 1, dashed: true), liftTip, centre + new Vector(0, -perG * shown * Math.Cos(bank)));
            dc.DrawLine(FadedPen(Colors.White, 0.3, 1, dashed: true), liftTip, centre + new Vector(perG * shown * Math.Sin(bank), 0));
        }

        DrawArrow(dc, centre, liftTip, Ink.Cyan, 2.8);

        DrawText(dc, Loc("Flight_Rear_Lift"), liftTip + new Vector(_bankNow >= 0 ? 8 : -8, -6), Ink.Cyan, 10.5, _bankNow >= 0 ? TextAnchor.Left : TextAnchor.Right);
        DrawText(dc, Loc("Flight_Rear_Weight"), centre + new Vector(8, perG + 4), Ink.Orange, 10.5);
        var text = LocFormat("Flight_Rear_LoadFactor", Number(n, "0.00"));
        var width = _labels.Text(text, 11.5, Ink.Gold).Width;
        DrawTargetPill(dc, text, new Point(box.Right - 12 - (width / 2) - 6, box.Bottom - 16), Ink.Gold, "loadfactor");
        if (Math.Abs(_bankNow) > 3)
        {
            var sideText = Loc("Flight_Rear_Side");
            var x = centre.X + (perG * shown * Math.Sin(bank)) + (_bankNow >= 0 ? 8 : -8);
            DrawText(dc, sideText, new Point(x, centre.Y), Ink.Gold, 10.5, _bankNow >= 0 ? TextAnchor.Left : TextAnchor.Right);
        }
    }

    /// <summary>The circle flown, from above, with its radius and how long one turn takes.</summary>
    private void DrawTopView(PaintContext dc)
    {
        var box = TopViewBox();
        DrawFrame(dc, box);
        DrawText(dc, Loc("Flight_Top_Title"), new Point(box.X + 14, box.Y + 14), Ink.Dim, 11, bold: true);
        AddBoxTarget("topview", new Rect(box.X, box.Y, box.Width, 28));

        var speed = TurnSpeed / 3.6;
        var radius = Trainer.TurnRadius(speed, _bankNow);
        var area = new Rect(box.X + 12, box.Y + 30, box.Width - 24, box.Height - 64);
        var middle = new Point(area.X + (area.Width / 2), area.Y + (area.Height / 2));
        var heading = _heading * Math.PI / 180;

        // North up on the map; the plane goes round its circle.
        var forward = new Vector(Math.Cos(heading), -Math.Sin(heading));
        if (double.IsPositiveInfinity(radius) || radius > 20_000 || Demo != TurnDemo.Turn)
        {
            dc.DrawLine(FadedPen(Ink.Gold, 0.8, 1.8, dashed: true), middle - (forward * 60), middle + (forward * 60));
            DrawPlaneIcon(dc, middle, forward);
            DrawText(dc, Loc("Flight_Top_Straight"), new Point(box.X + (box.Width / 2), box.Bottom - 22), Ink.Text, 11, TextAnchor.Center);
            return;
        }

        var r = Math.Min(area.Width, area.Height) * 0.42;
        var inward = new Vector(-forward.Y, forward.X) * (_bankNow > 0 ? 1 : -1);
        var centre = middle;
        var plane = centre - (inward * r);
        dc.DrawEllipse(null, FadedPen(Ink.Gold, 0.85, 1.8), centre, r, r);
        dc.DrawLine(FadedPen(Colors.White, 0.5, 1, dashed: true), centre, plane);
        dc.DrawEllipse(FadedBrush(Colors.White, 0.8), null, centre, 2.5, 2.5);
        DrawPlaneIcon(dc, plane, forward);
        var seconds = 360 / Math.Abs(Trainer.TurnRate(speed, _bankNow));
        DrawText(dc, LocFormat("Flight_Top_Radius", Metres(radius)), new Point(box.X + (box.Width / 2), box.Bottom - 30), Ink.Text, 11, TextAnchor.Center);
        DrawText(dc, LocFormat("Flight_Top_Time", Number(seconds, "0")), new Point(box.X + (box.Width / 2), box.Bottom - 14), Ink.Dim, 10.5, TextAnchor.Center);
    }

    private static void DrawPlaneIcon(PaintContext dc, Point at, Vector forward)
    {
        var side = new Vector(-forward.Y, forward.X);
        var brush = Solid(255, 0xE9, 0xEE, 0xF4);
        dc.DrawGeometry(brush, null, Polygon(at + (forward * 9), at + (side * 2) - (forward * 2), at + (side * 9) - (forward * 1), at + (side * 9) - (forward * 4), at + (side * 2) - (forward * 4),
            at + (side * 1.5) - (forward * 8), at + (side * 4) - (forward * 10), at - (side * 4) - (forward * 10), at - (side * 1.5) - (forward * 8), at - (side * 2) - (forward * 4),
            at - (side * 9) - (forward * 4), at - (side * 9) - (forward * 1), at - (side * 2) - (forward * 2)));
    }

    /// <summary>The bank and the stall warning, top right.</summary>
    private void DrawTurnReadout(PaintContext dc, Rect stage)
    {
        var speed = TurnSpeed / 3.6;
        var text = LocFormat("Flight_Instrument_Bank", Number(Math.Abs(_bankNow), "0"), Loc(_bankNow > 0.5 ? "Flight_Value_Right" : _bankNow < -0.5 ? "Flight_Value_Left" : "Flight_Value_Level"));
        var width = _labels.Text(text, 12, Ink.Text).Width;
        DrawPill(dc, text, new Point(stage.Right - 20 - (width / 2) - 6, stage.Y + 14), Ink.Text, 12);
        var speedText = LocFormat("Flight_Instrument_Speed", Kmh(speed));
        width = _labels.Text(speedText, 12, Ink.Text).Width;
        DrawTargetPill(dc, speedText, new Point(stage.Right - 20 - (width / 2) - 6, stage.Y + 42), Ink.Text, "airspeed", 12);
        if (Demo == TurnDemo.Turn && speed < Trainer.StallSpeed(1000, 0, Trainer.LoadFactor(_bankNow)))
        {
            var warn = Loc("Flight_Label_TurnStall");
            width = _labels.Text(warn, 13, Ink.Red).Width;
            DrawTargetPill(dc, warn, new Point(stage.Right - 20 - (width / 2) - 6, stage.Y + 72), Ink.Red, "stallwarn", 13);
        }
    }

    #endregion
}
