using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.AcidsBases;

/// <summary>
/// The neutralising view: a burette of sodium hydroxide on a stand over a flask of 25 mL of acid with an indicator and a
/// thermometer, a magnifier on the particles in the flask, and the titration curve, drawn only as far as the base has
/// gone. Clicking the tap turns it (shut, a drop at a time, a stream); clicking the flask lets one drop in; dragging along
/// the curve sets how much base has gone in.
/// </summary>
public sealed partial class AcidBaseDiagram
{
    /// <summary>The flask's temperature before any base, °C.</summary>
    public const double RoomTemperature = 20;

    /// <summary>mL a second through the tap when dripping and when streaming.</summary>
    private const double DripRate = 0.4, StreamRate = 2;

    private const double DropFall = 0.32;

    private readonly ParticleBox _flaskBox = new();
    private readonly List<double> _falling = [];
    private double _sinceDrop;
    private readonly Dictionary<(TitrationAcid Acid, double Molar, Rect Plot), StreamGeometry> _curves = [];

    private readonly record struct NeutralizeLayout(Rect Burette, Rect Tap, Point Tip, Rect Flask, double FlaskNeck, Rect Plot, Point Lens, double LensRadius, Rect Thermometer);

    private NeutralizeLayout LayOutNeutralize()
    {
        var stage = Stage();
        var leftWidth = Math.Clamp(stage.Width * 0.4, 260, 440);
        var left = new Rect(stage.Left, stage.Top, leftWidth, stage.Height);

        var buretteX = left.Left + (left.Width * 0.36);
        var buretteTop = left.Top + 6;
        var flaskHeight = Math.Clamp(left.Height * 0.32, 90, 190);
        var flask = new Rect(buretteX - (flaskHeight * 0.55), left.Bottom - flaskHeight - 8, flaskHeight * 1.1, flaskHeight);
        var tipY = flask.Top - 26;
        var tap = new Rect(buretteX - 14, tipY - 34, 28, 14);
        var burette = new Rect(buretteX - 9, buretteTop, 18, tap.Top - buretteTop);
        var tip = new Point(buretteX, tipY);
        var lensRadius = Math.Clamp(left.Width * 0.2, 44, 84);
        var lens = new Point(Math.Min(left.Right - lensRadius - 4, flask.Right + lensRadius + 26), flask.Top - lensRadius + 10);
        var thermometer = new Rect(flask.Left + (flask.Width * 0.3), flask.Top - 48, 8, flask.Height * 0.9);

        var plot = new Rect(left.Right + 52, stage.Top + 16, Math.Max(60, stage.Right - left.Right - 70), Math.Max(60, stage.Height - 58));
        return new NeutralizeLayout(burette, tap, tip, flask, flask.Width * 0.32, plot, lens, lensRadius, thermometer);
    }

    private double TapRate => Tap switch
    {
        TapState.Drip => DripRate,
        TapState.Stream => StreamRate,
        _ => 0,
    };

    private void StepNeutralize(double dt)
    {
        if (Tap != TapState.Closed && _drag is DragKind.None or DragKind.Pending)
        {
            var next = BaseMl + (TapRate * dt);
            if (next >= AcidBaseModel.BuretteMl)
            {
                next = AcidBaseModel.BuretteMl;
                SetCurrentValue(TapProperty, TapState.Closed);
            }

            SetCurrentValue(BaseMlProperty, next);
            _sinceDrop += dt;
            if (Tap == TapState.Drip && _sinceDrop > 0.13)
            {
                _sinceDrop = 0;
                _falling.Add(0);
            }
        }

        for (var i = _falling.Count - 1; i >= 0; i--)
        {
            _falling[i] += dt;
            if (_falling[i] >= DropFall)
            {
                _falling.RemoveAt(i);
                _ripples.Add(0);
            }
        }

        for (var i = _ripples.Count - 1; i >= 0; i--)
        {
            _ripples[i] += dt;
            if (_ripples[i] > 0.7) _ripples.RemoveAt(i);
        }

        _flaskBox.SetTarget(AcidBaseModel.SpeciesOf(AcidBaseModel.Titration(Acid, BaseMl, Molar)));
        _flaskBox.Step(dt);
    }

    /// <summary>One drop let into the flask by hand.</summary>
    private void AddOneDrop()
    {
        SetCurrentValue(BaseMlProperty, Math.Min(AcidBaseModel.BuretteMl, Math.Round((BaseMl + AcidBaseModel.DropMl) * 100) / 100));
        _falling.Add(0);
    }

