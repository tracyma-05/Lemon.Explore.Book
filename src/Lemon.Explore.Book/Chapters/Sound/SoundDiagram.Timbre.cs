using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Sound;

/// <summary>
/// The timbre view: an oscilloscope with the note's waveform (and a second note's, and their sum), the overtones as bars
/// that can be dragged, the note's loudness over time, and a two-octave keyboard (C4–B5) to play. The waveform is the
/// real one, to scale in milliseconds, drawn with all harmonics starting in step.
/// </summary>
public sealed partial class SoundDiagram
{
    private const int LowestKey = 60;
    private const int HighestKey = 83;

    /// <summary>Seconds of the loudness strip.</summary>
    private const double EnvelopeSeconds = 3;

    private double _noteStart = double.NegativeInfinity;
    private TimbreLayout _timbre;

    /// <summary>The timbre view's parts.</summary>
    private readonly record struct TimbreLayout(Rect Scope, Rect Envelope, Rect Spectrum, Rect Bars, Rect Keyboard)
    {
        public double KeyWidth => Keyboard.Width / 14;
    }

    private void OnNotePlayed() => _noteStart = _clock.Elapsed.TotalSeconds;

    private void UpdateTimbre()
    {
        var stage = Stage();
        var keyHeight = Math.Clamp(stage.Height * 0.24, 80, 136);
        var keyWidth = Math.Min(stage.Width - 140, 14 * 58);
        var keyboard = new Rect(stage.Left + ((stage.Width - keyWidth) / 2), stage.Bottom - keyHeight - 4, keyWidth, keyHeight);
        var top = stage.Top + 8;
        var areaHeight = Math.Max(150, keyboard.Top - 50 - top);
        var width = stage.Width - 80;
        var scopeWidth = width * 0.6;
        var envelopeHeight = Math.Clamp(areaHeight * 0.22, 44, 64);
        var scope = new Rect(stage.Left + 40, top, scopeWidth, areaHeight - envelopeHeight - 14);
        var envelope = new Rect(scope.Left, scope.Bottom + 14, scopeWidth, envelopeHeight);
        var spectrum = new Rect(scope.Right + 24, top, width - scopeWidth - 24, areaHeight);
        var bars = new Rect(spectrum.Left + 18, spectrum.Top + 40, spectrum.Width - 36, spectrum.Height - 40 - 64);
        _timbre = new TimbreLayout(scope, envelope, spectrum, bars, keyboard);
    }

    private void DrawTimbreMode(PaintContext dc, double now)
    {
        UpdateTimbre();
        DrawScope(dc);
        DrawEnvelopeStrip(dc, now);
        DrawSpectrum(dc);
        DrawKeyboard(dc, now);
    }

    private double FirstFrequency => SoundModel.NoteFrequency(NoteMidi);

    private double SecondFrequency => SoundModel.SecondFrequency(FirstFrequency, SecondNote, Detune);

    /// <summary>The note (and the second one) at <paramref name="t"/> seconds, each scaled to ±1.</summary>
    private (double First, double Second) Signal(double t, double peak)
    {
        var harmonics = Harmonics;
        var first = 0.0;
        var second = 0.0;
        var f1 = FirstFrequency;
        var f2 = SecondFrequency;
        for (var n = 1; n <= harmonics.Length; n++)
        {
            var a = harmonics[n - 1];
            if (a == 0) continue;
            first += a * Math.Sin(2 * Math.PI * n * f1 * t);
            if (f2 > 0) second += a * Math.Sin(2 * Math.PI * n * f2 * t);
        }

        return (first / peak, second / peak);
    }

