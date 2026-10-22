using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Earthquakes;

/// <summary>
/// The plates view: the world map with the plates tinted, the sea shaded deeper away from the ridges, the boundaries by
/// kind (trench teeth on the upper plate's side), arrows for how each plate moves, a year's earthquakes coloured by depth
/// that flash in turn, volcanoes, the Hawaiian chain, famous quakes and the viewer's place.
/// </summary>
public sealed partial class QuakeDiagram
{
    /// <summary>The map shows from 84° N to 66° S: the far south is all Antarctic plate and needs no room.</summary>
    private const double MapNorth = 84;

    private const double MapSouth = -66;

    /// <summary>A year's quakes flash in turn over this many seconds.</summary>
    private const double QuakeCycle = 40;

    private Rect _map;
    private RenderTargetBitmap? _mapLayer;
    private (Rect Map, bool Six, bool Quakes, bool Volcanoes) _mapLayerKey;
    private readonly List<(int Boundary, int Segment, Point A, Point B)> _boundarySegments = [];
    private Rect _boundarySegmentsMap;

    private static readonly Color[] PlateColours =
    [
        Color.FromRgb(0x5B, 0x8D, 0xEF), // PA
        Color.FromRgb(0xE8, 0x8A, 0x3C), // NA
        Color.FromRgb(0xE3, 0xC1, 0x4A), // SA
        Color.FromRgb(0x6C, 0xC4, 0x7A), // EU
        Color.FromRgb(0xD9, 0x6A, 0x6A), // AF
        Color.FromRgb(0xB0, 0x7C, 0xE0), // IN
        Color.FromRgb(0x4C, 0xC9, 0xC0), // AU
        Color.FromRgb(0xC8, 0xD2, 0xDC), // AN
        Color.FromRgb(0xF0, 0x8C, 0xC8), // NZ
        Color.FromRgb(0x9E, 0xD8, 0x6A), // CO
        Color.FromRgb(0xF2, 0xA0, 0x7A), // CA
        Color.FromRgb(0x8A, 0xA4, 0xF0), // PH
        Color.FromRgb(0xD8, 0xA8, 0x5C), // AR
        Color.FromRgb(0x9A, 0xB8, 0xC8), // SC
        Color.FromRgb(0xE0, 0x6C, 0xA0), // JF
    ];

    /// <summary>The six textbook plates: their name key's suffix, where the name goes, and the plate whose colour they take.</summary>
    private static readonly (string Group, GeoPoint Label, string ColourOf)[] Groups =
    [
        ("Pacific", new(5, -150), "PA"),
        ("American", new(35, -95), "NA"),
        ("Eurasian", new(55, 75), "EU"),
        ("African", new(4, 18), "AF"),
        ("Indian", new(-22, 88), "IN"),
        ("Antarctic", new(-60, 40), "AN"),
    ];

    private void InvalidateMap() => _mapLayer = null;

    private Point ToMap(double latitude, double longitude) => new(
        _map.X + ((longitude + 180) / 360 * _map.Width),
        _map.Y + ((MapNorth - latitude) / (MapNorth - MapSouth) * _map.Height));

    private Point ToMap(GeoPoint point) => ToMap(point.Latitude, point.Longitude);

    private GeoPoint FromMap(Point point) => new(
        Math.Clamp(MapNorth - ((point.Y - _map.Y) / _map.Height * (MapNorth - MapSouth)), -90, 90),
        PlateModel.Normalize(((point.X - _map.X) / _map.Width * 360) - 180));

    /// <summary>The colour a plate is tinted in (its group's in the six-plate map).</summary>
    private Color PlateColour(int index)
    {
        if (!SixPlates) return PlateColours[index];
        var group = PlateModel.Plates[index].Group;
        var owner = Groups.First(g => g.Group == group).ColourOf;
        return PlateColours[PlateModel.IndexOf(owner)];
    }

    /// <summary>A boundary is shown as a full plate boundary (in the six-plate map, only those between groups).</summary>
    private bool IsMainBoundary(PlateBoundary boundary) => boundary.Kind != BoundaryKind.Rift &&
        (!SixPlates || PlateModel.Find(boundary.A).Group != PlateModel.Find(boundary.B).Group);

