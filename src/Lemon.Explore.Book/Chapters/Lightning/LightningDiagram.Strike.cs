using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Lightning;

/// <summary>
/// Strike mode: the last 2 km under a thundercloud, the lowest 100-odd metres to scale and the rest squeezed. A stepped
/// leader feels its way down in jumps, branching; when its tip comes within striking distance of something, a streamer
/// rises from that thing to meet it (the rolling sphere shows what it can and cannot reach), and the return stroke
/// races up the channel. Later strokes follow the same channel after a dart leader. Then the thunder sets off toward
/// you at 343 m/s, shown in the strip below.
/// </summary>
public sealed partial class LightningDiagram
{
    private const double StrikeHalfWidth = 110;

    private double _strikeTime;
    private StrikeModel.Strike? _strike;
    private (double Reach, bool Rod, double[] Xs, double[] Heights)? _sphereZone;

    private StrikeModel.Strike CurrentStrike => _strike ??= StrikeModel.Leader(StrikeSeed, PeakCurrent, LightningRod);

    private double StrikeLength => StrikeTimeline.Length(Strokes, Distance);

    private void OnStrikeChanged()
    {
        _strike = null;
        _sphereZone = null;
        _strikeTime = 0;
        Push(StrikeTimeProperty, 0.0);
    }

    private void OnStrikeTimeChanged(double value)
    {
        if (!_pushing) _strikeTime = Math.Clamp(value, 0, StrikeLength);
    }

    /// <summary>The flash runs to the end of its thunder, then starts again.</summary>
    private void StepStrikeClock(double step)
    {
        _strikeTime += step;
        if (_strikeTime > StrikeLength) _strikeTime = 0;
        Push(StrikeTimeProperty, Math.Round(_strikeTime, 3));
    }

    #region layout

    /// <remarks>
    /// Up to <see cref="Linear"/> metres everything is to scale. Above, up to the cloud, heights are squeezed to
    /// <see cref="UpperScale"/> px/m (easing into it over <see cref="Blend"/> metres), and the leader's sideways
    /// wandering (measured from where its channel crosses <see cref="Linear"/>, <see cref="Pivot"/>) a little less, so
    /// the channel keeps its look of a bolt.
    /// </remarks>
    private readonly record struct StrikeView(Rect Scene, double Ground, double CentreX, double Scale, double Linear, double LinearPx, Rect Strip, double Pivot)
    {
        private const double Blend = 30;

        public double UpperScale => (Ground - LinearPx - Scene.Top - 26 - (Blend * Scale / 2)) / ((Blend / 2) + StrikeModel.CloudBase - Linear - Blend);

        /// <summary>How far through the easing a height is (0 below it, 1 above), and its smoothstep.</summary>
        private (double T, double S) Ease(double h)
        {
            var t = Math.Clamp((h - Linear) / Blend, 0, 1);
            return (t, t * t * (3 - (2 * t)));
        }

        public double Y(double h)
        {
            if (h <= Linear) return Ground - (h * Scale);
            var (t, _) = Ease(h);

            // The vertical scale eases from Scale to UpperScale (the integral of a smoothstep is t³ − t⁴/2).
            var eased = Blend * ((Scale * t) + ((UpperScale - Scale) * ((t * t * t) - (t * t * t * t / 2))));
            var beyond = Math.Max(0, h - Linear - Blend) * UpperScale;
            return Ground - LinearPx - eased - beyond;
        }

        /// <summary>The x of something on (or near) the ground.</summary>
        public double X(double x) => CentreX + (x * Scale);

        public Point P(double x, double h)
        {
            var (_, s) = Ease(h);
            var k = Mix(Scale, UpperScale, s) * Mix(1, 2.5, s);
            return new Point(CentreX + (Pivot * Scale) + ((x - Pivot) * k), Y(h));
        }
    }

    private StrikeView StrikeLayout()
    {
        var main = CurrentStrike.Main;
        var stage = Stage();
        var strip = new Rect(stage.Left + 40, stage.Bottom - 50, Math.Max(100, stage.Width - 80), 44);
        var scene = new Rect(stage.Left + 58, stage.Top + 4, Math.Max(100, stage.Width - 58 - 24), Math.Max(100, strip.Top - stage.Top - 48));
        var linearPx = scene.Height * 0.58;
        var scale = Math.Min(scene.Width / (2 * StrikeHalfWidth), linearPx / 100);
        var linear = linearPx / scale;
        var pivot = main[^1].X;
        for (var i = 1; i < main.Count; i++)
        {
            if (main[i].H > linear) continue;
            var t = (main[i - 1].H - linear) / Math.Max(1e-9, main[i - 1].H - main[i].H);
            pivot = Mix(main[i - 1].X, main[i].X, t);
            break;
        }

        return new StrikeView(scene, scene.Bottom, scene.Left + (scene.Width / 2), scale, linear, linearPx, strip, pivot);
    }