    private void DrawNeutralizeMode(PaintContext dc, double now)
    {
        var layout = LayOutNeutralize();
        var ph = AcidBaseModel.TitrationPh(Acid, BaseMl, Molar);

        DrawStand(dc, layout);
        DrawBurette(dc, layout, now);
        DrawFlask(dc, layout, ph, now);
        DrawThermometer(dc, layout, now);
        if (ShowParticles)
        {
            var spot = new Point(layout.Flask.Left + (layout.Flask.Width * 0.42), layout.Flask.Bottom - (layout.Flask.Height * 0.22));
            DrawMagnifier(dc, _flaskBox, layout.Lens, layout.LensRadius, spot, "reaction", now);
        }

        DrawCurve(dc, layout.Plot, ph, now);
    }

    private void DrawStand(PaintContext dc, NeutralizeLayout layout)
    {
        var baseRect = new Rect(layout.Flask.Left - 40, layout.Flask.Bottom + 2, layout.Flask.Width + 110, 8);
        dc.DrawRoundedRectangle(Ink.MeterBody, null, baseRect, 3, 3);
        var rodX = baseRect.Left + 14;
        dc.DrawRectangle(Ink.Metal, null, new Rect(rodX - 3, layout.Burette.Top - 4, 6, baseRect.Top - layout.Burette.Top + 4));
        var clampY = layout.Burette.Top + (layout.Burette.Height * 0.55);
        dc.DrawRectangle(Ink.Metal, null, new Rect(rodX, clampY - 3, layout.Burette.Left - rodX, 6));
        dc.DrawRoundedRectangle(Ink.MeterBody, null, new Rect(layout.Burette.Left - 4, clampY - 7, layout.Burette.Width + 8, 14), 3, 3);
    }

    private void DrawBurette(PaintContext dc, NeutralizeLayout layout, double now)
    {
        var b = layout.Burette;

        // Base left in the burette: the reading goes down from 0 at the top.
        double Y(double ml) => b.Top + 10 + (ml / AcidBaseModel.BuretteMl * (b.Height - 20));
        var level = Y(BaseMl);
        dc.DrawRectangle(FadedBrush(Color.FromRgb(0xC8, 0xD4, 0xF8), 0.42), null, new Rect(b.Left + 2, level, b.Width - 4, b.Bottom - level));
        dc.DrawLine(FadedPen(Colors.White, 0.7, 1.4), new Point(b.Left + 2, level), new Point(b.Right - 2, level));
        dc.DrawRectangle(Ink.GlassFill, Glows("burette") ? ExploreTargets.GlowPen(now, 3) : Ink.GlassPen, b);
        for (var ml = 0; ml <= AcidBaseModel.BuretteMl; ml += 5)
        {
            var y = Y(ml);
            var major = ml % 10 == 0;
            dc.DrawLine(Ink.GlassThin, new Point(b.Right - (major ? 9 : 5), y), new Point(b.Right, y));
            if (major && ShowLabels) DrawText(dc, Number(ml, "0"), new Point(b.Right + 5, y), Ink.Dim, 9.5);
        }

        DrawText(dc, Loc("Acid_Burette_Label"), new Point(b.Left - 8, b.Top + 26), Ink.Violet, 10.5, TextAnchor.Right);
        AddBoxTarget("burette", Inflated(b, 6, 0));

        // The tap: its handle across when shut, along when open.
        var tap = layout.Tap;
        var center = new Point(tap.Left + (tap.Width / 2), tap.Top + (tap.Height / 2));
        dc.DrawRectangle(Ink.GlassFill, Ink.GlassPen, new Rect(b.Left + 3, b.Bottom, b.Width - 6, tap.Top - b.Bottom));
        dc.DrawEllipse(Ink.MeterBody, Glows("tap") ? ExploreTargets.GlowPen(now, 3) : Ink.Frame, center, 8, 8);
        var handle = Tap switch
        {
            TapState.Closed => new Vector(18, 0),
            TapState.Drip => new Vector(13, -13),
            _ => new Vector(0, -18),
        };
        dc.DrawLine(FadedPen(Tap == TapState.Closed ? Ink.Red : Ink.Green, 1, 4), center - handle, center + handle);
        dc.DrawRectangle(Ink.GlassFill, Ink.GlassPen, new Rect(center.X - 2.5, tap.Bottom, 5, layout.Tip.Y - tap.Bottom));
        if (ShowLabels) DrawText(dc, Loc("Acid_Tap_" + Tap), new Point(center.X + 26, center.Y), Tap == TapState.Closed ? Ink.Dim : Ink.Green, 10.5);
        _targets.Add("tap", center, 16);

        // Drops falling, or a thin stream.
        var surface = layout.Flask.Top + (layout.Flask.Height * 0.45);
        if (Tap == TapState.Stream)
        {
            dc.DrawLine(FadedPen(Color.FromRgb(0xC8, 0xD4, 0xF8), 0.7, 2, flat: true), layout.Tip, new Point(layout.Tip.X, surface));
        }

        foreach (var age in _falling)
        {
            var t = age / DropFall;
            var y = layout.Tip.Y + ((surface - layout.Tip.Y) * t * t);
            DrawDrop(dc, new Point(layout.Tip.X, y), Color.FromRgb(0xC8, 0xD4, 0xF8), 3.2);
        }
    }

