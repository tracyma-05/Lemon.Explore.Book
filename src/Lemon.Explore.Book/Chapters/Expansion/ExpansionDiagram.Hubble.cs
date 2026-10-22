using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Expansion;

/// <summary>
/// The Hubble view: an endless sprinkling of galaxies, seen from home at the middle, drifting apart as space stretches
/// from 0.6 of today's size to now. Each galaxy's speed away from home is an arrow, twice as long twice as far away; three
/// tape measures give distances and speeds; the start of the span can be laid underneath like a second photo; and a grid
/// drawn on space stretches with it. Clicking any galaxy makes it home, and the picture glides over to it.
/// </summary>
public sealed partial class ExpansionDiagram
{
    /// <summary>Seconds the span takes to play.</summary>
    private const double HubbleSeconds = 14;

    /// <summary>Proper megaparsecs from home to the nearer edge of the stage.</summary>
    private const double HubbleView = 320;

    /// <summary>Arrow length per km/s, as a share of the stage's half-size.</summary>
    private const double ArrowScale = 0.15 / 22_000;

    /// <summary>The three tape measures, as cells relative to home, and their colours.</summary>
    private static readonly (int I, int J, Color Color)[] Rulers =
    [
        (1, 0, Color.FromRgb(0x7C, 0xFF, 0x9E)),
        (-2, 1, Color.FromRgb(0x6F, 0xD8, 0xFF)),
        (3, -2, Color.FromRgb(0xFF, 0x7E, 0xC0)),
    ];

    private readonly GalaxySprites _sprites = new();
    private readonly LineBatch _batch = new();

    /// <summary>The comoving point the view glides from after home moved, and how far the glide is (0 to 1).</summary>
    private Vector _panFrom;
    private double _pan = 1;

    private void OnHomeChanged(GalaxyCell old)
    {
        _panFrom = ViewCentre(GalaxyField.At(old).Position);
        _pan = 0;
    }

    private void StepPan(double dt)
    {
        if (_pan < 1) _pan = Math.Min(1, _pan + (dt / 0.8));
    }

    /// <summary>The comoving point at the middle of the view: home, or on the way to it.</summary>
    private Vector ViewCentre(Vector? from = null)
    {
        var home = GalaxyField.At(Home).Position;
        if (_pan >= 1) return home;
        var start = from ?? _panFrom;
        var s = Smooth(_pan);
        return start + ((home - start) * s);
    }

    private void StepHubble(double dt)
    {
        var progress = HubbleProgress;
        if (progress >= 1)
        {
            // Stay on "now" a moment, then start over; a walkthrough step showing "now" holds it.
            if (IsLessonActive) return;
            _hold += dt;
            if (_hold < 2.5) return;
            _hold = 0;
            SetCurrentValue(HubbleProgressProperty, 0.0);
            return;
        }

        SetCurrentValue(HubbleProgressProperty, Math.Min(1, progress + (dt / HubbleSeconds)));
    }

    /// <summary>Screen pixels per proper megaparsec.</summary>
    private double HubbleZoom(Rect stage) => 0.5 * Math.Min(stage.Width, stage.Height) / HubbleView;

    private Point HubbleCentre(Rect stage) => new(stage.X + (stage.Width / 2), stage.Y + (stage.Height / 2));

    /// <summary>Where a galaxy at a comoving place is drawn, at scale factor <paramref name="a"/>.</summary>
    private Point HubblePoint(Vector comoving, double a, Rect stage, Vector centre) =>
        HubbleCentre(stage) + ((comoving - centre) * a * HubbleZoom(stage));

    /// <summary>The galaxy nearest a screen point (within a few pixels of its picture), or null.</summary>
    private FieldGalaxy? GalaxyAt(Point point)
    {
        var stage = Stage();
        if (!stage.Contains(point)) return null;
        var a = HubbleSpan.Scale(HubbleProgress);
        var k = HubbleZoom(stage);
        var centre = ViewCentre();
        var comoving = centre + ((point - HubbleCentre(stage)) / (a * k));
        var reach = 18 / (a * k);
        FieldGalaxy? best = null;
        var bestDistance = double.MaxValue;
        foreach (var galaxy in GalaxyField.Within(new Rect(comoving.X - reach, comoving.Y - reach, 2 * reach, 2 * reach)))
        {
            var d = ((Vector)(HubblePoint(galaxy.Position, a, stage, centre) - point)).Length;
            if (d < bestDistance && d < 16)
            {
                best = galaxy;
                bestDistance = d;
            }
        }

        return best;
    }

