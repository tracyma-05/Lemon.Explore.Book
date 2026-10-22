using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Rainbow;

public sealed partial class RainbowDiagram
{
    /// <summary>How many times wider white light's colours are spread when <see cref="Exaggerate"/> is on.</summary>
    internal const double SpreadBoost = 5;

    /// <summary>Width of a drawn beam with wave crests, pixels.</summary>
    private const double BeamWidth = 34;

    /// <summary>Pixels of drawn wavelength per nanometre in empty space (red's crests 33 px apart).</summary>
    private const double CrestScale = 0.05;

    /// <summary>Pixels per second the crests move in air.</summary>
    private const double CrestSpeed = 55;

    /// <summary>The prism's apex angle, degrees.</summary>
    internal const double PrismApex = 60;

    /// <summary>The wavelength white light's crests and angles are drawn for (the numbers on the page are given for it too).</summary>
    private const double WhiteCrestNm = RainbowOptics.YellowNm;

    /// <summary>
    /// The index the drawing uses: the real one, except that white light's colours are spread
    /// <see cref="SpreadBoost"/> times wider round yellow when <see cref="Exaggerate"/> is on.
    /// </summary>
    private double DrawnIndex(double nm)
    {
        var n = RainbowOptics.Index(Medium, nm);
        if (!Exaggerate || Colour != LightColour.White) return n;
        var yellow = RainbowOptics.Index(Medium, RainbowOptics.YellowNm);
        return yellow + (SpreadBoost * (n - yellow));
    }

    /// <summary>Where the beam meets the surface, and how long the drawn rays are.</summary>
    private static (Point Hit, double Length) SurfaceLayout(Rect stage) =>
        (new Point(stage.Left + (stage.Width * 0.5), stage.Top + (stage.Height * 0.5)), Math.Min(stage.Width * 0.45, stage.Height * 0.47));

    /// <summary>The prism (apex up), and where the beam meets its left face.</summary>
    private static (Point[] Triangle, Point Entry, double Length) PrismLayout(Rect stage)
    {
        var side = Math.Min(stage.Width * 0.32, stage.Height * 0.82 / 0.866);
        var height = side * 0.866;
        var centre = new Point(stage.Left + (stage.Width * 0.44), stage.Top + (stage.Height * 0.58));
        var apex = new Point(centre.X, centre.Y - (height * 2 / 3));
        var left = new Point(centre.X - (side / 2), centre.Y + (height / 3));
        var right = new Point(centre.X + (side / 2), centre.Y + (height / 3));
        var entry = left + ((apex - left) * 0.55);
        return ([apex, right, left], entry, Math.Min(stage.Width * 0.3, 280));
    }

    /// <summary>The left face's normal, pointing out of the prism (up and to the left).</summary>
    private static readonly Vector PrismOutward = new(-Math.Cos(30 * Deg), -Math.Sin(30 * Deg));

    /// <summary>The beam's angle from the normal for the lamp dragged to <paramref name="point"/>.</summary>
    private double AimAngle(Rect stage, Point point)
    {
        if (Scene == RefractionScene.Prism)
        {
            var (_, entry, _) = PrismLayout(stage);
            return Math.Clamp(-SpaceDrawing.AngleBetween(PrismOutward, point - entry), 0, 89);
        }

        var (hit, _) = SurfaceLayout(stage);
        var v = point - hit;
        return Math.Clamp(Math.Atan2(Math.Abs(v.X), Math.Max(1e-6, Math.Abs(v.Y))) / Deg, 0, 89);
    }

