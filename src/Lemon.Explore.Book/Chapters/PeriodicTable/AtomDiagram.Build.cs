using Avalonia;
using Avalonia.Media;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.PeriodicTable;

/// <summary>
/// The build view: the nucleus packed with protons and neutrons, up to four shells of electrons going round it, the
/// legend of the three particles, and beside it the element's tile, its mass number, charge and whether the nucleus
/// lasts, over a tray of each particle with − and + buttons. A particle added flies from its tray into place.
/// </summary>
public sealed partial class AtomDiagram
{
    /// <summary>The most shells an atom built here uses (krypton has four).</summary>
    private const int BuildShells = 4;

    /// <summary>Seconds a particle takes to fly from its tray.</summary>
    private const double FlightSeconds = 0.45;

    /// <summary>Where the build view's parts go: the atom's centre and outer shell, and the panel beside or under it.</summary>
    private readonly record struct BuildLayout(Point Center, double Radius, Rect Panel, bool Stacked);

    /// <summary>A particle on its way from its tray's + button into the atom.</summary>
    private readonly record struct Flight(Particle Kind, Point From, double Start);

    private readonly List<Flight> _flights = [];

    /// <summary>Each tray's + button as last drawn, where an added particle starts from.</summary>
    private readonly Dictionary<Particle, Point> _addButtons = [];

    private BuildLayout LayOutBuild()
    {
        var s = Stage();
        if (s.Width >= 560)
        {
            var panelWidth = Math.Clamp(s.Width * 0.36, 220, 300);
            var area = new Rect(s.Left, s.Top, s.Width - panelWidth - 12, s.Height);
            var radius = Math.Max(60, (Math.Min(area.Width, area.Height - 40) / 2) - 18);
            return new BuildLayout(new Point(area.Center.X, area.Top + 8 + radius), radius, new Rect(s.Right - panelWidth, s.Top, panelWidth, s.Height), false);
        }

        var panelHeight = Math.Clamp(s.Height * 0.42, 180, 250);
        var atom = new Rect(s.Left, s.Top, s.Width, s.Height - panelHeight - 8);
        // Room under the atom for the legend, one particle under another.
        var r = Math.Max(50, (Math.Min(atom.Width, atom.Height - 70) / 2) - 10);
        return new BuildLayout(new Point(atom.Center.X, atom.Top + 4 + r), r, new Rect(s.Left, s.Bottom - panelHeight, s.Width, panelHeight), true);
    }

    /// <summary>The radius of shell <paramref name="k"/> (0 innermost) of an atom whose outer shell is <paramref name="radius"/>.</summary>
    private static double ShellRadius(double radius, int k) => radius * (0.36 + (0.64 * k / (BuildShells - 1)));

    private void OnParticleCountChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (Mode != AtomMode.Build || change.OldValue is not int before || change.NewValue is not int after || after <= before) return;
        var kind = change.Property == ProtonsProperty ? Particle.Proton : change.Property == NeutronsProperty ? Particle.Neutron : Particle.Electron;
        if (!_addButtons.TryGetValue(kind, out var from)) return;

