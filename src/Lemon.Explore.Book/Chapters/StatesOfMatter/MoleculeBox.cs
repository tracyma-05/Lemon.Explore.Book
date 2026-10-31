using Avalonia;
using Avalonia.Media;
using Lemon.Explore.Book.Controls.Space;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.StatesOfMatter;

/// <summary>
/// The molecules in a container: a solid's sit in a lattice and only shiver, a liquid's touch and slide past one another
/// at the bottom, a gas's fly apart and fill the space up to the lid. Asked for another mix of states
/// (<see cref="SetTarget"/>), molecules change one at a time where the states meet, so melting starts at the ice's
/// surface and boiling takes molecules off the top of the liquid. Ice floats on its water; dry ice sinks.
/// </summary>
/// <remarks>
/// Coordinates are in container widths: x from 0 to 1, y up from the floor. The owner sets the container's
/// <see cref="Ceiling"/> (the piston or the top of the window) each frame. Speeds grow with the square root of the
/// temperature, as the molecules' do.
/// </remarks>
internal sealed class MoleculeBox
{
    public const int Count = 40;

    /// <summary>A molecule's radius.</summary>
    public const double Radius = 0.03;

    /// <summary>The lattice spacing: molecules in a solid nearly touch.</summary>
    private const double Spacing = Radius * 2.15;

    private const double RowStep = Spacing * 0.866;

    /// <summary>Molecules that change state each second while the mix is off its target.</summary>
    private const double ChangesPerSecond = 16;

    private readonly Random _random = new(11);
    private readonly List<Molecule> _molecules = [];
    private readonly Point[] _sites;
    private readonly bool[] _taken;
    private double _changeDebt;

    public MoleculeBox()
    {
        _sites = Sites();
        _taken = new bool[_sites.Length];
        for (var i = 0; i < Count; i++)
        {
            var m = new Molecule { State = MatterState.Solid, Site = i, Angle = 0 };
            _taken[i] = true;
            m.Position = _sites[i];
            _molecules.Add(m);
        }

        Target = (Count, 0, 0);
    }

    /// <summary>What the molecules are made of (the glyph, and whether the solid floats on the liquid).</summary>
    public MatterSubstance Substance { get; set; }

    /// <summary>The temperature, K: how fast everything moves.</summary>
    public double Temperature { get; set; } = 250;

    /// <summary>The lid's height in container widths (the piston, or the top of the window).</summary>
    public double Ceiling { get; set; } = 0.8;

    /// <summary>Beyond the critical point: the fluid fills the container, dense, with no surface.</summary>
    public bool Supercritical { get; set; }

    /// <summary>How many molecules should be solid, liquid and gas.</summary>
    public (int Solid, int Liquid, int Gas) Target { get; private set; }

    public int SolidCount => _molecules.Count(m => m.State == MatterState.Solid);

    public int LiquidCount => _molecules.Count(m => m.State == MatterState.Liquid);

    public int GasCount => _molecules.Count(m => m.State == MatterState.Gas);

    /// <summary>The top of the solid and liquid, container widths (0 when everything is gas).</summary>
    public double CondensedTop { get; private set; }

    /// <summary>Sets the mix to aim for from fractions of solid, liquid and gas.</summary>
    public void SetTarget(double solid, double liquid, double gas)
    {
        var total = Math.Max(1e-9, solid + liquid + gas);
        var s = (int)Math.Round(solid / total * Count);
        var g = (int)Math.Round(gas / total * Count);
        s = Math.Clamp(s, 0, Count);
        g = Math.Clamp(g, 0, Count - s);
        Target = (s, Count - s - g, g);
    }

    /// <summary>Everything in one state at once, as if it had always been (a page opened, a walkthrough's jump).</summary>
    public void Settle()
    {
        for (var guard = 0; guard < Count * 3 && !OnTarget; guard++) ChangeOne();
        foreach (var m in _molecules)
        {
            m.Flash = 0;
            if (m.State == MatterState.Solid) m.Position = SitePosition(m.Site);
        }
    }

    private bool OnTarget => SolidCount == Target.Solid && LiquidCount == Target.Liquid && GasCount == Target.Gas;

    #region moving

