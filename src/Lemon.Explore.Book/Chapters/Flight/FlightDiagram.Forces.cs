using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Flight;

/// <summary>
/// The flying plane, seen from the side: the sky, clouds and the ground it passes (to scale), the plane (drawn larger),
/// its four forces as arrows, its flight path and angle of attack, a panel weighing lift against weight and thrust
/// against drag with the throttle lever, and its instruments.
/// </summary>
public sealed partial class FlightDiagram
{
    /// <summary>The scenery's scale, px per metre (the plane itself is drawn about six times larger).</summary>
    private const double SceneScale = 3.2;

    /// <summary>The plane's drawing scale, px per metre.</summary>
    private const double PlaneScale = 20;

    /// <summary>Thrust and drag are drawn this many times longer than lift and weight, or they would hardly show.</summary>
    internal const double ThrustDragScale = 8;

    private FlightState _state = new(2000, 1000, 55, 0, false);
    private bool _writingBack;
    private Point _planeScreen;
    private Rect _balanceBox;

    /// <summary>When the wheels last left or touched the runway (playing time), and which.</summary>
    private (double Time, bool LiftOff, double Sink)? _wheelsEvent;

    /// <summary>The propeller's turn, rad.</summary>
    private double _propeller;

    /// <summary>A new speed, height, climb angle, place or ground state from outside puts the plane there.</summary>
    private void OnStateSet()
    {
        if (_writingBack) return;
        _state = new FlightState(PositionX, Altitude, Speed, PathAngle, OnGround);
        _wheelsEvent = null;
    }

    private void StepForces(double dt)
    {
        var before = _state;
        _state = Trainer.Step(_state, PlaneAlpha, Throttle, Mass, dt);
        _propeller += dt * (8 + (60 * Throttle));
        if (before.OnGround && !_state.OnGround) _wheelsEvent = (_time, true, 0);
        if (!before.OnGround && _state.OnGround) _wheelsEvent = (_time, false, -before.Speed * Math.Sin(before.PathAngle));

        _writingBack = true;
        try
        {
            SetCurrentValue(SpeedProperty, _state.Speed);
            SetCurrentValue(AltitudeProperty, _state.Altitude);
            SetCurrentValue(PathAngleProperty, _state.PathAngle);
            SetCurrentValue(OnGroundProperty, _state.OnGround);
            SetCurrentValue(PositionXProperty, _state.X);
        }
        finally
        {
            _writingBack = false;
        }
    }

    /// <summary>The throttle lever's slot, at the right of the balance panel.</summary>
    private Rect ThrottleSlot() => _balanceBox == default ? default(Rect) : new Rect(_balanceBox.Right - 40, _balanceBox.Y + 26, 24, _balanceBox.Height - 40);

    private void DrawForcesMode(PaintContext dc, double now)
    {
        var stage = Stage();
        var s = _state;
        var forces = Trainer.Forces(s, PlaneAlpha, Throttle, Mass);
        var gamma = s.PathAngle;
        var theta = gamma + (forces.Alpha * Math.PI / 180);

        // Where the ground and the plane are on screen: the plane rises off the ground until mid-picture, then the ground drops away.
        var planeX = stage.X + (stage.Width * 0.4);
        var middle = stage.Y + (stage.Height * 0.46);
        var groundMax = stage.Y + (stage.Height * 0.74);
        var lowest = MainWheelDrop(theta);
        var planeY = Math.Max(middle, groundMax - (s.Altitude * SceneScale) - lowest);
        var groundY = planeY + (s.Altitude * SceneScale) + lowest;

        DrawSky(dc, s.Altitude);
        DrawClouds(dc, stage, planeX, groundY, s.X);
        DrawGround(dc, stage, planeX, groundY, s);

        // The stall shakes the plane.
        var shake = forces.Separation > 0.25 && !s.OnGround ? new Vector(Math.Sin(now * 53) * 1.6, Math.Cos(now * 41) * 1.6) : default;
        var cg = new Point(planeX, planeY) + shake;
        _planeScreen = cg;

        if (ShowLabels && !s.OnGround) DrawPathAndAngle(dc, cg, gamma, theta, forces.Alpha, now);
        DrawPlaneSide(dc, cg, theta, forces);
        DrawForceArrows(dc, stage, cg, gamma, theta, forces);
        DrawBalance(dc, stage, forces);
        DrawInstruments(dc, stage, s, forces, cg, now);
    }

