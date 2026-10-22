using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Chapters.BlueSky;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Waves;

public sealed partial class WavesDiagram
{
    /// <summary>Picture units from the slits to the screen.</summary>
    public const double SlitScreen = 30;

    /// <summary>How fast the light's waves move in the picture, units per second.</summary>
    internal const double SlitWaveSpeed = 4.5;

    /// <summary>Room for the laser on the left, DIPs.</summary>
    private const double LaserWidth = 66;

    private (double Nanometres, bool White, uint[] Palette)? _lightPalette;
    private (double Nanometres, double Spacing, bool One, bool White, int Rows, double Scale, WriteableBitmap Bitmap)? _slitScreen;

    /// <summary>The light's wavelength in the picture: 1.1 units for green (532 nm), far bigger than real.</summary>
    internal static double PictureWavelength(double nanometres) => nanometres / 532 * 1.1;

    /// <summary>The slits' distance in the picture: 5 units for a quarter of a millimetre.</summary>
    internal static double PictureSpacing(double millimetres) => millimetres * 20;

    /// <summary>Where the picture is: (0, 0) halfway between the slits, x to the screen, y down.</summary>
    private readonly record struct SlitGeometry(Rect Field, double BarrierX, double ScreenX, Point Origin, double Scale)
    {
        public Point ToScreen(Point world) => new(Origin.X + (world.X * Scale), Origin.Y + (world.Y * Scale));

        public Point ToWorld(Point screen) => new((screen.X - Origin.X) / Scale, (screen.Y - Origin.Y) / Scale);

        /// <summary>Where the laser's waves start, picture units (negative: left of the slits).</summary>
        public double LaserX => (Field.Left + LaserWidth - BarrierX) / Scale;
    }

    private SlitGeometry SlitLayout()
    {
        var field = Stage();
        var barrier = field.Left + Math.Max(LaserWidth + 90, field.Width * 0.27);
        var screen = field.Right - 40;
        var scale = Math.Max(1, (screen - barrier) / SlitScreen);
        return new SlitGeometry(field, barrier, screen, new Point(barrier, field.Y + (field.Height / 2)), scale);
    }

    /// <summary>
    /// The laser's straight waves meeting the barrier, the two slits each sending out rings of light that overlap on their
    /// way to the screen, and on the screen the stripes the overlap makes. Not to scale: the light's waves are drawn huge.
    /// </summary>
    private void DrawSlits(PaintContext dc)
    {
        var layout = SlitLayout();
        var field = layout.Field;
        var nm = WhiteLight ? 550 : LightWavelength;
        var lambda = PictureWavelength(nm);
        var spacing = PictureSpacing(SlitSpacing);
        var laserX = layout.LaserX;
        var k = 2 * Math.PI / lambda;
        var reachSlits = _startLight + (-laserX / SlitWaveSpeed);
        var slitPhase = -k * -laserX;

        var emitters = new List<Emitter>(3)
        {
            new(EmitterKind.Plane, new Point(laserX, 0), 0, _startLight, MaxX: 0),
            new(EmitterKind.Point, new Point(0, -spacing / 2), slitPhase, reachSlits, MinX: 0),
        };
        if (!OneSlit) emitters.Add(new Emitter(EmitterKind.Point, new Point(0, spacing / 2), slitPhase, reachSlits, MinX: 0));

        var clip = new RectangleGeometry(field, 14, 14);
        dc.PushClip(clip);
        dc.DrawRectangle(Ink.SlitBackdrop, null, field);
        var area = new Rect(new Point(field.Left + LaserWidth, field.Top), new Point(layout.ScreenX, field.Bottom));
        _field.Render(area, layout.Origin, layout.Scale, lambda, SlitWaveSpeed, _time, emitters, LightPalette(nm, WhiteLight), 1.7);
        _field.Draw(dc);

        DrawLaser(dc, layout, nm);
        DrawBarrier(dc, layout, spacing);
        DrawSlitScreen(dc, layout, lambda, spacing, laserX);
        DrawSlitProbe(dc, layout, lambda, spacing);
        dc.Pop();

        dc.DrawRoundedRectangle(null, Ink.Frame, field, 14, 14);
    }

