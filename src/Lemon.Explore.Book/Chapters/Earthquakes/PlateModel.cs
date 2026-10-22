using System.Globalization;
using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Earthquakes;

/// <summary>The kind of edge between two plates.</summary>
public enum BoundaryKind
{
    /// <summary>Plates move apart and new sea floor forms (a mid-ocean ridge, the Red Sea).</summary>
    Ridge,

    /// <summary>One plate sinks under the other at a deep-sea trench.</summary>
    Trench,

    /// <summary>Two continents meet head on and pile up mountains.</summary>
    Collision,

    /// <summary>Plates slide past each other.</summary>
    Transform,

    /// <summary>A wide zone with no single clean line (India against Australia, North against South America).</summary>
    Diffuse,

    /// <summary>A continent starting to split; not yet a plate boundary (the East African Rift).</summary>
    Rift,
}

/// <summary>A tectonic plate: its id, its rotation (an Euler pole) and the textbook "six plates" group it belongs to.</summary>
/// <param name="Id">Two letters, as geologists write them: PA, NA, EU …</param>
/// <param name="PoleLatitude">Euler pole latitude, °.</param>
/// <param name="PoleLongitude">Euler pole longitude, °.</param>
/// <param name="Rate">Rotation about the pole, ° per million years.</param>
/// <param name="Group">The plate of the six-plate textbook map this one is part of (Pacific, American, Eurasian, African, Indian, Antarctic).</param>
/// <param name="Label">Where its name is written on the map.</param>
public sealed record Plate(string Id, double PoleLatitude, double PoleLongitude, double Rate, string Group, GeoPoint Label);

/// <summary>
/// A plate boundary as a line of points. For a <see cref="BoundaryKind.Trench"/>, plate <see cref="A"/> sinks under
/// <see cref="B"/>. <see cref="MaxDepth"/> is how deep its quakes go (km) and <see cref="Dip"/> how steeply the sinking
/// plate goes down (°).
/// </summary>
public sealed record PlateBoundary(string Id, BoundaryKind Kind, string A, string B, GeoPoint[] Points, double MaxDepth = 25, double Dip = 45);

/// <summary>An earthquake on the map: where, how deep (km) and how big.</summary>
public readonly record struct MapQuake(double Latitude, double Longitude, double Depth, double Magnitude, int Boundary);

/// <summary>A well-known earthquake: <see cref="Id"/> names its strings and its narration.</summary>
public sealed record FamousQuake(string Id, int Year, double Magnitude, double Latitude, double Longitude, double Depth, BoundaryKind Kind);

/// <summary>A volcano or hotspot on the map.</summary>
public readonly record struct MapVolcano(double Latitude, double Longitude, bool Hotspot);

/// <summary>
/// The plates for the earthquake page's map: 15 plates whose rotations are NNR-NUVEL-1A Euler poles (DeMets et al. 1994,
/// typed from memory; Juan de Fuca's fitted to its known north-east drift), their boundaries as simplified lines (a few
/// degrees from the real ones in places), a 0.5° raster telling which plate a point is on, and a made-up but realistic
/// set of earthquakes placed along the boundaries: most shallow, the deep ones only behind trenches, as many of each size
/// as the Gutenberg–Richter law gives.
/// </summary>
public static class PlateModel
{
    /// <summary>Earth's mean radius, km.</summary>
    public const double EarthRadius = 6371;

    public const int GridWidth = 720;

    public const int GridHeight = 360;

    /// <summary>Kilometres per degree of a great circle.</summary>
    public const double KmPerDegree = Math.PI * EarthRadius / 180;

    public static IReadOnlyList<Plate> Plates { get; } =
    [
        new("PA", -63.037, 107.328, 0.6409, "Pacific", new(5, -150)),
        new("NA", -2.429, -86.031, 0.2069, "American", new(52, -100)),
        new("SA", -25.346, -124.605, 0.1163, "American", new(-12, -57)),
        new("EU", 50.621, -112.350, 0.2337, "Eurasian", new(55, 75)),
        new("AF", 50.569, -73.978, 0.2909, "African", new(4, 18)),
        new("IN", 45.512, 0.345, 0.5453, "Indian", new(8, 78)),
        new("AU", 33.851, 33.175, 0.6461, "Indian", new(-26, 133)),
        new("AN", 62.985, -115.875, 0.2383, "Antarctic", new(-62, 60)),
        new("NZ", 47.802, -100.228, 0.7434, "Pacific", new(-20, -92)),
        new("CO", 24.464, -115.801, 1.5099, "Pacific", new(9, -96)),
        new("CA", 25.0, -93.2, 0.214, "American", new(15, -76)),
        new("PH", -45.6, -31.4, 0.90, "Eurasian", new(18, 134)),
        new("AR", 45.230, -1.504, 0.5456, "Indian", new(23, 46)),
        new("SC", -2.0, -100.0, 0.10, "American", new(-58, -45)),
        new("JF", -18.0, 66.0, 0.50, "Pacific", new(46, -128.5)),
    ];

    /// <summary>A point inside each plate the raster is filled from.</summary>
    private static readonly Dictionary<string, GeoPoint[]> Seeds = new()
    {
        ["PA"] = [new(0, -160), new(-30, -150), new(30, 170)],
        ["NA"] = [new(50, -100), new(70, -40), new(65, 160)],
        ["SA"] = [new(-15, -55), new(-40, -40)],
        ["EU"] = [new(55, 60), new(30, 110), new(48, 10)],
        ["AF"] = [new(5, 20), new(-30, 0)],
        ["IN"] = [new(15, 78), new(-5, 75)],
        ["AU"] = [new(-25, 135), new(-35, 100)],
        ["AN"] = [new(-80, 0), new(-70, 150), new(-70, -120)],
        ["NZ"] = [new(-20, -90)],
        ["CO"] = [new(10, -95)],
        ["CA"] = [new(15, -75)],
        ["PH"] = [new(20, 135)],
        ["AR"] = [new(23, 45)],
        ["SC"] = [new(-58, -45)],
        ["JF"] = [new(46, -128.5)],
    };

    public static IReadOnlyList<PlateBoundary> Boundaries { get; } = BuildBoundaries();

    private static readonly Lazy<byte[]> Grid = new(BuildGrid);
    private static readonly Lazy<IReadOnlyList<MapQuake>> Catalog = new(() => MakeQuakes(2600, 20251010));