    #endregion

    #region drawing

    private void DrawStrikeMode(PaintContext dc, double width, double height)
    {
        var view = StrikeLayout();
        var strike = CurrentStrike;
        var (part, f, real) = StrikeTimeline.At(_strikeTime, Strokes);
        var bright = StrikeBrightness(part, f);

        dc.DrawRectangle(StrikeInk.Sky, null, new Rect(0, 0, width, height));
        if (bright > 0.02) dc.DrawRectangle(FadedBrush(Ink.Bolt, 0.25 * bright), null, new Rect(0, 0, width, height));

        DrawCloudBase(dc, view, bright);
        DrawStrikeAxis(dc, view);
        if (ShowSphere) DrawSphereZone(dc, view);
        DrawLand(dc, view, bright);
        DrawThings(dc, view, strike, part, bright);
        DrawChannel(dc, view, strike, part, f);
        DrawThunderStrip(dc, view, part, real);
        if (ShowLabels) DrawStrikeLabels(dc, view, strike, part, f);
    }

    /// <summary>How brightly the flash lights the scene: full at a stroke, fading in the glow after it.</summary>
    private static double StrikeBrightness(StrikeTimeline.Part part, double f) => part.Phase switch
    {
        StrikeTimeline.Phase.Stroke => (part.Stroke == 0 ? 1 : 0.75) * Smooth(0, 0.15, f),
        StrikeTimeline.Phase.Glow => (part.Stroke == 0 ? 1 : 0.75) * Math.Exp(-f * 4),
        StrikeTimeline.Phase.Dart => 0.08,
        _ => 0,
    };

    private void DrawCloudBase(PaintContext dc, StrikeView view, double bright)
    {
        var y = view.Y(StrikeModel.CloudBase);
        var light = Quantize(Lerp(Color.FromRgb(0x4A, 0x50, 0x60), Ink.Bolt, 0.5 * bright));
        var dark = Quantize(Lerp(Color.FromRgb(0x1E, 0x22, 0x2C), Color.FromRgb(0x8A, 0x86, 0xB8), 0.6 * bright));
        var balls = new List<(Point, double)>();
        var n = 0;
        for (var x = view.Scene.Left - 30; x <= view.Scene.Right + 30; x += 26, n++)
        {
            balls.Add((new Point(x + (Hash(n, 1.3) * 10), y - 6 - (Hash(n, 2.3) * 14)), 20 + (Hash(n, 3.3) * 12)));
        }

        dc.DrawRectangle(new SolidColorBrush(dark), null, new Rect(0, 0, ActualWidth, y - 20));
        DrawPuffs(dc, balls, y - 40, y + 10, 1, dark, light);
    }

    private void DrawStrikeAxis(PaintContext dc, StrikeView view)
    {
        var x = view.Scene.Left - 8;
        var last = double.NegativeInfinity;
        foreach (var h in new[] { 2000.0, 1500, 1000, 500, 100, 75, 50, 25, 0 })
        {
            if (h > view.Linear && h < 400) continue;
            var y = view.Y(h);

            // In a short window the squeezed heights crowd together: skip those that would overlap the one above.
            if (y - last < 18) continue;
            last = y;
            DrawTextRight(dc, Number(h, "0"), new Point(x, y), Ink.Dim, 10);
            dc.DrawLine(Ink.Frame, new Point(x + 3, y), new Point(x + 7, y));
        }

        DrawTextRight(dc, "m", new Point(x, view.Y(StrikeModel.CloudBase) + 16), Ink.Dim, 10);

        // Where the scale changes.
        var yl = view.Y(view.Linear);
        dc.DrawLine(StrikeInk.ScaleBreak, new Point(view.Scene.Left, yl), new Point(view.Scene.Right, yl));
        if (ShowLabels) AddBoxTarget("squeezed", DrawText(dc, Loc("Lightning_Strike_Squeezed"), new Point(view.Scene.Left + 6, yl - 9), Ink.Dim, 10));
    }

