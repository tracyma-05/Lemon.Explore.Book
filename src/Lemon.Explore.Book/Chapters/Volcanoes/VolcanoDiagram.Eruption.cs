using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Volcanoes;

/// <summary>
/// The eruption view: a volcano cut in half above its magma chamber. Magma flows into the chamber, the pressure builds
/// and the ground swells; the magma forces its way up the conduit and gas comes out of it as bubbles once it is shallow
/// enough, more and bigger the higher it gets; then it erupts in the style its stickiness and gas give it. Heights above
/// the ground (the column) and depths below it have their own scales, both on the ruler at the right.
/// </summary>
public sealed partial class VolcanoDiagram
{
    /// <summary>The depth shown below the ground, km, and the height of the sky's scale above the vent, km.</summary>
    private const double DepthShownKm = 10;

    private const double SkyShownKm = 45;

    private readonly Random _rng = new(5);
    private readonly List<Puff> _puffs = [];
    private readonly List<Spark> _sparks = [];
    private double _burstClock;
    private double _emitCarry;

    /// <summary>A puff of ash, steam or a pyroclastic flow, in units of <see cref="EruptionLayout.Unit"/> from the vent.</summary>
    private sealed class Puff
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public double R;
        public double Age;
        public double Life;
        public Color Color;
        public double Alpha;

        /// <summary>A pyroclastic flow's puff: it runs down the flank on side <see cref="Side"/> at distance <see cref="Along"/>.</summary>
        public bool Ground;

