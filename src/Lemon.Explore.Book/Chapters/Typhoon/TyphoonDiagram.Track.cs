using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Chapters.DayNight;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Typhoon;

/// <summary>
/// Track mode: the north-west Pacific and the South China Sea, 100–160° E and 0–45° N. The sea is tinted by its
/// temperature (the 26.5 °C edge dashed), the subtropical high drawn as its outline (the "588 line") with the steering
/// wind round it, and the storm's whole track coloured by grade, a dot a day, its landfalls named. The storm itself
/// spins along the track; the shared observer location shows how close it came and how hard the wind blew there.
/// </summary>
public sealed partial class TyphoonDiagram
{
    private readonly MapPainter _mapPainter = new();
    private RenderTargetBitmap? _mapBitmap;
    private Rect _mapBitmapRect;
    private WriteableBitmap? _seaBitmap;
    private int _seaMonth;
    private (StreamGeometry Edge, StreamGeometry Inner)? _highLines;
    private (int Month, double Ridge, Rect Map) _highKey;

    private readonly record struct TrackView(Rect Map, double K)
    {
        public Point P(double latitude, double longitude) =>
            new(Map.Left + ((longitude - TrackModel.West) * K), Map.Top + ((TrackModel.North - latitude) * K));

        public (double Latitude, double Longitude) Geo(Point point) =>
            (TrackModel.North - ((point.Y - Map.Top) / K), TrackModel.West + ((point.X - Map.Left) / K));

        /// <summary>The whole world at the same scale, for the land drawn by <see cref="MapPainter"/>.</summary>
        public Rect World => new(Map.Left - ((TrackModel.West + 180) * K), Map.Top - ((90 - TrackModel.North) * K), 360 * K, 180 * K);
    }

    private TrackView TrackLayout()
    {
        var stage = Stage();
        var span = TrackModel.East - TrackModel.West;
        var tall = TrackModel.North - TrackModel.South;
        var k = Math.Max(1, Math.Min((stage.Width - 40) / span, (stage.Height - 8) / tall));
        var map = new Rect(stage.Left + ((stage.Width - (span * k)) / 2), stage.Top + 4, span * k, tall * k);
        return new TrackView(map, k);
    }

    private TrackPoint[] CurrentTrack => TrackModel.Track(new TrackOptions(StartLatitude, StartLongitude, RidgeWest, Month));

    /// <summary>A click on the open sea, in the tropics, starts a new storm there.</summary>
    private void StartStormAt(Point screen)
    {
        var view = TrackLayout();
        if (!view.Map.Contains(screen)) return;
        var (latitude, longitude) = view.Geo(screen);
        if (latitude < 5 || latitude > 30 || TrackModel.IsLand(latitude, longitude)) return;
        SetCurrentValue(StartLatitudeProperty, Math.Round(latitude, 1));
        SetCurrentValue(StartLongitudeProperty, Math.Round(longitude, 1));
        SetCurrentValue(IsPlayingProperty, true);
    }

    private void DrawTrackMode(PaintContext dc, double width, double height)
    {
        var view = TrackLayout();
        var track = CurrentTrack;
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        _stars.Draw(dc, width, height, RenderScale(), _clock.Elapsed.TotalSeconds, 0.3);

        dc.PushClip(new RectangleGeometry(view.Map, 10, 10));
        DrawTrackMapBase(dc, view);
        DrawSeaWarmth(dc, view);
        DrawHigh(dc, view);
        DrawSteering(dc, view);
        DrawTrackLine(dc, view, track);
        DrawPlace(dc, view, track);
        DrawStormOnMap(dc, view, track);
        dc.Pop();
        dc.DrawRoundedRectangle(null, Ink.Frame, view.Map, 10, 10);
        DrawTrackGraticule(dc, view);
    }