    /// <summary>How far below the centre of gravity the main wheels' bottom sits when the plane is pitched by <paramref name="theta"/>, px.</summary>
    private static double MainWheelDrop(double theta)
    {
        var x = 0.2 * PlaneScale;
        var y = 1.17 * PlaneScale;
        return (-x * Math.Sin(theta)) + (y * Math.Cos(theta));
    }

    private void DrawSky(PaintContext dc, double altitude)
    {
        // Deeper blue higher up.
        var t = Math.Clamp(altitude / 4000, 0, 1);
        var top = Lerp(Color.FromRgb(0x2D, 0x6C, 0xC4), Color.FromRgb(0x0B, 0x24, 0x5A), t);
        var bottom = Lerp(Color.FromRgb(0xA9, 0xD3, 0xF2), Color.FromRgb(0x4D, 0x86, 0xC8), t);
        var key = ((int)(t * 40));
        if (_skyKey != key || _sky is null)
        {
            _skyKey = key;
            _sky = SpaceDrawing.LinearGradient(top, bottom, 90);
        }

        dc.DrawRectangle(_sky, null, new Rect(0, 0, ActualWidth, ActualHeight));
    }

    private IBrush? _sky;
    private int _skyKey = -1;

    private static readonly IBrush CloudBrush = Solid(150, 255, 255, 255);

    /// <summary>Clouds at fixed places in the sky (cells of 260 m by 180 m), passing at the scenery's scale.</summary>
    private void DrawClouds(PaintContext dc, Rect stage, double planeX, double groundY, double x)
    {
        var left = x - ((planeX + 200) / SceneScale);
        var right = x + ((ActualWidth - planeX + 200) / SceneScale);
        var top = (groundY + 100) / SceneScale;
        var bottom = Math.Max(0, (groundY - ActualHeight - 100) / SceneScale);
        for (var cx = Math.Floor(left / 260); cx <= Math.Ceiling(right / 260); cx++)
        {
            for (var cz = Math.Max(2, Math.Floor(bottom / 180)); cz <= Math.Min(18, Math.Ceiling(top / 180)); cz++)
            {
                var hash = Hash(cx, cz);
                if (hash % 100 > 55) continue;
                var wx = (cx * 260) + (hash % 170);
                var wz = (cz * 180) + ((hash / 7) % 120);
                var center = new Point(planeX + ((wx - x) * SceneScale), groundY - (wz * SceneScale));
                var size = 26 + ((hash / 13) % 30);
                var group = new GeometryGroup { FillRule = FillRule.NonZero };
                group.Children.Add(SpaceDrawing.Ellipse(center, size * 1.6, size * 0.55));
                group.Children.Add(SpaceDrawing.Ellipse(center + new Vector(-size * 0.6, -size * 0.25), size * 0.7, size * 0.55));
                group.Children.Add(SpaceDrawing.Ellipse(center + new Vector(size * 0.35, -size * 0.45), size * 0.8, size * 0.7));
                group.Children.Add(SpaceDrawing.Ellipse(center + new Vector(size * 1.0, -size * 0.1), size * 0.6, size * 0.45));
                dc.DrawGeometry(CloudBrush, null, group);
            }
        }
    }