    /// <summary>The oscilloscope: the waveform over a few periods (or the beats' swell over a second).</summary>
    private void DrawScope(PaintContext dc)
    {
        var box = _timbre.Scope;
        DrawFrame(dc, box);
        AddBoxTarget("waveform", box);
        var plot = new Rect(box.Left + 14, box.Top + 44, box.Width - 28, box.Height - 44 - 30);
        var mid = plot.Top + (plot.Height / 2);
        for (var i = 0; i <= 4; i++)
        {
            var y = plot.Top + (plot.Height * i / 4);
            dc.DrawLine(i == 2 ? Ink.Axis : Ink.Grid, new Point(plot.Left, y), new Point(plot.Right, y));
        }

        var f1 = FirstFrequency;
        var peak = SoundModel.WaveformPeak(Harmonics);
        var beats = SecondNote == SecondNote.Beats;
        var window = beats ? 2.4 / Math.Max(0.2, Detune) : (SecondNote == SecondNote.Fifth ? 4 : 3) / f1;
        var scaleY = plot.Height * (SecondNote == SecondNote.None ? 0.46 : 0.23);

        if (beats)
        {
            // Too many swings to draw one by one: the band they fill, and its swell and fade.
            var top = new List<Point>();
            var bottom = new List<Point>();
            for (var i = 0; i <= 240; i++)
            {
                var t = window * i / 240;
                var swell = Math.Abs(Math.Cos(Math.PI * Detune * t));
                top.Add(new Point(plot.Left + (plot.Width * i / 240), mid - (swell * scaleY * 2)));
                bottom.Add(new Point(plot.Left + (plot.Width * i / 240), mid + (swell * scaleY * 2)));
            }

            var band = new List<Point>(top);
            bottom.Reverse();
            band.AddRange(bottom);
            dc.DrawGeometry(FadedBrush(Ink.Cyan, 0.35), null, Line(band, closed: true, filled: true));
            dc.DrawGeometry(null, FadedPen(Ink.Gold, 0.9, 1.6), Line(top));
            bottom.Reverse();
            dc.DrawGeometry(null, FadedPen(Ink.Gold, 0.9, 1.6), Line(bottom));

            var beatPeriod = 1 / Math.Max(0.2, Detune);
            var x0 = plot.Left;
            var x1 = plot.Left + (plot.Width * beatPeriod / window);
            DrawSpan(dc, new Point(x0, plot.Top - 12), new Point(x1, plot.Top - 12), Ink.Gold);
            DrawTargetPill(dc, LocFormat("Sound_Label_BeatPeriod", Number(beatPeriod, beatPeriod < 1 ? "0.00" : "0.0"), Hertz(Detune)), new Point((x0 + x1) / 2, plot.Top - 12), Ink.Gold, "beat", 11);
        }
        else
        {
            var first = new List<Point>();
            var second = new List<Point>();
            var sum = new List<Point>();
            const int Samples = 480;
            for (var i = 0; i <= Samples; i++)
            {
                var t = window * i / Samples;
                var (a, b) = Signal(t, peak);
                var x = plot.Left + (plot.Width * i / Samples);
                first.Add(new Point(x, mid - (a * scaleY)));
                second.Add(new Point(x, mid - (b * scaleY)));
                sum.Add(new Point(x, mid - ((a + b) * scaleY)));
            }

            if (SecondNote == SecondNote.None)
            {
                dc.DrawGeometry(null, FadedPen(Ink.Cyan, 0.2, 6), Line(first));
                dc.DrawGeometry(null, FadedPen(Ink.Cyan, 1, 2), Line(first));
            }
            else
            {
                dc.DrawGeometry(null, FadedPen(Ink.Cyan, 0.45, 1.2), Line(first));
                dc.DrawGeometry(null, FadedPen(Ink.Violet, 0.45, 1.2), Line(second));
                dc.DrawGeometry(null, FadedPen(Ink.Text, 0.95, 1.8), Line(sum));
            }

            // One period of the first note.
            var period = 1 / f1;
            var x1 = plot.Left + (plot.Width * period / window);
            dc.DrawLine(FadedPen(Ink.Gold, 0.35, 1, dashed: true), new Point(x1, plot.Top - 4), new Point(x1, plot.Bottom));
            DrawSpan(dc, new Point(plot.Left, plot.Top - 12), new Point(x1, plot.Top - 12), Ink.Gold);
            DrawTargetPill(dc, LocFormat("Sound_Label_Period", Number(period * 1000, period < 0.01 ? "0.00" : "0.0")), new Point((plot.Left + x1) / 2, plot.Top - 12), Ink.Gold, "period", 11);
        }

        // The time axis.
        var ms = window * 1000;
        DrawText(dc, "0", new Point(plot.Left, plot.Bottom + 12), Ink.Dim, 10.5, TextAnchor.Center);
        DrawText(dc, beats ? LocFormat("Sound_Value_S", Number(window, "0.0")) : LocFormat("Sound_Value_Ms", Number(ms, ms < 10 ? "0.0" : "0")),
            new Point(plot.Right, plot.Bottom + 12), Ink.Dim, 10.5, TextAnchor.Right);
        if (SecondNote is SecondNote.Octave or SecondNote.Fifth && ShowLabels)
        {
            DrawText(dc, LocFormat("Sound_Label_Sum", SoundModel.NoteName(NoteMidi), SoundModel.NoteName(NoteMidi + SoundModel.SecondSemitones(SecondNote))),
                new Point(plot.Left + (plot.Width / 2), plot.Bottom + 12), Ink.Dim, 10.5, TextAnchor.Center);
        }

        var title = LocFormat("Sound_Label_Scope", Loc(InstrumentKey(Instrument)), SoundModel.NoteName(NoteMidi), Hertz(f1));
        var formatted = _labels.Text(title, 12.5, Ink.Text);
        DrawTargetPill(dc, title, new Point(box.Left + 14 + (formatted.Width / 2) + 6, box.Top + 16), Ink.Text, "timbre", 12.5);
    }