    /// <summary>The zone a sphere of the striking distance, rolled over everything, cannot touch: what lightning will not reach.</summary>
    private void DrawSphereZone(PaintContext dc, StrikeView view)
    {
        var reach = StrikeModel.StrikingDistance(PeakCurrent);
        if (_sphereZone is not { } zone || zone.Reach != reach || zone.Rod != LightningRod)
        {
            var xs = Enumerable.Range(0, 221).Select(i => -StrikeHalfWidth + i).ToArray();
            zone = (reach, LightningRod, xs, StrikeModel.Protected(xs, StrikeModel.Things(LightningRod), reach));
            _sphereZone = zone;
        }

        var points = new List<Point> { view.P(zone.Xs[0], 0) };
        for (var i = 0; i < zone.Xs.Length; i++) points.Add(view.P(zone.Xs[i], Math.Min(zone.Heights[i], view.Linear)));
        points.Add(view.P(zone.Xs[^1], 0));
        dc.DrawGeometry(StrikeInk.Safe, null, Shape([.. points]));
        var edge = Line(points.Skip(1).Take(points.Count - 2));
        if (Glows("zone")) dc.DrawGeometry(null, GlowLine, edge);
        dc.DrawGeometry(null, StrikeInk.SafeEdge, edge);

        // The zone is a target where it has some height, every few metres along its edge.
        for (var i = 0; i < zone.Xs.Length; i += 6)
        {
            var h = Math.Min(zone.Heights[i], view.Linear);
            if (h > 2) _targets.Add("zone", view.P(zone.Xs[i], h / 2), Math.Max(6, h * view.Scale / 2));
        }
    }