    private static int Hash(double a, double b)
    {
        unchecked
        {
            var h = ((int)a * 73856093) ^ ((int)b * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return h & 0x7FFFFFFF;
        }
    }

    private static readonly IBrush GrassBrush = SpaceDrawing.LinearGradient(Color.FromRgb(0x4E, 0x8A, 0x3C), Color.FromRgb(0x24, 0x4A, 0x22), 90);
    private static readonly IBrush HillBrush = Solid(255, 0x5C, 0x86, 0x7E);
    private static readonly IBrush RunwayBrush = Solid(255, 0x4A, 0x4E, 0x55);
    private static readonly IPen RunwayMark = MakePen(Solid(230, 255, 255, 255), 1.6);
    private static readonly IBrush TreeBrush = Solid(255, 0x2C, 0x5E, 0x2E);
    private static readonly IBrush TrunkBrush = Solid(255, 0x5A, 0x40, 0x2C);
    private static readonly IBrush HouseBrush = Solid(255, 0xE6, 0xDC, 0xC8);
    private static readonly IBrush RoofBrush = Solid(255, 0xB0, 0x4A, 0x3A);

    /// <summary>The runway's length, m, from x = 0.</summary>
    internal const double RunwayLength = 900;

    private void DrawGround(PaintContext dc, Rect stage, double planeX, double groundY, FlightState s)
    {
        if (groundY > ActualHeight + 60) return;

        // Far hills, moving slowly behind.
        var hills = new StreamGeometry();
        using (var context = hills.Open())
        {
            context.BeginFigure(new Point(0, groundY), true);
            for (var px = 0.0; px <= ActualWidth + 8; px += 8)
            {
                var wx = (px - planeX) + (s.X * SceneScale * 0.12);
                var h = 22 + (14 * Math.Sin(wx / 97)) + (9 * Math.Sin((wx / 41) + 1.3)) + (5 * Math.Sin(wx / 17));
                context.LineTo(new Point(px, groundY - h));
            }

            context.LineTo(new Point(ActualWidth, groundY));

            context.EndFigure(true);
        }
        dc.DrawGeometry(HillBrush, null, hills);
        dc.DrawRectangle(GrassBrush, null, new Rect(0, groundY, ActualWidth, Math.Max(0, ActualHeight - groundY)));

        var left = s.X - (planeX / SceneScale) - 20;
        var right = s.X + ((ActualWidth - planeX) / SceneScale) + 20;

        // Trees and houses beside the runway, every so often.
        for (var cell = Math.Floor(left / 22); cell <= Math.Ceiling(right / 22); cell++)
        {
            var hash = Hash(cell, 3);
            var wx = (cell * 22) + (hash % 18);
            if (wx > -80 && wx < RunwayLength + 80) continue;
            var x = planeX + ((wx - s.X) * SceneScale);
            switch (hash % 9)
            {
                case 0:
                case 1:
                case 2:
                {
                    var h = (9 + ((hash / 11) % 6)) * SceneScale;
                    dc.DrawRectangle(TrunkBrush, null, new Rect(x - 1.5, groundY - (h * 0.45), 3, h * 0.45));
                    dc.DrawEllipse(TreeBrush, null, new Point(x, groundY - (h * 0.65)), h * 0.26, h * 0.36);
                    break;
                }

                case 3:
                {
                    var w = 9 * SceneScale;
                    var h = 6 * SceneScale;
                    dc.DrawRectangle(HouseBrush, null, new Rect(x - (w / 2), groundY - h, w, h));
                    dc.DrawGeometry(RoofBrush, null, Polygon(new Point(x - (w / 2) - 3, groundY - h), new Point(x, groundY - h - (h * 0.7)), new Point(x + (w / 2) + 3, groundY - h)));
                    break;
                }
            }
        }

        // The runway: a grey strip with its centre line and the number at the start.
        var start = planeX + ((0 - s.X) * SceneScale);
        var end = planeX + ((RunwayLength - s.X) * SceneScale);
        if (end > 0 && start < ActualWidth)
        {
            var strip = new Rect(Math.Max(-10, start), groundY - 1, Math.Min(ActualWidth + 10, end) - Math.Max(-10, start), 7);
            dc.DrawRectangle(RunwayBrush, null, strip);
            for (var m = 30.0; m < RunwayLength - 20; m += 30)
            {
                var x = planeX + ((m - s.X) * SceneScale);
                if (x < -40 || x > ActualWidth + 40) continue;
                dc.DrawLine(RunwayMark, new Point(x, groundY + 2.5), new Point(x + (15 * SceneScale), groundY + 2.5));
            }

            if (s.Altitude < 60 && strip.Width > 60)
            {
                var at = new Point(Math.Clamp(planeX + 190, strip.Left + 50, strip.Right - 50), groundY + 18);
                if (ShowLabels) DrawTargetPill(dc, Loc("Flight_Label_Runway"), at, Ink.Text, "runway");
                else AddBoxTarget("runway", new Rect(strip.Left, strip.Top - 6, strip.Width, 18));
            }
        }
    }

    private static readonly IBrush BodyBrush = SpaceDrawing.LinearGradient(Color.FromRgb(0xF6, 0xF8, 0xFB), Color.FromRgb(0xB9, 0xC3, 0xD0), 90);
    private static readonly IBrush StripeBrush = Solid(255, 0x2E, 0x6F, 0xD8);
    private static readonly IBrush WindowBrush = Solid(255, 0x24, 0x36, 0x52);
    private static readonly IBrush WheelBrush = Solid(255, 0x22, 0x24, 0x28);
    private static readonly IPen Outline = MakePen(Solid(200, 0x2A, 0x33, 0x40), 1.2);

    private static Point M(double x, double y) => new(x * PlaneScale, y * PlaneScale);

    /// <summary>The plane from its left side, flying to the right, pitched by <paramref name="theta"/> about its centre of gravity.</summary>
    private void DrawPlaneSide(PaintContext dc, Point cg, double theta, FlightForces forces)
    {
        dc.PushTransform(new TranslateTransform(cg.X, cg.Y));
        dc.PushTransform(SpaceDrawing.Rotation(-theta * 180 / Math.PI));

        // Landing gear.
        var gear = MakePen(Solid(255, 0x3A, 0x40, 0x4A), 2.4);
        dc.DrawLine(gear, M(0.05, 0.45), M(0.2, 0.95));
        dc.DrawLine(gear, M(3.0, 0.45), M(3.0, 0.95));
        dc.DrawEllipse(WheelBrush, null, M(0.2, 0.95), 0.22 * PlaneScale, 0.22 * PlaneScale);
        dc.DrawEllipse(WheelBrush, null, M(3.0, 0.97), 0.18 * PlaneScale, 0.18 * PlaneScale);

        // Fin and rudder.
        dc.DrawGeometry(BodyBrush, Outline, Polygon(M(-2.5, -0.5), M(-3.55, -1.65), M(-3.95, -1.65), M(-3.9, -0.42)));
        dc.DrawLine(Outline, M(-3.62, -1.6), M(-3.62, -0.45));

        // Fuselage.
        var top = new[] { M(3.95, -0.05), M(3.6, -0.45), M(2.4, -0.62), M(1.7, -1.22), M(0.2, -1.28), M(-0.6, -0.95), M(-3.4, -0.45), M(-3.92, -0.38) };
        var bottom = new[] { M(-3.92, -0.18), M(-3.2, 0.0), M(-0.5, 0.52), M(1.2, 0.6), M(3.0, 0.5), M(3.7, 0.25), M(3.95, 0.05) };
        var body = new StreamGeometry();
        using (var context = body.Open())
        {
            context.BeginFigure(top[0], true);
            var curve = Smooth(top.Skip(1).Concat(bottom).ToArray(), top[0]);
            for (var i = 0; i + 2 < curve.Count; i += 3)
            {
                context.CubicBezierTo(curve[i], curve[i + 1], curve[i + 2]);
            }

            context.EndFigure(true);
        }
        dc.DrawGeometry(BodyBrush, Outline, body);
        dc.DrawGeometry(StripeBrush, null, Polygon(M(3.6, 0.1), M(-3.4, -0.22), M(-3.5, -0.12), M(3.6, 0.22)));
        dc.DrawGeometry(WindowBrush, null, Polygon(M(2.25, -0.66), M(1.72, -1.15), M(0.35, -1.18), M(0.3, -0.7)));

        // Horizontal tail with the elevator, tilted up when the pilot pulls back for more angle of attack.
        var deflection = Math.Clamp(-(PlaneAlpha - 2) * 1.6, -22, 18) * Math.PI / 180;
        var hinge = M(-3.5, -0.32);
        dc.DrawGeometry(BodyBrush, Outline, Polygon(M(-2.9, -0.38), hinge, M(-3.5, -0.24), M(-2.9, -0.28)));
        var tip = hinge + new Vector(-0.5 * PlaneScale * Math.Cos(deflection), 0.5 * PlaneScale * Math.Sin(deflection));
        dc.DrawLine(MakePen(Solid(255, 0x7C, 0xFF, 0x9E), 3.2), hinge, tip);

        // The wing, seen end on: a low wing under the cabin.
        dc.DrawGeometry(BodyBrush, Outline, WingProfile());

        // The propeller: a blur when it turns fast, one blade otherwise.
        var spin = _propeller;
        var blade = Math.Cos(spin) * 1.0;
        if (Throttle > 0.05) dc.DrawEllipse(Solid(50, 255, 255, 255), null, M(4.02, 0), 0.08 * PlaneScale, 1.0 * PlaneScale);
        dc.DrawLine(MakePen(Solid(230, 0x30, 0x34, 0x3A), 3), M(4.02, -blade), M(4.02, blade));
        dc.DrawEllipse(Solid(255, 0xD0, 0x45, 0x40), Outline, M(4.0, 0), 0.16 * PlaneScale, 0.16 * PlaneScale);

        // Torn air over the wing past the stall.
        if (forces.Separation > 0.25)
        {
            var wisp = FadedPen(Colors.White, 0.6 * forces.Separation, 1.4);
            for (var i = 0; i < 6; i++)
            {
                var x = 0.6 - (i * 0.45);
                var wobble = Math.Sin((_time * 9) + (i * 1.7)) * 0.18;
                dc.DrawLine(wisp, M(x, -0.2 + wobble - (i * 0.08)), M(x - 0.55, -0.45 - wobble - (i * 0.1)));
            }
        }

        dc.Pop();
        dc.Pop();

        // The plane, its propeller.
        var nose = Rotate(M(4.0, 0), theta) + (Vector)cg;
        _targets.Add("plane", cg + Rotate(new Vector(0.2 * PlaneScale, -0.2 * PlaneScale), theta), 0.9 * PlaneScale);
        _targets.Add("propeller", nose, 14);
    }

    private static Point Rotate(Point p, double theta) => (Point)Rotate((Vector)p, theta);

    /// <summary>A local drawing vector (y down) turned nose-up by <paramref name="theta"/>.</summary>
    private static Vector Rotate(Vector v, double theta) => new((v.X * Math.Cos(theta)) + (v.Y * Math.Sin(theta)), (-v.X * Math.Sin(theta)) + (v.Y * Math.Cos(theta)));

    private static Geometry WingProfile()
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(M(1.15, 0.42), true);
            context.CubicBezierTo(M(1.0, 0.22), M(0.4, 0.2), M(-0.55, 0.38));
            context.CubicBezierTo(M(0.2, 0.5), M(0.9, 0.56), M(1.15, 0.42));

            context.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>Control points for a smooth closed curve through <paramref name="points"/> (Catmull-Rom as cubic Béziers).</summary>
    private static List<Point> Smooth(Point[] points, Point first)
    {
        var all = new List<Point> { first };
        all.AddRange(points);
        all.Add(first);
        var result = new List<Point>();
        for (var i = 0; i < all.Count - 1; i++)
        {
            var p0 = all[Math.Max(0, i - 1)];
            var p1 = all[i];
            var p2 = all[i + 1];
            var p3 = all[Math.Min(all.Count - 1, i + 2)];
            result.Add(p1 + ((p2 - p0) / 6));
            result.Add(p2 - ((p3 - p1) / 6));
            result.Add(p2);
        }

        return result;
    }

    private void DrawPathAndAngle(PaintContext dc, Point cg, double gamma, double theta, double alphaDegrees, double now)
    {
        var path = new Vector(Math.Cos(gamma), -Math.Sin(gamma));
        var ahead = cg + (path * 210);
        if (Glows("path")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 6), Line([cg + (path * 110), ahead]));
        dc.DrawLine(FadedPen(Ink.Gold, 0.85, 1.4, dashed: true), cg + (path * 110), ahead);
        DrawArrowHead(dc, FadedBrush(Ink.Gold, 0.85), ahead, path, 8);
        DrawTargetPill(dc, Loc("Flight_Label_Path"), ahead + new Vector(0, 18), Ink.Gold, "path");

        // The horizon through the plane, for the climb angle.
        dc.DrawLine(FadedPen(Colors.White, 0.35, 1, dashed: true), cg + new Vector(100, 0), cg + new Vector(230, 0));

        if (Math.Abs(alphaDegrees) >= 1)
        {
            var body = new Vector(Math.Cos(theta), -Math.Sin(theta));
            var r = 125.0;
            var arc = new StreamGeometry();
            using (var context = arc.Open())
            {
                context.BeginFigure(cg + (path * r), false);
                context.ArcTo(cg + (body * r), new Size(r, r), 0, false, alphaDegrees > 0 ? SweepDirection.CounterClockwise : SweepDirection.Clockwise);

                context.EndFigure(false);
            }
            dc.DrawGeometry(null, FadedPen(Ink.Gold, 1, 1.6), arc);
            dc.DrawLine(FadedPen(Colors.White, 0.4, 1, dashed: true), cg + (body * 100), cg + (body * 150));
        }

        DrawTargetPill(dc, LocFormat("Flight_Label_Aoa", Number(alphaDegrees, "0.#")), cg + (path * 125) + new Vector(0, 30), Ink.Gold, "planeaoa");
    }

