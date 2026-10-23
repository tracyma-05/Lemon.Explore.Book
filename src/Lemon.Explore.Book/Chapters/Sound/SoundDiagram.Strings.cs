using Avalonia;
using Lemon.Explore.Book.Controls.Explore;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Sound;

/// <summary>
/// The strings view. A guitar's neck and body with one string, plucked (by the page or by dragging it) and ringing as
/// the sum of its harmonics, which are drawn one per row underneath, each fading at its own rate; or an air column in
/// an open or closed pipe, its dots of air moving in one standing wave, with the pipe's first resonances in rows below.
/// Both are slowed so the fundamental swings about once every two seconds; the shapes are exaggerated.
/// </summary>
public sealed partial class SoundDiagram
{
    /// <summary>The fundamental's swings per second on screen at 1×.</summary>
    private const double StringVisibleHz = 0.5;

    /// <summary>How many modes make up the drawn string's shape.</summary>
    private const int StringModes = 40;

    private const int HarmonicRows = 6;
    private const int PipeRows = 4;

    private double _stringTime;
    private double _pipeTime;

    /// <summary>Where the dragged string is pulled to: a fraction of the vibrating length and a height (−1…1).</summary>
    private double _pullPoint;
    private double _pullHeight;

    private StringLayout _string;
    private PipeLayout _pipe;

    /// <summary>The guitar: nut and bridge, the board, the body, the vibrating part and the harmonic rows.</summary>
    private readonly record struct StringLayout(double Nut, double Bridge, double Y, double BoardHeight, double Start, double MaxPull, Rect Board, Rect Rows, double RowHeight)
    {
        public double Scale => Bridge - Nut;

        /// <summary>The x of fret <paramref name="k"/> (0 is the nut).</summary>
        public double FretX(double k) => Nut + (Scale * (1 - Math.Pow(2, -k / 12)));

        /// <summary>The x of a point a fraction <paramref name="u"/> along the vibrating part.</summary>
        public double At(double u) => Start + ((Bridge - Start) * u);

        /// <summary>The fraction along the vibrating part at <paramref name="x"/>.</summary>
        public double Fraction(double x) => (x - Start) / Math.Max(1, Bridge - Start);
    }

    /// <summary>The pipe: its tube and the rows of its first resonances.</summary>
    private readonly record struct PipeLayout(Rect Tube, Rect Rows, double RowHeight);

    private void RestartString()
    {
        _stringTime = 0;
        _pipeTime = 0;
    }

    /// <summary>How much of harmonic <paramref name="n"/> is left after <paramref name="t"/> seconds on screen.</summary>
    private static double Fade(int n, double t) => Math.Exp(-t / (11 / Math.Pow(n, 0.75)));

    private void UpdateString()
    {
        var stage = Stage();
        var boardHeight = Math.Clamp(stage.Height * 0.15, 44, 70);
        var y = stage.Top + Math.Clamp(stage.Height * 0.2, 64, 120);
        // Room on the left for the harmonic rows' labels, which line up under the vibrating part.
        var nut = stage.Left + 150;
        var bridge = stage.Right - 80;
        var start = nut + ((bridge - nut) * (1 - Math.Pow(2, -Fret / 12.0)));
        var board = new Rect(nut, y - (boardHeight / 2), (bridge - nut) * (1 - Math.Pow(2, -19 / 12.0)), boardHeight);
        var rowsTop = y + (boardHeight * 1.25) + 34;
        var rows = new Rect(stage.Left, rowsTop, stage.Width, Math.Max(60, stage.Bottom - rowsTop - 6));
        _string = new StringLayout(nut, bridge, y, boardHeight, start, boardHeight * 0.95, board, rows, Math.Min(58, rows.Height / HarmonicRows));
    }