    /// <summary>The resource key of an instrument's name.</summary>
    internal static string InstrumentKey(Instrument instrument) => instrument switch
    {
        Instrument.Flute => "Sound_Instrument_Flute",
        Instrument.Clarinet => "Sound_Instrument_Clarinet",
        Instrument.Violin => "Sound_Instrument_Violin",
        Instrument.Trumpet => "Sound_Instrument_Trumpet",
        Instrument.Piano => "Sound_Instrument_Piano",
        Instrument.Custom => "Sound_Instrument_Custom",
        _ => "Sound_Instrument_Fork",
    };

    /// <summary>The note's loudness over its first seconds: how it starts, holds and ends (with a marker while it plays).</summary>
    private void DrawEnvelopeStrip(PaintContext dc, double now)
    {
        var box = _timbre.Envelope;
        DrawFrame(dc, box);
        AddBoxTarget("envelope", box);
        var plot = new Rect(box.Left + 12, box.Top + 8, box.Width - 24, box.Height - 16);
        var envelope = SoundModel.Envelope(Instrument);
        var points = new List<Point>();
        for (var i = 0; i <= 200; i++)
        {
            var t = EnvelopeSeconds * i / 200;
            points.Add(new Point(plot.Left + (plot.Width * i / 200), plot.Bottom - (envelope.At(t) * plot.Height)));
        }

        var area = new List<Point>(points) { new(plot.Right, plot.Bottom), new(plot.Left, plot.Bottom) };
        dc.DrawGeometry(FadedBrush(Ink.Green, 0.18), null, Line(area, closed: true, filled: true));
        dc.DrawGeometry(null, FadedPen(Ink.Green, 0.9, 1.6), Line(points));

        var since = now - _noteStart;
        if (since >= 0 && since <= EnvelopeSeconds)
        {
            var x = plot.Left + (plot.Width * since / EnvelopeSeconds);
            dc.DrawLine(FadedPen(Ink.Text, 0.8, 1.4), new Point(x, plot.Top), new Point(x, plot.Bottom));
        }

        DrawText(dc, Loc("Sound_Label_Loudness"), new Point(plot.Right, plot.Top + 6), Ink.Dim, 10.5, TextAnchor.Right);
        DrawText(dc, LocFormat("Sound_Value_S", Number(EnvelopeSeconds, "0")), new Point(plot.Right, plot.Bottom - 6), Ink.Dim, 10, TextAnchor.Right);
    }

