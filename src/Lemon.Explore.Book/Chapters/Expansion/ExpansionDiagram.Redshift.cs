using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Expansion;

/// <summary>
/// The redshift view: a strip of space from a distant galaxy (left) to the Milky Way (right), drawn at one fixed scale so
/// the strip visibly stretches. Light leaves the galaxy when it was nearer, crawls towards us while space grows under it,
/// and its waves stretch with the space they are in. Below, the galaxy's spectrum as it would arrive now beside the same
/// lines in a laboratory, with what eyes, Hubble and Webb can see.
/// </summary>
public sealed partial class ExpansionDiagram
{
    /// <summary>Seconds the light's journey takes to play, whatever its length.</summary>
    private const double JourneySeconds = 13;

    /// <summary>The spectrum's wavelength range on its log axis, nm.</summary>
    private const double SpectrumMin = 90;
    private const double SpectrumMax = 12_000;

    private double _arrivedAt = double.NegativeInfinity;

    private void OnRedshiftChanged()
    {
        _hold = 0;
        _arrivedAt = double.NegativeInfinity;
    }

    private void StepRedshift(double dt)
    {
        var journey = Journey;
        if (journey >= 1)
        {
            if (IsLessonActive) return;
            _hold += dt;
            if (_hold < 3) return;
            _hold = 0;
            SetCurrentValue(JourneyProperty, 0.0);
            return;
        }

        var next = Math.Min(1, journey + (dt / JourneySeconds));
        if (next >= 1) _arrivedAt = _clock.Elapsed.TotalSeconds;
        SetCurrentValue(JourneyProperty, next);
    }

    /// <summary>Where the light and the galaxy are: the times, scale factors and proper distances (Mpc) of this moment.</summary>
    internal readonly record struct LightTrip(double EmitScale, double EmitTime, double Time, double Scale, double SourceComoving, double PhotonComoving)
    {
        public static LightTrip At(double redshift, double journey)
        {
            var u = Expansion.Universe.Ours;
            var aE = 1 / (1 + Math.Max(0, redshift));
            var tE = u.TimeAt(aE);
            var t = tE + (Math.Clamp(journey, 0, 1) * (u.Age - tE));
            var a = Math.Clamp(u.ScaleAt(t), aE, 1);
            return new LightTrip(aE, tE, t, a, u.ComovingDistance(aE), u.ComovingDistance(a));
        }

        /// <summary>How much the light is stretched so far.</summary>
        public double Stretch => Scale / EmitScale;

        public double SourceNow => Scale * SourceComoving;

        public double SourceThen => EmitScale * SourceComoving;

        public double PhotonDistance => Scale * PhotonComoving;

        /// <summary>How far the light has come, Gly (a year of travel is a light-year).</summary>
        public double Travelled => Time - EmitTime;

        /// <summary>Whether the space under the light grows faster than the light crosses it, so it is carried away for now.</summary>
        public bool LosingGround => Expansion.Universe.Ours.Hubble(Scale) * PhotonDistance > Expansion.Universe.C;
    }

    private (Rect Strip, Rect Spectrum) RedshiftLayout(Rect stage)
    {
        var specHeight = Math.Min(176, Math.Max(150, stage.Height * 0.38));
        var spectrum = new Rect(stage.Left + 24, stage.Bottom - specHeight, Math.Max(100, stage.Width - 48), specHeight - 4);
        var stripTop = stage.Top + 52;
        var stripHeight = Math.Clamp(spectrum.Top - stripTop - 70, 90, 240);
        return (new Rect(stage.Left, stripTop, stage.Width, stripHeight), spectrum);
    }

