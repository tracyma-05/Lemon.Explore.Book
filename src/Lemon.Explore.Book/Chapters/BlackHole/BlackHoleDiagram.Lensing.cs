using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.BlackHole;

/// <summary>
/// The lensing view: the sky behind a black hole far away, worked out pixel by pixel (<see cref="LensRenderer"/>). The
/// hole drifts along a track in front of a galaxy (or a star, or a grid) and can be dragged anywhere; its Einstein ring,
/// where the source really is, the images of a star and how much brighter each looks, and a side view of the light's two
/// paths are drawn over it.
/// </summary>
public sealed partial class BlackHoleDiagram
{
    /// <summary>The track's height (view units): just below the source, so a star's light peaks at about 3×.</summary>
    public const double TrackY = -0.12;

    /// <summary>The track's ends (view units).</summary>
    public const double TrackEnd = 2.4;

    /// <summary>How fast the hole drifts along the track, view units per second.</summary>
    private const double DriftSpeed = 0.3;

    private const double LensPixel = 1.5;

    private readonly LensRenderer _lens = new();

    /// <summary>Where the view's centre is on the screen and how many view units a screen pixel is.</summary>
    private (Point Origin, double UnitsPerPixel) LensLayout(Rect stage) =>
        (new Point(stage.Left + (stage.Width / 2), stage.Top + (stage.Height / 2)), 2 / stage.Height);

    private void StepLensing(double dt)
    {
        if (LensX >= TrackEnd)
        {
            _hold += dt;
            if (_hold < 1.5) return;
            _hold = 0;
            SetCurrentValue(LensXProperty, -TrackEnd);
            SetCurrentValue(LensYProperty, TrackY);
            return;
        }

        SetCurrentValue(LensXProperty, Math.Min(TrackEnd, LensX + (dt * DriftSpeed)));
    }

    private void DrawLensingMode(PaintContext dc, double now)
    {
        var stage = Stage();
        var (origin, upp) = LensLayout(stage);
        var w = Math.Max(1, (int)Math.Ceiling(ActualWidth / LensPixel));
        var h = Math.Max(1, (int)Math.Ceiling(ActualHeight / LensPixel));
        var lens = new Point(LensX, LensY);
        var image = _lens.Render(w, h, new Point(origin.X / LensPixel, origin.Y / LensPixel), upp * LensPixel, LensSource, lens, EinsteinRadius, LensOn);
        dc.DrawImage(image, new Rect(0, 0, w * LensPixel, h * LensPixel));
        DrawTitleShade(dc);

        Point S(double x, double y) => new(origin.X + (x / upp), origin.Y - (y / upp));
        var lensAt = S(lens.X, lens.Y);
        var ringR = EinsteinRadius / upp;

        // The track the hole drifts along.
        var trackPen = FadedPen(Ink.Text, Glows("track") ? 0.9 : 0.25, Glows("track") ? 2 : 1, dashed: true);
        dc.DrawLine(trackPen, S(-TrackEnd, TrackY), S(TrackEnd, TrackY));
        if (Glows("track")) dc.DrawLine(ExploreGlow(now), S(-TrackEnd, TrackY), S(TrackEnd, TrackY));
        _targets.Add("track", S(-1.9, TrackY), 8);

        // Where the source really is.
        if (ShowTruePosition && LensSource != LensSource.Grid)
        {
            var at = S(0, 0);
            var pen = FadedPen(Ink.Green, 0.8, 1.2, dashed: true);
            if (LensSource == LensSource.Galaxy) dc.DrawEllipse(null, pen, at, 0.36 / upp, 0.36 * 0.72 / upp);
            else dc.DrawEllipse(null, pen, at, 9, 9);
            dc.DrawLine(FadedPen(Ink.Green, 0.8, 1), at + new Vector(-5, 0), at + new Vector(5, 0));
            dc.DrawLine(FadedPen(Ink.Green, 0.8, 1), at + new Vector(0, -5), at + new Vector(0, 5));
            var pill = at + new Vector(0, (LensSource == LensSource.Galaxy ? 0.26 / upp : 9) + 18);
            if (ShowLabels) DrawTargetPill(dc, Loc("Hole_Label_True"), pill, Ink.Green, "true", 10.5);
            else _targets.Add("true", at, 9);
        }

        if (LensOn && ShowRing)
        {
            dc.DrawEllipse(null, FadedPen(Ink.Gold, Glows("ring") ? 1 : 0.55, Glows("ring") ? 2.2 : 1.1, dashed: true), lensAt, ringR, ringR);
            if (Glows("ring")) dc.DrawEllipse(null, ExploreGlow(now), lensAt, ringR, ringR);
            var at = lensAt + new Vector(ringR * 0.72, -ringR * 0.72);
            if (ShowLabels) DrawTargetPill(dc, Loc("Hole_Label_Ring"), at + new Vector(34, -10), Ink.Gold, "ring", 10.5);
            else _targets.Add("ring", at, 9);
        }

        // The images of the source's middle (the star, the galaxy's core).
        if (LensOn && LensSource != LensSource.Grid)
        {
            var d = new Vector(-lens.X, -lens.Y);
            var u = d.Length / EinsteinRadius;
            if (u > 1e-4)
            {
                d /= d.Length;
                var (outer, inner, outerGain, innerGain) = BlackHoleModel.LensImages(u);
                var a = lensAt + (new Vector(d.X, -d.Y) * outer * ringR);
                var b = lensAt + (new Vector(d.X, -d.Y) * inner * ringR);
                if (LensSource == LensSource.Star)
                {
                    if (ShowLabels)
                    {
                        DrawCallout(dc, LocFormat("Hole_Label_Image1", Number(outerGain, "0.0")), a, a + new Vector(46 * Math.Sign(d.X + 1e-9), -40), Ink.Cyan, "image1");
                        DrawCallout(dc, LocFormat("Hole_Label_Image2", Number(innerGain, "0.0")), b, b + new Vector(-46 * Math.Sign(d.X + 1e-9), 40), Ink.Cyan, "image2");
                    }
                    else
                    {
                        _targets.Add("image1", a, 10);
                        _targets.Add("image2", b, 8);
                    }
                }
                else if (ShowLabels)
                {
                    DrawCallout(dc, Loc("Hole_Label_Arc"), a, a + new Vector(52 * Math.Sign(d.X + 1e-9), -46), Ink.Cyan, "arc");
                }
                else
                {
                    _targets.Add("arc", a, 12);
                }
            }
        }

        // The hole itself: far too small to see at this distance, so only a mark.
        dc.DrawEllipse(Brushes.Black, FadedPen(Ink.Text, 0.85, 1.2), lensAt, 4.5, 4.5);
        _targets.Add("lens", lensAt, 10);
        if (ShowLabels) DrawTargetPill(dc, Loc("Hole_Label_Lens"), lensAt + new Vector(0, 18), Ink.Text, "lens", 10.5);

        DrawSideView(dc, stage, lens);
    }