    private void DrawRefraction(PaintContext dc, Rect stage, double width, double height)
    {
        if (Scene == RefractionScene.Prism)
        {
            DrawPrism(dc, stage, width, height);
            return;
        }

        var (hit, length) = SurfaceLayout(stage);
        var surface = hit.Y;
        dc.DrawRectangle(Ink.AirBack, null, new Rect(0, 0, width, surface));
        dc.DrawRectangle(MediumBrush(Medium), null, new Rect(0, surface, width, Math.Max(0, height - surface)));
        dc.DrawLine(Ink.Surface, new Point(0, surface), new Point(width, surface));

        var white = Colour == LightColour.White;
        var nm = white ? WhiteCrestNm : RainbowOptics.Wavelength(Colour);
        var nMedium = DrawnIndex(nm);
        var (n1, n2) = FromInside ? (nMedium, 1.0) : (1.0, nMedium);
        var down = FromInside ? -1.0 : 1.0;
        var angle = Math.Clamp(Angle, 0, 89);

        var incoming = new Vector(Math.Sin(angle * Deg), down * Math.Cos(angle * Deg));
        var source = hit - (incoming * length);
        var reflected = new Vector(incoming.X, -incoming.Y);
        var refraction = RainbowOptics.Refraction(n1, n2, angle);
        var through = double.IsNaN(refraction) ? (Vector?)null : new Vector(Math.Sin(refraction * Deg), down * Math.Cos(refraction * Deg));
        var real = RainbowOptics.Index(Medium, nm);
        var share = FromInside ? RainbowOptics.Reflectance(real, 1, angle) : RainbowOptics.Reflectance(1, real, angle);
        var color = LightColor(white ? double.NaN : nm);

        // The two sides and the normal.
        DrawMediumLabels(dc, stage, surface, real);
        dc.DrawLine(Ink.Normal, new Point(hit.X, surface - (length * 0.95)), new Point(hit.X, surface + (length * 0.95)));
        var normalText = _labels.Text(Loc("Rain_Diagram_Normal"), 11, Ink.Dim);
        var normalAt = new Point(hit.X + 6, surface - (length * 0.95));
        dc.DrawText(normalText, normalAt);
        AddTextTarget("normal", normalText, normalAt);

        // Light coming from inside: the critical angle, past which none gets out.
        var critical = RainbowOptics.CriticalAngle(n1, n2);
        if (!double.IsNaN(critical))
        {
            var edge = new Vector(Math.Sin(critical * Deg), down * Math.Cos(critical * Deg));
            var from = hit - (edge * length * 0.9);
            dc.DrawLine(Ink.Guide, from, hit);
            DrawPill(dc, stage, LocFormat("Rain_Diagram_Critical", Number(critical, "0.0")), from - new Vector(0, 14 * down), Ink.Violet, 11, "critical");
        }

        if (ShowWaves)
        {
            DrawBeamsWithCrests(dc, hit, length, source, incoming, reflected, through, n1, n2, nm, color, share);
            _targets.Add("crests", through is { } inside ? hit + (inside * length * 0.5) : hit - (incoming * length * 0.5), 20);
        }
        else
        {
            DrawRay(dc, source, hit, color, 1);
            DrawRay(dc, hit, hit + (reflected * length), color, share);
            if (through is { } t && !white) DrawRay(dc, hit, hit + (t * length), color, 1 - share);
        }

        // White light: each colour bent its own amount.
        if (white && through is not null)
        {
            foreach (var fanNm in RainbowOptics.Fan)
            {
                var n = DrawnIndex(fanNm);
                var r = FromInside ? RainbowOptics.Refraction(n, 1, angle) : RainbowOptics.Refraction(1, n, angle);
                if (double.IsNaN(r)) continue;
                var direction = new Vector(Math.Sin(r * Deg), down * Math.Cos(r * Deg));
                DrawRay(dc, hit, hit + (direction * length), LightColor(fanNm), (1 - share) * 0.6, 1.8);
            }
        }

        if (!ShowWaves)
        {
            var incidentLeg = new Leg(source, hit, 1 / n1, 1);
            var legs = new List<Leg> { incidentLeg };
            if (through is { } t) legs.Add(new Leg(hit, hit + (t * length), 1 / n2, 1 - share));
            DrawPulses(dc, legs, color);
            DrawPulses(dc, [new Leg(hit, hit + (reflected * length), 1 / n1, share)], color,
                delay: ((Vector)(hit - source)).Length / (PulseSpeed / n1));
        }

        // The angles.
        var toSource = source - hit;
        DrawAngle(dc, hit, new Vector(0, -down), toSource, 58, $"θ₁ {Number(angle, "0.0")}°", Ink.Gold, "incidence");
        if (through is { } bent)
        {
            DrawAngle(dc, hit, new Vector(0, down), bent, 46, $"θ₂ {Number(refraction, "0.0")}°", Ink.Cyan, "refracted");
        }

        // How the light is shared out.
        var reflectedEnd = hit + (reflected * length * 0.82);
        DrawPill(dc, stage, LocFormat("Rain_Diagram_Reflected", Percent(share)), reflectedEnd + new Vector(0, -18 * down), Ink.Text, id: "reflected");
        if (through is { } go)
        {
            var end = hit + (go * length * 0.82);
            DrawPill(dc, stage, LocFormat("Rain_Diagram_Through", Percent(1 - share)), end + new Vector(46, 0), Ink.Text, id: "through");
        }
        else
        {
            DrawPill(dc, stage, Loc("Rain_Diagram_AllReflected"), hit + new Vector(0, 40 * down), Ink.Violet, 12.5, "total");
        }

        DrawLamp(dc, source, incoming, color);
        _targets.Add("lamp", source - (incoming * 23), 20);
    }