    /// <summary>Plate index per 0.5° cell, row 0 at 90° N, column 0 at 180° W.</summary>
    public static byte[] PlateGrid => Grid.Value;

    /// <summary>The map's quakes, made once (the same every run).</summary>
    public static IReadOnlyList<MapQuake> Quakes => Catalog.Value;

    public static Plate Find(string id) => Plates.First(p => p.Id == id);

    public static int IndexOf(string id)
    {
        for (var i = 0; i < Plates.Count; i++)
        {
            if (Plates[i].Id == id) return i;
        }

        throw new ArgumentException($"No plate {id}.", nameof(id));
    }

    /// <summary>The plate a point is on.</summary>
    public static Plate PlateAt(double latitude, double longitude)
    {
        var (x, y) = Cell(latitude, longitude);
        return Plates[PlateGrid[(y * GridWidth) + x]];
    }

    private static (int X, int Y) Cell(double latitude, double longitude)
    {
        var x = (int)Math.Floor((Normalize(longitude) + 180) / 360 * GridWidth);
        var y = (int)Math.Floor((90 - latitude) / 180 * GridHeight);
        return (Math.Clamp(x, 0, GridWidth - 1), Math.Clamp(y, 0, GridHeight - 1));
    }

    #region motion

    /// <summary>How a plate moves at a point (no-net-rotation frame): speed in cm a year and the direction, ° from north.</summary>
    public static (double Speed, double Azimuth) Velocity(Plate plate, double latitude, double longitude)
    {
        var (east, north) = VelocityEN(plate, latitude, longitude);
        return (Math.Sqrt((east * east) + (north * north)), Normalize360(Math.Atan2(east, north) * 180 / Math.PI));
    }

    /// <summary>East and north speed, cm a year.</summary>
    public static (double East, double North) VelocityEN(Plate plate, double latitude, double longitude)
    {
        // v = ω × r, with ω along the pole at the plate's rate.
        var w = plate.Rate * Math.PI / 180 / 1e6;
        var pole = Unit(plate.PoleLatitude, plate.PoleLongitude);
        var r = Unit(latitude, longitude);
        var wx = w * pole.X;
        var wy = w * pole.Y;
        var wz = w * pole.Z;
        var rx = EarthRadius * 1e5 * r.X;
        var ry = EarthRadius * 1e5 * r.Y;
        var rz = EarthRadius * 1e5 * r.Z;
        var vx = (wy * rz) - (wz * ry);
        var vy = (wz * rx) - (wx * rz);
        var vz = (wx * ry) - (wy * rx);

        var lat = latitude * Math.PI / 180;
        var lon = longitude * Math.PI / 180;
        var east = (-Math.Sin(lon) * vx) + (Math.Cos(lon) * vy);
        var north = (-Math.Sin(lat) * Math.Cos(lon) * vx) - (Math.Sin(lat) * Math.Sin(lon) * vy) + (Math.Cos(lat) * vz);
        return (east, north);
    }

    /// <summary>How fast two plates move against each other at a point, cm a year.</summary>
    public static double RelativeSpeed(Plate a, Plate b, double latitude, double longitude)
    {
        var va = VelocityEN(a, latitude, longitude);
        var vb = VelocityEN(b, latitude, longitude);
        return Math.Sqrt(Math.Pow(va.East - vb.East, 2) + Math.Pow(va.North - vb.North, 2));
    }

    /// <summary>
    /// The relative motion at a point of a boundary, split into closing (+, plates come together; −, they part) and
    /// sliding along the line, cm a year.
    /// </summary>
    public static (double Closing, double Sliding) RelativeMotion(PlateBoundary boundary, int segment, double t)
    {
        var p = boundary.Points[segment];
        var q = boundary.Points[segment + 1];
        var at = Interpolate(p, q, t);
        var a = Find(boundary.A);
        var b = Find(boundary.B);
        var va = VelocityEN(a, at.Latitude, at.Longitude);
        var vb = VelocityEN(b, at.Latitude, at.Longitude);
        var dx = va.East - vb.East;
        var dy = va.North - vb.North;

        // The line's direction in local east/north, and the normal pointing from A's side to B's side.
        var (ex, ey) = Direction(p, q);
        var (nx, ny) = (ey, -ex);
        if (SideOf(boundary, segment, t, nx, ny) is var side && side < 0)
        {
            nx = -nx;
            ny = -ny;
        }

        return ((dx * nx) + (dy * ny), Math.Abs((dx * ex) + (dy * ey)));
    }

    /// <summary>
    /// The direction across a boundary segment towards plate B (for a trench, the upper plate), in local east/north.
    /// </summary>
    public static (double East, double North) TowardsB(PlateBoundary boundary, int segment)
    {
        var (ex, ey) = Direction(boundary.Points[segment], boundary.Points[segment + 1]);
        var (nx, ny) = (ey, -ex);
        return SideOf(boundary, segment, 0.5, nx, ny) < 0 ? (-nx, -ny) : (nx, ny);
    }

    /// <summary>+1 if the normal (nx, ny) points into plate B's side, −1 if into A's, 0 if neither could be told.</summary>
    private static int SideOf(PlateBoundary boundary, int segment, double t, double nx, double ny)
    {
        var at = Interpolate(boundary.Points[segment], boundary.Points[segment + 1], t);
        var b = IndexOf(boundary.B);
        var a = IndexOf(boundary.A);
        foreach (var km in new[] { 80.0, 150, 250 })
        {
            var ahead = Offset(at, nx, ny, km);
            var behind = Offset(at, -nx, -ny, km);
            var plateAhead = PlateIndexAt(ahead);
            var plateBehind = PlateIndexAt(behind);
            if (plateAhead == b || plateBehind == a) return 1;
            if (plateAhead == a || plateBehind == b) return -1;
        }

        return 0;
    }

    private static int PlateIndexAt(GeoPoint point)
    {
        var (x, y) = Cell(point.Latitude, point.Longitude);
        return PlateGrid[(y * GridWidth) + x];
    }

    /// <summary>The unit direction from p to q in local east/north at p.</summary>
    internal static (double East, double North) Direction(GeoPoint p, GeoPoint q)
    {
        var dLon = Normalize(q.Longitude - p.Longitude) * Math.Cos(p.Latitude * Math.PI / 180);
        var dLat = q.Latitude - p.Latitude;
        var length = Math.Sqrt((dLon * dLon) + (dLat * dLat));
        return length < 1e-9 ? (1, 0) : (dLon / length, dLat / length);
    }

