using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using Lemon.Explore.Book.Controls.Space;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.Kepler;

/// <summary>
/// The ellipse view, Kepler's first law: the orbit is an ellipse with the Sun at one focus and nothing at the other.
/// The string of the gardener's construction runs from the planet to both foci; its two parts always add up to the
/// long axis. Shared with the areas view: the orbit's layout and the planet (a comet with its tail for Halley's).
/// </summary>
public sealed partial class KeplerDiagram
{
    // View transform of the orbit views: the ellipse's centre and the Sun on screen, and DIPs per semi-major axis.
    private Point _center;
    private Point _sunScreen;
    private double _orbitScale = 1;

    private void DrawEllipse(PaintContext dc, double dpi, double now)
    {
        var e = ShownEccentricity;
        LayoutOrbit(e);
        var body = KeplerGeometry.Body(BodyId);
        var s = _orbitScale;
        var b = Math.Sqrt(1 - (e * e));
        var otherFocus = new Point(_center.X - (e * s), _center.Y);

        if (ShowCircle)
        {
            // Labelled at its upper left, where it leaves the ellipse most.
            dc.DrawEllipse(null, KeplerDrawing.CirclePen, _center, s, s);
            var text = Loc("Kepler_Label_Circle");
            var label = _labels.Text(text, 11, KeplerDrawing.You);
            var at = new Point(_center.X - (s * 0.7071), _center.Y - (s * 0.7071));
            var topLeft = new Point(Math.Max(8, at.X - label.Width - 22), at.Y - label.Height - 6);
            _labels.DrawTextPill(dc, text, topLeft, 11, KeplerDrawing.You);
            AddPillTarget("circle", text, topLeft, 11, KeplerDrawing.You);
        }

        // The long axis through both foci, the short axis, and the centre.
        dc.DrawLine(KeplerDrawing.AxisPen, new Point(_center.X - s - 18, _center.Y), new Point(_center.X + s + 18, _center.Y));
        dc.DrawLine(Paint.MinorAxis, new Point(_center.X, _center.Y - (b * s)), new Point(_center.X, _center.Y + (b * s)));
        if (Glows("orbit")) dc.DrawEllipse(null, ExploreTargets.GlowPen(now), _center, s, b * s);
        dc.DrawEllipse(null, KeplerDrawing.OrbitPen, _center, s, b * s);
        DrawCross(dc, _center);
        _targets.Add("center", _center, 6);

        // c, from the centre to the Sun, above the axis when there is room.
        if (e * s > 26)
        {
            var y = _center.Y - 12;
            dc.DrawLine(Paint.Bracket, new Point(_center.X, y), new Point(_sunScreen.X, y));
            dc.DrawLine(Paint.Bracket, new Point(_center.X, y - 4), new Point(_center.X, y + 4));
            dc.DrawLine(Paint.Bracket, new Point(_sunScreen.X, y - 4), new Point(_sunScreen.X, y + 4));
            var c = _labels.Text("c", 12, KeplerDrawing.DimText);
            dc.DrawText(c, new Point(((_center.X + _sunScreen.X) / 2) - (c.Width / 2), y - c.Height - 1));
            if (e * s > SunRadius(e) + 30) _targets.AddBox("focal", new Rect(new Point(_center.X + 6, y - c.Height - 1), new Point(_sunScreen.X - SunRadius(e) - 4, y + 4)));
        }

        var point = KeplerGeometry.At(_phase, e);
        var planet = OrbitScreen(point.X, point.Y);

        // The pins at the foci and the string round the planet.
        if (ShowString)
        {
            if (Glows("string"))
            {
                var glow = ExploreTargets.GlowPen(now, 9);
                dc.DrawLine(glow, otherFocus, planet);
                dc.DrawLine(glow, _sunScreen, planet);
            }

            dc.DrawLine(KeplerDrawing.CyanPen, otherFocus, planet);
            dc.DrawLine(KeplerDrawing.GoldPen, _sunScreen, planet);
            var r1 = point.Radius;
            var r2 = 2 - r1;
            DrawLength(dc, _sunScreen, planet, LocFormat("Kepler_Label_R1", KeplerFormat.Au(r1 * body.SemiMajorAxisAu)), KeplerDrawing.Gold, "string");
            DrawLength(dc, otherFocus, planet, LocFormat("Kepler_Label_R2", KeplerFormat.Au(r2 * body.SemiMajorAxisAu)), KeplerDrawing.Cyan, "string");
        }

        dc.DrawEllipse(Paint.Pin, KeplerDrawing.DotOutline, otherFocus, 4.5, 4.5);
        if (!ShowString) dc.DrawEllipse(null, Paint.EmptyFocus, otherFocus, 8, 8);
        _labels.DrawCentered(dc, "Kepler_Label_OtherFocus", new Point(otherFocus.X, otherFocus.Y + 12), 11.5, KeplerDrawing.Cyan);
        AddCenteredLabelTarget("otherfocus", "Kepler_Label_OtherFocus", new Point(otherFocus.X, otherFocus.Y + 12), 11.5, KeplerDrawing.Cyan);
        _targets.Add("otherfocus", otherFocus, 8);

        DrawApsides(dc, body, e, false);
        DrawSun(dc, dpi, _sunScreen, SunRadius(e), now);
        var sunLabel = new Point(_sunScreen.X, _sunScreen.Y + SunRadius(e) + 8);
        _labels.DrawCentered(dc, "Kepler_Label_SunFocus", sunLabel, 11.5, KeplerDrawing.Gold);
        AddCenteredLabelTarget("sun", "Kepler_Label_SunFocus", sunLabel, 11.5, KeplerDrawing.Gold);
        _targets.Add("sun", _sunScreen, SunRadius(e));

        // The long axis measured underneath; with the string, the string's two parts add up to just that.
        var under = _center.Y + (b * s) + 30;
        var axis = KeplerFormat.Au(2 * body.SemiMajorAxisAu);
        DrawSpan(dc, _center.X - s, _center.X + s, under,
            LocFormat(ShowString ? "Kepler_Label_StringSum" : "Kepler_Label_MajorAxis", axis), "majoraxis");

        DrawPlanet(dc, dpi, body, point, planet, e);
    }

