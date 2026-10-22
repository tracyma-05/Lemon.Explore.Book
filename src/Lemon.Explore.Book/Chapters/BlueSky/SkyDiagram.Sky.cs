using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.BlueSky;

/// <summary>
/// Sky mode: the sky as <see cref="SkyRenderer"/> works it out, drawn on a cylinder round the observer centred on the
/// Sun (or away from it), above a sea that reflects it, with distant hills to the south and north, a beach in front,
/// the Sun and its path through the day. Altitude and azimuth share one scale, so the sky is not stretched.
/// </summary>
public sealed partial class SkyDiagram
{
    /// <summary>The bitmap of the sky (and its reflection) is this many times coarser than the screen.</summary>
    private const int SkyPixel = 3;

    /// <summary>The bitmap is redrawn when the view turns by this much, degrees, and shifted in between.</summary>
    private const double SkyTurnStep = 0.5;

    /// <summary>Sky colours are a little more vivid than the plain sums, as the eye and a camera see them.</summary>
    private const double SkySaturation = 1.5;

    /// <summary>How big the Sun is drawn: about six times its real half-degree, to be seen.</summary>
    private const double SunRadiusDegrees = 1.5;

    /// <summary>Trees along the ridges, by azimuth.</summary>
    private static readonly double[] TreeAzimuths = [-38, -31, -12, -8, 9, 24, 29, 152, 158, 171, -166, -160];

    private readonly SkyRenderer _sky = new();
    private WriteableBitmap? _skyBitmap;
    private byte[] _skyPixels = [];
    private (double Altitude, double Haze, double Centre, double Width, double Height) _skyDrawn;

    /// <summary>Where the horizon is on screen: the sky above takes most of the height, the sea and beach the rest.</summary>
    private double SkyHorizonY() => Math.Round(ActualHeight * 0.7);

    /// <summary>Pixels per degree, the same across and up, with straight up just above the top edge.</summary>
    private double SkyScale() => SkyHorizonY() / 92;

    /// <summary>The azimuth (from south, west positive) in the middle of the view.</summary>
    private double SkyCentre() => SkyRenderer.Normalize(SkyOptics.SunAzimuth(_hour, Latitude) + (LookAway ? 180 : 0));

    private double SkyX(double azimuth) => (ActualWidth / 2) + (SkyRenderer.Normalize(azimuth - SkyCentre()) * SkyScale());

    private void DrawSky(PaintContext dc, double now)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        var horizon = SkyHorizonY();
        var scale = SkyScale();
        var altitude = Altitude;
        var sunAzimuth = SkyOptics.SunAzimuth(_hour, Latitude);
        _sky.Update(altitude, Haze);

        // The bitmap is made for the view turned to the nearest step, then shifted to the exact one.
        var centre = SkyCentre();
        var drawnCentre = Math.Round(centre / SkyTurnStep) * SkyTurnStep;
        var margin = Math.Ceiling(SkyTurnStep * scale) + SkyPixel;
        UpdateSkyBitmap(width, height, horizon, scale, sunAzimuth, drawnCentre, margin);
        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
        if (_skyBitmap is not null)
        {
            var shift = SkyRenderer.Normalize(drawnCentre - centre) * scale;
            SpaceDrawing.DrawSmooth(dc, _skyBitmap, new Rect(-margin + shift, 0, _skyBitmap.PixelSize.Width * SkyPixel, _skyBitmap.PixelSize.Height * SkyPixel));
        }

