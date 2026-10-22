using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.DayNight;

/// <summary>The flat map: the lit half of Earth, twilight, the noon and midnight meridians, cities and time zones.</summary>
public sealed partial class DayNightDiagram
{
    /// <summary>The book puts the title on the left page: only a margin at the top (the original kept 150 for its title).</summary>
    private const double TitleSpace = 34;

    /// <summary>The toolbar, and a walkthrough's subtitles above it while one runs.</summary>
    private double ToolbarSpace => 92 + _reserve.Current;
    private const double BandHeight = 22;

    /// <summary>The map's rectangle: as large as fits, twice as wide as tall, with a label band above and below.</summary>
    private Rect MapRect()
    {
        const double side = 28;
        var availableWidth = Math.Max(10, ActualWidth - (2 * side));
        var availableHeight = Math.Max(10, ActualHeight - TitleSpace - ToolbarSpace - (2 * BandHeight));
        var width = Math.Min(availableWidth, availableHeight * 2);
        var height = width / 2;
        return new Rect(side + ((availableWidth - width) / 2), TitleSpace + BandHeight + ((availableHeight - height) / 2), width, height);
    }

    private GeoPoint? MapPointAt(Point point)
    {
        var map = MapRect();
        return map.Contains(point) ? MapPainter.ToGeo(map, point) : null;
    }