    /// <summary>The point <paramref name="km"/> away from <paramref name="from"/> towards local east/north (dx, dy) (small distances).</summary>
    internal static GeoPoint Offset(GeoPoint from, double dx, double dy, double km)
    {
        var lat = from.Latitude + (dy * km / KmPerDegree);
        var cos = Math.Max(0.05, Math.Cos(from.Latitude * Math.PI / 180));
        var lon = Normalize(from.Longitude + (dx * km / KmPerDegree / cos));
        return new GeoPoint(Math.Clamp(lat, -89.9, 89.9), lon);
    }

    #endregion

    #region distances

    /// <summary>Great-circle distance between two points, km.</summary>
    public static double Distance(GeoPoint a, GeoPoint b) => ArcDegrees(a, b) * KmPerDegree;

    /// <summary>Angle between two points seen from Earth's centre, °.</summary>
    public static double ArcDegrees(GeoPoint a, GeoPoint b)
    {
        var la = a.Latitude * Math.PI / 180;
        var lb = b.Latitude * Math.PI / 180;
        var dLat = lb - la;
        var dLon = (b.Longitude - a.Longitude) * Math.PI / 180;
        var h = Math.Pow(Math.Sin(dLat / 2), 2) + (Math.Cos(la) * Math.Cos(lb) * Math.Pow(Math.Sin(dLon / 2), 2));
        return 2 * Math.Asin(Math.Min(1, Math.Sqrt(h))) * 180 / Math.PI;
    }

    /// <summary>The direction from <paramref name="a"/> to <paramref name="b"/> along the great circle, ° from north.</summary>
    public static double Bearing(GeoPoint a, GeoPoint b)
    {
        var la = a.Latitude * Math.PI / 180;
        var lb = b.Latitude * Math.PI / 180;
        var dLon = (b.Longitude - a.Longitude) * Math.PI / 180;
        var y = Math.Sin(dLon) * Math.Cos(lb);
        var x = (Math.Cos(la) * Math.Sin(lb)) - (Math.Sin(la) * Math.Cos(lb) * Math.Cos(dLon));
        return Normalize360(Math.Atan2(y, x) * 180 / Math.PI);
    }

    /// <summary>The nearest point of any plate boundary (rifts not counted): which boundary, which segment, and how far, km.</summary>
    public static (int Boundary, int Segment, double T, double Km) Nearest(GeoPoint point, bool includeRifts = false)
    {
        var best = (Boundary: -1, Segment: 0, T: 0.0, Km: double.MaxValue);
        for (var i = 0; i < Boundaries.Count; i++)
        {
            var boundary = Boundaries[i];
            if (boundary.Kind == BoundaryKind.Rift && !includeRifts) continue;
            for (var s = 0; s + 1 < boundary.Points.Length; s++)
            {
                var p = boundary.Points[s];
                var q = boundary.Points[s + 1];

                // Sampled along the segment; segments are short (a few degrees), so a dozen samples are within ~20 km.
                var steps = Math.Max(4, (int)(ArcDegrees(p, q) * 4));
                for (var k = 0; k <= steps; k++)
                {
                    var t = (double)k / steps;
                    var km = Distance(point, Interpolate(p, q, t));
                    if (km < best.Km) best = (i, s, t, km);
                }
            }
        }

        return best;
    }

    /// <summary>A point between two others, longitude taken the short way round.</summary>
    public static GeoPoint Interpolate(GeoPoint p, GeoPoint q, double t)
    {
        var dLon = Normalize(q.Longitude - p.Longitude);
        return new GeoPoint(p.Latitude + ((q.Latitude - p.Latitude) * t), Normalize(p.Longitude + (dLon * t)));
    }

    #endregion

    #region the raster

    private static byte[] BuildGrid()
    {
        const byte Unset = 255;
        const byte Wall = 254;
        var grid = new byte[GridWidth * GridHeight];
        Array.Fill(grid, Unset);

        // The boundaries as walls: 8-connected lines stop a 4-connected fill.
        foreach (var boundary in Boundaries)
        {
            if (boundary.Kind == BoundaryKind.Rift) continue;
            for (var s = 0; s + 1 < boundary.Points.Length; s++) DrawWall(grid, boundary.Points[s], boundary.Points[s + 1], Wall);
        }

        // Each plate floods out from its seeds.
        var queue = new Queue<int>();
        for (var i = 0; i < Plates.Count; i++)
        {
            foreach (var seed in Seeds[Plates[i].Id])
            {
                var (x, y) = Cell(seed.Latitude, seed.Longitude);
                var cell = (y * GridWidth) + x;
                if (grid[cell] != Unset) continue;
                grid[cell] = (byte)i;
                queue.Enqueue(cell);
            }
        }

        Flood(grid, queue, c => grid[c] == Unset);

        // The walls (and anything cut off) take the plate of a neighbour.
        for (var pass = 0; pass < 8; pass++)
        {
            var changed = false;
            for (var cell = 0; cell < grid.Length; cell++)
            {
                if (grid[cell] < Wall) continue;
                foreach (var n in Neighbours(cell))
                {
                    if (grid[n] < Wall)
                    {
                        queue.Enqueue(cell);
                        break;
                    }
                }
            }

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var n in Neighbours(cell))
                {
                    if (grid[n] < Wall)
                    {
                        grid[cell] = grid[n];
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed) break;
        }

        for (var cell = 0; cell < grid.Length; cell++)
        {
            if (grid[cell] >= Wall) grid[cell] = 0;
        }

        return grid;
    }