    private void DrawFlask(PaintContext dc, NeutralizeLayout layout, double ph, double now)
    {
        var f = layout.Flask;
        var neckHalf = layout.FlaskNeck / 2;
        var cx = f.Left + (f.Width / 2);
        var neckBottom = f.Top + (f.Height * 0.3);

        // Liquid: 25 mL fills a third; the base raises it.
        var share = Math.Clamp(0.36 + (BaseMl / 140), 0.36, 0.72);
        var surface = f.Bottom - (f.Height * 0.7 * share / 0.72 * 0.9);
        double HalfAt(double y) => neckHalf + ((f.Width / 2) - neckHalf - 4) * Math.Clamp((y - neckBottom) / (f.Bottom - 6 - neckBottom), 0, 1);
        var liquid = Line(
        [
            new Point(cx - HalfAt(surface), surface), new Point(cx + HalfAt(surface), surface),
            new Point(cx + (f.Width / 2) - 4, f.Bottom - 4), new Point(cx - (f.Width / 2) + 4, f.Bottom - 4),
        ], closed: true, filled: true);
        var color = AcidBaseModel.LiquidColor(FlaskIndicator, ph);
        dc.DrawGeometry(LiquidBrush(color), null, liquid);

        // Where a drop lands, the liquid briefly has more base than the rest, as if a millilitre more had gone in: near
        // the end, phenolphthalein flashes pink there before the stirring spreads it.
        var localPh = AcidBaseModel.TitrationPh(Acid, Math.Min(AcidBaseModel.BuretteMl, BaseMl + 1), Molar);
        foreach (var ripple in _ripples)
        {
            var local = AcidBaseModel.LiquidColor(FlaskIndicator, localPh);
            var r = 4 + (ripple * 26);
            dc.DrawEllipse(FadedBrush(Color.FromRgb(local.R, local.G, local.B), (local.A / 255.0) * 0.6 * (1 - (ripple / 0.7))), null, new Point(cx, surface + 6), r, r * 0.35);
        }

        // Swirl lines from the stirrer.
        var swirl = (_drift * 2.2) % (Math.PI * 2);
        for (var i = 0; i < 3; i++)
        {
            var y = surface + 14 + (i * (f.Bottom - surface - 20) / 3);
            var half = HalfAt(y) * 0.7;
            var phase = Math.Sin(swirl + (i * 1.3));
            dc.DrawLine(FadedPen(Colors.White, 0.12, 1.2), new Point(cx - (half * Math.Abs(phase)), y), new Point(cx + (half * Math.Abs(phase)), y + 3));
        }

        dc.DrawRoundedRectangle(Ink.MeterBody, null, new Rect(cx - 10, f.Bottom - 10, 20, 5), 2, 2);

        var glass = Line(
        [
            new Point(cx - neckHalf - 3, f.Top - 3), new Point(cx - neckHalf, f.Top), new Point(cx - neckHalf, neckBottom),
            new Point(cx - (f.Width / 2), f.Bottom - 2), new Point(cx + (f.Width / 2), f.Bottom - 2),
            new Point(cx + neckHalf, neckBottom), new Point(cx + neckHalf, f.Top), new Point(cx + neckHalf + 3, f.Top - 3),
        ]);
        dc.DrawGeometry(Ink.GlassFill, Glows("flask") ? ExploreTargets.GlowPen(now, 4) : Ink.GlassPen, glass);
        AddBoxTarget("flask", new Rect(f.Left, neckBottom, f.Width, f.Bottom - neckBottom));
        if (ShowLabels)
        {
            var acid = Loc(Acid == TitrationAcid.Strong ? "Acid_Flask_Strong" : "Acid_Flask_Weak");
            DrawText(dc, acid, new Point(cx, f.Bottom + 22), Acid == TitrationAcid.Strong ? Ink.Red : Ink.Orange, 10.5, TextAnchor.Center);
        }

        // The indicator's name on a pill: what colour to watch for.
        if (FlaskIndicator != Indicator.None)
        {
            DrawTargetPill(dc, Loc(IndicatorShortKey(FlaskIndicator)), new Point(f.Left - 10, f.Top + (f.Height * 0.55)), Ink.Text, "indicator", 10.5);
        }
    }