    private void DrawPlatesMode(PaintContext dc, double now)
    {
        var stage = Stage();
        var width = Math.Min(stage.Width - 40, (stage.Height - 10) * 360 / (MapNorth - MapSouth));
        var height = width * (MapNorth - MapSouth) / 360;
        _map = new Rect(stage.X + ((stage.Width - width) / 2), stage.Y + Math.Max(0, (stage.Height - height) / 2), width, height);

        DrawMapLayer(dc);
        dc.PushClip(new RectangleGeometry(_map, 6, 6));
        if (ShowArrows) DrawPlateArrows(dc, now);
        DrawBoundaries(dc, now);
        if (ShowQuakes) DrawQuakeFlashes(dc, now);
        if (ShowVolcanoes) DrawHawaiiChain(dc);
        DrawFamousQuakes(dc, now);
        DrawPlace(dc);
        DrawPlateLabels(dc);
        dc.Pop();
        dc.DrawRoundedRectangle(null, Ink.Frame, _map, 6, 6);
        DrawMapLegend(dc);
    }

    #region the base map

    /// <summary>The sea, the land, the plate tints, the quakes and the volcanoes as one picture, made again only when they change.</summary>
    private void DrawMapLayer(PaintContext dc)
    {
        var key = (_map, SixPlates, ShowQuakes, ShowVolcanoes);
        if (_mapLayer is null || _mapLayerKey != key)
        {
            _mapLayerKey = key;
            _mapLayer = RenderMapLayer();
        }

        dc.DrawImage(_mapLayer, _map);
    }

    private RenderTargetBitmap RenderMapLayer()
    {
        var dpi = RenderScale();
        var pixelWidth = Math.Max(2, (int)Math.Round(_map.Width * dpi));
        var pixelHeight = Math.Max(2, (int)Math.Round(_map.Height * dpi));
        var surface = MapSurface(pixelWidth, pixelHeight);

        var bitmap = SpaceDrawing.RenderPixels(pixelWidth, pixelHeight, 96 * dpi, dc =>
        {
            var local = new Rect(0, 0, _map.Width, _map.Height);
            dc.DrawImage(surface, local);
            var offset = _map.TopLeft;
            dc.PushTransform(new TranslateTransform(-offset.X, -offset.Y));
            DrawBoundaryLines(dc);
            if (ShowQuakes) DrawQuakeDots(dc);
            if (ShowVolcanoes) DrawVolcanoes(dc);
            dc.Pop();
        });
        return bitmap;
    }

    /// <summary>The map's pixels: sea deeper away from the ridges, land, and each plate's tint over both.</summary>
    private WriteableBitmap MapSurface(int width, int height)
    {
        var mask = WorldMap.LandMask;
        var grid = PlateModel.PlateGrid;
        var ridge = PlateModel.RidgeDistance;
        var tints = new Color[PlateModel.Plates.Count];
        for (var i = 0; i < tints.Length; i++) tints[i] = PlateColour(i);

        var shallowSea = Color.FromRgb(0x2E, 0x6C, 0x9A);
        var deepSea = Color.FromRgb(0x0C, 0x24, 0x44);
        var lowLand = Color.FromRgb(0x5E, 0x6E, 0x52);
        var ice = Color.FromRgb(0xC8, 0xD0, 0xD8);
        var pixels = new byte[width * height * 4];
        Parallel.For(0, height, row =>
        {
            var latitude = MapNorth - ((row + 0.5) / height * (MapNorth - MapSouth));
            var gy = Math.Clamp((int)((90 - latitude) / 180 * PlateModel.GridHeight), 0, PlateModel.GridHeight - 1);
            for (var col = 0; col < width; col++)
            {
                var longitude = ((col + 0.5) / width * 360) - 180;
                var gx = Math.Clamp((int)((longitude + 180) / 360 * PlateModel.GridWidth), 0, PlateModel.GridWidth - 1);
                var cell = (gy * PlateModel.GridWidth) + gx;

                // The sea floor sinks with age: shade by the distance to the nearest ridge (2.5 cm a year each side).
                var age = ridge[cell] / 25.0;
                var depth = Math.Clamp((BoundaryModel.SeaDepth(age) - 2.5) / 3.0, 0, 1);
                var sea = Lerp(shallowSea, deepSea, Math.Sqrt(depth));
                var land = Math.Abs(latitude) > 64 ? Lerp(lowLand, ice, Math.Clamp((Math.Abs(latitude) - 64) / 10, 0, 1)) : lowLand;
                var share = mask.Sample(latitude, longitude);
                var colour = Lerp(sea, land, share);
                colour = Lerp(colour, tints[grid[cell]], 0.22 + (0.1 * share));

                var i = ((row * width) + col) * 4;
                pixels[i] = colour.B;
                pixels[i + 1] = colour.G;
                pixels[i + 2] = colour.R;
                pixels[i + 3] = 255;
            }
        });

        var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        SpaceDrawing.WritePixels(bitmap, pixels, width, height);
        return bitmap;
    }