    private void DrawForceArrows(PaintContext dc, Rect stage, Point cg, double gamma, double theta, FlightForces f)
    {
        var unit = Math.Min(115, stage.Height * 0.22) / f.Weight;
        var velocity = new Vector(Math.Cos(gamma), -Math.Sin(gamma));
        var liftDir = new Vector(-Math.Sin(gamma), -Math.Cos(gamma));
        var body = new Vector(Math.Cos(theta), -Math.Sin(theta));

        var liftTip = cg + (liftDir * f.Lift * unit);
        var weightTip = cg + new Vector(0, f.Weight * unit);
        var thrustTip = cg + (body * Math.Min(f.Thrust * unit * ThrustDragScale, 2.4 * f.Weight * unit));
        var dragTip = cg - (velocity * Math.Min(f.Drag * unit * ThrustDragScale, 2.4 * f.Weight * unit));

        DrawArrow(dc, cg, weightTip, Ink.Orange, 3.4);
        if (f.Lift * unit > 3) DrawArrow(dc, cg, liftTip, Ink.Cyan, 3.4);
        if (f.Thrust * unit * ThrustDragScale > 3) DrawArrow(dc, cg, thrustTip, Ink.Green, 3.4);
        if (f.Drag * unit * ThrustDragScale > 3) DrawArrow(dc, cg, dragTip, Ink.Red, 3.4);
        dc.DrawEllipse(FadedBrush(Colors.White, 1), FadedPen(Colors.Black, 0.6, 1), cg, 3.5, 3.5);

        if (f.Lift * unit > 24) DrawTargetPill(dc, LocFormat("Flight_Label_Lift", Newtons(f.Lift)), liftTip + (liftDir * 16), Ink.Cyan, "lift");
        DrawTargetPill(dc, LocFormat("Flight_Label_Weight", Newtons(f.Weight)), weightTip + new Vector(0, 16), Ink.Orange, "weight");
        DrawTargetPill(dc, LocFormat("Flight_Label_Thrust", Newtons(f.Thrust)), thrustTip + (body * 18) + new Vector(26, 18), Ink.Green, "thrust");
        if (f.Drag * unit * ThrustDragScale > 24) DrawTargetPill(dc, LocFormat("Flight_Label_Drag", Newtons(f.Drag)), dragTip - (velocity * 16) + new Vector(-34, -16), Ink.Red, "drag");
        _targets.Add("lift", liftTip, 9);
        _targets.Add("weight", weightTip, 9);
        _targets.Add("thrust", thrustTip, 9);
        _targets.Add("drag", dragTip, 9);
    }