    /// <summary>The overtones as bars (the fundamental gold), and a second note's in violet where they fall.</summary>
    private void DrawSpectrum(PaintContext dc)
    {
        var box = _timbre.Spectrum;
        var bars = _timbre.Bars;
        DrawFrame(dc, box);
        DrawText(dc, Loc("Sound_Label_Overtones"), new Point(box.Left + 14, box.Top + 16), Ink.Text, 12.5, TextAnchor.Left, bold: true);
        DrawText(dc, Loc("Sound_Label_DragBars"), new Point(box.Right - 12, box.Top + 16), Ink.Dim, 10.5, TextAnchor.Right);
        dc.DrawLine(Ink.Axis, new Point(bars.Left, bars.Bottom), new Point(bars.Right, bars.Bottom));

        var harmonics = Harmonics;
        var count = harmonics.Length;
        var slot = bars.Width / count;
        var width = Math.Max(4, slot * 0.56);
        for (var n = 1; n <= count; n++)
        {
            var value = Math.Clamp(harmonics[n - 1], 0, 1);
            var cx = bars.Left + ((n - 0.5) * slot);
            var rect = new Rect(cx - (width / 2), bars.Bottom - (value * bars.Height), width, Math.Max(1, value * bars.Height));
            var color = n == 1 ? Ink.Gold : Ink.Cyan;
            dc.DrawRoundedRectangle(FadedBrush(color, 0.85), null, rect, 2, 2);
            DrawText(dc, n.ToString(CultureInfo.InvariantCulture), new Point(cx, bars.Bottom + 10), Ink.Dim, 10, TextAnchor.Center);
        }

        // The second note's harmonics, on the first note's scale: an octave's all land on the first's even ones.
        var ratio = SecondNote == SecondNote.None ? 0 : SecondFrequency / FirstFrequency;
        if (ratio > 0)
        {
            for (var n = 1; n <= count; n++)
            {
                var position = n * ratio;
                if (position > count + 0.4) break;
                var value = Math.Clamp(harmonics[n - 1], 0, 1);
                var cx = bars.Left + ((position - 0.5) * slot) + (SecondNote == SecondNote.Beats ? width * 0.7 : 0);
                dc.DrawRoundedRectangle(FadedBrush(Ink.Violet, 0.8), null, new Rect(cx - 1.5, bars.Bottom - (value * bars.Height), 3, Math.Max(1, value * bars.Height)), 1, 1);
            }
        }

        var labelY = bars.Bottom + 34;
        DrawTargetPill(dc, Loc("Sound_Label_Fundamental"), new Point(bars.Left + (slot * 0.5) + 30, labelY), Ink.Gold, "fundamental", 11);
        DrawTargetPill(dc, Loc("Sound_Label_OvertoneBars"), new Point(bars.Left + (slot * 7), labelY), Ink.Cyan, "overtone", 11);
        DrawText(dc, LocFormat("Sound_Label_BarHz", Hertz(FirstFrequency), Hertz(FirstFrequency * 2)), new Point(bars.Left + (bars.Width / 2), labelY + 20), Ink.Dim, 10, TextAnchor.Center);
    }

    /// <summary>Which bar (0-based) is under <paramref name="x"/>.</summary>
    private int BarAt(double x) => Math.Clamp((int)((x - _timbre.Bars.Left) / (_timbre.Bars.Width / Harmonics.Length)), 0, Harmonics.Length - 1);

    /// <summary>The bar height a point gives, 0–1.</summary>
    private double BarValue(double y) => Math.Clamp((_timbre.Bars.Bottom - y) / _timbre.Bars.Height, 0, 1);

    #region keyboard

    /// <summary>The white keys of the two octaves, left to right, as MIDI numbers.</summary>
    private static IEnumerable<int> WhiteKeys()
    {
        for (var m = LowestKey; m <= HighestKey; m++)
        {
            if (!SoundModel.IsBlack(m)) yield return m;
        }
    }

    /// <summary>Where a key is drawn.</summary>
    private Rect KeyRect(int midi)
    {
        var k = _timbre.Keyboard;
        var whiteIndex = 0;
        for (var m = LowestKey; m < midi; m++)
        {
            if (!SoundModel.IsBlack(m)) whiteIndex++;
        }

        if (!SoundModel.IsBlack(midi)) return new Rect(k.Left + (whiteIndex * _timbre.KeyWidth), k.Top, _timbre.KeyWidth, k.Height);
        var w = _timbre.KeyWidth * 0.6;
        return new Rect(k.Left + (whiteIndex * _timbre.KeyWidth) - (w / 2), k.Top, w, k.Height * 0.62);
    }