    /// <summary>
    /// The same thing seen from the side, not to scale: light from the source passing above and below the hole on its
    /// way to us, each path bent where it passes, and where we see each image.
    /// </summary>
    private void DrawSideView(PaintContext dc, Rect stage, Point lens)
    {
        var box = new Rect(stage.Left + 16, stage.Bottom - 132, 290, 118);
        DrawFrame(dc, box);
        AddBoxTarget("sideview", box);
        DrawText(dc, Loc("Hole_Side_Title"), new Point(box.Left + 12, box.Top + 14), Ink.Dim, 10.5);

        var mid = box.Top + 66;
        var eye = new Point(box.Left + 28, mid);
        var hole = new Point(box.Left + 150, mid);
        var u = Math.Abs(lens.Y) < 1e-9 && Math.Abs(lens.X) < 1e-9 ? 0 : new Vector(lens.X, lens.Y).Length / EinsteinRadius;

        // The source sits u Einstein radii off the line from us through the hole (drawn up), the images at the two roots.
        const double unit = 18;
        var src = new Point(box.Right - 22, mid - (Math.Min(u, 2.2) * unit * 1.9));
        dc.DrawLine(FadedPen(Ink.Text, 0.25, 1, dashed: true), eye, new Point(box.Right - 10, mid));
        dc.DrawEllipse(Brushes.Black, FadedPen(Ink.Text, 0.9, 1), hole, 4, 4);
        dc.DrawEllipse(FadedBrush(Ink.Gold, 1), null, src, 4, 4);
        DrawEye(dc, eye);

        if (LensOn)
        {
            var (outer, inner, _, _) = BlackHoleModel.LensImages(Math.Max(u, 1e-3));
            foreach (var root in new[] { outer, inner })
            {
                var pass = new Point(hole.X, mid - (root * unit));
                dc.DrawLine(FadedPen(Ink.Cyan, 0.9, 1.4), src, pass);
                dc.DrawLine(FadedPen(Ink.Cyan, 0.9, 1.4), pass, eye);

                // Where we think the light comes from: straight back along the last part.
                var back = eye + ((pass - eye) * 1.9);
                dc.DrawLine(FadedPen(Ink.Cyan, 0.35, 1, dashed: true), pass, back);
            }
        }
        else
        {
            dc.DrawLine(FadedPen(Ink.Cyan, 0.9, 1.4), src, eye);
        }

        DrawText(dc, Loc("Hole_Side_Us"), new Point(eye.X, box.Bottom - 12), Ink.Dim, 10, TextAnchor.Center);
        DrawText(dc, Loc("Hole_Side_Hole"), new Point(hole.X, box.Bottom - 12), Ink.Dim, 10, TextAnchor.Center);
        DrawText(dc, Loc("Hole_Side_Source"), new Point(box.Right - 30, box.Bottom - 12), Ink.Dim, 10, TextAnchor.Center);
    }

    private void DrawEye(PaintContext dc, Point at)
    {
        var pen = FadedPen(Ink.Text, 0.9, 1.2);
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(at + new Vector(-8, 0), false);
            c.QuadraticBezierTo(at + new Vector(0, -8), at + new Vector(8, 0));
            c.QuadraticBezierTo(at + new Vector(0, 8), at + new Vector(-8, 0));

            c.EndFigure(true);
        }
        dc.DrawGeometry(null, pen, g);
        dc.DrawEllipse(FadedBrush(Ink.Text, 0.9), null, at, 2.5, 2.5);
    }

    /// <summary>Moves the hole to a point on the screen (it can go anywhere in view).</summary>
    private void MoveLensTo(Point screen)
    {
        var (origin, upp) = LensLayout(Stage());
        var x = Math.Clamp((screen.X - origin.X) * upp, -TrackEnd, TrackEnd);
        var y = Math.Clamp((origin.Y - screen.Y) * upp, -0.98, 0.98);
        SetCurrentValue(LensXProperty, x);
        SetCurrentValue(LensYProperty, y);
    }
}