    /// <summary>A palette from dark (troughs and still light) to the light's colour at a crest.</summary>
    private uint[] LightPalette(double nanometres, bool white)
    {
        if (_lightPalette is { } cached && cached.Nanometres == nanometres && cached.White == white) return cached.Palette;

        var colour = white ? Color.FromRgb(0xF4, 0xF6, 0xFA) : SkyOptics.SpectralColour(nanometres);
        var palette = WaveField.Palette(256,
            (-1, Colors.Black),
            (0, Color.FromRgb(0x03, 0x04, 0x08)),
            (0.55, Lerp(Colors.Black, colour, 0.7)),
            (1, Lerp(colour, Colors.White, 0.25)));
        _lightPalette = (nanometres, white, palette);
        return palette;
    }

    private void DrawLaser(PaintContext dc, SlitGeometry layout, double nanometres)
    {
        var field = layout.Field;
        var box = new Rect(field.Left + 12, layout.Origin.Y - 22, LaserWidth - 18, 44);
        dc.DrawRoundedRectangle(Ink.LaserBody, Ink.Frame, box, 6, 6);
        var colour = WhiteLight ? Ink.Text : SkyOptics.SpectralColour(nanometres);
        dc.DrawEllipse(SpaceDrawing.Solid(colour), null, new Point(box.Right - 6, layout.Origin.Y), 3.5, 3.5);

        var name = _labels.Get(WhiteLight ? "Waves_Diagram_Lamp" : "Waves_Diagram_Laser", 11.5, Ink.Text, true);
        var nameAt = new Point(box.Left + ((box.Width - name.Width) / 2), box.Top - name.Height - 6);
        dc.DrawText(name, nameAt);
        var target = box.Union(new Rect(nameAt, new Size(name.Width, name.Height)));
        target = target.Inflate(new Thickness(2, 2));
        _targets.AddBox("laser", target);
        if (WhiteLight) return;

        var value = _labels.Text($"{Number(nanometres, "0")} nm", 11, colour);
        dc.DrawText(value, new Point(box.Left + ((box.Width - value.Width) / 2), box.Bottom + 6));
    }

    /// <summary>The barrier with its slits (the lower one taped over when covered), and the distance d between them.</summary>
    private void DrawBarrier(PaintContext dc, SlitGeometry layout, double spacing)
    {
        var field = layout.Field;
        var x = layout.BarrierX;
        var upper = layout.ToScreen(new Point(0, -spacing / 2)).Y;
        var lower = layout.ToScreen(new Point(0, spacing / 2)).Y;
        var gap = Math.Max(2.5, 0.22 * layout.Scale);

        dc.DrawRectangle(Ink.BarrierFill, null, new Rect(x - 3, field.Top, 6, upper - gap - field.Top));
        dc.DrawRectangle(Ink.BarrierFill, null, new Rect(x - 3, upper + gap, 6, Math.Max(0, lower - gap - upper - gap)));
        dc.DrawRectangle(Ink.BarrierFill, null, new Rect(x - 3, lower + gap, 6, Math.Max(0, field.Bottom - lower - gap)));
        if (OneSlit)
        {
            var tape = new Rect(x - 6, lower - gap - 5, 12, (2 * gap) + 10);
            dc.DrawRoundedRectangle(Ink.TapeFill, Ink.TapePen, tape, 2, 2);
        }

        // d, on the laser's side of the barrier.
        var bracketX = x - 14;
        dc.DrawLine(Ink.ScalePen, new Point(bracketX, upper), new Point(bracketX, lower));
        dc.DrawLine(Ink.ScalePen, new Point(bracketX - 4, upper), new Point(bracketX + 4, upper));
        dc.DrawLine(Ink.ScalePen, new Point(bracketX - 4, lower), new Point(bracketX + 4, lower));
        var d = _labels.Text("d", 12, Ink.Text, true);
        var dAt = new Point(bracketX - d.Width - 6, ((upper + lower) / 2) - (d.Height / 2));
        dc.DrawText(d, dAt);
        _targets.AddBox("spacing", new Rect(dAt.X - 4, Math.Min(upper, dAt.Y) - 2, bracketX + 6 - dAt.X, Math.Max(lower, dAt.Y + d.Height) - Math.Min(upper, dAt.Y) + 4));

        var name = _labels.Get(OneSlit ? "Waves_Diagram_OneSlit" : "Waves_Diagram_TwoSlits", 11.5, Ink.Text, true);
        var nameAt = new Point(x - (name.Width / 2) - 6, field.Top + 10);
        _labels.DrawTextPill(dc, Loc(OneSlit ? "Waves_Diagram_OneSlit" : "Waves_Diagram_TwoSlits"), nameAt, 11.5, Ink.Text);
        AddPillTarget("slits", name, nameAt);

        // The slits themselves, added last so the walkthrough's ring goes round them rather than the name.
        if (OneSlit)
        {
            _targets.AddBox("tape", new Rect(x - 8, lower - gap - 7, 16, (2 * gap) + 14));
            _targets.Add("slits", new Point(x, upper), 10);
        }
        else
        {
            _targets.AddBox("slits", new Rect(x - 9, upper - gap - 6, 18, lower - upper + (2 * gap) + 12));
        }
    }

