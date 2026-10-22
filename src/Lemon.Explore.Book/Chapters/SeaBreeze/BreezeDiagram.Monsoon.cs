using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Infrastructures.Localization;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.SeaBreeze;

/// <summary>
/// Monsoon mode: a map of Asia, the Indian Ocean and northern Australia through the year, with the big pressure systems,
/// the surface winds they drive and the rain of a few cities.
/// </summary>
public sealed partial class BreezeDiagram
{
    /// <summary>Grid steps of the pressure field (for isobars) and of the wind field (for the particles), degrees.</summary>
    private const double PressureStep = 2;

    private const double WindStep = 2;

    private static readonly int PressureColumns = (int)((BreezeModel.East - BreezeModel.West) / PressureStep) + 1;
    private static readonly int PressureRows = (int)((BreezeModel.North - BreezeModel.South) / PressureStep) + 1;
    private static readonly int WindColumns = (int)((BreezeModel.East - BreezeModel.West) / WindStep) + 1;
    private static readonly int WindRows = (int)((BreezeModel.North - BreezeModel.South) / WindStep) + 1;

    private readonly MapPainter _mapPainter = new();
    private readonly double[,] _pressure = new double[PressureRows, PressureColumns];
    private readonly (double U, double V)[,] _wind = new (double, double)[WindRows, WindColumns];
    private readonly List<WindParticle> _windParticles = [];
    private double _fieldMonth = double.NaN;

    private Rect _landRect;
    private Rect _mapRect;

    private sealed class WindParticle
    {
        public double Latitude;
        public double Longitude;
        public double Age;
        public double Life;
    }

    /// <summary>The map's place on screen: the shown part of the world at <paramref name="k"/> pixels per degree.</summary>
    private readonly record struct MonsoonView(Rect Map, double K)
    {
        public Point P(double latitude, double longitude) =>
            new(Map.Left + ((longitude - BreezeModel.West) * K), Map.Top + ((BreezeModel.North - latitude) * K));

        /// <summary>The whole world at the same scale, for drawing the land of the full map clipped to this part.</summary>
        public Rect World => new(Map.Left - ((BreezeModel.West + 180) * K), Map.Top - ((90 - BreezeModel.North) * K), 360 * K, 180 * K);
    }

    private MonsoonView MonsoonLayout()
    {
        var stage = Stage(TitleSpace);
        var span = BreezeModel.East - BreezeModel.West;
        var tall = BreezeModel.North - BreezeModel.South;
        var k = Math.Max(1, Math.Min((stage.Width - 40) / span, (stage.Height - 8) / tall));
        var map = new Rect(stage.Left + ((stage.Width - (span * k)) / 2), stage.Top + 4, span * k, tall * k);
        return new MonsoonView(map, k);
    }

    #region fields

    /// <summary>Works the pressure and wind grids out again when the month has moved on enough.</summary>
    private void UpdateFields()
    {
        var month = _month;
        if (!double.IsNaN(_fieldMonth))
        {
            var moved = Math.Abs(month - _fieldMonth);
            if (Math.Min(moved, 12 - moved) < 0.04) return;
        }

        _fieldMonth = month;
        for (var row = 0; row < PressureRows; row++)
        {
            var latitude = BreezeModel.North - (row * PressureStep);
            for (var column = 0; column < PressureColumns; column++)
            {
                _pressure[row, column] = BreezeModel.Pressure(latitude, BreezeModel.West + (column * PressureStep), month);
            }
        }

        for (var row = 0; row < WindRows; row++)
        {
            var latitude = BreezeModel.North - (row * WindStep);
            for (var column = 0; column < WindColumns; column++)
            {
                _wind[row, column] = BreezeModel.Wind(latitude, BreezeModel.West + (column * WindStep), month);
            }
        }

        _isobars = null;
    }