    private void DrawHubbleMode(PaintContext dc, double now)
    {
        var stage = Stage();
        var dpi = RenderScale();
        _stars.Draw(dc, ActualWidth, ActualHeight, dpi, now, 0.3);

        var a = HubbleSpan.Scale(HubbleProgress);
        var hubble = Expansion.Universe.Ours.Hubble(a);
        var k = HubbleZoom(stage);
        var centre = ViewCentre();
        var home = GalaxyField.At(Home);
        var homePoint = HubblePoint(home.Position, a, stage, centre);
        var half = 0.5 * Math.Min(stage.Width, stage.Height);
        var arrowPerKms = ArrowScale * half;

        dc.PushClip(new RectangleGeometry(new Rect(0, stage.Top, ActualWidth, stage.Height)));

        // The comoving box the stage shows (with a margin so galaxies slide in from outside).
        var reachX = (stage.Width / 2 / (a * k)) + GalaxyField.Spacing;
        var reachY = (stage.Height / 2 / (a * k)) + GalaxyField.Spacing;
        var box = new Rect(centre.X - reachX, centre.Y - reachY, 2 * reachX, 2 * reachY);
        var galaxies = GalaxyField.Within(box).ToList();

        if (ShowGrid) DrawHubbleGrid(dc, stage, a, k, centre, box);

        // The span's start, laid under the picture and lined up on home: every galaxy has moved straight away from home,
        // the far ones further.
        if (ShowOverlay)
        {
            // Opaque (a dark cyan) rather than translucent: hundreds of translucent strokes in one geometry are slow to draw.
            var trailPen = FadedPen(Color.FromRgb(0x2A, 0x55, 0x6A), 1, 1, flat: true);
            var start = HubbleSpan.StartScale;
            var ghosts = new List<Point>();
            foreach (var galaxy in galaxies)
            {
                var then = homePoint + ((galaxy.Position - home.Position) * start * k);
                var nowPoint = HubblePoint(galaxy.Position, a, stage, centre);
                if (!stage.Contains(then) && !stage.Contains(nowPoint)) continue;
                _batch.Add(trailPen, then, nowPoint);
                ghosts.Add(then);
            }

            _batch.Draw(dc);
            foreach (var then in ghosts) _sprites.DrawRing(dc, then, 4, dpi);
        }

        // Arrows: each galaxy's speed away from home, v = H × d.
        // Only round home (out to a little past the stage's half-height, fading), so the picture stays readable.
        if (ShowArrows)
        {
            var reach = half * 1.15;
            var heads = new Dictionary<int, (StreamGeometry Geometry, StreamGeometryContext Context)>();
            foreach (var galaxy in galaxies)
            {
                if (galaxy.Cell == Home) continue;
                var p = HubblePoint(galaxy.Position, a, stage, centre);
                var fromHome = ((Vector)(p - homePoint)).Length;
                if (!stage.Contains(p) || fromHome > reach) continue;
                var d = (galaxy.Position - home.Position) * a;
                var length = hubble * d.Length * arrowPerKms;
                if (length < 3) continue;
                var fade = (int)Math.Round(Math.Clamp((reach - fromHome) / (reach * 0.25), 0.25, 1) * 4);
                var dir = d / d.Length;
                var from = p + (dir * 7);
                var tip = from + (dir * length);
                var headSize = Math.Min(6, length * 0.5);
                _batch.Add(FadedPen(Ink.Gold, 0.8 * fade / 4, 1.5, flat: true), from, tip - (dir * headSize * 0.7));
                if (!heads.TryGetValue(fade, out var head))
                {
                    var geometry = new StreamGeometry();
                    heads[fade] = head = (geometry, geometry.Open());
                }

                var normal = new Vector(-dir.Y, dir.X);
                var back = tip - (dir * headSize);
                head.Context.BeginFigure(tip, true);
                head.Context.LineTo(back + (normal * headSize * 0.5));
                head.Context.LineTo(back - (normal * headSize * 0.5));
                head.Context.EndFigure(true);
            }

            _batch.Draw(dc);
            foreach (var (fade, head) in heads)
            {
                head.Context.Dispose();
                dc.DrawGeometry(FadedBrush(Ink.Gold, 0.9 * fade / 4), null, head.Geometry);
            }
        }

        // The galaxies themselves: the same size all the time (they are held together by their own gravity).
        var baseRadius = Math.Clamp(half * 0.028, 6, 10);
        foreach (var galaxy in galaxies)
        {
            if (galaxy.Cell == Home || galaxy.Cell.IsMilkyWay) continue;
            var p = HubblePoint(galaxy.Position, a, stage, centre);
            if (p.X < -20 || p.X > ActualWidth + 20 || p.Y < stage.Top - 20 || p.Y > stage.Bottom + 20) continue;
            _sprites.Draw(dc, galaxy.Look, p, baseRadius * galaxy.Size, dpi);
            if (stage.Contains(p)) _targets.Add("galaxy", p, 10);
        }

        // The Milky Way (wherever it is) and home.
        var milkyWay = HubblePoint(new Vector(0, 0), a, stage, centre);
        if (!Home.IsMilkyWay && milkyWay.X > -20 && milkyWay.X < ActualWidth + 20 && milkyWay.Y > stage.Top - 20 && milkyWay.Y < stage.Bottom + 20)
        {
            _sprites.DrawMilkyWay(dc, milkyWay, baseRadius * 1.25, dpi);
            if (stage.Contains(milkyWay))
            {
                _targets.Add("galaxy", milkyWay, 11);
                if (ShowLabels) DrawPill(dc, Loc("Expand_Label_MilkyWay"), milkyWay + new Vector(0, baseRadius + 14), Ink.Gold, 10.5);
            }
        }

        if (ShowRulers) DrawRulers(dc, stage, a, hubble, k, centre, home, homePoint, baseRadius);

        var homeRadius = baseRadius * 1.35;
        var glow = 0.5 + (0.5 * Math.Sin(now * 2.4));
        dc.DrawEllipse(null, FadedPen(Ink.Gold, 0.35 + (0.3 * glow), 1.6), homePoint, homeRadius + 7, homeRadius + 7);
        if (Home.IsMilkyWay) _sprites.DrawMilkyWay(dc, homePoint, homeRadius, dpi);
        else _sprites.Draw(dc, home.Look, homePoint, homeRadius, dpi);
        _targets.Add("home", homePoint, homeRadius + 6);
        if (ShowLabels)
        {
            var label = Home.IsMilkyWay ? Loc("Expand_Label_HomeMilkyWay") : Loc("Expand_Label_NewHome");
            DrawPill(dc, label, homePoint + new Vector(0, homeRadius + 18), Ink.Gold);
        }

        // The arrow the walkthrough points at: the far tape measure's galaxy's.
        if (ShowArrows)
        {
            var far = GalaxyField.At(new GalaxyCell(Home.I + Rulers[2].I, Home.J + Rulers[2].J));
            var p = HubblePoint(far.Position, a, stage, centre);
            var d = (far.Position - home.Position) * a;
            if (d.Length > 1 && stage.Contains(p))
            {
                var dir = d / d.Length;
                _targets.Add("arrow", p + (dir * (7 + (hubble * d.Length * arrowPerKms * 0.55))), 10);
            }
        }

        if (ShowOverlay)
        {
            var mid = GalaxyField.At(new GalaxyCell(Home.I + Rulers[1].I, Home.J + Rulers[1].J));
            var then = homePoint + ((mid.Position - home.Position) * HubbleSpan.StartScale * k);
            if (stage.Contains(then)) _targets.Add("ghost", then, 8);
        }

        // The galaxy the walkthrough points at (added last so its ring lands here): the middle tape measure's.
        var marked = GalaxyField.At(new GalaxyCell(Home.I + Rulers[1].I, Home.J + Rulers[1].J));
        var markedPoint = HubblePoint(marked.Position, a, stage, centre);
        if (stage.Contains(markedPoint)) _targets.Add("galaxy", markedPoint, 10);

        dc.Pop();

        DrawHubbleReadouts(dc, stage, a, hubble);
    }

