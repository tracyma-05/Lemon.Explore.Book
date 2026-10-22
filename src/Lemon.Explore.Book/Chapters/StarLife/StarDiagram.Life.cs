using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.StarLife;

/// <summary>
/// The life view: the star in the middle, its size, colour and glow from the model, with what goes on around it: the
/// cloud falling in, the disc and jets of a protostar; the shell of a planetary nebula; the flash and blast of a
/// supernova; the beams of a pulsar; the dark disc of a black hole. Sizes go by the cube root of the radius, so a
/// supergiant a thousand times the Sun and a white dwarf a hundredth of it both fit; today's Sun and the inner planets'
/// orbits are drawn on the same scale.
/// </summary>
public sealed partial class StarDiagram
{
    /// <summary>Planets' orbits in Sun radii: Mercury, Venus, Earth, Mars.</summary>
    private static readonly (string Key, double Radius)[] Orbits =
        [("SolarSystem_Mercury_Name", 83), ("SolarSystem_Venus_Name", 155), ("SolarSystem_Earth_Name", 215), ("SolarSystem_Mars_Name", 328)];

    private static readonly (double Angle, double Radius, double Speed, double Size)[] Dust = MakeDust();

    // Layout of the life view: the star's centre and the screen radius of the Sun.
    private Point _starCenter;
    private double _sunPixels = 20;

    private void DrawLife(PaintContext dc, IReadOnlyList<StarStageSpan> track, StarState state, double now)
    {
        const double top = 150;
        var bottom = 190 + _reserve.Current;
        var half = Math.Max(60, (ActualHeight - top - bottom) / 2);
        _starCenter = new Point(ActualWidth / 2, top + half);

        // A red supergiant is about 11 times the Sun in cube root; the frame fits it.
        _sunPixels = Math.Min(half, (ActualWidth / 2) - 20) / 11.5;

        switch (state.Stage)
        {
            case StarStage.Protostar:
                DrawProtostar(dc, state, now);
                break;
            case StarStage.PlanetaryNebula:
                DrawNebulaShell(dc, state.Progress, 1);
                DrawBody(dc, state, now);
                break;
            case StarStage.WhiteDwarf:
                if (state.Progress < 0.3) DrawNebulaShell(dc, 1, 1 - (state.Progress / 0.3));
                DrawBody(dc, state, now);
                break;
            case StarStage.Supernova:
                DrawSupernova(dc, track, state, now);
                break;
            case StarStage.NeutronStar:
                DrawRemnantCloud(dc, state.Progress);
                DrawPulsar(dc, now);
                break;
            case StarStage.BlackHole:
                DrawRemnantCloud(dc, state.Progress);
                DrawBlackHole(dc, now);
                break;
            default:
                DrawBody(dc, state, now);
                break;
        }

        // Over the star, so a giant swallowing them shows; the Sun's outline only where the scale says something.
        if (ShowOrbits && StarModel.ClassOf(Mass) == StarClass.SunLike) DrawOrbits(dc);
        if (ShowSun && state.Stage is not (StarStage.Supernova or StarStage.NeutronStar or StarStage.BlackHole)) DrawSunOutline(dc);
        DrawStarLabel(dc, state);

        // The star (or what is left of it) explains its stage; a giant fills the view, so its ring is kept inside it.
        var reach = state.Stage switch
        {
            StarStage.Supernova or StarStage.NeutronStar or StarStage.BlackHole => 30,
            StarStage.Protostar => Math.Max(Pixels(state.Radius) * 1.8, 30),
            _ => Math.Max(Pixels(state.Radius), 12),
        };
        _targets.Add(StageTarget(state.Stage), _starCenter, Math.Min(reach, Math.Min(ActualWidth, ActualHeight) * 0.3));
    }

    /// <summary>The screen radius of a star <paramref name="radius"/> Suns across: by its cube root.</summary>
    private double Pixels(double radius) => _sunPixels * Math.Cbrt(radius);

    private void DrawBody(PaintContext dc, StarState state, double now)
    {
        var boiling = state.Radius > 20;
        var pulse = state.Stage == StarStage.LateBurning ? 1 + (0.025 * Math.Sin(now * 2.2)) : 1;
        StarDrawing.DrawStar(dc, _starCenter, Pixels(state.Radius) * pulse, state.Temperature, state.Luminosity, now, boiling);
    }

