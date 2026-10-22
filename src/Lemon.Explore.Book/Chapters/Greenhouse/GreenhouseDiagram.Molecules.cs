using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Greenhouse;

/// <summary>
/// Molecules mode: one molecule, big, wiggling in the chosen way (slowed down about 10¹³ times), with its small
/// charges and its dipole. Heat rays from the ground cross it, each labelled with its wavelength; a ray whose
/// frequency matches a wiggle that shifts charge is caught, the molecule wiggles harder, and a moment later it sends the
/// ray out again in any direction. Below, the ground's glow by wavelength with what this gas catches shaded.
/// </summary>
public sealed partial class GreenhouseDiagram
{
    /// <summary>Rays per second of playback at 1×.</summary>
    private const double BeamRate = 2.2;

    private const double BeamSpeed = 150;

    /// <summary>How long a caught ray's energy stays in the molecule before it is sent out again, seconds.</summary>
    private const double HoldSeconds = 1.2;

    private readonly List<BeamRay> _beam = [];
    private readonly Queue<bool> _outcomes = new();
    private readonly List<(double Lambda, bool Caught, double Time)> _ticks = [];
    private double _beamDue;
    private Vibration? _excitedMode;
    private double _excitedAt = double.NegativeInfinity;
    private double _reemitAt = double.PositiveInfinity;
    private double _reemitLambda;

    private sealed class BeamRay
    {
        public double Lambda;
        public Point From;
        public Vector Direction;
        public double Start;
        public bool Checked;
        public double Phase;
    }

    private void OnGasChanged()
    {
        _beam.Clear();
        _outcomes.Clear();
        _ticks.Clear();
        _excitedMode = null;
        _excitedAt = double.NegativeInfinity;
        _reemitAt = double.PositiveInfinity;
    }

    private (Rect Chamber, Rect Spectrum, Point Center, double Scale) MoleculesLayout()
    {
        var stage = Stage();
        var chamber = new Rect(stage.Left + 24, stage.Top, Math.Max(200, stage.Width - 48), Math.Max(120, stage.Height * 0.58));
        var spectrum = new Rect(stage.Left + 70, chamber.Bottom + 40, Math.Max(150, stage.Width - 70 - 40), Math.Max(60, stage.Bottom - chamber.Bottom - 40 - 52));
        var center = new Point(chamber.Left + (chamber.Width * 0.5), chamber.Top + (chamber.Height * 0.5));
        var scale = Math.Clamp(Math.Min(chamber.Height * 0.22, chamber.Width * 0.11), 42, 100);
        return (chamber, spectrum, center, scale);
    }

    private Molecule CurrentMolecule => Molecules.Get(Gas);

    /// <summary>The wiggle picked in the side panel (the first one if the id is not this molecule's).</summary>
    private Vibration? SelectedVibration => CurrentMolecule.Modes.FirstOrDefault(m => m.Id == VibrationId) ?? CurrentMolecule.Modes.FirstOrDefault();

    private bool IsExcited => _t - _excitedAt < HoldSeconds + 0.4;