        // A preset or the chart changes many at once: only a step of one flies.
        if (after - before != 1) return;
        _flights.Add(new Flight(kind, from, _clock.Elapsed.TotalSeconds));
    }

    private void DrawBuildMode(PaintContext dc, double now)
    {
        var layout = LayOutBuild();
        var atom = new AtomBuild(Protons, Neutrons, Electrons);
        _flights.RemoveAll(f => now - f.Start > FlightSeconds);

        DrawNucleus(dc, layout, atom, now);
        DrawShells(dc, layout, atom, now);
        DrawFlights(dc, layout, now);
        DrawParticleLegend(dc, layout);
        DrawPanel(dc, layout, atom);
    }

    /// <summary>The nucleus: its protons and neutrons packed in a sunflower spiral, mixed evenly.</summary>
    private void DrawNucleus(PaintContext dc, BuildLayout layout, AtomBuild atom, double now)
    {
        var center = layout.Center;
        var count = atom.Protons + atom.Neutrons;
        var inner = ShellRadius(layout.Radius, 0);
        if (count == 0)
        {
            dc.DrawEllipse(null, FadedPen(Ink.Dim, 0.5, 1.2, dashed: true), center, inner * 0.3, inner * 0.3);
            _targets.Add("nucleus", center, inner * 0.3);
            return;
        }

        // Nucleons small enough that the packed nucleus stays well inside the first shell.
        const double spacing = 1.9;
        var particle = Math.Min(layout.Radius * 0.06, inner * 0.72 / ((spacing * Math.Sqrt(count)) + 1));
        var packed = (spacing * particle * Math.Sqrt(count)) + particle;
        dc.DrawEllipse(Ink.NucleusGlow, null, center, packed * 1.8, packed * 1.8);

        // Spread the protons evenly through the nucleons: nucleon i is a proton when the running share steps up.
        var golden = Math.PI * (3 - Math.Sqrt(5));
        for (var i = count - 1; i >= 0; i--)
        {
            var isProton = (int)((i + 1L) * atom.Protons / count) > (int)((long)i * atom.Protons / count);
            var r = spacing * particle * Math.Sqrt(i + 0.5) * 0.95;
            var angle = i * golden;
            var jiggle = particle * 0.08;
            var at = new Point(
                center.X + (r * Math.Cos(angle)) + (jiggle * Math.Sin((now * 5.1) + i)),
                center.Y + (r * Math.Sin(angle)) + (jiggle * Math.Cos((now * 4.3) + (i * 1.7))));
            DrawParticle(dc, isProton ? Particle.Proton : Particle.Neutron, at, particle);
        }

        _targets.Add("nucleus", center, packed);
    }

    /// <summary>The shells: the full ones solid, the next to fill dashed, and the electrons going round, slower further out.</summary>
    private void DrawShells(PaintContext dc, BuildLayout layout, AtomBuild atom, double now)
    {
        var center = layout.Center;
        var shells = atom.Shells;
        var glow = Glows("shell");
        var electron = Math.Clamp(layout.Radius * 0.04, 4, 7);
        for (var k = 0; k < BuildShells; k++)
        {
            var r = ShellRadius(layout.Radius, k);
            if (k < shells.Count)
            {
                if (glow) dc.DrawEllipse(null, ExploreTargets.GlowPen(now), center, r, r);
                dc.DrawEllipse(null, Ink.Shell, center, r, r);
            }
            else if (k == shells.Count)
            {
                dc.DrawEllipse(null, Ink.ShellNext, center, r, r);
            }
        }

        for (var k = 0; k < shells.Count; k++)
        {
            var r = ShellRadius(layout.Radius, k);
            var n = shells[k];
            var phase = (_drift * 0.7 / (k + 1)) + (k * 0.5);
            for (var i = 0; i < n; i++)
            {
                var angle = phase + (2 * Math.PI * i / n);
                var at = new Point(center.X + (r * Math.Cos(angle)), center.Y + (r * Math.Sin(angle)));
                dc.DrawEllipse(FadedBrush(Ink.Electron, 0.18), null, at, electron * 1.9, electron * 1.9);
                DrawParticle(dc, Particle.Electron, at, electron);
                _targets.Add("electron", at, electron + 2);
            }

            // Each shell's count, up and to the right where no electron waits for long.
            var labelAngle = -Math.PI / 4;
            var spot = new Point(center.X + (r * Math.Cos(labelAngle)), center.Y + (r * Math.Sin(labelAngle)));
            if (ShowLabels) DrawTargetPill(dc, LocFormat("Atom_Label_Shell", k + 1, n), spot, Ink.Electron, "shell", 10.5);
            else _targets.Add("shell", spot, 8);
        }
    }

    /// <summary>Particles on their way in: a proton or neutron to the nucleus, an electron to the outermost shell.</summary>
    private void DrawFlights(PaintContext dc, BuildLayout layout, double now)
    {
        var electron = Math.Clamp(layout.Radius * 0.04, 4, 7);
        foreach (var flight in _flights)
        {
            var t = Math.Clamp((now - flight.Start) / FlightSeconds, 0, 1);
            var eased = 1 - Math.Pow(1 - t, 3);
            var to = flight.Kind == Particle.Electron
                ? layout.Center + new Vector(0, -ShellRadius(layout.Radius, Math.Max(0, Electrons <= 0 ? 0 : AtomModel.ShellsFor(Electrons).Count - 1)))
                : layout.Center;
            var at = flight.From + ((to - flight.From) * eased);
            dc.PushOpacity(1 - (t * 0.6));
            DrawParticle(dc, flight.Kind, at, flight.Kind == Particle.Electron ? electron : 7);
            dc.Pop();
        }
    }

    /// <summary>What the three particles look like and carry, under the atom: in a row, or one under another when narrow.</summary>
    private void DrawParticleLegend(PaintContext dc, BuildLayout layout)
    {
        var y = layout.Center.Y + layout.Radius + 22;
        var items = new[] { (Particle.Proton, "proton", "Atom_Legend_Proton"), (Particle.Neutron, "neutron", "Atom_Legend_Neutron"), (Particle.Electron, "electron", "Atom_Legend_Electron") };
        var texts = items.Select(i => _labels.Text(Loc(i.Item3), 11, Ink.Text)).ToArray();
        var total = texts.Sum(t => t.Width + 30) - 12;
        var inRow = total <= Stage().Width;
        var widest = texts.Max(t => t.Width) + 18;
        var x = inRow ? layout.Center.X - (total / 2) : layout.Center.X - (widest / 2);
        for (var i = 0; i < items.Length; i++)
        {
            var (kind, id, _) = items[i];
            DrawParticle(dc, kind, new Point(x + 7, y), 7);
            dc.DrawText(texts[i], new Point(x + 18, y - (texts[i].Height / 2)));
            AddBoxTarget(id, new Rect(x - 2, y - 10, texts[i].Width + 24, 20));
            if (inRow) x += texts[i].Width + 30;
            else y += 20;
        }
    }

    /// <summary>The tile and its pills, then the three trays.</summary>
    private void DrawPanel(PaintContext dc, BuildLayout layout, AtomBuild atom)
    {
        var panel = layout.Panel;
        var inner = panel.Deflate(12);

        // Side by side: the tile with its pills on the left, the trays on the right; else the tile over them, the
        // panel only as tall as they need.
        Rect tileColumn;
        Rect trays;
        if (layout.Stacked)
        {
            tileColumn = new Rect(inner.Left, inner.Top, inner.Width * 0.42, inner.Height);
            trays = new Rect(tileColumn.Right + 10, inner.Top, inner.Width - tileColumn.Width - 10, inner.Height);
        }
        else
        {
            var tileSide = Math.Clamp(Math.Min(inner.Width * 0.8, 150), 70, Math.Max(70, (inner.Height * 0.58) - 92));
            tileColumn = new Rect(inner.Left, inner.Top, inner.Width, tileSide + 92);
            trays = new Rect(inner.Left, tileColumn.Bottom + 10, inner.Width, Math.Min(138, inner.Bottom - tileColumn.Bottom - 10));
            panel = panel.WithHeight(Math.Min(panel.Height, trays.Bottom + 12 - panel.Top));
        }

        dc.DrawRoundedRectangle(Ink.Panel, Ink.Frame, panel, 12, 12);

        var side = Math.Min(Math.Min(tileColumn.Width * 0.8, 150), tileColumn.Height - 92);
        side = Math.Max(70, side);
        var tile = new Rect(tileColumn.Center.X - (side / 2), tileColumn.Top + 4, side, side);
        var element = atom.Element;
        var color = element is null ? Ink.Dim : AtomColours.Category(element.Category);
        DrawTile(dc, tile, element, color, atom.Protons + atom.Neutrons > 0 ? atom.MassNumber : null, atom.Charge);
        _targets.AddBox("tile", tile);

        var y = tile.Bottom + 16;
        DrawTargetPill(dc, LocFormat("Atom_Label_MassNumber", atom.MassNumber, atom.Protons, atom.Neutrons), new Point(tileColumn.Center.X, y), Ink.Gold, "massnumber", 11);
        y += 26;
        var chargeColor = atom.Charge switch
        {
            0 => Ink.Green,
            > 0 => Ink.Red,
            _ => Ink.Violet,
        };
        DrawTargetPill(dc, LocFormat("Atom_Label_Charge", AtomModel.SignedCharge(atom.Charge)), new Point(tileColumn.Center.X, y), chargeColor, "charge", 11);
        y += 26;
        if (atom.Protons + atom.Neutrons > 0)
        {
            DrawTargetPill(dc, Loc(atom.IsStable ? "Atom_Label_Stable" : "Atom_Label_Radioactive"), new Point(tileColumn.Center.X, y),
                atom.IsStable ? Ink.Green : Ink.Orange, "stability", 11);
        }

        DrawTrays(dc, trays, atom);
    }

    /// <summary>A row per particle: its ball, its name and how many, and − and + buttons (dimmed at the limit).</summary>
    private void DrawTrays(PaintContext dc, Rect area, AtomBuild atom)
    {
        var rowHeight = Math.Clamp(area.Height / 3, 30, 46);
        var top = area.Top;
        for (var i = 0; i < Trays.Count; i++)
        {
            var (kind, tray, add, remove) = Trays[i];
            var row = new Rect(area.Left, top + (i * rowHeight), area.Width, rowHeight - 6);
            var (count, max, nameKey) = kind switch
            {
                Particle.Proton => (atom.Protons, AtomModel.MaxProtons, "Atom_Proton"),
                Particle.Neutron => (atom.Neutrons, AtomModel.MaxNeutrons, "Atom_Neutron"),
                _ => (atom.Electrons, AtomModel.MaxElectrons(atom.Protons), "Atom_Electron"),
            };
            var glow = Highlight == tray;
            dc.DrawRoundedRectangle(FadedBrush(Colors.White, 0.05), glow ? ExploreTargets.GlowPen(_drift, 3) : Ink.Frame, row, 8, 8);
            var midY = row.Center.Y;
            DrawParticle(dc, kind, new Point(row.Left + 16, midY), 8);
            var button = Math.Min(13, (row.Height / 2) - 1);
            var plus = new Point(row.Right - button - 6, midY);
            var minus = new Point(plus.X - (button * 2) - 8, midY);
            var nameRoom = minus.X - button - (row.Left + 30) - 6;
            var label = _labels.Text($"{Loc(nameKey)}  {count}", 12, Ink.Text, bold: true);
            if (label.Width > nameRoom) label = _labels.Text(Number(count), 13, Ink.Text, bold: true);
            dc.DrawText(label, new Point(row.Left + 30, midY - (label.Height / 2)));
            _targets.AddBox(tray, new Rect(row.Left, row.Top, Math.Max(20, minus.X - button - 4 - row.Left), row.Height));

            DrawButton(dc, minus, button, "−", count > 0);
            DrawButton(dc, plus, button, "+", count < max);
            _targets.Add(remove, minus, button);
            _targets.Add(add, plus, button);
            _addButtons[kind] = plus;
        }
    }

    private void DrawButton(PaintContext dc, Point center, double radius, string mark, bool enabled)
    {
        var alpha = enabled ? 1 : 0.35;
        dc.DrawEllipse(FadedBrush(Colors.White, 0.12 * alpha), FadedPen(Ink.Text, 0.7 * alpha, 1.4), center, radius, radius);
        DrawText(dc, mark, new Point(center.X, center.Y - 1), SpaceDrawing.WithAlpha(Ink.Text, alpha), radius * 1.3, TextAnchor.Center, bold: true);
    }

}