    /// <summary>
    /// Fits the orbit between the title and the toolbar, with room for the measurement underneath: as wide as fits,
    /// unless that would make it too tall. The shape eases with <see cref="ShownEccentricity"/>, so the view zooms
    /// smoothly when it changes.
    /// </summary>
    private void LayoutOrbit(double e)
    {
        const double top = 162;
        var bottom = 150 + _reserve.Current;
        var halfWidth = Math.Max(60, (ActualWidth - 260) / 2);
        var halfHeight = Math.Max(60, (ActualHeight - top - bottom) / 2);
        var b = Math.Sqrt(1 - (e * e));
        _orbitScale = Math.Min(halfWidth, halfHeight / Math.Max(b, 0.35));
        _center = new Point(ActualWidth / 2, top + halfHeight - 6);
        _sunScreen = new Point(_center.X + (e * _orbitScale), _center.Y);
    }

    /// <summary>A point of the orbit (units of a, the Sun at the origin) on screen; y is up.</summary>
    private Point OrbitScreen(double x, double y) => new(_sunScreen.X + (x * _orbitScale), _sunScreen.Y - (y * _orbitScale));

    /// <summary>The Sun as big as fits inside perihelion.</summary>
    private double SunRadius(double e) => Math.Clamp(0.4 * (1 - e) * _orbitScale, 5, 24);

    private double PlanetRadius(double e) => Math.Clamp(0.3 * (1 - e) * _orbitScale, 4, 13);