    private void StepMolecules(double step)
    {
        if (step <= 0) return;
        var (chamber, _, center, scale) = MoleculesLayout();
        var molecule = CurrentMolecule;
        var reach = scale * 1.6;

        _beamDue += step * BeamRate;
        while (_beamDue >= 1)
        {
            _beamDue -= 1;

            // Aimed at the molecule, a little spread out; wavelengths as the ground glows them.
            var spread = (_random.NextDouble() + _random.NextDouble() - 1) * scale * 1.2;
            _beam.Add(new BeamRay
            {
                Lambda = InfraredModel.SampleGround(_random.NextDouble()),
                From = new Point(chamber.Left - 20, center.Y + spread),
                Direction = new Vector(1, 0),
                Start = _t,
                Phase = _random.NextDouble() * Math.PI * 2,
            });
        }

        for (var i = _beam.Count - 1; i >= 0; i--)
        {
            var ray = _beam[i];
            var head = ray.From + (ray.Direction * BeamSpeed * (_t - ray.Start));
            if (!ray.Checked && head.X >= center.X)
            {
                ray.Checked = true;
                var near = Math.Abs(head.Y - center.Y) < reach;
                var catcher = near && !IsExcited ? molecule.Catcher(ray.Lambda) : null;
                if (near)
                {
                    Record(catcher is not null);
                    _ticks.Add((ray.Lambda, catcher is not null, _t));
                }

                if (catcher is not null)
                {
                    _excitedMode = catcher;
                    _excitedAt = _t;
                    _reemitAt = _t + HoldSeconds * (0.8 + (0.4 * _random.NextDouble()));
                    _reemitLambda = ray.Lambda;
                    _beam.RemoveAt(i);
                    continue;
                }
            }

            var margin = 160;
            if (head.X > chamber.Right + margin || head.X < chamber.Left - margin || head.Y < chamber.Top - margin || head.Y > chamber.Bottom + margin)
            {
                _beam.RemoveAt(i);
            }
        }

        if (_t >= _reemitAt)
        {
            // Sent out again in any direction: as likely back down to the ground as on up.
            _reemitAt = double.PositiveInfinity;
            var angle = _random.NextDouble() * Math.PI * 2;
            _beam.Add(new BeamRay
            {
                Lambda = _reemitLambda,
                From = center,
                Direction = new Vector(Math.Cos(angle), Math.Sin(angle)),
                Start = _t,
                Checked = true,
                Phase = _random.NextDouble() * Math.PI * 2,
            });
        }

        _ticks.RemoveAll(t => _t - t.Time > 6);
    }

    private void Record(bool caught)
    {
        _outcomes.Enqueue(caught);
        while (_outcomes.Count > 40) _outcomes.Dequeue();
    }

    private void DrawMoleculesMode(PaintContext dc, double width, double height)
    {
        var (chamber, spectrum, center, scale) = MoleculesLayout();
        var molecule = CurrentMolecule;

        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        _stars.Draw(dc, width, height, RenderScale(), _clock.Elapsed.TotalSeconds, 0.35);

        // The molecule's halo while it holds a caught ray.
        var excitedAge = (_t - _excitedAt) / HoldSeconds;
        if (excitedAge is >= 0 and < 1.3)
        {
            var glow = 1 - Math.Clamp(excitedAge - 0.3, 0, 1);
            dc.DrawEllipse(FadedBrush(Ink.Orange, 0.22 * glow), null, center, scale * 2.1, scale * 2.1);
            dc.DrawEllipse(FadedBrush(Ink.Orange, 0.18 * glow), null, center, scale * 1.4, scale * 1.4);
        }

        DrawBeam(dc);

        var mode = IsExcited && _excitedMode is not null ? _excitedMode : SelectedVibration;
        var amplitude = IsExcited ? 1.5 : 1.0;
        var swing = Math.Sin(2 * Math.PI * 1.1 * _t) * amplitude;
        var spin = _t * (IsExcited ? 4.5 : 2.2);
        var positions = molecule.Positions(mode, swing, spin);
        DrawMolecule(dc, molecule, positions, center, scale);
        DrawDipole(dc, molecule, mode, positions, center, scale);

        if (ShowLabels) DrawMoleculeLabels(dc, chamber, center, scale, molecule, mode);
        DrawSpectrum(dc, spectrum, molecule);
        _targets.Add(GasId(molecule.Gas), center, scale * 1.25);
    }

    private void DrawBeam(PaintContext dc)
    {
        foreach (var ray in _beam)
        {
            var head = ray.From + (ray.Direction * BeamSpeed * (_t - ray.Start));
            var period = Math.Clamp(ray.Lambda * 2.6, 9, 100);
            var length = Math.Clamp(period * 2.2, 30, 150);
            var travelled = ((Vector)(head - ray.From)).Length;
            if (ray.Checked && travelled < 4) continue;
            var color = Quantize(InfraredColor(ray.Lambda));
            dc.DrawGeometry(null, FadedPen(color, 0.95, 2.2, flat: true),
                Line(Wiggle(head, ray.Direction, ray.Checked ? Math.Min(length, travelled) : length, period, 6, ray.Phase - (_t * 10))));
            if (ShowLabels)
            {
                var label = LocFormat("Green_Mol_Ray", Number(ray.Lambda, ray.Lambda < 10 ? "0.0" : "0"));
                DrawText(dc, label, head - (ray.Direction * (length / 2)) + new Vector(0, -14), Ink.Dim, 10, center: true);
            }
        }
    }