    /// <summary>A panel weighing lift against weight and thrust against drag, with the throttle lever.</summary>
    private void DrawBalance(PaintContext dc, Rect stage, FlightForces f)
    {
        var box = new Rect(stage.X + 18, stage.Bottom - 136, 292, 124);
        _balanceBox = box;
        DrawFrame(dc, box);
        DrawText(dc, Loc("Flight_Balance_Title"), new Point(box.X + 14, box.Y + 14), Ink.Dim, 11, bold: true);

        var barLeft = box.X + 74;
        var barMax = box.Width - 74 - 60;
        var pairOne = Math.Max(f.Lift, f.Weight) * 1.05;
        var pairTwo = Math.Max(Math.Max(f.Thrust, f.Drag), 0.15 * f.Weight) * 1.05;
        var rows = new (string Key, double Value, double Scale, Color Colour)[]
        {
            ("Flight_Balance_Lift", f.Lift, pairOne, Ink.Cyan),
            ("Flight_Balance_Weight", f.Weight, pairOne, Ink.Orange),
            ("Flight_Balance_Thrust", f.Thrust, pairTwo, Ink.Green),
            ("Flight_Balance_Drag", f.Drag, pairTwo, Ink.Red),
        };
        for (var i = 0; i < rows.Length; i++)
        {
            var (key, value, scale, colour) = rows[i];
            var y = box.Y + 36 + (i * 17) + (i >= 2 ? 6 : 0);
            DrawText(dc, Loc(key), new Point(box.X + 14, y), Ink.Text, 11);
            var length = Math.Max(1, barMax * value / scale);
            dc.DrawRoundedRectangle(FadedBrush(colour, 0.9), null, new Rect(barLeft, y - 5, length, 10), 3, 3);
        }

        // What the balance means.
        var verdict = Loc(Verdict(f));
        DrawText(dc, verdict, new Point(box.X + 14, box.Bottom - 14), Ink.Gold, 11);
        AddBoxTarget("balance", new Rect(box.X, box.Y, box.Width - 50, box.Height));

        // The throttle lever.
        var slot = ThrottleSlot();
        dc.DrawRoundedRectangle(Solid(255, 0x10, 0x14, 0x1E), Ink.Frame, new Rect(slot.X + 8, slot.Y, 8, slot.Height), 4, 4);
        var knobY = slot.Bottom - 8 - (Throttle * (slot.Height - 16));
        dc.DrawRoundedRectangle(Solid(255, 0x2B, 0x2F, 0x38), MakePen(Solid(255, Ink.Green.R, Ink.Green.G, Ink.Green.B), 1.6), new Rect(slot.X, knobY - 6, slot.Width, 12), 4, 4);
        DrawText(dc, Number(Throttle * 100, "0") + "%", new Point(slot.X + (slot.Width / 2), box.Y + 14), Ink.Green, 11, TextAnchor.Center, bold: true);
        AddBoxTarget("throttle", slot);
    }