    /// <summary>Sea and land, rendered into a bitmap once per size.</summary>
    private void DrawTrackMapBase(PaintContext dc, TrackView view)
    {
        if (_mapBitmap is null || _mapBitmapRect != view.Map)
        {
            var dpi = RenderScale();
            _mapBitmapRect = view.Map;
            var world = view.World;
            world = world.Translate(new Vector(-view.Map.Left, -view.Map.Top));
            _mapBitmap = SpaceDrawing.RenderPixels(Math.Max(1, (int)Math.Ceiling(view.Map.Width * dpi)), Math.Max(1, (int)Math.Ceiling(view.Map.Height * dpi)), 96 * dpi, context => _mapPainter.DrawSurface(context, world));
        }

        dc.DrawImage(_mapBitmap, view.Map);
    }

    /// <summary>The sea tinted by its warmth (orange over 28.5 °C, yellow over 26.5), and the 26.5 °C edge dashed.</summary>
    private void DrawSeaWarmth(PaintContext dc, TrackView view)
    {
        if (_seaBitmap is null || _seaMonth != Month)
        {
            _seaMonth = Month;
            const int columns = 240;
            const int rows = 180;
            var pixels = new uint[columns * rows];
            for (var row = 0; row < rows; row++)
            {
                var latitude = TrackModel.North - ((row + 0.5) * (TrackModel.North - TrackModel.South) / rows);
                var sea = TrackModel.SeaTemperature(latitude, Month);
                var (alpha, r, g, b) = sea >= 28.5 ? (52, 0xFF, 0x6A, 0x3A) : sea >= StormModel.GenesisSea ? (36, 0xFF, 0xC8, 0x3A) : (0, 0, 0, 0);
                for (var column = 0; column < columns; column++)
                {
                    var longitude = TrackModel.West + ((column + 0.5) * (TrackModel.East - TrackModel.West) / columns);
                    var water = 1 - WorldMap.LandMask.Sample(latitude, longitude);
                    var a = alpha * water / 255.0;
                    pixels[(row * columns) + column] = ((uint)(a * 255) << 24) | ((uint)(r * a) << 16) | ((uint)(g * a) << 8) | (uint)(b * a);
                }
            }

            _seaBitmap = new WriteableBitmap(new PixelSize(columns, rows), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            SpaceDrawing.WritePixels(_seaBitmap, pixels, columns, rows);
        }

        dc.DrawImage(_seaBitmap, view.Map);
        var edge = TrackModel.GenesisEdge(Month);
        var y = view.P(edge, TrackModel.West).Y;
        if (Glows("warmedge")) dc.DrawLine(Glow, new Point(view.Map.Left, y), new Point(view.Map.Right, y));
        dc.DrawLine(TrackInk.WarmEdge, new Point(view.Map.Left, y), new Point(view.Map.Right, y));
        if (ShowLabels) AddBoxTarget("warmedge", DrawTextRight(dc, Loc("Typhoon_Track_WarmEdge"), new Point(view.Map.Right - 8, y - 9), Ink.Gold, 10));
    }

    /// <summary>The subtropical high: its outline (the "588 line") and the stronger middle, with its ridge.</summary>
    private void DrawHigh(PaintContext dc, TrackView view)
    {
        if (_highLines is null || _highKey != (Month, RidgeWest, view.Map))
        {
            _highKey = (Month, RidgeWest, view.Map);
            _highLines = (Contour(view, 3), Contour(view, 8));
        }

        dc.DrawGeometry(null, TrackInk.HighInner, _highLines.Value.Inner);
        if (Glows("edge588")) dc.DrawGeometry(null, Glow, _highLines.Value.Edge);
        dc.DrawGeometry(null, TrackInk.HighEdge, _highLines.Value.Edge);

        var ridge = TrackModel.RidgeLatitude(Month);
        var tip = RidgeWest + 2;
        dc.DrawLine(TrackInk.Ridge, view.P(ridge, tip), view.P(ridge, TrackModel.East));
        if (!ShowLabels) return;
        var centre = view.P(ridge, Math.Min(TrackModel.East - 8, Math.Max(RidgeWest + 22, 145)));
        AddBoxTarget("high", DrawText(dc, Loc("Typhoon_Track_High"), centre, TrackInk.HighColor, 20, center: true, bold: true));
        AddBoxTarget("high", DrawText(dc, Loc("Typhoon_Track_HighName"), centre + new Vector(0, 20), TrackInk.HighColor, 11, center: true));
        // Just south of the outline, where the 3 hPa contour lies (13° from the ridge).
        AddBoxTarget("edge588", DrawText(dc, Loc("Typhoon_Track_588"), view.P(ridge - 14.2, Math.Clamp(RidgeWest + 20, 120, 150)), TrackInk.HighColor, 10, center: true));
    }

    /// <summary>A contour of the high's pressure (marching squares on a half-degree grid).</summary>
    private StreamGeometry Contour(TrackView view, double level)
    {
        const double step = 0.5;
        var columns = (int)((TrackModel.East - TrackModel.West) / step) + 1;
        var rows = (int)((TrackModel.North - TrackModel.South) / step) + 1;
        var field = new double[rows, columns];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                field[row, column] = TrackModel.HighPressure(TrackModel.North - (row * step), TrackModel.West + (column * step), RidgeWest, Month) - level;
            }
        }

