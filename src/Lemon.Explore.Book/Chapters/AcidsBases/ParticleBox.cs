using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.AcidsBases;

/// <summary>The particles a magnifier shows.</summary>
public enum ParticleKind
{
    Water,
    Hydronium,
    Hydroxide,
    Chloride,
    Sodium,
    AcidMolecule,
    AcidIon,
    BaseMolecule,
    BaseIon,
}

/// <summary>
/// The particles in a magnifier: water molecules jostling, and the ions and molecules of what is dissolved, drawn in
/// proportion to one another (see <see cref="CountsFor"/>). When a solution changes, particles fade in and out one at a
/// time, so adding a drop shows ions arriving and H₃O⁺ meeting OH⁻ shows them turning into water. In water with almost no
/// ions, two water molecules now and then swap a proton for a moment, as they really do (one in about 550 million).
/// </summary>
internal sealed class ParticleBox
{
    /// <summary>The most particles of one kind drawn: the commonest kind gets this many.</summary>
    public const int MostDrawn = 16;

    /// <summary>The smallest amount per litre one drawn particle stands for, so a trace of ions shows as none.</summary>
    public const double SmallestUnit = 1e-5;

    private const int WaterCount = 64;

    private readonly Random _random = new(7);
    private readonly List<Particle> _particles = [];
    private readonly Dictionary<ParticleKind, int> _target = [];
    private double _time;
    private double _nextSwap = 1.5;
    private double _sinceChange;

    /// <summary>What each drawn particle stands for, mol/L.</summary>
    public double Unit { get; private set; } = SmallestUnit;

    /// <summary>Two water molecules are swapping a proton for a moment.</summary>
    public bool IsSwapping => _particles.Any(p => p.SwapUntil > _time);

    /// <summary>Where a short-lived swap happens (unit disk), for its label.</summary>
    public Point? SwapAt => _particles.FirstOrDefault(p => p.SwapUntil > _time && p.Kind == ParticleKind.Hydronium)?.Position;

    /// <summary>
    /// How many of each kind to draw for <paramref name="s"/>: the commonest dissolved kind gets <see cref="MostDrawn"/>,
    /// the others in proportion, each particle standing for at least <see cref="SmallestUnit"/> mol/L.
    /// </summary>
    public static (Dictionary<ParticleKind, int> Counts, double Unit) CountsFor(Species s)
    {
        var amounts = new Dictionary<ParticleKind, double>
        {
            [ParticleKind.Hydronium] = s.Hydronium,
            [ParticleKind.Hydroxide] = s.Hydroxide,
            [ParticleKind.Chloride] = s.Chloride,
            [ParticleKind.Sodium] = s.Sodium,
            [ParticleKind.AcidMolecule] = s.AcidMolecules,
            [ParticleKind.AcidIon] = s.AcidIons,
            [ParticleKind.BaseMolecule] = s.BaseMolecules,
            [ParticleKind.BaseIon] = s.BaseIons,
        };
        var unit = Math.Max(amounts.Values.Max() / MostDrawn, SmallestUnit);
        var counts = amounts.ToDictionary(a => a.Key, a => (int)Math.Round(a.Value / unit));
        counts[ParticleKind.Water] = WaterCount;
        return (counts, unit);
    }

    /// <summary>Sets what should be drawn; the particles follow over the next moments.</summary>
    public void SetTarget(Species species)
    {
        var (counts, unit) = CountsFor(species);
        Unit = unit;
        var changed = false;
        foreach (var (kind, count) in counts)
        {
            if (_target.TryGetValue(kind, out var old) && old == count) continue;
            _target[kind] = count;
            changed = true;
        }

        if (changed) _sinceChange = 0;
    }