    private void DrawSunOutline(PaintContext dc)
    {
        dc.DrawEllipse(null, LifePaint.SunOutline, _starCenter, _sunPixels, _sunPixels);
        var label = _labels.Get("Star_Label_SunToday", 10.5, StarDrawing.Gold);
        var at = new Point(_starCenter.X + (_sunPixels * 0.72) + 4, _starCenter.Y - (_sunPixels * 0.72) - label.Height);
        dc.DrawLine(LifePaint.SunLeader, new Point(_starCenter.X + (_sunPixels * 0.7), _starCenter.Y - (_sunPixels * 0.7)), new Point(at.X, at.Y + label.Height));
        _labels.DrawPill(dc, "Star_Label_SunToday", new Point(at.X, at.Y - 2), 10.5, StarDrawing.Gold);
        _targets.AddBox("sun", new Rect(at.X, at.Y - 2, label.Width + 12, label.Height + 4));
    }

    /// <summary>Mercury to Mars on the same cube-root scale: a red giant reaches out to about Earth's orbit.</summary>
    private void DrawOrbits(PaintContext dc)
    {
        for (var i = 0; i < Orbits.Length; i++)
        {
            var (key, radius) = Orbits[i];
            var r = Pixels(radius);
            dc.DrawEllipse(null, LifePaint.Orbit, _starCenter, r, r);
            var angle = -2.2 + (i * 0.25);
            var at = new Point(_starCenter.X + (r * Math.Cos(angle)), _starCenter.Y + (r * Math.Sin(angle)));
            dc.DrawEllipse(LifePaint.Planet, null, at, 2.6, 2.6);
            var label = _labels.Get(key, 10, StarDrawing.DimText);
            _labels.DrawPill(dc, key, new Point(at.X - label.Width - 18, at.Y - (label.Height / 2) - 2), 10, StarDrawing.DimText);
            _targets.AddBox("orbits", new Rect(at.X - label.Width - 18, at.Y - (label.Height / 2) - 2, label.Width + 24, label.Height + 4));
        }
    }

    /// <summary>A cloud of gas and dust spiralling in onto a hot core, a disc round it and jets from its poles.</summary>
    private void DrawProtostar(PaintContext dc, StarState state, double now)
    {
        var p = state.Progress;
        var cloud = _sunPixels * 10;

        // The glow of the cloud, fading as it falls in.
        var glow = cloud * (1.1 - (0.6 * p));
        var glowBrush = SpaceDrawing.RadialGradient(
            (Color.FromArgb((byte)(110 * (1 - (0.6 * p))), 0xC8, 0x6A, 0x50), 0),
            (Color.FromArgb((byte)(60 * (1 - (0.6 * p))), 0x7A, 0x3A, 0x5A), 0.5),
            (Color.FromArgb(0, 0x40, 0x20, 0x40), 1));
        dc.DrawEllipse(glowBrush, null, _starCenter, glow, glow);

        foreach (var (angle, radius, speed, size) in Dust)
        {
            var r = cloud * radius * Math.Max(0.05, 1 - (0.9 * Math.Pow(p, 0.7)));
            var a = angle + (now * speed / Math.Max(0.2, radius)) + (p * 6 / radius);
            var at = new Point(_starCenter.X + (r * Math.Cos(a)), _starCenter.Y + (r * Math.Sin(a) * 0.55));
            dc.DrawEllipse(LifePaint.Dust, null, at, size, size);
        }

        // The disc the star feeds from, and the jets it blows out along its axis.
        var disc = Pixels(state.Radius) * 3.2;
        dc.DrawEllipse(LifePaint.Disc, null, _starCenter, disc, disc * 0.22);
        if (p is > 0.25 and < 0.95)
        {
            var strength = Math.Sin((p - 0.25) / 0.7 * Math.PI);
            foreach (var direction in new[] { -1, 1 })
            {
                var tip = new Point(_starCenter.X, _starCenter.Y + (direction * cloud * 0.55));
                var brush = new ImmutableLinearGradientBrush([new ImmutableGradientStop(0, Color.FromArgb((byte)(200 * strength), 0xB8, 0xD8, 0xFF)), new ImmutableGradientStop(1, Color.FromArgb(0, 0xB8, 0xD8, 0xFF))], startPoint: new RelativePoint(_starCenter, RelativeUnit.Absolute), endPoint: new RelativePoint(tip, RelativeUnit.Absolute));
                dc.DrawGeometry(brush, null, Polygon(
                    new Point(_starCenter.X - 2, _starCenter.Y), new Point(tip.X - 12, tip.Y), new Point(tip.X + 12, tip.Y), new Point(_starCenter.X + 2, _starCenter.Y)));
            }
        }

        DrawBody(dc, state, now);
    }

