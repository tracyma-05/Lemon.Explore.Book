using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Sound;

/// <summary>
/// The wave view: a loudspeaker on the left pushes the air, dots of air (each a crowd of molecules) bunch into
/// compressions and spread into rarefactions on their way to an ear on the right; under them the pressure along the
/// way and a ruler in metres. Lengths are to scale (the ruler zooms in steps so a few wavelengths fit); time is slowed
/// so the sound crosses the picture at <see cref="AirPixelsPerSecond"/>, and the dots' movement is exaggerated.
/// </summary>
/// <remarks>
/// The cone's position is recorded as it moves, and each dot repeats what the cone did a moment earlier (the time the
/// sound needs to reach it), so a pulse pushed by hand travels like the tone does and a new pitch leaves the speaker
/// while the old one is still on its way.
/// </remarks>
public sealed partial class SoundDiagram
{
    /// <summary>How fast the sound crosses the picture, DIPs per second at 1×.</summary>
    private const double AirPixelsPerSecond = 150;

    /// <summary>The widths of air the picture can show, m: it picks the one that fits a few wavelengths.</summary>
    private static readonly double[] Spans = [0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 20, 50];

    private const int HistorySize = 4096;
    private const double DotSpacing = 11;

    private readonly double[] _historyTime = new double[HistorySize];
    private readonly double[] _historyValue = new double[HistorySize];
    private int _historyCount;
    private int _historyHead;

    private double _span = 2;
    private double _airTime;
    private double _airStart;
    private double _conePhase;

    /// <summary>The cone's position now, DIPs forward of rest.</summary>
    private double _cone;

    /// <summary>The viewer's hand has the cone (or had it until <see cref="_handUntil"/>): the tone waits.</summary>
    private double _handCone;
    private double _handReleased = double.NegativeInfinity;
    private double _handUntil = double.NegativeInfinity;

    /// <summary>The dot followed in red: its column and row.</summary>
    private int _trackColumn = -1;
    private int _trackRow = -1;

    private WaveLayout _wave;

    /// <summary>Where the wave view's parts are and the scale it is drawn at.</summary>
    private readonly record struct WaveLayout(Rect Band, Rect Graph, double RulerY, Rect Speaker, Rect Ear, double MetresPerPixel,
        double LambdaPixels, double AmplitudePixels, double Slow, double Speed)
    {
        public double X0 => Band.Left;

        public double X1 => Band.Right;

        public int Columns => Math.Max(2, (int)(Band.Width / DotSpacing));

        public int Rows => Math.Max(3, (int)(Band.Height / DotSpacing));
    }

    /// <summary>Starts the air still again: the sound leaves the speaker afresh.</summary>
    private void ResetAir()
    {
        _historyCount = 0;
        _historyHead = 0;
        _airStart = _airTime;
        _conePhase = 0;
        _cone = 0;
        _handUntil = double.NegativeInfinity;
    }

    /// <summary>The wave view's layout for the current size and sound, choosing the zoom (and starting afresh when it changes).</summary>
    private void UpdateWave()
    {
        var stage = Stage();
        var bandHeight = Math.Clamp(stage.Height * 0.44, 120, 250);
        var graphHeight = Math.Clamp(stage.Height * 0.2, 56, 110);

        // The band and graph stop growing before a tall page runs out: centre them in the stage's height (the label
        // line above the band, the gap, and the distance scale under the graph included).
        var needed = 34 + bandHeight + 34 + graphHeight + 40;
        var top = stage.Top + Math.Max(0, (stage.Height - needed) / 2);
        var band = new Rect(stage.Left + 120, top + 34, Math.Max(200, stage.Width - 120 - 130), bandHeight);
        var graph = new Rect(band.Left, band.Bottom + 34, band.Width, graphHeight);
        var speaker = new Rect(stage.Left + 30, band.Top + (band.Height * 0.06), 82, band.Height * 0.88);
        var ear = new Rect(band.Right + 6, band.Top + (band.Height * 0.12), 110, band.Height * 0.76);

        var speed = SoundModel.Speed(Medium, Temperature);
        var shown = speed > 0 ? speed : SoundModel.SpeedInAir(Temperature);
        var lambda = SoundModel.Wavelength(Math.Max(1, Frequency), shown);
        var count = _span / lambda;
        if (count is < 1.3 or > 9)
        {
            var best = _span;
            var bestScore = double.MaxValue;
            foreach (var span in Spans)
            {
                var score = Math.Abs(Math.Log(span / lambda / 3.5));
                if (score < bestScore)
                {
                    bestScore = score;
                    best = span;
                }
            }

            if (best != _span)
            {
                _span = best;
                ResetAir();
            }
        }

        var metresPerPixel = _span / band.Width;
        var lambdaPixels = lambda / metresPerPixel;
        var strain = 0.12 + (0.38 * Math.Clamp(Loudness / 120, 0, 1));
        var amplitude = Math.Min(26, lambdaPixels / (2 * Math.PI) * strain);
        var slow = shown / metresPerPixel / AirPixelsPerSecond;
        _wave = new WaveLayout(band, graph, graph.Bottom + 16, speaker, ear, metresPerPixel, lambdaPixels, amplitude, slow, speed);
    }

