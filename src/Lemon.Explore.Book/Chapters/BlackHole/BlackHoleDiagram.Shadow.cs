using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.BlackHole;

/// <summary>
/// The up-close view: the ray-traced picture (<see cref="ShadowRenderer"/>) filling the diagram, the hole in the middle of
/// the stage, and labels on its parts: the shadow, the photon ring, the far side of the disk bent up over the top and its
/// underside bent round below, the side coming towards us and the side going away, and the horizon's real size.
/// </summary>
public sealed partial class BlackHoleDiagram
{
    /// <summary>Screen pixels per rendered pixel, each way.</summary>
    private const double ShadowPixel = 1.5;

    private readonly ShadowRenderer _shadow = new();

    /// <summary>Seconds of disk rotation so far.</summary>
    private double _diskTime;

    /// <summary>The hole's centre on the screen and screen pixels per M.</summary>
    private (Point Centre, double Scale) ShadowLayout(Rect stage)
    {
        var scale = Math.Min(stage.Width / 46, stage.Height / 25);
        return (new Point(stage.Left + (stage.Width / 2), stage.Top + (stage.Height / 2)), scale);
    }

    private void DrawShadowMode(PaintContext dc, double now)
    {
        var stage = Stage();
        var (centre, scale) = ShadowLayout(stage);
        var w = Math.Max(1, (int)Math.Ceiling(ActualWidth / ShadowPixel));
        var h = Math.Max(1, (int)Math.Ceiling(ActualHeight / ShadowPixel));
        var image = _shadow.Render(w, h, scale / ShadowPixel, new Point(centre.X / ShadowPixel, centre.Y / ShadowPixel),
            Inclination, BendLight, ShowDisk, Doppler, ShowStars, _diskTime);
        SpaceDrawing.DrawSmooth(dc, image, new Rect(0, 0, w * ShadowPixel, h * ShadowPixel));

        var shadowR = ShadowRenderer.ShadowRadius(BendLight) * scale;
        var horizonR = BlackHoleModel.Horizon * scale;
        _targets.Add("shadow", centre, shadowR * 0.8);

        Point Screen(Point? p) => p is { } q ? new Point(q.X * ShadowPixel, q.Y * ShadowPixel) : default;
        var anchors = _shadow.Anchors;
        var labels = ShowLabels;

        // The horizon's real size, inside the shadow when light bends (dashed: it is not what you see).
        dc.DrawEllipse(null, FadedPen(Ink.Cyan, Glows("horizon") ? 1 : labels ? 0.55 : 0.25, Glows("horizon") ? 2 : 1.2, dashed: true), centre, horizonR, horizonR);

        // Inside the shadow (when light bends it is big enough): its name above the horizon's circle, the horizon's below.
        var ringAt = centre + (new Vector(Math.Cos(-0.75), Math.Sin(-0.75)) * (shadowR + 2));
        if (labels)
        {
            DrawTargetPill(dc, Loc("Hole_Label_Shadow"), centre + new Vector(0, -horizonR - 14), Ink.Text, "shadow");
            if (BendLight)
            {
                DrawTargetPill(dc, Loc("Hole_Label_Horizon"), centre + new Vector(0, horizonR + 14), Ink.Cyan, "horizon", 10.5);
                DrawCallout(dc, Loc("Hole_Label_PhotonRing"), ringAt, ringAt + new Vector(70, -54), Ink.Gold, "photonring");
            }
            else
            {
                DrawCallout(dc, Loc("Hole_Label_Horizon"), centre + new Vector(horizonR * 0.7, horizonR * 0.7), centre + new Vector(horizonR + 90, horizonR + 60), Ink.Cyan, "horizon");
            }
        }
        else
        {
            _targets.Add("horizon", centre + new Vector(horizonR, 0), 8);
            if (BendLight) _targets.Add("photonring", ringAt, 10);
        }

        if (ShowDisk)
        {
            void Part(string id, string key, Point? anchor, Vector offset, Color color)
            {
                if (anchor is null) return;
                var at = Screen(anchor);
                if (labels) DrawCallout(dc, Loc(key), at, at + offset, color, id);
                else _targets.Add(id, at, 14);
            }

            if (BendLight && Inclination >= 45)
            {
                Part("hat", "Hole_Label_Hat", anchors.Hat, new Vector(0, -34), Ink.Gold);
                Part("below", "Hole_Label_Below", anchors.Below, new Vector(150, 26), Ink.Gold);
            }

            // Seen from above the gas moves across the line of sight, so neither side is coming or going.
            if (Inclination >= 30)
            {
                Part("coming", Doppler ? "Hole_Label_Coming" : "Hole_Label_ComingFlat", anchors.Coming, new Vector(-40, -60), Ink.Cyan);
                Part("going", Doppler ? "Hole_Label_Going" : "Hole_Label_GoingFlat", anchors.Going, new Vector(40, -60), Ink.Orange);
            }

            Part("inner", "Hole_Label_Inner", anchors.Inner, new Vector(-150, 48), Ink.Pink);
        }

        if (ShowStars && BendLight)
        {
            // Stars just outside the shadow (or above the far side's arch): their light has been bent round, so the sky
            // there is a warped mirror.
            var at = ShowDisk && Inclination >= 45 && anchors.Hat is { } hat
                ? new Point(centre.X - (shadowR * 1.1), Screen(hat).Y - 26)
                : centre + (new Vector(Math.Cos(-2.3), Math.Sin(-2.3)) * shadowR * 1.3);
            if (labels) DrawCallout(dc, Loc("Hole_Label_Stars"), at, at + new Vector(-70, -40), Ink.Violet, "stars");
            else _targets.Add("stars", at, 12);
        }

        DrawScaleBar(dc, stage, scale);
    }

    /// <summary>A bar 10 M long in the bottom-left corner of the stage.</summary>
    private void DrawScaleBar(PaintContext dc, Rect stage, double scale)
    {
        var y = stage.Bottom - 14;
        var x0 = stage.Left + 28;
        var x1 = x0 + (10 * scale);
        var pen = FadedPen(Ink.Text, 0.75, 1.5, flat: true);
        dc.DrawLine(pen, new Point(x0, y), new Point(x1, y));
        dc.DrawLine(pen, new Point(x0, y - 4), new Point(x0, y + 4));
        dc.DrawLine(pen, new Point(x1, y - 4), new Point(x1, y + 4));
        var box = DrawText(dc, Loc("Hole_Scale_TenM"), new Point(x1 + 8, y), Ink.Text, 11);
        AddBoxTarget("scale", box.Union(new Rect(x0, y - 5, x1 - x0, 10)));
    }

    private static IPen ExploreGlow(double now) => Lemon.Explore.Book.Controls.Explore.ExploreTargets.GlowPen(now);

    /// <summary>Tilting by dragging up and down: degrees per screen pixel.</summary>
    private const double TiltPerPixel = 0.3;
}