    /// <summary>The shed outer layers, lit up by the hot core: a glowing shell that grows and fades.</summary>
    private void DrawNebulaShell(PaintContext dc, double progress, double opacity)
    {
        var radius = Pixels(300) * (0.5 + (1.3 * progress));
        var alpha = opacity * (1 - (0.4 * progress));
        var brush = SpaceDrawing.RadialGradient(
            (Color.FromArgb(0, 0x6F, 0xE3, 0xC0), 0.35),
            (WithAlpha(StarDrawing.Nebula, 0.28 * alpha), 0.7),
            (WithAlpha(StarDrawing.Pink, 0.45 * alpha), 0.9),
            (Color.FromArgb(0, 0xFF, 0x7E, 0xB6), 1));
        dc.DrawEllipse(brush, null, _starCenter, radius, radius * 0.92);
    }

    /// <summary>A flash brighter than the whole galaxy, then a blast wave of glowing debris; the core is left behind.</summary>
    private void DrawSupernova(PaintContext dc, IReadOnlyList<StarStageSpan> track, StarState state, double now)
    {
        var p = state.Progress;
        var reach = Math.Min(ActualWidth, ActualHeight) * 0.45;

        if (p < 0.06)
        {
            // The red supergiant in its last seconds, collapsing.
            var rsg = track[Math.Max(0, state.Index - 1)];
            var radius = StarModel.RadiusOf(Math.Pow(10, rsg.LogL1), Math.Pow(10, rsg.LogT1));
            StarDrawing.DrawStar(dc, _starCenter, Pixels(radius) * (1 - (p / 0.06 * 0.3)), Math.Pow(10, rsg.LogT1), 1e5, now, true);
        }

        // The blast wave: a glowing shell of hot gas, with filaments streaming out through it.
        var shell = reach * Math.Pow(p, 0.6);
        var fade = 1 - (0.7 * p);
        var debris = SpaceDrawing.RadialGradient(
            (Color.FromArgb((byte)(40 * fade), 0x8A, 0xB8, 0xFF), 0),
            (Color.FromArgb((byte)(70 * fade), 0xC8, 0x60, 0xA0), 0.55),
            (Color.FromArgb((byte)(150 * fade), 0xFF, 0x8A, 0x4A), 0.85),
            (Color.FromArgb((byte)(230 * fade), 0xFF, 0xE6, 0xB8), 0.97),
            (Color.FromArgb(0, 0xFF, 0xE6, 0xB8), 1));
        dc.DrawEllipse(debris, null, _starCenter, shell, shell);
        dc.PushOpacity(fade);
        foreach (var (angle, start, length, red) in Filaments)
        {
            var (sin, cos) = Math.SinCos(angle + (0.05 * Math.Sin((now * 0.2) + angle)));
            var from = _starCenter + (new Vector(cos, sin) * shell * start);
            var to = _starCenter + (new Vector(cos, sin) * shell * Math.Min(1.04, start + length));
            dc.DrawLine(red ? LifePaint.FilamentRed : LifePaint.FilamentBlue, from, to);
        }

        dc.Pop();

        // The flash itself, blinding at first.
        var flash = Math.Clamp(1 - ((p - 0.05) / 0.35), 0, 1) * Math.Clamp(p / 0.03, 0, 1);
        if (flash > 0)
        {
            var size = reach * (0.35 + (0.5 * flash));
            var brush = SpaceDrawing.RadialGradient(
            (Color.FromArgb((byte)(255 * flash), 255, 255, 255), 0),
            (Color.FromArgb((byte)(160 * flash), 0xFF, 0xF0, 0xD0), 0.15),
            (Color.FromArgb((byte)(50 * flash), 0xFF, 0xB0, 0x70), 0.45),
            (Color.FromArgb(0, 0xFF, 0xB0, 0x70), 1));
            dc.DrawEllipse(brush, null, _starCenter, size, size);
        }

        if (p > 0.5)
        {
            var core = (p - 0.5) / 0.5;
            dc.DrawEllipse(new SolidColorBrush(WithAlpha(Color.FromRgb(0xC8, 0xDC, 0xFF), core)), null, _starCenter, 3, 3);
        }
    }