    /// <summary>Moves the cone on by <paramref name="step"/> seconds (the tone, or where the hand holds it) and records it.</summary>
    private void StepAir(double step)
    {
        UpdateWave();
        if (step <= 0) return;
        _airTime += step;
        var fVisible = AirPixelsPerSecond / _wave.LambdaPixels;
        _conePhase = (_conePhase + (step * fVisible)) % 1;
        var tone = _wave.AmplitudePixels * Math.Sin(2 * Math.PI * _conePhase);
        if (_drag == DragKind.Cone)
        {
            _cone = _handCone;
        }
        else if (_airTime < _handUntil)
        {
            // Let go: the cone springs back to rest, then the tone fades back in once the pulse has crossed the picture.
            _cone = _handCone * Math.Exp(-(_airTime - _handReleased) / 0.12);
        }
        else
        {
            _cone = tone * Smooth((_airTime - _handUntil) / 0.8);
        }

        _historyTime[_historyHead] = _airTime;
        _historyValue[_historyHead] = _cone;
        _historyHead = (_historyHead + 1) % HistorySize;
        _historyCount = Math.Min(HistorySize, _historyCount + 1);
    }

    /// <summary>The viewer let go of the cone: the tone waits until the hand's pulse has crossed the picture.</summary>
    private void ReleaseCone()
    {
        _handReleased = _airTime;
        _handUntil = _airTime + (_wave.Band.Width / AirPixelsPerSecond) + 0.4;
    }

    /// <summary>How far the air <paramref name="x"/> DIPs from the speaker is pushed now: what the cone did x / speed ago.</summary>
    private double Displacement(double x)
    {
        if (Medium == SoundMedium.Vacuum || _historyCount == 0) return 0;
        var t = _airTime - (x / AirPixelsPerSecond);
        if (t <= _airStart) return 0;

        // Binary search over the ring, oldest (0) to newest (count − 1).
        var oldest = (_historyHead - _historyCount + HistorySize) % HistorySize;
        double TimeAt(int i) => _historyTime[(oldest + i) % HistorySize];
        double ValueAt(int i) => _historyValue[(oldest + i) % HistorySize];
        if (t <= TimeAt(0)) return ValueAt(0);
        var last = _historyCount - 1;
        if (t >= TimeAt(last)) return ValueAt(last);
        int lo = 0, hi = last;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;
            if (TimeAt(mid) <= t) lo = mid;
            else hi = mid;
        }