    private void DrawLand(PaintContext dc, StrikeView view, double bright)
    {
        var top = view.Ground;
        var color = Quantize(Lerp(Color.FromRgb(0x2A, 0x44, 0x26), Color.FromRgb(0x7A, 0x9A, 0x6A), 0.5 * bright));
        dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(view.Scene.Left - 58, top, view.Scene.Width + 82, 12));
        dc.DrawRectangle(StrikeInk.Soil, null, new Rect(view.Scene.Left - 58, top + 12, view.Scene.Width + 82, 10));
    }

    private void DrawThings(PaintContext dc, StrikeView view, StrikeModel.Strike strike, StrikeTimeline.Part part, double bright)
    {
        var s = view.Scale;
        var lit = bright * 0.6;

        // The tree.
        var tree = view.P(-70, 0);
        dc.DrawRectangle(StrikeInk.Trunk, null, new Rect(tree.X - (0.6 * s), view.Y(6), 1.2 * s, 6 * s));
        dc.DrawEllipse(new SolidColorBrush(Quantize(Lerp(Color.FromRgb(0x2A, 0x5E, 0x30), Color.FromRgb(0x8A, 0xC8, 0x8A), lit))), null, view.P(-70, 10.5), 6 * s, 5.5 * s);
        _targets.Add("tree", view.P(-70, 9), 6 * s);

        // The house, its roof, and its rod with the conductor down to an earth rod.
        var wall = Quantize(Lerp(Color.FromRgb(0x8A, 0x7A, 0x6A), Color.FromRgb(0xE8, 0xE0, 0xD8), lit));
        dc.DrawRectangle(new SolidColorBrush(wall), null, new Rect(view.X(7), view.Y(6), 16 * s, 6 * s));
        dc.DrawRectangle(StrikeInk.Window, null, new Rect(view.X(10), view.Y(4.5), 3 * s, 2 * s));
        dc.DrawRectangle(StrikeInk.Window, null, new Rect(view.X(17), view.Y(4.5), 3 * s, 2 * s));
        dc.DrawGeometry(StrikeInk.Roof, null, Shape(view.P(6, 6), view.P(15, 9), view.P(24, 6)));
        _targets.AddBox("house", new Rect(view.P(6, 9), view.P(24, 0)));
        if (LightningRod)
        {
            var current = (part.Phase is StrikeTimeline.Phase.Stroke or StrikeTimeline.Phase.Glow) && strike.Target == StrikeTarget.Rod ? bright : 0;
            var pen = current > 0.05 ? FadedPen(Ink.Gold, 0.6 + (0.4 * current), 2.4) : StrikeInk.Conductor;
            dc.DrawLine(pen, view.P(15, 9), view.P(15, 14));
            dc.DrawGeometry(null, pen, Line([view.P(15, 9), view.P(23.5, 6.2), view.P(23.5, 0), view.P(23.5, -1.8)]));
            _targets.Add("lrod", view.P(15, 12), Math.Max(7, 2.5 * s));
        }

        // The person out in the open.
        var foot = view.P(78, 0);
        var head = view.P(78, 1.55);
        dc.DrawLine(StrikeInk.Person, foot, view.P(78, 1.3));
        dc.DrawEllipse(StrikeInk.Skin, null, head, Math.Max(1.5, 0.15 * s), Math.Max(1.5, 0.15 * s));
        _targets.Add("person", view.P(78, 0.9), 8);
    }

    /// <summary>The leader, the streamers, the strokes, by the part of the flash.</summary>
    private void DrawChannel(PaintContext dc, StrikeView view, StrikeModel.Strike strike, StrikeTimeline.Part part, double f)
    {
        var last = strike.Main.Count - 2;
        var target = new Point(strike.TargetX, strike.TargetTop);
        var main = strike.Main.Select(n => view.P(n.X, n.H)).ToList();
        main.Add(view.P(target.X, target.Y));

        switch (part.Phase)
        {
            case StrikeTimeline.Phase.Leader:
            {
                var step = f * Math.Max(1, last);
                DrawGrowing(dc, view, strike.Main.Take(last + 1).ToList(), step, 0.55, 1.3);
                foreach (var branch in strike.Branches) DrawGrowing(dc, view, branch.Nodes, step, 0.4, 1);
                var tip = PointAt(view, strike.Main.Take(last + 1).ToList(), step);
                dc.DrawEllipse(Glow(Ink.Violet, 0.9), null, tip, 12, 12);
                _targets.Add("leader", tip, 12);
                break;
            }

            case StrikeTimeline.Phase.Attach:
            {
                DrawGrowing(dc, view, strike.Main.Take(last + 1).ToList(), last, 0.55, 1.3);
                foreach (var branch in strike.Branches) DrawGrowing(dc, view, branch.Nodes, last, 0.4, 1);
                var junction = main[^2];
                var down = Mix(main[last], junction, f);
                var up = Mix(main[^1], junction, f);
                _batch.Add(Ink.Violet, 0.7, [main[last], down]);
                _batch.Add(Ink.Cyan, 0.85, [main[^1], up]);

                // Others reach up too, and fall short.
                var tipNode = strike.Main[last];
                foreach (var thing in StrikeModel.Things(LightningRod))
                {
                    if (thing.Kind == strike.Target || thing.Kind == StrikeTarget.House && strike.Target == StrikeTarget.Rod) continue;
                    var dx = tipNode.X - thing.X;
                    var dh = tipNode.H - thing.Top;
                    var distance = Math.Sqrt((dx * dx) + (dh * dh));
                    var reach = StrikeModel.StrikingDistance(PeakCurrent);
                    if (distance > reach * 1.7) continue;
                    var from = view.P(thing.X, thing.Top);
                    _batch.Add(Ink.Cyan, 0.45, [from, Mix(from, view.P(tipNode.X, tipNode.H), f * 0.25)]);
                }

                _batch.Draw(dc, this, 1.4);
                dc.DrawEllipse(Glow(Ink.Violet, 0.9), null, down, 12, 12);
                dc.DrawEllipse(Glow(Ink.Cyan, 0.8), null, up, 9, 9);
                if (ShowSphere)
                {
                    var r = StrikeModel.StrikingDistance(PeakCurrent) * view.Scale;
                    if (Glows("sphere")) dc.DrawEllipse(null, GlowLine, main[last], r, r);
                    dc.DrawEllipse(null, StrikeInk.Sphere, main[last], r, r);
                    for (var k = 0; k < 16; k++) _targets.Add("sphere", main[last] + new Vector(Math.Cos(k * Math.PI / 8) * r, Math.Sin(k * Math.PI / 8) * r), 7);
                }

                _targets.Add("streamer", Mix(main[^1], up, 0.5), 10);

                break;
            }

            case StrikeTimeline.Phase.Stroke:
            {
                // The return stroke: a wave of light racing up from the ground.
                var front = Smooth(0, 0.55, f);
                var lengths = Cumulative(main);
                var total = lengths[0];
                var litFrom = total * (1 - front);
                DrawLitPath(dc, main, lengths, litFrom, part.Stroke == 0 ? 1 : 0.8);
                if (part.Stroke == 0)
                {
                    foreach (var branch in strike.Branches)
                    {
                        if (view.Y(branch.Nodes[0].H) < PathY(main, lengths, litFrom)) continue;
                        var points = branch.Nodes.Select(n => view.P(n.X, n.H)).ToArray();
                        _batch.Add(Ink.Bolt, 0.6, points);
                    }

                    _batch.Draw(dc, this, 1.6);
                }

                if (front < 1) dc.DrawEllipse(Glow(Ink.White, 1), null, PathPoint(main, lengths, litFrom), 16, 16);
                _targets.Add(part.Stroke == 0 ? "stroke" : "later", PathPoint(main, lengths, Math.Max(litFrom, lengths[0] * 0.35)), 14);
                DrawGroundSplash(dc, view, strike, f);
                break;
            }

            case StrikeTimeline.Phase.Glow:
            {
                var fade = Math.Exp(-f * 3.5);
                if (fade > 0.03)
                {
                    _batch.Add(Ink.Violet, 0.5 * fade, [.. main]);
                    _batch.Draw(dc, this, 6 * fade + 1);
                    _batch.Add(Ink.Bolt, fade, [.. main]);
                    _batch.Draw(dc, this, 2);
                }

                if (part.Stroke == 0 && f < 0.3)
                {
                    foreach (var branch in strike.Branches) _batch.Add(Ink.Bolt, 0.5 * (1 - (f / 0.3)), branch.Nodes.Select(n => view.P(n.X, n.H)).ToArray());
                    _batch.Draw(dc, this, 1.2);
                }

                break;
            }

            case StrikeTimeline.Phase.Dart:
            {
                // The dart leader: a bright tip sliding down the old channel, no branches.
                var lengths = Cumulative(main);
                var at = lengths[0] * f;
                _batch.Add(Ink.Violet, 0.12, [.. main]);
                _batch.Draw(dc, this, 2);
                var points = new List<Point>();
                for (var i = 0; i < main.Count; i++)
                {
                    if (lengths[0] - lengths[i] > at) break;
                    points.Add(main[i]);
                }

                var tip = PathPoint(main, lengths, lengths[0] - at);
                points.Add(tip);
                _batch.Add(Ink.Bolt, 0.55, [.. points]);
                _batch.Draw(dc, this, 1.6);
                dc.DrawEllipse(Glow(Ink.Bolt, 1), null, tip, 11, 11);
                _targets.Add("dart", tip, 12);
                break;
            }
        }
    }

    /// <summary>Lengths along the path from each point to its end (the ground), screen px.</summary>
    private static double[] Cumulative(List<Point> path)
    {
        var lengths = new double[path.Count];
        for (var i = path.Count - 2; i >= 0; i--) lengths[i] = lengths[i + 1] + ((Vector)(path[i + 1] - path[i])).Length;
        return lengths;
    }

    /// <summary>The point on the path at <paramref name="fromEnd"/> px from its end.</summary>
    private static Point PathPoint(List<Point> path, double[] lengths, double fromEnd)
    {
        for (var i = path.Count - 2; i >= 0; i--)
        {
            if (lengths[i] < fromEnd) continue;
            var span = lengths[i] - lengths[i + 1];
            var t = span < 1e-9 ? 0 : (fromEnd - lengths[i + 1]) / span;
            return Mix(path[i + 1], path[i], t);
        }

        return path[0];
    }

    private static double PathY(List<Point> path, double[] lengths, double fromEnd) => PathPoint(path, lengths, fromEnd).Y;

    /// <summary>The channel lit from the ground up to <paramref name="fromEnd"/>: a violet halo and a white core.</summary>
    private void DrawLitPath(PaintContext dc, List<Point> path, double[] lengths, double fromEnd, double strength)
    {
        var lit = new List<Point> { PathPoint(path, lengths, fromEnd) };
        var dim = new List<Point>();
        for (var i = 0; i < path.Count; i++)
        {
            if (lengths[i] <= fromEnd) lit.Add(path[i]);
            else dim.Add(path[i]);
        }

        if (dim.Count > 0)
        {
            dim.Add(lit[0]);
            _batch.Add(Ink.Violet, 0.5, [.. dim]);
            _batch.Draw(dc, this, 1.3);
        }

        if (lit.Count < 2) return;
        _batch.Add(Ink.Violet, 0.45 * strength, [.. lit]);
        _batch.Draw(dc, this, 10);
        _batch.Add(Ink.Bolt, 0.85 * strength, [.. lit]);
        _batch.Draw(dc, this, 4);
        _batch.Add(Ink.White, strength, [.. lit]);
        _batch.Draw(dc, this, 1.8);
    }

    /// <summary>Nodes reached by <paramref name="step"/>, the last one only part of the way.</summary>
    private void DrawGrowing(PaintContext dc, StrikeView view, IReadOnlyList<StrikeModel.Node> nodes, double step, double alpha, double thickness)
    {
        var points = new List<Point>();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Step <= step)
            {
                points.Add(view.P(nodes[i].X, nodes[i].H));
                continue;
            }

            if (i > 0 && nodes[i - 1].Step <= step)
            {
                var t = Math.Clamp(step - nodes[i - 1].Step, 0, 1);
                points.Add(Mix(view.P(nodes[i - 1].X, nodes[i - 1].H), view.P(nodes[i].X, nodes[i].H), t));
            }

            break;
        }

        if (points.Count < 2) return;
        _batch.Add(Ink.Violet, alpha, [.. points]);
        _batch.Draw(dc, this, thickness);
    }

    private static Point PointAt(StrikeView view, IReadOnlyList<StrikeModel.Node> nodes, double step)
    {
        var i = Math.Clamp((int)Math.Floor(step), 0, nodes.Count - 1);
        if (i >= nodes.Count - 1) return view.P(nodes[^1].X, nodes[^1].H);
        var t = step - i;
        return Mix(view.P(nodes[i].X, nodes[i].H), view.P(nodes[i + 1].X, nodes[i + 1].H), t);
    }

    /// <summary>Where the current goes into the ground it spreads out in rings: the ground's voltage falls off with distance (step voltage).</summary>
    private void DrawGroundSplash(PaintContext dc, StrikeView view, StrikeModel.Strike strike, double f)
    {
        var x = strike.Target switch
        {
            StrikeTarget.Rod => 23.5,
            StrikeTarget.House => strike.TargetX,
            _ => strike.TargetX,
        };
        var at = view.P(x, 0);
        for (var k = 0; k < 3; k++)
        {
            var r = ((f * 1.2) + (k * 0.25)) * 18 * view.Scale;
            var alpha = Math.Max(0, 0.7 - (r / (40 * view.Scale)));
            if (alpha > 0.03) dc.DrawEllipse(null, FadedPen(Ink.Gold, alpha, 1.5), at, r, r * 0.12);
        }

        if (strike.Target is StrikeTarget.Tree or StrikeTarget.House or StrikeTarget.Person)
        {
            // Without a rod the current takes its own way: through the wood, the roof, or a body.
            dc.DrawEllipse(Glow(Ink.Orange, 0.8), null, view.P(strike.TargetX, strike.TargetTop), 18, 18);
        }
    }

    /// <summary>The strip below: the strike on the left, you on the right, the light there at once and the sound on its way.</summary>
    private void DrawThunderStrip(PaintContext dc, StrikeView view, StrikeTimeline.Part part, double real)
    {
        var strip = view.Strip;
        dc.DrawRoundedRectangle(StrikeInk.StripBack, Ink.Frame, strip, 10, 10);
        _targets.AddBox("strip", strip);
        var km = Math.Max(0.1, Distance);
        var x0 = strip.Left + 40;
        var x1 = strip.Right - 60;
        double X(double d) => x0 + (d / km * (x1 - x0));
        var y = strip.Top + (strip.Height * 0.62);

        dc.DrawLine(Ink.Frame, new Point(x0, y), new Point(x1, y));
        var tick = km > 8 ? 2 : km > 3 ? 1 : 0.5;
        for (var d = 0.0; d <= km + 1e-9; d += tick)
        {
            dc.DrawLine(Ink.Frame, new Point(X(d), y - 3), new Point(X(d), y + 3));
            DrawText(dc, Number(d, tick < 1 ? "0.0" : "0"), new Point(X(d), y + 10), Ink.Dim, 9.5, center: true);
        }

        DrawText(dc, "km", new Point(x1 + 14, y + 10), Ink.Dim, 9.5);

        // The bolt at the left, you at the right, lit when the flash is.
        var bright = StrikeBrightness(part, StrikeTimeline.At(_strikeTime, Strokes).Fraction);
        var bolt = new Point(x0 - 22, y - 2);
        dc.DrawGeometry(FadedBrush(Ink.Gold, 0.5 + (0.5 * bright)), null, Shape(bolt + new Vector(2, -11), bolt + new Vector(-4, 1), bolt + new Vector(0, 1), bolt + new Vector(-2, 10), bolt + new Vector(5, -3), bolt + new Vector(1, -3), bolt + new Vector(4, -11)));
        var you = new Point(x1 + 32, y - 4);
        if (bright > 0.05) dc.DrawEllipse(Glow(Ink.Bolt, bright), null, you, 18, 18);
        DrawStickFigure(dc, you);

        var since = real - StrikeTimeline.FirstStrokeReal;
        if (since < 0)
        {
            if (ShowLabels) DrawText(dc, LocFormat("Lightning_Strike_Strip_Wait", Number(StrikeModel.ThunderDelay(km), "0.0")), new Point(x0 + 6, strip.Top + 11), Ink.Dim, 10.5);
            return;
        }

        var heard = since * StrikeModel.SoundSpeed / 1000;
        if (heard < km)
        {
            var fx = X(heard);
            dc.DrawRectangle(StrikeInk.Sound, null, new Rect(x0, y - 7, Math.Max(0, fx - x0), 14));
            for (var k = 0; k < 3; k++)
            {
                var r = 6 + (k * 4);
                dc.DrawGeometry(null, FadedPen(Ink.Cyan, 0.9 - (k * 0.25), 1.4), Arc(new Point(fx - 10, y), r));
            }

            DrawPill(dc, LocFormat("Lightning_Strike_Strip_Counting", Number(Math.Floor(since), "0"), Number(heard, "0.0")), new Point(Math.Clamp(fx, x0 + 70, x1 - 70), strip.Top + 11), Ink.Cyan, 10.5);
        }
        else
        {
            // Rumbling: sound from further up the channel keeps arriving for a few seconds.
            var rumble = since - StrikeModel.ThunderDelay(km);
            var loud = Math.Max(0, 1 - (rumble / StrikeTimeline.Rumble(km)));
            for (var k = 0; k < 3; k++)
            {
                var r = 8 + (Frac((_t * 1.6) + (k / 3.0)) * 16);
                if (loud > 0) dc.DrawEllipse(null, FadedPen(Ink.Cyan, loud * (1 - ((r - 8) / 16)), 1.4), you, r, r);
            }

            DrawPill(dc, LocFormat("Lightning_Strike_Strip_Heard", Number(StrikeModel.ThunderDelay(km), "0.0"), Number(km, "0.0"), Number(StrikeModel.ThunderDelay(km) / 3, "0.0")), new Point((x0 + x1) / 2, strip.Top + 11), Ink.Gold, 10.5);
        }
    }

    /// <summary>A short arc bulging right: a sound front.</summary>
    private static StreamGeometry Arc(Point centre, double r)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(centre + new Vector(r * Math.Cos(-0.8), r * Math.Sin(-0.8)), false);
            context.ArcTo(centre + new Vector(r * Math.Cos(0.8), r * Math.Sin(0.8)), new Size(r, r), 0, false, SweepDirection.Clockwise);

            context.EndFigure(false);
        }
        return geometry;
    }

    private static void DrawStickFigure(PaintContext dc, Point at)
    {
        var pen = StrikeInk.PersonIcon;
        dc.DrawEllipse(StrikeInk.Skin, null, at + new Vector(0, -9), 3.5, 3.5);
        dc.DrawLine(pen, at + new Vector(0, -5), at + new Vector(0, 5));
        dc.DrawLine(pen, at + new Vector(0, -3), at + new Vector(-5, 2));
        dc.DrawLine(pen, at + new Vector(0, -3), at + new Vector(5, 2));
        dc.DrawLine(pen, at + new Vector(0, 5), at + new Vector(-4, 12));
        dc.DrawLine(pen, at + new Vector(0, 5), at + new Vector(4, 12));
    }

    private void DrawStrikeLabels(PaintContext dc, StrikeView view, StrikeModel.Strike strike, StrikeTimeline.Part part, double f)
    {
        // The labels under the things, pushed apart when the window is narrow.
        var below = view.Ground + 32;
        var tree = DrawPill(dc, Loc("Lightning_Strike_Tree"), new Point(view.X(-70), below), Ink.Text, 10.5);
        AddBoxTarget("tree", tree);
        var houseText = Loc(LightningRod ? "Lightning_Strike_HouseRod" : "Lightning_Strike_House");
        var houseWidth = _labels.Text(houseText, 10.5, Ink.Text).Width + 12;
        var house = DrawPill(dc, houseText, new Point(Math.Max(view.X(15), tree.Right + 6 + (houseWidth / 2)), below), Ink.Text, 10.5);
        AddBoxTarget("house", house);
        var personText = Loc("Lightning_Strike_Person");
        var personWidth = _labels.Text(personText, 10.5, Ink.Text).Width + 12;
        AddBoxTarget("person", DrawPill(dc, personText, new Point(Math.Max(view.X(78), house.Right + 6 + (personWidth / 2)), below), Ink.Text, 10.5));
        AddBoxTarget("cloudbase", DrawPill(dc, LocFormat("Lightning_Strike_CloudBase", Number(StrikeModel.CloudBase / 1000, "0")), new Point(view.Scene.Left + 70, view.Y(StrikeModel.CloudBase) + 18), Ink.Dim, 10.5));

        // The slow motion: how much real time one second of the picture stands for.
        var slow = part.Phase == StrikeTimeline.Phase.Thunder
            ? Loc("Lightning_Strike_RealTime")
            : LocFormat("Lightning_Strike_Slow", DurationText(part.Real / part.Story));
        AddBoxTarget("slow", DrawPill(dc, slow, new Point(view.Scene.Right - 90, view.Scene.Top + 30), Ink.Gold, 11));

        var last = strike.Main.Count - 2;
        var key = part.Phase switch
        {
            StrikeTimeline.Phase.Leader => "Lightning_Strike_Label_Leader",
            StrikeTimeline.Phase.Attach => "Lightning_Strike_Label_Streamer",
            StrikeTimeline.Phase.Stroke => part.Stroke == 0 ? "Lightning_Strike_Label_Stroke" : "Lightning_Strike_Label_Later",
            StrikeTimeline.Phase.Dart => "Lightning_Strike_Label_Dart",
            _ => null,
        };
        if (key is null) return;
        var node = part.Phase == StrikeTimeline.Phase.Leader
            ? strike.Main[Math.Clamp((int)(f * last), 0, last)]
            : strike.Main[Math.Max(0, last - 3)];
        var anchor = view.P(node.X, node.H);
        var side = anchor.X > view.CentreX ? -1 : 1;
        var id = part.Phase switch
        {
            StrikeTimeline.Phase.Leader => "leader",
            StrikeTimeline.Phase.Attach => "streamer",
            StrikeTimeline.Phase.Stroke => part.Stroke == 0 ? "stroke" : "later",
            _ => "dart",
        };
        AddBoxTarget(id, DrawPill(dc, Loc(key), anchor + new Vector(side * 110, part.Phase == StrikeTimeline.Phase.Leader ? 0 : -20), Ink.Violet, 11));

        if (part.Phase is StrikeTimeline.Phase.Attach or StrikeTimeline.Phase.Stroke)
        {
            AddBoxTarget("hit", DrawPill(dc, LocFormat("Lightning_Strike_Hit", Loc($"Lightning_Target_{strike.Target}")), view.P(strike.TargetX, strike.TargetTop) + new Vector(0, -46), Ink.Gold, 11));
        }
    }

    /// <summary>A short duration in ms or µs, for the slow-motion pill.</summary>
    internal static string DurationText(double seconds) => seconds >= 1e-3
        ? LocFormat("Lightning_Unit_Ms", Number(seconds * 1000, seconds >= 0.01 ? "0" : "0.0"))
        : LocFormat("Lightning_Unit_Us", Number(seconds * 1e6, seconds >= 1e-5 ? "0" : "0.0"));

    #endregion

    private static class StrikeInk
    {
        public static readonly IBrush Sky = SpaceDrawing.LinearGradient(Color.FromRgb(0x10, 0x12, 0x1C), Color.FromRgb(0x1C, 0x22, 0x30), 90);
        public static readonly IBrush Soil = Solid(255, 0x2A, 0x20, 0x18);
        public static readonly IBrush Trunk = Solid(255, 0x5A, 0x40, 0x2A);
        public static readonly IBrush Window = Solid(255, 0xFF, 0xD3, 0x6B);
        public static readonly IBrush Roof = Solid(255, 0x8A, 0x3A, 0x2E);
        public static readonly IPen Conductor = MakePen(Solid(220, 0xB0, 0xB8, 0xC4), 1.6);
        public static readonly IPen Person = MakePen(Solid(255, 0xE8, 0xEC, 0xF2), 1.6);
        public static readonly IPen PersonIcon = MakePen(Solid(255, 0xE8, 0xEC, 0xF2), 1.6);
        public static readonly IBrush Skin = Solid(255, 0xE8, 0xC0, 0x98);
        public static readonly IBrush Safe = Solid(45, 0x8B, 0xE0, 0x8F);
        public static readonly IPen SafeEdge = MakePen(Solid(150, 0x8B, 0xE0, 0x8F), 1, dashed: true);
        public static readonly IPen Sphere = MakePen(Solid(150, 0xFF, 0xD3, 0x6B), 1.2, dashed: true);
        public static readonly IPen ScaleBreak = MakePen(Solid(50, 255, 255, 255), 1, dashed: true);
        public static readonly IBrush StripBack = Solid(170, 0x08, 0x0C, 0x16);
        public static readonly IBrush Sound = SpaceDrawing.LinearGradient(Color.FromArgb(0, 0x6F, 0xD8, 0xFF), Color.FromArgb(70, 0x6F, 0xD8, 0xFF), 0);
    }
}