    /// <summary>Moves everything on by <paramref name="dt"/> seconds and brings the counts towards the target one particle at a time.</summary>
    public void Step(double dt)
    {
        _time += dt;
        _sinceChange += dt;
        foreach (var p in _particles)
        {
            p.Fade = p.Leaving ? Math.Max(0, p.Fade - (dt * 2.2)) : Math.Min(1, p.Fade + (dt * 2.5));
            p.Velocity += new Vector(Gaussian() * 0.9 * dt, Gaussian() * 0.9 * dt);
            p.Velocity *= Math.Exp(-1.6 * dt);
            p.Position += p.Velocity * dt;
            p.Angle += p.Spin * dt;
            var r = ((Vector)p.Position).Length;
            if (r > 0.9)
            {
                var n = (Vector)p.Position / r;
                p.Position = (Point)(n * 0.9);
                var away = Vector.Dot(p.Velocity, n);
                if (away > 0) p.Velocity -= 2 * away * n;
            }

            if (p.SwapUntil > 0 && p.SwapUntil <= _time)
            {
                // The swapped proton goes back: both are water again.
                p.SwapUntil = 0;
                p.Kind = ParticleKind.Water;
                p.Flash = 0.5;
            }

            p.Flash = Math.Max(0, p.Flash - dt);
        }

        _particles.RemoveAll(p => p.Leaving && p.Fade <= 0);
        Reconcile();
        SwapNow();
    }

    /// <summary>Adds or removes one particle of each kind that is off its target, a few times a second.</summary>
    private void Reconcile()
    {
        // Right after a change, quickly; then the rest at a steadier pace.
        var pace = _sinceChange < 0.6 ? 1 : 3;
        foreach (var (kind, want) in _target)
        {
            // A water molecule swapping a proton for a moment still counts as water.
            var have = _particles.Count(p => !p.Leaving && (p.SwapUntil > 0 ? kind == ParticleKind.Water : p.Kind == kind));
            for (var i = 0; i < pace && have < want; i++, have++) Spawn(kind);
            for (var i = 0; i < pace && have > want; i++, have--) Remove(kind);
        }
    }

    private void Spawn(ParticleKind kind)
    {
        var angle = _random.NextDouble() * Math.PI * 2;
        var radius = 0.85 * Math.Sqrt(_random.NextDouble());
        _particles.Add(new Particle
        {
            Kind = kind,
            Position = new Point(radius * Math.Cos(angle), radius * Math.Sin(angle)),
            Velocity = new Vector(Gaussian() * 0.08, Gaussian() * 0.08),
            Angle = _random.NextDouble() * Math.PI * 2,
            Spin = Gaussian() * 0.8,
            Fade = kind == ParticleKind.Water && _particles.Count < WaterCount ? 1 : 0,
        });
    }

    private void Remove(ParticleKind kind)
    {
        var p = _particles.LastOrDefault(p => p.Kind == kind && !p.Leaving && p.SwapUntil == 0);
        if (p is null) return;
        p.Leaving = true;

        // H₃O⁺ and OH⁻ that go away have met each other and become water: a flash where they were.
        if (kind is ParticleKind.Hydronium or ParticleKind.Hydroxide) p.Flash = 0.6;
    }

    /// <summary>In nearly pure water, every so often two neighbouring water molecules swap a proton for a moment.</summary>
    private void SwapNow()
    {
        if (_time < _nextSwap) return;
        _nextSwap = _time + 2.6 + (_random.NextDouble() * 1.5);
        var ions = _target.GetValueOrDefault(ParticleKind.Hydronium) + _target.GetValueOrDefault(ParticleKind.Hydroxide);
        if (ions > 0) return;

        var waters = _particles.Where(p => p.Kind == ParticleKind.Water && !p.Leaving && p.Fade >= 1 && ((Vector)p.Position).Length < 0.7).ToList();
        if (waters.Count < 2) return;
        var a = waters[_random.Next(waters.Count)];
        var b = waters.Where(w => w != a).MinBy(w => ((Vector)(w.Position - a.Position)).Length)!;
        a.Kind = ParticleKind.Hydronium;
        b.Kind = ParticleKind.Hydroxide;
        a.SwapUntil = b.SwapUntil = _time + 1.4;
        a.Flash = b.Flash = 0.4;
    }

