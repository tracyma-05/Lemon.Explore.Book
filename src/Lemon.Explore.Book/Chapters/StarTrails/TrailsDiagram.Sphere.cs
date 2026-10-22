using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.StarTrails;

/// <summary>
/// Sphere mode: the celestial sphere round an observer standing on a disc of ground, seen from outside at a slant. The
/// sky's axis through the celestial poles is tilted up from the horizon by the latitude; the stars' daily circles run
/// round it, parallel to the celestial equator. A gold cap round the visible pole never sets, a blue cap round the other
/// never rises; the circles' parts below the ground are dashed. A few bright stars run round their circles.
/// </summary>
public sealed partial class TrailsDiagram
{
    /// <summary>The stars shown on the sphere, north to south.</summary>
    internal static readonly string[] SphereStars = ["Polaris", "Dubhe", "Capella", "Vega", "Sirius", "Antares", "Canopus", "Acrux"];

    /// <summary>Where the sphere is seen from (dragging moves round it): azimuth and height, degrees.</summary>
    private double _sphereAzimuth = 215;

    private double _sphereElevation = 22;

    private Point _sphereCenter;
    private double _sphereRadius;

    private readonly record struct SpherePoint(Point Screen, double Depth, double Up);

    /// <summary>A point on (or in) the sphere, x east, y north, z up, on the picture with its depth (positive in front).</summary>
    private SpherePoint OnSphere(double x, double y, double z)
    {
        var a = _sphereAzimuth * Math.PI / 180;
        var e = _sphereElevation * Math.PI / 180;
        var (rx, ry) = (-Math.Cos(a), Math.Sin(a));
        var (ux, uy, uz) = (-Math.Sin(e) * Math.Sin(a), -Math.Sin(e) * Math.Cos(a), Math.Cos(e));
        var (dx, dy, dz) = (Math.Sin(a) * Math.Cos(e), Math.Cos(a) * Math.Cos(e), Math.Sin(e));
        var sx = (x * rx) + (y * ry);
        var sy = (x * ux) + (y * uy) + (z * uz);
        return new SpherePoint(
            new Point(_sphereCenter.X + (sx * _sphereRadius), _sphereCenter.Y - (sy * _sphereRadius)),
            (x * dx) + (y * dy) + (z * dz),
            z);
    }

    /// <summary>Where a star at declination <paramref name="dec"/> and hour angle <paramref name="hour"/> (hours) is on the sphere.</summary>
    private (double X, double Y, double Z) Celestial(double dec, double hour)
    {
        var phi = Latitude * Math.PI / 180;
        var d = dec * Math.PI / 180;
        var h = hour * 15 * Math.PI / 180;
        var (px, py, pz) = (0.0, Math.Cos(phi), Math.Sin(phi));
        var (qx, qy, qz) = (0.0, -Math.Sin(phi), Math.Cos(phi));
        return (
            (Math.Sin(d) * px) + (Math.Cos(d) * ((Math.Cos(h) * qx) - Math.Sin(h))),
            (Math.Sin(d) * py) + (Math.Cos(d) * Math.Cos(h) * qy),
            (Math.Sin(d) * pz) + (Math.Cos(d) * Math.Cos(h) * qz));
    }

    private SpherePoint Project((double X, double Y, double Z) p) => OnSphere(p.X, p.Y, p.Z);