    /// <summary>The beams as bands with their wave crests: closer together where light is slower, joined at the surface.</summary>
    private void DrawBeamsWithCrests(PaintContext dc, Point hit, double length, Point source, Vector incoming, Vector reflected,
        Vector? through, double n1, double n2, double nm, Color color, double share)
    {
        var surface = hit.Y;
        var across = new Vector(-incoming.Y, incoming.X);
        var a = source + (across * BeamWidth / 2);
        var b = source - (across * BeamWidth / 2);
        var footA = a + (incoming * ((surface - a.Y) / incoming.Y));
        var footB = b + (incoming * ((surface - b.Y) / incoming.Y));

        var lambda = nm * CrestScale;
        var frequency = CrestSpeed / lambda;
        var cycles = _time * frequency;

        DrawBand(dc, [a, footA, footB, b], hit, incoming, lambda / n1, cycles, -length - BeamWidth, BeamWidth * 3, color, 1);
        DrawBand(dc, [footA, footA + (reflected * length), footB + (reflected * length), footB], hit, reflected, lambda / n1, cycles,
            -BeamWidth * 3, length, color, share);
        if (through is { } t)
        {
            DrawBand(dc, [footA, footA + (t * length), footB + (t * length), footB], hit, t, lambda / n2, cycles, -BeamWidth * 3,
                length, color, 1 - share);
        }
    }

    /// <summary>
    /// One beam: a band filled faintly, its crests where the phase is a whole number of waves (s = λ(m + f t) along the
    /// beam from where it meets the surface), and its centre line.
    /// </summary>
    private static void DrawBand(PaintContext dc, Point[] outline, Point hit, Vector direction, double lambda, double cycles,
        double from, double to, Color color, double power)
    {
        var band = Polygon(outline);
        var strength = Math.Clamp(Math.Sqrt(Math.Max(0, power)), 0, 1);
        dc.DrawGeometry(new SolidColorBrush(WithAlpha(color, 0.13 * strength)), null, band);
        dc.PushClip(band);
        var crest = MakePen(new SolidColorBrush(WithAlpha(color, (0.2 + (0.65 * strength)))), 2.2);
        var across = new Vector(-direction.Y, direction.X) * BeamWidth * 1.6;
        var first = (int)Math.Floor((from / lambda) - cycles);
        var last = (int)Math.Ceiling((to / lambda) - cycles);
        for (var m = first; m <= last; m++)
        {
            var centre = hit + (direction * (lambda * (m + cycles)));
            dc.DrawLine(crest, centre - across, centre + across);
        }

        dc.Pop();
        dc.DrawLine(MakePen(new SolidColorBrush(WithAlpha(color, 0.35 + (0.5 * strength))), 1.2), outline[0] + ((outline[^1] - outline[0]) / 2),
            outline[1] + ((outline[2] - outline[1]) / 2));
    }

    /// <summary>"Air" above and the material below, each with its index and light's speed there.</summary>
    private void DrawMediumLabels(PaintContext dc, Rect stage, double surface, double index)
    {
        var air = LocFormat("Rain_Diagram_Medium", Loc("Rain_Medium_Air"), "1.000", Speed(1));
        var below = LocFormat("Rain_Diagram_Medium", Loc($"Rain_Medium_{Medium}"), Number(index, "0.000"), Speed(index));
        _labels.DrawTextPill(dc, air, new Point(stage.Left + 4, surface - 30), 12, Ink.Text);
        _labels.DrawTextPill(dc, below, new Point(stage.Left + 4, surface + 10), 12, Ink.Cyan);
        AddPillTarget("index", _labels.Text(air, 12, Ink.Text), new Point(stage.Left + 4, surface - 30));
        AddPillTarget("index", _labels.Text(below, 12, Ink.Cyan), new Point(stage.Left + 4, surface + 10));
    }

    /// <summary>A small lamp (a laser box) with its beam leaving from <paramref name="mouth"/>.</summary>
    private static void DrawLamp(PaintContext dc, Point mouth, Vector direction, Color color)
    {
        var angle = Math.Atan2(direction.Y, direction.X) / Deg;
        dc.PushTransform(SpaceDrawing.Rotation(angle, mouth.X, mouth.Y));
        dc.DrawRoundedRectangle(Ink.LampBody, Ink.LampEdge, new Rect(mouth.X - 46, mouth.Y - 9, 46, 18), 4, 4);
        dc.DrawRectangle(new SolidColorBrush(WithAlpha(color, 0.9)), null, new Rect(mouth.X - 4, mouth.Y - 5, 4, 10));
        dc.DrawEllipse(Solid(200, 0xC9, 0xD1, 0xDC), null, new Point(mouth.X - 36, mouth.Y), 3, 3);
        dc.Pop();
    }