    /// <summary>Moves everything on by <paramref name="dt"/> seconds and changes a few molecules towards the target.</summary>
    public void Step(double dt)
    {
        if (dt <= 0) return;
        _changeDebt = OnTarget ? 0 : _changeDebt + (dt * ChangesPerSecond);
        while (_changeDebt >= 1 && !OnTarget)
        {
            ChangeOne();
            _changeDebt -= 1;
        }

        var heat = Math.Sqrt(Math.Max(20, Temperature) / 300);
        var (liquidBottom, liquidTop, solidBase) = Layers();
        CondensedTop = Math.Max(liquidTop, SolidCount > 0 ? solidBase + SolidHeight() : 0);
        var ceiling = Math.Max(Ceiling, CondensedTop + (Radius * 3));

        foreach (var m in _molecules)
        {
            switch (m.State)
            {
                case MatterState.Solid:
                    StepSolid(m, dt, heat);
                    break;

                case MatterState.Liquid:
                    StepLiquid(m, dt, heat, liquidBottom, liquidTop);
                    break;

                default:
                    StepGas(m, dt, heat, ceiling);
                    break;
            }
        }

        Repel(dt);
    }

    /// <summary>A solid's molecule shivers about its place in the lattice (and glides there if it has just frozen).</summary>
    private void StepSolid(Molecule m, double dt, double heat)
    {
        m.Jitter += new Vector(Gaussian(), Gaussian()) * (0.05 * heat * Math.Sqrt(dt));
        m.Jitter *= Math.Exp(-12 * dt);
        var home = SitePosition(m.Site) + m.Jitter;
        m.Position += (home - m.Position) * Math.Min(1, dt * 7);
        m.Velocity = default;
        m.Angle += (Math.Sin(m.Site * 1.7) * 0.25 - m.Angle) * Math.Min(1, dt * 5);
    }

    /// <summary>A liquid's molecule wanders among its neighbours below the surface.</summary>
    private void StepLiquid(Molecule m, double dt, double heat, double bottom, double top)
    {
        m.Velocity += new Vector(Gaussian(), Gaussian()) * (0.9 * heat * Math.Sqrt(dt));
        m.Velocity += new Vector(0, -0.6 * dt);
        m.Velocity *= Math.Exp(-4 * dt);
        m.Position += m.Velocity * dt;
        m.Angle += m.Spin * heat * dt;
        Keep(m, bottom + Radius, Math.Max(bottom + Radius, top - Radius));
    }

    /// <summary>A gas's molecule flies straight until it hits a wall, the lid or the surface below.</summary>
    private void StepGas(Molecule m, double dt, double heat, double ceiling)
    {
        var speed = (Supercritical ? 0.35 : 0.55) * heat;
        var v = m.Velocity.Length;
        if (v < 1e-6) m.Velocity = new Vector(Gaussian(), Math.Abs(Gaussian()) + 0.5) * speed;
        else m.Velocity *= 1 + ((speed / v) - 1) * Math.Min(1, dt * 2);
        m.Position += m.Velocity * dt;
        m.Angle += m.Spin * heat * 3 * dt;
        var floor = Supercritical ? 0 : CondensedTop;
        Keep(m, floor + Radius, ceiling - Radius);
    }

    /// <summary>Keeps a molecule between the walls and between two heights, bouncing it back in.</summary>
    private static void Keep(Molecule m, double low, double high)
    {
        if (m.Position.X < Radius)
        {
            m.Position = m.Position.WithX(Radius);
            m.Velocity = m.Velocity.WithX(Math.Abs(m.Velocity.X));
        }
        else if (m.Position.X > 1 - Radius)
        {
            m.Position = m.Position.WithX(1 - Radius);
            m.Velocity = m.Velocity.WithX(-Math.Abs(m.Velocity.X));
        }

        if (m.Position.Y < low)
        {
            m.Position = m.Position.WithY(low);
            m.Velocity = m.Velocity.WithY(Math.Abs(m.Velocity.Y));
        }
        else if (m.Position.Y > high)
        {
            m.Position = m.Position.WithY(high);
            m.Velocity = m.Velocity.WithY(-Math.Abs(m.Velocity.Y));
        }
    }