    private void DrawMap(PaintContext dc, DateTime utc, SunPosition sun, double now)
    {
        var map = MapRect();
        var zones = Mode == DayNightMode.TimeZones;

        dc.PushClip(new RectangleGeometry(map));
        _map.DrawSurface(dc, map);
        if (zones) DrawZoneStripes(dc, map, utc);
        MapPainter.DrawGraticule(dc, map);
        _map.DrawNight(dc, map, sun, zones ? 0.8 : 1);

        if (!zones && ShowTwilight)
        {
            foreach (var altitude in new[] { DayNightGeometry.CivilTwilight, DayNightGeometry.NauticalTwilight, DayNightGeometry.AstronomicalTwilight })
            {
                MapPainter.DrawCircle(dc, map, sun, altitude, DayNightPaint.TwilightLine);
            }
        }

        var line = MapPainter.CircleGeometry(map, DayNightGeometry.Circle(sun, 0, 720));
        if (Highlight == "terminator") dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 10), line);
        dc.DrawGeometry(null, DayNightPaint.DayNightLineShadow, line);
        dc.DrawGeometry(null, DayNightPaint.DayNightLine, line);

        if (zones)
        {
            DrawDateLines(dc, map, utc, now);
        }
        else
        {
            if (Highlight == "noonline") DrawMeridian(dc, map, sun.SubsolarLongitude, ExploreTargets.GlowPen(now, 9));
            DrawMeridian(dc, map, sun.SubsolarLongitude, DayNightPaint.NoonLine);
            DrawMeridian(dc, map, sun.SubsolarLongitude + 180, DayNightPaint.MidnightLine);
            DrawParallelNames(dc, map);
        }

        DrawCities(dc, map, utc, sun, zones);
        MapPainter.DrawSun(dc, map, sun, 7);
        _targets.Add("sun", MapPainter.ToScreen(map, sun.Subsolar), 12);
        if (!zones)
        {
            DrawLineNames(dc, map, sun);
            DrawOverheadLabel(dc, map, sun);
        }

        DrawPlace(dc, map, utc);
        dc.Pop();

        dc.DrawRectangle(null, DayNightPaint.MapFrame, map);
        if (zones)
        {
            DrawZoneBands(dc, map, utc);
        }
        else
        {
            DrawNoonBand(dc, map, sun);
            DrawLongitudeBand(dc, map);
        }
    }

    private static void DrawMeridian(PaintContext dc, Rect map, double longitude, IPen pen)
    {
        var x = MapPainter.ToScreen(map, 0, DayNightGeometry.Normalize180(longitude)).X;
        dc.DrawLine(pen, new Point(x, map.Top), new Point(x, map.Bottom));
    }

    /// <summary>Names of the tropics and polar circles at the map's right edge (over the Pacific).</summary>
    private void DrawParallelNames(PaintContext dc, Rect map)
    {
        if (map.Width < 560) return;

        foreach (var (latitude, key, color) in new[]
                 {
                     (90 - SeasonsTilt, "DayNight_Line_ArcticCircle", DayNightPaint.Blue),
                     (SeasonsTilt, "DayNight_Line_TropicCancer", DayNightPaint.Gold),
                     (0.0, "DayNight_Line_Equator", DayNightPaint.Text),
                     (-SeasonsTilt, "DayNight_Line_TropicCapricorn", DayNightPaint.Gold),
                     (-(90 - SeasonsTilt), "DayNight_Line_AntarcticCircle", DayNightPaint.Blue),
                 })
        {
            var y = MapPainter.ToScreen(map, latitude, 0).Y;
            var text = _labels.Get(key, 10, WithAlpha(color, 0.75));
            var at = new Point(map.Right - text.Width - 4, y - text.Height - 1);
            dc.DrawText(text, at);
            AddLabelTarget(latitude == 0 ? "equator" : Math.Abs(latitude) < 45 ? "tropic" : "polarcircle", at, text.Width, text.Height);
        }
    }

    private const double SeasonsTilt = 23.44;

    /// <summary>
    /// "Sunrise line" and "Sunset line" where the line between day and night crosses the equator: west of the Sun the
    /// day is starting, east of it ending.
    /// </summary>
    private void DrawLineNames(PaintContext dc, Rect map, SunPosition sun)
    {
        foreach (var (offset, key) in new[] { (-90.0, "DayNight_Label_SunriseLine"), (90.0, "DayNight_Label_SunsetLine") })
        {
            // Where the line crosses the equator, 90° from the Sun.
            var longitude = DayNightGeometry.Normalize180(sun.SubsolarLongitude + offset);
            var at = MapPainter.ToScreen(map, 0, longitude);
            var text = _labels.Get(key, 11.5, DayNightPaint.Gold, true);
            var left = offset < 0 ? at.X + 8 : at.X - text.Width - 20;
            left = Math.Clamp(left, map.Left + 4, map.Right - text.Width - 16);
            _labels.DrawPill(dc, key, new Point(left, at.Y + 6), 11.5, DayNightPaint.Gold);
            AddLabelTarget(offset < 0 ? "sunriseline" : "sunsetline", new Point(left, at.Y + 6), text.Width + 12, text.Height + 4);
        }
    }

    /// <summary>Under the Sun: where it is overhead, and that it moves west 15° an hour.</summary>
    private void DrawOverheadLabel(PaintContext dc, Rect map, SunPosition sun)
    {
        var at = MapPainter.ToScreen(map, sun.Subsolar);
        var label = LocFormat("DayNight_Label_Overhead", LatitudeText(sun.Declination));
        var text = _labels.Text(label, 11, DayNightPaint.Text);
        var left = Math.Clamp(at.X - ((text.Width + 12) / 2), map.Left + 2, map.Right - text.Width - 14);
        _labels.DrawTextPill(dc, label, new Point(left, at.Y + 14), 11, DayNightPaint.Text);

        var moves = _labels.Get("DayNight_Label_SunMoves", 10, DayNightPaint.DimText);
        var movesLeft = Math.Clamp(at.X - (moves.Width / 2), map.Left + 2, map.Right - moves.Width - 2);
        dc.DrawText(moves, new Point(movesLeft, at.Y + 14 + text.Height + 7));
    }

    /// <summary>Above the map: where it is noon and where it is midnight right now.</summary>
    private void DrawNoonBand(PaintContext dc, Rect map, SunPosition sun)
    {
        foreach (var (longitude, key, color, brush) in new[]
                 {
                     (sun.SubsolarLongitude, "DayNight_Label_Noon", DayNightPaint.Gold, DayNightPaint.GoldBrush),
                     (sun.SubsolarLongitude + 180, "DayNight_Label_Midnight", DayNightPaint.Blue, DayNightPaint.BlueBrush),
                 })
        {
            var x = MapPainter.ToScreen(map, 0, DayNightGeometry.Normalize180(longitude)).X;
            var text = _labels.Get(key, 11.5, color, true);
            var left = Math.Clamp(x - (text.Width / 2), map.Left, map.Right - text.Width);
            dc.DrawText(text, new Point(left, map.Top - text.Height - 5));
            AddLabelTarget(key == "DayNight_Label_Noon" ? "noon" : "midnight", new Point(left, map.Top - text.Height - 5), text.Width, text.Height);
            DrawArrowHead(dc, brush, new Point(x, map.Top + 1), new Vector(0, 1), 6);
        }
    }

    /// <summary>Longitudes below the map, every 30°.</summary>
    private void DrawLongitudeBand(PaintContext dc, Rect map)
    {
        for (var lon = -180; lon <= 180; lon += 30)
        {
            var x = MapPainter.ToScreen(map, 0, lon).X;
            var name = lon switch
            {
                0 => "0°",
                180 or -180 => "180°",
                > 0 => $"{lon}°E",
                _ => $"{-lon}°W",
            };
            var text = _labels.Text(name, 10, DayNightPaint.DimText);
            var left = Math.Clamp(x - (text.Width / 2), map.Left - 12, map.Right - text.Width + 12);
            dc.DrawText(text, new Point(left, map.Bottom + 4));
        }
    }

    #region time zones

    /// <summary>The date in the ideal time zone <paramref name="zone"/> (UTC+zone hours).</summary>
    private static DateTime ZoneTime(DateTime utc, int zone) => utc.AddHours(zone);

    private static (double West, double East) ZoneEdges(int zone) => (Math.Max(-180, (zone * 15) - 7.5), Math.Min(180, (zone * 15) + 7.5));

    /// <summary>Alternating strips for the 25 ideal zones; the strips already on the next date are tinted.</summary>
    private void DrawZoneStripes(PaintContext dc, Rect map, DateTime utc)
    {
        for (var zone = -12; zone <= 12; zone++)
        {
            var (west, east) = ZoneEdges(zone);
            var rect = new Rect(MapPainter.ToScreen(map, 90, west), MapPainter.ToScreen(map, -90, east));
            if (ZoneTime(utc, zone).Date > ZoneTime(utc, -12).Date) dc.DrawRectangle(DayNightPaint.LaterDate, null, rect);
            if ((zone & 1) == 0) dc.DrawRectangle(DayNightPaint.ZoneBand, null, rect);
        }

        // The ideal zone of the chosen place.
        var placeZone = DayNightGeometry.NominalZone(PlaceLongitude);
        var (placeWest, placeEast) = ZoneEdges(placeZone);
        var placeRect = new Rect(MapPainter.ToScreen(map, 90, placeWest), MapPainter.ToScreen(map, -90, placeEast));
        dc.DrawRectangle(null, DayNightPaint.ZoneOutline, placeRect);
    }

    /// <summary>
    /// The midnight edge, where the new date has reached so far (it moves west an hour's width every hour), the
    /// date line at 180°, and the date on each side.
    /// </summary>
    private void DrawDateLines(PaintContext dc, Rect map, DateTime utc, double now)
    {
        // The edge between the last zone still on the earlier date and the first on the later one.
        int? firstLater = null;
        for (var zone = -11; zone <= 12; zone++)
        {
            if (ZoneTime(utc, zone).Date > ZoneTime(utc, zone - 1).Date)
            {
                firstLater = zone;
                break;
            }
        }

        var dateLine = MapPainter.ToScreen(map, 0, 180).X - 1;
        if (Highlight == "dateline") dc.DrawLine(ExploreTargets.GlowPen(now, 9), new Point(dateLine, map.Top), new Point(dateLine, map.Bottom));
        dc.DrawLine(DayNightPaint.DateLine, new Point(dateLine, map.Top), new Point(dateLine, map.Bottom));
        var dateLineText = _labels.Get("DayNight_Label_DateLine", 10.5, Color.FromRgb(0xFF, 0x9F, 0x8A), true);
        DrawVerticalText(dc, dateLineText, new Point(dateLine - 4, map.Bottom - 8));
        AddLabelTarget("dateline", new Point(dateLine - 4 - dateLineText.Height, map.Bottom - 8 - dateLineText.Width), dateLineText.Height, dateLineText.Width);

        var earlier = ZoneTime(utc, -12).Date;
        var later = earlier.AddDays(1);
        double split;
        if (firstLater is { } newDayZone)
        {
            split = MapPainter.ToScreen(map, 0, ZoneEdges(newDayZone).West).X;
            if (Highlight == "newday") dc.DrawLine(ExploreTargets.GlowPen(now, 9), new Point(split, map.Top), new Point(split, map.Bottom));
            dc.DrawLine(DayNightPaint.NewDayLine, new Point(split, map.Top), new Point(split, map.Bottom));
            var midnight = _labels.Get("DayNight_Label_NewDay", 11, DayNightPaint.Blue, true);
            var left = Math.Clamp(split - midnight.Width - 18, map.Left + 4, map.Right - midnight.Width - 16);
            _labels.DrawPill(dc, "DayNight_Label_NewDay", new Point(left, map.Bottom - midnight.Height - 14), 11, DayNightPaint.Blue);
            AddLabelTarget("newday", new Point(left, map.Bottom - midnight.Height - 14), midnight.Width + 12, midnight.Height + 4);
        }
        else
        {
            split = map.Left;
            earlier = later;
        }

        DrawDatePill(dc, earlier, map.Left, split, map.Top + 8);
        DrawDatePill(dc, later, split, dateLine, map.Top + 8);
    }

    private void DrawDatePill(PaintContext dc, DateTime date, double from, double to, double top)
    {
        if (to - from < 70) return;
        var label = DateText(date);
        var text = _labels.Text(label, 12, DayNightPaint.Text, true);
        _labels.DrawTextPill(dc, label, new Point(((from + to) / 2) - ((text.Width + 12) / 2), top), 12, DayNightPaint.Text);
    }

    private static void DrawVerticalText(PaintContext dc, FormattedText text, Point bottomRight)
    {
        dc.PushTransform(SpaceDrawing.Rotation(-90, bottomRight.X, bottomRight.Y));
        dc.DrawText(text, new Point(bottomRight.X, bottomRight.Y - text.Height));
        dc.Pop();
    }

    /// <summary>Above the map each zone's offset from UTC, below it the time on its clocks.</summary>
    private void DrawZoneBands(PaintContext dc, Rect map, DateTime utc)
    {
        var placeZone = DayNightGeometry.NominalZone(PlaceLongitude);
        var utcText = _labels.Text("UTC", 10, DayNightPaint.DimText);
        dc.DrawText(utcText, new Point(map.Left - utcText.Width - 4, map.Top - utcText.Height - 5));
        AddLabelTarget("utc", new Point(map.Left - utcText.Width - 4, map.Top - utcText.Height - 5), utcText.Width, utcText.Height);

        for (var zone = -12; zone <= 12; zone++)
        {
            var (west, east) = ZoneEdges(zone);
            var x = MapPainter.ToScreen(map, 0, (west + east) / 2).X;
            var color = zone == placeZone ? DayNightPaint.Gold : DayNightPaint.DimText;

            var offset = zone switch
            {
                0 => "0",
                > 0 => $"+{zone}",
                _ => $"−{-zone}",
            };
            var offsetText = _labels.Text(offset, 10, color, zone == placeZone);
            dc.DrawText(offsetText, new Point(x - (offsetText.Width / 2), map.Top - offsetText.Height - 5));
            if (zone == placeZone) _targets.Add("zone", new Point(x, map.Top - (offsetText.Height / 2) - 5), 12);

            var clock = ZoneTime(utc, zone).ToString("H:mm", CultureInfo.InvariantCulture);
            var clockText = _labels.Text(clock, 10, zone == placeZone ? DayNightPaint.Gold : DayNightPaint.Text, zone == placeZone);
            dc.DrawText(clockText, new Point(x - (clockText.Width / 2), map.Bottom + 4));
        }
    }

    #endregion

    #region cities and the chosen place

    private static readonly Color Shadow = Color.FromArgb(220, 0, 0, 0);

    private void DrawCities(PaintContext dc, Rect map, DateTime utc, SunPosition sun, bool withTimes)
    {
        foreach (var city in WorldCities.All)
        {
            var at = MapPainter.ToScreen(map, city.Location);
            var altitude = DayNightGeometry.Altitude(sun, city.Latitude, city.Longitude);
            MapPainter.DrawCityLight(dc, at, DayNightPaint.Darkness(altitude));
            dc.DrawEllipse(DayNightPaint.CityDot, DayNightPaint.CityOutline, at, 2.6, 2.6);

            if (city.Id == PlaceCity) continue;
            var name = Loc(city.NameKey);
            var label = withTimes ? $"{name} {ClockText(TimeZoneInfo.ConvertTimeFromUtc(utc, city.TimeZone))}" : name;
            DrawShadowedText(dc, map, label, at, city.Label, 10.5, DayNightPaint.Text);
        }
    }

    private void DrawShadowedText(PaintContext dc, Rect map, string label, Point at, LabelSide side, double size, Color color)
    {
        var text = _labels.Text(label, size, color);
        var shadow = _labels.Text(label, size, Shadow);
        var origin = side switch
        {
            LabelSide.Left => new Point(at.X - 6 - text.Width, at.Y - (text.Height / 2)),
            LabelSide.Above => new Point(at.X - (text.Width / 2), at.Y - 5 - text.Height),
            LabelSide.Below => new Point(at.X - (text.Width / 2), at.Y + 4),
            _ => new Point(at.X + 6, at.Y - (text.Height / 2)),
        };

        // Keep names near the date line inside the map.
        origin = origin.WithX(Math.Clamp(origin.X, map.Left + 2, Math.Max(map.Left + 2, map.Right - text.Width - 2)));
        dc.DrawText(shadow, origin + new Vector(1, 1));
        dc.DrawText(shadow, origin + new Vector(-0.6, 0.6));
        dc.DrawText(text, origin);
    }

    /// <summary>The chosen place: a ring and a pill with its name and the time on its clocks.</summary>
    private void DrawPlace(PaintContext dc, Rect map, DateTime utc)
    {
        var at = MapPainter.ToScreen(map, PlaceLatitude, PlaceLongitude);
        dc.DrawEllipse(null, DayNightPaint.PlaceOutline, at, 8, 8);
        dc.DrawEllipse(null, DayNightPaint.PlaceRing, at, 7, 7);
        dc.DrawEllipse(DayNightPaint.PlaceFill, DayNightPaint.PlaceOutline, at, 3.2, 3.2);

        var time = TimeZoneInfo.ConvertTimeFromUtc(utc, ZoneOf(PlaceCity, PlaceLongitude));
        var label = $"{PlaceName()} {ClockText(time)}";
        var text = _labels.Text(label, 12, DayNightPaint.Gold, true);
        var width = text.Width + 12;
        var left = at.X + 12 + width > map.Right ? at.X - 12 - width : at.X + 12;
        var top = Math.Clamp(at.Y - text.Height - 10, map.Top + 2, map.Bottom - text.Height - 6);
        _labels.DrawTextPill(dc, label, new Point(left, top), 12, DayNightPaint.Gold);
    }

    private string PlaceName() => WorldCities.Find(PlaceCity) is { } city
        ? Loc(city.NameKey)
        : LocFormat("DayNight_Place_Point", LatitudeText(PlaceLatitude), LongitudeText(PlaceLongitude));

    #endregion
}