    private void DrawPrism(PaintContext dc, Rect stage, double width, double height)
    {
        dc.DrawRectangle(Ink.AirBack, null, new Rect(0, 0, width, height));
        var (triangle, entry, length) = PrismLayout(stage);
        var white = Colour == LightColour.White;
        var angle = Math.Clamp(Angle, 0, 89);
        var incoming = Rotate(-PrismOutward, -angle);
        var source = entry - (incoming * length);
        var screenX = stage.Right - 26;
        var color = LightColor(white ? double.NaN : RainbowOptics.Wavelength(Colour));

        // The screen the light falls on.
        var screen = new Rect(screenX, stage.Top + 8, 12, stage.Height - 16);
        dc.DrawRectangle(Ink.Screen, Ink.ScreenEdge, screen);

        // The prism.
        dc.DrawGeometry(MediumBrush(Medium, prism: true), PrismEdge, Outline(triangle));

        // The normal to the first face.
        dc.DrawLine(Ink.Normal, entry + (PrismOutward * 70), entry - (PrismOutward * 50));
        dc.DrawLine(MakePen(new SolidColorBrush(WithAlpha(color, 0.95)), 2.4), source, entry);

        // Each colour through the prism, onto the screen.
        RaySegment[]? middle = null;
        var hits = new List<(double Nm, double Y)>();
        foreach (var nm in white ? SpectrumSamples : [RainbowOptics.Wavelength(Colour)])
        {
            var segments = RainbowOptics.TracePolygon(triangle, source, incoming, DrawnIndex(nm), 4000);
            var drawn = white ? RainbowOptics.Fan.Any(f => Math.Abs(f - nm) < 1) : true;
            if (drawn || Math.Abs(nm - 585) < 1) DrawPrismRay(dc, segments, LightColor(nm), screenX);
            if ((white && Math.Abs(nm - 585) < 1) || !white) middle = segments.ToArray();
            var last = segments[^1];
            var dir = last.To - last.From;
            if (!last.Inside && dir.X > 1e-6)
            {
                var y = last.From.Y + (dir.Y * ((screenX - last.From.X) / dir.X));
                if (y > screen.Top && y < screen.Bottom) hits.Add((nm, y));
            }
        }

        // What reaches the screen: a spectrum, or one spot.
        for (var i = 0; i < hits.Count; i++)
        {
            var (nm, y) = hits[i];
            var next = i + 1 < hits.Count ? hits[i + 1].Y : y + 3;
            var top = Math.Min(y, next) - 1.5;
            var size = Math.Max(3, Math.Abs(next - y) + 3);
            dc.DrawRectangle(new SolidColorBrush(LightColor(nm)), null, new Rect(screen.Left + 1, top, screen.Width - 2, size));
        }

        if (hits.Count > 0)
        {
            // Above the light on the screen, clear of the rays coming to it.
            var text = white ? Loc("Rain_Diagram_Spectrum") : Loc("Rain_Diagram_Spot");
            var formatted = _labels.Text(text, 11.5, Ink.Text);
            var textAt = new Point(screen.Right - formatted.Width - 12, hits.Min(h => h.Y) - 44);
            _labels.DrawTextPill(dc, text, textAt, 11.5, Ink.Text);
            AddPillTarget("spectrum", formatted, textAt);
            var top = hits.Min(h => h.Y);
            _targets.AddBox("spectrum", new Rect(screen.Left - 4, top - 4, screen.Width + 8, Math.Max(10, hits.Max(h => h.Y) - top + 8)));
        }

        // The turn, measured where the beam in and the beam out meet.
        if (middle is { Length: >= 3 } path && !path[^1].Inside)
        {
            var exit = path[^1];
            Vector outgoing = exit.To - exit.From;
            outgoing = outgoing.Normalize();
            var meet = Meet(source, incoming, exit.From, outgoing);
            if (meet is { } x && ((Vector)(x - entry)).Length < length * 1.5)
            {
                dc.DrawLine(Ink.Guide, entry, x + (incoming * 90));
                dc.DrawLine(Ink.Guide, x, exit.From);
                var turn = Math.Abs(SpaceDrawing.AngleBetween(incoming, outgoing));
                var label = white
                    ? LocFormat("Rain_Diagram_TurnWhite", Number(turn, "0.0"))
                    : LocFormat("Rain_Diagram_Turn", Number(turn, "0.0"));
                DrawAngle(dc, x, incoming, outgoing, 52, label, Ink.Gold, "turn");
            }

            var legs = path.Select(s => new Leg(s.From, s.Inside ? s.To : ToScreen(s, screenX), s.Inside ? 1 / DrawnIndex(white ? 585 : RainbowOptics.Wavelength(Colour)) : 1, s.Power)).ToList();
            DrawPulses(dc, legs, white ? Colors.White : color);
        }

        DrawAngle(dc, entry, PrismOutward, source - entry, 50, $"{Number(angle, "0.0")}°", Ink.Cyan, "incidence");
        var name = LocFormat("Rain_Diagram_Prism", Loc($"Rain_Medium_{Medium}"), Number(PrismApex, "0"));
        DrawPill(dc, stage, name, new Point(triangle[0].X, triangle[2].Y + 22), Ink.Dim, id: "prism");
        DrawLamp(dc, source, incoming, color);
        _targets.Add("lamp", source - (incoming * 23), 20);
        var centroid = new Point((triangle[0].X + triangle[1].X + triangle[2].X) / 3, (triangle[0].Y + triangle[1].Y + triangle[2].Y) / 3);
        _targets.Add("prism", centroid, (triangle[1].X - triangle[2].X) * 0.22);
    }