    /// <summary>The body on its orbit: a ray-traced planet, lit from the Sun, or Halley's comet with a tail away from the Sun.</summary>
    private void DrawPlanet(PaintContext dc, double dpi, KeplerBody body, OrbitPoint point, Point at, double e)
    {
        var radius = PlanetRadius(e);
        if (body.Id == "Halley")
        {
            DrawCometTail(dc, at, point.Radius * body.SemiMajorAxisAu);
            radius = Math.Min(radius, 5);
        }

        var id = body.Id switch
        {
            "Custom" => "Neptune",
            "Pluto" or "Halley" => "Moon",
            _ => body.Id,
        };
        DrawSphere(dc, Renderer(id), at, radius, LightFrom(at, _sunScreen, 0.15), dpi);
        _targets.Add("planet", at, radius + 2);

        // The name outside the orbit, away from the arrows and lines drawn inside it.
        var name = Loc(BodyNameKey(body.Id));
        var label = _labels.Text(name, 12.5, KeplerDrawing.Text, true);
        // Inside instead near the top, where the title is.
        var away = Outward(at);
        var reach = radius + 6 + (Math.Abs(away.X) * label.Width / 2) + (Math.Abs(away.Y) * label.Height / 2);
        var anchor = at + (away * reach);
        if (anchor.Y - (label.Height / 2) < 150) anchor = at - (away * reach);
        dc.DrawText(label, new Point(anchor.X - (label.Width / 2), anchor.Y - (label.Height / 2)));
    }

    /// <summary>The unit vector from the ellipse's centre out through <paramref name="point"/>.</summary>
    private Vector Outward(Point point)
    {
        Vector away = point - _center;
        return away.Length < 1 ? new Vector(0, -1) : away / away.Length;
    }

    /// <summary>A comet's tail points away from the Sun and grows as it comes in: none beyond about 3 AU.</summary>
    private void DrawCometTail(PaintContext dc, Point at, double distanceAu)
    {
        var length = 180 * Math.Clamp((3 - distanceAu) / 2.4, 0, 1);
        if (length < 4) return;

        Vector away = at - _sunScreen;
        if (away.Length < 1e-6) return;
        away /= away.Length;
        var side = new Vector(-away.Y, away.X);
        var tip = at + (away * length);
        var brush = new ImmutableLinearGradientBrush([new ImmutableGradientStop(0, Color.FromArgb(200, 210, 235, 255)), new ImmutableGradientStop(1, Color.FromArgb(0, 160, 200, 255))], startPoint: new RelativePoint(at, RelativeUnit.Absolute), endPoint: new RelativePoint(tip, RelativeUnit.Absolute));
        dc.DrawGeometry(brush, null, Polygon(at + (side * 3), tip + (side * length * 0.16), tip - (side * length * 0.16), at - (side * 3)));
    }

    /// <summary>Perihelion and aphelion with their distances; in the areas view, fastest and slowest.</summary>
    private void DrawApsides(PaintContext dc, KeplerBody body, double e, bool speeds)
    {
        if (e < KeplerGeometry.CircleEccentricity) return;

        var perihelion = OrbitScreen(1 - e, 0);
        var aphelion = OrbitScreen(-1 - e, 0);
        dc.DrawEllipse(KeplerDrawing.FastBrush, KeplerDrawing.DotOutline, perihelion, 4, 4);
        dc.DrawEllipse(Paint.SlowBrush, KeplerDrawing.DotOutline, aphelion, 4, 4);

        var periText = speeds
            ? LocFormat("Kepler_Label_PerihelionSpeed", KeplerGeometry.SpeedKmS(body.SemiMajorAxisAu, 1 - e).ToString("0.#"))
            : LocFormat("Kepler_Label_Perihelion", KeplerFormat.Au(body.SemiMajorAxisAu * (1 - e)));
        var aphText = speeds
            ? LocFormat("Kepler_Label_AphelionSpeed", KeplerGeometry.SpeedKmS(body.SemiMajorAxisAu, 1 + e).ToString("0.#"))
            : LocFormat("Kepler_Label_Aphelion", KeplerFormat.Au(body.SemiMajorAxisAu * (1 + e)));

        var peri = _labels.Text(periText, 11.5, KeplerDrawing.Fast);
        var periAt = new Point(Math.Min(perihelion.X + 12, ActualWidth - peri.Width - 20), perihelion.Y - peri.Height - 14);
        _labels.DrawTextPill(dc, periText, periAt, 11.5, KeplerDrawing.Fast);
        var aph = _labels.Text(aphText, 11.5, KeplerDrawing.Slow);
        var aphAt = new Point(Math.Max(8, aphelion.X - aph.Width - 24), aphelion.Y - aph.Height - 14);
        _labels.DrawTextPill(dc, aphText, aphAt, 11.5, KeplerDrawing.Slow);

        // The dots click too; the pills, added last, carry the rings (and already say what they are).
        _targets.Add("perihelion", perihelion, 5);
        _targets.Add("aphelion", aphelion, 5);
        AddPillTarget("perihelion", periText, periAt, 11.5, KeplerDrawing.Fast);
        AddPillTarget("aphelion", aphText, aphAt, 11.5, KeplerDrawing.Slow);
    }