    /// <summary>Liquid molecules (and a supercritical fluid's) push apart when they overlap, so they touch but do not pile up.</summary>
    private void Repel(double dt)
    {
        var loose = _molecules.Where(m => m.State == MatterState.Liquid || (Supercritical && m.State == MatterState.Gas)).ToList();
        var touch = Radius * 2;
        for (var i = 0; i < loose.Count; i++)
        {
            for (var j = i + 1; j < loose.Count; j++)
            {
                Vector apart = loose[j].Position - loose[i].Position;
                var d = apart.Length;
                if (d >= touch || d < 1e-9) continue;
                var push = apart / d * ((touch - d) * 0.5 * Math.Min(1, dt * 20));
                loose[i].Position -= push;
                loose[j].Position += push;
            }
        }
    }

    #endregion

    #region changing state

    /// <summary>The liquid's floor and surface, and where the solid's lattice starts: on the floor, or for ice on top of the water.</summary>
    private (double LiquidBottom, double LiquidTop, double SolidBase) Layers()
    {
        var liquidHeight = LiquidCount * (Spacing * Spacing * 0.95);
        if (Substance == MatterSubstance.Water || SolidCount == 0) return (0, liquidHeight, liquidHeight);
        var solidTop = SolidHeight();
        return (solidTop, solidTop + liquidHeight, 0);
    }

    /// <summary>The height of the rows of the lattice in use.</summary>
    private double SolidHeight()
    {
        var top = -1;
        for (var i = 0; i < _sites.Length; i++)
        {
            if (_taken[i]) top = Math.Max(top, Row(i));
        }

        return top < 0 ? 0 : ((top + 1) * RowStep) + (Radius * 0.3);
    }

    private Point SitePosition(int site) => _sites[site] + new Vector(0, Layers().SolidBase);

    /// <summary>Changes one molecule towards the target, where the two states meet.</summary>
    private void ChangeOne()
    {
        var (s, l, g) = Target;
        var solid = SolidCount;
        var liquid = LiquidCount;
        var gas = GasCount;
        var floats = Substance == MatterSubstance.Water;

        if (solid > s)
        {
            // Melting or subliming at the solid's open face: the bottom of floating ice, else the top.
            var m = _molecules.Where(x => x.State == MatterState.Solid)
                .OrderBy(x => floats && liquid > 0 ? x.Position.Y : -x.Position.Y).First();
            Release(m, liquid < l ? MatterState.Liquid : MatterState.Gas);
        }
        else if (gas > g)
        {
            // Condensing or freezing out of the gas: the molecule lowest down.
            var m = _molecules.Where(x => x.State == MatterState.Gas).OrderBy(x => x.Position.Y).First();
            if (liquid < l) Become(m, MatterState.Liquid);
            else Freeze(m);
        }
        else if (liquid > l)
        {
            if (gas < g)
            {
                // Boiling: off the top of the liquid, upwards.
                var m = _molecules.Where(x => x.State == MatterState.Liquid).OrderByDescending(x => x.Position.Y).First();
                Become(m, MatterState.Gas);
                m.Velocity = new Vector(Gaussian() * 0.2, 0.6);
            }
            else
            {
                // Freezing: the liquid next to the solid.
                var m = _molecules.Where(x => x.State == MatterState.Liquid)
                    .OrderBy(x => floats ? -x.Position.Y : x.Position.Y).First();
                Freeze(m);
            }
        }
    }

    private void Release(Molecule m, MatterState to)
    {
        _taken[m.Site] = false;
        m.Site = -1;
        Become(m, to);
        m.Velocity = new Vector(Gaussian() * 0.1, to == MatterState.Gas ? 0.5 : 0);
    }

    private void Freeze(Molecule m)
    {
        var site = Array.IndexOf(_taken, false);
        if (site < 0) return;
        _taken[site] = true;
        m.Site = site;
        m.Jitter = default;
        Become(m, MatterState.Solid);
    }

    private void Become(Molecule m, MatterState state)
    {
        m.State = state;
        m.Flash = 0.5;
        if (m.Spin == 0) m.Spin = Gaussian() * 1.5;
    }

