using Avalonia;
using Avalonia.Media;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Seasons;

/// <summary>
/// The sun-path view: the sky as a dome over the observer, seen from behind them and a little above, facing the
/// equator (south in the northern hemisphere, north in the southern). Today's path of the Sun is drawn over the paths
/// at the solstices and equinoxes, with sunrise and sunset on the horizon and a pole casting its shadow.
/// Local directions are (east, north, up) on a unit dome.
/// </summary>
public sealed partial class SeasonsDiagram
{
    private static readonly double SkyElevation = 16 * Math.PI / 180;
    private static readonly double SinS = Math.Sin(SkyElevation);
    private static readonly double CosS = Math.Cos(SkyElevation);

    /// <summary>Height of the pole at the observer, in dome radii.</summary>
    private const double PoleHeight = 0.2;

    /// <summary>Room above the dome for the compass label (the chapter's title is on the facing page).</summary>
    private const double SkyTopRoom = 55;

    /// <summary>Room below the horizon for the toolbar over the diagram.</summary>
    private const double SkyBottomRoom = 96;

    private Point _skyOrigin;
    private double _skyScale = 1;
    private double _facing = -1;

    /// <summary>A local direction (east, north, up) on screen, for an observer facing <see cref="_facing"/>.</summary>
    private Point SkyPoint(double east, double north, double up)
    {
        var right = _facing * east;
        var away = _facing * north;
        return new Point(_skyOrigin.X + (right * _skyScale), _skyOrigin.Y - (((up * CosS) + (away * SinS)) * _skyScale));
    }