        // Stars come out as the sky darkens.
        var skyShown = 1 - Math.Exp(-_sky.MeanLuminance * _sky.Exposure);
        var starOpacity = Math.Clamp(1 - (skyShown * 9), 0, 1);
        if (starOpacity > 0.01)
        {
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, horizon)));
            _stars.Draw(dc, width, height, RenderScale(), now, starOpacity * 0.9);
            dc.Pop();
        }

        DrawSunPath(dc, horizon, scale, now);
        var sun = new Point(SkyX(sunAzimuth), horizon - (altitude * scale));
        var sunLight = SkyOptics.ToRgb(SkyOptics.Transmittance(Math.Max(altitude, 0.05), Haze));
        if (!LookAway) DrawSkySun(dc, sun, scale, sunLight, horizon, now);
        DrawLand(dc, width, height, horizon, scale);
        DrawCompass(dc, horizon, scale);
        DrawSkyLabels(dc, sun, horizon, scale, altitude);

        dc.DrawRectangle(Ink.TitleScrim, null, new Rect(0, 0, width, 200));
    }

    #region the sky and the sea

    private void UpdateSkyBitmap(double width, double height, double horizon, double scale, double sunAzimuth, double centre, double margin)
    {
        var key = (_sky.SunAltitude, _sky.Haze, centre, width, height);
        if (_skyBitmap is not null && key == _skyDrawn) return;
        _skyDrawn = key;

        var w = Math.Max(1, (int)Math.Ceiling((width + (2 * margin)) / SkyPixel));
        var h = Math.Max(1, (int)Math.Ceiling(height / SkyPixel));
        if (_skyBitmap is null || _skyBitmap.PixelSize.Width != w || _skyBitmap.PixelSize.Height != h)
        {
            _skyBitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            _skyPixels = new byte[w * h * 4];
        }

        var exposure = _sky.Exposure;
        var pixels = _skyPixels;
        var light = (_sky.SkyLight + _sky.SunLight).Luminance;
        var water = new LinearRgb(0.012, 0.03, 0.045) * light;
        Parallel.For(0, h, row =>
        {
            var y = (row + 0.5) * SkyPixel;
            var view = (horizon - y) / scale;
            for (var column = 0; column < w; column++)
            {
                var azimuth = centre + (((((column + 0.5) * SkyPixel) - margin) - (width / 2)) / scale);
                var fromSun = azimuth - sunAzimuth;
                LinearRgb radiance;
                if (view >= 0)
                {
                    radiance = _sky.Radiance(view, fromSun);
                }
                else
                {
                    // The sea mirrors the sky, more strongly towards the horizon, over its own dark blue-green.
                    var depth = -view;
                    var mirror = 0.03 + (0.32 * Math.Exp(-depth / 1.4));
                    radiance = (_sky.Radiance(depth, fromSun) * mirror) + water;
                }

                var colour = SkyOptics.Display(radiance, exposure, 255, view >= 0 ? SkySaturation : 1.1);
                var i = ((row * w) + column) * 4;
                pixels[i] = colour.B;
                pixels[i + 1] = colour.G;
                pixels[i + 2] = colour.R;
                pixels[i + 3] = 255;
            }
        });

        SpaceDrawing.WritePixels(_skyBitmap, pixels, w, h);
    }

    #endregion

    #region the Sun

    private void DrawSkySun(PaintContext dc, Point sun, double scale, LinearRgb light, double horizon, double now)
    {
        if (Altitude < -SunRadiusDegrees) return;
        var colour = SunColour(light);
        var radius = SunRadiusDegrees * scale;

        // Its glare, wider while it is bright.
        var glare = radius * (3 + (5 * Math.Clamp(light.Luminance, 0, 1)));
        var glow = SpaceDrawing.RadialGradient(
            (WithAlpha(colour, 0.55), 0),
            (WithAlpha(colour, 0.12), 0.35),
            (WithAlpha(colour, 0), 1));
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, horizon)));
        dc.DrawEllipse(glow, null, sun, glare, glare);
        dc.DrawEllipse(new SolidColorBrush(colour), null, sun, radius, radius);
        dc.Pop();
        _targets.Add("sun", sun, radius + 4);

        // Its path of light across the sea, shimmering.
        if (Altitude < -0.3) return;
        var length = (ActualHeight - horizon) * (0.4 + (0.6 * Math.Exp(-Altitude / 12)));
        for (var i = 0; i < 26; i++)
        {
            var f = (i + 0.5) / 26;
            var y = horizon + 2 + (f * length);
            var shimmer = 0.5 + (0.5 * Math.Sin((now * 3.1) + (i * 1.7)));
            var half = radius * (0.5 + (f * 2.2)) * (0.6 + (0.5 * shimmer));
            var alpha = (1 - f) * (0.25 + (0.45 * shimmer)) * Math.Clamp(0.4 + light.Luminance, 0, 1);
            dc.DrawRectangle(new SolidColorBrush(WithAlpha(colour, alpha)), null, new Rect(sun.X - half, y, half * 2, 1.6 + (f * 2)));
        }
    }

    /// <summary>The Sun's path through the day, with the hours along it.</summary>
    private void DrawSunPath(PaintContext dc, double horizon, double scale, double now)
    {
        if (LookAway) return;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            Point? previous = null;
            for (var hour = SkyOptics.FirstHour; hour <= SkyOptics.LastHour + 1e-9; hour += 0.05)
            {
                var point = new Point(SkyX(SkyOptics.SunAzimuth(hour, Latitude)), horizon - (SkyOptics.SunAltitude(hour, Latitude) * scale));

                // Start again where the path goes off one side and comes back on the other.
                if (previous is null || Math.Abs(point.X - previous.Value.X) > ActualWidth / 2) context.BeginFigure(point, false);
                else context.LineTo(point);
                previous = point;
            }

            context.EndFigure(false);
        }
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, horizon)));
        if (Glows("sunpath")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now), geometry);
        dc.DrawGeometry(null, Paint.SunPath, geometry);
        for (var hour = 6; hour <= 18; hour += 2)
        {
            var point = new Point(SkyX(SkyOptics.SunAzimuth(hour, Latitude)), horizon - (SkyOptics.SunAltitude(hour, Latitude) * scale));
            if (Math.Abs(hour - _hour) < 0.6) continue;
            dc.DrawEllipse(Paint.SunPathDot, null, point, 2.5, 2.5);
            var text = _labels.Text($"{hour:00}:00", 10.5, Ink.Dim);
            dc.DrawText(text, new Point(point.X - (text.Width / 2), point.Y - text.Height - 5));
        }

        dc.Pop();
    }

    #endregion

    #region the land

    /// <summary>How high the distant hills rise above the sea, degrees: to the south and north, none east and west.</summary>
    private static double HillHeight(double azimuth)
    {
        static double Sector(double azimuth, double middle, double half) =>
            Math.Clamp((half - Math.Abs(SkyRenderer.Normalize(azimuth - middle))) / 12, 0, 1);

        var share = Math.Max(Sector(azimuth, 0, 50), Sector(azimuth, 180, 45));
        if (share <= 0) return 0;
        var a = azimuth * Deg;
        var hills = 1.6 + (0.8 * Math.Sin((3 * a) + 0.6)) + (0.5 * Math.Sin((7 * a) + 1.3)) + (0.22 * Math.Sin((17 * a) + 0.4));
        return Math.Max(0.3, hills) * Math.Sqrt(share);
    }

    private void DrawLand(PaintContext dc, double width, double height, double horizon, double scale)
    {
        var centre = SkyCentre();
        double HillTop(double azimuth) => horizon - (HillHeight(azimuth) * scale);

        // Lit by the Sun and the sky: grass and sand, the far hills seen through a little haze.
        var light = _sky.SunLight + _sky.SkyLight;
        var exposure = _sky.Exposure;
        var hazeColour = SkyOptics.Display(_sky.Radiance(1, 180), exposure, 255, SkySaturation);
        var hillColour = Lerp(SkyOptics.Display(new LinearRgb(0.03, 0.045, 0.025) * light * (1 / Math.PI), exposure, 255, 1.2), hazeColour, 0.45);
        var sand = SkyOptics.Display(new LinearRgb(0.11, 0.09, 0.06) * light * (1 / Math.PI), exposure, 255, 1.1);

        // The distant hills, rising from the sea.
        var hills = new StreamGeometry();
        using (var context = hills.Open())
        {
            context.BeginFigure(new Point(-2, horizon + 1), true);
            for (var x = -2.0; x <= width + 4; x += 3) context.LineTo(new Point(x, Math.Min(horizon + 1, HillTop(centre + ((x - (width / 2)) / scale)))));
            context.LineTo(new Point(width + 4, horizon + 1));

            context.EndFigure(true);
        }
        dc.DrawGeometry(new SolidColorBrush(hillColour), null, hills);

        var treeBrush = new SolidColorBrush(Lerp(hillColour, Colors.Black, 0.35));
        foreach (var azimuth in TreeAzimuths)
        {
            var x = SkyX(azimuth);
            if (x < -20 || x > width + 20 || HillHeight(azimuth) < 0.6) continue;
            var root = new Point(x, HillTop(azimuth) + 1);
            var h = scale * 1.2;
            dc.DrawGeometry(treeBrush, null, Polygon(new Point(root.X - (h * 0.3), root.Y), new Point(root.X, root.Y - h),
                new Point(root.X + (h * 0.3), root.Y)));
        }

        // The beach in front, a gentle curve, darker towards you.
        var shore = horizon + ((height - horizon) * 0.66);
        var beach = new StreamGeometry();
        using (var context = beach.Open())
        {
            context.BeginFigure(new Point(-2, height + 2), true);
            for (var x = -2.0; x <= width + 4; x += 6)
            {
                var azimuth = (centre + ((x - (width / 2)) / scale)) * Deg;
                context.LineTo(new Point(x, shore + (9 * Math.Sin((2 * azimuth) + 0.8)) + (4 * Math.Sin((5 * azimuth) + 2.1))));
            }

            context.LineTo(new Point(width + 4, height + 2));

            context.EndFigure(true);
        }
        var brush = new ImmutableLinearGradientBrush(
        [
            new ImmutableGradientStop(0, sand),
            new ImmutableGradientStop(1, Lerp(sand, Colors.Black, 0.45)),
        ],
            startPoint: new RelativePoint(0, shore - 14, RelativeUnit.Absolute),
            endPoint: new RelativePoint(0, height, RelativeUnit.Absolute));
        dc.DrawGeometry(brush, Paint.Surf, beach);
    }

    /// <summary>The compass points along the horizon.</summary>
    private void DrawCompass(PaintContext dc, double horizon, double scale)
    {
        string[] keys = ["Sky_Compass_S", "Sky_Compass_SW", "Sky_Compass_W", "Sky_Compass_NW", "Sky_Compass_N", "Sky_Compass_NE", "Sky_Compass_E", "Sky_Compass_SE"];
        for (var i = 0; i < keys.Length; i++)
        {
            var x = SkyX(i * 45);
            if (x < 20 || x > ActualWidth - 20) continue;
            var text = _labels.Get(keys[i], 11, Ink.Text);
            _labels.DrawPill(dc, keys[i], new Point(x - (text.Width / 2) - 6, horizon + (2.2 * scale)), 11, Ink.Text);
        }
    }

    #endregion

    #region labels

    private void DrawSkyLabels(PaintContext dc, Point sun, double horizon, double scale, double altitude)
    {
        if (LookAway)
        {
            // After sunset the Earth's shadow rises opposite the Sun, with the pink belt of Venus above it.
            if (altitude is < 0.5 and > -7)
            {
                var top = Math.Max(1.5, -altitude);
                DrawCenteredPill(dc, Loc("Sky_Label_EarthShadow"), new Point(ActualWidth / 2, horizon - (top * 0.45 * scale)), 11.5, Ink.Text);
                AddCenteredPillTarget("earthshadow", Loc("Sky_Label_EarthShadow"), new Point(ActualWidth / 2, horizon - (top * 0.45 * scale)), 11.5, Ink.Text);
                DrawCenteredPill(dc, Loc("Sky_Label_BeltOfVenus"), new Point(ActualWidth / 2, horizon - ((top + 6) * scale)), 11.5, Ink.Text);
                AddCenteredPillTarget("beltofvenus", Loc("Sky_Label_BeltOfVenus"), new Point(ActualWidth / 2, horizon - ((top + 6) * scale)), 11.5, Ink.Text);
            }

            var away = _labels.Text(Loc("Sky_Label_SunBehind"), 11.5, Ink.Dim);
            _labels.DrawTextPill(dc, Loc("Sky_Label_SunBehind"), new Point((ActualWidth - away.Width) / 2, horizon - (45 * scale)), 11.5, Ink.Dim);
            AddPillTarget("sunbehind", Loc("Sky_Label_SunBehind"), new Point((ActualWidth - away.Width) / 2, horizon - (45 * scale)), 11.5, Ink.Dim);
            return;
        }

        if (altitude > -SunRadiusDegrees)
        {
            var label = LocFormat("Sky_Label_Sun", Format(altitude));
            var text = _labels.Text(label, 11.5, Ink.Gold);
            var x = sun.X + (SunRadiusDegrees * scale) + 10;
            var y = Math.Max(4, sun.Y - (text.Height / 2) - 2);
            if (altitude < 3) y = Math.Min(y, horizon - (5 * scale));
            _labels.DrawTextPill(dc, label, new Point(Math.Min(x, ActualWidth - text.Width - 16), y), 11.5, Ink.Gold);
            AddPillTarget("sun", label, new Point(Math.Min(x, ActualWidth - text.Width - 16), y), 11.5, Ink.Gold);
        }
        else
        {
            DrawCenteredPill(dc, LocFormat("Sky_Label_SunBelow", Format(-altitude)), new Point(sun.X, horizon - (6 * scale)), 11.5, Ink.Gold);
            AddCenteredPillTarget("sun", LocFormat("Sky_Label_SunBelow", Format(-altitude)), new Point(sun.X, horizon - (6 * scale)), 11.5, Ink.Gold);
        }
    }

    /// <summary>An angle for a label: whole degrees, with a decimal when small.</summary>
    internal static string Format(double degrees) =>
        degrees.ToString(Math.Abs(degrees) < 10 ? "0.0" : "0", CultureInfo.InvariantCulture);

    #endregion

    private static class Paint
    {
        public static readonly IPen SunPath = MakePen(Solid(90, 255, 255, 255), 1.2, dashed: true);
        public static readonly IBrush SunPathDot = Solid(150, 255, 255, 255);
        public static readonly IPen Surf = MakePen(Solid(70, 255, 255, 255), 1.2);
    }
}