    /// <summary>The key under <paramref name="point"/>, black keys first, or null.</summary>
    private int? KeyAt(Point point)
    {
        if (!_timbre.Keyboard.Contains(point)) return null;
        for (var m = LowestKey; m <= HighestKey; m++)
        {
            if (SoundModel.IsBlack(m) && KeyRect(m).Contains(point)) return m;
        }

        foreach (var m in WhiteKeys())
        {
            if (KeyRect(m).Contains(point)) return m;
        }

        return null;
    }

    private void DrawKeyboard(PaintContext dc, double now)
    {
        var k = _timbre.Keyboard;
        var lit = now - _noteStart < 0.7;
        var second = SecondNote is SecondNote.Octave or SecondNote.Fifth ? NoteMidi + SoundModel.SecondSemitones(SecondNote) : -1;
        var edge = FadedPen(Colors.Black, 0.6, 1);

        foreach (var m in WhiteKeys())
        {
            var rect = KeyRect(m);
            var fill = lit && (m == NoteMidi || m == second)
                ? Solid(255, 0xFF, 0xE2, 0x96)
                : SpaceDrawing.LinearGradient(Color.FromRgb(0xFA, 0xF7, 0xEE), Color.FromRgb(0xDA, 0xD4, 0xC4), 90);
            dc.DrawRoundedRectangle(fill, edge, new Rect(rect.Left + 0.5, rect.Top, rect.Width - 1, rect.Height), 4, 4);
            if (m % 12 == 0) DrawText(dc, SoundModel.NoteName(m), new Point(rect.Left + (rect.Width / 2), rect.Bottom - 12), Color.FromRgb(0x50, 0x50, 0x58), 10.5, TextAnchor.Center);
        }

        for (var m = LowestKey; m <= HighestKey; m++)
        {
            if (!SoundModel.IsBlack(m)) continue;
            var rect = KeyRect(m);
            var fill = lit && (m == NoteMidi || m == second)
                ? Solid(255, 0xC8, 0x9A, 0x30)
                : SpaceDrawing.LinearGradient(Color.FromRgb(0x30, 0x32, 0x3A), Color.FromRgb(0x0C, 0x0D, 0x12), 90);
            dc.DrawRoundedRectangle(fill, edge, rect, 3, 3);
        }

        // The key played (gold) and the second note's (violet).
        var chosen = KeyRect(NoteMidi);
        var dotY = chosen.Bottom - (SoundModel.IsBlack(NoteMidi) ? 10 : 26);
        dc.DrawEllipse(Solid(255, Ink.Gold.R, Ink.Gold.G, Ink.Gold.B), null, new Point(chosen.Left + (chosen.Width / 2), dotY), 4.5, 4.5);
        if (second is >= LowestKey and <= HighestKey)
        {
            var other = KeyRect(second);
            dc.DrawEllipse(Solid(255, Ink.Violet.R, Ink.Violet.G, Ink.Violet.B), null,
                new Point(other.Left + (other.Width / 2), other.Bottom - (SoundModel.IsBlack(second) ? 10 : 26)), 4.5, 4.5);
        }

        // An octave: from one C to the next.
        var c4 = KeyRect(60);
        var c5 = KeyRect(72);
        var y = k.Top - 14;
        DrawSpan(dc, new Point(c4.Left + 2, y), new Point(c5.Left - 2, y), Ink.Green);
        DrawTargetPill(dc, LocFormat("Sound_Label_Octave", Hertz(SoundModel.NoteFrequency(60)), Hertz(SoundModel.NoteFrequency(72))), new Point((c4.Left + c5.Left) / 2, y), Ink.Green, "octave", 11);
        DrawTargetPill(dc, Loc("Sound_Label_Keyboard"), new Point((c5.Left + k.Right) / 2, y), Ink.Dim, "keyboard", 11);
    }

    #endregion
}