    private void DrawSky(DrawingContext dc, double dpi, double now)
    {
        var width = ActualWidth;
        var height = ActualHeight;

        // Leave room for the toolbar (and a walkthrough's subtitles) below.
        var bottom = SkyBottomRoom + _reserve.Current;
        _skyScale = Math.Max(40, Math.Min(width / 2.5, (height - bottom - SkyTopRoom + 15) / (1.02 + SinS + 0.08)));
        _skyOrigin = new Point(width / 2, Math.Min(height - bottom - (_skyScale * (SinS + 0.08)), SkyTopRoom + (_skyScale * 1.02)));

        var latitude = Math.Clamp(Latitude, -90, 90);
        var tilt = Math.Clamp(Tilt, 0, 90);
        _facing = latitude >= 0 ? -1 : 1;
        var declination = SeasonsGeometry.Declination(_day, tilt);
        var hourAngle = SeasonsGeometry.HourAngle(_time);
        var sun = SeasonsGeometry.SunDirection(latitude, declination, hourAngle);
        var altitude = SeasonsGeometry.ToDegrees(Math.Asin(Math.Clamp(sun.Up, -1, 1)));

        // Daylight: 0 at night, 1 with the Sun more than 8° up; twilight in between.
        var daylight = Noise.SmoothStep(-8, 8, altitude);
        dc.FillRectangle(SkyBrush(altitude), new Rect(0, 0, width, height));
        _stars.Draw(dc, width, height, dpi, now, 0.85 * (1 - daylight));

        // The dome's outline and the ground.
        var r = _skyScale;
        var dome = new StreamGeometry();
        using (var ctx = dome.Open())
        {
            ctx.BeginFigure(new Point(_skyOrigin.X - r, _skyOrigin.Y), false);
            ctx.ArcTo(new Point(_skyOrigin.X + r, _skyOrigin.Y), new Size(r, r), 0, false, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }

        dc.DrawGeometry(null, Sky.DomePen, dome);
        using (dc.PushOpacity(0.55 + (0.45 * daylight)))
        {
            dc.DrawEllipse(Sky.Ground, Sky.HorizonPen, _skyOrigin, r, r * SinS);
        }

        DrawCompass(dc);

        // The paths at the solstices and equinoxes, then today's path over them.
        var reference = new (double Declination, string Key, IPen Pen)[]
        {
            (tilt, "Seasons_Event_JuneSolstice", Sky.JunePath),
            (0, "Seasons_Label_EquinoxPath", Sky.EquinoxPath),
            (-tilt, "Seasons_Event_DecemberSolstice", Sky.DecemberPath),
        };
        foreach (var (dec, key, pen) in reference)
        {
            if (Highlight == "paths" && dec != 0)
            {
                DrawSunPathGlow(dc, latitude, dec, now);
            }

            DrawSunPath(dc, latitude, dec, pen, null);
            var noon = SeasonsGeometry.SunDirection(latitude, dec, 0);
            if (noon.Up > 0.02 && Math.Abs(dec - declination) > 1.5)
            {
                var at = SkyPoint(noon.East, noon.North, noon.Up);
                var label = _labels.Get(key, 10.5, ((ISolidColorBrush)pen.Brush!).Color);
                dc.DrawText(label, new Point(at.X + 6, at.Y - label.Height - 1));
            }
        }

        if (Highlight == "todaypath")
        {
            DrawSunPathGlow(dc, latitude, declination, now);
        }

        DrawSunPath(dc, latitude, declination, Sky.TodayPath, Sky.TodayBelow);
        DrawSunriseAndSunset(dc, latitude, declination);
        DrawNoon(dc, latitude, declination);

        // The pole, its shadow and the line of sight to the Sun.
        var foot = SkyPoint(0, 0, 0);
        var top = SkyPoint(0, 0, PoleHeight);
        var sunAt = SkyPoint(sun.East, sun.North, sun.Up);
        var shadowEnd = top;
        if (altitude > 0.3)
        {
            var horizontal = Math.Sqrt((sun.East * sun.East) + (sun.North * sun.North));
            var length = Math.Min(PoleHeight * horizontal / Math.Max(sun.Up, 1e-6), 1.25);
            var shadow = horizontal < 1e-6
                ? foot
                : SkyPoint(-sun.East / horizontal * length, -sun.North / horizontal * length, 0);
            shadowEnd = shadow;
            using (dc.PushOpacity(daylight))
            {
                dc.DrawLine(Sky.ShadowPen, foot, shadow);
                dc.DrawLine(Sky.RayPen, top, sunAt);
            }
        }

        dc.DrawLine(Sky.PolePen, foot, top);
        var poleMiddle = new Point((foot.X + shadowEnd.X) / 2, (foot.Y + shadowEnd.Y) / 2);
        _targets.Add("pole", poleMiddle, Math.Max(16, (((Vector)(shadowEnd - foot)).Length / 2) + 4));
        dc.DrawEllipse(Sky.ObserverFill, null, top, 2.5, 2.5);
        var you = _labels.Get("Seasons_Label_Observer", 11, Sky.Text);
        dc.DrawText(you, new Point(foot.X + 7, foot.Y - you.Height + 2));

        // The Sun itself, dim and dashed once it has set.
        var glow = 46 * Math.Max(0.25, daylight);
        if (altitude > -2)
        {
            using (dc.PushOpacity(Math.Max(0.15, daylight)))
            {
                dc.DrawEllipse(Sky.SunGlow, null, sunAt, glow, glow);
            }

            dc.DrawEllipse(altitude > 0 ? Sky.SunDisc : Sky.LowSunDisc, null, sunAt, 11, 11);
            _targets.Add("skysun", sunAt, 13);
        }
        else
        {
            dc.DrawEllipse(null, Sky.SetSunPen, sunAt, 11, 11);
        }
    }

    /// <summary>The sky's colour from the Sun's altitude: night, twilight with a glowing horizon, day.</summary>
    internal static IBrush SkyBrush(double altitude)
    {
        var day = Noise.SmoothStep(-2, 12, altitude);
        var dusk = Noise.SmoothStep(-12, -1, altitude) * (1 - Noise.SmoothStep(4, 14, altitude));
        var top = Lerp(Lerp(Color.FromRgb(0x03, 0x05, 0x0C), Color.FromRgb(0x1B, 0x2A, 0x52), dusk), Color.FromRgb(0x2C, 0x67, 0xB0), day);
        var horizon = Lerp(Lerp(Color.FromRgb(0x05, 0x08, 0x12), Color.FromRgb(0xD9, 0x7B, 0x4A), dusk), Color.FromRgb(0x9C, 0xC8, 0xEC), day);
        return LinearGradient(top, horizon, 90);
    }

    /// <summary>The Sun's path for one declination: solid above the horizon, dashed below it when a pen is given.</summary>
    private void DrawSunPath(DrawingContext dc, double latitude, double declination, IPen above, IPen? below)
    {
        const int steps = 144;
        var previous = SeasonsGeometry.SunDirection(latitude, declination, -180);
        for (var i = 1; i <= steps; i++)
        {
            var next = SeasonsGeometry.SunDirection(latitude, declination, -180 + (360.0 * i / steps));
            var up = (previous.Up + next.Up) / 2;
            var pen = up >= 0 ? above : below;
            if (pen is not null)
            {
                dc.DrawLine(pen, SkyPoint(previous.East, previous.North, previous.Up), SkyPoint(next.East, next.North, next.Up));
            }

            previous = next;
        }
    }

    /// <summary>A walkthrough's glow along the part of a sun path above the horizon, as one stroke so it does not bead at the joins.</summary>
    private void DrawSunPathGlow(DrawingContext dc, double latitude, double declination, double now)
    {
        const int steps = 144;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var open = false;
            for (var i = 0; i <= steps; i++)
            {
                var d = SeasonsGeometry.SunDirection(latitude, declination, -180 + (360.0 * i / steps));
                if (d.Up < 0)
                {
                    if (open)
                    {
                        ctx.EndFigure(false);
                    }

                    open = false;
                    continue;
                }

                var point = SkyPoint(d.East, d.North, d.Up);
                if (!open)
                {
                    ctx.BeginFigure(point, false);
                    open = true;
                }
                else
                {
                    ctx.LineTo(point);
                }
            }

            if (open)
            {
                ctx.EndFigure(false);
            }
        }

        dc.DrawGeometry(null, ExploreTargets.GlowPen(now), geometry);
    }