    /// <summary>The bonds of a molecule: which atoms, and how many lines (double and triple bonds).</summary>
    private static (int A, int B, int Order)[] Bonds(Gas gas) => gas switch
    {
        Gas.N2 => [(0, 1, 3)],
        Gas.O2 => [(0, 1, 2)],
        Gas.CO2 => [(1, 0, 2), (1, 2, 2)],
        Gas.H2O => [(0, 1, 1), (0, 2, 1)],
        Gas.CH4 => [(0, 1, 1), (0, 2, 1), (0, 3, 1), (0, 4, 1)],
        _ => [],
    };

    private static double AtomRadius(string element) => element switch
    {
        "H" => 0.3,
        "C" => 0.46,
        "N" => 0.46,
        "O" => 0.48,
        _ => 0.62,
    };

    private void DrawMolecule(PaintContext dc, Molecule molecule, Vec3[] positions, Point center, double scale)
    {
        Point Screen(Vec3 p) => center + new Vector(p.X * scale, p.Y * scale);

        foreach (var (a, b, order) in Bonds(molecule.Gas))
        {
            var pa = Screen(positions[a]);
            var pb = Screen(positions[b]);
            Vector v = pb - pa;
            if (v.Length < 1) continue;
            var across = new Vector(-v.Y, v.X) / v.Length;
            for (var k = 0; k < order; k++)
            {
                var offset = across * ((k - ((order - 1) / 2.0)) * 6);
                dc.DrawLine(MoleculeInk.Bond, pa + offset, pb + offset);
            }
        }

        foreach (var i in Enumerable.Range(0, positions.Length).OrderBy(i => positions[i].Z))
        {
            var atom = molecule.Atoms[i];
            var depth = 1 + (positions[i].Z * 0.12);
            var r = AtomRadius(atom.Element) * scale * depth;
            var at = Screen(positions[i]);
            dc.DrawEllipse(MoleculeInk.Shadow, null, at + new Vector(2, 3), r, r);
            dc.DrawEllipse(MoleculeInk.AtomBrush(atom.Element), MoleculeInk.AtomEdge, at, r, r);
            DrawText(dc, atom.Element, at, atom.Element is "H" or "Ar" ? Color.FromRgb(0x20, 0x26, 0x30) : Ink.Text, Math.Clamp(r * 0.55, 9, 15), center: true, bold: true);
        }
    }

    /// <summary>The dipole as an arrow from the centre, with a word on whether it changes (what the light can push on).</summary>
    private void DrawDipole(PaintContext dc, Molecule molecule, Vibration? mode, Vec3[] positions, Point center, double scale)
    {
        if (molecule.Atoms.All(a => a.Charge == 0)) return;
        var d = molecule.Dipole(positions);
        var v = new Vector(d.X, d.Y) * scale * 2.4;
        if (v.Length > scale * 1.6) v *= scale * 1.6 / v.Length;
        if (v.Length > 4) DrawArrow(dc, center, center + v, FadedBrush(Ink.Gold, 0.95), 2.4, 10);

        if (!ShowLabels) return;

        // Little charges by each atom: δ− on the atoms that pull the electrons, δ+ on the others.
        for (var i = 0; i < molecule.Atoms.Length; i++)
        {
            var atom = molecule.Atoms[i];
            if (atom.Charge == 0 || positions[i].Z < -0.3) continue;
            var at = center + new Vector(positions[i].X * scale, positions[i].Y * scale);
            Vector outward = at - center;
            var r = AtomRadius(atom.Element) * scale;

            // The middle atom's charge goes up and to the right, clear of the atoms around it.
            var place = outward.Length < scale * 0.3 ? at + new Vector(r * 0.8, -r * 0.8) + new Vector(10, -6) : at + (outward / outward.Length * (r + 13));
            DrawText(dc, atom.Charge < 0 ? "δ−" : "δ+", place, atom.Charge < 0 ? Ink.Cyan : Ink.Orange, 12, center: true, bold: true);
        }
    }