    /// <summary>What is left of the supernova: a faint, growing shell around the remnant.</summary>
    private void DrawRemnantCloud(PaintContext dc, double progress)
    {
        var reach = Math.Min(ActualWidth, ActualHeight) * 0.45;
        var radius = reach * (1 + (0.25 * progress));
        var alpha = 0.25 * (1 - progress);
        if (alpha < 0.01) return;
        var brush = SpaceDrawing.RadialGradient(
            (Color.FromArgb(0, 0xFF, 0x9A, 0x5A), 0.6),
            (WithAlpha(StarDrawing.Blast, alpha), 0.92),
            (Color.FromArgb(0, 0xFF, 0x9A, 0x5A), 1));
        dc.DrawEllipse(brush, null, _starCenter, radius, radius);
    }

    /// <summary>A neutron star: a city-sized ball spinning many times a second, its beams sweeping like a lighthouse's.</summary>
    private void DrawPulsar(PaintContext dc, double now)
    {
        var angle = now * 2.4;
        var length = Math.Min(ActualWidth, ActualHeight) * 0.42;
        foreach (var side in new[] { 0.0, Math.PI })
        {
            var direction = new Vector(Math.Cos(angle + side), Math.Sin(angle + side) * 0.6);
            var tip = _starCenter + (direction * length);
            var normal = new Vector(-direction.Y, direction.X);
            var brush = new ImmutableLinearGradientBrush([new ImmutableGradientStop(0, Color.FromArgb(200, 0xC8, 0xDC, 0xFF)), new ImmutableGradientStop(1, Color.FromArgb(0, 0xC8, 0xDC, 0xFF))], startPoint: new RelativePoint(_starCenter, RelativeUnit.Absolute), endPoint: new RelativePoint(tip, RelativeUnit.Absolute));
            dc.DrawGeometry(brush, null, Polygon(_starCenter, tip + (normal * length * 0.09), tip - (normal * length * 0.09)));
        }

        dc.DrawEllipse(LifePaint.PulsarGlow, null, _starCenter, 26, 26);
        dc.DrawEllipse(LifePaint.PulsarCore, null, _starCenter, 4, 4);
    }

    /// <summary>A black hole: no light at all, only a dark disc edged by light bent round it, and gas swirling in.</summary>
    private void DrawBlackHole(PaintContext dc, double now)
    {
        var horizon = Math.Max(14, _sunPixels * 0.9);
        var disc = horizon * 4.2;

        // The far half of the disc, behind the hole, then the hole, then the near half in front.
        dc.PushClip(new RectangleGeometry(new Rect(_starCenter.X - disc, _starCenter.Y - disc, disc * 2, disc)));
        dc.DrawEllipse(LifePaint.AccretionDisc, null, _starCenter, disc, disc * 0.28);
        dc.Pop();

        dc.DrawEllipse(LifePaint.Lensing, null, _starCenter, horizon * 1.6, horizon * 1.6);
        dc.DrawEllipse(Brushes.Black, LifePaint.PhotonRing, _starCenter, horizon, horizon);

        dc.PushClip(new RectangleGeometry(new Rect(_starCenter.X - disc, _starCenter.Y, disc * 2, disc)));
        dc.DrawEllipse(LifePaint.AccretionDisc, null, _starCenter, disc, disc * 0.28);
        dc.Pop();

        // Clumps of gas going round.
        for (var k = 0; k < 10; k++)
        {
            var a = (k * 0.63) + (now * (0.9 + (0.07 * k)));
            var r = disc * (0.45 + (0.05 * k));
            var at = new Point(_starCenter.X + (r * Math.Cos(a)), _starCenter.Y + (r * Math.Sin(a) * 0.28));
            if (Math.Sin(a) < 0 && Math.Abs(at.X - _starCenter.X) < horizon) continue;
            dc.DrawEllipse(LifePaint.Clump, null, at, 2.2, 1.4);
        }
    }