    /// <summary>A length along a line, written beside its middle.</summary>
    private void DrawLength(PaintContext dc, Point from, Point to, string text, Color color, string id)
    {
        Vector along = to - from;
        if (along.Length < 30) return;
        var normal = new Vector(-along.Y, along.X);
        normal /= normal.Length;

        // Keep the label on the side away from the ellipse's centre.
        var middle = from + (along / 2);
        if (Vector.Dot(normal, middle - _center) < 0) normal = -normal;
        DrawCenteredPill(dc, text, middle + (normal * 14), 11.5, color);
        AddCenteredPillTarget(id, text, middle + (normal * 14), 11.5, color);
    }

    /// <summary>A measurement with arrows at both ends and its text in the middle.</summary>
    private void DrawSpan(PaintContext dc, double left, double right, double y, string text, string id)
    {
        if (y > ActualHeight - 96 - _reserve.Current) return;
        dc.DrawLine(Paint.Bracket, new Point(left, y), new Point(right, y));
        DrawArrowHead(dc, Paint.BracketBrush, new Point(left, y), new Vector(-1, 0), 6);
        DrawArrowHead(dc, Paint.BracketBrush, new Point(right, y), new Vector(1, 0), 6);
        DrawCenteredPill(dc, text, new Point((left + right) / 2, y), 11.5, KeplerDrawing.DimText);
        AddCenteredPillTarget(id, text, new Point((left + right) / 2, y), 11.5, KeplerDrawing.DimText);
    }

    private static void DrawCross(PaintContext dc, Point at)
    {
        dc.DrawLine(Paint.Bracket, new Point(at.X - 5, at.Y - 5), new Point(at.X + 5, at.Y + 5));
        dc.DrawLine(Paint.Bracket, new Point(at.X - 5, at.Y + 5), new Point(at.X + 5, at.Y - 5));
    }

    /// <summary>The name of a body: the Solar System page's for the planets, the page's own for the rest.</summary>
    internal static string BodyNameKey(string id) => id is "Custom" or "Pluto" or "Halley" or "Ceres"
        ? $"Kepler_Body_{id}"
        : $"SolarSystem_{id}_Name";

    private static class Paint
    {
        public static readonly IPen MinorAxis = MakePen(Solid(40, 255, 255, 255), 1, dashed: true);
        public static readonly IPen Bracket = MakePen(Solid(140, 255, 255, 255), 1);
        public static readonly IBrush BracketBrush = Solid(160, 255, 255, 255);
        public static readonly IBrush Pin = SpaceDrawing.Solid(KeplerDrawing.Cyan);
        public static readonly IPen EmptyFocus = MakePen(Solid(150, 0x7F, 0xE0, 0xFF), 1.2, dashed: true);
        public static readonly IBrush SlowBrush = SpaceDrawing.Solid(KeplerDrawing.Slow);
    }
}