    private void DrawRedshiftMode(PaintContext dc, double now)
    {
        var stage = Stage();
        var dpi = RenderScale();
        var (strip, spectrum) = RedshiftLayout(stage);
        var trip = LightTrip.At(Redshift, Journey);
        var homeX = strip.Right - 86;
        var leftX = strip.Left + 74;
        var k = (homeX - leftX) / Math.Max(1e-6, trip.SourceComoving);
        var yc = strip.Top + (strip.Height / 2);
        double X(double properMpc) => homeX - (properMpc * k);

        // The strip of space, ruled at equal comoving steps: the rulings spread apart as space stretches.
        dc.DrawRectangle(FadedBrush(Ink.Blue, 0.06), null, new Rect(0, strip.Top, homeX + 30, strip.Height));
        var gridPen = FadedPen(Ink.Cyan, Glows("space") ? 0.6 : 0.24, Glows("space") ? 1.8 : 1, flat: true);
        for (var j = 1; j <= 40; j++)
        {
            var x = X(trip.Scale * j * trip.SourceComoving / 10);
            if (x < 0) break;
            _batch.Add(gridPen, new Point(x, strip.Top + 6), new Point(x, strip.Bottom - 6));
        }

        _batch.Add(FadedPen(Ink.Cyan, 0.3, 1, flat: true), new Point(0, yc), new Point(homeX, yc));
        _batch.Draw(dc);
        var gridMark = X(trip.Scale * 3 * trip.SourceComoving / 10);
        if (gridMark > strip.Left + 10) _targets.AddBox("space", new Rect(gridMark - 6, strip.Top + 4, 12, strip.Height - 8));

        // Where the light set off: the galaxy was this close then.
        var startX = X(trip.SourceThen);
        dc.DrawLine(FadedPen(Ink.Dim, 0.6, 1.2, dashed: true), new Point(startX, strip.Top + 4), new Point(startX, strip.Bottom - 4));
        dc.DrawEllipse(null, FadedPen(Ink.Dim, 0.8, 1.2), new Point(startX, yc), 5, 5);

        // The galaxy, now further away.
        var sourceX = X(trip.SourceNow);
        var sourcePoint = new Point(sourceX, yc);
        if (SourceId == "Quasar") _sprites.DrawQuasar(dc, sourcePoint, 18, dpi);
        else _sprites.Draw(dc, SourceId == "Coma" || SourceId == "Virgo" ? 5 : 1, sourcePoint, 15, dpi);
        _targets.Add("source", sourcePoint, 16);

        // The Milky Way: us.
        var homePoint = new Point(homeX, yc);
        _sprites.DrawMilkyWay(dc, homePoint, 17, dpi);
        _targets.Add("us", homePoint, 18);
        var sinceArrival = now - _arrivedAt;
        if (Journey >= 1 && sinceArrival < 1.6)
        {
            var r = 18 + (sinceArrival * 40);
            dc.DrawEllipse(null, FadedPen(LightColour(SpectralLine.Wave.Nanometres * trip.Stretch), 1 - (sinceArrival / 1.6), 2.2), homePoint, r, r);
        }

        DrawWave(dc, trip, strip, sourceX, homeX, yc, k);

        if (ShowLabels) DrawTripLabels(dc, trip, strip, sourcePoint, startX, homePoint);
        DrawRedshiftClock(dc, stage, trip);
        DrawSpectrum(dc, spectrum, trip);
    }

    /// <summary>The light: a short train of waves at the light's place, each wave as stretched as the space it has crossed.</summary>
    private void DrawWave(PaintContext dc, LightTrip trip, Rect strip, double sourceX, double homeX, double yc, double k)
    {
        if (Journey >= 1) return;
        var front = homeX - (trip.PhotonDistance * k);
        var startLength = Math.Clamp(70 / (1 + Redshift), 8, 26);
        var wavelength = startLength * trip.Stretch;
        var length = wavelength * 3.5;
        var amplitude = Math.Min(strip.Height * 0.28, 16);
        var nm = SpectralLine.Wave.Nanometres * trip.Stretch;
        var color = LightColour(nm);
        var infrared = nm > 750;
        var points = new List<Point>();
        var tail = Math.Max(front - length, sourceX + 2);
        for (var x = front; x >= tail; x -= 1)
        {
            var behind = front - x;
            var envelope = Math.Sin(Math.PI * Math.Clamp(behind / length, 0, 1));
            points.Add(new Point(x, yc - (amplitude * envelope * Math.Sin(2 * Math.PI * behind / wavelength))));
        }

        if (points.Count < 2) return;
        var glow = Glows("light");
        dc.DrawGeometry(null, FadedPen(color, 0.25, glow ? 9 : 6), Line(points));
        dc.DrawGeometry(null, FadedPen(infrared ? Color.FromRgb(0xE0, 0x8A, 0x7A) : color, 1, 2.2, dashed: infrared), Line(points));
        dc.DrawEllipse(FadedBrush(Colors.White, 0.9), null, new Point(front, yc), 2.5, 2.5);
        _targets.Add("light", new Point(front - (Math.Min(length, front - tail) / 2), yc), Math.Max(12, Math.Min(length, front - tail) / 2));

        if (ShowLabels && trip.LosingGround)
        {
            var pill = DrawPill(dc, Loc("Expand_Label_Drift"), new Point(front - (length / 2), strip.Bottom + 12), Ink.Orange, 10.5);
            AddBoxTarget("drift", pill);
        }
    }