        Point At(int row, int column) => view.P(TrackModel.North - (row * step), TrackModel.West + (column * step));
        Point Cross(int r1, int c1, int r2, int c2)
        {
            var a = field[r1, c1];
            var b = field[r2, c2];
            var t = a / (a - b);
            return Mix(At(r1, c1), At(r2, c2), t);
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var row = 0; row < rows - 1; row++)
            {
                for (var column = 0; column < columns - 1; column++)
                {
                    var crossings = new List<Point>(4);
                    if ((field[row, column] < 0) != (field[row, column + 1] < 0)) crossings.Add(Cross(row, column, row, column + 1));
                    if ((field[row, column + 1] < 0) != (field[row + 1, column + 1] < 0)) crossings.Add(Cross(row, column + 1, row + 1, column + 1));
                    if ((field[row + 1, column + 1] < 0) != (field[row + 1, column] < 0)) crossings.Add(Cross(row + 1, column + 1, row + 1, column));
                    if ((field[row + 1, column] < 0) != (field[row, column] < 0)) crossings.Add(Cross(row + 1, column, row, column));
                    for (var i = 0; i + 1 < crossings.Count; i += 2)
                    {
                        context.BeginFigure(crossings[i], false);
                        context.LineTo(crossings[i + 1]);
                        context.EndFigure(false);
                    }
                }
            }
        }
        return geometry;
    }

    /// <summary>The steering wind every 5°: short arrows as long as the wind is strong.</summary>
    private void DrawSteering(PaintContext dc, TrackView view)
    {
        for (var latitude = 5.0; latitude <= 40; latitude += 5)
        {
            for (var longitude = 105.0; longitude <= 155; longitude += 5)
            {
                var (u, v) = TrackModel.Steering(latitude, longitude, RidgeWest, Month);
                var speed = Math.Sqrt((u * u) + (v * v));
                if (speed < 0.5) continue;
                var at = view.P(latitude, longitude);
                var direction = new Vector(u, -v) / speed;
                var length = Math.Min(view.K * 3.5, speed * view.K * 0.45);
                var tail = at - (direction * length / 2);
                var head = at + (direction * length / 2);
                if (Glows("steering")) dc.DrawLine(Glow, tail, head);
                dc.DrawLine(TrackInk.Steering, tail, head);
                DrawArrowHead(dc, TrackInk.SteeringHead, head, direction, 5);
                _targets.Add("steering", at, Math.Min(8, length / 2));
            }
        }
    }

    private void DrawTrackLine(PaintContext dc, TrackView view, TrackPoint[] track)
    {
        if (Glows("track")) dc.DrawGeometry(null, Glow, Line(track.Select(p => view.P(p.Latitude, p.Longitude))));

        // The line is a target every few hours, the day dots and the landfalls on top of it.
        for (var i = 3; i < track.Length; i += 6) _targets.Add("track", view.P(track[i].Latitude, track[i].Longitude), 6);
        for (var i = 1; i < track.Length; i++)
        {
            var color = GradeColor(StormModel.Grade(track[i].Wind));
            var past = track[i].Hour <= _trackHour;
            dc.DrawLine(FadedPen(color, past ? 1 : 0.45, past ? 3 : 2), view.P(track[i - 1].Latitude, track[i - 1].Longitude), view.P(track[i].Latitude, track[i].Longitude));
        }

        // A dot a day.
        for (var i = 24; i < track.Length; i += 24)
        {
            var at = view.P(track[i].Latitude, track[i].Longitude);
            dc.DrawEllipse(FadedBrush(Ink.Text, 0.9), null, at, 2.6, 2.6);
            if (ShowLabels) AddBoxTarget(DayId(i / 24), DrawText(dc, LocFormat("Typhoon_Track_Day", i / 24), at + new Vector(6, -8), Ink.Dim, 9.5));
            _targets.Add(DayId(i / 24), at, 6);
        }

        var start = view.P(track[0].Latitude, track[0].Longitude);
        dc.DrawEllipse(null, FadedPen(Ink.Text, 0.9, 1.4), start, 5, 5);
        if (ShowLabels) AddBoxTarget("start", DrawText(dc, Loc("Typhoon_Track_Start"), start + new Vector(8, 10), Ink.Text, 10));
        _targets.Add("start", start, 8);

        var falls = TrackModel.Landfalls(track);
        for (var k = 0; k < falls.Count; k++)
        {
            var landfall = falls[k];
            var p = track[landfall.Index];
            var at = view.P(p.Latitude, p.Longitude);
            dc.DrawEllipse(null, FadedPen(Ink.Red, 1, 2), at, 6, 6);
            if (ShowLabels && p.Hour <= _trackHour + 0.01)
            {
                var text = LocFormat("Typhoon_Track_Landfall", Loc($"Typhoon_City_{landfall.City.Id}"), GradeName(StormModel.Grade(p.Wind)), Number(p.Wind, "0"));
                AddBoxTarget(LandfallId(k), DrawPillOnMap(dc, view, text, at + new Vector(0, 18), Quantize(GradeColor(StormModel.Grade(p.Wind))), 10.5));
            }

            _targets.Add(LandfallId(k), at, 8);
        }
    }

    /// <summary>The shared observer location, and the storm's closest pass to it.</summary>
    private void DrawPlace(PaintContext dc, TrackView view, TrackPoint[] track)
    {
        var place = view.P(PlaceLatitude, PlaceLongitude);
        if (!view.Map.Contains(place)) return;
        var (index, distance, wind) = TrackModel.ClosestPass(track, PlaceLatitude, PlaceLongitude);
        var closest = view.P(track[index].Latitude, track[index].Longitude);
        var reached = track[index].Hour <= _trackHour;
        if (reached && distance < 1500)
        {
            dc.DrawLine(TrackInk.PassLine, place, closest);
        }

        dc.DrawEllipse(FadedBrush(Ink.Gold, 0.3), null, place, 8, 8);
        dc.DrawEllipse(FadedBrush(Ink.Gold, 1), null, place, 3.5, 3.5);
        AddBoxTarget("place", DrawText(dc, PlaceName, place + new Vector(8, -9), Ink.Gold, 11, bold: true));
        if (reached && ShowLabels)
        {
            var text = LocFormat("Typhoon_Track_Pass", Number(distance, "0"), ForceText(StormModel.Beaufort(wind)));
            AddBoxTarget("place", DrawPillOnMap(dc, view, text, place + new Vector(0, 18), Ink.Gold, 10.5));
        }

        _targets.Add("place", place, 9);
    }

    /// <summary>The storm on the map: its cloud (to scale) turning, the centre marked, a pill with its grade.</summary>
    private void DrawStormOnMap(PaintContext dc, TrackView view, TrackPoint[] track)
    {
        var now = TrackModel.At(track, _trackHour);
        var at = view.P(now.Latitude, now.Longitude);
        var pressure = StormModel.CentralPressure(Math.Max(15, now.Wind));
        var cloud = _cloud.Get(pressure, south: false, spin: true);
        var radius = StormCloud.ViewRadius / 111.0 * view.K;
        dc.PushOpacity(Smooth(10, 25, now.Wind) * 0.9);
        dc.PushTransform(SpaceDrawing.Rotation(-CloudTurn * 4 * _t, at.X, at.Y));
        dc.DrawImage(cloud, new Rect(at.X - radius, at.Y - radius, radius * 2, radius * 2));
        dc.Pop();
        dc.Pop();

        var grade = StormModel.Grade(now.Wind);
        var color = Quantize(GradeColor(grade));
        dc.DrawEllipse(FadedBrush(color, 1), MakePen(Solid(255, 0x08, 0x0C, 0x16), 1.2), at, 4.5, 4.5);
        var text = grade == StormGrade.None
            ? Loc("Typhoon_Track_Dissipated")
            : LocFormat("Typhoon_Track_Now", GradeName(grade), Number(now.Wind, "0"), Number(pressure, "0"));
        AddBoxTarget("storm", DrawPillOnMap(dc, view, text, at + new Vector(0, -radius * 0.35 - 14), color, 11));
        _targets.Add("storm", at, 9);
    }

    /// <summary>A pill kept inside the map (the map is clipped, so a pill near its edge would be cut off); returns where it went.</summary>
    private Rect DrawPillOnMap(PaintContext dc, TrackView view, string text, Point center, Color color, double size)
    {
        var half = (_labels.Text(text, size, color).Width / 2) + 10;
        var x = Math.Clamp(center.X, view.Map.Left + half, Math.Max(view.Map.Left + half, view.Map.Right - half));
        var y = Math.Clamp(center.Y, view.Map.Top + 12, view.Map.Bottom - 12);
        return DrawPill(dc, text, new Point(x, y), color, size);
    }

    private void DrawTrackGraticule(PaintContext dc, TrackView view)
    {
        if (!ShowLabels) return;
        foreach (var latitude in new[] { 10, 20, 30, 40 })
        {
            DrawTextRight(dc, LocFormat("Typhoon_Lat_N", latitude), new Point(view.Map.Left - 4, view.P(latitude, TrackModel.West).Y), Ink.Dim, 9.5);
        }

        foreach (var longitude in new[] { 110, 120, 130, 140, 150 })
        {
            DrawText(dc, LocFormat("Typhoon_Lon_E", longitude), new Point(view.P(0, longitude).X, view.Map.Bottom + 9), Ink.Dim, 9.5, center: true);
        }

        AddBoxTarget("pacific", DrawText(dc, Loc("Typhoon_Track_Pacific"), view.P(18, 152), Ink.Dim, 12, center: true));
        AddBoxTarget("scs", DrawText(dc, Loc("Typhoon_Track_SouthChinaSea"), view.P(14, 114), Ink.Dim, 11, center: true));
    }

    private static class TrackInk
    {
        public static readonly Color HighColor = Color.FromRgb(0xFF, 0x8A, 0x8A);
        public static readonly IPen HighEdge = MakePen(Solid(220, 0xFF, 0x8A, 0x8A), 2);
        public static readonly IPen HighInner = MakePen(Solid(110, 0xFF, 0x8A, 0x8A), 1.2, dashed: true);
        public static readonly IPen Ridge = MakePen(Solid(150, 0xFF, 0x8A, 0x8A), 1.2, dashed: true);
        public static readonly IPen WarmEdge = MakePen(Solid(170, 0xFF, 0xD3, 0x6B), 1.2, dashed: true);
        public static readonly IPen Steering = MakePen(Solid(110, 0xEE, 0xF1, 0xF6), 1.2);
        public static readonly IBrush SteeringHead = Solid(130, 0xEE, 0xF1, 0xF6);
        public static readonly IPen PassLine = MakePen(Solid(200, 0xFF, 0xD3, 0x6B), 1.2, dashed: true);
    }
}