    /// <summary>All the year's quakes as dots: size by magnitude, colour by depth, deep ones drawn last so they show.</summary>
    private void DrawQuakeDots(PaintContext dc)
    {
        var scale = Math.Clamp(_map.Width / 1100, 0.6, 1.4);
        foreach (var quake in PlateModel.Quakes.OrderBy(q => q.Depth))
        {
            var at = ToMap(quake.Latitude, quake.Longitude);
            var r = (0.9 + ((quake.Magnitude - 4.5) * 1.1)) * scale;
            dc.DrawEllipse(FadedBrush(DepthColour(quake.Depth), 0.6), null, at, r, r);
        }
    }

    private void DrawVolcanoes(PaintContext dc)
    {
        var size = Math.Clamp(_map.Width / 260, 2.5, 4.5);
        var fill = FadedBrush(Ink.Magma, 0.95);
        var edge = FadedPen(Color.FromRgb(0x30, 0x10, 0x05), 0.8, 0.8);
        foreach (var volcano in PlateModel.Volcanoes)
        {
            var at = ToMap(volcano.Latitude, volcano.Longitude);
            var s = volcano.Hotspot ? size * 1.4 : size;
            dc.DrawGeometry(volcano.Hotspot ? FadedBrush(Ink.Gold, 1) : fill, edge,
                Polygon(new Point(at.X, at.Y - s), new Point(at.X + (s * 0.9), at.Y + (s * 0.7)), new Point(at.X - (s * 0.9), at.Y + (s * 0.7))));
        }
    }

    #endregion

    #region boundaries, arrows

    /// <summary>Every boundary's line, and teeth on trenches and collisions (part of the cached map picture).</summary>
    private void DrawBoundaryLines(PaintContext dc)
    {
        for (var i = 0; i < PlateModel.Boundaries.Count; i++)
        {
            var boundary = PlateModel.Boundaries[i];
            var main = IsMainBoundary(boundary);
            var colour = BoundaryColour(boundary.Kind);
            var thick = main ? (boundary.Kind is BoundaryKind.Collision ? 2.6 : 2.1) : 1.2;
            var dashed = boundary.Kind is BoundaryKind.Diffuse or BoundaryKind.Rift || !main;
            var pen = FadedPen(main ? colour : Ink.Diffuse, main ? 0.95 : 0.45, thick, dashed);
            for (var s = 0; s + 1 < boundary.Points.Length; s++)
            {
                var (a, b) = SegmentOnMap(boundary.Points[s], boundary.Points[s + 1]);
                DrawWrapped(dc, pen, a, b);

                // Teeth on the upper plate's side of a trench, both sides of a collision.
                if (main && boundary.Kind is BoundaryKind.Trench or BoundaryKind.Collision) DrawTeeth(dc, boundary, s, a, b, colour);
            }
        }
    }

