using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Waves;

public sealed partial class WavesDiagram
{
    /// <summary>The tank is at least this wide and tall, cm.</summary>
    private const double TankWidth = 40;
    private const double TankHeight = 24;

    /// <summary>Deep water in a trough, calm water, light glancing off a crest.</summary>
    private static readonly uint[] WaterPalette = WaveField.Palette(512,
        (-1, Color.FromRgb(0x02, 0x10, 0x1E)),
        (-0.4, Color.FromRgb(0x08, 0x31, 0x50)),
        (0, Color.FromRgb(0x0E, 0x4A, 0x70)),
        (0.45, Color.FromRgb(0x2A, 0x84, 0xB0)),
        (0.8, Color.FromRgb(0x8C, 0xDA, 0xF4)),
        (1, Color.FromRgb(0xEC, 0xFB, 0xFF)));

    /// <summary>How far the water rises and falls: still (dark) to heaving (bright).</summary>
    private static readonly uint[] SwingPalette = WaveField.Palette(512,
        (-1, Color.FromRgb(0x01, 0x05, 0x0B)),
        (-0.55, Color.FromRgb(0x07, 0x22, 0x3A)),
        (0, Color.FromRgb(0x10, 0x5C, 0x84)),
        (0.55, Color.FromRgb(0x4E, 0xC0, 0xE8)),
        (1, Color.FromRgb(0xF4, 0xFD, 0xFF)));

    /// <summary>Where the tank is and how it maps to centimetres: (0, 0) halfway between the dippers, y down.</summary>
    private readonly record struct RippleGeometry(Rect Tank, Point Origin, double Scale)
    {
        public Point ToScreen(Point cm) => new(Origin.X + (cm.X * Scale), Origin.Y + (cm.Y * Scale));

        public Point ToWorld(Point screen) => new((screen.X - Origin.X) / Scale, (screen.Y - Origin.Y) / Scale);
    }

    private RippleGeometry RippleLayout()
    {
        var tank = Stage();
        var scale = Math.Min(tank.Width / TankWidth, tank.Height / TankHeight);
        return new RippleGeometry(tank, new Point(tank.X + (tank.Width / 2), tank.Y + (tank.Height / 2)), scale);
    }

    /// <summary>
    /// The tank from above: the water painted from both dippers' waves, the lines of still water and of the biggest waves
    /// if asked for, the probe with its distance to each dipper, and the dippers bobbing.
    /// </summary>
    private void DrawRipple(PaintContext dc)
    {
        var layout = RippleLayout();
        var tank = layout.Tank;
        var wavelength = Wavelength;
        var separation = Separation;
        var second = SecondSource;
        var phaseB = Opposite ? Math.PI : 0;

        var emitters = new List<Emitter>(2) { new(EmitterKind.Point, WavePhysics.SourceA(separation), 0, _startA) };
        if (second) emitters.Add(new Emitter(EmitterKind.Point, WavePhysics.SourceB(separation), phaseB, _startB));

        var clip = new RectangleGeometry(tank, 14, 14);
        dc.PushClip(clip);
        _field.Render(tank, layout.Origin, layout.Scale, wavelength, WavePhysics.RippleSpeed, _time, emitters, WaterPalette,
            ShowAverage ? 1.2 : 1.35, ShowAverage, SwingPalette);
        _field.Draw(dc);

        // The scale bar and the caption in the bottom corners; line labels keep clear of them.
        var caption = _labels.Get(ShowAverage ? "Waves_Diagram_TankSwing" : "Waves_Diagram_Tank", 12, Ink.Dim);
        var captionAt = new Point(tank.Right - caption.Width - 14, tank.Bottom - caption.Height - 10);
        var scaleLength = 5 * layout.Scale;
        Rect[] keepOut =
        [
            new(tank.Left, tank.Bottom - 44, scaleLength + 40, 44),
            new(captionAt.X - 12, captionAt.Y - 8, caption.Width + 26, caption.Height + 18),
        ];

        if (ShowLines && second) DrawRippleLines(dc, layout, wavelength, separation, keepOut);

        var a = layout.ToScreen(WavePhysics.SourceA(separation));
        var b = layout.ToScreen(WavePhysics.SourceB(separation));
        var probe = layout.ToScreen(Probe);
        if (Glows("paths"))
        {
            var glow = ExploreTargets.GlowPen(_now, 5);
            dc.DrawLine(glow, a, probe);
            if (second) dc.DrawLine(glow, b, probe);
        }

        dc.DrawLine(Ink.PathA, a, probe);
        if (second) dc.DrawLine(Ink.PathB, b, probe);

        var omega = 2 * Math.PI * WavePhysics.RippleFrequency(wavelength);
        DrawDipper(dc, a, "A", Ink.CyanBrush, Math.Sin(omega * _time), true);
        DrawDipper(dc, b, "B", Ink.PinkBrush, Math.Sin((omega * _time) + phaseB), second);
        _targets.Add("dippera", a, 12);
        _targets.Add("dipperb", b, 12);

        var difference = WavePhysics.PathDifference(Probe, separation) / wavelength;
        var meeting = second ? WavePhysics.MeetingOf(WavePhysics.PhaseGap(difference * wavelength, wavelength, Opposite)) : Meeting.Between;
        DrawProbeMark(dc, probe, !second ? Ink.CyanBrush : meeting switch
        {
            Meeting.InStep => Ink.GoldBrush,
            Meeting.OutOfStep => Ink.WhiteBrush,
            _ => Ink.CyanBrush,
        });
        _targets.Add("probe", probe, 12);
        dc.Pop();

        dc.DrawRoundedRectangle(null, Ink.Frame, tank, 14, 14);

        if (second)
        {
            var text = LocFormat("Waves_Diagram_Difference", Number(difference, "0.00"));
            DrawPillInside(dc, text, new Point(probe.X + 14, probe.Y - 30), tank, 12, meeting == Meeting.InStep ? Ink.Gold : Ink.Text,
                "difference");
        }

        DrawScaleBar(dc, new Point(tank.Left + 18, tank.Bottom - 16), scaleLength, LocFormat("Waves_Diagram_Centimetres", 5));
        dc.DrawText(caption, captionAt);
        AddTextTarget("tank", caption, captionAt);
    }