    /// <summary>The galaxy's name, its distance when the light left and now, and how far the light has come.</summary>
    private void DrawTripLabels(PaintContext dc, LightTrip trip, Rect strip, Point source, double startX, Point home)
    {
        var name = LightSource.Find(SourceId) is { } s ? Loc("Expand_Source_" + s.Id) : LocFormat("Expand_Source_Custom", Number(Redshift, "0.##"));
        DrawPill(dc, name, source + new Vector(0, -34), Ink.Text, 12);
        DrawPill(dc, Loc("Expand_Label_Us"), home + new Vector(0, -34), Ink.Gold, 12);

        var nowText = LocFormat("Expand_Label_NowAway", ExpansionFormat.Distance(trip.SourceNow));
        var nowPill = DrawPill(dc, nowText, new Point(Math.Max(source.X, strip.Left + 90), strip.Top - 14), Ink.Cyan, 11);
        AddBoxTarget("distnow", nowPill);

        if (Math.Abs(startX - source.X) > 30 || Journey < 0.02)
        {
            var thenText = LocFormat("Expand_Label_ThenAway", ExpansionFormat.Distance(trip.SourceThen));
            var thenPill = DrawPill(dc, thenText, new Point(startX, strip.Bottom + 12 + (trip.LosingGround ? 22 : 0)), Ink.Dim, 11);
            AddBoxTarget("distthen", thenPill);
        }

        var travelText = LocFormat("Expand_Label_Travelled", ExpansionFormat.DistanceGly(trip.Travelled), Number(trip.Stretch, trip.Stretch < 1.1 ? "0.000" : "0.0"));
        var travelPill = DrawPill(dc, travelText, new Point(home.X - 90, strip.Bottom + 34 + (trip.LosingGround ? 22 : 0)), Ink.Text, 11);
        AddBoxTarget("travel", travelPill);
    }

    /// <summary>The universe's age and size while the light travels, top right.</summary>
    private void DrawRedshiftClock(PaintContext dc, Rect stage, LightTrip trip)
    {
        var text = LocFormat("Expand_Redshift_Clock", ExpansionFormat.Time(trip.Time), ExpansionFormat.Size(trip.Scale));
        var size = PillSize(text, 12.5);
        DrawTargetPill(dc, text, new Point(stage.Right - (size.Width / 2) - 14, stage.Top + 4), Ink.Text, "rclock", 12.5);
    }

    private double SpectrumX(Rect axis, double nm) =>
        axis.Left + (Math.Log(Math.Clamp(nm, SpectrumMin, SpectrumMax) / SpectrumMin) / Math.Log(SpectrumMax / SpectrumMin) * axis.Width);

