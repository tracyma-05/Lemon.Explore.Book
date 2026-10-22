using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.BlueSky;

/// <summary>
/// Scattering mode: sunlight as a stream of coloured photons through a box standing for the air the Sun's light crosses
/// at this time of day (1 overhead, about 38 on the horizon). Each photon may be bounced aside by a molecule, with a
/// chance going as 1/λ⁴ from the real scattering coefficients, or by a speck of haze, the same for every colour. An eye
/// looking at the side of the box sees the light bounced out of the beam; an eye at the end sees what is left.
/// </summary>
public sealed partial class SkyDiagram
{
    /// <summary>The colours the photons come in, nm.</summary>
    private static readonly double[] PhotonWavelengths = [410, 450, 480, 520, 570, 600, 650];

    private static readonly Color[] PhotonColours = PhotonWavelengths.Select(SkyOptics.SpectralColour).ToArray();

    private const int MaxPhotons = 1200;
    private const double PhotonsPerSecond = 120;

    private readonly List<Photon> _photons = [];
    private double _spawnDebt;
    private (double X, double Y)[] _molecules = [];

    private struct Photon
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public int Band;
        public bool Scattered;
        public bool ByHaze;
        public double Age;
    }

    /// <summary>The box of air, the Sun beside it and the beam's height.</summary>
    private (Rect Box, Point Sun, double MidY) ScatterLayout()
    {
        var width = ActualWidth;
        var height = ActualHeight - _reserve.Current;
        var midY = Math.Round(height * 0.44);
        var half = Math.Min(height * 0.15, 120);
        var box = new Rect(170, midY - half, Math.Max(100, width - 340), half * 2);
        return (box, new Point(82, midY), midY);
    }

    /// <summary>How many times the air overhead the beam crosses: the air mass at the Sun's altitude (the horizon's once it is down).</summary>
    private double BeamAirMass => SkyOptics.AirMass(Altitude);

    /// <summary>The chance per crossing of the whole box that a photon is scattered by molecules and by haze.</summary>
    private (double Molecules, double Haze) Depths(double nm) =>
        (SkyOptics.Rayleigh(nm) * SkyOptics.RayleighScaleHeight * BeamAirMass,
            SkyOptics.Mie(nm, Haze) * SkyOptics.MieScaleHeight * BeamAirMass);

    private void StepPhotons(double dt)
    {
        if (dt <= 0 || ActualWidth < 400) return;
        var (box, sun, midY) = ScatterLayout();
        var speed = Math.Max(240, ActualWidth * 0.3);

        _spawnDebt += PhotonsPerSecond * dt;
        while (_spawnDebt >= 1 && _photons.Count < MaxPhotons)
        {
            _spawnDebt -= 1;
            _photons.Add(new Photon
            {
                X = sun.X + 30 + (_random.NextDouble() * speed * dt),
                Y = midY + ((_random.NextDouble() - 0.5) * 24),
                Vx = speed,
                Band = _random.Next(PhotonWavelengths.Length),
            });
        }

        _spawnDebt = Math.Min(_spawnDebt, 1);
        for (var i = _photons.Count - 1; i >= 0; i--)
        {
            var p = _photons[i];
            var x0 = p.X;
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            p.Age += dt;

            if (!p.Scattered && p.X > box.Left && x0 < box.Right)
            {
                var (molecules, haze) = Depths(PhotonWavelengths[p.Band]);
                var dx = (Math.Min(p.X, box.Right) - Math.Max(x0, box.Left)) / box.Width;
                if (_random.NextDouble() < 1 - Math.Exp(-(molecules + haze) * dx))
                {
                    p.Scattered = true;
                    p.ByHaze = _random.NextDouble() * (molecules + haze) < haze;
                    var angle = p.ByHaze ? HazeAngle() : MoleculeAngle();
                    p.Vx = speed * Math.Cos(angle);
                    p.Vy = speed * Math.Sin(angle);
                }
            }

            if (p.X < -20 || p.X > ActualWidth + 20 || p.Y < -20 || p.Y > ActualHeight + 20 || p.Age > 8) _photons.RemoveAt(i);
            else _photons[i] = p;
        }
    }

    /// <summary>A molecule scatters forward and back twice as much as sideways (1 + cos²θ).</summary>
    private double MoleculeAngle()
    {
        while (true)
        {
            var angle = _random.NextDouble() * 2 * Math.PI;
            if (_random.NextDouble() * 2 < 1 + (Math.Cos(angle) * Math.Cos(angle))) return angle;
        }
    }

    /// <summary>A speck of haze scatters mostly forward (Henyey–Greenstein).</summary>
    private double HazeAngle()
    {
        const double g = SkyOptics.MieAsymmetry;
        var u = _random.NextDouble();
        var s = (1 - (g * g)) / (1 - g + (2 * g * u));
        var cos = Math.Clamp((1 + (g * g) - (s * s)) / (2 * g), -1, 1);
        return Math.Acos(cos) * (_random.Next(2) == 0 ? 1 : -1);
    }

    /// <summary>The light left in the beam a share <paramref name="f"/> of the way through the box.</summary>
    private LinearRgb BeamLight(double f)
    {
        var bands = new double[SkyOptics.Bands];
        for (var b = 0; b < bands.Length; b++)
        {
            var nm = SkyOptics.Wavelengths[b];
            var (molecules, haze) = Depths(nm);
            bands[b] = Math.Exp(-(molecules + (haze / SkyOptics.MieAlbedo)) * f);
        }

        return SkyOptics.ToRgb(bands);
    }

    /// <summary>
    /// The light bounced out of the beam where the side eye looks, halfway through the box: what is left of the beam
    /// there, times how strongly each colour is scattered. Blue while the beam is still white; once the blue has gone
    /// from the beam, the air glows orange.
    /// </summary>
    private LinearRgb ScatteredLight()
    {
        var bands = new double[SkyOptics.Bands];
        for (var b = 0; b < bands.Length; b++)
        {
            var (molecules, haze) = Depths(SkyOptics.Wavelengths[b]);
            bands[b] = Math.Exp(-(molecules + (haze / SkyOptics.MieAlbedo)) / 2) * (molecules + haze);
        }

        return SkyOptics.ToRgb(bands);
    }

    private void DrawScattering(PaintContext dc, double now)
    {
        var width = ActualWidth;
        dc.DrawRectangle(Scatter.Background, null, new Rect(0, 0, width, ActualHeight));
        _stars.Draw(dc, width, ActualHeight, RenderScale(), now, 0.25);
        if (width < 400) return;
        var height = ActualHeight - _reserve.Current;

        var (box, sun, midY) = ScatterLayout();
        var end = width - 52;

        // The beam: white from the Sun, fading through the box to what is left at the end.
        var stops = new GradientStops { new(WithAlpha(Colors.White, 0.24), 0) };
        for (var i = 0; i <= 8; i++)
        {
            var f = i / 8.0;
            var x = box.Left + (f * box.Width);
            stops.Add(new GradientStop(WithAlpha(SunColour(BeamLight(f)), 0.24), (x - sun.X) / (end - sun.X)));
        }

        stops.Add(new GradientStop(WithAlpha(SunColour(BeamLight(1)), 0.24), 1));
        var beam = new LinearGradientBrush
        {
            GradientStops = stops,
            StartPoint = new RelativePoint(sun.X, 0, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(end, 0, RelativeUnit.Absolute),
        };
        if (Glows("beam")) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(now), new Rect(sun.X, midY - 18, end - sun.X - 24, 36), 18, 18);
        dc.DrawRectangle(beam, null, new Rect(sun.X, midY - 16, end - sun.X - 24, 32));

        // The box of air, its molecules jiggling, and specks of haze.
        dc.DrawRoundedRectangle(Scatter.BoxFill, Scatter.BoxEdge, box, 14, 14);
        _targets.AddBox("box", box);
        if (_molecules.Length == 0)
        {
            var random = new Random(3);
            _molecules = Enumerable.Range(0, 110).Select(_ => (random.NextDouble(), random.NextDouble())).ToArray();
        }

        for (var i = 0; i < _molecules.Length; i++)
        {
            var (mx, my) = _molecules[i];
            var p = new Point(box.Left + 8 + (mx * (box.Width - 16)) + (1.6 * Math.Sin((now * 5.3) + i)),
                box.Top + 8 + (my * (box.Height - 16)) + (1.6 * Math.Cos((now * 4.7) + (i * 1.9))));
            dc.DrawEllipse(Scatter.Molecule, null, p, 1.7, 1.7);
        }

        var specks = (int)Math.Round(Haze * 30);
        for (var i = 0; i < specks; i++)
        {
            var (mx, my) = _molecules[(i * 7) % _molecules.Length];
            var p = new Point(box.Left + 10 + (((mx + 0.37) % 1) * (box.Width - 20)) + (0.8 * Math.Sin((now * 2.1) + i)),
                box.Top + 10 + (((my + 0.61) % 1) * (box.Height - 20)));
            dc.DrawEllipse(Scatter.Speck, null, p, 3.6, 3.6);
        }

        // The photons, as short streaks.
        foreach (var p in _photons)
        {
            var speed = Math.Sqrt((p.Vx * p.Vx) + (p.Vy * p.Vy));
            var tail = new Vector(p.Vx / speed, p.Vy / speed) * 9;
            dc.DrawLine(Scatter.PhotonPens[p.Band, p.Scattered ? 1 : 0], new Point(p.X, p.Y) - tail, new Point(p.X, p.Y));
        }

        // The Sun.
        dc.DrawEllipse(Scatter.SunGlow, null, sun, 70, 70);
        dc.DrawEllipse(Scatter.SunDisc, null, sun, 30, 30);
        _labels.DrawCentered(dc, "Sky_Label_SunShort", new Point(sun.X, sun.Y + 38), 12, Ink.Gold);
        _targets.Add("sun", sun, 30);

        // The box's label: how much air.
        var boxLabel = Altitude > 0
            ? LocFormat("Sky_Scatter_Box", BeamAirMass.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
            : Loc("Sky_Scatter_Box_Down");
        _labels.DrawTextPill(dc, boxLabel, new Point(box.Left + 4, box.Top - 30), 12, Ink.Text);
        AddPillTarget("box", boxLabel, new Point(box.Left + 4, box.Top - 30), 12, Ink.Text);

        // An eye looking at the side of the box, and the colour it sees.
        var sideEye = new Point(box.Left + (box.Width / 2), Math.Min(box.Bottom + 100, height - 128));
        dc.DrawLine(Scatter.Sight, sideEye + new Vector(0, -16), new Point(sideEye.X, box.Top + 10));
        DrawEye(dc, sideEye, new Vector(0, -1), 18);
        var sideSwatch = sideEye + new Vector(48, 0);
        DrawSwatch(dc, sideSwatch, Lerp(SkyOptics.Hue(ScatteredLight()), Colors.White, 0.12));
        var sideText = _labels.Text(Loc("Sky_Scatter_SideEye"), 12.5, Ink.Text);
        _labels.DrawTextPill(dc, Loc("Sky_Scatter_SideEye"), new Point(sideSwatch.X + 30, sideSwatch.Y - (sideText.Height / 2) - 2), 12.5, Ink.Text);
        _targets.AddBox("sideeye", new Rect(new Point(sideEye.X - 22, sideSwatch.Y - 30), new Point(sideSwatch.X + 42 + sideText.Width, sideSwatch.Y + 30)));

        // An eye at the end of the beam, looking back at the Sun.
        var endEye = new Point(end + 20, midY);
        DrawEye(dc, endEye, new Vector(-1, 0), 18);
        var endSwatch = new Point(endEye.X - 4, midY - 66);
        DrawSwatch(dc, endSwatch, SunColour(BeamLight(1)));
        var endText = _labels.Text(Loc("Sky_Scatter_EndEye"), 12.5, Ink.Text);
        _labels.DrawTextPill(dc, Loc("Sky_Scatter_EndEye"), new Point(width - endText.Width - 26, endSwatch.Y - 54), 12.5, Ink.Text);
        AddPillTarget("endeye", Loc("Sky_Scatter_EndEye"), new Point(width - endText.Width - 26, endSwatch.Y - 54), 12.5, Ink.Text);
        _targets.Add("endeye", endSwatch, 30);
        _targets.Add("endeye", endEye, 20);
    }

    private static void DrawSwatch(PaintContext dc, Point centre, Color colour)
    {
        dc.DrawEllipse(new SolidColorBrush(WithAlpha(colour, 0.35)), null, centre, 30, 30);
        dc.DrawEllipse(new SolidColorBrush(colour), Scatter.SwatchEdge, centre, 20, 20);
    }

    /// <summary>An eye looking along <paramref name="direction"/>: an almond with an iris.</summary>
    private static void DrawEye(PaintContext dc, Point centre, Vector direction, double size)
    {
        direction = direction.Normalize();
        var across = new Vector(-direction.Y, direction.X);
        var a = centre - (across * size);
        var b = centre + (across * size);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(a, true);
            context.QuadraticBezierTo(centre + (direction * size * 1.1), b);
            context.QuadraticBezierTo(centre - (direction * size * 0.7), a);

            context.EndFigure(true);
        }
        dc.DrawGeometry(Scatter.EyeWhite, Scatter.EyeEdge, geometry);
        var iris = centre + (direction * size * 0.18);
        dc.DrawEllipse(Scatter.Iris, null, iris, size * 0.36, size * 0.36);
        dc.DrawEllipse(Brushes.Black, null, iris, size * 0.17, size * 0.17);
    }

    private static class Scatter
    {
        public static readonly IBrush Background = Solid(255, 4, 7, 13);
        public static readonly IBrush BoxFill = Solid(22, 120, 170, 255);
        public static readonly IPen BoxEdge = MakePen(Solid(90, 150, 195, 255), 1.2, dashed: true);
        public static readonly IBrush Molecule = Solid(150, 220, 230, 245);
        public static readonly IBrush Speck = Solid(170, 0xC8, 0xB8, 0x98);
        public static readonly IPen Sight = MakePen(Solid(110, 255, 255, 255), 1.2, dashed: true);
        public static readonly IBrush EyeWhite = Solid(235, 240, 244, 250);
        public static readonly IPen EyeEdge = MakePen(Solid(255, 30, 36, 48), 1.5);
        public static readonly IBrush Iris = Solid(255, 0x4A, 0x7A, 0xB5);
        public static readonly IPen SwatchEdge = MakePen(Solid(200, 255, 255, 255), 1.5);

        public static readonly IBrush SunDisc = SpaceDrawing.RadialGradient(
            (Color.FromRgb(255, 255, 250), 0),
            (Color.FromRgb(255, 240, 200), 1));

        public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(150, 255, 245, 210), 0.3),
            (Color.FromArgb(0, 255, 245, 210), 1));

        /// <summary>Per colour: in the beam, and scattered (brighter, so the bounced ones stand out).</summary>
        public static readonly IPen[,] PhotonPens = MakePhotonPens();

        private static IPen[,] MakePhotonPens()
        {
            var pens = new IPen[PhotonColours.Length, 2];
            for (var i = 0; i < PhotonColours.Length; i++)
            {
                pens[i, 0] = new ImmutablePen(((IBrush)SpaceDrawing.Solid(WithAlpha(PhotonColours[i], 0.75))).ToImmutable(), 2.4, null, PenLineCap.Round);
                pens[i, 1] = new ImmutablePen(((IBrush)SpaceDrawing.Solid(PhotonColours[i])).ToImmutable(), 2.8, null, PenLineCap.Round, PenLineJoin.Miter);
            }

            return pens;
        }
    }
}