    /// <summary>The wind at a place, interpolated from the wind grid, m/s.</summary>
    private (double U, double V) WindAt(double latitude, double longitude)
    {
        var x = Math.Clamp((longitude - BreezeModel.West) / WindStep, 0, WindColumns - 1.001);
        var y = Math.Clamp((BreezeModel.North - latitude) / WindStep, 0, WindRows - 1.001);
        var x0 = (int)x;
        var y0 = (int)y;
        var fx = x - x0;
        var fy = y - y0;
        var a = _wind[y0, x0];
        var b = _wind[y0, x0 + 1];
        var c = _wind[y0 + 1, x0];
        var d = _wind[y0 + 1, x0 + 1];
        return (
            (((a.U * (1 - fx)) + (b.U * fx)) * (1 - fy)) + (((c.U * (1 - fx)) + (d.U * fx)) * fy),
            (((a.V * (1 - fx)) + (b.V * fx)) * (1 - fy)) + (((c.V * (1 - fx)) + (d.V * fx)) * fy));
    }

    private void AdvanceWindParticles(double step)
    {
        UpdateFields();
        while (_windParticles.Count < 700) _windParticles.Add(NewWindParticle(true));
        if (step <= 0) return;
        foreach (var p in _windParticles)
        {
            var (u, v) = WindAt(p.Latitude, p.Longitude);
            var cos = Math.Max(0.3, Math.Cos(p.Latitude * Math.PI / 180));
            p.Longitude += u * WindDegrees * step / cos;
            p.Latitude += v * WindDegrees * step;
            p.Age += step;
            if (p.Age > p.Life || p.Longitude < BreezeModel.West || p.Longitude > BreezeModel.East
                || p.Latitude < BreezeModel.South || p.Latitude > BreezeModel.North)
            {
                var fresh = NewWindParticle(false);
                (p.Latitude, p.Longitude, p.Age, p.Life) = (fresh.Latitude, fresh.Longitude, fresh.Age, fresh.Life);
            }
        }
    }

    /// <summary>A wind particle's tail: this many pieces, each this many seconds of its motion back.</summary>
    private const int TailSteps = 4;

    private const double TailSeconds = 0.45;

    /// <summary>Degrees a particle moves per second of playback for each m/s of wind.</summary>
    private const double WindDegrees = 0.28;

    private WindParticle NewWindParticle(bool anyAge)
    {
        var life = 2.5 + (_random.NextDouble() * 3);
        return new WindParticle
        {
            Latitude = BreezeModel.South + (_random.NextDouble() * (BreezeModel.North - BreezeModel.South)),
            Longitude = BreezeModel.West + (_random.NextDouble() * (BreezeModel.East - BreezeModel.West)),
            Life = life,
            Age = anyAge ? _random.NextDouble() * life : 0,
        };
    }

    #endregion

    private void DrawMonsoonMode(PaintContext dc, double width, double height)
    {
        UpdateFields();
        var view = MonsoonLayout();
        _mapRect = view.Map;
        var summer = BreezeModel.Summer(_month);

        _stars.Draw(dc, width, height, RenderScale(), _clock.Elapsed.TotalSeconds, 0.35);
        dc.PushClip(new RectangleGeometry(view.Map, 10, 10));
        DrawMapBase(dc, view);
        DrawSeasonTint(dc, view, summer);
        DrawLatitudes(dc, view);
        if (ShowPressure) DrawIsobars(dc, view);
        DrawTrough(dc, view);
        if (ShowFlow) DrawWindParticles(dc, view);
        if (ShowLabels) DrawMonsoonArrows(dc, view, summer);
        if (ShowPressure) DrawPressureCentres(dc, view, summer);
        DrawCities(dc, view);
        dc.Pop();
        dc.DrawRoundedRectangle(null, Ink.Frame, view.Map, 10, 10);

        DrawMonthClock(dc, width, view);
    }

    /// <summary>
    /// The sea and land of the map, and a white land mask, rendered into bitmaps once per size: drawing the world's coasts
    /// as geometry every frame keeps the render thread far too busy.
    /// </summary>
    private void DrawMapBase(PaintContext dc, MonsoonView view)
    {
        var dpi = RenderScale();
        if (_mapBitmap is null || _landRect != view.Map)
        {
            _landRect = view.Map;
            var pixelWidth = Math.Max(1, (int)Math.Ceiling(view.Map.Width * dpi));
            var pixelHeight = Math.Max(1, (int)Math.Ceiling(view.Map.Height * dpi));
            var world = view.World;
            world = world.Translate(new Vector(-view.Map.Left, -view.Map.Top));

            _mapBitmap = SpaceDrawing.RenderPixels(pixelWidth, pixelHeight, 96 * dpi, context => _mapPainter.DrawSurface(context, world));
            var land = LandGeometry(world);
            RenderTargetBitmap LandIn(Color color)
            {
                var bitmap = SpaceDrawing.RenderPixels(pixelWidth, pixelHeight, 96 * dpi, context => context.DrawGeometry(new SolidColorBrush(color), null, land));
                return bitmap;
            }

            _warmLand = LandIn(Ink.Red);
            _coldLand = LandIn(Ink.Blue);
        }

        SpaceDrawing.DrawWhole(dc, _mapBitmap, view.Map);
    }