    private void DrawCompass(DrawingContext dc)
    {
        foreach (var (east, north, key) in new[]
                 {
                     (0.0, 1.0, "Seasons_Compass_N"), (1.0, 0.0, "Seasons_Compass_E"),
                     (0.0, -1.0, "Seasons_Compass_S"), (-1.0, 0.0, "Seasons_Compass_W"),
                 })
        {
            var at = SkyPoint(east * 1.08, north * 1.08, 0);
            var text = _labels.Get(key, 12, Sky.Text, true);
            var offset = Math.Abs(east) > 0.5
                ? new Vector(east * _facing > 0 ? 4 : -4 - text.Width, -text.Height / 2)
                : new Vector(-text.Width / 2, north * _facing > 0 ? -text.Height - 2 : 2);
            dc.DrawText(text, at + offset);
        }
    }

    private void DrawSunriseAndSunset(DrawingContext dc, double latitude, double declination)
    {
        var h0 = SeasonsGeometry.SunsetHourAngle(latitude, declination);
        if (h0 <= 0.01 || h0 >= 179.99)
        {
            return;
        }

        foreach (var (hourAngle, key) in new[] { (-h0, "Seasons_Label_Sunrise"), (h0, "Seasons_Label_Sunset") })
        {
            var direction = SeasonsGeometry.SunDirection(latitude, declination, hourAngle);
            var at = SkyPoint(direction.East, direction.North, 0);
            dc.DrawEllipse(Sky.HorizonDot, null, at, 3.5, 3.5);
            _targets.Add(hourAngle < 0 ? "sunrise" : "sunset", at, 9);
            var text = _labels.Text(LocFormat(key, ClockText(12 + (hourAngle / 15))), 11, Sky.Text);
            dc.DrawText(text, new Point(at.X - (text.Width / 2), at.Y + 6));
        }
    }