    private void DrawThermometer(PaintContext dc, NeutralizeLayout layout, double now)
    {
        var t = layout.Thermometer;
        var rise = AcidBaseModel.TemperatureRise(Acid, BaseMl, Molar);

        // The column: 20 °C to 30 °C over the stem, so even a rise under a degree shows.
        var bulb = new Point(t.Left + (t.Width / 2), t.Bottom - 5);
        var top = t.Top + 6;
        var share = Math.Clamp(0.25 + (rise / 10 * 0.7), 0, 1);
        var column = bulb.Y - ((bulb.Y - top) * share);
        dc.DrawRoundedRectangle(Ink.GlassFill, Glows("thermometer") ? ExploreTargets.GlowPen(now, 3) : Ink.GlassThin, t, 4, 4);
        dc.DrawRectangle(FadedBrush(Ink.Red, 0.95), null, new Rect(bulb.X - 1.6, column, 3.2, bulb.Y - column));
        dc.DrawEllipse(FadedBrush(Ink.Red, 1), null, bulb, 5, 5);
        var reading = LocFormat("Acid_Value_Celsius", Number(RoomTemperature + rise, "0.0"));
        DrawTargetPill(dc, reading, new Point(t.Left - 38, t.Top + 6), Ink.Orange, "thermometer", 10.5);
        AddBoxTarget("thermometer", t);
    }