    /// <summary>A grid ruled on space itself: its squares grow with the scale factor, the galaxies stay in their squares.</summary>
    private void DrawHubbleGrid(PaintContext dc, Rect stage, double a, double k, Vector centre, Rect box)
    {
        var pen = FadedPen(Ink.Cyan, Glows("grid") ? 0.55 : 0.2, Glows("grid") ? 1.6 : 1, flat: true);
        var s = GalaxyField.Spacing;
        var i0 = (int)Math.Floor(box.Left / s);
        var i1 = (int)Math.Ceiling(box.Right / s);
        var j0 = (int)Math.Floor(box.Top / s);
        var j1 = (int)Math.Ceiling(box.Bottom / s);
        for (var i = i0; i <= i1; i++)
        {
            var x = HubblePoint(new Vector((i + 0.5) * s, 0), a, stage, centre).X;
            if (x >= 0 && x <= ActualWidth) _batch.Add(pen, new Point(x, stage.Top), new Point(x, stage.Bottom));
        }

        for (var j = j0; j <= j1; j++)
        {
            var y = HubblePoint(new Vector(0, (j + 0.5) * s), a, stage, centre).Y;
            if (y >= stage.Top && y <= stage.Bottom) _batch.Add(pen, new Point(0, y), new Point(ActualWidth, y));
        }

        _batch.Draw(dc);

        // A crossing near the top left stands for the grid.
        var corner = new Point(stage.X + (stage.Width * 0.18), stage.Y + (stage.Height * 0.2));
        var ci = Math.Round(((corner.X - HubbleCentre(stage).X) / (a * k) + centre.X) / s - 0.5);
        var cj = Math.Round(((corner.Y - HubbleCentre(stage).Y) / (a * k) + centre.Y) / s - 0.5);
        var crossing = HubblePoint(new Vector((ci + 0.5) * s, (cj + 0.5) * s), a, stage, centre);
        if (stage.Contains(crossing)) _targets.Add("grid", crossing, 9);
    }