    private double Gaussian()
    {
        var u = 1 - _random.NextDouble();
        var v = _random.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v);
    }

    /// <summary>Draws the particles into a circle at <paramref name="center"/> of <paramref name="radius"/>, water first.</summary>
    public void Draw(PaintContext dc, Point center, double radius)
    {
        var size = radius / 21;
        foreach (var p in _particles.Where(p => p.Kind == ParticleKind.Water)) DrawOne(dc, p, center, radius, size);
        foreach (var p in _particles.Where(p => p.Kind != ParticleKind.Water)) DrawOne(dc, p, center, radius, size);
    }

    /// <summary>The drawn particles of <paramref name="kind"/> that are fully there, in the circle's coordinates.</summary>
    public IEnumerable<Point> PlacesOf(ParticleKind kind, Point center, double radius) =>
        _particles.Where(p => p.Kind == kind && !p.Leaving && p.Fade > 0.8).Select(p => center + ((Vector)p.Position * radius));

    private static void DrawOne(PaintContext dc, Particle p, Point center, double radius, double size)
    {
        var at = center + ((Vector)p.Position * radius);
        var alpha = p.Fade;
        if (alpha <= 0.02) return;
        if (alpha < 0.98) dc.PushOpacity(alpha);
        if (p.Flash > 0)
        {
            var glow = p.Flash / 0.6;
            dc.DrawEllipse(null, Glow, at, size * (2.4 + (2 * (1 - glow))), size * (2.4 + (2 * (1 - glow))));
        }

        DrawGlyph(dc, p.Kind, at, size, p.Angle);
        if (alpha < 0.98) dc.Pop();
    }

    /// <summary>One particle's ball-and-stick picture at <paramref name="at"/>; also used by the legend.</summary>
    public static void DrawGlyph(PaintContext dc, ParticleKind kind, Point at, double size, double angle)
    {
        Point Around(double r, double a) => at + new Vector(r * Math.Cos(angle + a), r * Math.Sin(angle + a));
        switch (kind)
        {
            case ParticleKind.Water:
                dc.DrawEllipse(WaterH, null, Around(size * 1.05, -0.95), size * 0.5, size * 0.5);
                dc.DrawEllipse(WaterH, null, Around(size * 1.05, 0.95), size * 0.5, size * 0.5);
                dc.DrawEllipse(WaterO, null, at, size * 0.85, size * 0.85);
                break;

            case ParticleKind.Hydronium:
                for (var i = 0; i < 3; i++) dc.DrawEllipse(Hydrogen, null, Around(size * 1.2, i * 2.094), size * 0.55, size * 0.55);
                dc.DrawEllipse(HydroniumO, RimPen, at, size, size);
                Charge(dc, at + new Vector(size * 1.3, -size * 1.3), size, plus: true);
                break;

            case ParticleKind.Hydroxide:
                dc.DrawEllipse(Hydrogen, null, Around(size * 1.15, 0), size * 0.55, size * 0.55);
                dc.DrawEllipse(HydroxideO, RimPen, at, size, size);
                Charge(dc, at + new Vector(-size * 1.2, -size * 1.3), size, plus: false);
                break;

            case ParticleKind.Chloride:
                dc.DrawEllipse(ChlorideBall, RimPen, at, size * 1.3, size * 1.3);
                Charge(dc, at + new Vector(size * 1.5, -size * 1.4), size, plus: false);
                break;

            case ParticleKind.Sodium:
                dc.DrawEllipse(SodiumBall, RimPen, at, size * 0.9, size * 0.9);
                Charge(dc, at + new Vector(size * 1.2, -size * 1.2), size, plus: true);
                break;

            case ParticleKind.AcidMolecule:
            case ParticleKind.AcidIon:
            {
                // CH₃–C(=O)–O(H): two carbons, two oxygens, and the acid's proton on one oxygen unless it gave it away.
                var c1 = Around(size * 1.1, Math.PI);
                var c2 = at;
                var o1 = Around(size * 1.2, -1.0);
                var o2 = Around(size * 1.2, 1.0);
                dc.DrawLine(BondPen(size), c1, c2);
                dc.DrawLine(BondPen(size), c2, o1);
                dc.DrawLine(BondPen(size), c2, o2);
                dc.DrawEllipse(Carbon, null, c1, size * 0.8, size * 0.8);
                dc.DrawEllipse(Carbon, null, c2, size * 0.75, size * 0.75);
                dc.DrawEllipse(AcidO, null, o1, size * 0.7, size * 0.7);
                dc.DrawEllipse(AcidO, null, o2, size * 0.7, size * 0.7);
                if (kind == ParticleKind.AcidMolecule) dc.DrawEllipse(Hydrogen, null, o2 + ((o2 - c2) * 0.75), size * 0.45, size * 0.45);
                else Charge(dc, o2 + new Vector(size * 1.1, -size * 0.9), size, plus: false);
                break;
            }

            case ParticleKind.BaseMolecule:
            case ParticleKind.BaseIon:
            {
                var count = kind == ParticleKind.BaseIon ? 4 : 3;
                for (var i = 0; i < count; i++) dc.DrawEllipse(Hydrogen, null, Around(size * 1.15, (i * Math.PI * 2 / count) + 0.3), size * 0.5, size * 0.5);
                dc.DrawEllipse(Nitrogen, kind == ParticleKind.BaseIon ? RimPen : null, at, size * 0.9, size * 0.9);
                if (kind == ParticleKind.BaseIon) Charge(dc, at + new Vector(size * 1.4, -size * 1.4), size, plus: true);
                break;
            }
        }
    }

    private static void Charge(PaintContext dc, Point at, double size, bool plus)
    {
        var half = Math.Max(2, size * 0.42);
        var pen = plus ? PlusPen : MinusPen;
        dc.DrawLine(pen, at - new Vector(half, 0), at + new Vector(half, 0));
        if (plus) dc.DrawLine(pen, at - new Vector(0, half), at + new Vector(0, half));
    }

    private static readonly Dictionary<int, IPen> BondPens = [];

    private static IPen BondPen(double size)
    {
        var key = (int)Math.Round(size * 4);
        if (BondPens.TryGetValue(key, out var pen)) return pen;
        pen = MakePen(Solid(200, 0xC9, 0xD2, 0xDE), Math.Max(1, size * 0.35));
        BondPens[key] = pen;
        return pen;
    }

    /// <summary>The colour each particle kind is known by on the page (legend, lists).</summary>
    public static Color ColorOf(ParticleKind kind) => kind switch
    {
        ParticleKind.Hydronium => Color.FromRgb(0xFF, 0x5A, 0x5F),
        ParticleKind.Hydroxide => Color.FromRgb(0x5A, 0xA8, 0xFF),
        ParticleKind.Chloride => Color.FromRgb(0x7C, 0xFF, 0x9E),
        ParticleKind.Sodium => Color.FromRgb(0xB0, 0x8C, 0xFF),
        ParticleKind.AcidMolecule or ParticleKind.AcidIon => Color.FromRgb(0xFF, 0xD3, 0x6B),
        ParticleKind.BaseMolecule or ParticleKind.BaseIon => Color.FromRgb(0x6F, 0xD8, 0xFF),
        _ => Color.FromRgb(0x8F, 0xB4, 0xD8),
    };

    private static readonly IBrush WaterO = Solid(150, 0x6F, 0x9C, 0xC8);
    private static readonly IBrush WaterH = Solid(120, 0xDD, 0xE6, 0xF0);
    private static readonly IBrush Hydrogen = Solid(255, 0xF2, 0xF4, 0xF8);
    private static readonly IBrush HydroniumO = Solid(255, 0xFF, 0x5A, 0x5F);
    private static readonly IBrush HydroxideO = Solid(255, 0x5A, 0xA8, 0xFF);
    private static readonly IBrush ChlorideBall = Solid(255, 0x5E, 0xD8, 0x7E);
    private static readonly IBrush SodiumBall = Solid(255, 0xB0, 0x8C, 0xFF);
    private static readonly IBrush Carbon = Solid(255, 0x8C, 0x94, 0xA4);
    private static readonly IBrush AcidO = Solid(255, 0xFF, 0xC2, 0x4A);
    private static readonly IBrush Nitrogen = Solid(255, 0x4F, 0xC8, 0xF0);
    private static readonly IPen RimPen = MakePen(Solid(220, 255, 255, 255), 1.1);
    private static readonly IPen PlusPen = MakePen(Solid(255, 0xFF, 0xE0, 0xA0), 1.6);
    private static readonly IPen MinusPen = MakePen(Solid(255, 0xBF, 0xE4, 0xFF), 1.6);
    private static readonly IPen Glow = MakePen(Solid(200, 0xFF, 0xD3, 0x6B), 2);

    private sealed class Particle
    {
        public ParticleKind Kind;
        public Point Position;
        public Vector Velocity;
        public double Angle;
        public double Spin;
        public double Fade;
        public bool Leaving;
        public double Flash;

        /// <summary>A water molecule turned ion for a moment until this time (0: a lasting particle).</summary>
        public double SwapUntil;
    }
}