    private void DrawMoleculeLabels(PaintContext dc, Rect chamber, Point center, double scale, Molecule molecule, Vibration? mode)
    {
        // What the molecule is and how it is wiggling now.
        var name = Loc($"Green_Gas_{molecule.Gas}");
        DrawText(dc, name, new Point(chamber.Left + 4, chamber.Top + 12), Ink.Text, 15, bold: true);
        DrawText(dc, Loc("Green_Mol_Slowed"), new Point(chamber.Left + 4, chamber.Top + 32), Ink.Dim, 10.5);

        string text;
        Color color;
        if (mode is null)
        {
            text = Loc("Green_Mol_NoModes");
            color = Ink.Dim;
        }
        else
        {
            var wave = mode.Spin
                ? Loc("Green_Mol_SpinBand")
                : LocFormat("Green_Mol_ModeBand", Number(mode.Wavenumber, "0"), Number(mode.Wavelength, mode.Wavelength < 10 ? "0.00" : "0.0"));
            text = LocFormat(mode.Active ? "Green_Mol_ModeActive" : "Green_Mol_ModeInactive", Loc($"Green_Vib_{mode.Id}"), wave);
            color = mode.Active ? Ink.Green : Ink.Dim;
        }

        if (IsExcited && _excitedMode is not null) text = LocFormat("Green_Mol_Holding", Number(_reemitLambda, "0.0"));
        var state = DrawPill(dc, text, new Point(center.X, center.Y + (scale * 1.75)), IsExcited ? Ink.Orange : color, 12);
        if (mode is not null) AddBoxTarget(VibrationTargetId(mode.Id), state);

        // The dipole and the light: a word under the picture.
        if (molecule.Atoms.Any(a => a.Charge != 0))
        {
            AddBoxTarget("dipole", DrawText(dc, Loc("Green_Mol_Dipole"),
                new Point(chamber.Right - 4 - _labels.Text(Loc("Green_Mol_Dipole"), 10.5, Ink.Gold).Width, chamber.Top + 52), Ink.Gold, 10.5));
        }

        AddBoxTarget("groundrays", DrawText(dc, Loc("Green_Mol_FromGround"), new Point(chamber.Left + 4, chamber.Bottom - 8), Ink.Dim, 10.5));

        // The tally of the last rays that crossed it.
        if (_outcomes.Count > 0)
        {
            var caught = _outcomes.Count(o => o);
            var tally = LocFormat("Green_Mol_Tally", caught, _outcomes.Count);
            var formatted = _labels.Text(tally, 12, Ink.Text);
            _labels.DrawTextPill(dc, tally, new Point(chamber.Right - formatted.Width - 16, chamber.Top + 4), 12, caught > 0 ? Ink.Orange : Ink.Text);
        }
    }