    private void DrawNoon(DrawingContext dc, double latitude, double declination)
    {
        var noon = SeasonsGeometry.SunDirection(latitude, declination, 0);
        if (noon.Up <= 0)
        {
            return;
        }

        var at = SkyPoint(noon.East, noon.North, noon.Up);
        dc.DrawEllipse(Sky.NoonDot, null, at, 3, 3);
        _targets.Add("noon", at, 9);
        var altitude = SeasonsGeometry.NoonAltitude(latitude, declination);
        var text = _labels.Text(LocFormat("Seasons_Label_NoonAltitude", altitude), 11.5, Sky.Gold, true);
        dc.DrawText(text, new Point(at.X - text.Width - 8, at.Y - text.Height - 2));
    }

    /// <summary>Local solar time as hours and minutes, e.g. 06:12.</summary>
    internal static string ClockText(double hours)
    {
        var minutes = (int)Math.Round(hours * 60);
        minutes = ((minutes % 1440) + 1440) % 1440;
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

    private static class Sky
    {
        public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
        public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);

        public static readonly IPen DomePen = MakePen(Solid(60, 255, 255, 255), 1, dashed: true);
        public static readonly IPen HorizonPen = MakePen(Solid(150, 255, 255, 255), 1.2);
        public static readonly IBrush Ground = RadialGradient(
            (Color.FromRgb(0x4F, 0x6B, 0x3A), 0),
            (Color.FromRgb(0x2D, 0x40, 0x24), 1));

        public static readonly IPen JunePath = MakePen(Solid(170, 0xFF, 0x9F, 0x6B), 1.1, dashed: true);
        public static readonly IPen EquinoxPath = MakePen(Solid(150, 0xE6, 0xEA, 0xF2), 1.1, dashed: true);
        public static readonly IPen DecemberPath = MakePen(Solid(170, 0x8E, 0xC5, 0xFF), 1.1, dashed: true);
        public static readonly IPen TodayPath = MakePen(Solid(255, 0xFF, 0xD3, 0x6B), 2.4);
        public static readonly IPen TodayBelow = MakePen(Solid(90, 0xFF, 0xD3, 0x6B), 1.2, dashed: true);
        public static readonly IBrush HorizonDot = Solid(255, 0xFF, 0xD3, 0x6B);
        public static readonly IBrush NoonDot = Solid(255, 0xFF, 0xF1, 0xC4);

        public static readonly IPen PolePen = MakePen(Solid(255, 0xF2, 0xF4, 0xF8), 2.2);
        public static readonly IPen ShadowPen = MakePen(Solid(170, 8, 12, 6), 3.2);
        public static readonly IPen RayPen = MakePen(Solid(120, 0xFF, 0xE6, 0xA8), 1, dashed: true);
        public static readonly IBrush ObserverFill = Solid(255, 0xFF, 0xD3, 0x6B);

        public static readonly IBrush SunGlow = RadialGradient(
            (Color.FromArgb(170, 255, 240, 200), 0.15),
            (Color.FromArgb(60, 255, 210, 140), 0.45),
            (Color.FromArgb(0, 255, 190, 110), 1));

        public static readonly IBrush SunDisc = RadialGradient((Color.FromRgb(0xFF, 0xFC, 0xF0), 0), (Color.FromRgb(0xFF, 0xD8, 0x7A), 1));
        public static readonly IBrush LowSunDisc = RadialGradient((Color.FromRgb(0xFF, 0xD9, 0xA0), 0), (Color.FromRgb(0xF2, 0x8A, 0x48), 1));
        public static readonly IPen SetSunPen = MakePen(Solid(120, 0xFF, 0xD3, 0x6B), 1, dashed: true);
    }
}
