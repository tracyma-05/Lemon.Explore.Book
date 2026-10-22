using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.StarLife;

/// <summary>
/// The fates view: from a cloud of gas on the left, four rows by mass, each a few steps to its end: red dwarf to a
/// helium white dwarf; Sun-like star to red giant, planetary nebula and white dwarf; massive star to red supergiant,
/// supernova and neutron star; the heaviest to a black hole. The chosen star's row is lit, with a ring on where it is
/// now; clicking a row picks a star of that kind.
/// </summary>
public sealed partial class StarDiagram
{
    /// <summary>The rows: their kind, the mass a click picks, and the stages shown in columns 1–4 (none for a gap).</summary>
    private static readonly (StarClass Class, double Mass, StarStage?[] Stages)[] FateRows =
    [
        (StarClass.RedDwarf, 0.2, [StarStage.MainSequence, StarStage.BlueDwarf, null, StarStage.WhiteDwarf]),
        (StarClass.SunLike, 1, [StarStage.MainSequence, StarStage.RedGiant, StarStage.PlanetaryNebula, StarStage.WhiteDwarf]),
        (StarClass.Massive, 12, [StarStage.MainSequence, StarStage.RedSupergiant, StarStage.Supernova, StarStage.NeutronStar]),
        (StarClass.VeryMassive, 30, [StarStage.MainSequence, StarStage.RedSupergiant, StarStage.Supernova, StarStage.BlackHole]),
    ];

    private const double FatesTop = 170;
    private double FatesBottom => 110 + _reserve.Current;
    private const double FatesLeft = 210;

    private double FateRowHeight => Math.Max(60, (ActualHeight - FatesTop - FatesBottom) / FateRows.Length);

    private double FateRowY(int row) => FatesTop + (FateRowHeight * (row + 0.5));

    private double FateColumnX(int column)
    {
        var right = ActualWidth - 70;
        return FatesLeft + 70 + ((right - FatesLeft - 70) * column / 3);
    }

    private void DrawFates(PaintContext dc, StarState state, double now)
    {
        var current = StarModel.ClassOf(Mass);
        var root = new Point(70, FatesTop + (FateRowHeight * FateRows.Length / 2));

        // The cloud every star starts from.
        dc.DrawEllipse(FatesPaint.Cloud, null, root, 46, 34);
        _labels.DrawCentered(dc, "Star_Fates_Cloud", new Point(root.X, root.Y + 40), 12, StarDrawing.Text, true);
        _targets.Add(StageTarget(StarStage.Protostar), root, 40);

        // The chosen kind's pictures are added last, so the walkthrough's ring finds them before another row's.
        var litTargets = new List<(string Id, Point At)>();

        for (var row = 0; row < FateRows.Length; row++)
        {
            var (kind, mass, stages) = FateRows[row];
            var lit = kind == current;
            var y = FateRowY(row);
            var opacity = lit ? 1 : 0.42;
            dc.PushOpacity(opacity);

            var band = new Rect(16, y - (FateRowHeight / 2) + 4, ActualWidth - 32, FateRowHeight - 8);
            if (lit) dc.DrawRoundedRectangle(FatesPaint.LitRow, FatesPaint.LitEdge, band, 14, 14);
            _targets.AddBox(ClassPrefix + kind, band);

            // From the cloud to the row, then along it.
            var start = new Point(FatesLeft - 8, y);
            var curve = new StreamGeometry();
            using (var ctx = curve.Open())
            {
                ctx.BeginFigure(new Point(root.X + 46, root.Y), false);
                ctx.CubicBezierTo(new Point(root.X + 110, root.Y), new Point(start.X - 90, y), start);

                ctx.EndFigure(false);
            }
            dc.DrawGeometry(null, lit ? FatesPaint.LitPath : FatesPaint.Path, curve);
            dc.DrawLine(lit ? FatesPaint.LitPath : FatesPaint.Path, start, new Point(FateColumnX(3), y));

            // The kind of star and how long it shines.
            var nameX = FateColumnX(0) + 28;
            var name = _labels.Get($"Star_Class_{kind}", 13, lit ? StarDrawing.Gold : StarDrawing.Text, true);
            dc.DrawText(name, new Point(nameX, y - name.Height - 24));
            var range = _labels.Get($"Star_Class_{kind}_Range", 11, StarDrawing.DimText);
            dc.DrawText(range, new Point(nameX + name.Width + 10, y - name.Height - 22 + ((name.Height - range.Height) / 2)));

            var track = StarModel.Track(mass);
            for (var column = 0; column < stages.Length; column++)
            {
                if (stages[column] is not { } stage) continue;
                var at = new Point(FateColumnX(column), y);
                DrawFateNode(dc, track, stage, mass, at, now);
                if (lit) litTargets.Add((StageTarget(stage), at));
                else _targets.Add(StageTarget(stage), at, 20);
                var label = _labels.Get(StageKey(stage), 11, StarDrawing.DimText);
                dc.DrawText(label, new Point(at.X - (label.Width / 2), at.Y + 22));

                // Where the chosen star is now.
                if (lit && ColumnOf(state.Stage) == column)
                {
                    var pulse = 26 + (3 * Math.Sin(now * 4));
                    dc.DrawEllipse(null, FatesPaint.NowRing, at, pulse, pulse);
                }
            }

            if (lit && state.Stage == StarStage.Protostar)
            {
                var pulse = 18 + (3 * Math.Sin(now * 4));
                dc.DrawEllipse(null, FatesPaint.NowRing, start, pulse, pulse);
            }

            dc.Pop();
        }

        foreach (var (id, at) in litTargets) _targets.Add(id, at, 20);
    }