    /// <summary>The balance in words: level, climbing, descending, gliding, on the runway, stalled.</summary>
    internal static string Verdict(FlightForces f) =>
        f.Separation > 0.25 ? "Flight_Verdict_Stall"
        : f.Lift < 0.5 * f.Weight ? (f.Thrust > f.Drag ? "Flight_Verdict_Rolling" : "Flight_Verdict_Slowing")
        : Math.Abs(f.Lift - f.Weight) < 0.02 * f.Weight && Math.Abs(f.Thrust - f.Drag) < 0.03 * f.Weight ? "Flight_Verdict_Balanced"
        : f.Thrust > f.Drag ? "Flight_Verdict_MoreThrust"
        : "Flight_Verdict_LessThrust";

    private void DrawInstruments(PaintContext dc, Rect stage, FlightState s, FlightForces f, Point cg, double now)
    {
        var x = stage.Right - 20;
        var y = stage.Y + 14;
        var climb = s.Speed * Math.Sin(s.PathAngle);
        var lines = new (string Id, string Text, Color Colour)[]
        {
            ("airspeed", LocFormat("Flight_Instrument_Speed", Kmh(s.Speed)), Ink.Text),
            ("altimeter", LocFormat("Flight_Instrument_Altitude", Metres(s.Altitude)), Ink.Text),
            ("vario", LocFormat("Flight_Instrument_Climb", Number(climb, "+0.0;-0.0;0.0")), climb > 0.3 ? Ink.Green : climb < -0.3 ? Ink.Orange : Ink.Text),
        };
        foreach (var (id, text, colour) in lines)
        {
            var width = _labels.Text(text, 12, colour).Width;
            DrawTargetPill(dc, text, new Point(x - (width / 2) - 6, y), colour, id, 12);
            y += 28;
        }

        if (f.Separation > 0.25 && !s.OnGround)
        {
            var flash = 0.6 + (0.4 * Math.Sin(now * 9));
            var warn = Loc("Flight_Label_Stall");
            var warnWidth = _labels.Text(warn, 13, Ink.Red).Width;
            DrawTargetPill(dc, warn, new Point(x - (warnWidth / 2) - 6, y + 4), Color.FromArgb(255, Ink.Red.R, (byte)(Ink.Red.G * flash), (byte)(Ink.Red.B * flash)), "stallwarn", 13);
        }

        if (_wheelsEvent is { } wheels && _time - wheels.Time < 3)
        {
            var text = wheels.LiftOff ? Loc("Flight_Label_LiftOff")
                : wheels.Sink > 3 ? LocFormat("Flight_Label_HardLanding", Number(wheels.Sink, "0.0"))
                : Loc("Flight_Label_Touchdown");
            DrawPill(dc, text, cg + new Vector(0, -120), wheels.Sink > 3 ? Ink.Orange : Ink.Green, 13);
        }
    }
}