    /// <summary>A dipper: a ball bobbing up and down (bigger when up), with its name; dim when off.</summary>
    private void DrawDipper(PaintContext dc, Point at, string name, IBrush brush, double lift, bool on)
    {
        if (on)
        {
            var radius = 6 + (1.8 * lift);
            dc.DrawEllipse(Ink.WhiteBrush, Ink.DipperRim, at, radius, radius);
            dc.DrawEllipse(brush, null, at, 2.6, 2.6);
        }
        else
        {
            dc.DrawEllipse(null, Ink.Frame, at, 6, 6);
        }

        var label = _labels.Text(on ? name : LocFormat("Waves_Diagram_Off", name), 12, on ? Ink.Text : Ink.Dim, true);
        _labels.DrawTextPill(dc, on ? name : LocFormat("Waves_Diagram_Off", name), new Point(at.X - (label.Width / 2) - 6, at.Y + 12), 12,
            on ? Ink.Text : Ink.Dim);
    }

    /// <summary>
    /// The lines of still water (dashed, labelled at the top with how much farther A is) and of the biggest waves (gold,
    /// labelled at the bottom): hyperbolas with the two dippers as their foci.
    /// </summary>
    private void DrawRippleLines(PaintContext dc, RippleGeometry layout, double wavelength, double separation, IReadOnlyList<Rect> keepOut)
    {
        var tank = layout.Tank;
        var yMax = (tank.Height / 2 / layout.Scale) + 1;

        foreach (var delta in WavePhysics.LoudDifferences(separation, wavelength, Opposite))
        {
            var points = WavePhysics.Hyperbola(delta, separation, yMax).Select(layout.ToScreen).ToList();
            var line = Polyline(points);
            if (Glows("loud")) dc.DrawGeometry(null, ExploreTargets.GlowPen(_now), line);
            dc.DrawGeometry(null, Ink.LoudLine, line);
            LabelLine(dc, points.AsEnumerable().Reverse(), tank, keepOut, Halves(delta / wavelength), Ink.Gold, fromTop: false, "loud");
        }

        foreach (var delta in WavePhysics.QuietDifferences(separation, wavelength, Opposite))
        {
            var points = WavePhysics.Hyperbola(delta, separation, yMax).Select(layout.ToScreen).ToList();
            var line = Polyline(points);
            if (Glows("quiet")) dc.DrawGeometry(null, ExploreTargets.GlowPen(_now), line);
            dc.DrawGeometry(null, Ink.QuietLine, line);
            LabelLine(dc, points, tank, keepOut, Halves(delta / wavelength), Ink.Text, fromTop: true, "quiet");
        }
    }

    /// <summary>
    /// A small label where a line comes into the tank, nudged in from the edge and clear of <paramref name="keepOut"/>;
    /// a target named <paramref name="id"/>.
    /// </summary>
    private void LabelLine(PaintContext dc, IEnumerable<Point> points, Rect tank, IReadOnlyList<Rect> keepOut, string text, Color color,
        bool fromTop, string id)
    {
        var inner = new Rect(tank.Left + 10, tank.Top + 10, tank.Width - 20, tank.Height - 20);
        var formatted = _labels.Text(text, 11, color, true);
        foreach (var point in points)
        {
            if (!inner.Contains(point)) continue;
            var topLeft = new Point(point.X - (formatted.Width / 2) - 6, fromTop ? point.Y + 2 : point.Y - formatted.Height - 6);
            var pill = new Rect(topLeft, new Size(formatted.Width + 12, formatted.Height + 4));
            if (keepOut.Any(r => r.Intersects(pill))) continue;
            _labels.DrawTextPill(dc, text, topLeft, 11, color);
            _targets.AddBox(id, pill);
            return;
        }
    }

    private static StreamGeometry Polyline(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            if (points.Count > 0)
            {
                context.BeginFigure(points[0], false);
                for (var i = 1; i < points.Count; i++) context.LineTo(points[i]);
            }

            context.EndFigure(false);
        }
        return geometry;
    }
}