    /// <summary>The lattice's places from the floor up, every other row shifted half a place.</summary>
    private static Point[] Sites()
    {
        var sites = new List<Point>();
        for (var row = 0; sites.Count < Count; row++)
        {
            var shift = row % 2 == 1 ? Spacing / 2 : 0;
            for (var x = Radius + 0.004 + shift; x <= 1 - Radius && sites.Count < Count; x += Spacing)
            {
                sites.Add(new Point(x, Radius + (row * RowStep)));
            }
        }

        return [.. sites];
    }

    private int Row(int site) => (int)Math.Round((_sites[site].Y - Radius) / RowStep);

    private double Gaussian()
    {
        var u = 1 - _random.NextDouble();
        var v = _random.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v);
    }

    #endregion

    #region drawing

    /// <summary>
    /// Draws the molecules in <paramref name="box"/> (its width is the container's), the solid's bonds first when
    /// <paramref name="bonds"/> is set.
    /// </summary>
    public void Draw(PaintContext dc, Rect box, bool bonds)
    {
        var scale = box.Width;
        Point At(Point p) => new(box.Left + (p.X * scale), box.Bottom - (p.Y * scale));
        if (bonds)
        {
            var solids = _molecules.Where(m => m.State == MatterState.Solid).ToList();
            var reach = Spacing * 1.3;
            for (var i = 0; i < solids.Count; i++)
            {
                for (var j = i + 1; j < solids.Count; j++)
                {
                    if (((Vector)(solids[i].Position - solids[j].Position)).Length < reach) dc.DrawLine(BondPen, At(solids[i].Position), At(solids[j].Position));
                }
            }
        }

        var size = Radius * scale;
        foreach (var m in _molecules)
        {
            var at = At(m.Position);
            if (m.Flash > 0)
            {
                var r = size * (1.4 + (1.2 * (1 - (m.Flash / 0.5))));
                dc.DrawEllipse(null, FlashPen, at, r, r);
            }

            DrawGlyph(dc, Substance, at, size, m.Angle);
        }
    }

    /// <summary>Lets flashes fade (called each frame with the step).</summary>
    public void Fade(double dt)
    {
        foreach (var m in _molecules) m.Flash = Math.Max(0, m.Flash - dt);
    }

    /// <summary>One molecule: water's oxygen with two hydrogens at 104.5°, or carbon dioxide's O=C=O in a line.</summary>
    public static void DrawGlyph(PaintContext dc, MatterSubstance substance, Point at, double size, double angle)
    {
        Point Around(double r, double a) => at + new Vector(r * Math.Cos(angle + a), r * Math.Sin(angle + a));
        if (substance == MatterSubstance.CarbonDioxide)
        {
            dc.DrawEllipse(Oxygen, null, Around(size * 0.62, 0), size * 0.42, size * 0.42);
            dc.DrawEllipse(Oxygen, null, Around(size * 0.62, Math.PI), size * 0.42, size * 0.42);
            dc.DrawEllipse(Carbon, Rim, at, size * 0.38, size * 0.38);
            return;
        }

        const double half = 104.5 / 2 * Math.PI / 180;
        dc.DrawEllipse(Hydrogen, null, Around(size * 0.62, (Math.PI / 2) - half), size * 0.3, size * 0.3);
        dc.DrawEllipse(Hydrogen, null, Around(size * 0.62, (Math.PI / 2) + half), size * 0.3, size * 0.3);
        dc.DrawEllipse(Oxygen, Rim, at, size * 0.55, size * 0.55);
    }

    private static readonly IBrush Oxygen = Solid(255, 0xFF, 0x6A, 0x6A);
    private static readonly IBrush Hydrogen = Solid(255, 0xF2, 0xF4, 0xF8);
    private static readonly IBrush Carbon = Solid(255, 0x5C, 0x64, 0x72);
    private static readonly IPen Rim = MakePen(Solid(140, 255, 255, 255), 0.8);
    private static readonly IPen BondPen = MakePen(Solid(70, 0x8F, 0xD8, 0xFF), 1);
    private static readonly IPen FlashPen = MakePen(Solid(120, 0xFF, 0xD3, 0x6B), 1);

    #endregion

    private sealed class Molecule
    {
        public MatterState State;
        public Point Position;
        public Vector Velocity;
        public Vector Jitter;
        public double Angle;
        public double Spin;
        public double Flash;

        /// <summary>The lattice place of a solid's molecule (−1 otherwise).</summary>
        public int Site = -1;
    }
}