    /// <summary>The stage and size under the star.</summary>
    private void DrawStarLabel(PaintContext dc, StarState state)
    {
        var text = state.Stage switch
        {
            StarStage.NeutronStar or StarStage.BlackHole => LocFormat("Star_Label_Remnant", Loc(StageKey(state.Stage)), StarFormat.Km(StarModel.RemnantRadiusKm(Mass) * 2)),
            StarStage.WhiteDwarf => LocFormat("Star_Label_Remnant", Loc(StageKey(state.Stage)), StarFormat.Km(state.Radius * StarModel.SunRadiusKm * 2)),
            StarStage.Supernova => Loc(StageKey(state.Stage)),
            _ => LocFormat("Star_Label_Star", Loc(StageKey(state.Stage)), StarFormat.Times(state.Radius)),
        };

        var reach = state.Stage switch
        {
            StarStage.Supernova or StarStage.NeutronStar or StarStage.BlackHole => 70,
            StarStage.Protostar => Math.Max(Pixels(state.Radius) * 1.8, 40),
            _ => Math.Max(Pixels(state.Radius) * 1.15, 18),
        };
        var y = Math.Min(_starCenter.Y + reach + 16, ActualHeight - 186 - _reserve.Current);
        DrawCenteredPill(dc, text, new Point(_starCenter.X, y), 12.5, StarDrawing.Text);
        var formatted = _labels.Text(text, 12.5, StarDrawing.Text);
        AddPillTarget(StageTarget(state.Stage), text, new Point(_starCenter.X - (formatted.Width / 2) - 6, y - (formatted.Height / 2) - 2), 12.5, StarDrawing.Text);
    }

    /// <summary>Streaks of the supernova's debris: angle, where they start and how long they are (shares of the shell), colour.</summary>
    private static readonly (double Angle, double Start, double Length, bool Red)[] Filaments = MakeFilaments();

    private static (double Angle, double Start, double Length, bool Red)[] MakeFilaments()
    {
        var random = new Random(11);
        return Enumerable.Range(0, 90)
            .Select(_ => (random.NextDouble() * 2 * Math.PI, 0.55 + (random.NextDouble() * 0.35), 0.06 + (random.NextDouble() * 0.16), random.NextDouble() < 0.7))
            .ToArray();
    }

    private static (double Angle, double Radius, double Speed, double Size)[] MakeDust()
    {
        var random = new Random(7);
        return Enumerable.Range(0, 360)
            .Select(_ => (random.NextDouble() * 2 * Math.PI, 0.15 + (random.NextDouble() * 0.9), 0.15 + (random.NextDouble() * 0.25), 0.6 + (random.NextDouble() * 1.3)))
            .ToArray();
    }

    private static class LifePaint
    {
        public static readonly IPen SunOutline = MakePen(Solid(200, 0xFF, 0xD3, 0x6B), 1.2, dashed: true);
        public static readonly IPen SunLeader = MakePen(Solid(120, 0xFF, 0xD3, 0x6B), 1);
        public static readonly IPen Orbit = MakePen(Solid(60, 0xB8, 0xC7, 0xFF), 1, dashed: true);
        public static readonly IBrush Planet = Solid(230, 0xB8, 0xC7, 0xFF);
        public static readonly IBrush Dust = Solid(150, 0xE8, 0xA8, 0x88);
        public static readonly IPen FilamentRed = MakePen(Solid(150, 0xFF, 0x7A, 0x50), 1.4);
        public static readonly IPen FilamentBlue = MakePen(Solid(130, 0x9A, 0xC0, 0xFF), 1.2);

        public static readonly IBrush Disc = SpaceDrawing.RadialGradient(
            (Color.FromArgb(200, 0xFF, 0xB0, 0x70), 0),
            (Color.FromArgb(110, 0xC8, 0x60, 0x40), 0.6),
            (Color.FromArgb(0, 0x80, 0x30, 0x30), 1));

        public static readonly IBrush PulsarGlow = SpaceDrawing.RadialGradient((Color.FromArgb(200, 0xC8, 0xDC, 0xFF), 0), (Color.FromArgb(0, 0x80, 0xA0, 0xFF), 1));
        public static readonly IBrush PulsarCore = Solid(255, 0xF0, 0xF6, 0xFF);

        public static readonly IBrush AccretionDisc = SpaceDrawing.RadialGradient(
            (Color.FromArgb(0, 0xFF, 0xC0, 0x80), 0.25),
            (Color.FromArgb(230, 0xFF, 0xE0, 0xB0), 0.3),
            (Color.FromArgb(160, 0xFF, 0x9A, 0x40), 0.55),
            (Color.FromArgb(0, 0xC0, 0x40, 0x20), 1));

        public static readonly IBrush Lensing = SpaceDrawing.RadialGradient(
            (Color.FromArgb(0, 0xFF, 0xD0, 0xA0), 0.55),
            (Color.FromArgb(120, 0xFF, 0xD0, 0xA0), 0.66),
            (Color.FromArgb(0, 0xFF, 0xD0, 0xA0), 1));

        public static readonly IPen PhotonRing = MakePen(Solid(230, 0xFF, 0xE6, 0xC0), 1.6);
        public static readonly IBrush Clump = Solid(200, 0xFF, 0xE0, 0xB0);
    }
}