        var t0 = TimeAt(lo);
        var t1 = TimeAt(hi);
        var f = t1 > t0 ? (t - t0) / (t1 - t0) : 0;
        return ValueAt(lo) + ((ValueAt(hi) - ValueAt(lo)) * f);
    }

    /// <summary>The pressure at <paramref name="x"/>, −1…1 of a full tone's swing: the air is squeezed where it bunches.</summary>
    private double Pressure(double x)
    {
        var full = Math.Max(1e-6, _wave.AmplitudePixels * 2 * Math.PI / _wave.LambdaPixels);
        var dx = Math.Max(2, _wave.LambdaPixels / 40);
        return Math.Clamp(-(Displacement(x + dx) - Displacement(x - dx)) / (2 * dx) / full, -1.6, 1.6);
    }

    private void DrawWaveMode(PaintContext dc, double now)
    {
        UpdateWave();
        var w = _wave;
        var vacuum = Medium == SoundMedium.Vacuum;

        // The air itself: a faint band of the medium's colour.
        dc.DrawRoundedRectangle(FadedBrush(MediumTint(), 0.06), FadedPen(MediumTint(), 0.18, 1), Inflate(w.Band, 6, 6), 10, 10);

        var columns = w.Columns;
        var rows = w.Rows;
        var spacingX = w.Band.Width / columns;
        var spacingY = w.Band.Height / rows;
        var shift = new double[columns];
        var pressure = new double[columns];
        for (var c = 0; c < columns; c++)
        {
            var x = (c + 0.5) * spacingX;
            shift[c] = Displacement(x);
            pressure[c] = Pressure(x);
        }

        if (_trackColumn < 0 || _trackColumn >= columns || _trackRow >= rows)
        {
            _trackColumn = (int)(columns * 0.36);
            _trackRow = rows / 2;
        }

        if (!vacuum) DrawDots(dc, w, columns, rows, spacingX, spacingY, shift, pressure, now);
        DrawSpeaker(dc, w);
        DrawEar(dc, w, vacuum ? 0 : Displacement(w.Band.Width));
        DrawPressureGraph(dc, w, vacuum);
        DrawRuler(dc, w);

        if (vacuum)
        {
            DrawPill(dc, Loc("Sound_Label_NoAir"), new Point(w.Band.Left + (w.Band.Width / 2), w.Band.Top + (w.Band.Height / 2)), Ink.Red, 13);
        }
        else
        {
            DrawTrackedDot(dc, w, spacingX, spacingY, shift, now);
            DrawCompressionLabels(dc, w, spacingX, pressure);
        }

        // The medium and the slow-down, over the band's top edge.
        var mediumText = Medium switch
        {
            SoundMedium.Air => LocFormat("Sound_Label_MediumAir", Number(Temperature, "0"), Number(w.Speed, "0")),
            SoundMedium.Water => LocFormat("Sound_Label_MediumWater", Number(w.Speed, "0")),
            SoundMedium.Steel => LocFormat("Sound_Label_MediumSteel", Number(w.Speed, "0")),
            _ => Loc("Sound_Label_MediumVacuum"),
        };
        var mediumPill = DrawPill(dc, mediumText, new Point(w.Band.Left + 90, w.Band.Top - 20), MediumTint());
        mediumPill = new Rect(mediumPill.Left, mediumPill.Top, mediumPill.Width, mediumPill.Height);
        AddBoxTarget("medium", mediumPill);
        if (ShowLabels)
        {
            var slowText = LocFormat("Sound_Label_Slowed", Thousands(w.Slow));
            var formatted = _labels.Text(slowText, 11, Ink.Dim);

            // On a narrow page it would run into the medium's pill: then it goes on a line of its own above it.
            var left = w.Band.Right - formatted.Width;
            var y = left < mediumPill.Right + 10 ? w.Band.Top - 40 : w.Band.Top - 20;
            DrawText(dc, slowText, new Point(left, y), Ink.Dim, 11);
        }
    }

    private Color MediumTint() => Medium switch
    {
        SoundMedium.Water => Ink.Blue,
        SoundMedium.Steel => Ink.Steel,
        SoundMedium.Vacuum => Ink.Dim,
        _ => Ink.Cyan,
    };

    /// <summary>The dots of air, coloured by the pressure where they are: gold squeezed, blue stretched.</summary>
    private void DrawDots(PaintContext dc, WaveLayout w, int columns, int rows, double spacingX, double spacingY, double[] shift, double[] pressure, double now)
    {
        const int Buckets = 11;
        var figures = new StreamGeometry[Buckets];
        var contexts = new StreamGeometryContext[Buckets];
        for (var b = 0; b < Buckets; b++)
        {
            figures[b] = new StreamGeometry();
            contexts[b] = figures[b].Open();
        }

        var radius = Medium == SoundMedium.Steel ? 2.6 : 2.2;
        for (var c = 0; c < columns; c++)
        {
            var bucket = (int)Math.Round((Math.Clamp(pressure[c], -1, 1) + 1) / 2 * (Buckets - 1));
            var context = contexts[bucket];
            for (var r = 0; r < rows; r++)
            {
                if (c == _trackColumn && r == _trackRow) continue;
                var (jx, jy) = Jitter(c, r);

                // Every molecule also jiggles on its own (heat), a little, whatever the sound does.
                var wobble = Medium == SoundMedium.Steel ? 0.25 : 0.7;
                var x = w.X0 + ((c + 0.5) * spacingX) + shift[c] + (jx * spacingX * 0.32) + (wobble * Math.Sin((now * (3.1 + (2 * jy))) + (c * 1.7) + r));
                var y = w.Band.Top + ((r + 0.5) * spacingY) + (jy * spacingY * 0.32) + (wobble * Math.Cos((now * (2.7 + (2 * jx))) + (r * 2.3) + c));
                if (x < w.X0 - 2 || x > w.X1 + 4) continue;
                context.BeginFigure(new Point(x, y - radius), true);
                context.ArcTo(new Point(x, y + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                context.ArcTo(new Point(x, y - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                context.EndFigure(true);
            }
        }

        for (var b = 0; b < Buckets; b++)
        {
            contexts[b].Dispose();
            dc.DrawGeometry(DotBrush(b / (double)(Buckets - 1)), null, figures[b]);
        }
    }

    /// <summary>A dot's colour for a pressure from 0 (most stretched) to 1 (most squeezed), tinted by the medium.</summary>
    private IBrush DotBrush(double t)
    {
        var (low, mid, high) = Medium switch
        {
            SoundMedium.Water => (Color.FromRgb(0x2A, 0x5E, 0xB8), Color.FromRgb(0x7F, 0xB8, 0xFF), Color.FromRgb(0xD6, 0xF0, 0xFF)),
            SoundMedium.Steel => (Color.FromRgb(0x5A, 0x63, 0x72), Color.FromRgb(0xA9, 0xB2, 0xC0), Color.FromRgb(0xFF, 0xE6, 0xB0)),
            _ => (Color.FromRgb(0x4A, 0x7C, 0xD8), Color.FromRgb(0xB8, 0xC6, 0xD8), Ink.Gold),
        };
        var color = t < 0.5 ? Lerp(low, mid, t * 2) : Lerp(mid, high, (t - 0.5) * 2);
        return FadedBrush(color, 0.55 + (0.4 * Math.Abs(t - 0.5) * 2));
    }

    /// <summary>A dot's fixed offset from its grid place (−1…1 each way), so the air does not look like a lattice.</summary>
    private static (double X, double Y) Jitter(int c, int r)
    {
        var h = (uint)((c * 73856093) ^ (r * 19349663));
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return (((h & 0xFFFF) / 32768.0) - 1, (((h >> 16) & 0xFFFF) / 32768.0) - 1);
    }

    /// <summary>The followed dot: red, with a tick at its resting place and its swing either way.</summary>
    private void DrawTrackedDot(PaintContext dc, WaveLayout w, double spacingX, double spacingY, double[] shift, double now)
    {
        var (jx, jy) = Jitter(_trackColumn, _trackRow);
        var rest = new Point(w.X0 + ((_trackColumn + 0.5) * spacingX) + (jx * spacingX * 0.32), w.Band.Top + ((_trackRow + 0.5) * spacingY) + (jy * spacingY * 0.32));
        var at = new Point(rest.X + shift[_trackColumn], rest.Y);
        var tick = FadedPen(Ink.Red, 0.7, 1.2);
        dc.DrawLine(tick, new Point(rest.X, rest.Y - 11), new Point(rest.X, rest.Y + 11));
        if (ShowLabels && w.AmplitudePixels > 4)
        {
            var y = rest.Y - 15;
            DrawSpan(dc, new Point(rest.X - w.AmplitudePixels, y), new Point(rest.X + w.AmplitudePixels, y), Color.FromArgb(200, Ink.Red.R, Ink.Red.G, Ink.Red.B), 1.1);
        }

        dc.DrawEllipse(Glow(Ink.Red), null, at, 11, 11);
        dc.DrawEllipse(Solid(255, Ink.Red.R, Ink.Red.G, Ink.Red.B), FadedPen(Colors.White, 0.8, 1), at, 4.6, 4.6);
        _targets.Add("particle", at, 9);
    }

    private readonly Dictionary<Color, IBrush> _glows = [];

    private IBrush Glow(Color color)
    {
        if (_glows.TryGetValue(color, out var brush)) return brush;
        brush = SpaceDrawing.RadialGradient((WithAlpha(color, 0.45), 0), (WithAlpha(color, 0), 1));
        _glows[color] = brush;
        return brush;
    }

    /// <summary>"Compression" over the most squeezed column near the middle, "rarefaction" over the most stretched, and λ between two compressions.</summary>
    private void DrawCompressionLabels(PaintContext dc, WaveLayout w, double spacingX, double[] pressure)
    {
        var columns = pressure.Length;
        var middle = columns / 2;
        var reach = Math.Max(2, (int)Math.Ceiling(w.LambdaPixels / spacingX / 2));
        int high = -1, low = -1;
        for (var c = Math.Max(1, middle - reach); c <= Math.Min(columns - 2, middle + reach); c++)
        {
            if (high < 0 || pressure[c] > pressure[high]) high = c;
            if (low < 0 || pressure[c] < pressure[low]) low = c;
        }

        if (high < 0 || pressure[high] < 0.45) return;
        var xHigh = w.X0 + ((high + 0.5) * spacingX);
        var xLow = w.X0 + ((low + 0.5) * spacingX);
        var top = w.Band.Top + 14;
        if (ShowLabels || Highlight is "compression" or "rarefaction")
        {
            DrawTargetPill(dc, Loc("Sound_Label_Compression"), new Point(xHigh, top), Ink.Gold, "compression");
            if (pressure[low] < -0.45 && Math.Abs(xLow - xHigh) > 70) DrawTargetPill(dc, Loc("Sound_Label_Rarefaction"), new Point(xLow, top), Ink.Blue, "rarefaction");
        }
        else
        {
            _targets.Add("compression", new Point(xHigh, w.Band.Top + (w.Band.Height / 2)), 14);
            if (pressure[low] < -0.45) _targets.Add("rarefaction", new Point(xLow, w.Band.Top + (w.Band.Height / 2)), 14);
        }

        // One wavelength on from the compression, if the sound has got that far and it fits.
        var next = xHigh + w.LambdaPixels;
        if (next > w.X1 - 4)
        {
            next = xHigh;
            xHigh -= w.LambdaPixels;
        }

        var nextColumn = (int)((next - w.X0) / spacingX);
        if (xHigh < w.X0 + 4 || nextColumn < 0 || nextColumn >= columns || pressure[Math.Clamp(nextColumn, 0, columns - 1)] < 0.3) return;
        var y = w.Band.Bottom + 15;
        DrawSpan(dc, new Point(xHigh, y), new Point(next, y), Ink.Gold);
        var guide = FadedPen(Ink.Gold, 0.35, 1, dashed: true);
        dc.DrawLine(guide, new Point(xHigh, w.Band.Top + 2), new Point(xHigh, y + 4));
        dc.DrawLine(guide, new Point(next, w.Band.Top + 2), new Point(next, y + 4));
        var lambda = SoundModel.Wavelength(Frequency, Math.Max(1, w.Speed));
        DrawTargetPill(dc, LocFormat("Sound_Label_Wavelength", Metres(lambda)), new Point((xHigh + next) / 2, y), Ink.Gold, "wavelength");
    }

    /// <summary>The loudspeaker: a cabinet, a magnet, and the cone whose front moves with <see cref="_cone"/>.</summary>
    private void DrawSpeaker(PaintContext dc, WaveLayout w)
    {
        var box = w.Speaker;
        dc.DrawRoundedRectangle(SpaceDrawing.LinearGradient(Color.FromRgb(0x3A, 0x40, 0x4E), Color.FromRgb(0x1C, 0x20, 0x2A), 90),
            FadedPen(Colors.White, 0.25, 1), new Rect(box.Left, box.Top, box.Width - 18, box.Height), 8, 8);

        var cy = box.Top + (box.Height / 2);
        var magnet = new Rect(box.Left + 8, cy - (box.Height * 0.14), 22, box.Height * 0.28);
        dc.DrawRoundedRectangle(Solid(255, 0x6A, 0x72, 0x82), null, magnet, 3, 3);

        // The cone opens from the magnet to the front; the front (and the air touching it) moves with the cone.
        var push = Math.Clamp(_cone, -22, 22);
        var backX = magnet.Right + 2 + (push * 0.5);
        var frontX = box.Right - 14 + push;
        var cone = new StreamGeometry();
        using (var c = cone.Open())
        {
            c.BeginFigure(new Point(backX, cy - (box.Height * 0.12)), true);
            c.LineTo(new Point(frontX, cy - (box.Height * 0.44)));
            c.LineTo(new Point(frontX, cy + (box.Height * 0.44)));
            c.LineTo(new Point(backX, cy + (box.Height * 0.12)));

            c.EndFigure(true);
        }
        dc.DrawGeometry(SpaceDrawing.LinearGradient(Color.FromRgb(0x2A, 0x2E, 0x38), Color.FromRgb(0x5C, 0x64, 0x74), 0),
            FadedPen(Colors.White, 0.35, 1), cone);
        dc.DrawEllipse(Solid(255, 0x8A, 0x92, 0xA2), null, new Point(frontX - 6, cy), 6, box.Height * 0.1);
        dc.DrawLine(FadedPen(Ink.Text, 0.85, 3), new Point(frontX, cy - (box.Height * 0.44)), new Point(frontX, cy + (box.Height * 0.44)));
        AddBoxTarget("speaker", box);
        if (ShowLabels) DrawText(dc, Loc("Sound_Label_Speaker"), new Point(box.Left + ((box.Width - 18) / 2), box.Bottom + 14), Ink.Dim, 11.5, TextAnchor.Center);
    }

    /// <summary>The ear: the canal, the eardrum moved by the air that reaches it, the three small bones and the cochlea.</summary>
    private void DrawEar(PaintContext dc, WaveLayout w, double push)
    {
        var box = w.Ear;
        var cy = w.Band.Top + (w.Band.Height / 2);
        var skin = Color.FromRgb(0xE8, 0xB4, 0x96);

        // The outer ear, a curl round the canal's opening.
        var pinna = new StreamGeometry();
        using (var c = pinna.Open())
        {
            c.BeginFigure(new Point(box.Left + 6, cy - (box.Height * 0.5)), false);
            c.CubicBezierTo(new Point(box.Left - 30, cy - (box.Height * 0.42)), new Point(box.Left - 26, cy + (box.Height * 0.05)), new Point(box.Left - 4, cy + (box.Height * 0.22)));
            c.CubicBezierTo(new Point(box.Left + 6, cy + (box.Height * 0.32)), new Point(box.Left - 6, cy + (box.Height * 0.5)), new Point(box.Left + 14, cy + (box.Height * 0.5)));

            c.EndFigure(false);
        }
        dc.DrawGeometry(null, FadedPen(skin, 0.75, 5), pinna);

        var canalTop = cy - 16;
        var canalBottom = cy + 16;
        var drumX = box.Left + 40 + Math.Clamp(push * 0.45, -9, 9);
        var canalPen = FadedPen(skin, 0.6, 2);
        dc.DrawLine(canalPen, new Point(box.Left + 2, canalTop), new Point(box.Left + 40, canalTop));
        dc.DrawLine(canalPen, new Point(box.Left + 2, canalBottom), new Point(box.Left + 40, canalBottom));

        // The eardrum bulges in and out with the air at the end of the band.
        var drum = new StreamGeometry();
        using (var c = drum.Open())
        {
            c.BeginFigure(new Point(box.Left + 40, canalTop - 4), false);
            c.QuadraticBezierTo(new Point(drumX + ((drumX - (box.Left + 40)) * 1.2), cy), new Point(box.Left + 40, canalBottom + 4));

            c.EndFigure(false);
        }
        dc.DrawGeometry(null, FadedPen(Ink.Orange, 0.95, 2.6), drum);
        var bones = FadedBrush(Ink.Ivory, 0.75);
        dc.DrawEllipse(bones, null, new Point(drumX + 9, cy - 6), 4, 3);
        dc.DrawEllipse(bones, null, new Point(drumX + 17, cy - 2), 3.5, 3);
        dc.DrawEllipse(bones, null, new Point(drumX + 24, cy + 2), 3, 2.5);

        // The cochlea, a snail-shell spiral.
        var spiral = new List<Point>();
        var center = new Point(box.Left + 82, cy + 6);
        for (var a = 0.0; a < 5.5 * Math.PI; a += 0.15)
        {
            var r = 2 + (a * 1.25);
            spiral.Add(new Point(center.X + (r * Math.Cos(a)), center.Y + (r * Math.Sin(a))));
        }

        spiral.Reverse();
        dc.DrawGeometry(null, FadedPen(Ink.Violet, 0.75, 2.2), Line(spiral));
        AddBoxTarget("ear", new Rect(box.Left - 20, cy - 34, 128, 68));
        if (ShowLabels) DrawText(dc, Loc("Sound_Label_Eardrum"), new Point(box.Left + 40, canalBottom + 22), Ink.Orange, 11.5, TextAnchor.Center);
    }

    /// <summary>The pressure along the way: above the line squeezed, below stretched.</summary>
    private void DrawPressureGraph(PaintContext dc, WaveLayout w, bool vacuum)
    {
        var g = w.Graph;
        dc.DrawRoundedRectangle(Ink.Panel, Ink.Frame, Inflate(g, 4, 4), 8, 8);
        var mid = g.Top + (g.Height / 2);
        dc.DrawLine(Ink.Axis, new Point(g.Left, mid), new Point(g.Right, mid));

        var points = new List<Point>();
        for (var x = 0.0; x <= g.Width; x += 3)
        {
            var p = vacuum ? 0 : Pressure(x);
            points.Add(new Point(g.Left + x, mid - (Math.Clamp(p, -1.3, 1.3) * g.Height * 0.36)));
        }

        var area = new List<Point>(points) { new(g.Right, mid), new(g.Left, mid) };
        var fill = Line(area, closed: true, filled: true);
        dc.PushClip(new RectangleGeometry(new Rect(g.Left, g.Top, g.Width, g.Height / 2)));
        dc.DrawGeometry(FadedBrush(Ink.Gold, 0.3), null, fill);
        dc.Pop();
        dc.PushClip(new RectangleGeometry(new Rect(g.Left, mid, g.Width, g.Height / 2)));
        dc.DrawGeometry(FadedBrush(Ink.Blue, 0.3), null, fill);
        dc.Pop();
        dc.DrawGeometry(null, FadedPen(Ink.Text, 0.85, 1.6), Line(points));
        AddBoxTarget("pressure", g);

        DrawText(dc, Loc("Sound_Label_Pressure"), new Point(g.Left - 10, mid), Ink.Dim, 11, TextAnchor.Right);
        if (ShowLabels)
        {
            DrawText(dc, Loc("Sound_Label_Higher"), new Point(g.Left - 10, g.Top + 8), Ink.Gold, 10.5, TextAnchor.Right);
            DrawText(dc, Loc("Sound_Label_Lower"), new Point(g.Left - 10, g.Bottom - 8), Ink.Blue, 10.5, TextAnchor.Right);
        }
    }

    /// <summary>A ruler in metres under the graph, at the picture's scale.</summary>
    private void DrawRuler(PaintContext dc, WaveLayout w)
    {
        var y = w.RulerY;
        var pen = FadedPen(Ink.Dim, 0.7, 1);
        dc.DrawLine(pen, new Point(w.X0, y), new Point(w.X1, y));
        var step = NiceStep(_span / 5);
        for (var m = 0.0; m <= _span + 1e-9; m += step)
        {
            var x = w.X0 + (m / w.MetresPerPixel);
            dc.DrawLine(pen, new Point(x, y - 4), new Point(x, y + 4));
            DrawText(dc, m == 0 ? "0" : Metres(m), new Point(x, y + 13), Ink.Dim, 10.5, TextAnchor.Center);
        }
    }

    /// <summary>The 1–2–5 step nearest above <paramref name="rough"/>.</summary>
    private static double NiceStep(double rough)
    {
        var power = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var unit = rough / power;
        return (unit <= 1 ? 1 : unit <= 2 ? 2 : unit <= 5 ? 5 : 10) * power;
    }

    private static Rect Inflate(Rect rect, double x, double y)
    {
        rect = rect.Inflate(new Thickness(x, y));
        return rect;
    }

    private static string Thousands(double value) => value switch
    {
        >= 1e6 => (Math.Round(value / 1e4) * 1e4).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture),
        >= 1e3 => (Math.Round(value / 10) * 10).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture),
        _ => Math.Round(value).ToString("0", System.Globalization.CultureInfo.InvariantCulture),
    };
}
