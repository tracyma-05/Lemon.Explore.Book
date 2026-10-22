using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.StarTrails;

/// <summary>
/// Camera mode: a long exposure of the night sky from the viewer's latitude, as a wide-angle photograph across the whole
/// picture. Every star is drawn out into an arc of its daily circle from the moment the shutter opened to now; the
/// stereographic lens keeps those circles round, so the arcs are drawn as true arcs. A hilly horizon with trees hides
/// what is below, and the celestial pole, compass points, named stars and constellations are marked.
/// </summary>
public sealed partial class TrailsDiagram
{
    /// <summary>The camera's horizontal field of view, degrees (a wide-angle lens).</summary>
    private const double CameraFov = 120;

    private IReadOnlyList<SkyStar>? _fill;

    private IReadOnlyList<SkyStar> FillStars => _fill ??= StarCatalog.Fill();

    /// <summary>The camera's centre and scale this frame (pixels per stereographic unit).</summary>
    private Point _cameraCenter;

    private double _cameraScale;
    private Horizontal _view;

    /// <summary>A direction in the sky on the picture, or null when it is behind the camera.</summary>
    private Point? Screen(Horizontal p)
    {
        var projected = SkyModel.Stereographic(p, _view);
        if (!projected.Visible) return null;
        return new Point(_cameraCenter.X + (projected.X * _cameraScale), _cameraCenter.Y - (projected.Y * _cameraScale));
    }

    /// <summary>The sidereal time when the shutter opened, and now.</summary>
    private (double Start, double Now) CameraTimes() =>
        (SkyModel.SiderealTime(Day, StartHour), SkyModel.SiderealTime(Day, StartHour + _exposure));

    private void DrawCameraMode(PaintContext dc, double width, double height)
    {
        dc.DrawRectangle(Ink.NightSky, null, new Rect(0, 0, width, height));
        var stage = Stage();
        _view = new Horizontal(Math.Clamp(ViewAltitude, 0, 90), SkyModel.Wrap360(ViewAzimuth));
        _cameraScale = width / 2 / SkyModel.HalfWidth(CameraFov);
        _cameraCenter = new Point(width / 2, (stage.Top + stage.Bottom) / 2);

        var (lst0, lst) = CameraTimes();
        DrawTrails(dc, lst0, lst);
        if (ShowFigures) DrawFigures(dc, lst);

        DrawGround(dc, width, height);
        DrawPoleMark(dc, lst0, lst);
        DrawCompass(dc, width, height);
        if (ShowLabels) DrawStarNames(dc, lst, width, height);
        DrawShutter(dc, stage, width);
    }

    #region trails

    /// <summary>
    /// The picture's "film": the trails drawn into a bitmap and added to frame by frame, the way a stack of short exposures
    /// builds up. Thousands of long arcs redrawn every frame are too slow for the render thread.
    /// </summary>
    private RenderTargetBitmap? _film;

    /// <summary>What the film was exposed for: its size, the camera, the place and the night.</summary>
    private (int Width, int Height, double Azimuth, double Altitude, double Latitude, double Day, double Start) _filmKey;

    /// <summary>The exposure the film holds so far, hours.</summary>
    private double _filmExposure = -1;

    private readonly Dictionary<(int Colour, int Width, int Alpha, bool Dot), IPen> _trailPens = [];