    private RenderTargetBitmap? _mapBitmap;
    private RenderTargetBitmap? _warmLand;
    private RenderTargetBitmap? _coldLand;

    /// <summary>Land warmer than the sea is tinted red, colder blue: the north's land in its summer and winter, the south's the other way.</summary>
    private void DrawSeasonTint(PaintContext dc, MonsoonView view, double summer)
    {
        if (_warmLand is null || _coldLand is null) return;
        var north = (2 * summer) - 1;
        var equator = view.P(0, 0).Y;
        void Tint(Rect clip, double amount)
        {
            if (Math.Abs(amount) < 0.03) return;
            dc.PushClip(new RectangleGeometry(clip));
            dc.PushOpacity(0.32 * Math.Abs(amount));
            SpaceDrawing.DrawWhole(dc, amount > 0 ? _warmLand : _coldLand, view.Map);
            dc.Pop();
            dc.Pop();
        }

        Tint(new Rect(view.Map.Left, view.Map.Top, view.Map.Width, Math.Max(0, equator - view.Map.Top)), north);
        Tint(new Rect(view.Map.Left, equator, view.Map.Width, Math.Max(0, view.Map.Bottom - equator)), -north);
    }

    private static StreamGeometry LandGeometry(Rect world)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.SetFillRule(FillRule.EvenOdd);
            foreach (var ring in WorldMap.Land)
            {
                if (ring.Length < 3) continue;
                context.BeginFigure(MapPainter.ToScreen(world, ring[0]), true);
                for (var i = 1; i < ring.Length; i++) context.LineTo(MapPainter.ToScreen(world, ring[i]));
                context.EndFigure(true);
            }
        }
        return geometry;
    }

    /// <summary>The equator and the tropics, with their latitudes at the left edge.</summary>
    private void DrawLatitudes(PaintContext dc, MonsoonView view)
    {
        var equator = view.P(0, BreezeModel.West);
        dc.DrawLine(MonsoonInk.Equator, equator, new Point(view.Map.Right, equator.Y));
        foreach (var latitude in new[] { 23.44, -23.44 })
        {
            var y = view.P(latitude, 0).Y;
            dc.DrawLine(MonsoonInk.Tropic, new Point(view.Map.Left, y), new Point(view.Map.Right, y));
        }

        if (!ShowLabels) return;
        foreach (var latitude in new[] { 40, 20, 0, -20 })
        {
            var text = latitude == 0 ? Loc("Breeze_Equator") : LocFormat(latitude > 0 ? "Breeze_Lat_N" : "Breeze_Lat_S", Math.Abs(latitude));
            var drawn = DrawText(dc, text, view.P(latitude, BreezeModel.West) + new Vector(6, -8), Ink.Dim, 10);
            if (latitude == 0) AddBoxTarget("equator", drawn);
        }
    }

    #region pressure

    /// <summary>The isobars every 4 hPa and those in between, each set in one geometry (isobars never cross).</summary>
    private (StreamGeometry Major, StreamGeometry Minor)? _isobars;

    private Rect _isobarsRect;

    /// <summary>Isobars every 2 hPa (marching squares on the pressure grid), every 4 hPa a little brighter.</summary>
    private void DrawIsobars(PaintContext dc, MonsoonView view)
    {
        if (_isobars is null || _isobarsRect != view.Map)
        {
            _isobarsRect = view.Map;
            _isobars = (Contours(view, 996, 4), Contours(view, 994, 4));
        }

        dc.DrawGeometry(null, MonsoonInk.IsobarMajor, _isobars.Value.Major);
        dc.DrawGeometry(null, MonsoonInk.IsobarMinor, _isobars.Value.Minor);
    }

    /// <summary>The isobars from <paramref name="first"/> up to 1040 hPa every <paramref name="step"/> hPa.</summary>
    private StreamGeometry Contours(MonsoonView view, double first, double step)
    {
        var geometry = new StreamGeometry();
        var crossings = new Point[4];
        using (var context = geometry.Open())
        {
            for (var level = first; level <= 1040; level += step)
            {
                for (var row = 0; row < PressureRows - 1; row++)
                {
                    for (var column = 0; column < PressureColumns - 1; column++)
                    {
                        var a = _pressure[row, column] - level;
                        var b = _pressure[row, column + 1] - level;
                        var c = _pressure[row + 1, column + 1] - level;
                        var d = _pressure[row + 1, column] - level;
                        if ((a < 0) == (b < 0) && (b < 0) == (c < 0) && (c < 0) == (d < 0)) continue;

                        // Where the level crosses each edge of the cell (top, right, bottom, left).
                        var count = 0;
                        if ((a < 0) != (b < 0)) crossings[count++] = Crossing(view, row, column + (a / (a - b)));
                        if ((b < 0) != (c < 0)) crossings[count++] = Crossing(view, row + (b / (b - c)), column + 1);
                        if ((c < 0) != (d < 0)) crossings[count++] = Crossing(view, row + 1, column + 1 - (c / (c - d)));
                        if ((d < 0) != (a < 0)) crossings[count++] = Crossing(view, row + 1 - (d / (d - a)), column);
                        for (var i = 0; i + 1 < count; i += 2)
                        {
                            context.BeginFigure(crossings[i], false);
                            context.LineTo(crossings[i + 1]);
                            context.EndFigure(false);
                        }
                    }
                }
            }
        }
        return geometry;
    }

    /// <summary>A point of the pressure grid, by its (fractional) row and column, on screen.</summary>
    private static Point Crossing(MonsoonView view, double row, double column) =>
        view.P(BreezeModel.North - (row * PressureStep), BreezeModel.West + (column * PressureStep));

    /// <summary>The named highs and lows that are strong enough at this time of year, with their central pressure.</summary>
    private void DrawPressureCentres(PaintContext dc, MonsoonView view, double summer)
    {
        foreach (var system in BreezeModel.Systems)
        {
            var anomaly = system.Anomaly(summer);
            var high = anomaly > 0;
            var name = $"{system.Key}_{(high ? "High" : "Low")}";
            if (!system.Labelled || !NamedCentres.Contains(name)) continue;
            var opacity = Math.Clamp((Math.Abs(anomaly) - 3) / 3, 0, 1);
            var longitude = system.Longitude(summer);
            var latitude = system.Latitude(summer);
            if (opacity <= 0.02 || longitude < BreezeModel.West + 3 || longitude > BreezeModel.East - 3) continue;
            if (latitude < BreezeModel.South + 3 || latitude > BreezeModel.North - 3) continue;
            var at = view.P(latitude, longitude);
            var id = SystemId(system.Key);
            if (!ShowLabels)
            {
                DrawPressureMark(dc, at, high, 18, opacity, id);
                continue;
            }

            // Names above the marks, except the West Pacific high's, which would cover Shanghai's.
            dc.PushOpacity(opacity);
            var pill = DrawPill(dc, $"{Loc($"Breeze_System_{name}")} {Number(BreezeModel.CentralPressure(system, _month), "0")} hPa",
                at + new Vector(0, system.Key == "PacificRidge" ? 30 : -30), high ? Ink.High : Ink.Low, 11);
            dc.Pop();
            if (opacity > 0.5) AddBoxTarget(id, pill);
            DrawPressureMark(dc, at, high, 18, opacity, id);
        }
    }

    /// <summary>The highs and lows that have a name on the map (a system is only named while it is what its name says).</summary>
    private static readonly HashSet<string> NamedCentres =
        ["Siberian_High", "AsianLow_Low", "Mascarene_High", "Australia_High", "Australia_Low", "PacificRidge_High"];

    /// <summary>The equatorial low-pressure trough where the trade winds meet, moving north in the northern summer.</summary>
    private void DrawTrough(PaintContext dc, MonsoonView view)
    {
        var line = new StreamGeometry();
        using (var context = line.Open())
        {
            for (var longitude = BreezeModel.West; longitude <= BreezeModel.East + 0.01; longitude += 2)
            {
                var at = view.P(BreezeModel.TroughLatitude(longitude, _month), longitude);
                if (longitude == BreezeModel.West) context.BeginFigure(at, false);
                else context.LineTo(at);
            }

            context.EndFigure(false);
        }
        dc.DrawGeometry(null, MonsoonInk.Trough, line);
        if (ShowLabels)
        {
            var at = view.P(BreezeModel.TroughLatitude(152, _month), 152);
            AddBoxTarget("trough", DrawPill(dc, Loc("Breeze_Trough"), at + new Vector(0, 14), Ink.Gold, 10.5));
        }
    }

    #endregion

    #region winds

    private void DrawWindParticles(PaintContext dc, MonsoonView view)
    {
        Span<Point> tail = stackalloc Point[TailSteps + 1];
        foreach (var p in _windParticles)
        {
            var (u, v) = WindAt(p.Latitude, p.Longitude);
            var speed = Math.Sqrt((u * u) + (v * v));
            if (speed < 0.6) continue;
            var life = p.Age / p.Life;
            var alpha = Math.Min(1, Math.Min(life, 1 - life) * 5) * Math.Min(1, speed / 4);
            if (alpha < 0.03) continue;
            // A tail traced back along the wind, so it bends with the flow.
            var (latitude, longitude) = (p.Latitude, p.Longitude);
            tail[0] = view.P(latitude, longitude);
            for (var k = 1; k <= TailSteps; k++)
            {
                var (tu, tv) = WindAt(latitude, longitude);
                var cos = Math.Max(0.3, Math.Cos(latitude * Math.PI / 180));
                latitude -= tv * WindDegrees * TailSeconds;
                longitude -= tu * WindDegrees * TailSeconds / cos;
                tail[k] = view.P(latitude, longitude);
            }

            _batch.Add(FadedPen(speed > 7 ? MonsoonInk.FastWind : MonsoonInk.Wind, alpha, speed > 7 ? 1.7 : 1.4, flat: true), tail);
        }

        _batch.Draw(dc);
    }

    /// <summary>The textbook's named monsoon arrows, each shown in its own season.</summary>
    private void DrawMonsoonArrows(PaintContext dc, MonsoonView view, double summer)
    {
        var summerOpacity = Smooth(0.55, 0.85, summer);
        var winterOpacity = 1 - Smooth(0.15, 0.45, summer);

        void Arrow((double Lat, double Lon) from, (double Lat, double Lon) control, (double Lat, double Lon) to, Color color, double opacity,
            string name, (double Lat, double Lon) label)
        {
            if (opacity < 0.03) return;
            var start = view.P(from.Lat, from.Lon);
            var bend = view.P(control.Lat, control.Lon);
            var end = view.P(to.Lat, to.Lon);
            DrawCurvedArrow(dc, start, bend, end, color, 5, opacity * 0.85);
            dc.PushOpacity(opacity);
            var pill = DrawPill(dc, Loc("Breeze_Monsoon_" + name), view.P(label.Lat, label.Lon), color, 11.5);
            dc.Pop();
            if (opacity < 0.5) return;
            _targets.Add(MonsoonId(name), CurveMiddle(start, bend, end), 16);
            AddBoxTarget(MonsoonId(name), pill);
        }

        Arrow((2, 52), (9, 70), (20, 79), Ink.Cyan, summerOpacity, "SW", (6, 66));
        Arrow((18, 136), (25, 128), (33, 114), Ink.Cyan, summerOpacity, "SE", (16, 140));
        Arrow((49, 108), (42, 118), (29, 121), Ink.Violet, winterOpacity, "NW", (51, 122));
        Arrow((25, 97), (17, 92), (8, 80), Ink.Violet, winterOpacity, "NE", (24, 84));
        Arrow((4, 112), (-6, 118), (-14, 131), Ink.Cyan, winterOpacity, "Australia", (-3, 104));
    }

    #endregion

    #region cities

    /// <summary>The cities as dots, each with a blue disc as big as this month's rain; the picked one ringed.</summary>
    private void DrawCities(PaintContext dc, MonsoonView view)
    {
        foreach (var city in BreezeModel.Cities)
        {
            var at = view.P(city.Latitude, city.Longitude);
            var rain = BreezeModel.RainIn(city.Id, _month);
            var radius = Math.Sqrt(rain / 840) * 26;
            if (radius > 1) dc.DrawEllipse(MonsoonInk.Rain, MonsoonInk.RainEdge, at, radius, radius);
            var picked = city.Id == City;
            if (picked) dc.DrawEllipse(null, MakePen(Solid(255, Ink.Gold.R, Ink.Gold.G, Ink.Gold.B), 2), at, 8, 8);
            dc.DrawEllipse(MonsoonInk.CityDot, null, at, 3.5, 3.5);
            if (!ShowLabels && !picked)
            {
                _targets.Add(CityId(city.Id), at, 12);
                continue;
            }

            var name = Loc($"Breeze_City_{city.Id}");
            var label = picked ? $"{name} · {LocFormat("Breeze_Rain_Value", Number(rain, "0"))}" : name;
            var formatted = _labels.Text(label, 11, picked ? Ink.Gold : Ink.Text, picked);
            // Changsha sits between Shanghai and Guangzhou: its name goes on the left.
            var left = city.Id switch
            {
                MonsoonCity.Shanghai => at.X + 10,
                MonsoonCity.Changsha => at.X - 9 - formatted.Width,
                _ => at.X + 9,
            };
            var top = city.Id == MonsoonCity.Guangzhou ? at.Y + 2 : at.Y - formatted.Height - 1;
            var back = new Rect(left - 3, top - 1, formatted.Width + 6, formatted.Height + 2);
            dc.DrawRoundedRectangle(MonsoonInk.LabelBack, null, back, 4, 4);
            dc.DrawText(formatted, new Point(left, top));
            AddBoxTarget(CityId(city.Id), back);
            _targets.Add(CityId(city.Id), at, 12);
        }
    }

    /// <summary>The month, large, at the top right.</summary>
    private void DrawMonthClock(PaintContext dc, double width, MonsoonView view)
    {
        var right = width - 22;
        var y = Math.Max(30, view.Map.Top - 70);
        DrawTextRight(dc, MonthName(BreezeModel.MonthNumber(_month)), new Point(right, y), Ink.Text, 30);
    }

    /// <summary>"7月" in Chinese, "July" in English.</summary>
    internal static string MonthName(int month)
    {
        var culture = LocalizationService.Instance.CurrentCulture;
        return culture.TwoLetterISOLanguageName == "zh"
            ? culture.DateTimeFormat.GetAbbreviatedMonthName(month)
            : culture.DateTimeFormat.GetMonthName(month);
    }

    #endregion

    private static class MonsoonInk
    {
        public static readonly IPen Equator = MakePen(Solid(80, 255, 255, 255), 1);
        public static readonly IPen Tropic = MakePen(Solid(90, 0xFF, 0xD3, 0x6B), 1, dashed: true);
        // Flat caps: an isobar is thousands of tiny segments, and round caps on each would be costly to draw.
        public static readonly IPen IsobarMinor = MakePen(Solid(70, 0xEE, 0xF1, 0xF6), 0.9);
        public static readonly IPen IsobarMajor = MakePen(Solid(140, 0xEE, 0xF1, 0xF6), 1.2);
        public static readonly IPen Trough = new ImmutablePen(((IBrush)Solid(200, 0xFF, 0xD3, 0x6B)).ToImmutable(), 2, new ImmutableDashStyle([5, 4], 0));
        public static readonly Color Wind = Color.FromRgb(0xEE, 0xF4, 0xFF);
        public static readonly Color FastWind = Color.FromRgb(0x9F, 0xE6, 0xFF);
        public static readonly IBrush Rain = Solid(90, 0x4A, 0x9A, 0xFF);
        public static readonly IPen RainEdge = MakePen(Solid(170, 0x8F, 0xC8, 0xFF), 1);
        public static readonly IBrush CityDot = Solid(255, 0xF2, 0xF4, 0xF8);
        public static readonly IBrush LabelBack = Solid(170, 8, 12, 22);
    }
}