    private static readonly IPen PrismEdge = MakePen(Solid(200, 0xC9, 0xE6, 0xFF), 1.4);

    /// <summary>A closed outline with every side stroked.</summary>
    private static StreamGeometry Outline(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], true);
            foreach (var point in points.Skip(1)) context.LineTo(point);

            context.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>Wavelengths the prism's spectrum is drawn from, red to violet.</summary>
    private static readonly double[] SpectrumSamples = Enumerable.Range(0, 46).Select(i => 685 - (i * 6.0)).Append(585).Concat(RainbowOptics.Fan)
        .Distinct().OrderByDescending(nm => nm).ToArray();

    /// <summary>One colour's path from where it enters the prism, the last part cut at the screen.</summary>
    private static void DrawPrismRay(PaintContext dc, IReadOnlyList<RaySegment> segments, Color color, double screenX)
    {
        foreach (var segment in segments.Skip(1))
        {
            var end = segment.Inside ? segment.To : ToScreen(segment, screenX);
            DrawRay(dc, segment.From, end, color, segment.Power * (segment.Inside ? 0.7 : 0.8), 1.8);
        }
    }

    /// <summary>Where a ray leaving the prism reaches the screen (or its own end).</summary>
    private static Point ToScreen(RaySegment segment, double screenX)
    {
        var dir = segment.To - segment.From;
        if (dir.X <= 1e-6) return segment.To;
        var t = Math.Min(1, (screenX - segment.From.X) / dir.X);
        return segment.From + (dir * Math.Max(0, t));
    }

    /// <summary>Where two lines meet, or null if they are (nearly) parallel.</summary>
    private static Point? Meet(Point a, Vector da, Point b, Vector db)
    {
        var cross = (da.X * db.Y) - (da.Y * db.X);
        if (Math.Abs(cross) < 1e-9) return null;
        var t = (((b - a).X * db.Y) - ((b - a).Y * db.X)) / cross;
        return a + (da * t);
    }

    private static IBrush MediumBrush(OpticalMedium medium, bool prism = false)
    {
        var (top, bottom) = medium switch
        {
            OpticalMedium.Glass => (Color.FromArgb(prism ? (byte)90 : (byte)255, 0x1C, 0x30, 0x34), Color.FromArgb(prism ? (byte)140 : (byte)255, 0x0C, 0x18, 0x1C)),
            OpticalMedium.Diamond => (Color.FromArgb(prism ? (byte)90 : (byte)255, 0x2A, 0x2E, 0x44), Color.FromArgb(prism ? (byte)140 : (byte)255, 0x12, 0x14, 0x22)),
            _ => (Color.FromArgb(prism ? (byte)90 : (byte)255, 0x10, 0x34, 0x58), Color.FromArgb(prism ? (byte)140 : (byte)255, 0x06, 0x14, 0x26)),
        };
        return Ink.Vertical(top, bottom);
    }

    /// <summary>"225 000 km/s".</summary>
    internal static string Speed(double index) => $"{(RainbowOptics.SpeedIn(index) / 1000).ToString("0", System.Globalization.CultureInfo.InvariantCulture)} 000 km/s";

    internal static string Percent(double share) => $"{Number(share * 100, share < 0.1 ? "0.0" : "0")}%";
}
