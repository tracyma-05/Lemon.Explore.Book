using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Vector3D = Lemon.Explore.Book.Controls.Space.Vector3D;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Oscillations;

public sealed partial class OscillatorDiagram
{
    /// <summary>Spacing of the pendulums along the bar, metres.</summary>
    private const double WaveSpacing = 0.065;

    /// <summary>How far below the bar the base of the frame is, metres.</summary>
    private const double WaveBase = 0.45;

    private const double WaveBobRadius = 0.018;

    /// <summary>Half the length of the bar, metres: a little past the end pendulums.</summary>
    private const double RigHalfLength = ((OscillatorPhysics.WaveCount - 1) / 2.0 * WaveSpacing) + 0.08;

    /// <summary>Where the camera looks at the rig from (front, right and above) and what it looks at.</summary>
    private static readonly Point3D WaveCamera = new(1.5, 0.75, 2.3);
    private static readonly Point3D WaveTarget = new(0, -0.24, 0);

    /// <summary>
    /// Fifteen pendulums on one bar, the longest on the left, each swinging toward and away from you, seen from the front
    /// and to one side; below, the same seen from straight above, where the snake and the rows are easiest to see.
    /// </summary>
    private void DrawWave(PaintContext dc, double now)
    {
        var width = ActualWidth;
        var height = PictureHeight;
        var gravity = Gravity;
        var aboveHeight = Math.Clamp((height * 0.2) - 20, 100, 150);
        var top = new Rect(40, 170, Math.Max(100, width - 80), Math.Max(100, height - 96 - aboveHeight - 40 - 170));
        var above = new Rect(70, top.Bottom + 22, Math.Max(100, width - 140), aboveHeight);

        var camera = new WaveView(WaveCamera, WaveTarget);
        camera.Fit(RigCorners(), top);

        var angles = new double[OscillatorPhysics.WaveCount];
        for (var i = 0; i < angles.Length; i++) angles[i] = OscillatorPhysics.WaveAngle(i, _waveTime, gravity);

        DrawRig(dc, camera);

        // Pendulums far to near, so the near ones are drawn over the far ones.
        var order = Enumerable.Range(0, angles.Length)
            .Select(i => (Index: i, Bob: Bob(i, angles[i])))
            .OrderByDescending(p => camera.Depth(p.Bob))
            .ToList();

        foreach (var (_, bob) in order)
        {
            var shadow = camera.Project(new Point3D(bob.X, -WaveBase, bob.Z));
            dc.DrawEllipse(WaveShadow, null, shadow, camera.Size(WaveBobRadius * 1.3, bob), camera.Size(WaveBobRadius * 0.55, bob));
        }

        foreach (var (index, bob) in order)
        {
            var hang = camera.Project(new Point3D(bob.X, 0, 0));
            var at = camera.Project(bob);
            dc.DrawLine(WaveString, hang, at);
            DrawBall(dc, at, Math.Max(3, camera.Size(WaveBobRadius, bob)), WaveColour(index));
            if (index == 0) _targets.Add("longest", at, Math.Max(8, camera.Size(WaveBobRadius, bob) + 3));
            if (index == angles.Length - 1) _targets.Add("shortest", at, Math.Max(8, camera.Size(WaveBobRadius, bob) + 3));
        }

        var badge = _labels.Text(LocFormat("Osc_Diagram_PlaceBadge", Loc($"Osc_Place_{Place}"), Number(gravity, "0.00")), 12, Ink.Text);
        DrawPlaceBadge(dc, new Point(top.Right - (badge.Width / 2) - 10, top.Top + 4));

        DrawTopView(dc, above, angles, now);
    }

    /// <summary>The bob of pendulum <paramref name="index"/> at <paramref name="angle"/>, in metres: x along the bar, z toward you.</summary>
    private static Point3D Bob(int index, double angle)
    {
        var length = OscillatorPhysics.WaveLength(index);
        return new Point3D(WaveX(index), -length * Math.Cos(angle), length * Math.Sin(angle));
    }

    private static double WaveX(int index) => (index - ((OscillatorPhysics.WaveCount - 1) / 2.0)) * WaveSpacing;

    private static IEnumerable<Point3D> RigCorners()
    {
        const double x = RigHalfLength;
        const double z = 0.22;
        foreach (var sx in new[] { -x, x })
        {
            yield return new Point3D(sx, 0.03, 0);
            foreach (var sz in new[] { -z, z }) yield return new Point3D(sx, -WaveBase, sz);
        }
    }

    /// <summary>The frame: a base plate, two posts and the bar the pendulums hang from.</summary>
    private static void DrawRig(PaintContext dc, WaveView camera)
    {
        const double x = RigHalfLength;
        const double z = 0.22;
        var plate = Polygon(
            camera.Project(new Point3D(-x, -WaveBase, -z)),
            camera.Project(new Point3D(x, -WaveBase, -z)),
            camera.Project(new Point3D(x, -WaveBase, z)),
            camera.Project(new Point3D(-x, -WaveBase, z)));
        dc.DrawGeometry(WavePlate, WavePlateEdge, plate);

        foreach (var sx in new[] { -x, x })
        {
            dc.DrawLine(WavePost, camera.Project(new Point3D(sx, -WaveBase, 0)), camera.Project(new Point3D(sx, 0.02, 0)));
        }

        dc.DrawLine(WaveBar, camera.Project(new Point3D(-x - 0.02, 0.02, 0)), camera.Project(new Point3D(x + 0.02, 0.02, 0)));
    }