    /// <summary>The lines in the lab and as they arrive now, with the bands eyes and telescopes can see.</summary>
    private void DrawSpectrum(PaintContext dc, Rect frame, LightTrip trip)
    {
        DrawFrame(dc, frame);
        var axis = new Rect(frame.Left + 96, frame.Top, Math.Max(40, frame.Width - 112), frame.Height);
        var labTop = frame.Top + 38;
        const double stripHeight = 22;
        var arriveTop = labTop + stripHeight + 24;
        var labStrip = new Rect(axis.Left, labTop, axis.Width, stripHeight);
        var arriveStrip = new Rect(axis.Left, arriveTop, axis.Width, stripHeight);

        var visibleLeft = SpectrumX(axis, 380);
        var visibleRight = SpectrumX(axis, 750);
        foreach (var strip in new[] { labStrip, arriveStrip })
        {
            dc.DrawRectangle(FadedBrush(Ink.Ultraviolet, 0.25), null, new Rect(strip.Left, strip.Top, visibleLeft - strip.Left, strip.Height));
            dc.DrawRectangle(RainbowBrush, null, new Rect(visibleLeft, strip.Top, visibleRight - visibleLeft, strip.Height));
            dc.DrawRectangle(FadedBrush(Ink.Infrared, 0.3), null, new Rect(visibleRight, strip.Top, strip.Right - visibleRight, strip.Height));
            dc.DrawRectangle(null, Ink.Faint, strip);
        }

        // Region names over the lab strip.
        DrawText(dc, Loc("Expand_Spec_UV"), new Point((labStrip.Left + visibleLeft) / 2, frame.Top + 13), Ink.Violet, 10.5, TextAnchor.Center);
        var visibleBox = DrawText(dc, Loc("Expand_Spec_Visible"), new Point((visibleLeft + visibleRight) / 2, frame.Top + 13), Ink.Text, 10.5, TextAnchor.Center);
        AddBoxTarget("visible", visibleBox);
        var irBox = DrawText(dc, Loc("Expand_Spec_IR"), new Point((visibleRight + labStrip.Right) / 2, frame.Top + 13), Color.FromRgb(0xE0, 0x8A, 0x7A), 10.5, TextAnchor.Center);
        AddBoxTarget("infrared", irBox);

        var labName = DrawText(dc, Loc("Expand_Spec_Lab"), new Point(frame.Left + 14, labStrip.Top + (stripHeight / 2)), Ink.Dim, 11);
        AddBoxTarget("lab", labName.Union(labStrip));
        var arriveName = DrawText(dc, LocFormat("Expand_Spec_Arriving", Number(trip.Stretch, trip.Stretch < 1.1 ? "0.000" : "0.0")),
            new Point(frame.Left + 14, arriveStrip.Top + (stripHeight / 2)), Ink.Text, 11);
        AddBoxTarget("arriving", arriveName.Union(arriveStrip));

        var connector = FadedPen(Colors.White, 0.18, 1, flat: true);
        foreach (var line in SpectralLine.All)
        {
            var labX = SpectrumX(axis, line.Nanometres);
            var observed = line.Nanometres * trip.Stretch;
            var obsX = SpectrumX(axis, observed);
            var glow = Glows("line") && line == SpectralLine.Wave;
            dc.DrawLine(FadedPen(LineInk(line.Nanometres), 1, 2.2, flat: true), new Point(labX, labStrip.Top + 2), new Point(labX, labStrip.Bottom - 2));
            if (observed <= SpectrumMax)
            {
                _batch.Add(connector, new Point(labX, labStrip.Bottom), new Point(obsX, arriveStrip.Top));
                dc.DrawLine(FadedPen(LineInk(observed), 1, glow ? 3.4 : 2.2, flat: true), new Point(obsX, arriveStrip.Top + 2), new Point(obsX, arriveStrip.Bottom - 2));
            }

            if (ShowLabels && line.Id is "lya" or "hb" or "ha")
            {
                DrawText(dc, line.Name, new Point(labX, labStrip.Top - 8), Ink.Dim, 9, TextAnchor.Center);
                if (observed <= SpectrumMax) DrawText(dc, line.Name, new Point(obsX, arriveStrip.Bottom + 7), Ink.Text, 9, TextAnchor.Center);
            }
        }

        _batch.Draw(dc);
        var hb = SpectralLine.Wave.Nanometres * trip.Stretch;
        if (hb <= SpectrumMax) _targets.Add("line", new Point(SpectrumX(axis, hb), arriveStrip.Top + (stripHeight / 2)), 9);

        // Wavelength ticks.
        var tickY = arriveStrip.Bottom + 19;
        foreach (var (nm, label) in new[] { (100.0, "0.1"), (200.0, "0.2"), (500.0, "0.5"), (1000.0, "1"), (2000.0, "2"), (5000.0, "5"), (10_000.0, "10") })
        {
            var x = SpectrumX(axis, nm);
            dc.DrawLine(Ink.Faint, new Point(x, arriveStrip.Bottom), new Point(x, arriveStrip.Bottom + 4));
            DrawText(dc, label, new Point(x, tickY), Ink.Dim, 9.5, TextAnchor.Center);
        }

        DrawText(dc, Loc("Expand_Spec_Microns"), new Point(axis.Right, tickY + 12), Ink.Dim, 9.5, TextAnchor.Right);

        // What can see where.
        var barTop = tickY + 12;
        var bars = new[] { ("eye", "Expand_Spec_Eye", 380.0, 750.0, Ink.Text), ("hubble", "Expand_Spec_Hubble", 115.0, 1700.0, Ink.Cyan), ("jwst", "Expand_Spec_Jwst", 600.0, 28_000.0, Ink.Gold) };
        var row = 0;
        foreach (var (id, key, from, to, color) in bars)
        {
            var y = barTop + (row * 11);
            if (y > frame.Bottom - 5) break;
            var x0 = SpectrumX(axis, from);
            var x1 = SpectrumX(axis, to);
            dc.DrawRoundedRectangle(FadedBrush(color, Glows(id) ? 0.95 : 0.6), null, new Rect(x0, y - 2, Math.Max(2, x1 - x0), 4), 2, 2);
            var name = DrawText(dc, Loc(key), new Point(axis.Left - 8, y), color, 9.5, TextAnchor.Right);
            if (id == "jwst") AddBoxTarget("jwst", name.Union(new Rect(x0, y - 4, x1 - x0, 8)));
            row++;
        }
    }

    /// <summary>A spectral line's ink: its own colour if seen, a light violet or salmon outside the visible band.</summary>
    private static Color LineInk(double nm) => nm > 750 ? Color.FromRgb(0xF0, 0x9C, 0x8C) : nm < 380 ? Color.FromRgb(0xC4, 0xA8, 0xFF) : LightColour(nm);

    private static readonly IBrush RainbowBrush = MakeRainbow();

    private static IBrush MakeRainbow()
    {
        var stops = new List<(Color Color, double Offset)>();
        for (var i = 0; i <= 12; i++)
        {
            var nm = 380 + (370 * i / 12.0);

            // Stops spaced like the log axis, so colours sit under their wavelengths.
            var offset = Math.Log(nm / 380) / Math.Log(750 / 380.0);
            stops.Add((WithAlpha(LightColour(nm), 0.38), offset));
        }
        return SpaceDrawing.LinearGradient(0, [.. stops]);
    }

}