    /// <summary>The string's displacement (in "pulled" units, −1…1) at a fraction <paramref name="u"/> along its vibrating part.</summary>
    private double StringY(double u)
    {
        if (_drag == DragKind.String)
        {
            // Held: two straight lines to the finger.
            return u <= _pullPoint ? _pullHeight * u / Math.Max(1e-3, _pullPoint) : _pullHeight * (1 - u) / Math.Max(1e-3, 1 - _pullPoint);
        }

        var periods = _stringTime * StringVisibleHz;
        if (SoloHarmonic > 0)
        {
            var n = SoloHarmonic;
            return 0.7 * Math.Sin(n * Math.PI * u) * Math.Cos(2 * Math.PI * n * periods) * Fade(n, _stringTime * 0.6);
        }

        return PluckHeight * SoundModel.StringShape(u, periods, PluckPoint, StringModes, n => Fade(n, _stringTime));
    }

    private void DrawStringMode(PaintContext dc, double now)
    {
        UpdateString();
        var s = _string;
        DrawGuitar(dc, s);

        // The part between the nut and the finger lies still on the board.
        var steel = Ink.Steel;
        if (s.Start > s.Nut + 1) dc.DrawLine(FadedPen(steel, 0.55, 1.8), new Point(s.Nut, s.Y), new Point(s.Start, s.Y));

        var points = new List<Point>();
        const int Samples = 180;
        for (var i = 0; i <= Samples; i++)
        {
            var u = i / (double)Samples;
            points.Add(new Point(s.At(u), s.Y - (StringY(u) * s.MaxPull)));
        }

        var line = Line(points);
        if (Glows("string")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 9), line);
        dc.DrawGeometry(null, FadedPen(Ink.Text, 0.12, 6), line);
        dc.DrawGeometry(null, FadedPen(steel, 1, 2), line);
        _targets.Add("string", new Point(s.At(0.62), s.Y - (StringY(0.62) * s.MaxPull)), 12);

        // The pick, where the next pluck will be (or the finger holding the string).
        if (_drag != DragKind.String)
        {
            var pickX = s.At(PluckPoint);
            var pick = new Point(pickX, s.Y - s.BoardHeight * 0.95);
            var tri = Polygon(new Point(pick.X - 8, pick.Y - 7), new Point(pick.X + 8, pick.Y - 7), new Point(pick.X, pick.Y + 7));
            dc.DrawGeometry(FadedBrush(Ink.Orange, 0.9), FadedPen(Colors.White, 0.5, 1), tri);
            dc.DrawLine(FadedPen(Ink.Orange, 0.35, 1, dashed: true), new Point(pickX, pick.Y + 8), new Point(pickX, s.Y - 4));
            _targets.Add("pluck", pick, 10);
        }
        else
        {
            var at = new Point(s.At(_pullPoint), s.Y - (_pullHeight * s.MaxPull));
            dc.DrawEllipse(FadedBrush(Ink.Orange, 0.9), FadedPen(Colors.White, 0.7, 1), at, 6, 6);
        }

        if (ShowLabels)
        {
            var title = LocFormat("Sound_Label_StringNote", SoundModel.NoteName(StringFrequency), Hertz(StringFrequency));
            DrawPill(dc, title, new Point(s.At(0.5), s.Y - s.BoardHeight - 18), Ink.Text, 12.5);
        }