    /// <summary>The picked or hovered boundary drawn over the map, and the walkthrough's glow on one kind.</summary>
    private void DrawBoundaries(PaintContext dc, double now)
    {
        if (_boundarySegmentsMap != _map || _boundarySegments.Count == 0)
        {
            _boundarySegments.Clear();
            _boundarySegmentsMap = _map;
            for (var i = 0; i < PlateModel.Boundaries.Count; i++)
            {
                var points = PlateModel.Boundaries[i].Points;
                for (var s = 0; s + 1 < points.Length; s++)
                {
                    var (a, b) = SegmentOnMap(points[s], points[s + 1]);
                    _boundarySegments.Add((i, s, a, b));
                }
            }
        }

        foreach (var index in new[] { SelectedBoundary, _hoverBoundary }.Distinct())
        {
            if (index < 0 || index >= PlateModel.Boundaries.Count) continue;
            var colour = BoundaryColour(PlateModel.Boundaries[index].Kind);
            var glow = FadedPen(colour, 0.35, 9);
            var line = FadedPen(colour, 1, 3.4);
            foreach (var (i, _, a, b) in _boundarySegments)
            {
                if (i != index) continue;
                DrawWrapped(dc, glow, a, b);
                DrawWrapped(dc, line, a, b);
            }
        }

        // The glow the walkthrough puts on one kind of boundary.
        if (Highlight is "ridge" or "trench" or "collision" or "transform" && !_targets.TryGet(Highlight, out _))
        {
            var kind = Highlight switch
            {
                "ridge" => BoundaryKind.Ridge,
                "trench" => BoundaryKind.Trench,
                "collision" => BoundaryKind.Collision,
                _ => BoundaryKind.Transform,
            };
            var glow = ExploreTargets.GlowPen(now, 6);
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                foreach (var (index, _, a, b) in _boundarySegments)
                {
                    if (PlateModel.Boundaries[index].Kind != kind || !IsMainBoundary(PlateModel.Boundaries[index]) || Math.Abs(b.X - a.X) > _map.Width / 2) continue;
                    ctx.BeginFigure(a, false);
                    ctx.LineTo(b);
                    ctx.EndFigure(false);
                }
            }
            dc.DrawGeometry(null, glow, geometry);
        }
    }

    /// <summary>A segment on the map; a segment over the date line comes back as a long one, drawn twice by <see cref="DrawWrapped"/>.</summary>
    private (Point A, Point B) SegmentOnMap(GeoPoint p, GeoPoint q)
    {
        var a = ToMap(p);
        var lon = p.Longitude + PlateModel.Normalize(q.Longitude - p.Longitude);
        var b = ToMap(q.Latitude, lon);
        return (a, b);
    }

    private void DrawWrapped(PaintContext dc, IPen pen, Point a, Point b)
    {
        dc.DrawLine(pen, a, b);
        if (b.X > _map.Right) dc.DrawLine(pen, new Point(a.X - _map.Width, a.Y), new Point(b.X - _map.Width, b.Y));
        else if (b.X < _map.Left) dc.DrawLine(pen, new Point(a.X + _map.Width, a.Y), new Point(b.X + _map.Width, b.Y));
    }

    private void DrawTeeth(PaintContext dc, PlateBoundary boundary, int segment, Point a, Point b, Color colour)
    {
        Vector along = b - a;
        var length = along.Length;
        if (length < 6) return;
        along /= length;
        var (east, north) = PlateModel.TowardsB(boundary, segment);
        var normal = new Vector(east, -north);
        if (normal.Length < 1e-6) return;
        normal = normal.Normalize();

        var brush = FadedBrush(colour, 0.95);
        var size = Math.Clamp(_map.Width / 320, 2.5, 4.2);
        for (var d = 5.0; d < length - 2; d += size * 4.2)
        {
            var at = a + (along * d);
            Draw(at, normal);
            if (boundary.Kind == BoundaryKind.Collision) Draw(at + (along * size * 2), -normal);
        }

        void Draw(Point at, Vector n) => dc.DrawGeometry(brush, null,
            Polygon(at - (along * size), at + (along * size), at + (n * size * 1.6)));
    }

    /// <summary>Which way each plate moves: an arrow every 15° of longitude and 12° of latitude, as long as the speed.</summary>
    private void DrawPlateArrows(PaintContext dc, double now)
    {
        var perCm = _map.Width / 1100 * 3.4;
        var pulse = 0.55 + (0.2 * Math.Sin(now * 2));
        var pen = FadedPen(Colors.White, pulse, 1.4);
        for (var latitude = -54.0; latitude <= 72; latitude += 12)
        {
            var step = latitude is > 60 or < -50 ? 30.0 : 15.0;
            for (var longitude = -172.5; longitude < 180; longitude += step)
            {
                var plate = PlateModel.PlateAt(latitude, longitude);
                var (eastCm, northCm) = PlateModel.VelocityEN(plate, latitude, longitude);
                var from = ToMap(latitude, longitude);
                var tip = from + (new Vector(eastCm, -northCm) * perCm);
                if (PlateModel.PlateAt(MapLatitude(tip), MapLongitude(tip)) != plate) continue;
                if (((Vector)(tip - from)).Length < 4) continue;
                dc.DrawEllipse(FadedBrush(Colors.White, pulse * 0.8), null, from, 1.3, 1.3);
                DrawArrow(dc, pen, from, tip, 5);
            }
        }
    }

    private double MapLatitude(Point point) => MapNorth - ((point.Y - _map.Y) / _map.Height * (MapNorth - MapSouth));

    private double MapLongitude(Point point) => PlateModel.Normalize(((point.X - _map.X) / _map.Width * 360) - 180);

    #endregion

    #region quakes, volcanoes, places

    /// <summary>The year's quakes flash one after another: each gets a moment in a 40-second loop and rings out then.</summary>
    private void DrawQuakeFlashes(PaintContext dc, double now)
    {
        var quakes = PlateModel.Quakes;
        var phase = now / QuakeCycle % 1;
        var scale = Math.Clamp(_map.Width / 1100, 0.6, 1.4);
        for (var i = 0; i < quakes.Count; i++)
        {
            var quake = quakes[i];
            var moment = (i * 0.6180339887) % 1;
            var since = (phase - moment + 1) % 1 * QuakeCycle;
            if (since > 1.2) continue;
            var at = ToMap(quake.Latitude, quake.Longitude);
            var size = (2 + ((quake.Magnitude - 4.5) * 3)) * scale;
            var r = size * (1 + (since * 3));
            dc.DrawEllipse(null, FadedPen(DepthColour(quake.Depth), 0.9 * (1 - (since / 1.2)), 1.4), at, r, r);
        }
    }

    private void DrawHawaiiChain(PaintContext dc)
    {
        var chain = PlateModel.HawaiiChain;
        var showAges = Highlight == "hawaii" || SelectedObject == "hawaii";
        var pen = FadedPen(Ink.Gold, showAges ? 0.8 : 0.35, 1, dashed: true);
        for (var i = 0; i + 1 < chain.Count; i++)
        {
            var (a, b) = SegmentOnMap(chain[i].Point, chain[i + 1].Point);
            DrawWrapped(dc, pen, a, b);
        }

        for (var i = 0; i < chain.Count; i++)
        {
            var at = ToMap(chain[i].Point);
            var r = Math.Max(1.4, 3.2 - (i * 0.15));
            dc.DrawEllipse(FadedBrush(Ink.Gold, 0.9 - (i * 0.04)), null, at, r, r);
            if (showAges && i is 0 or 4 or 6 or 8 or 12)
            {
                DrawPill(dc, LocFormat("Quake_Value_Ma", Number(chain[i].Age, "0")), at + new Vector(0, -12), Ink.Gold, 10);
            }
        }

        _targets.Add("hawaii", ToMap(chain[0].Point), 9);
    }

    private void DrawFamousQuakes(PaintContext dc, double now)
    {
        foreach (var quake in PlateModel.Famous)
        {
            var at = ToMap(quake.Latitude, quake.Longitude);
            var picked = Famous == quake.Id;
            var r = picked ? 7.5 : 5.5;
            if (picked)
            {
                var ring = 8 + (now * 14 % 18);
                dc.DrawEllipse(null, FadedPen(Ink.Gold, 1 - ((ring - 8) / 18), 1.6), at, ring, ring);
            }

            dc.DrawGeometry(FadedBrush(Ink.Gold, 1), FadedPen(Colors.Black, 0.7, 1), Star(at, r));
            if (ShowLabels && (picked || _map.Width > 900))
            {
                DrawText(dc, quake.Year.ToString(System.Globalization.CultureInfo.InvariantCulture), at + new Vector(r + 3, -1), Ink.Gold, 10);
            }

            _targets.Add(FamousTargetId(quake.Id), at, 8);
        }
    }

    /// <summary>A five-pointed star.</summary>
    private static StreamGeometry Star(Point center, double r)
    {
        var points = new Point[10];
        for (var i = 0; i < 10; i++)
        {
            var angle = (-Math.PI / 2) + (i * Math.PI / 5);
            var radius = i % 2 == 0 ? r : r * 0.45;
            points[i] = new Point(center.X + (radius * Math.Cos(angle)), center.Y + (radius * Math.Sin(angle)));
        }

        return Polygon(points);
    }

    /// <summary>The viewer's place: a pin, and a dashed line to the nearest plate boundary.</summary>
    private void DrawPlace(PaintContext dc)
    {
        var place = new GeoPoint(PlaceLatitude, PlaceLongitude);
        var at = ToMap(place);
        var nearest = PlateModel.Nearest(place);
        if (nearest.Boundary >= 0)
        {
            var boundary = PlateModel.Boundaries[nearest.Boundary];
            var point = PlateModel.Interpolate(boundary.Points[nearest.Segment], boundary.Points[nearest.Segment + 1], nearest.T);
            var to = ToMap(point);
            if (Math.Abs(to.X - at.X) < _map.Width / 2)
            {
                dc.DrawLine(FadedPen(Ink.Cyan, 0.8, 1.2, dashed: true), at, to);
                if (ShowLabels)
                {
                    // Past the place's own name: most of the way to the boundary, or beyond it when the line is short.
                    Vector along = to - at;
                    var middle = along.Length < 60 ? to + (along.Length < 1 ? new Vector(0, -14) : along / along.Length * 30) : at + (along * 0.65);
                    DrawPill(dc, LocFormat("Quake_Value_Km", Number(Math.Round(nearest.Km / 10) * 10, "#,0")), middle, Ink.Cyan, 10);
                }
            }
        }

        dc.DrawEllipse(FadedBrush(Ink.Cyan, 0.3), null, at, 9, 9);
        dc.DrawEllipse(FadedBrush(Ink.Cyan, 1), FadedPen(Colors.Black, 0.6, 1), at, 4, 4);
        if (ShowLabels && !string.IsNullOrEmpty(PlaceName)) AddBoxTarget("place", DrawPill(dc, PlaceName, at + new Vector(0, 16), Ink.Cyan, 11));
        _targets.Add("place", at, 9);
    }

    private void DrawPlateLabels(PaintContext dc)
    {
        if (SixPlates)
        {
            foreach (var (group, label, colourOf) in Groups)
            {
                var box = DrawPill(dc, Loc(GroupKey(group)), ToMap(label), Lerp(PlateColours[PlateModel.IndexOf(colourOf)], Colors.White, 0.45), 12);
                AddBoxTarget("group:" + group, box);
            }

            return;
        }

        foreach (var plate in PlateModel.Plates)
        {
            var colour = Lerp(PlateColours[PlateModel.IndexOf(plate.Id)], Colors.White, 0.45);
            var small = plate.Id is "JF" or "SC" or "CO" or "CA" or "PH" or "AR";
            if (small && _map.Width < 760) continue;
            var box = DrawPill(dc, Loc(PlateKey(plate.Id)), ToMap(plate.Label), colour, small ? 10 : 12);
            AddBoxTarget("plate:" + plate.Id, box);
        }
    }

    /// <summary>The key for kinds of boundary and the depth colours, in the map's lower-left corner; each kind can be clicked.</summary>
    private void DrawMapLegend(PaintContext dc)
    {
        if (!ShowLabels) return;
        (string Id, BoundaryKind Kind)[] kinds =
        [
            ("ridge", BoundaryKind.Ridge),
            ("trench", BoundaryKind.Trench),
            ("collision", BoundaryKind.Collision),
            ("transform", BoundaryKind.Transform),
        ];

        var x = _map.X + 10;
        var y = _map.Bottom - 10 - (ShowQuakes ? 104 : 80);
        var box = new Rect(x - 4, y - 6, 168, ShowQuakes ? 118 : 94);
        if (_map.Width < 700) return;
        dc.DrawRoundedRectangle(Ink.Panel, Ink.Frame, box, 8, 8);
        foreach (var (id, kind) in kinds)
        {
            var colour = BoundaryColour(kind);
            dc.DrawLine(FadedPen(colour, 1, 2.4), new Point(x + 4, y + 8), new Point(x + 26, y + 8));
            if (kind == BoundaryKind.Trench) dc.DrawGeometry(FadedBrush(colour, 1), null, Polygon(new Point(x + 12, y + 8), new Point(x + 18, y + 8), new Point(x + 15, y + 3)));
            var text = DrawText(dc, Loc(KindKey(kind)), new Point(x + 32, y + 8), Ink.Text, 11);
            AddBoxTarget(id, new Rect(x, y, text.Right - x, 16));
            y += 19;
        }

        if (!ShowQuakes) return;
        var cx = x + 4;
        (string Key, double Depth)[] depths = [("Quake_Depth_Shallow", 30), ("Quake_Depth_Mid", 150), ("Quake_Depth_Deep", 500)];
        foreach (var (key, depth) in depths)
        {
            dc.DrawEllipse(FadedBrush(DepthColour(depth), 1), null, new Point(cx + 4, y + 9), 4, 4);
            var text = DrawText(dc, Loc(key), new Point(cx + 11, y + 9), Ink.Dim, 10);
            cx = text.Right + 8;
        }

        AddBoxTarget("depth", new Rect(x, y, cx - x, 18));
    }

    #endregion

    /// <summary>A plate's name key: <c>Quake_Plate_PA</c>.</summary>
    internal static string PlateKey(string id) => id switch
    {
        "PA" => "Quake_Plate_PA",
        "NA" => "Quake_Plate_NA",
        "SA" => "Quake_Plate_SA",
        "EU" => "Quake_Plate_EU",
        "AF" => "Quake_Plate_AF",
        "IN" => "Quake_Plate_IN",
        "AU" => "Quake_Plate_AU",
        "AN" => "Quake_Plate_AN",
        "NZ" => "Quake_Plate_NZ",
        "CO" => "Quake_Plate_CO",
        "CA" => "Quake_Plate_CA",
        "PH" => "Quake_Plate_PH",
        "AR" => "Quake_Plate_AR",
        "SC" => "Quake_Plate_SC",
        _ => "Quake_Plate_JF",
    };

    /// <summary>A textbook plate's name key: <c>Quake_Group_Pacific</c>.</summary>
    internal static string GroupKey(string group) => group switch
    {
        "Pacific" => "Quake_Group_Pacific",
        "American" => "Quake_Group_American",
        "Eurasian" => "Quake_Group_Eurasian",
        "African" => "Quake_Group_African",
        "Indian" => "Quake_Group_Indian",
        _ => "Quake_Group_Antarctic",
    };

    /// <summary>A kind of boundary's name key.</summary>
    internal static string KindKey(BoundaryKind kind) => kind switch
    {
        BoundaryKind.Ridge => "Quake_Kind_Ridge",
        BoundaryKind.Trench => "Quake_Kind_Trench",
        BoundaryKind.Collision => "Quake_Kind_Collision",
        BoundaryKind.Transform => "Quake_Kind_Transform",
        BoundaryKind.Rift => "Quake_Kind_Rift",
        _ => "Quake_Kind_Diffuse",
    };

    /// <summary>The target id for a kind of boundary (the card that explains it).</summary>
    internal static string KindTarget(BoundaryKind kind) => kind switch
    {
        BoundaryKind.Ridge => "ridge",
        BoundaryKind.Trench => "trench",
        BoundaryKind.Collision => "collision",
        BoundaryKind.Transform => "transform",
        BoundaryKind.Rift => "rift",
        _ => "diffuse",
    };

    /// <summary>A famous quake's target id: its id in lower case.</summary>
    internal static string FamousTargetId(string id) => id.ToLowerInvariant();
}