    /// <summary>The ground's glow by wavelength (3–50 µm, log), what this gas and the whole air catch, the window and this molecule's wiggles.</summary>
    private void DrawSpectrum(PaintContext dc, Rect plot, Molecule molecule)
    {
        const double from = 3;
        const double to = 50;
        double X(double l) => plot.Left + (Math.Log(l / from) / Math.Log(to / from) * plot.Width);
        var peak = InfraredModel.PerLog(InfraredModel.PeakWavelength(288) * 1.25, 288);
        double Y(double share) => plot.Bottom - (share * plot.Height);

        var samples = Enumerable.Range(0, 161).Select(i => from * Math.Pow(to / from, i / 160.0)).ToArray();
        var glow = samples.Select(l => InfraredModel.PerLog(l, 288) / peak).ToArray();

        var panel = new Rect(plot.Left - 10, plot.Top - 26, plot.Width + 20, plot.Height + 72);
        dc.DrawRectangle(MoleculeInk.SpectrumBack, Ink.Frame, panel);
        AddBoxTarget("spectrum", panel);

        // The window, 8–13 µm, where little is caught.
        var window = new Rect(X(8), plot.Top, X(13) - X(8), plot.Height);
        dc.DrawRectangle(MoleculeInk.Window, null, window);
        var windowName = DrawText(dc, Loc("Green_Spectrum_Window"), new Point(window.Left + (window.Width / 2), plot.Top - 12), Ink.Cyan, 10.5, center: true);
        AddBoxTarget("window", window.Union(windowName));

        IEnumerable<Point> Area(Func<int, double> share) =>
            new[] { new Point(X(from), plot.Bottom) }
                .Concat(samples.Select((l, i) => new Point(X(l), Y(glow[i] * share(i)))))
                .Append(new Point(X(to), plot.Bottom));

        dc.DrawGeometry(MoleculeInk.Glow, null, Shape(Area(_ => 1).ToArray()));
        var gasColor = GasColor(molecule.Gas);
        if (molecule.Gas is Gas.CO2 or Gas.H2O or Gas.CH4)
        {
            dc.DrawGeometry(FadedBrush(gasColor, 0.6), null, Shape(Area(i => InfraredModel.Absorption(molecule.Gas, samples[i])).ToArray()));
        }

        dc.DrawGeometry(null, MoleculeInk.AllAir, Line(samples.Select((l, i) => new Point(X(l), Y(glow[i] * InfraredModel.AirAbsorption(l))))));
        dc.DrawGeometry(null, MoleculeInk.GlowLine, Line(samples.Select((l, i) => new Point(X(l), Y(glow[i])))));

        // This molecule's wiggles at their wavelengths: green if they catch light, grey if not; labels that would
        // touch step down a line.
        var labelEnds = new List<(double Right, double Y)>();
        foreach (var mode in molecule.Modes)
        {
            if (mode.Spin)
            {
                var start = X(18);
                dc.DrawLine(FadedPen(Ink.Green, 0.9, 2), new Point(start, plot.Top + 26), new Point(X(to), plot.Top + 26));
                var spinName = DrawText(dc, Loc("Green_Vib_Spin"), new Point(start + 4, plot.Top + 36), Ink.Green, 10);
                AddBoxTarget(VibrationTargetId(mode.Id), spinName.Union(new Rect(start, plot.Top + 20, X(to) - start, 12)));
                continue;
            }

            var l = mode.Wavelength;
            if (l < from || l > to) continue;
            var x = X(l);
            var selected = mode.Id == SelectedVibration?.Id;
            dc.DrawLine(mode.Active ? FadedPen(Ink.Green, selected ? 1 : 0.6, selected ? 2 : 1.2) : MoleculeInk.InactiveMode, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var label = LocFormat(mode.Active ? "Green_Spectrum_Mode" : "Green_Spectrum_ModeOff", Number(l, l < 10 ? "0.0" : "0"));
            var y = plot.Top + 8;
            while (labelEnds.Any(e => Math.Abs(e.Y - y) < 1 && e.Right > x)) y += 14;
            var name = DrawText(dc, label, new Point(x + 3, y), mode.Active ? Ink.Green : Ink.Dim, 10);
            AddBoxTarget(VibrationTargetId(mode.Id), name.Union(new Rect(x - 4, plot.Top, 8, plot.Height)));
            labelEnds.Add((x + 6 + _labels.Text(label, 10, Ink.Green).Width, y));
        }

        // The rays of the last few seconds, at their wavelengths.
        foreach (var (lambda, caught, time) in _ticks)
        {
            var age = Math.Clamp((_t - time) / 6, 0, 1);
            var x = X(lambda);
            dc.DrawEllipse(FadedBrush(caught ? Ink.Orange : Ink.Text, (caught ? 1 : 0.5) * (1 - age)), null, new Point(x, plot.Bottom - 5), caught ? 4 : 2.5, caught ? 4 : 2.5);
        }

        // The axis, with the false colours the rays are drawn in.
        foreach (var tick in new[] { 3.0, 4, 5, 7, 10, 15, 20, 30, 50 })
        {
            var x = X(tick);
            dc.DrawLine(Ink.Axis, new Point(x, plot.Bottom), new Point(x, plot.Bottom + 4));
            DrawText(dc, Number(tick, "0"), new Point(x, plot.Bottom + 21), Ink.Dim, 10, center: true);
        }

        for (var i = 0; i < 60; i++)
        {
            var a = from * Math.Pow(to / from, i / 60.0);
            var b = from * Math.Pow(to / from, (i + 1) / 60.0);
            var color = InfraredColor(Math.Min(40, Math.Sqrt(a * b)));
            dc.DrawRectangle(FadedBrush(Quantize(color), 0.9), null, new Rect(X(a), plot.Bottom + 6, X(b) - X(a) + 0.5, 5));
        }

        dc.DrawLine(Ink.Axis, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));
        DrawTextRight(dc, Loc("Green_Spectrum_Axis"), new Point(plot.Right, plot.Bottom + 36), Ink.Dim, 10);
        DrawText(dc, Loc("Green_Spectrum_Glow"), new Point(plot.Left, plot.Top - 12), Ink.Text, 10.5);
        var caughtKey = molecule.Gas is Gas.CO2 or Gas.H2O or Gas.CH4 ? "Green_Spectrum_Caught" : "Green_Spectrum_CaughtNone";
        var caughtText = LocFormat(caughtKey, Loc($"Green_Gas_{molecule.Gas}_Short"), Number(InfraredModel.CaughtShare(molecule.Gas) * 100, "0"));
        DrawTextRight(dc, caughtText, new Point(plot.Right, plot.Top - 12), gasColor, 10.5);
        DrawTextRight(dc, LocFormat("Green_Spectrum_AllAir", Number(InfraredModel.AirCaughtShare() * 100, "0")), new Point(plot.Right, plot.Top + 6), Ink.Dim, 10);
    }