        if (ShowHarmonics) DrawHarmonicRows(dc, s, now);
    }

    /// <summary>The neck with its frets, the body with the sound hole, and the bridge.</summary>
    private void DrawGuitar(PaintContext dc, StringLayout s)
    {
        // The body: wider than the neck, from about the 14th fret to past the bridge.
        var bodyLeft = s.FretX(14);
        var body = new Rect(bodyLeft, s.Y - (s.BoardHeight * 1.25), s.Bridge + 54 - bodyLeft, s.BoardHeight * 2.5);
        dc.DrawRoundedRectangle(SpaceDrawing.LinearGradient(Color.FromRgb(0xD9, 0xA8, 0x6C), Color.FromRgb(0x9E, 0x6A, 0x3A), 90),
            FadedPen(Colors.Black, 0.4, 1), body, body.Height * 0.45, body.Height * 0.45);
        var hole = new Point(s.FretX(23.5), s.Y);
        var holeRadius = s.BoardHeight * 0.78;
        dc.DrawEllipse(FadedBrush(Ink.Wood, 0.6), null, hole, holeRadius + 6, holeRadius + 6);
        dc.DrawEllipse(Solid(255, 0x14, 0x0C, 0x08), null, hole, holeRadius, holeRadius);
        _targets.Add("body", new Point(hole.X, hole.Y + holeRadius * 0.7), holeRadius * 0.45);

        // The fingerboard and its frets.
        dc.DrawRoundedRectangle(SpaceDrawing.LinearGradient(Color.FromRgb(0x4A, 0x30, 0x22), Color.FromRgb(0x2C, 0x1C, 0x14), 90),
            null, s.Board, 4, 4);
        var fretPen = FadedPen(Color.FromRgb(0xC8, 0xCC, 0xD4), 0.9, 2, flat: true);
        for (var k = 1; k <= 19; k++)
        {
            var x = s.FretX(k);
            dc.DrawLine(fretPen, new Point(x, s.Board.Top), new Point(x, s.Board.Bottom));
        }

        var inlay = FadedBrush(Ink.Ivory, 0.65);
        foreach (var k in new[] { 3, 5, 7, 9, 15, 17 }) dc.DrawEllipse(inlay, null, new Point((s.FretX(k - 1) + s.FretX(k)) / 2, s.Y), 3.5, 3.5);
        var twelve = (s.FretX(11) + s.FretX(12)) / 2;
        dc.DrawEllipse(inlay, null, new Point(twelve, s.Y - (s.BoardHeight * 0.26)), 3.5, 3.5);
        dc.DrawEllipse(inlay, null, new Point(twelve, s.Y + (s.BoardHeight * 0.26)), 3.5, 3.5);

        // Fret numbers under the board (a click there or on the board holds a fret down).
        var numberY = s.Board.Bottom + 11;
        for (var k = 1; k <= 12; k++)
        {
            // On a short neck only the marked frets keep their numbers, so they do not run together.
            if (s.FretX(k) - s.FretX(k - 1) < 17 && k is not (3 or 5 or 7 or 9 or 12) && k != Fret) continue;
            var color = k == Fret ? Ink.Gold : Ink.Dim;
            DrawText(dc, k.ToString(System.Globalization.CultureInfo.InvariantCulture), new Point((s.FretX(k - 1) + s.FretX(k)) / 2, numberY), color, 10.5, TextAnchor.Center);
        }

        AddBoxTarget("fret", new Rect(s.Nut, numberY - 7, s.FretX(12) - s.Nut, 14));

        // The nut and the bridge's saddle.
        dc.DrawRectangle(Solid(255, Ink.Ivory.R, Ink.Ivory.G, Ink.Ivory.B), null, new Rect(s.Nut - 6, s.Board.Top - 2, 6, s.Board.Height + 4));
        var saddle = new Rect(s.Bridge - 3, s.Y - (s.BoardHeight * 0.55), 7, s.BoardHeight * 1.1);
        dc.DrawRoundedRectangle(Solid(255, 0x2C, 0x1C, 0x14), null, new Rect(saddle.Left - 12, saddle.Top - 6, 32, saddle.Height + 12), 5, 5);
        dc.DrawRectangle(Solid(255, Ink.Ivory.R, Ink.Ivory.G, Ink.Ivory.B), null, saddle);
        AddBoxTarget("bridge", new Rect(saddle.Left - 12, saddle.Top - 6, 32, saddle.Height + 12));
        if (ShowLabels)
        {
            DrawText(dc, Loc("Sound_Label_Nut"), new Point(s.Nut - 3, s.Board.Top - 12), Ink.Dim, 10.5, TextAnchor.Center);
            DrawText(dc, Loc("Sound_Label_Bridge"), new Point(s.Bridge + 4, body.Top - 12), Ink.Dim, 10.5, TextAnchor.Center);
        }

        // The finger on the string just behind the fret held down.
        if (Fret > 0)
        {
            var at = new Point(s.FretX(Fret) - Math.Min(12, (s.FretX(Fret) - s.FretX(Fret - 1)) * 0.4), s.Y);
            dc.DrawEllipse(FadedBrush(Color.FromRgb(0xE8, 0xB4, 0x96), 0.95), FadedPen(Colors.Black, 0.35, 1), at, 9, 12);
            _targets.Add("fret", at, 12);
        }
    }

    /// <summary>The first harmonics one per row: each a standing wave with its nodes, as strong as it is in the pluck now.</summary>
    private void DrawHarmonicRows(PaintContext dc, StringLayout s, double now)
    {
        // While the string is held, the rows show what the pulled shape is made of (still, as it will start).
        var held = _drag == DragKind.String;
        var pluck = held ? _pullPoint : PluckPoint;
        var height = held ? _pullHeight : PluckHeight;
        var time = held ? 0 : _stringTime;
        var largest = 1e-6;
        for (var n = 1; n <= HarmonicRows; n++) largest = Math.Max(largest, Math.Abs(SoundModel.PluckAmplitude(n, pluck)));
        var periods = time * StringVisibleHz;
        var left = s.At(0);
        var right = s.At(1);
        for (var n = 1; n <= HarmonicRows; n++)
        {
            var cy = s.Rows.Top + ((n - 0.5) * s.RowHeight);
            var solo = SoloHarmonic > 0 && !held;
            var on = !solo || n == SoloHarmonic;
            var b = SoundModel.PluckAmplitude(n, pluck) / largest;
            var silenced = !solo && SoundModel.IsSilenced(n, pluck);
            var amount = solo ? (on ? Fade(n, time * 0.6) : 0) : b * Math.Abs(height) * Fade(n, time);
            var half = s.RowHeight * 0.44;
            var color = n == 1 ? Ink.Gold : Ink.Cyan;
            var alpha = on && !silenced ? 1 : 0.35;

            var row = new Rect(left - 140, cy - (s.RowHeight / 2) + 2, right - left + 150, s.RowHeight - 4);
            if (solo && on) dc.DrawRoundedRectangle(FadedBrush(color, 0.08), FadedPen(color, 0.4, 1), row, 8, 8);
            dc.DrawLine(FadedPen(Ink.Dim, 0.25 * alpha, 1), new Point(left, cy), new Point(right, cy));

            // The envelope (how far it can swing now) and the wave itself.
            var envelope = new List<Point>();
            var under = new List<Point>();
            var wave = new List<Point>();
            for (var i = 0; i <= 120; i++)
            {
                var u = i / 120.0;
                var shape = Math.Sin(n * Math.PI * u);
                envelope.Add(new Point(s.At(u), cy - (Math.Abs(amount * shape) * half)));
                under.Add(new Point(s.At(u), cy + (Math.Abs(amount * shape) * half)));
                wave.Add(new Point(s.At(u), cy - (amount * shape * Math.Cos(2 * Math.PI * n * periods) * half * Math.Sign(b == 0 ? 1 : b) * (height < 0 ? -1 : 1))));
            }

            var dashed = FadedPen(color, 0.35 * alpha, 1, dashed: true);
            dc.DrawGeometry(null, dashed, Line(envelope));
            dc.DrawGeometry(null, dashed, Line(under));
            dc.DrawGeometry(null, FadedPen(color, 0.95 * alpha, 1.8), Line(wave));

            // Nodes: where this harmonic never moves.
            for (var k = 0; k <= n; k++)
            {
                var node = new Point(s.At(k / (double)n), cy);
                dc.DrawEllipse(FadedBrush(Ink.Text, 0.85 * alpha), null, node, 2.6, 2.6);
                if (n == 2 && k == 1) _targets.Add("node", node, 7);
            }

            if (n == 2) _targets.Add("antinode", new Point(s.At(0.25), cy - (Math.Abs(amount) * half)), 8);

            var label = LocFormat("Sound_Label_Harmonic", n, Hertz(StringFrequency * n));
            var pill = DrawPill(dc, label, new Point(left - 82, cy), on && !silenced ? color : Ink.Dim, 11);
            if (silenced) DrawText(dc, Loc("Sound_Label_Missing"), new Point(right + 10, cy), Ink.Red, 10.5);
            AddBoxTarget(n == 1 ? "harmonic1" : $"harmonic{n}", new Rect(pill.Left, row.Top, row.Right - pill.Left, row.Height));
        }
    }

    #region pipes

    private void UpdatePipe()
    {
        var stage = Stage();
        var height = Math.Clamp(stage.Height * 0.17, 52, 84);
        var top = stage.Top + Math.Clamp(stage.Height * 0.2, 60, 116) - (height / 2);
        var tube = new Rect(stage.Left + 190, top, Math.Max(200, stage.Width - 190 - 80), height);
        var rowsTop = tube.Bottom + 74;
        var rows = new Rect(stage.Left, rowsTop, stage.Width, Math.Max(60, stage.Bottom - rowsTop - 6));
        _pipe = new PipeLayout(tube, rows, Math.Min(64, rows.Height / PipeRows));
    }

    private void DrawPipeMode(PaintContext dc, double now)
    {
        UpdatePipe();
        var p = _pipe;
        var tube = p.Tube;
        var closed = Resonator == Resonator.ClosedPipe;
        var mode = Math.Clamp(PipeMode, 1, PipeRows + 1);
        var harmonic = SoundModel.PipeHarmonic(mode, closed);
        var swing = Math.Cos(2 * Math.PI * StringVisibleHz * 0.9 * harmonic * _pipeTime);

        // The glass tube.
        dc.DrawRoundedRectangle(FadedBrush(Ink.Cyan, 0.05), FadedPen(Ink.Text, 0.55, 2), tube, 6, 6);
        AddBoxTarget("pipe", new Rect(tube.Left + (tube.Width * 0.3), tube.Top, tube.Width * 0.4, tube.Height));

        // The dots of air in it: they swing along the tube, as much as the mode allows where they are.
        var columns = (int)(tube.Width / 10);
        var rows = Math.Max(3, (int)(tube.Height / 10));
        var spacingX = tube.Width / columns;
        var spacingY = tube.Height / rows;
        var amplitude = Math.Min(14, spacingX * 1.4);
        const int Buckets = 11;
        var figures = new StreamGeometry[Buckets];
        var contexts = new StreamGeometryContext[Buckets];
        for (var b = 0; b < Buckets; b++)
        {
            figures[b] = new StreamGeometry();
            contexts[b] = figures[b].Open();
        }

        for (var c = 0; c < columns; c++)
        {
            var u = (c + 0.5) / columns;
            var shift = amplitude * SoundModel.PipeDisplacement(mode, u, closed) * swing;
            var pressure = SoundModel.PipePressure(mode, u, closed) * swing;
            var context = contexts[(int)Math.Round((Math.Clamp(pressure, -1, 1) + 1) / 2 * (Buckets - 1))];
            for (var r = 0; r < rows; r++)
            {
                var (jx, jy) = Jitter(c, r);
                var x = tube.Left + ((c + 0.5) * spacingX) + shift + (jx * spacingX * 0.3);
                var y = tube.Top + ((r + 0.5) * spacingY) + (jy * spacingY * 0.3);
                if (x < tube.Left + 2 || x > tube.Right - 2) continue;
                context.BeginFigure(new Point(x, y - 2), true);
                context.ArcTo(new Point(x, y + 2), new Size(2, 2), 0, false, SweepDirection.Clockwise);
                context.ArcTo(new Point(x, y - 2), new Size(2, 2), 0, false, SweepDirection.Clockwise);
                context.EndFigure(true);
            }
        }

        for (var b = 0; b < Buckets; b++)
        {
            contexts[b].Dispose();
            dc.DrawGeometry(DotBrush(b / (double)(Buckets - 1)), null, figures[b]);
        }

        // How far the air can move along the tube: wide at the open ends, nothing at a closed one.
        var top = new List<Point>();
        var bottom = new List<Point>();
        for (var i = 0; i <= 160; i++)
        {
            var u = i / 160.0;
            var d = Math.Abs(SoundModel.PipeDisplacement(mode, u, closed));
            top.Add(new Point(tube.Left + (tube.Width * u), tube.Top + (tube.Height / 2) - (d * ((tube.Height / 2) - 3))));
            bottom.Add(new Point(tube.Left + (tube.Width * u), tube.Top + (tube.Height / 2) + (d * ((tube.Height / 2) - 3))));
        }

        var envelopePen = FadedPen(Ink.Green, 0.7, 1.4, dashed: true);
        dc.DrawGeometry(null, envelopePen, Line(top));
        dc.DrawGeometry(null, envelopePen, Line(bottom));

        DrawPipeEnds(dc, tube, closed, swing);
        DrawPipeMarks(dc, tube, mode, closed);

        var frequency = SoundModel.PipeFrequency(mode, PipeLength, SoundModel.SpeedInAir(Temperature), closed);
        if (ShowLabels)
        {
            DrawPill(dc, LocFormat("Sound_Label_PipeLength", Metres(PipeLength)), new Point(tube.Left + (tube.Width / 2), tube.Bottom + 18), Ink.Dim, 11);
            DrawPill(dc, LocFormat("Sound_Label_PipeNote", SoundModel.NoteName(frequency), Hertz(frequency)), new Point(tube.Left + (tube.Width / 2), tube.Top - 40), Ink.Text, 12.5);
        }

        DrawPipeRows(dc, p, closed);
    }

    /// <summary>The mouth on the left (with the player's breath), and the far end: open, or capped.</summary>
    private void DrawPipeEnds(PaintContext dc, Rect tube, bool closed, double swing)
    {
        var cy = tube.Top + (tube.Height / 2);
        var ring = FadedPen(Ink.Text, 0.8, 2);
        dc.DrawEllipse(FadedBrush(Ink.Cyan, 0.12), ring, new Point(tube.Left, cy), 7, tube.Height / 2);

        // The breath blown across the mouth, wavering with the air inside.
        var jet = new List<Point>();
        for (var i = 0; i <= 20; i++)
        {
            var t = i / 20.0;
            jet.Add(new Point(tube.Left - 60 + (60 * t), cy - (tube.Height * 0.62) + (t * tube.Height * 0.28) + (Math.Sin(t * 9) * swing * 3)));
        }

        dc.DrawGeometry(null, FadedPen(Ink.Cyan, 0.6, 2), Line(jet));
        DrawArrowHead(dc, FadedBrush(Ink.Cyan, 0.7), jet[^1], new Vector(1, 0.45), 7);
        _targets.Add("openend", new Point(tube.Left, cy), 14);

        var far = new Point(tube.Right, cy);
        if (closed)
        {
            var cap = new Rect(tube.Right - 2, tube.Top - 4, 12, tube.Height + 8);
            dc.DrawRoundedRectangle(Solid(255, 0x8A, 0x92, 0xA2), FadedPen(Colors.White, 0.5, 1), cap, 3, 3);
            AddBoxTarget("closedend", cap);
        }
        else
        {
            dc.DrawEllipse(FadedBrush(Ink.Cyan, 0.12), ring, far, 7, tube.Height / 2);
            _targets.Add("openend", far, 14);
        }

        if (ShowLabels)
        {
            DrawText(dc, Loc("Sound_Label_Mouth"), new Point(tube.Left - 34, tube.Bottom + 18), Ink.Dim, 10.5, TextAnchor.Center);
            DrawText(dc, Loc(closed ? "Sound_Label_ClosedEnd" : "Sound_Label_OpenEnd"), new Point(tube.Right, tube.Bottom + 18), closed ? Ink.Orange : Ink.Dim, 10.5, TextAnchor.Center);
        }
    }

    /// <summary>"A" where the air moves most (displacement antinodes) and "N" where it stays still (nodes).</summary>
    private void DrawPipeMarks(PaintContext dc, Rect tube, int mode, bool closed)
    {
        var harmonic = SoundModel.PipeHarmonic(mode, closed);
        var y = tube.Top - 14;
        var firstNode = true;
        var firstAntinode = true;
        for (var k = 0; k <= harmonic * 2; k++)
        {
            // Open pipe: antinodes every 1/n, nodes halfway. Closed: antinodes every 2/m from the mouth, nodes between.
            var u = closed ? k / (double)harmonic : k / (2.0 * harmonic);
            if (u > 1 + 1e-9) break;
            var antinode = closed ? k % 2 == 0 : k % 2 == 0;
            var x = tube.Left + (tube.Width * u);
            var box = DrawPill(dc, Loc(antinode ? "Sound_Label_A" : "Sound_Label_N"), new Point(x, y), antinode ? Ink.Green : Ink.Orange, 10.5);
            if (antinode && firstAntinode)
            {
                AddBoxTarget("pantinode", box);
                firstAntinode = false;
            }
            else if (!antinode && firstNode)
            {
                AddBoxTarget("pnode", box);
                firstNode = false;
            }
        }
    }

    /// <summary>The pipe's first resonances one per row, the chosen one lit: a click picks another.</summary>
    private void DrawPipeRows(PaintContext dc, PipeLayout p, bool closed)
    {
        var speed = SoundModel.SpeedInAir(Temperature);
        for (var m = 1; m <= PipeRows; m++)
        {
            var cy = p.Rows.Top + ((m - 0.5) * p.RowHeight);
            var height = Math.Min(26, p.RowHeight * 0.48);
            var tube = new Rect(p.Tube.Left, cy - (height / 2), p.Tube.Width, height);
            var on = m == PipeMode;
            var color = on ? Ink.Green : Ink.Dim;
            var row = new Rect(tube.Left - 180, cy - (p.RowHeight / 2) + 2, tube.Width + 190, p.RowHeight - 4);
            if (on) dc.DrawRoundedRectangle(FadedBrush(Ink.Green, 0.08), FadedPen(Ink.Green, 0.4, 1), row, 8, 8);
            dc.DrawRoundedRectangle(null, FadedPen(Ink.Text, on ? 0.6 : 0.3, 1.2), tube, 4, 4);
            if (closed) dc.DrawRectangle(FadedBrush(Ink.Text, on ? 0.6 : 0.3), null, new Rect(tube.Right - 1, tube.Top - 2, 5, tube.Height + 4));

            var top = new List<Point>();
            var bottom = new List<Point>();
            for (var i = 0; i <= 120; i++)
            {
                var u = i / 120.0;
                var d = Math.Abs(SoundModel.PipeDisplacement(m, u, closed));
                top.Add(new Point(tube.Left + (tube.Width * u), cy - (d * ((height / 2) - 2))));
                bottom.Add(new Point(tube.Left + (tube.Width * u), cy + (d * ((height / 2) - 2))));
            }

            var pen = FadedPen(color, on ? 0.9 : 0.55, 1.3);
            dc.DrawGeometry(null, pen, Line(top));
            dc.DrawGeometry(null, pen, Line(bottom));

            var frequency = SoundModel.PipeFrequency(m, PipeLength, speed, closed);
            var label = LocFormat("Sound_Label_PipeMode", SoundModel.PipeHarmonic(m, closed), Hertz(frequency));
            var pill = DrawPill(dc, label, new Point(tube.Left - 100, cy), color, 11);
            AddBoxTarget($"pipemode{m}", new Rect(pill.Left, row.Top, row.Right - pill.Left, row.Height));
        }
    }

    #endregion
}
