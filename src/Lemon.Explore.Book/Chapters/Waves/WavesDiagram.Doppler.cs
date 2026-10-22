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
    /// <summary>The scene is at least this wide and tall, metres.</summary>
    private const double SceneWidth = 340;
    private const double SceneHeight = 220;

    /// <summary>Half the road's width, metres.</summary>
    private const double RoadHalfWidth = 7;

    /// <summary>Ring pens from faint (old, far) to bright (new): picked by age so nothing is made per frame.</summary>
    private static readonly IPen[] RingPens = Enumerable.Range(0, 16)
        .Select(i => MakePen(Solid((byte)(40 + (i * 12)), 0xB8, 0xE6, 0xFF), 1.3))
        .ToArray();

    /// <summary>The scene and how it maps to metres: (0, 0) where the source is halfway through its pass, y down.</summary>
    private readonly record struct DopplerGeometry(Rect Scene, Point Origin, double Scale)
    {
        public Point ToScreen(Point metres) => new(Origin.X + (metres.X * Scale), Origin.Y + (metres.Y * Scale));

        public Point ToWorld(Point screen) => new((screen.X - Origin.X) / Scale, (screen.Y - Origin.Y) / Scale);
    }

    private DopplerGeometry DopplerLayout()
    {
        var scene = Stage();
        var scale = Math.Min(scene.Width / SceneWidth, scene.Height / SceneHeight);
        return new DopplerGeometry(scene, new Point(scene.X + (scene.Width / 2), scene.Y + (scene.Height * 0.4)), scale);
    }

    /// <summary>
    /// The road from above: a ring for every 22nd wave the source sends out, each centred where the source was when it
    /// went out; the cone they pile into faster than sound; the listener, flashing as each ring reaches them, with what
    /// they hear.
    /// </summary>
    private void DrawDoppler(PaintContext dc)
    {
        var layout = DopplerLayout();
        var scene = layout.Scene;
        var mach = Mach;
        var t = _time;
        var c = WavePhysics.SpeedOfSound;

        dc.PushClip(new RectangleGeometry(scene, 14, 14));
        dc.DrawRectangle(Ink.Ground, null, scene);
        var road = new Rect(new Point(scene.Left, layout.ToScreen(new Point(0, -RoadHalfWidth)).Y),
            new Point(scene.Right, layout.ToScreen(new Point(0, RoadHalfWidth)).Y));
        dc.DrawRectangle(Ink.Road, null, road);
        dc.DrawLine(Ink.RoadEdge, road.TopLeft, road.TopRight);
        dc.DrawLine(Ink.RoadEdge, road.BottomLeft, road.BottomRight);
        dc.DrawLine(Ink.RoadMarking, new Point(scene.Left, layout.Origin.Y), new Point(scene.Right, layout.Origin.Y));

        // Rings, oldest (biggest, faintest) first.
        var farthest = Math.Sqrt((scene.Width * scene.Width) + (scene.Height * scene.Height)) / layout.Scale;
        var oldest = (farthest + (mach * c * 2)) / c;
        var newest = (int)Math.Floor(t / WavePhysics.RingInterval);
        for (var n = newest - (int)Math.Ceiling(oldest / WavePhysics.RingInterval); n <= newest; n++)
        {
            var emitted = n * WavePhysics.RingInterval;
            var age = t - emitted;
            if (age < 0) continue;
            var center = layout.ToScreen(new Point(WavePhysics.SourceX(emitted, mach), 0));
            var radius = c * age * layout.Scale;
            var fade = Math.Clamp(1 - (age / oldest), 0, 1);
            dc.DrawEllipse(null, RingPens[(int)Math.Round(Math.Pow(fade, 0.8) * (RingPens.Length - 1))], center, radius, radius);
        }

        var source = layout.ToScreen(new Point(WavePhysics.SourceX(t, mach), 0));
        if (mach > 1) DrawCone(dc, layout, source, mach, farthest);
        DrawSource(dc, source, mach, scene);
        DrawListener(dc, layout, mach, t);
        dc.Pop();

        dc.DrawRoundedRectangle(null, Ink.Frame, scene, 14, 14);
        DrawScaleBar(dc, new Point(scene.Left + 18, scene.Bottom - 16), 50 * layout.Scale, LocFormat("Waves_Diagram_Metres", 50));
    }

    /// <summary>The cone the rings pile into behind a source faster than sound; the bang travels with its edge.</summary>
    private void DrawCone(PaintContext dc, DopplerGeometry layout, Point source, double mach, double length)
    {
        var alpha = WavePhysics.ConeAngle(mach);
        var reach = length * layout.Scale * 1.5;
        var upper = source + new Vector(-Math.Cos(alpha) * reach, -Math.Sin(alpha) * reach);
        var lower = source + new Vector(-Math.Cos(alpha) * reach, Math.Sin(alpha) * reach);
        dc.DrawGeometry(Ink.ConeFill, null, Polygon(source, upper, lower));
        if (Glows("cone"))
        {
            var glow = ExploreTargets.GlowPen(_now);
            dc.DrawLine(glow, source, upper);
            dc.DrawLine(glow, source, lower);
        }

        dc.DrawLine(Ink.ConePen, source, upper);
        dc.DrawLine(Ink.ConePen, source, lower);

        var labelAt = source + ((upper - source) * Math.Min(0.35, 140 / reach));
        var text = LocFormat("Waves_Diagram_Cone", Number(alpha * 180 / Math.PI, "0"));
        var formatted = _labels.Text(text, 11.5, Ink.Orange);
        var pillAt = new Point(labelAt.X - formatted.Width - 4, labelAt.Y - formatted.Height - 10);
        _labels.DrawTextPill(dc, text, pillAt, 11.5, Ink.Orange);
        AddPillTarget("cone", formatted, pillAt);
    }

    /// <summary>The source: a glowing dot with its speed as an arrow; ahead and behind, which way its waves are squeezed.</summary>
    private void DrawSource(PaintContext dc, Point at, double mach, Rect scene)
    {
        dc.DrawEllipse(Ink.SourceGlow, null, at, 16, 16);
        dc.DrawEllipse(Ink.GoldBrush, Ink.DipperRim, at, 6.5, 6.5);
        if (mach > 0.005)
        {
            var arrow = 18 + (34 * Math.Min(mach, 2));
            var tip = new Point(at.X + 10 + arrow, at.Y);
            dc.DrawLine(MakePen(Ink.GoldBrush, 2.2), new Point(at.X + 10, at.Y), new Point(tip.X - 6, tip.Y));
            DrawArrowHead(dc, Ink.GoldBrush, tip, new Vector(1, 0), 10);
        }

        var name = LocFormat("Waves_Diagram_Source", Number(WavePhysics.SourcePitch, "0"));
        var formatted = _labels.Text(name, 11.5, Ink.Gold);
        DrawPillInside(dc, name, new Point(at.X - (formatted.Width / 2) - 6, at.Y - formatted.Height - 26), scene, 11.5, Ink.Gold);
        _targets.Add("source", at, 14);

        if (mach is < 0.12 or > 0.97) return;
        var ahead = _labels.Get("Waves_Diagram_Bunched", 11, Ink.Text);
        var behind = _labels.Get("Waves_Diagram_Stretched", 11, Ink.Dim);
        var aheadAt = new Point(at.X + 26, at.Y + 16);
        var behindAt = new Point(at.X - behind.Width - 26, at.Y + 16);
        dc.DrawText(ahead, aheadAt);
        dc.DrawText(behind, behindAt);
        AddTextTarget("bunched", ahead, aheadAt);
        AddTextTarget("stretched", behind, behindAt);
    }

    /// <summary>The listener: a head that flashes as each ring arrives, and what they hear now.</summary>
    private void DrawListener(PaintContext dc, DopplerGeometry layout, double mach, double t)
    {
        var listener = Listener;
        var at = layout.ToScreen(listener);
        var sounds = WavePhysics.Heard(listener, t, mach);
        var hearing = WavePhysics.HearingOf(sounds, mach, t, WavePhysics.ConeArrival(listener, mach));

        foreach (var sound in sounds)
        {
            // A ring went out at each multiple of the ring interval: how long since the last one arrived.
            var since = (sound.Emitted / WavePhysics.RingInterval) - Math.Floor(sound.Emitted / WavePhysics.RingInterval);
            var flash = Math.Exp(-since * 5);
            dc.PushOpacity(flash);
            dc.DrawEllipse(null, Ink.ArrivalPen, at, 10 + (10 * since), 10 + (10 * since));
            dc.Pop();
        }

        if (hearing == Hearing.Boom)
        {
            var boom = Math.Clamp((t - WavePhysics.ConeArrival(listener, mach)) / 0.3, 0, 1);
            dc.PushOpacity(1 - boom);
            dc.DrawEllipse(Ink.BoomFill, null, at, 20 + (60 * boom), 20 + (60 * boom));
            dc.Pop();
        }

        dc.DrawEllipse(Ink.Head, Ink.DipperRim, at, 7.5, 7.5);
        dc.DrawEllipse(Ink.Ear, null, new Point(at.X + 7, at.Y), 2.6, 3.6);

        var text = hearing switch
        {
            Hearing.Silent => Loc("Waves_Diagram_Silent"),
            Hearing.Boom => Loc("Waves_Diagram_Boom"),
            Hearing.Two => LocFormat("Waves_Diagram_HearsTwo", Number(sounds[0].Frequency, "0"), Number(sounds[1].Frequency, "0")),
            _ => LocFormat("Waves_Diagram_Hears", Number(sounds[0].Frequency, "0"), WavePhysics.NoteName(sounds[0].Frequency)),
        };
        var colour = hearing switch
        {
            Hearing.Approaching => Ink.Gold,
            Hearing.Receding => Ink.Cyan,
            Hearing.Boom => Ink.Orange,
            Hearing.Silent => Ink.Dim,
            _ => Ink.Text,
        };
        var formatted = _labels.Text(text, 12, colour, true);
        var below = at.Y + 16;
        var top = below + formatted.Height + 8 > layout.Scene.Bottom - 8 ? at.Y - formatted.Height - 24 : below;
        DrawPillInside(dc, text, new Point(at.X - (formatted.Width / 2) - 6, top), layout.Scene, 12, colour, "heard");
        _targets.Add("listener", at, 12);
    }
}