    private void DrawSphereMode(PaintContext dc, double width, double height)
    {
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        var stage = Stage();
        _sphereRadius = Math.Clamp(Math.Min(stage.Width * 0.34, stage.Height * 0.42), 80, 330);
        _sphereCenter = new Point(stage.Left + (stage.Width / 2), stage.Top + (stage.Height * 0.52));
        var latitude = Latitude;
        var north = latitude >= 0;
        var limit = 90 - Math.Abs(latitude);

        // The sphere's outline and the disc of ground.
        dc.DrawEllipse(FadedBrush(Color.FromRgb(0x30, 0x48, 0x78), 0.12), FadedPen(Ink.Text, 0.25, 1), _sphereCenter, _sphereRadius, _sphereRadius);
        var horizon = Enumerable.Range(0, 97).Select(i => OnSphere(Math.Sin(i * Math.PI / 48), Math.Cos(i * Math.PI / 48), 0).Screen).ToList();
        dc.DrawGeometry(FadedBrush(Color.FromRgb(0x3A, 0x6A, 0x3A), 0.32), FadedPen(Ink.Green, 0.7, 1.4), Line(horizon, closed: true, filled: true));

        // The caps: never sets round the visible pole, never rises round the other.
        if (limit < 89.5)
        {
            var sign = north ? 1 : -1;
            DrawCap(dc, sign * limit, Ink.Gold, "circumpolar", "Trails_Label_Circumpolar");
            DrawCap(dc, -sign * limit, Ink.Blue, "never", "Trails_Label_NeverRises");
        }

        // The axis through the celestial poles, and the poles.
        var ncp = Project(Celestial(90, 0));
        var scp = Project(Celestial(-90, 0));
        var axisPen = FadedPen(Ink.Text, 0.45, 1.2, dashed: true);
        dc.DrawLine(axisPen, Mix(scp.Screen, ncp.Screen, -0.15), Mix(scp.Screen, ncp.Screen, 1.15));
        DrawPoleDot(dc, ncp, "ncp", "Trails_Label_NorthPole");
        DrawPoleDot(dc, scp, "scp", "Trails_Label_SouthPole");

        // The latitude: the pole's height above the horizon.
        var phi = Math.Abs(latitude);
        if (phi > 2)
        {
            var arc = Enumerable.Range(0, 25).Select(i =>
            {
                var t = phi * i / 24 * Math.PI / 180;
                return OnSphere(0, (north ? 1 : -1) * Math.Cos(t) * 0.42, Math.Sin(t) * 0.42).Screen;
            }).ToList();
            dc.DrawGeometry(null, FadedPen(Ink.Gold, 0.9, 1.6), Line(arc));
            if (ShowLabels) DrawText(dc, $"{Number(phi, "0")}°", arc[12] + new Vector(6, -6), Ink.Gold, 12, bold: true);
        }

        // The celestial equator, and the stars' circles with the stars on them.
        DrawDiurnal(dc, 0, Ink.Cyan, 1.8, glow: Glows("equator"));
        var equatorFront = Enumerable.Range(0, 48).Select(i => Project(Celestial(0, i / 2.0))).MaxBy(p => p.Depth - (Math.Abs(p.Up) * 0.6));
        if (ShowLabels) DrawTargetPill(dc, Loc("Trails_Obj_Equator"), equatorFront.Screen + new Vector(0, 16), Ink.Cyan, "equator", 11);
        else _targets.Add("equator", equatorFront.Screen, 12);

        foreach (var id in SphereStars)
        {
            var star = StarCatalog.Get(id);
            var path = SkyModel.PathOf(star.Dec, latitude);
            var colour = path switch
            {
                StarPath.Circumpolar => Ink.Gold,
                StarPath.NeverRises => Ink.Blue,
                _ => Ink.Text,
            };
            if (Math.Abs(star.Dec) < 89) DrawDiurnal(dc, star.Dec, colour, 1.2, glow: false);
            var p = Project(Celestial(star.Dec, SphereTime - star.Ra));
            var above = p.Up > 0;
            var size = Math.Clamp(3.6 - (0.6 * star.Magnitude), 2.2, 5);
            dc.DrawEllipse(FadedBrush(StarColor(star.ColorIndex), above ? 0.3 : 0.12), null, p.Screen, size * 2, size * 2);
            dc.DrawEllipse(FadedBrush(StarColor(star.ColorIndex), above ? 1 : 0.4), null, p.Screen, size, size);
            _targets.Add(StarId(id), p.Screen, 8);
            if (ShowLabels) DrawText(dc, Loc(ObjectTitleKey(StarId(id))!), p.Screen + new Vector(8, -9), above ? Ink.Gold : Ink.Dim, 11);
        }

        // The band of stars that rise and set, between the caps.
        if (ShowLabels && limit > 3)
        {
            var band = Project(Celestial(north ? -limit * 0.35 : limit * 0.35, 0));
            DrawTargetPill(dc, Loc("Trails_Label_Risers"), band.Screen + new Vector(_sphereRadius * 0.35, 0), Ink.Text, "risers", 11);
        }

        // The zenith, the compass points and the observer.
        var zenith = OnSphere(0, 0, 1);
        dc.DrawLine(FadedPen(Ink.Text, 0.3, 1, dashed: true), _sphereCenter, zenith.Screen);
        dc.DrawEllipse(FadedBrush(Ink.Text, 0.9), null, zenith.Screen, 3.5, 3.5);
        if (Glows("zenith")) dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), zenith.Screen, 12, 12);
        _targets.Add("zenith", zenith.Screen, 10);
        if (ShowLabels) DrawTextRight(dc, Loc("Trails_Label_Zenith"), zenith.Screen + new Vector(-8, -10), Ink.Text, 11);

        string[] keys = ["Trails_Dir_N", "Trails_Dir_E", "Trails_Dir_S", "Trails_Dir_W"];
        for (var i = 0; i < 4; i++)
        {
            var az = i * Math.PI / 2;
            var p = OnSphere(Math.Sin(az) * 1.1, Math.Cos(az) * 1.1, 0);
            var box = DrawText(dc, Loc(keys[i]), p.Screen, p.Depth >= 0 ? Ink.Text : Ink.Dim, 14, center: true, bold: true);
            AddBoxTarget("horizon", box);
        }

        DrawObserver(dc, _sphereCenter);
    }

    /// <summary>A star's daily circle at <paramref name="dec"/>: solid above the ground, dashed below, fainter behind.</summary>
    private void DrawDiurnal(PaintContext dc, double dec, Color colour, double thickness, bool glow)
    {
        var points = Enumerable.Range(0, 121).Select(i => Project(Celestial(dec, i * 24 / 120.0))).ToList();
        if (glow) dc.DrawGeometry(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 8), Line(points.Select(p => p.Screen)));

        // Runs of points of the same kind (above or below, in front or behind), each drawn in its style.
        var run = new List<Point> { points[0].Screen };
        (bool Above, bool Front) Kind(SpherePoint p) => (p.Up >= 0, p.Depth >= 0);
        var kind = Kind(points[0]);
        for (var i = 1; i < points.Count; i++)
        {
            var next = Kind(points[i]);
            run.Add(points[i].Screen);
            if (next == kind && i < points.Count - 1) continue;
            var alpha = (kind.Above ? 0.95 : 0.4) * (kind.Front ? 1 : 0.55);
            dc.DrawGeometry(null, FadedPen(colour, alpha, kind.Above ? thickness : thickness * 0.8, dashed: !kind.Above), Line(run));
            run = [points[i].Screen];
            kind = next;
        }
    }

    /// <summary>A cap of sky bounded by the daily circle at <paramref name="dec"/>, tinted, with its name.</summary>
    private void DrawCap(PaintContext dc, double dec, Color colour, string id, string label)
    {
        var points = Enumerable.Range(0, 97).Select(i => Project(Celestial(dec, i * 24 / 96.0)).Screen).ToList();
        dc.DrawGeometry(FadedBrush(colour, Glows(id) ? 0.3 : 0.16), FadedPen(colour, 0.8, 1.4, dashed: true), Line(points, closed: true, filled: true));
        var center = Project(Celestial(Math.Sign(dec) * 90, 0)).Screen;
        var mid = Mix(center, points[0], 0.5);
        if (ShowLabels) DrawTargetPill(dc, Loc(label), mid, colour, id, 11);
        else _targets.Add(id, mid, 14);
    }

    private void DrawPoleDot(PaintContext dc, SpherePoint pole, string id, string label)
    {
        dc.DrawEllipse(FadedBrush(Ink.Text, pole.Up >= 0 ? 0.95 : 0.45), null, pole.Screen, 4, 4);
        if (Glows(id)) dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), pole.Screen, 12, 12);
        _targets.Add(id, pole.Screen, 10);
        if (ShowLabels) DrawText(dc, Loc(label), pole.Screen + new Vector(8, 10), pole.Up >= 0 ? Ink.Text : Ink.Dim, 11);
    }

    /// <summary>A little person standing at the centre.</summary>
    private void DrawObserver(PaintContext dc, Point foot)
    {
        var s = Math.Clamp(_sphereRadius / 14, 9, 20);
        var pen = FadedPen(Ink.Orange, 1, Math.Max(1.6, s * 0.14));
        dc.DrawLine(pen, foot, foot + new Vector(-s * 0.3, s * 0.05));
        dc.DrawLine(pen, foot + new Vector(0, -s * 0.9), foot + new Vector(-s * 0.15, 0));
        dc.DrawLine(pen, foot + new Vector(0, -s * 0.9), foot + new Vector(s * 0.2, 0));
        dc.DrawLine(pen, foot + new Vector(0, -s * 0.9), foot + new Vector(0, -s * 1.6));
        dc.DrawLine(pen, foot + new Vector(0, -s * 1.4), foot + new Vector(s * 0.4, -s * 1.8));
        dc.DrawEllipse(FadedBrush(Ink.Orange, 1), null, foot + new Vector(0, -s * 1.85), s * 0.25, s * 0.25);
        if (Glows("observer")) dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), foot + new Vector(0, -s), s * 1.3, s * 1.3);
        _targets.Add("observer", foot + new Vector(0, -s), s);
    }
}