    internal static Color GasColor(Gas gas) => gas switch
    {
        Gas.CO2 => Color.FromRgb(0xFF, 0x9F, 0x5A),
        Gas.H2O => Color.FromRgb(0x5A, 0xB0, 0xFF),
        Gas.CH4 => Color.FromRgb(0x8B, 0xE0, 0x8F),
        _ => Color.FromRgb(0x9A, 0xA5, 0xB5),
    };

    private static class MoleculeInk
    {
        public static readonly IPen Bond = MakePen(Solid(255, 0xA8, 0xB0, 0xBC), 4);
        public static readonly IPen AtomEdge = MakePen(Solid(120, 0x00, 0x00, 0x00), 1);
        public static readonly IBrush Shadow = Solid(70, 0, 0, 0);
        public static readonly IBrush SpectrumBack = Solid(150, 0x06, 0x09, 0x10);
        public static readonly IBrush Window = Solid(26, 0x6F, 0xD8, 0xFF);
        public static readonly IBrush Glow = Solid(34, 0xEE, 0xF1, 0xF6);
        public static readonly IPen GlowLine = MakePen(Solid(200, 0xEE, 0xF1, 0xF6), 1.5);
        public static readonly IPen AllAir = MakePen(Solid(150, 0xEE, 0xF1, 0xF6), 1, dashed: true);
        public static readonly IPen InactiveMode = MakePen(Solid(130, 0x9A, 0xA5, 0xB5), 1.2, dashed: true);

        private static readonly Dictionary<string, IBrush> Atoms = new()
        {
            ["H"] = Ball(0xF4, 0xF6, 0xFA, 0xA8, 0xB0, 0xBC),
            ["C"] = Ball(0x6A, 0x70, 0x7C, 0x22, 0x26, 0x2E),
            ["N"] = Ball(0x8A, 0xA6, 0xFF, 0x2A, 0x4A, 0xC8),
            ["O"] = Ball(0xFF, 0x8A, 0x7A, 0xC0, 0x2A, 0x22),
            ["Ar"] = Ball(0xB8, 0xF0, 0xFF, 0x3A, 0x9A, 0xC0),
        };

        public static IBrush AtomBrush(string element) => Atoms.TryGetValue(element, out var brush) ? brush : Atoms["Ar"];

        private static IBrush Ball(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2) => new ImmutableRadialGradientBrush(
        [
            new ImmutableGradientStop(0, Color.FromRgb(r1, g1, b1)),
            new ImmutableGradientStop(1, Color.FromRgb(r2, g2, b2)),
        ],
            center: new RelativePoint(0.42, 0.4, RelativeUnit.Relative),
            gradientOrigin: new RelativePoint(0.35, 0.3, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.65, RelativeUnit.Relative),
            radiusY: new RelativeScalar(0.65, RelativeUnit.Relative));
    }
}