    /// <summary>The bobs seen from straight above: along the bar across, toward you down, joined into the snake.</summary>
    private void DrawTopView(PaintContext dc, Rect panel, double[] angles, double now)
    {
        dc.DrawRoundedRectangle(Ink.Paper, Ink.PaperEdge, panel, 8, 8);
        _targets.AddBox("topview", panel);
        var title = _labels.Get("Osc_Diagram_TopView", 11, Ink.Dim);
        dc.DrawText(title, new Point(panel.Left + 10, panel.Top + 6));
        var bar = panel.Top + (panel.Height / 2) + 6;
        var reach = OscillatorPhysics.WaveLength(0) * Math.Sin(OscillatorPhysics.WaveAmplitude);
        var scale = ((panel.Height / 2) - 18) / reach;
        dc.DrawLine(Ink.PaperCentre, new Point(panel.Left + 14, bar), new Point(panel.Right - 14, bar));

        var points = new Point[angles.Length];
        for (var i = 0; i < angles.Length; i++)
        {
            var x = panel.Left + 40 + (i * (panel.Width - 80) / (angles.Length - 1));
            points[i] = new Point(x, bar + (OscillatorPhysics.WaveLength(i) * Math.Sin(angles[i]) * scale));
        }

        var snake = new StreamGeometry();
        using (var context = snake.Open())
        {
            context.BeginFigure(points[0], false);
            foreach (var next in points.Skip(1).ToList()) context.LineTo(next);

            context.EndFigure(false);
        }
        if (Glows("snake")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now), snake);
        dc.DrawGeometry(null, WaveSnake, snake);
        for (var i = 0; i < points.Length; i++) DrawBall(dc, points[i], 7, WaveColour(i));

        var near = _labels.Get("Osc_Diagram_TowardYou", 10.5, Ink.Dim);
        dc.DrawText(near, new Point(panel.Right - near.Width - 10, panel.Bottom - near.Height - 4));
    }

    /// <summary>A colour for each pendulum, blue on the long left end through green to orange on the short right end.</summary>
    internal static Color WaveColour(int index)
    {
        var t = index / (double)(OscillatorPhysics.WaveCount - 1);
        return t < 0.5
            ? Lerp(Color.FromRgb(0x5F, 0xA8, 0xFF), Color.FromRgb(0x6B, 0xE0, 0x9A), t * 2)
            : Lerp(Color.FromRgb(0x6B, 0xE0, 0x9A), Color.FromRgb(0xFF, 0xA0, 0x4C), (t - 0.5) * 2);
    }

    private static readonly IPen WaveString = MakePen(Solid(170, 0xC9, 0xD1, 0xDC), 1);
    private static readonly IPen WaveBar = MakePen(Solid(255, 0x8A, 0x96, 0xA8), 5);
    private static readonly IPen WavePost = MakePen(Solid(255, 0x5A, 0x66, 0x78), 4);
    private static readonly IPen WavePlateEdge = MakePen(Solid(70, 255, 255, 255), 1);
    private static readonly IBrush WavePlate = Solid(40, 0x9A, 0xA5, 0xB5);
    private static readonly IBrush WaveShadow = Solid(70, 0, 0, 0);
    private static readonly IPen WaveSnake = MakePen(Solid(90, 255, 255, 255), 1.5);

    /// <summary>A simple perspective camera, fitted so a set of points fills a rectangle on screen.</summary>
    private sealed class WaveView
    {
        private readonly Point3D _eye;
        private readonly Vector3D _right;
        private readonly Vector3D _up;
        private readonly Vector3D _forward;
        private double _scale = 1;
        private Vector _offset;

        public WaveView(Point3D eye, Point3D target)
        {
            _eye = eye;
            _forward = target - eye;
            _forward = _forward.Normalize();
            _right = Vector3D.CrossProduct(_forward, new Vector3D(0, 1, 0));
            _right = _right.Normalize();
            _up = Vector3D.CrossProduct(_right, _forward);
        }

        public double Depth(Point3D point) => Vector3D.DotProduct(point - _eye, _forward);

        public Point Project(Point3D point)
        {
            var raw = Raw(point);
            return new Point((raw.X * _scale) + _offset.X, (raw.Y * _scale) + _offset.Y);
        }

        /// <summary>The size on screen of something <paramref name="size"/> metres across at <paramref name="point"/>.</summary>
        public double Size(double size, Point3D point) => size / Depth(point) * _scale;

        public void Fit(IEnumerable<Point3D> points, Rect area)
        {
            var raws = points.Select(Raw).ToList();
            var left = raws.Min(p => p.X);
            var right = raws.Max(p => p.X);
            var top = raws.Min(p => p.Y);
            var bottom = raws.Max(p => p.Y);
            _scale = Math.Min(area.Width / (right - left), area.Height / (bottom - top));
            _offset = new Vector(area.Left + ((area.Width - ((right - left) * _scale)) / 2) - (left * _scale),
                area.Top + ((area.Height - ((bottom - top) * _scale)) / 2) - (top * _scale));
        }

        private Point Raw(Point3D point)
        {
            var d = point - _eye;
            var depth = Vector3D.DotProduct(d, _forward);
            return new Point(Vector3D.DotProduct(d, _right) / depth, -Vector3D.DotProduct(d, _up) / depth);
        }
    }
}