    private void DrawCurve(PaintContext dc, Rect plot, double ph, double now)
    {
        Point P(double ml, double p) => new(plot.Left + (ml / AcidBaseModel.BuretteMl * plot.Width), plot.Bottom - (Math.Clamp(p, 0, 14) / 14 * plot.Height));
        dc.DrawRoundedRectangle(Ink.PlotBack, Ink.Frame, Inflated(plot, 8, 8), 10, 10);

        // The indicator's colour change as a band.
        if (AcidBaseModel.ChangeRange(FlaskIndicator) is { } range)
        {
            var band = new Rect(new Point(plot.Left, P(0, range.High).Y), new Point(plot.Right, P(0, range.Low).Y));
            var mid = AcidBaseModel.IndicatorColor(FlaskIndicator, range.High);
            dc.DrawRectangle(FadedBrush(Color.FromRgb(mid.R, mid.G, mid.B), 0.16), null, band);
            if (ShowLabels) DrawText(dc, LocFormat("Acid_Curve_Band", Loc(IndicatorShortKey(FlaskIndicator))), new Point(plot.Right - 6, band.Top - 9), Color.FromRgb(mid.R, mid.G, mid.B), 10, TextAnchor.Right);
            AddBoxTarget("band", band);
        }

        for (var p = 0; p <= 14; p += 2)
        {
            var y = P(0, p).Y;
            dc.DrawLine(Ink.Grid, new Point(plot.Left, y), new Point(plot.Right, y));

            // The top line is labelled with the axis' name instead.
            DrawText(dc, p == 14 ? "pH" : Number(p, "0"), new Point(plot.Left - 8, y), p == 7 ? Ink.Green : Ink.Dim, 10, TextAnchor.Right);
        }

        for (var ml = 0; ml <= 50; ml += 5)
        {
            var x = P(ml, 0).X;
            dc.DrawLine(Ink.Grid, new Point(x, plot.Top), new Point(x, plot.Bottom));
            if (ml % 10 == 0) DrawText(dc, Number(ml, "0"), new Point(x, plot.Bottom + 12), Ink.Dim, 10, TextAnchor.Center);
        }

        DrawText(dc, Loc("Acid_Curve_Axis"), new Point(plot.Right, plot.Bottom + 28), Ink.Dim, 10.5, TextAnchor.Right);
        DrawText(dc, Loc("Acid_Curve_Title"), new Point(plot.Left + 8, plot.Top + 10), Ink.Text, 12, bold: true);

        // Where acid and base match.
        var eq = P(AcidBaseModel.EquivalenceMl, 0).X;
        dc.DrawLine(Glows("equivalence") ? ExploreTargets.GlowPen(now, 3) : FadedPen(Ink.Gold, 0.55, 1.2, dashed: true), new Point(eq, plot.Top), new Point(eq, plot.Bottom));
        var eqLabel = DrawText(dc, Loc("Acid_Curve_Equivalence"), new Point(eq + 6, plot.Top + 30), Ink.Gold, 10.5);
        AddBoxTarget("equivalence", eqLabel.Union(new Rect(eq - 5, plot.Top, 10, plot.Height)));

        // The other acid, faint, for comparison.
        if (ShowCompare)
        {
            var other = Acid == TitrationAcid.Strong ? TitrationAcid.Weak : TitrationAcid.Strong;
            dc.DrawGeometry(null, FadedPen(other == TitrationAcid.Strong ? Ink.Red : Ink.Orange, 0.4, 1.6, dashed: true), CurveGeometry(other, plot, AcidBaseModel.BuretteMl));
        }

        // The curve, as far as the base has gone.
        var color = Acid == TitrationAcid.Strong ? Ink.Red : Ink.Orange;
        var pen = Glows("curve") ? ExploreTargets.GlowPen(now, 4) : FadedPen(color, 1, 2.6);
        if (BaseMl > 0.01)
        {
            dc.PushClip(new RectangleGeometry(new Rect(plot.Left - 4, plot.Top - 4, P(BaseMl, 0).X - plot.Left + 4, plot.Height + 8)));
            dc.DrawGeometry(null, pen, CurveGeometry(Acid, plot, AcidBaseModel.BuretteMl));
            dc.Pop();
        }

        AddBoxTarget("curve", plot);

        // The steep part, once it has been reached.
        if (BaseMl > AcidBaseModel.EquivalenceMl - 0.6)
        {
            var jump = new Rect(new Point(P(23.5, 0).X, P(0, 12.2).Y), new Point(P(26.5, 0).X, P(0, Acid == TitrationAcid.Strong ? 2.6 : 6.2).Y));
            dc.DrawRoundedRectangle(null, FadedPen(Ink.Gold, 0.35, 1, dashed: true), jump, 6, 6);
            AddBoxTarget("jump", jump);
        }

        // Halfway with a weak acid: pH = pKa.
        if (Acid == TitrationAcid.Weak && BaseMl >= (AcidBaseModel.EquivalenceMl / 2) - 0.2)
        {
            var half = P(AcidBaseModel.EquivalenceMl / 2, AcidBaseModel.TitrationPh(TitrationAcid.Weak, AcidBaseModel.EquivalenceMl / 2, Molar));
            dc.DrawEllipse(Brushes.Transparent, FadedPen(Ink.Cyan, 1, 2), half, 6, 6);
            var label = DrawText(dc, Loc("Acid_Curve_Half"), half + new Vector(10, 16), Ink.Cyan, 10.5);
            _targets.Add("halfway", half, 10);
            AddBoxTarget("halfway", label);
        }

        // Where we are.
        var at = P(BaseMl, ph);
        dc.DrawEllipse(Brushes.White, FadedPen(color, 1, 2), at, 5, 5);
        var pill = LocFormat("Acid_Curve_Point", Number(ph, "0.0"), Number(BaseMl, "0.0"));
        DrawPill(dc, pill, at + new Vector(at.X > plot.Right - 120 ? -70 : 70, at.Y < plot.Top + 40 ? 22 : -20), Ink.Text, 11);
    }

    /// <summary>The titration curve of <paramref name="acid"/> from 0 to <paramref name="toMl"/>, worked out once per plot.</summary>
    private StreamGeometry CurveGeometry(TitrationAcid acid, Rect plot, double toMl)
    {
        if (_curves.TryGetValue((acid, Molar, plot), out var geometry)) return geometry;
        var points = new List<Point>();
        for (var ml = 0.0; ml <= toMl + 1e-9; ml += 0.05)
        {
            var p = AcidBaseModel.TitrationPh(acid, ml, Molar);
            points.Add(new Point(plot.Left + (ml / AcidBaseModel.BuretteMl * plot.Width), plot.Bottom - (Math.Clamp(p, 0, 14) / 14 * plot.Height)));
        }

        geometry = Line(points);
        if (_curves.Count > 12) _curves.Clear();
        _curves[(acid, Molar, plot)] = geometry;
        return geometry;
    }

    /// <summary>The base (mL) under a point's x on the curve's plot.</summary>
    private double PlotMl(Point point)
    {
        var plot = LayOutNeutralize().Plot;
        return Math.Clamp((point.X - plot.Left) / plot.Width * AcidBaseModel.BuretteMl, 0, AcidBaseModel.BuretteMl);
    }
}