    private static void Flood(byte[] grid, Queue<int> queue, Func<int, bool> open)
    {
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            var plate = grid[cell];
            foreach (var n in Neighbours(cell))
            {
                if (!open(n)) continue;
                grid[n] = plate;
                queue.Enqueue(n);
            }
        }
    }

    /// <summary>The four cells beside a cell; longitude wraps, the poles do not.</summary>
    private static IEnumerable<int> Neighbours(int cell)
    {
        var x = cell % GridWidth;
        var y = cell / GridWidth;
        yield return (y * GridWidth) + ((x + 1) % GridWidth);
        yield return (y * GridWidth) + ((x + GridWidth - 1) % GridWidth);
        if (y > 0) yield return cell - GridWidth;
        if (y < GridHeight - 1) yield return cell + GridWidth;
    }

    private static void DrawWall(byte[] grid, GeoPoint p, GeoPoint q, byte wall)
    {
        var x0 = (p.Longitude + 180) / 360 * GridWidth;
        var y0 = (90 - p.Latitude) / 180 * GridHeight;
        var x1 = x0 + (Normalize(q.Longitude - p.Longitude) / 360 * GridWidth);
        var y1 = (90 - q.Latitude) / 180 * GridHeight;

        // Bresenham-like walk with a step of under a cell, every cell touched marked: no diagonal gaps.
        var steps = (int)Math.Ceiling(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) * 2) + 1;
        int? lastX = null;
        int? lastY = null;
        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            var cx = (int)Math.Floor(x0 + ((x1 - x0) * t));
            var cy = Math.Clamp((int)Math.Floor(y0 + ((y1 - y0) * t)), 0, GridHeight - 1);
            cx = ((cx % GridWidth) + GridWidth) % GridWidth;
            if (lastX is { } lx && lastY is { } ly && lx != cx && ly != cy)
            {
                // A diagonal step: fill the corner so a 4-connected flood cannot squeeze through.
                grid[(ly * GridWidth) + cx] = wall;
            }

            grid[(cy * GridWidth) + cx] = wall;
            lastX = cx;
            lastY = cy;
        }
    }

    private static readonly Lazy<float[]> RidgeKm = new(BuildRidgeDistance);

    /// <summary>
    /// For each 0.5° cell, how far it is to the nearest ridge, km (shortest path over the grid). The map uses it to
    /// shade the sea deeper away from the ridges, as the floor sinks with age.
    /// </summary>
    public static float[] RidgeDistance => RidgeKm.Value;

    private static float[] BuildRidgeDistance()
    {
        var distance = new float[GridWidth * GridHeight];
        Array.Fill(distance, float.MaxValue);
        var queue = new PriorityQueue<int, float>();
        foreach (var boundary in Boundaries)
        {
            if (boundary.Kind != BoundaryKind.Ridge) continue;
            for (var s = 0; s + 1 < boundary.Points.Length; s++)
            {
                var steps = Math.Max(2, (int)(ArcDegrees(boundary.Points[s], boundary.Points[s + 1]) * 4));
                for (var k = 0; k <= steps; k++)
                {
                    var at = Interpolate(boundary.Points[s], boundary.Points[s + 1], (double)k / steps);
                    var (x, y) = Cell(at.Latitude, at.Longitude);
                    var cell = (y * GridWidth) + x;
                    distance[cell] = 0;
                    queue.Enqueue(cell, 0);
                }
            }
        }

        var cellKm = KmPerDegree * 180.0 / GridHeight;
        while (queue.TryDequeue(out var cell, out var d))
        {
            if (d > distance[cell]) continue;
            var x = cell % GridWidth;
            var y = cell / GridWidth;
            var cos = Math.Max(0.05, Math.Cos((90 - ((y + 0.5) * 180.0 / GridHeight)) * Math.PI / 180));
            for (var dy = -1; dy <= 1; dy++)
            {
                var ny = y + dy;
                if (ny < 0 || ny >= GridHeight) continue;
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var nx = (x + dx + GridWidth) % GridWidth;
                    var step = (float)(cellKm * Math.Sqrt((dx * dx * cos * cos) + (dy * dy)));
                    var n = (ny * GridWidth) + nx;
                    if (d + step >= distance[n]) continue;
                    distance[n] = d + step;
                    queue.Enqueue(n, d + step);
                }
            }
        }

        return distance;
    }

    /// <summary>The distance to the nearest ridge at a point, km (on the 0.5° grid).</summary>
    public static double RidgeDistanceAt(double latitude, double longitude)
    {
        var (x, y) = Cell(latitude, longitude);
        return RidgeDistance[(y * GridWidth) + x];
    }

    #endregion

    #region earthquakes, volcanoes

    /// <summary>
    /// How many earthquakes of at least each magnitude happen in a year worldwide (USGS estimates, rounded): about one of
    /// 8 or more, 15 of 7–7.9, 134 of 6–6.9, 1,300 of 5–5.9 and so on, tenfold per step down.
    /// </summary>
    public static IReadOnlyList<(double Magnitude, double PerYear)> PerYear { get; } =
    [
        (2, 1_300_000),
        (3, 130_000),
        (4, 13_000),
        (5, 1_319),
        (6, 134),
        (7, 15),
        (8, 1),
    ];

    /// <summary>The energy an earthquake of a magnitude sends out as waves, J (log₁₀E = 1.5M + 4.8).</summary>
    public static double Energy(double magnitude) => Math.Pow(10, (1.5 * magnitude) + 4.8);

    /// <summary>
    /// Makes <paramref name="count"/> earthquakes of magnitude 4.5 and up along the boundaries: trenches get the most,
    /// then collisions and transforms, ridges few and small. Behind a trench they go deeper the farther they are from it
    /// (down the sinking plate); a few fall inside continents where India pushes into Asia.
    /// </summary>
    public static IReadOnlyList<MapQuake> MakeQuakes(int count, int seed)
    {
        var random = new Random(seed);
        var pieces = new List<(int Boundary, int Segment, double Weight)>();
        for (var i = 0; i < Boundaries.Count; i++)
        {
            var boundary = Boundaries[i];
            var weight = boundary.Kind switch
            {
                BoundaryKind.Trench => 6.0,
                BoundaryKind.Collision => 3.5,
                BoundaryKind.Transform => 2.5,
                BoundaryKind.Ridge => 1.0,
                BoundaryKind.Diffuse => 0.5,
                _ => 0.6,
            };
            for (var s = 0; s + 1 < boundary.Points.Length; s++)
            {
                pieces.Add((i, s, weight * ArcDegrees(boundary.Points[s], boundary.Points[s + 1])));
            }
        }

        var total = pieces.Sum(p => p.Weight);
        var quakes = new List<MapQuake>(count);
        var inland = (int)(count * 0.07);
        for (var n = 0; n < count - inland; n++)
        {
            var pick = random.NextDouble() * total;
            var piece = pieces[^1];
            foreach (var p in pieces)
            {
                pick -= p.Weight;
                if (pick > 0) continue;
                piece = p;
                break;
            }

            var boundary = Boundaries[piece.Boundary];
            var t = random.NextDouble();
            var at = Interpolate(boundary.Points[piece.Segment], boundary.Points[piece.Segment + 1], t);
            var magnitude = Magnitude(random);
            double depth;
            var spread = (random.NextDouble() - 0.5) * 2;
            switch (boundary.Kind)
            {
                case BoundaryKind.Trench:
                {
                    // Depth: most near the top of the sinking plate, fewer the deeper, none below the trench's limit.
                    var u = random.NextDouble();
                    depth = u < 0.62 ? 5 + (random.NextDouble() * 55)
                        : u < 0.9 ? 60 + (random.NextDouble() * 240)
                        : 300 + (random.NextDouble() * 390);
                    depth = Math.Min(depth, boundary.MaxDepth * (0.4 + (0.6 * random.NextDouble())));
                    depth = Math.Max(depth, 4);

                    // Down the sinking plate: farther from the trench, on the upper plate's side.
                    var behind = (depth / Math.Tan(boundary.Dip * Math.PI / 180)) + 20 + (spread * 25);
                    at = Behind(boundary, piece.Segment, t, behind);
                    break;
                }

                case BoundaryKind.Collision:
                    depth = 5 + (Math.Pow(random.NextDouble(), 2) * 70);
                    at = Behind(boundary, piece.Segment, t, (40 + (random.NextDouble() * 260)) * Math.Sign(spread + 0.6));
                    break;

                case BoundaryKind.Diffuse:
                    depth = 5 + (random.NextDouble() * 25);
                    at = Behind(boundary, piece.Segment, t, spread * 300);
                    break;

                default:
                    depth = 3 + (random.NextDouble() * 15);
                    at = Behind(boundary, piece.Segment, t, spread * 18);
                    break;
            }

            quakes.Add(new MapQuake(at.Latitude, at.Longitude, depth, magnitude, piece.Boundary));
        }

        // Inside Asia, where India's push is felt far from any boundary: Tibet, the Tian Shan, north China, Iran.
        (double Lat0, double Lat1, double Lon0, double Lon1, double Weight)[] zones =
        [
            (27, 37, 80, 104, 5),
            (39, 45, 72, 90, 2),
            (34, 41, 105, 121, 1.5),
            (27, 38, 46, 62, 2.5),
            (44, 51, 88, 104, 1),
            (37, 41, 26, 44, 1.5),
        ];
        var zoneTotal = zones.Sum(z => z.Weight);
        for (var n = 0; n < inland; n++)
        {
            var pick = random.NextDouble() * zoneTotal;
            var zone = zones[^1];
            foreach (var z in zones)
            {
                pick -= z.Weight;
                if (pick > 0) continue;
                zone = z;
                break;
            }

            var lat = zone.Lat0 + (random.NextDouble() * (zone.Lat1 - zone.Lat0));
            var lon = zone.Lon0 + (random.NextDouble() * (zone.Lon1 - zone.Lon0));
            quakes.Add(new MapQuake(lat, lon, 5 + (random.NextDouble() * 25), Magnitude(random), -1));
        }

        return quakes;
    }

    /// <summary>A magnitude from 4.5 up, with ten times fewer for each step up (Gutenberg–Richter, b = 1).</summary>
    private static double Magnitude(Random random) => Math.Min(9.2, 4.5 - Math.Log10(1 - random.NextDouble()));

    /// <summary>
    /// A point <paramref name="km"/> from a boundary across it: for a trench or a collision onto the upper plate's side
    /// (B), otherwise to the side the sign says.
    /// </summary>
    internal static GeoPoint Behind(PlateBoundary boundary, int segment, double t, double km)
    {
        var p = boundary.Points[segment];
        var q = boundary.Points[segment + 1];
        var at = Interpolate(p, q, t);
        var (ex, ey) = Direction(p, q);
        var (nx, ny) = (ey, -ex);
        if (boundary.Kind is BoundaryKind.Trench or BoundaryKind.Collision && SideOf(boundary, segment, t, nx, ny) < 0)
        {
            nx = -nx;
            ny = -ny;
        }

        return Offset(at, nx, ny, km);
    }

    /// <summary>
    /// Volcanoes: arcs behind the trenches whose sinking plate reaches about 100 km down (where it gives off water and the
    /// mantle above melts), about one every 300 km; the ridge volcanoes of Iceland and the Afar; and the hotspots.
    /// </summary>
    public static IReadOnlyList<MapVolcano> Volcanoes => LazyVolcanoes.Value;

    private static readonly Lazy<IReadOnlyList<MapVolcano>> LazyVolcanoes = new(MakeVolcanoes);

    private static IReadOnlyList<MapVolcano> MakeVolcanoes()
    {
        var list = new List<MapVolcano>();
        foreach (var boundary in Boundaries)
        {
            if (boundary.Kind != BoundaryKind.Trench || boundary.MaxDepth < 110) continue;
            var behind = 100 / Math.Tan(boundary.Dip * Math.PI / 180);
            var carried = 150.0;
            for (var s = 0; s + 1 < boundary.Points.Length; s++)
            {
                var length = Distance(boundary.Points[s], boundary.Points[s + 1]);
                var position = carried;
                while (position < length)
                {
                    var at = Behind(boundary, s, position / length, behind);
                    list.Add(new MapVolcano(at.Latitude, at.Longitude, false));
                    position += 300;
                }

                carried = position - length;
            }
        }

        // Named ones beside the arcs: Iceland and Afar sit on ridges, Kilimanjaro on the rift, Changbai Shan in Asia.
        list.Add(new MapVolcano(64.6, -17.5, false));
        list.Add(new MapVolcano(13.6, 40.7, false));
        list.Add(new MapVolcano(-3.07, 37.35, false));
        list.Add(new MapVolcano(42.0, 128.06, false));
        list.Add(new MapVolcano(37.75, 15.0, false));
        foreach (var hotspot in Hotspots) list.Add(new MapVolcano(hotspot.Latitude, hotspot.Longitude, true));
        return list;
    }

    /// <summary>Hotspots: plumes from deep in the mantle, far from boundaries (Hawaii, Yellowstone, Réunion, Galápagos).</summary>
    public static IReadOnlyList<GeoPoint> Hotspots { get; } =
    [
        new(19.4, -155.3),
        new(44.4, -110.7),
        new(-21.2, 55.7),
        new(-0.4, -91.5),
    ];

    /// <summary>
    /// The Hawaiian–Emperor chain: volcanoes the Pacific plate carried away from the Hawaii hotspot, with their ages in
    /// millions of years (rounded). The bend at about 47 million years is where the plate turned.
    /// </summary>
    public static IReadOnlyList<(GeoPoint Point, double Age)> HawaiiChain { get; } =
    [
        (new(19.4, -155.3), 0),
        (new(20.8, -156.3), 1.3),
        (new(21.5, -158.0), 3),
        (new(22.1, -159.5), 5),
        (new(23.6, -164.7), 10),
        (new(25.0, -168.0), 15),
        (new(28.2, -177.4), 28),
        (new(30.0, 178.0), 38),
        (new(32.0, 172.3), 47),
        (new(36.5, 171.0), 55),
        (new(44.5, 170.3), 65),
        (new(51.0, 168.0), 76),
        (new(53.0, 164.5), 82),
    ];

    /// <summary>Earthquakes the page can point at.</summary>
    public static IReadOnlyList<FamousQuake> Famous { get; } =
    [
        new("Chile1960", 1960, 9.5, -38.2, -73.0, 25, BoundaryKind.Trench),
        new("Alaska1964", 1964, 9.2, 61.0, -147.7, 25, BoundaryKind.Trench),
        new("Sumatra2004", 2004, 9.1, 3.3, 95.9, 30, BoundaryKind.Trench),
        new("Tohoku2011", 2011, 9.1, 38.3, 142.4, 29, BoundaryKind.Trench),
        new("Bolivia1994", 1994, 8.2, -13.8, -67.6, 631, BoundaryKind.Trench),
        new("SanFrancisco1906", 1906, 7.9, 37.75, -122.55, 8, BoundaryKind.Transform),
        new("Wenchuan2008", 2008, 7.9, 31.0, 103.4, 19, BoundaryKind.Collision),
        new("Nepal2015", 2015, 7.8, 28.2, 84.7, 15, BoundaryKind.Collision),
        new("Turkey2023", 2023, 7.8, 37.2, 37.0, 10, BoundaryKind.Transform),
        new("Tangshan1976", 1976, 7.6, 39.6, 118.2, 12, BoundaryKind.Diffuse),
    ];

    #endregion

    #region the boundaries

    private static IReadOnlyList<PlateBoundary> BuildBoundaries()
    {
        var list = new List<PlateBoundary>();

        void Add(string id, BoundaryKind kind, string a, string b, string points, double maxDepth = 25, double dip = 45) =>
            list.Add(new PlateBoundary(id, kind, a, b, Parse(points), kind == BoundaryKind.Trench ? maxDepth : Math.Min(maxDepth, 40), dip));

        // The Atlantic: the Mid-Atlantic Ridge from the Arctic to the Bouvet triple junction, and on through Siberia to Japan.
        Add("MidAtlanticNorth", BoundaryKind.Ridge, "NA", "EU",
            "39 -29, 42 -29.5, 45 -28, 48 -27.8, 52 -30, 52.5 -34, 55 -35, 58 -32, 61 -27, 63.5 -22, 64.5 -18, 66.5 -18.5, 68 -17, 71 -8, 72.5 2, 75 8, 78.5 7, 80.5 3, 83 -5, 84.5 10, 86.5 60, 85.5 100, 81 121, 78 126, 73 129");
        Add("Chersky", BoundaryKind.Diffuse, "NA", "EU",
            "73 129, 70 136, 66 143, 62 150, 57 146, 52 143, 48 142.5, 45 141.7, 42 140, 39 139, 37 138, 35 139");
        Add("MidAtlanticCentral", BoundaryKind.Ridge, "NA", "AF",
            "39 -29, 36 -34, 33 -38.5, 30 -42, 26 -45, 23 -45.5, 20 -46, 15 -46.5");
        Add("MidAtlanticSouth", BoundaryKind.Ridge, "SA", "AF",
            "15 -46.5, 12 -44, 8 -38.5, 5 -33, 1 -28, 0 -20, -1 -15, -5 -12, -10 -13, -15 -13.5, -20 -12, -25 -13.5, -30 -13.5, -35 -16.5, -40 -16.5, -45 -14, -50 -8, -54.5 -1");
        Add("SouthwestIndian", BoundaryKind.Ridge, "AF", "AN",
            "-54.5 -1, -54 5, -52.5 15, -50 25, -46.5 33, -43 40, -39 47, -35 53, -31 59, -28 64, -25.5 70");
        Add("SoutheastIndian", BoundaryKind.Ridge, "AU", "AN",
            "-25.5 70, -30 75.5, -36 78, -42 83, -46 92, -48 100, -50 110, -50 120, -50.5 130, -54 140, -58 150, -61.5 161");
        Add("CentralIndianSouth", BoundaryKind.Ridge, "AU", "AF", "-25.5 70, -20 66.5, -15 66.5, -12 66");
        Add("CentralIndianNorth", BoundaryKind.Ridge, "IN", "AF", "-12 66, -6 68, -1 67.5, 3 64.5, 6 61, 10 57.5, 14.5 56.5");
        Add("GulfOfAden", BoundaryKind.Ridge, "AR", "AF", "14.5 56.5, 13.5 52, 12.5 48, 12 45, 12 43.5");
        Add("RedSea", BoundaryKind.Ridge, "AR", "AF", "12 43.5, 15 41.5, 18 39.8, 21 38.2, 24 36.5, 27 34.8, 28 34.6");
        Add("DeadSea", BoundaryKind.Transform, "AR", "AF", "28 34.6, 29.5 35, 31.5 35.5, 33.5 35.8, 36 36.2");

        // Africa against Europe, Arabia and India against Asia.
        Add("AzoresGibraltar", BoundaryKind.Transform, "AF", "EU", "39 -29, 37.5 -20, 36.5 -12, 36 -6.5");
        Add("NorthAfrica", BoundaryKind.Collision, "AF", "EU", "36 -6.5, 36 -2, 36.8 3, 37.3 9, 37.5 12.5, 38 15.5");
        Add("Hellenic", BoundaryKind.Trench, "AF", "EU", "38 15.5, 37.5 18, 37 20.5, 35.5 22.5, 34.5 25, 35 27.5, 36 29, 34.8 32, 36 36.2", 180, 35);
        Add("EastAnatolian", BoundaryKind.Transform, "AR", "EU", "36 36.2, 37.5 38, 38.3 39.5");
        Add("Zagros", BoundaryKind.Collision, "AR", "EU", "38.3 39.5, 38 43, 36.5 45, 34 46.5, 31.5 48.5, 29.5 50.5, 27.5 53, 26.8 56");
        Add("Makran", BoundaryKind.Trench, "AR", "EU", "26.8 56, 25.3 58, 25 61, 25 63", 90, 15);
        Add("Owen", BoundaryKind.Transform, "IN", "AR", "14.5 56.5, 17 57.5, 20 59.5, 23 61.5, 25 63");
        Add("Chaman", BoundaryKind.Transform, "IN", "EU", "25 63, 25.5 66, 28 66.5, 31 67, 34 69, 36 71");
        Add("Himalaya", BoundaryKind.Collision, "IN", "EU", "36 71, 35 74, 33 76, 31 78.5, 29.5 81, 28.3 84, 27.3 88, 27 91.5, 27.7 95, 28 96.5");
        Add("Burma", BoundaryKind.Trench, "IN", "EU", "28 96.5, 25 94.5, 22 93.3, 19 93.6, 16 94, 13 92.5, 10 92.3, 7 93, 4.5 94, 2 95.8, 0 96.8", 160, 45);
        Add("Sunda", BoundaryKind.Trench, "AU", "EU", "0 96.8, -2.5 99, -5 101.5, -7.5 104.5, -9.5 108, -10.5 112, -11 116, -11 119.5, -10.3 122, -10 124, -9 127, -7.5 130, -5.5 132, -3 132.5, 0 133", 650, 50);
        Add("IndiaAustralia", BoundaryKind.Diffuse, "IN", "AU", "-12 66, -8 73, -5 80, -3 87, -1.5 92, 0 96.8");

        // The western Pacific: the Philippine Sea plate, Japan, the Kurils and the Aleutians.
        Add("NankaiRyukyu", BoundaryKind.Trench, "PH", "EU", "35 139, 34.2 137.5, 33.2 135.5, 32.3 133.5, 31 132, 29.5 131, 28 129.8, 26.5 128.5, 25 126.5, 24 124, 23.5 122.3", 280, 45);
        Add("Manila", BoundaryKind.Trench, "EU", "PH", "23.5 122.3, 22 120.2, 20 119.6, 17.5 119.4, 15 119.6, 13.5 120.2", 220, 45);
        Add("PhilippineFault", BoundaryKind.Transform, "EU", "PH", "13.5 120.2, 13.5 124.5");
        Add("Philippine", BoundaryKind.Trench, "PH", "EU", "13.5 124.5, 12 126, 9.5 126.5, 7 127, 4.5 127.5, 2 128.3, 0 133", 200, 50);
        Add("IzuBoninMariana", BoundaryKind.Trench, "PA", "PH", "34 142, 32 141.8, 29 142.5, 26 143, 24 143.3, 21.5 146.3, 20 147.5, 17 147.7, 14 147, 12 145, 11.3 142.5, 10 139.5, 8 137.5, 7 135.5, 4 135, 2 134.5, 0 133", 680, 70);
        Add("Sagami", BoundaryKind.Trench, "PH", "NA", "35 139, 34.6 140.5, 34 142", 120, 30);
        Add("JapanKurilAleutian", BoundaryKind.Trench, "PA", "NA",
            "34 142, 36 142.5, 38 143.8, 40 144.3, 41.5 144.6, 42.5 146, 44 148.5, 46 152, 48 154.5, 50 157, 52 159.5, 54 162.2, 55.3 164, 54.5 167, 53.5 171, 52.5 174, 51.4 178, 51 -178, 51 -174, 51.5 -170, 52.5 -167, 53.5 -163, 54.5 -159, 56 -155.5, 57.5 -152.5, 59 -149, 59.7 -146, 59.8 -143",
            650, 40);

        // North America's west coast.
        Add("QueenCharlotte", BoundaryKind.Transform, "PA", "NA", "59.8 -143, 59 -139, 57.5 -136.5, 55 -134, 53 -132.5, 51.5 -131, 50.8 -130.3");
        Add("Cascadia", BoundaryKind.Trench, "JF", "NA", "50.8 -130.3, 49.5 -127.8, 48 -126, 46 -124.9, 44 -124.9, 42 -124.8, 40.4 -124.9", 80, 15);
        Add("JuanDeFuca", BoundaryKind.Ridge, "PA", "JF", "50.8 -130.3, 49 -129, 47 -129.2, 45 -130, 44 -129.5, 43.5 -128, 43 -127, 41 -127.3, 40.4 -127, 40.4 -124.9");
        Add("SanAndreas", BoundaryKind.Transform, "PA", "NA", "40.4 -124.9, 39 -123.8, 37.8 -122.5, 36.5 -121.3, 35.3 -119.7, 34.6 -118.5, 34 -117, 33.2 -115.8, 32 -115.2, 31 -114.3");
        Add("GulfOfCalifornia", BoundaryKind.Ridge, "PA", "NA", "31 -114.3, 29 -112.8, 27 -111, 25 -109.5, 23.5 -108.5, 22 -107.5, 19.5 -106");

        // Central America and the Caribbean.
        Add("MiddleAmerica", BoundaryKind.Trench, "CO", "NA", "19.5 -106, 18 -104, 16.8 -100.5, 15.8 -97.5, 15 -94.5, 14.2 -92.5", 200, 25);
        Add("CentralAmerica", BoundaryKind.Trench, "CO", "CA", "14.2 -92.5, 13 -90.5, 11.5 -88.5, 10 -86.5, 8.5 -84.2, 7.2 -82.6", 220, 45);
        Add("EastPacificNorth", BoundaryKind.Ridge, "PA", "CO", "19.5 -106, 16 -105.2, 12 -104.2, 8 -103.8, 4 -102.8, 2 -102");
        Add("Galapagos", BoundaryKind.Ridge, "CO", "NZ", "2 -102, 2.2 -97, 1.8 -91, 1.5 -86, 2.5 -83.5, 5 -82.7, 7.2 -82.6");
        Add("Panama", BoundaryKind.Transform, "NZ", "CA", "7.2 -82.6, 6.8 -79.5");
        Add("SouthCaribbean", BoundaryKind.Transform, "CA", "SA", "6.8 -79.5, 9 -77, 10.8 -75, 11.8 -72, 11 -68, 10.7 -64.5, 11 -61");
        Add("LesserAntillesSouth", BoundaryKind.Trench, "SA", "CA", "11 -61, 12.5 -59.7, 15 -59.6", 200, 50);
        Add("LesserAntillesNorth", BoundaryKind.Trench, "NA", "CA", "15 -59.6, 17 -60.3, 18.4 -62, 19.5 -64.5, 19.8 -67", 200, 50);
        Add("Cayman", BoundaryKind.Transform, "NA", "CA", "19.8 -67, 20 -70, 20 -73.5, 19.7 -76.5, 19 -80, 17.6 -84, 16.3 -87, 15.3 -89.5, 14.2 -92.5");
        Add("NorthSouthAmerica", BoundaryKind.Diffuse, "NA", "SA", "15 -46.5, 15.2 -52, 15 -59.6");

        // The south-east Pacific and South America.
        Add("EastPacificSouth", BoundaryKind.Ridge, "PA", "NZ", "2 -102, -2 -102.5, -6 -106.5, -10 -110.3, -15 -112.8, -20 -113.5, -25 -113, -30 -111.8, -35 -110.5");
        Add("PacificAntarctic", BoundaryKind.Ridge, "PA", "AN", "-35 -110.5, -40 -111.5, -45 -113, -50 -116, -55 -121, -57 -130, -60 -145, -63 -160, -65 -172, -65 177, -63 168, -61.5 161");
        Add("ChileRise", BoundaryKind.Ridge, "NZ", "AN", "-35 -110.5, -37 -105, -38 -97.5, -40.5 -92, -43 -85, -45 -80, -46.3 -75.8");
        Add("PeruChile", BoundaryKind.Trench, "NZ", "SA",
            "6.8 -79.5, 4 -78.4, 1.5 -79.6, -1 -81.2, -4 -81.8, -7 -81, -10 -79.5, -13 -77.5, -16 -74.8, -18 -71.8, -20.5 -71, -24 -71.2, -28 -71.8, -32 -72.4, -36 -73.6, -40 -74.6, -44 -75.6, -46.3 -75.8",
            660, 30);
        Add("SouthChile", BoundaryKind.Trench, "AN", "SA", "-46.3 -75.8, -49 -76.4, -52 -76, -54.5 -73, -56 -67", 80, 25);
        Add("NorthScotia", BoundaryKind.Transform, "SC", "SA", "-56 -67, -55 -64, -53.8 -58, -53.5 -50, -53.8 -42, -54.5 -35, -55.5 -29");
        Add("SouthSandwich", BoundaryKind.Trench, "SA", "SC", "-55.5 -29, -57 -26, -58.8 -25.3, -60.2 -26.5", 300, 55);
        Add("SouthScotia", BoundaryKind.Transform, "SC", "AN", "-60.2 -26.5, -60.6 -35, -61 -43, -61 -50, -60.5 -56, -59.5 -61, -58 -64.5, -56 -67");
        Add("AmericaAntarctic", BoundaryKind.Ridge, "SA", "AN", "-60.2 -26.5, -59 -20, -57 -12, -55.5 -5, -54.5 -1");

        // Australia's north and east: New Guinea, the Solomons and Vanuatu, Tonga, New Zealand, Macquarie.
        Add("NewGuinea", BoundaryKind.Collision, "AU", "PA", "0 133, -1.5 138.5, -2.5 141, -3.5 144, -4.5 146.5, -5.8 149");
        Add("SolomonVanuatu", BoundaryKind.Trench, "AU", "PA", "-5.8 149, -6.5 151.5, -6.5 154, -8 156, -9.5 159, -10.8 161.5, -11.5 164.5, -13 166, -16 166.5, -19 168, -21.5 169.8, -22.8 171.5", 640, 60);
        Add("Fiji", BoundaryKind.Transform, "AU", "PA", "-22.8 171.5, -21.5 175, -19 177.5, -16.5 179.5, -15.2 -176, -15 -173.5");
        Add("TongaKermadec", BoundaryKind.Trench, "PA", "AU",
            "-15 -173.5, -17 -172.8, -20 -173.2, -23 -174.5, -26 -175.5, -29 -176.8, -32 -177.5, -35 -178.5, -37.5 179.5, -39 178.8, -41 177.3, -42 174.5",
            690, 55);
        Add("Alpine", BoundaryKind.Transform, "AU", "PA", "-42 174.5, -42.5 172.3, -43.5 170, -44.5 168, -46 166.5");
        Add("Puysegur", BoundaryKind.Trench, "AU", "PA", "-46 166.5, -48 165.5, -50 164", 120, 45);
        Add("Macquarie", BoundaryKind.Transform, "AU", "PA", "-50 164, -52.5 160, -55 158.8, -58 158.5, -61.5 161");

        // Not plate boundaries yet: Africa beginning to split.
        Add("EastAfricanRift", BoundaryKind.Rift, "AF", "AF", "12 43.5, 9 40, 6.5 38, 4 36.5, 0 36, -3 36, -6 35.8, -9 34, -12 34.5, -15 35");
        Add("WesternRift", BoundaryKind.Rift, "AF", "AF", "2.5 31, 0 29.8, -3 29.2, -6 29.5, -8.5 31.5, -9 33.5");

        return list;
    }

    /// <summary>"lat lon, lat lon, …" to points.</summary>
    private static GeoPoint[] Parse(string text) => text
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(pair =>
        {
            var parts = pair.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return new GeoPoint(double.Parse(parts[0], CultureInfo.InvariantCulture), double.Parse(parts[1], CultureInfo.InvariantCulture));
        })
        .ToArray();

    #endregion

    private static (double X, double Y, double Z) Unit(double latitude, double longitude)
    {
        var lat = latitude * Math.PI / 180;
        var lon = longitude * Math.PI / 180;
        return (Math.Cos(lat) * Math.Cos(lon), Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat));
    }

    internal static double Normalize(double longitude)
    {
        longitude %= 360;
        if (longitude > 180) longitude -= 360;
        if (longitude <= -180) longitude += 360;
        return longitude;
    }

    internal static double Normalize360(double degrees)
    {
        degrees %= 360;
        return degrees < 0 ? degrees + 360 : degrees;
    }
}