    /// <summary>The column a stage is shown in, the stages before and after it folded in with it.</summary>
    private static int ColumnOf(StarStage stage) => stage switch
    {
        StarStage.Protostar => -1,
        StarStage.MainSequence => 0,
        StarStage.PlanetaryNebula or StarStage.Supernova => 2,
        StarStage.WhiteDwarf or StarStage.NeutronStar or StarStage.BlackHole => 3,
        _ => 1,
    };

    /// <summary>A small picture of a stage: the star in its colour, sized by its radius, or the remnant's own look.</summary>
    private void DrawFateNode(PaintContext dc, IReadOnlyList<StarStageSpan> track, StarStage stage, double mass, Point at, double now)
    {
        switch (stage)
        {
            case StarStage.PlanetaryNebula:
                dc.DrawEllipse(FatesPaint.Nebula, null, at, 20, 18);
                StarDrawing.DrawStar(dc, at, 2.5, 100_000, 100, now, false);
                return;
            case StarStage.Supernova:
                dc.DrawEllipse(FatesPaint.Blast, null, at, 22, 22);
                return;
            case StarStage.NeutronStar:
                dc.DrawEllipse(FatesPaint.PulsarGlow, null, at, 12, 12);
                dc.DrawLine(FatesPaint.Beam, at + new Vector(-16, -9), at + new Vector(16, 9));
                dc.DrawEllipse(FatesPaint.PulsarCore, null, at, 2.5, 2.5);
                return;
            case StarStage.BlackHole:
                dc.DrawEllipse(FatesPaint.Disc, null, at, 20, 6);
                dc.DrawEllipse(Brushes.Black, FatesPaint.Ring, at, 7, 7);
                return;
            case StarStage.WhiteDwarf:
                StarDrawing.DrawStar(dc, at, 3, 15000, 0.01, now, false);
                return;
        }

        // Mid-stage on the model's track; the size on a gentle scale so a supergiant still fits the row.
        var index = Enumerable.Range(0, track.Count).FirstOrDefault(i => track[i].Stage == stage);
        var state = StarModel.At(track, mass, StarModel.StoryStart(track, index) + (track[index].Seconds * (stage == StarStage.MainSequence ? 0.2 : 0.95)));
        var radius = Math.Clamp(6 * Math.Pow(state.Radius, 0.22), 3, 22);
        StarDrawing.DrawStar(dc, at, radius, state.Temperature, Math.Min(state.Luminosity, 10), now, state.Radius > 20);
    }

    private static class FatesPaint
    {
        public static readonly IBrush Cloud = SpaceDrawing.RadialGradient(
            (Color.FromArgb(150, 0xC8, 0x6A, 0x50), 0),
            (Color.FromArgb(70, 0x7A, 0x3A, 0x5A), 0.6),
            (Color.FromArgb(0, 0x40, 0x20, 0x40), 1));

        public static readonly IBrush LitRow = Solid(18, 0xFF, 0xD3, 0x6B);
        public static readonly IPen LitEdge = MakePen(Solid(80, 0xFF, 0xD3, 0x6B), 1);
        public static readonly IPen Path = MakePen(Solid(90, 255, 255, 255), 1.4);
        public static readonly IPen LitPath = MakePen(Solid(220, 0xFF, 0xD3, 0x6B), 2);
        public static readonly IPen NowRing = MakePen(Solid(230, 0xFF, 0xD3, 0x6B), 2);

        public static readonly IBrush Nebula = SpaceDrawing.RadialGradient(
            (Color.FromArgb(0, 0x6F, 0xE3, 0xC0), 0.3),
            (Color.FromArgb(120, 0x6F, 0xE3, 0xC0), 0.7),
            (Color.FromArgb(150, 0xFF, 0x7E, 0xB6), 0.9),
            (Color.FromArgb(0, 0xFF, 0x7E, 0xB6), 1));

        public static readonly IBrush Blast = SpaceDrawing.RadialGradient(
            (Color.FromArgb(255, 255, 255, 255), 0),
            (Color.FromArgb(200, 0xFF, 0xD0, 0x90), 0.3),
            (Color.FromArgb(120, 0xFF, 0x9A, 0x5A), 0.7),
            (Color.FromArgb(0, 0xFF, 0x9A, 0x5A), 1));

        public static readonly IBrush PulsarGlow = SpaceDrawing.RadialGradient((Color.FromArgb(220, 0xC8, 0xDC, 0xFF), 0), (Color.FromArgb(0, 0x80, 0xA0, 0xFF), 1));
        public static readonly IBrush PulsarCore = Solid(255, 255, 255, 255);
        public static readonly IPen Beam = MakePen(Solid(160, 0xC8, 0xDC, 0xFF), 2);

        public static readonly IBrush Disc = SpaceDrawing.RadialGradient(
            (Color.FromArgb(0, 0xFF, 0xC0, 0x80), 0.3),
            (Color.FromArgb(230, 0xFF, 0xD0, 0x90), 0.4),
            (Color.FromArgb(0, 0xC0, 0x40, 0x20), 1));

        public static readonly IPen Ring = MakePen(Solid(230, 0xFF, 0xE6, 0xC0), 1.4);
    }
}