        public int Side;
        public double Along;
    }

    /// <summary>A glowing lump of lava (fountain spray, a bomb), in units from the vent.</summary>
    private sealed class Spark
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public double Age;
        public double Life;
        public double Size;
        public bool Bomb;
    }

    /// <summary>Where things are in the eruption view this frame.</summary>
    private readonly record struct EruptionLayout(
        Rect Stage,
        double Ground,
        double Center,
        double ConeHeight,
        double HalfWidth,
        Point Vent,
        double KmDown,
        double KmUp,
        Rect Chamber,
        double ConduitWidth,
        ConeShape Shape,
        double Bulge,
        double Unit,
        double RulerX);

    private void OnMagmaChanged()
    {
        _puffs.Clear();
        _sparks.Clear();
    }

    private EruptionLayout LayOutEruption()
    {
        var s = Stage();
        var rulerX = s.Right - 46;
        var width = rulerX - s.Left - 30;
        var ground = s.Top + (s.Height * 0.46);
        var center = s.Left + (width * 0.47);
        var shape = VolcanoModel.Shape(Silica, Water);
        var (coneHeight, halfWidth) = shape switch
        {
            ConeShape.Shield => (s.Height * 0.075, width * 0.46),
            ConeShape.Caldera => (s.Height * 0.19, width * 0.27),
            _ => (s.Height * 0.21, width * 0.25),
        };

        var kmDown = (s.Bottom - 6 - ground) / DepthShownKm;
        var top = VolcanoModel.ChamberTopKm(Silica);
        var chamberWidth = Math.Min(width * 0.34, halfWidth * 1.3);
        var swell = 1 + (0.05 * VolcanoModel.Overpressure(Progress) / 18);
        var chamber = new Rect(center - (chamberWidth * swell / 2), ground + (top * kmDown), chamberWidth * swell, 2 * kmDown * swell);
        var bulge = VolcanoModel.Overpressure(Progress) / 18 * Math.Max(2, coneHeight * 0.05);
        var layout = new EruptionLayout(s, ground, center, coneHeight, halfWidth, default, kmDown, 0, chamber,
            Math.Max(8, width * 0.02), shape, bulge, s.Height / 10, rulerX);
        var vent = new Point(center, SurfaceY(layout, center) + (shape == ConeShape.Caldera ? 0 : 2));
        var kmUp = (vent.Y - (s.Top + 8)) / SkyShownKm;
        return layout with { Vent = vent, KmUp = kmUp };
    }

    /// <summary>The height of the cone above the plain at a share <paramref name="d"/> (0 at the top, 1 at the foot) of its half width.</summary>
    private static double ConeProfile(ConeShape shape, double d)
    {
        d = Math.Clamp(d, 0, 1);
        switch (shape)
        {
            case ConeShape.Shield:
                return 1 - Math.Pow(d, 1.5);

            case ConeShape.Caldera:
            {
                var strato = Math.Pow(1 - d, 1.7);
                var capped = Math.Min(strato, 0.78);
                var dip = 0.22 * (1 - VolcanoModel.SmoothStep(0.1, 0.17, d));
                return capped - dip;
            }

            default:
            {
                var strato = Math.Pow(1 - d, 1.8);
                var crater = 0.07 * (1 - VolcanoModel.SmoothStep(0.025, 0.06, d));
                return strato - crater;
            }
        }
    }

    /// <summary>The ground's height on the screen at <paramref name="x"/>, with the cone and the swelling.</summary>
    private static double SurfaceY(EruptionLayout l, double x)
    {
        var d = Math.Abs(x - l.Center) / l.HalfWidth;
        var bulge = l.Bulge * Math.Exp(-Math.Pow((x - l.Center) / (l.Chamber.Width * 0.9), 2));
        return l.Ground - (l.ConeHeight * ConeProfile(l.Shape, d)) - bulge;
    }

    /// <summary>The point on the flank on side <paramref name="side"/> (−1 left, +1 right) a share <paramref name="along"/> of the way from the vent to the foot.</summary>
    private static Point FlankPoint(EruptionLayout l, int side, double along)
    {
        var start = l.Shape == ConeShape.Caldera ? 0.17 : l.Shape == ConeShape.Shield ? 0.01 : 0.06;
        var x = l.Center + (side * l.HalfWidth * (start + ((1.15 - start) * along)));
        return new Point(x, SurfaceY(l, x));
    }

    private Point FromVent(EruptionLayout l, double x, double y) => new(l.Vent.X + (x * l.Unit), l.Vent.Y + (y * l.Unit));

    #region time

    private void StepEruption(double dt)
    {
        Advance(dt / VolcanoModel.RoundSeconds, wrap: true);
        var l = LayOutEruption();
        var phase = VolcanoModel.PhaseAt(Progress);
        var style = VolcanoModel.StyleOf(Silica, Water);
        var share = VolcanoModel.EruptionShare(Progress);

        // The eruption goes on even while the round is held still (a walkthrough step), so the picture stays alive.
        if (_drag != DragKind.Scrub) Emit(l, phase, style, share, dt);
        if (dt > 0) MoveParticles(l, style, dt);
    }

    /// <summary>The eruption's height in units from the vent: a local scale for fountains, the sky's for columns.</summary>
    private double TopUnits(EruptionLayout l, EruptionStyle style)
    {
        var km = VolcanoModel.EruptionHeightKm(style, Water);
        var room = (l.Vent.Y - l.Stage.Top - 24) / l.Unit;
        return style is EruptionStyle.Hawaiian or EruptionStyle.Strombolian
            ? Math.Min(room * 0.8, km * l.Stage.Height * (style == EruptionStyle.Strombolian ? 0.9 : 0.45) / l.Unit)
            : Math.Min(room, km * l.KmUp / l.Unit);
    }

    private void Emit(EruptionLayout l, EruptionPhase phase, EruptionStyle style, double share, double dt)
    {
        _burstClock += dt;
        if (phase == EruptionPhase.Erupting)
        {
            var strength = Math.Sin(Math.PI * Math.Clamp(share * 1.05, 0, 1));
            var top = TopUnits(l, style);
            switch (style)
            {
                case EruptionStyle.Hawaiian:
                    EmitFountain(top * (0.55 + (0.45 * strength)), 160 * dt * (0.3 + strength));
                    if (_rng.NextDouble() < dt * 3) AddPuff(0, -0.2, (_rng.NextDouble() - 0.5) * 0.2, -0.5, 0.3, 4, Ink.Steam, 0.16);
                    break;

                case EruptionStyle.Strombolian:
                    if (_burstClock > 0.6 + (_rng.NextDouble() * 0.6))
                    {
                        _burstClock = 0;
                        for (var i = 0; i < 26; i++) AddBlob(top, spread: 0.6, bomb: i < 6);
                        for (var i = 0; i < 2; i++) AddPuff(0, -0.2, (_rng.NextDouble() - 0.5) * 0.3, -0.5, 0.22, 2.2, Ink.AshDark, 0.5);
                    }

                    break;

                case EruptionStyle.Vulcanian:
                    if (_burstClock > 2.4 + (_rng.NextDouble() * 1.2))
                    {
                        _burstClock = 0;
                        for (var i = 0; i < 16; i++)
                        {
                            var speed = Math.Sqrt(2 * 2.2 * top) * (0.6 + (_rng.NextDouble() * 0.5));
                            AddPuff((_rng.NextDouble() - 0.5) * 0.3, -0.1, (_rng.NextDouble() - 0.5) * 0.6, -speed, 0.35 + (_rng.NextDouble() * 0.25), 6, Ink.Ash, 0.75);
                        }

                        for (var i = 0; i < 9; i++) AddBlob(top * 0.35, spread: 1.0, bomb: true);
                    }

                    break;

                case EruptionStyle.Plinian:
                    _emitCarry += dt * 50 * (0.35 + strength);
                    while (_emitCarry >= 1)
                    {
                        _emitCarry -= 1;
                        var speed = Math.Sqrt(2 * 1.6 * top) * (0.8 + (_rng.NextDouble() * 0.3));
                        AddPuff((_rng.NextDouble() - 0.5) * 0.25, 0, (_rng.NextDouble() - 0.5) * 0.25, -speed, 0.36, 9, _rng.NextDouble() < 0.5 ? Ink.Ash : Ink.AshDark, 0.8);
                    }

                    // Late on the column grows too heavy and falls back: pyroclastic flows down both sides.
                    if (share > 0.62 && share < 0.92 && _rng.NextDouble() < dt * 16) AddFlowPuff(_rng.NextDouble() < 0.5 ? -1 : 1);
                    break;

                case EruptionStyle.Dome:
                    // A piece of the dome breaks off and avalanches down one side.
                    if (share > 0.55 && share < 0.78 && _rng.NextDouble() < dt * 12) AddFlowPuff(1);
                    if (_rng.NextDouble() < dt * 2) AddPuff(0, -0.3, 0.1, -0.4, 0.25, 5, Ink.Steam, 0.16);
                    break;
            }
        }
        else if (phase == EruptionPhase.Calm || phase == EruptionPhase.Rising)
        {
            // Steam and gas from the vent between eruptions.
            if (_rng.NextDouble() < dt * (phase == EruptionPhase.Calm ? 3 : 1.5)) AddPuff(0, -0.1, 0.12 + ((_rng.NextDouble() - 0.5) * 0.1), -0.35, 0.16, 5, Ink.Steam, 0.18);
        }
    }

    /// <summary>Lava fountain spray: <paramref name="rate"/> blobs (fractions carried over) reaching about <paramref name="top"/> units.</summary>
    private void EmitFountain(double top, double rate)
    {
        _emitCarry += rate;
        while (_emitCarry >= 1)
        {
            _emitCarry -= 1;
            AddBlob(top, spread: 0.14, bomb: false);
        }
    }

    /// <summary>A lump of lava thrown up to about <paramref name="top"/> units, spread by <paramref name="spread"/> radians.</summary>
    private void AddBlob(double top, double spread, bool bomb)
    {
        const double g = 9;
        var speed = Math.Sqrt(2 * g * Math.Max(0.2, top)) * (0.75 + (_rng.NextDouble() * 0.3));
        var angle = (_rng.NextDouble() - 0.5) * 2 * spread;
        _sparks.Add(new Spark
        {
            X = (_rng.NextDouble() - 0.5) * 0.1,
            Y = 0,
            Vx = Math.Sin(angle) * speed,
            Vy = -Math.Cos(angle) * speed,
            Life = 6,
            Size = bomb ? 2.6 + (_rng.NextDouble() * 1.8) : 1.2 + (_rng.NextDouble() * 1.2),
            Bomb = bomb,
        });
        if (_sparks.Count > 900) _sparks.RemoveAt(0);
    }

    private void AddPuff(double x, double y, double vx, double vy, double r, double life, Color color, double alpha)
    {
        _puffs.Add(new Puff { X = x, Y = y, Vx = vx, Vy = vy, R = r, Life = life, Color = color, Alpha = alpha });
        if (_puffs.Count > 420) _puffs.RemoveAt(0);
    }

    private void AddFlowPuff(int side) =>
        _puffs.Add(new Puff { Ground = true, Side = side, Along = 0, R = 0.18, Life = 4.5, Color = Ink.AshDark, Alpha = 0.85, Vx = 0.32 + (_rng.NextDouble() * 0.12) });

    private void MoveParticles(EruptionLayout l, EruptionStyle style, double dt)
    {
        const double g = 9;
        for (var i = _sparks.Count - 1; i >= 0; i--)
        {
            var s = _sparks[i];
            s.Age += dt;
            s.Vy += g * dt;
            s.X += s.Vx * dt;
            s.Y += s.Vy * dt;

            // Lands on the cone (or falls back into the vent).
            var p = FromVent(l, s.X, s.Y);
            if ((s.Vy > 0 && p.Y > SurfaceY(l, p.X) + 1) || s.Age > s.Life) _sparks.RemoveAt(i);
        }

        var top = TopUnits(l, style);
        var spreadAt = top * 0.82;
        for (var i = _puffs.Count - 1; i >= 0; i--)
        {
            var p = _puffs[i];
            p.Age += dt;
            if (p.Age > p.Life)
            {
                _puffs.RemoveAt(i);
                continue;
            }

            if (p.Ground)
            {
                // A pyroclastic flow races down the flank and spreads out on the plain, a cloud billowing above it.
                p.Along += p.Vx * dt * (1 - (p.Age / p.Life * 0.6));
                p.R += dt * 0.16;
                continue;
            }

            var height = -p.Y;
            if (style == EruptionStyle.Plinian && height > spreadAt * 0.92)
            {
                // At the height where it is as light as the air around, the column spreads sideways as an umbrella.
                var side = p.X == 0 ? 1 : Math.Sign(p.X);
                if (Math.Abs(p.X) < top * 0.55) p.Vx += side * dt * 1.6;
                else p.Vx *= Math.Pow(0.2, dt);
                p.Vy *= Math.Pow(0.05, dt);
                p.Vy += (spreadAt - height) * dt * 0.8;
            }
            else
            {
                // Gas and ash rise quickly at first and slow as they mix with air.
                p.Vy *= Math.Pow(style == EruptionStyle.Vulcanian ? 0.35 : 0.75, dt);
                if (style == EruptionStyle.Plinian) p.Vy -= dt * 0.6;
            }

            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            p.R += dt * (0.08 + (0.015 * Math.Min(height, 20)));
        }
    }

    #endregion

    #region drawing

    private void DrawEruptionMode(PaintContext dc, double now)
    {
        var l = LayOutEruption();
        var s = l.Stage;
        var phase = VolcanoModel.PhaseAt(Progress);
        var style = VolcanoModel.StyleOf(Silica, Water);
        var share = VolcanoModel.EruptionShare(Progress);

        // Sky and the rock below.
        var skyRect = new Rect(s.Left, s.Top, l.RulerX - s.Left + 8, Math.Max(1, l.Ground - s.Top));
        dc.DrawRoundedRectangle(Ink.Sky, null, skyRect, 10, 10);
        DrawUnderground(dc, l);
        DrawChamber(dc, l, phase, now);
        DrawConduit(dc, l, phase, style, now);
        DrawCone(dc, l, now);
        DrawLavaFlows(dc, l, phase, style, share, now);
        if (style == EruptionStyle.Dome) DrawDome(dc, l, phase, share, now);
        dc.PushClip(new RectangleGeometry(new Rect(s.Left, s.Top, l.RulerX - s.Left + 8, s.Height), 10, 10));
        DrawEruptionParticles(dc, l, style, now);
        dc.Pop();
        DrawRuler(dc, l, style, phase);
        DrawGauge(dc, l, now);
        DrawEruptionLabels(dc, l, phase, style, share, now);
    }

    private void DrawUnderground(PaintContext dc, EruptionLayout l)
    {
        var s = l.Stage;
        var rect = new Rect(s.Left, l.Ground, l.RulerX - s.Left + 8, Math.Max(1, s.Bottom - l.Ground));
        dc.DrawRoundedRectangle(_rockBrush ??= SpaceDrawing.LinearGradient(Ink.Rock, Ink.RockDeep, 90), null, rect, 10, 10);

        // Faint layers of older rock.
        for (var i = 1; i < 9; i++)
        {
            var y = l.Ground + (i * l.KmDown * 1.1) + (Math.Sin(i * 1.7) * 4);
            if (y > s.Bottom - 4) break;
            dc.DrawLine(Ink.Grid, new Point(s.Left + 6, y), new Point(l.RulerX + 4, y + (Math.Sin(i) * 6)));
        }

        // The magma feeding the chamber from deep below.
        var feed = new Rect(l.Center - (l.ConduitWidth * 0.9), l.Chamber.Bottom - 6, l.ConduitWidth * 1.8, Math.Max(0, s.Bottom - l.Chamber.Bottom + 4));
        dc.DrawRectangle(FadedBrush(Ink.LavaDark, 0.8), null, feed);
        if (VolcanoModel.PhaseAt(Progress) == EruptionPhase.Recharge)
        {
            for (var i = 0; i < 4; i++)
            {
                var t = ((_drift * 0.35) + (i * 0.25)) % 1;
                var y = feed.Bottom - (t * feed.Height);
                dc.DrawEllipse(SoftBrush(Ink.Lava, 0.9), null, new Point(l.Center, y), l.ConduitWidth * 1.2, l.ConduitWidth * 1.5);
            }
        }
    }

    private IBrush? _rockBrush;
    private IBrush? _chamberBrush;

    private void DrawChamber(PaintContext dc, EruptionLayout l, EruptionPhase phase, double now)
    {
        var c = l.Chamber;
        var center = new Point(c.Left + (c.Width / 2), c.Top + (c.Height / 2));
        dc.DrawEllipse(SoftBrush(Ink.Lava, 0.25 + (0.1 * Math.Sin(now * 2))), null, center, c.Width * 0.62, c.Height * 0.75);
        _chamberBrush ??= new ImmutableRadialGradientBrush(
        [
            new ImmutableGradientStop(0, Ink.LavaHot),
            new ImmutableGradientStop(1, Ink.LavaDark),
        ],
            gradientOrigin: new RelativePoint(0.45, 0.4, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.5, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.5, RelativeUnit.Relative));
        dc.DrawEllipse(_chamberBrush, Glows("chamber") ? ExploreTargets.GlowPen(now) : FadedPen(Ink.LavaDark, 0.9, 2), center, c.Width / 2, c.Height / 2);

        // Crystals floating in the cooler edges of the chamber.
        for (var i = 0; i < 26; i++)
        {
            var a = i * 2.39996;
            var r = 0.55 + (0.4 * SqueezeTexture.Hash(i, 51));
            var p = new Point(center.X + (Math.Cos(a) * c.Width / 2 * r), center.Y + (Math.Sin(a) * c.Height / 2 * r));
            dc.DrawRectangle(FadedBrush(Ink.Crust, 0.5), null, new Rect(p.X - 1.5, p.Y - 1, 3, 2));
        }

        _targets.Add("chamber", center, Math.Min(c.Width, c.Height) / 2);

        // Small earthquakes as magma pushes the rock apart.
        if (phase is EruptionPhase.Recharge or EruptionPhase.Rising)
        {
            var quakeAt = new Point(center.X + (c.Width * 0.42), c.Top - 6);
            for (var i = 0; i < 6; i++)
            {
                var flicker = Math.Sin((now * (3 + i)) + (i * 2.1));
                if (flicker < 0.55) continue;
                var x = center.X + ((SqueezeTexture.Hash(i, 61) - 0.5) * c.Width * 1.1);
                var y = phase == EruptionPhase.Rising
                    ? l.Vent.Y + ((c.Top - l.Vent.Y) * SqueezeTexture.Hash(i, 62))
                    : c.Top + ((SqueezeTexture.Hash(i, 62) - 0.7) * c.Height);
                if (phase == EruptionPhase.Rising) x = l.Center + ((SqueezeTexture.Hash(i, 61) - 0.5) * l.ConduitWidth * 5);
                DrawQuake(dc, new Point(x, y), 7, (flicker - 0.55) * 2.2);
                quakeAt = new Point(x, y);
            }

            _quakeSpot = quakeAt;
        }
        else
        {
            _quakeSpot = null;
        }
    }

    private Point? _quakeSpot;

    /// <summary>The top of the magma in the conduit: rising up it, at the vent during the eruption, sinking back after.</summary>
    private double MagmaTopY(EruptionLayout l, EruptionPhase phase)
    {
        var bottom = l.Chamber.Top + 4;
        return phase switch
        {
            EruptionPhase.Recharge => bottom - ((bottom - l.Vent.Y) * 0.22 * VolcanoModel.SmoothStep(0, VolcanoModel.RisingStart, Progress)),
            EruptionPhase.Rising => bottom - ((bottom - l.Vent.Y) * (0.22 + (0.78 * VolcanoModel.SmoothStep(VolcanoModel.RisingStart, VolcanoModel.EruptionStart, Progress)))),
            EruptionPhase.Erupting => l.Vent.Y,
            _ => l.Vent.Y + ((bottom - l.Vent.Y) * 0.5 * VolcanoModel.SmoothStep(VolcanoModel.CalmStart, 1, Progress)),
        };
    }

    /// <summary>The conduit's middle at height <paramref name="y"/>: it wanders a little on its way up.</summary>
    private static double ConduitX(EruptionLayout l, double y) => l.Center + (Math.Sin((y - l.Vent.Y) / Math.Max(20, l.Chamber.Top - l.Vent.Y) * 5.5) * l.ConduitWidth * 0.35);

    private void DrawConduit(PaintContext dc, EruptionLayout l, EruptionPhase phase, EruptionStyle style, double now)
    {
        var w = l.ConduitWidth;
        var bottom = l.Chamber.Top + 6;
        var points = new List<Point>();
        for (var y = l.Vent.Y; y <= bottom; y += 4) points.Add(new Point(ConduitX(l, y), y));
        points.Add(new Point(ConduitX(l, bottom), bottom));

        dc.DrawGeometry(null, Glows("conduit") ? ExploreTargets.GlowPen(now, w + 8) : FadedPen(Ink.RockDeep, 1, w + 4, flat: true), Line(points));
        dc.DrawGeometry(null, FadedPen(Color.FromRgb(0x15, 0x10, 0x0E), 1, w, flat: true), Line(points));

        // The magma in it, glowing.
        var top = MagmaTopY(l, phase);
        var filled = points.Where(p => p.Y >= top).ToList();
        if (filled.Count > 1) dc.DrawGeometry(null, FadedPen(Ink.Lava, 0.95, w - 2, flat: true), Line(filled));
        var mid = new Point(ConduitX(l, (l.Vent.Y + bottom) / 2), (l.Vent.Y + bottom) / 2);
        _targets.Add("conduit", mid, w * 1.5);

        // Gas: bubbles from where the magma can no longer hold its water, growing as they rise; ash above where the froth tears apart.
        var satY = DepthY(l, VolcanoModel.SaturationDepth(Silica, Water));
        var explosive = VolcanoModel.IsExplosive(style);
        var frag = explosive ? VolcanoModel.FragmentationDepth(Silica, Water) : null;
        var fragY = frag is { } f ? DepthY(l, f) : double.NaN;
        var flowing = phase is EruptionPhase.Erupting or EruptionPhase.Rising;
        var speed = flowing ? (style is EruptionStyle.Hawaiian or EruptionStyle.Strombolian ? 0.5 : 0.35) : 0.04;
        var drawnTop = Math.Max(top, l.Vent.Y);
        var bubbleSpot = (Point?)null;
        for (var i = 0; i < 70; i++)
        {
            var phaseOfBubble = ((i * 0.6180339887) + (_drift * speed * (0.7 + (0.6 * SqueezeTexture.Hash(i, 71))))) % 1;
            var y = satY - (phaseOfBubble * (satY - drawnTop));
            if (y < drawnTop || y > satY || y > bottom) continue;
            var fraction = VolcanoModel.GasFraction(Silica, Water, YDepth(l, y));
            if (explosive && !double.IsNaN(fragY) && y < fragY && phase == EruptionPhase.Erupting)
            {
                // Torn froth: a spray of ash and gas.
                var ax = ConduitX(l, y) + ((SqueezeTexture.Hash(i, 72) - 0.5) * (w - 3));
                dc.DrawRectangle(FadedBrush(Ink.Ash, 0.85), null, new Rect(ax - 1, y - 1, 2, 2));
                continue;
            }

            var r = Math.Min(w * 0.45, 0.6 + (w * 0.42 * Math.Cbrt(fraction)));
            var x = ConduitX(l, y) + ((SqueezeTexture.Hash(i, 72) - 0.5) * Math.Max(0, w - (2 * r) - 2));
            dc.DrawEllipse(FadedBrush(Color.FromRgb(0xFF, 0xF1, 0xC8), 0.55), FadedPen(Ink.LavaDark, 0.8, 0.8), new Point(x, y), r, r * 1.05);
            if (fraction > 0.3) bubbleSpot ??= new Point(x, y);
        }

        // Strombolian: a big gas slug rising before each burst.
        if (style == EruptionStyle.Strombolian && phase == EruptionPhase.Erupting)
        {
            var t = Math.Clamp(_burstClock / 1.3, 0, 1);
            var sy = satY - ((satY - l.Vent.Y) * t);
            dc.DrawRoundedRectangle(FadedBrush(Color.FromRgb(0xFF, 0xF1, 0xC8), 0.6), null, new Rect(ConduitX(l, sy) - (w * 0.38), sy - (w * 1.2), w * 0.76, w * 2.4), w * 0.38, w * 0.38);
        }

        _bubbleSpot = bubbleSpot ?? (satY > top ? new Point(ConduitX(l, (satY + drawnTop) / 2), (satY + drawnTop) / 2) : null);
        if (_bubbleSpot is { } spot && satY > drawnTop + 6) _targets.Add("bubbles", spot, Math.Max(8, w));

        // The two depths, marked across the conduit.
        _satLine = new Point(ConduitX(l, satY), satY);
        _fragLine = double.IsNaN(fragY) ? null : new Point(ConduitX(l, fragY), fragY);
    }

    private Point? _bubbleSpot;
    private Point _satLine;
    private Point? _fragLine;

    /// <summary>The screen height of a depth below the vent's plain (inside the cone, above the plain, depths are 0).</summary>
    private static double DepthY(EruptionLayout l, double km) => km <= 0 ? l.Vent.Y : Math.Max(l.Vent.Y, l.Ground + (km * l.KmDown));

    private static double YDepth(EruptionLayout l, double y) => Math.Max(0, (y - l.Ground) / l.KmDown);

    private void DrawCone(PaintContext dc, EruptionLayout l, double now)
    {
        var s = l.Stage;
        var left = s.Left;
        var right = l.RulerX + 8;
        var outline = new List<Point>();
        for (var x = left; x <= right; x += 3) outline.Add(new Point(x, SurfaceY(l, x)));
        outline.Add(new Point(right, SurfaceY(l, right)));
        outline.Add(new Point(right, l.Ground + 2));
        outline.Add(new Point(left, l.Ground + 2));
        var cone = Shape(outline);

        // Layers of old eruptions inside the cone: lava and ash in turn (thin lava flows in a shield).
        dc.PushClip(cone);
        dc.DrawRectangle(FadedBrush(Ink.Rock, 1), null, new Rect(left, s.Top, right - left, l.Ground - s.Top + 3));
        var layers = l.Shape == ConeShape.Shield ? 7 : 9;
        for (var i = layers; i >= 1; i--)
        {
            var k = i / (double)layers;
            var shell = new List<Point>();
            for (var x = left; x <= right; x += 4)
            {
                var d = Math.Abs(x - l.Center) / (l.HalfWidth * k);
                shell.Add(new Point(x, l.Ground - (l.ConeHeight * k * ConeProfile(l.Shape, d))));
            }

            shell.Add(new Point(right, l.Ground + 2));
            shell.Add(new Point(left, l.Ground + 2));
            var color = l.Shape == ConeShape.Shield
                ? (i % 2 == 0 ? Color.FromRgb(0x3A, 0x2C, 0x28) : Color.FromRgb(0x4C, 0x36, 0x2E))
                : (i % 2 == 0 ? Color.FromRgb(0x6A, 0x62, 0x5C) : Color.FromRgb(0x46, 0x34, 0x2E));
            dc.DrawGeometry(FadedBrush(color, 1), null, Shape(shell));
        }

        dc.Pop();
        dc.DrawGeometry(null, Glows("cone") ? ExploreTargets.GlowPen(now) : Ink.Surface, Line(outline.Take(outline.Count - 2)));
        _targets.Add("cone", FlankPoint(l, -1, 0.42) + new Vector(0, l.ConeHeight * 0.18), Math.Max(12, l.ConeHeight * 0.18));
        _targets.Add("vent", l.Vent, Math.Max(9, l.ConduitWidth));
    }

    private void DrawLavaFlows(PaintContext dc, EruptionLayout l, EruptionPhase phase, EruptionStyle style, double share, double now)
    {
        _lavaLabel = null;
        if (style is EruptionStyle.Plinian or EruptionStyle.Dome) return;
        var logEta = VolcanoModel.LogViscosity(Silica);
        var full = Math.Clamp(Math.Log10(VolcanoModel.LavaFlowLengthKm(logEta) / 0.15) / Math.Log10(400), 0.1, 1);
        if (style == EruptionStyle.Vulcanian) full *= 0.5;
        var grown = phase switch
        {
            EruptionPhase.Erupting => VolcanoModel.SmoothStep(0.05, 0.75, share),
            EruptionPhase.Calm => 1,
            _ => 0,
        };
        if (grown <= 0) return;

        // Still glowing while the eruption feeds it; a dark crust as it cools.
        var glow = phase == EruptionPhase.Erupting ? 1 : 1 - VolcanoModel.SmoothStep(VolcanoModel.CalmStart, 0.97, Progress);
        var thickness = 3 + (Math.Clamp(logEta - 1, 0, 6) * 1.6);
        Point? labelAt = null;
        foreach (var side in style == EruptionStyle.Hawaiian ? new[] { -1, 1 } : [1])
        {
            var length = full * grown * (side == 1 ? 1 : 0.7);
            var points = new List<Point>();
            for (var t = 0.0; t <= length; t += 0.01) points.Add(FlankPoint(l, side, t) + new Vector(0, -thickness * 0.3));
            if (points.Count < 2) continue;
            var geometry = Line(points);
            dc.DrawGeometry(null, Glows("lavaflow") ? ExploreTargets.GlowPen(now, thickness + 6) : FadedPen(Ink.Crust, 1, thickness + 1.5), geometry);
            if (glow > 0.05)
            {
                dc.DrawGeometry(null, FadedPen(Ink.Lava, glow * (0.85 + (0.15 * Math.Sin(now * 5))), thickness), geometry);
                dc.DrawGeometry(null, FadedPen(Ink.LavaHot, glow * 0.7, Math.Max(1, thickness * 0.35)), geometry);
            }

            if (side == 1) labelAt = points[points.Count * 2 / 3];
        }

        if (labelAt is { } at) _targets.Add("lavaflow", at, thickness + 6);
        _lavaLabel = labelAt;
    }

    private Point? _lavaLabel;

    private void DrawDome(PaintContext dc, EruptionLayout l, EruptionPhase phase, double share, double now)
    {
        var grown = phase switch
        {
            EruptionPhase.Erupting => VolcanoModel.SmoothStep(0, 0.6, share),
            EruptionPhase.Calm => 1 - (0.25 * VolcanoModel.SmoothStep(VolcanoModel.CalmStart, 1, Progress)),
            _ => 0.15,
        };
        var collapsed = phase == EruptionPhase.Erupting && share > 0.6 ? Math.Min(1, (share - 0.6) / 0.2) : phase == EruptionPhase.Calm ? 1 : 0;
        var r = l.HalfWidth * 0.1 * (0.4 + grown);
        var center = new Point(l.Vent.X, l.Vent.Y + (r * 0.25));
        var dome = new List<Point>();
        for (var a = 0.0; a <= Math.PI; a += Math.PI / 24)
        {
            var bite = collapsed * 0.4 * Math.Max(0, Math.Cos(a));
            var rr = r * (1 - bite) * (1 + (0.06 * Math.Sin((a * 7) + 1)));
            dome.Add(new Point(center.X + (Math.Cos(a) * rr * 1.25), center.Y - (Math.Sin(a) * rr)));
        }

        var geometry = Shape(dome);
        dc.DrawGeometry(FadedBrush(Color.FromRgb(0x4A, 0x40, 0x3C), 1), FadedPen(Ink.Crust, 1, 1.2), geometry);

        // Glowing cracks in the dome's skin.
        var crackPen = FadedPen(Ink.Lava, 0.6 + (0.3 * Math.Sin(now * 3)), 1.3);
        for (var i = 0; i < 5; i++)
        {
            var a = (0.4 + (i * 0.55)) % Math.PI;
            var from = center + new Vector(Math.Cos(a) * r * 0.2, -Math.Sin(a) * r * 0.2);
            var to = center + new Vector(Math.Cos(a) * r * 0.85 * 1.2, -Math.Sin(a) * r * 0.8);
            dc.DrawLine(crackPen, from, to);
        }

        _targets.Add("dome", center + new Vector(0, -r * 0.5), Math.Max(10, r));
    }

    private void DrawEruptionParticles(PaintContext dc, EruptionLayout l, EruptionStyle style, double now)
    {
        // Ash falling out of the umbrella.
        var umbrella = _puffs.Where(p => !p.Ground && -p.Y > TopUnits(l, style) * 0.7).ToList();
        if (style == EruptionStyle.Plinian && umbrella.Count > 8)
        {
            var pen = FadedPen(Ink.AshDark, 0.35, 1, flat: true);
            for (var i = 0; i < 60; i++)
            {
                var p = umbrella[i % umbrella.Count];
                var c = FromVent(l, p.X, p.Y);
                var fall = ((now * 0.6) + SqueezeTexture.Hash(i, 81)) % 1;
                var x = c.X + ((SqueezeTexture.Hash(i, 82) - 0.5) * p.R * l.Unit * 2);
                var y0 = c.Y + (fall * (l.Ground - c.Y));
                dc.DrawLine(pen, new Point(x, y0), new Point(x - 2, y0 + 7));
            }
        }

        foreach (var p in _puffs)
        {
            var fade = Math.Min(1, (p.Life - p.Age) / 1.2) * Math.Min(1, p.Age * 4);
            if (p.Ground)
            {
                var flank = FlankPoint(l, p.Side, Math.Min(1.25, p.Along));
                var r = p.R * l.Unit;
                dc.DrawEllipse(SoftBrush(p.Color, p.Alpha * fade), null, flank + new Vector(0, -r * 0.55), r * 1.4, r);
                continue;
            }

            var c = FromVent(l, p.X, p.Y);
            var radius = p.R * l.Unit;
            dc.DrawEllipse(SoftBrush(p.Color, p.Alpha * fade), null, c, radius, radius);
        }

        foreach (var s in _sparks)
        {
            var c = FromVent(l, s.X, s.Y);
            var heat = Math.Clamp(1 - (s.Age / 2.4), 0, 1);
            var color = s.Bomb ? Lerp(Ink.Crust, Ink.Lava, heat * 0.8) : Lerp(Ink.LavaDark, Ink.LavaHot, heat);
            var size = s.Size * (l.Unit / 60);
            dc.DrawEllipse(FadedBrush(color, 0.95), s.Bomb ? FadedPen(Ink.Lava, heat * 0.8, 1) : null, c, size + 0.8, size + 0.8);
        }
    }

    /// <summary>The ruler at the right: km above the vent for the column, km below the ground for the chamber.</summary>
    private void DrawRuler(PaintContext dc, EruptionLayout l, EruptionStyle style, EruptionPhase phase)
    {
        var x = l.RulerX + 18;
        dc.DrawLine(Ink.Faint, new Point(x, l.Stage.Top + 8), new Point(x, l.Stage.Bottom - 6));
        for (var km = 0; km <= SkyShownKm; km += 10)
        {
            var y = l.Vent.Y - (km * l.KmUp);
            if (km > 0 && y > l.Ground - 10) continue;
            dc.DrawLine(Ink.Faint, new Point(x - 4, y), new Point(x, y));
            DrawText(dc, km == 0 ? "0" : Km(km), new Point(x + 4, y), Ink.Dim, 10);
        }

        for (var km = 2; km <= DepthShownKm; km += 2)
        {
            var y = l.Ground + (km * l.KmDown);
            dc.DrawLine(Ink.Faint, new Point(x - 4, y), new Point(x, y));
            DrawText(dc, "−" + Km(km), new Point(x + 4, y), Ink.Dim, 10);
        }

        DrawText(dc, Loc("Volc_Unit_Km"), new Point(x, l.Stage.Top + 2), Ink.Dim, 10, TextAnchor.Center);
    }

    /// <summary>A pressure gauge on the chamber: how far its pressure is above the rock's.</summary>
    private void DrawGauge(PaintContext dc, EruptionLayout l, double now)
    {
        var c = l.Chamber;
        var r = Math.Clamp(c.Height * 0.32, 11, 20);
        var center = new Point(c.Right + r + 10, c.Top + (c.Height / 2));
        if (center.X + r > l.RulerX) center = new Point(c.Left - r - 10, center.Y);
        dc.DrawEllipse(FadedBrush(Color.FromRgb(0x14, 0x18, 0x22), 1), FadedPen(Ink.Dim, 0.9, 1.5), center, r, r);
        var pressure = VolcanoModel.Overpressure(Progress);
        for (var i = 0; i <= 4; i++)
        {
            var a = Math.PI * (1.25 - (i * 0.375));
            dc.DrawLine(FadedPen(i == 4 ? Ink.Red : Ink.Dim, 0.9, 1), center + new Vector(Math.Cos(a) * r * 0.72, -Math.Sin(a) * r * 0.72),
                center + new Vector(Math.Cos(a) * r * 0.9, -Math.Sin(a) * r * 0.9));
        }

        var angle = Math.PI * (1.25 - (1.5 * pressure / 20));
        dc.DrawLine(FadedPen(pressure > 15 ? Ink.Red : Ink.Gold, 1, 1.8), center, center + new Vector(Math.Cos(angle) * r * 0.8, -Math.Sin(angle) * r * 0.8));
        _gaugeAt = center;
        _gaugeRadius = r;
        _targets.Add("gauge", center, r);
    }

    private Point _gaugeAt;
    private double _gaugeRadius;

    private void DrawEruptionLabels(PaintContext dc, EruptionLayout l, EruptionPhase phase, EruptionStyle style, double share, double now)
    {
        var top = TopUnits(l, style);
        var topY = l.Vent.Y - (top * l.Unit);
        var erupting = phase == EruptionPhase.Erupting;

        // The two depths on the conduit: pills on its right (the froth's on the left if the two would touch).
        var c0 = l.Chamber;
        var conduitLabelY = c0.Top - ((c0.Top - l.Ground) * 0.35);
        var pillRight = l.Center + (l.ConduitWidth * 2.5) + 74;
        var pillLeft = l.Center - (l.ConduitWidth * 2.5) - 74;
        var satShown = ShowLevels && _satLine.Y > l.Vent.Y + 6;
        if (satShown)
        {
            dc.DrawLine(FadedPen(Ink.Cyan, 0.8, 1.2, dashed: true), new Point(_satLine.X - (l.ConduitWidth * 2.5), _satLine.Y), new Point(_satLine.X + (l.ConduitWidth * 2.5), _satLine.Y));
            DrawTargetPill(dc, LocFormat("Volc_Label_SatLevel", Km(VolcanoModel.SaturationDepth(Silica, Water))), new Point(pillRight, _satLine.Y), Ink.Cyan, "satlevel", 10.5);
        }

        if (ShowLevels && _fragLine is { } frag && frag.Y > l.Vent.Y + 4)
        {
            dc.DrawLine(FadedPen(Ink.Pink, 0.85, 1.2, dashed: true), new Point(frag.X - (l.ConduitWidth * 2.5), frag.Y), new Point(frag.X + (l.ConduitWidth * 2.5), frag.Y));
            var onLeft = satShown && Math.Abs(frag.Y - _satLine.Y) < 24;
            DrawTargetPill(dc, LocFormat("Volc_Label_FragLevel", Number(VolcanoModel.FragmentationDepth(Silica, Water)!.Value * 1000, "0")),
                new Point(onLeft ? pillLeft : pillRight, frag.Y), Ink.Pink, "fraglevel", 10.5);
        }

        // The eruption's things, where they are.
        if (erupting)
        {
            switch (style)
            {
                case EruptionStyle.Hawaiian:
                    _targets.Add("fountain", new Point(l.Vent.X, (l.Vent.Y + topY) / 2), Math.Max(12, (l.Vent.Y - topY) / 3));
                    if (ShowLabels) DrawTargetPill(dc, LocFormat("Volc_Label_Fountain", Number(VolcanoModel.EruptionHeightKm(style, Water) * 1000, "0")), new Point(l.Vent.X + 74, topY + 10), Ink.LavaHot, "fountain");
                    break;

                case EruptionStyle.Strombolian:
                case EruptionStyle.Vulcanian:
                {
                    var bomb = _sparks.Where(s => s.Bomb).Select(s => FromVent(l, s.X, s.Y)).FirstOrDefault();
                    if (bomb != default) _targets.Add("bombs", bomb, 12);
                    if (ShowLabels) DrawTargetPill(dc, Loc("Volc_Obj_Bombs"), new Point(l.Vent.X - 80, l.Vent.Y - (l.ConeHeight * 0.5) - 20), Ink.Orange, "bombs");
                    if (style == EruptionStyle.Vulcanian && ShowLabels)
                    {
                        DrawTargetPill(dc, LocFormat("Volc_Label_Column", Km(VolcanoModel.EruptionHeightKm(style, Water))), new Point(l.Vent.X + 70, topY), Ink.Steam, "column");
                    }

                    break;
                }

                case EruptionStyle.Plinian:
                {
                    var columnMid = new Point(l.Vent.X, (l.Vent.Y + topY) / 2);
                    _targets.Add("column", columnMid, Math.Max(14, l.Unit * 0.8));
                    if (Glows("column")) dc.DrawLine(ExploreTargets.GlowPen(now, 6), new Point(l.Vent.X, l.Vent.Y - 6), new Point(l.Vent.X, topY));
                    var umbrellaAt = new Point(l.Vent.X + (l.Unit * 2.2), topY + (l.Unit * 0.2));
                    _targets.Add("umbrella", umbrellaAt, l.Unit * 0.9);
                    var ashAt = new Point(l.Vent.X + (l.Unit * 3.2), (topY + l.Ground) / 2);
                    _targets.Add("ashfall", ashAt, l.Unit * 0.7);
                    if (ShowLabels)
                    {
                        DrawTargetPill(dc, LocFormat("Volc_Label_Column", Km(VolcanoModel.EruptionHeightKm(style, Water))), new Point(l.Vent.X - 92, (l.Vent.Y + topY) / 2), Ink.Steam, "column");
                        DrawTargetPill(dc, Loc("Volc_Obj_Umbrella"), umbrellaAt + new Vector(0, -l.Unit * 0.6), Ink.Steam, "umbrella");
                        DrawTargetPill(dc, Loc("Volc_Obj_AshFall"), ashAt, Ink.Dim, "ashfall");
                    }

                    break;
                }
            }
        }

        // A pyroclastic flow, once one runs.
        var flow = _puffs.Where(p => p.Ground && p.Age > 0.4).OrderByDescending(p => p.Along).FirstOrDefault();
        if (flow is not null)
        {
            var at = FlankPoint(l, flow.Side, Math.Min(1.1, flow.Along * 0.8));
            _targets.Add("pyroflow", at + new Vector(0, -10), 16);
            if (Glows("pyroflow")) dc.DrawEllipse(null, ExploreTargets.GlowPen(now, 4), at + new Vector(0, -10), 26, 16);
            if (ShowLabels) DrawTargetPill(dc, Loc("Volc_Obj_PyroFlow"), at + new Vector(flow.Side * 40, -34), Ink.Orange, "pyroflow");
        }

        if (!ShowLabels) return;
        var c = l.Chamber;
        DrawTargetPill(dc, Loc("Volc_Obj_Chamber"), new Point(c.Left + (c.Width / 2), c.Bottom + 12), Ink.LavaHot, "chamber");
        DrawTargetPill(dc, Loc("Volc_Obj_Conduit"), new Point(l.Center - (l.ConduitWidth * 2) - 46, conduitLabelY), Ink.Orange, "conduit", 10.5);
        DrawTargetPill(dc, Loc("Volc_Obj_Vent"), new Point(l.Vent.X - 56, l.Vent.Y - 12), Ink.Text, "vent", 10.5);
        var coneKey = l.Shape switch
        {
            ConeShape.Shield => "Volc_Label_Shield",
            ConeShape.Caldera => "Volc_Label_Caldera",
            _ => "Volc_Label_Strato",
        };
        DrawTargetPill(dc, Loc(coneKey), FlankPoint(l, -1, 0.62) + new Vector(-20, -18), Ink.Dim, "cone", 10.5);
        if (_lavaLabel is { } lava) DrawTargetPill(dc, Loc("Volc_Obj_LavaFlow"), lava + new Vector(10, -22), Ink.Lava, "lavaflow", 10.5);
        if (style == EruptionStyle.Dome && phase is EruptionPhase.Erupting or EruptionPhase.Calm)
        {
            DrawTargetPill(dc, Loc("Volc_Obj_Dome"), new Point(l.Vent.X + 60, l.Vent.Y - (l.HalfWidth * 0.12) - 10), Ink.Orange, "dome", 10.5);
        }

        if (_quakeSpot is { } quake) DrawTargetPill(dc, Loc("Volc_Obj_Quakes"), quake + new Vector(40, -24), Ink.Gold, "quakes", 10.5);
        DrawTargetPill(dc, LocFormat("Volc_Label_Gauge", Number(VolcanoModel.Overpressure(Progress), "0")), _gaugeAt + new Vector(0, _gaugeRadius + 13), Ink.Dim, "gauge", 10);
        if (_bubbleSpot is { } bubble && _satLine.Y > MagmaTopY(l, phase) + 10)
        {
            // On the left, clear of the conduit's own label.
            var y = Math.Abs(bubble.Y - conduitLabelY) < 24 ? conduitLabelY - 26 : bubble.Y;
            DrawTargetPill(dc, Loc("Volc_Obj_Bubbles"), new Point(l.Center - (l.ConduitWidth * 2) - 46, y), Color.FromRgb(0xFF, 0xF1, 0xC8), "bubbles", 10.5);
        }
    }

    #endregion
}