    /// <summary>
    /// The screen: the light that reaches it, worked out from the picture's own slits and wavelength, fading in once the
    /// waves get there; the bright stripes numbered by how many wavelengths farther one slit is than the other.
    /// </summary>
    private void DrawSlitScreen(PaintContext dc, SlitGeometry layout, double lambda, double spacing, double laserX)
    {
        var field = layout.Field;
        var strip = new Rect(layout.ScreenX + 2, field.Top, 16, field.Height);
        var rows = Math.Max(1, (int)Math.Ceiling(field.Height));
        var white = WhiteLight;
        var nm = LightWavelength;
        if (_slitScreen is not { } cached || cached.Nanometres != nm || cached.Spacing != spacing || cached.One != OneSlit ||
            cached.White != white || cached.Rows != rows || cached.Scale != layout.Scale)
        {
            _slitScreen = (nm, spacing, OneSlit, white, rows, layout.Scale, ScreenBitmap(layout, rows, spacing));
        }

        dc.DrawRectangle(Ink.ScreenBack, null, strip);
        var arrive = _startLight + ((SlitScreen - laserX) / SlitWaveSpeed);
        var shown = Math.Clamp((_time - arrive) / 1.2, 0, 1);
        if (shown > 0)
        {
            dc.PushOpacity(shown);
            dc.DrawImage(_slitScreen.Value.Bitmap, strip);
            dc.Pop();
        }

        dc.DrawLine(Ink.ScalePen, new Point(strip.Left, field.Top), new Point(strip.Left, field.Bottom));
        var name = _labels.Get("Waves_Diagram_Screen", 11.5, Ink.Text, true);
        var nameAt = new Point(strip.Right - name.Width - 14, field.Top + 10);
        _labels.DrawTextPill(dc, Loc("Waves_Diagram_Screen"), nameAt, 11.5, Ink.Text);
        AddPillTarget("screen", name, nameAt);
        _targets.AddBox("screen", new Rect(strip.Left - 1, field.Top + 4, strip.Width + 2, field.Height - 8));

        if (OneSlit || shown < 0.5) return;

        // Bright stripe n: one slit n wavelengths farther than the other.
        for (var n = -12; n <= 12; n++)
        {
            if (Math.Abs(n) * lambda >= spacing) continue;
            var y = layout.ToScreen(new Point(SlitScreen, StripeHeight(n * lambda, spacing))).Y;
            if (y < field.Top + 34 || y > field.Bottom - 12) continue;
            var label = _labels.Text(Math.Abs(n).ToString(System.Globalization.CultureInfo.InvariantCulture), 10.5, Ink.Dim, true);
            var labelAt = new Point(strip.Left - label.Width - 5, y - (label.Height / 2));
            dc.DrawText(label, labelAt);
            AddTextTarget("order", label, labelAt);
        }
    }

    /// <summary>Where on the screen (picture units) one slit is <paramref name="delta"/> farther than the other.</summary>
    internal static double StripeHeight(double delta, double spacing)
    {
        double Difference(double y) =>
            Math.Sqrt((SlitScreen * SlitScreen) + Math.Pow(y + (spacing / 2), 2)) - Math.Sqrt((SlitScreen * SlitScreen) + Math.Pow(y - (spacing / 2), 2));

        // The difference grows with y from 0 towards the spacing: bisect.
        double low = 0, high = 1000;
        var target = Math.Abs(delta);
        for (var i = 0; i < 80; i++)
        {
            var mid = (low + high) / 2;
            if (Difference(mid) < target) low = mid;
            else high = mid;
        }

        return Math.Sign(delta) * (low + high) / 2;
    }