    /// <summary>
    /// The trails from the shutter opening to now: the film, brought up to date (from scratch when the camera, the place or
    /// the night changed, or the exposure went back), then the brightest stars glowing where they are now.
    /// </summary>
    private void DrawTrails(PaintContext dc, double lst0, double lst)
    {
        var dpi = RenderScale();
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(ActualWidth * dpi));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(ActualHeight * dpi));
        var key = (pixelWidth, pixelHeight, Math.Round(_view.Azimuth, 2), Math.Round(_view.Altitude, 2), Latitude, Math.Floor(Day), StartHour);
        var fresh = _film is null || key != _filmKey || _exposure < _filmExposure - 1e-6 || _exposure - _filmExposure > 1.5;
        if (fresh)
        {
            if (_film is null || _film.PixelSize.Width != pixelWidth || _film.PixelSize.Height != pixelHeight)
            {
                _film?.Dispose();
                _film = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight), new Vector(96 * dpi, 96 * dpi));
            }
            else
            {
                using (_film.CreateDrawingContext(true))
                {
                    // Opening it with clear wipes the film.
                }
            }

            Expose(lst0, lst, startDots: true);
            _filmKey = key;
        }
        else if (_exposure > _filmExposure + 1e-6)
        {
            Expose(SkyModel.SiderealTime(Day, StartHour + _filmExposure), lst, startDots: false);
        }

        _filmExposure = _exposure;
        SpaceDrawing.DrawWhole(dc, _film!, new Rect(0, 0, ActualWidth, ActualHeight));

        // The brightest stars where they are now, a little glow at the end of their trails.
        foreach (var star in StarCatalog.Bright.Where(s => s.Magnitude < 2.2))
        {
            var now = SkyModel.ToHorizontal(star.Ra, star.Dec, lst, Latitude);
            if (now.Altitude < 0 || Screen(now) is not { } p) continue;
            var size = Math.Clamp(3.2 - (0.7 * star.Magnitude), 1.4, 4.5);
            dc.DrawEllipse(FadedBrush(StarColor(star.ColorIndex), 0.25), null, p, size * 2.2, size * 2.2);
            dc.DrawEllipse(FadedBrush(StarColor(star.ColorIndex), 1), null, p, size, size);
        }
    }

    /// <summary>
    /// Adds every star's arc from <paramref name="from"/> to <paramref name="to"/> (sidereal hours) to the film, gathered
    /// into one geometry per colour, thickness and brightness; with <paramref name="startDots"/>, the stars' starting dots too.
    /// The arcs have flat ends so the pieces added frame by frame join without beads.
    /// </summary>
    private void Expose(double from, double to, bool startDots)
    {
        var buckets = new Dictionary<(int Colour, int Width, int Alpha, bool Dot), (StreamGeometry Geometry, Figures Context)>();
        var latitude = Latitude;
        var span = SkyModel.Wrap24(to - from);
        if (span > 23.9) span = 0;

        Figures Bucket((int, int, int, bool) key)
        {
            if (buckets.TryGetValue(key, out var bucket)) return bucket.Context;
            var geometry = new StreamGeometry();
            bucket = (geometry, new Figures(geometry.Open()));
            buckets[key] = bucket;
            return bucket.Context;
        }

        void Add(SkyStar star)
        {
            if (SkyModel.PathOf(star.Dec, latitude) == StarPath.NeverRises) return;
            var h0 = SkyModel.ToHorizontal(star.Ra, star.Dec, from, latitude);
            var h1 = SkyModel.ToHorizontal(star.Ra, star.Dec, from + span, latitude);
            var hm = SkyModel.ToHorizontal(star.Ra, star.Dec, from + (span / 2), latitude);
            if (Math.Max(h0.Altitude, Math.Max(h1.Altitude, hm.Altitude)) < -2 && (span < 0.5 || !RisesBetween(star, from, from + span))) return;

            var colour = (int)Math.Clamp(Math.Round((star.ColorIndex + 0.3) / 0.35), 0, 6);
            var thickness = Math.Clamp(2.5 - (0.33 * star.Magnitude), 0.6, 3.4);
            var alpha = Math.Clamp(1.05 - (0.13 * (star.Magnitude + 1)), 0.18, 1);
            var widthKey = (int)Math.Round(thickness * 4);
            var alphaKey = (int)Math.Round(alpha * 10);

            if (Screen(h0) is not { } s) return;
            if (startDots)
            {
                var dot = Bucket((colour, widthKey, alphaKey, true));
                dot.Begin(s);
                dot.LineTo(s + new Vector(0.1, 0));
            }

            if (span < 1e-4) return;
            var context = Bucket((colour, widthKey, alphaKey, false));
            if (Screen(h1) is not { } e || Screen(hm) is not { } m)
            {
                // Part of the circle is behind the camera: a polyline through the visible part.
                var first = true;
                var steps = Math.Max(2, (int)(span * 4));
                for (var i = 0; i <= steps; i++)
                {
                    if (Screen(SkyModel.ToHorizontal(star.Ra, star.Dec, from + (span * i / steps), latitude)) is not { } point)
                    {
                        first = true;
                        continue;
                    }

                    if (first) context.Begin(point);
                    else context.LineTo(point);
                    first = false;
                }

                return;
            }

            context.Begin(s);
            if (((Vector)(e - s)).Length < 2.5 || !FitCircle(s, m, e, out var center, out var radius) || radius > 50000)
            {
                context.LineTo(e);
                return;
            }

            // The arc through the middle point: the large one when the centre is on the middle point's side of the chord.
            var chord = e - s;
            var large = Math.Sign(Cross(chord, m - s)) == Math.Sign(Cross(chord, center - s));
            var sweep = Cross(m - s, e - m) > 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise;
            context.ArcTo(e, new Size(radius, radius), large, sweep);
        }

        foreach (var star in FillStars) Add(star);
        foreach (var star in StarCatalog.Bright) Add(star);

        foreach (var (_, (_, context)) in buckets)
        {
            context.Dispose();
        }

        // Onto the film, over what it already holds.
        using var film = _film!.CreateDrawingContext(false);
        foreach (var (bucketKey, (geometry, _)) in buckets)
        {
            film.DrawGeometry(null, TrailPen(bucketKey), geometry);
        }
    }

    /// <summary>A geometry's open line figures: starting one ends the one before, and closing ends the last.</summary>
    private sealed class Figures(StreamGeometryContext context) : IDisposable
    {
        private bool _open;

        public void Begin(Point point)
        {
            if (_open) context.EndFigure(false);
            context.BeginFigure(point, false);
            _open = true;
        }

        public void LineTo(Point point) => context.LineTo(point);

        public void ArcTo(Point point, Size size, bool isLargeArc, SweepDirection sweep) => context.ArcTo(point, size, 0, isLargeArc, sweep);

        public void Dispose()
        {
            if (_open) context.EndFigure(false);
            _open = false;
            context.Dispose();
        }
    }

    /// <summary>A trail's pen: flat-ended for arcs (so pieces join cleanly), round and a little wider for the starting dots.</summary>
    private IPen TrailPen((int Colour, int Width, int Alpha, bool Dot) key)
    {
        if (_trailPens.TryGetValue(key, out var pen)) return pen;
        var colour = StarColor(-0.3 + (key.Colour * 0.35));
        var brush = Solid((byte)Math.Round(key.Alpha * 25.5), colour.R, colour.G, colour.B);
        var cap = key.Dot ? PenLineCap.Round : PenLineCap.Flat;
        pen = new ImmutablePen(brush, key.Width / 4.0 * (key.Dot ? 1.7 : 1), null, cap, PenLineJoin.Round);
        _trailPens[key] = pen;
        return pen;
    }

    /// <summary>Whether a star comes up during the exposure even though its start, middle and end are all below.</summary>
    private bool RisesBetween(SkyStar star, double lst0, double lst)
    {
        for (var i = 1; i < 8; i++)
        {
            if (SkyModel.ToHorizontal(star.Ra, star.Dec, lst0 + (SkyModel.Wrap24(lst - lst0) * i / 8), Latitude).Altitude > -1) return true;
        }

        return false;
    }

    private static double Cross(Vector a, Vector b) => (a.X * b.Y) - (a.Y * b.X);

    /// <summary>The circle through three points, or false when they are in a line.</summary>
    private static bool FitCircle(Point a, Point b, Point c, out Point center, out double radius)
    {
        var d = 2 * ((a.X * (b.Y - c.Y)) + (b.X * (c.Y - a.Y)) + (c.X * (a.Y - b.Y)));
        if (Math.Abs(d) < 1e-6)
        {
            center = default;
            radius = 0;
            return false;
        }

        var a2 = (a.X * a.X) + (a.Y * a.Y);
        var b2 = (b.X * b.X) + (b.Y * b.Y);
        var c2 = (c.X * c.X) + (c.Y * c.Y);
        center = new Point(((a2 * (b.Y - c.Y)) + (b2 * (c.Y - a.Y)) + (c2 * (a.Y - b.Y))) / d, ((a2 * (c.X - b.X)) + (b2 * (a.X - c.X)) + (c2 * (b.X - a.X))) / d);
        radius = ((Vector)(a - center)).Length;
        return true;
    }

    #endregion

    #region constellations, names, the pole

    /// <summary>The constellations' lines between their stars where they are now, and their names.</summary>
    private void DrawFigures(PaintContext dc, double lst)
    {
        var pen = FadedPen(Ink.Figure, 0.5, 1.1);
        foreach (var figure in StarCatalog.Figures)
        {
            var any = false;
            foreach (var (a, b) in figure.Lines)
            {
                var sa = StarCatalog.Get(a);
                var sb = StarCatalog.Get(b);
                var ha = SkyModel.ToHorizontal(sa.Ra, sa.Dec, lst, Latitude);
                var hb = SkyModel.ToHorizontal(sb.Ra, sb.Dec, lst, Latitude);
                if (ha.Altitude < 0 || hb.Altitude < 0 || Screen(ha) is not { } pa || Screen(hb) is not { } pb) continue;
                dc.DrawLine(pen, pa, pb);
                any = true;
            }

            if (!any || !ShowLabels) continue;
            var (ra, dec) = StarCatalog.Center(figure);
            var center = SkyModel.ToHorizontal(ra, dec, lst, Latitude);
            if (center.Altitude < 3 || Screen(center) is not { } c || !OnScreen(c, 30)) continue;
            DrawTargetPill(dc, Loc(ObjectTitleKey(FigureId(figure.Id))!), c + new Vector(0, 18), Ink.Figure, FigureId(figure.Id), 10.5);
        }
    }

    private bool OnScreen(Point p, double margin = 0) =>
        p.X > -margin && p.X < ActualWidth + margin && p.Y > -margin && p.Y < ActualHeight + margin;

    /// <summary>The named stars' names beside where they are now.</summary>
    private void DrawStarNames(PaintContext dc, double lst, double width, double height)
    {
        foreach (var star in StarCatalog.Named)
        {
            var now = SkyModel.ToHorizontal(star.Ra, star.Dec, lst, Latitude);
            if (now.Altitude < 1 || Screen(now) is not { } p || !OnScreen(p, -4)) continue;
            var id = StarId(star.Id);
            DrawText(dc, Loc(ObjectTitleKey(id)!), p + new Vector(8, -9), Ink.Gold, 11);
            _targets.Add(id, p, 9);
        }
    }

    /// <summary>The visible celestial pole (the centre of every circle), and the angle one star's trail has swept round it.</summary>
    private void DrawPoleMark(PaintContext dc, double lst0, double lst)
    {
        var north = Latitude >= 0;
        var pole = new Horizontal(Math.Abs(Latitude), north ? 0 : 180);
        if (Screen(pole) is not { } p || !OnScreen(p, -10)) return;

        var pen = FadedPen(Ink.Cyan, 0.85, 1.2);
        dc.DrawEllipse(null, pen, p, 7, 7);
        dc.DrawLine(pen, p + new Vector(-13, 0), p + new Vector(-4, 0));
        dc.DrawLine(pen, p + new Vector(4, 0), p + new Vector(13, 0));
        dc.DrawLine(pen, p + new Vector(0, -13), p + new Vector(0, -4));
        dc.DrawLine(pen, p + new Vector(0, 4), p + new Vector(0, 13));
        if (Glows("pole")) dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), p, 16, 16);
        _targets.Add("pole", p, 14);
        if (ShowLabels) DrawTextRight(dc, Loc(north ? "Trails_Label_NorthPole" : "Trails_Label_SouthPole"), p + new Vector(-14, 16), Ink.Cyan, 11);

        // How far the sky has turned: one star's trail seen from the pole.
        if (_exposure < 0.3) return;
        var reference = StarCatalog.Get(north ? "Kochab" : "Atria");
        var a = Screen(SkyModel.ToHorizontal(reference.Ra, reference.Dec, lst0, Latitude));
        var b = Screen(SkyModel.ToHorizontal(reference.Ra, reference.Dec, lst, Latitude));
        if (a is not { } start || b is not { } end) return;
        var wedge = FadedPen(Ink.Cyan, 0.5, 1, dashed: true);
        dc.DrawLine(wedge, p, start);
        dc.DrawLine(wedge, p, end);
        var turned = _exposure * SkyModel.DegreesPerHour;
        var mid = (Vector)(start - p) + (Vector)(end - p);
        var reach = (((Vector)(start - p)).Length + ((Vector)(end - p)).Length) / 2;
        var at = mid.Length > 1 ? p + (mid / mid.Length * Math.Clamp(reach * 0.6, 40, 140)) : p + new Vector(40, 0);
        DrawTargetPill(dc, LocFormat("Trails_Label_Arc", Number(turned, "0")), at, Ink.Cyan, "arc", 11);
    }

    #endregion

    #region the ground and the compass

    /// <summary>The hills' height above the true horizon at an azimuth, degrees, with a few trees.</summary>
    private static double Skyline(double azimuth)
    {
        var r = azimuth * Math.PI / 180;
        var hills = 1.3 + (1.4 * Math.Sin(r * 2.3)) + (0.8 * Math.Sin((r * 5.1) + 0.7)) + (0.4 * Math.Sin((r * 11.3) + 2.1));
        var height = Math.Max(0.4, hills);

        // Pine trees: narrow spikes at a few places.
        foreach (var tree in new[] { 23.0, 26.5, 61, 148, 151, 205, 247, 250, 253, 318, 322 })
        {
            var off = Math.Abs(SkyModel.Wrap360(azimuth - tree + 180) - 180);
            if (off < 1.6) height += (3.2 + (0.6 * Math.Sin(tree))) * (1 - (off / 1.6));
        }

        return height;
    }

    private void DrawGround(PaintContext dc, double width, double height)
    {
        var ring = _view.Altitude >= 50;
        var points = new List<Point>();
        var from = ring ? 0 : _view.Azimuth - 115;
        var to = ring ? 360 : _view.Azimuth + 115;
        for (var az = from; az <= to; az += 0.8)
        {
            if (Screen(new Horizontal(Skyline(SkyModel.Wrap360(az)), SkyModel.Wrap360(az))) is { } p) points.Add(p);
        }

        if (points.Count < 2) return;
        Geometry ground;
        if (ring)
        {
            // Looking up: the ground is everything outside the ring of the horizon.
            var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
            group.Children.Add(new RectangleGeometry(new Rect(-width, -height, width * 3, height * 3)));
            group.Children.Add(Line(points, closed: true, filled: true));
            ground = group;
        }
        else
        {
            var below = height + 2000;
            points.Insert(0, new Point(points[0].X, below));
            points.Add(new Point(points[^1].X, below));
            ground = Line(points, closed: true, filled: true);
        }

        dc.DrawGeometry(Ink.Ground, Ink.GroundEdge, ground);

        // The horizon straight ahead, to click.
        if (!ShowLabels) return;
        if (Screen(new Horizontal(-2.5, _view.Azimuth + 16)) is { } ahead && OnScreen(ahead, -20))
        {
            DrawTargetPill(dc, Loc("Trails_Obj_Horizon"), ahead + new Vector(0, 22), Ink.Dim, "horizon", 11);
        }
    }

    /// <summary>The compass points on the ground below the horizon.</summary>
    private void DrawCompass(PaintContext dc, double width, double height)
    {
        string[] keys = ["Trails_Dir_N", "Trails_Dir_NE", "Trails_Dir_E", "Trails_Dir_SE", "Trails_Dir_S", "Trails_Dir_SW", "Trails_Dir_W", "Trails_Dir_NW"];
        for (var i = 0; i < 8; i++)
        {
            var at = Screen(new Horizontal(-3.5, i * 45));
            if (at is not { } p || !OnScreen(p, -10)) continue;
            var box = DrawText(dc, Loc(keys[i]), p, i % 2 == 0 ? Ink.Text : Ink.Dim, i % 2 == 0 ? 15 : 12, center: true, bold: i % 2 == 0);
            AddBoxTarget("compass", box);
        }
    }

    /// <summary>The camera's display: how long the shutter has been open.</summary>
    private void DrawShutter(PaintContext dc, Rect stage, double width)
    {
        var minutes = (int)Math.Round(_exposure * 60);
        var text = LocFormat("Trails_Shutter", minutes / 60, (minutes % 60).ToString("00"));
        var formatted = _labels.Text(text, 13, Ink.Text, true);
        var box = new Rect(width - formatted.Width - 58, stage.Top + 4, formatted.Width + 34, formatted.Height + 10);
        dc.DrawRoundedRectangle(FadedBrush(Colors.Black, 0.55), Ink.Frame, box, 8, 8);
        var recording = IsPlaying && _exposure < MaxExposure;
        var blink = recording && Frac(_clock.Elapsed.TotalSeconds) < 0.6;
        dc.DrawEllipse(FadedBrush(Ink.Red, recording ? (blink ? 1 : 0.35) : 0.25), null, new Point(box.Left + 13, box.Top + (box.Height / 2)), 5, 5);
        dc.DrawText(formatted, new Point(box.Left + 24, box.Top + 5));
        if (Glows("shutter")) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), box, 8, 8);
        AddBoxTarget("shutter", box);
    }

    #endregion
}