    /// <summary>Tape measures from home to three galaxies: how far, and how fast it moves away.</summary>
    private void DrawRulers(PaintContext dc, Rect stage, double a, double hubble, double k, Vector centre, FieldGalaxy home, Point homePoint, double baseRadius)
    {
        for (var r = 0; r < Rulers.Length; r++)
        {
            var (di, dj, color) = Rulers[r];
            var galaxy = GalaxyField.At(new GalaxyCell(Home.I + di, Home.J + dj));
            var p = HubblePoint(galaxy.Position, a, stage, centre);
            var d = (galaxy.Position - home.Position) * a;
            var distance = d.Length;
            if (distance < 1) continue;
            var dir = (p - homePoint) / Math.Max(1, ((Vector)(p - homePoint)).Length);
            var from = homePoint + (dir * (baseRadius * 1.35 + 4));
            var to = p - (dir * (baseRadius + 3));
            if (((Vector)(to - from)).Length < 8) continue;
            dc.DrawLine(FadedPen(color, Glows("ruler") ? 1 : 0.75, Glows("ruler") ? 2.2 : 1.4, dashed: true), from, to);
            dc.DrawEllipse(null, FadedPen(color, 0.9, 1.4), p, baseRadius + 3, baseRadius + 3);
            if (!ShowLabels) continue;

            var text = LocFormat("Expand_Ruler", ExpansionFormat.Distance(distance), ExpansionFormat.Speed(hubble * distance));
            var size = PillSize(text, 11);
            var normal = new Vector(-dir.Y, dir.X);
            if (normal.Y > 0) normal = -normal;
            var at = p + (dir * (size.Width / 2 + baseRadius + 6)) + (normal * 6);
            at = at.WithX(Math.Clamp(at.X, stage.Left + (size.Width / 2) + 4, stage.Right - (size.Width / 2) - 4));
            at = at.WithY(Math.Clamp(at.Y, stage.Top + 12, stage.Bottom - 12));
            var pill = DrawPill(dc, text, at, color, 11);
            AddBoxTarget("ruler", pill);
        }
    }

    /// <summary>The clock (how long ago, how big) at the top and Hubble's law with today's rate at the bottom.</summary>
    private void DrawHubbleReadouts(PaintContext dc, Rect stage, double a, double hubble)
    {
        var ago = Expansion.Universe.Ours.Age - HubbleSpan.Time(HubbleProgress);
        var when = ago > 0.02 ? LocFormat("Expand_Ago", ExpansionFormat.Time(ago)) : Loc("Expand_Now");
        var clock = LocFormat("Expand_Hubble_Clock", when, Number(a, "0.00"));
        var clockSize = PillSize(clock, 12.5);
        DrawTargetPill(dc, clock, new Point(stage.Right - (clockSize.Width / 2) - 14, stage.Top + 4), Ink.Text, "clock", 12.5);

        var law = LocFormat("Expand_Hubble_Law", Number(hubble, "0"));
        var lawSize = PillSize(law, 12.5);
        DrawTargetPill(dc, law, new Point(stage.Left + (lawSize.Width / 2) + 14, stage.Bottom - 8), Ink.Gold, "law", 12.5);
    }
}