    /// <summary>The screen's light row by row, from the picture's two (or one) slits; white light adds up its colours.</summary>
    private WriteableBitmap ScreenBitmap(SlitGeometry layout, int rows, double spacing)
    {
        var bands = WhiteLight ? SkyOptics.Wavelengths : [LightWavelength];
        var colour = SkyOptics.SpectralColour(LightWavelength);
        var linear = new LinearRgb(Linear(colour.R), Linear(colour.G), Linear(colour.B));
        var pixels = new uint[rows];
        for (var row = 0; row < rows; row++)
        {
            var y = (layout.Field.Top + row + 0.5 - layout.Origin.Y) / layout.Scale;
            var light = default(LinearRgb);
            for (var band = 0; band < bands.Length; band++)
            {
                var intensity = PictureIntensity(y, PictureWavelength(bands[band]), spacing);
                light += (WhiteLight ? SkyOptics.BandColours[band] : linear) * intensity;
            }

            pixels[row] = WaveField.Pack(Color.FromRgb(SkyOptics.Encode(light.R), SkyOptics.Encode(light.G), SkyOptics.Encode(light.B)));
        }

        var bitmap = new WriteableBitmap(new PixelSize(1, rows), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        SpaceDrawing.WritePixels(bitmap, pixels, 1, rows);
        return bitmap;
    }

    /// <summary>Brightness on the picture's screen at height y: 1 in the middle with both slits.</summary>
    private double PictureIntensity(double y, double lambda, double spacing)
    {
        var r1 = Math.Sqrt((SlitScreen * SlitScreen) + Math.Pow(y + (spacing / 2), 2));
        var r2 = Math.Sqrt((SlitScreen * SlitScreen) + Math.Pow(y - (spacing / 2), 2));
        var a1 = WavePhysics.RippleAmplitude(r1, lambda);
        var middle = WavePhysics.RippleAmplitude(Math.Sqrt((SlitScreen * SlitScreen) + (spacing * spacing / 4)), lambda);
        if (OneSlit) return a1 * a1 / (4 * middle * middle);

        var a2 = WavePhysics.RippleAmplitude(r2, lambda);
        var k = 2 * Math.PI / lambda;
        return ((a1 * a1) + (a2 * a2) + (2 * a1 * a2 * Math.Cos(k * (r1 - r2)))) / (4 * middle * middle);
    }

    /// <summary>The probe: its way to each slit, and how much farther it is from one than the other.</summary>
    private void DrawSlitProbe(PaintContext dc, SlitGeometry layout, double lambda, double spacing)
    {
        var probe = SlitProbe;
        var at = layout.ToScreen(probe);
        var upper = layout.ToScreen(new Point(0, -spacing / 2));
        var lower = layout.ToScreen(new Point(0, spacing / 2));
        if (Glows("paths"))
        {
            var glow = ExploreTargets.GlowPen(_now, 5);
            dc.DrawLine(glow, upper, at);
            if (!OneSlit) dc.DrawLine(glow, lower, at);
        }

        dc.DrawLine(Ink.PathA, upper, at);
        if (!OneSlit) dc.DrawLine(Ink.PathB, lower, at);

        var difference = (((Vector)(probe - new Point(0, -spacing / 2))).Length - ((Vector)(probe - new Point(0, spacing / 2))).Length) / lambda;
        var meeting = WavePhysics.MeetingOf(2 * Math.PI * difference);
        DrawProbeMark(dc, at, OneSlit ? Ink.CyanBrush : meeting switch
        {
            Meeting.InStep => Ink.GoldBrush,
            Meeting.OutOfStep => Ink.WhiteBrush,
            _ => Ink.CyanBrush,
        });
        _targets.Add("probe", at, 12);
        if (OneSlit) return;

        var text = LocFormat("Waves_Diagram_Difference", Number(Math.Abs(difference), "0.00"));
        var formatted = _labels.Text(text, 12, Ink.Text);
        var left = at.X - formatted.Width - 30 > layout.BarrierX ? at.X - formatted.Width - 30 : at.X + 14;
        DrawPillInside(dc, text, new Point(left, at.Y - 30), layout.Field, 12, meeting == Meeting.InStep ? Ink.Gold : Ink.Text, "difference");
    }

    private static double Linear(byte channel) => Math.Pow(channel / 255.0, 2.2);
}
