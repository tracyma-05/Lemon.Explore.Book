using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Greenhouse;

/// <summary>
/// Glass-house mode: a small glass greenhouse and a car parked in the sun through a clear day at the shared observer
/// location. Sunlight gets in through the glass; the warm floor glows in the infrared, which the glass soaks up (or a
/// rock-salt roof lets out); warm air rises and is held under the roof unless the vents are open. Temperatures come
/// from <see cref="GlassHouseModel"/>.
/// </summary>
public sealed partial class GreenhouseDiagram
{
    private const double RaySpeed = 320;
    private const double GlassInfraredSpeed = 120;

    private readonly List<GlassRay> _rays = [];
    private readonly List<AirBlob> _airBlobs = [];
    private double _raysDue;
    private double _glowDue;
    private double _blobsDue;

    private enum RayFate
    {
        Vanish,
        GlassFlash,
        InfraredAtGlass,
    }

    private sealed class GlassRay
    {
        public bool Sunlight;
        public Point From;
        public Point To;
        public double Start;
        public double Duration;
        public RayFate Fate;
        public double Phase;
        public double Alpha = 1;

        /// <summary>The hour it was sent at: sunlight from an earlier Sun is dropped when the day is turned by hand.</summary>
        public double Hour;
    }

    private sealed class AirBlob
    {
        public Point At;
        public double Born;
        public bool Cool;
        public bool Leaving;
        public double Size;
    }

    /// <summary>Where things go in glass-house mode.</summary>
    private readonly record struct GlassLayout(Rect Stage, double Ground, Point ArcCenter, double ArcX, double ArcY,
        double HouseLeft, double HouseRight, double Eaves, Point Ridge, Rect Car, Point Post);

    private GlassLayout Glass()
    {
        var stage = Stage();
        var ground = stage.Bottom - Math.Clamp(stage.Height * 0.16, 36, 80);
        var skyHeight = ground - stage.Top;
        var houseWidth = Math.Clamp(stage.Width * 0.34, 200, 420);
        var houseX = stage.Left + (stage.Width * 0.3);
        var houseHeight = Math.Clamp(skyHeight * 0.6, 110, 280);
        var carWidth = Math.Clamp(stage.Width * 0.2, 140, 260);
        var carHeight = carWidth * 0.36;
        var carX = stage.Left + (stage.Width * 0.76);
        return new GlassLayout(
            stage,
            ground,
            new Point(stage.Left + (stage.Width / 2), ground),
            stage.Width * 0.45,
            skyHeight * 0.95,
            houseX - (houseWidth / 2),
            houseX + (houseWidth / 2),
            ground - (houseHeight * 0.58),
            new Point(houseX, ground - houseHeight),
            new Rect(carX - (carWidth / 2), ground - carHeight - 4, carWidth, carHeight),
            new Point(stage.Left + (stage.Width * 0.58), ground));
    }

    private DayOptions Options => new(Latitude, Season, VentsOpen, SaltPane, CarWindowOpen);

    private Point SunPoint(GlassLayout layout, DayState state)
    {
        var declination = GlassHouseModel.Declination(Season, Latitude);
        var across = GlassHouseModel.SunAcross(_hour, Latitude, declination);

        // Facing the Sun at noon: south in the north, so the morning Sun is on the left; the other way round down south.
        if (Latitude < 0) across = -across;
        return new Point(layout.ArcCenter.X + (across * layout.ArcX), layout.Ground - (Math.Sin(state.SunAltitude * Math.PI / 180) * layout.ArcY));
    }

    /// <summary>The roof's vent panels: on each slope near the ridge, hinged at their lower edge.</summary>
    private static (Point Hinge, Point Free)[] Vents(GlassLayout layout)
    {
        var left = new Point(layout.HouseLeft, layout.Eaves);
        var right = new Point(layout.HouseRight, layout.Eaves);
        return [(Mix(left, layout.Ridge, 0.6), Mix(left, layout.Ridge, 0.92)), (Mix(right, layout.Ridge, 0.6), Mix(right, layout.Ridge, 0.92))];
    }

    private static Rect CarWindow(GlassLayout layout)
    {
        var car = layout.Car;
        return new Rect(car.Left + (car.Width * 0.3), car.Top + (car.Height * 0.08), car.Width * 0.24, car.Height * 0.38);
    }

    /// <summary>The greenhouse's glass as a line: up the left wall, over the roof, down the right wall.</summary>
    private static Point[] GlassOutline(GlassLayout layout) =>
    [
        new Point(layout.HouseLeft, layout.Ground),
        new Point(layout.HouseLeft, layout.Eaves),
        layout.Ridge,
        new Point(layout.HouseRight, layout.Eaves),
        new Point(layout.HouseRight, layout.Ground),
    ];

    /// <summary>The first point where a segment crosses the glass, or null.</summary>
    private static Point? CrossGlass(GlassLayout layout, Point a, Point b)
    {
        var outline = GlassOutline(layout);
        Point? best = null;
        var bestT = double.MaxValue;
        for (var i = 0; i < outline.Length - 1; i++)
        {
            var p = outline[i];
            var r = outline[i + 1] - p;
            var s = b - a;
            var denominator = (s.X * r.Y) - (s.Y * r.X);
            if (Math.Abs(denominator) < 1e-9) continue;
            var q = p - a;
            var t = ((q.X * r.Y) - (q.Y * r.X)) / denominator;
            var u = ((q.X * s.Y) - (q.Y * s.X)) / denominator;
            if (t is > 1e-6 and <= 1 && u is >= 0 and <= 1 && t < bestT)
            {
                bestT = t;
                best = a + (s * t);
            }
        }

        return best;
    }

    private void StepGlassHouse(double step)
    {
        if (step <= 0) return;
        var layout = Glass();
        var state = GlassHouseModel.At(Options, _hour);
        var sun = SunPoint(layout, state);

        // Sunlight: more rays the brighter it is; most aimed at the greenhouse, some at the car.
        _rays.RemoveAll(r => r.Sunlight && Math.Abs(r.Hour - _hour) > 0.4);
        _raysDue += step * 10 * state.Sunlight / 1000;
        while (_raysDue >= 1)
        {
            _raysDue -= 1;
            var pick = _random.NextDouble();
            Point target;
            if (pick < 0.62) target = new Point(Mix(layout.HouseLeft + 10, layout.HouseRight - 10, _random.NextDouble()), layout.Ground - 10);
            else if (pick < 0.85) target = new Point(Mix(layout.Car.Left + 10, layout.Car.Right - 10, _random.NextDouble()), layout.Car.Top + (layout.Car.Height * 0.35));
            else target = new Point(Mix(layout.Stage.Left, layout.Stage.Right, _random.NextDouble()), layout.Ground + 2);

            var cross = CrossGlass(layout, sun, target);
            if (cross is { } glass && _random.NextDouble() > GlassHouseModel.PaneTransmission)
            {
                // A glint: some of the light bounces off the pane.
                AddRay(new GlassRay { Sunlight = true, From = sun, To = glass, Fate = RayFate.GlassFlash }, RaySpeed);
            }
            else
            {
                AddRay(new GlassRay { Sunlight = true, From = sun, To = target, Fate = RayFate.Vanish }, RaySpeed);
            }
        }

        // The floor's infrared: the warmer it is, the more (σT⁴).
        _glowDue += step * 3.2 * Math.Pow((state.Floor + 273.15) / 300, 4);
        while (_glowDue >= 1)
        {
            _glowDue -= 1;
            var from = new Point(Mix(layout.HouseLeft + 8, layout.HouseRight - 8, _random.NextDouble()), layout.Ground - 10);
            var angle = (_random.NextDouble() - 0.5) * 1.4;
            var far = from + (new Vector(Math.Sin(angle), -Math.Cos(angle)) * 2000);
            var hit = CrossGlass(layout, from, far);
            if (hit is { } glass) AddRay(new GlassRay { From = from, To = glass, Fate = RayFate.InfraredAtGlass }, GlassInfraredSpeed);
        }

        for (var i = _rays.Count - 1; i >= 0; i--)
        {
            var ray = _rays[i];
            if (_t < ray.Start + ray.Duration) continue;
            _rays.RemoveAt(i);
            if (ray.Fate == RayFate.InfraredAtGlass)
            {
                Vector direction = ray.To - ray.From;
                direction /= direction.Length;
                if (SaltPane)
                {
                    // Rock salt is clear to infrared: the ray goes on out to the sky.
                    AddRay(new GlassRay { From = ray.To, To = ray.To + (direction * 260), Fate = RayFate.Vanish, Alpha = 0.8 }, GlassInfraredSpeed);
                }
                else
                {
                    // Glass soaks it up and glows itself, half back in and half out (fainter: the glass is cooler).
                    _flashes.Add((ray.To, _t, Ink.Infrared, 5));
                    var back = new Vector(-direction.X + ((_random.NextDouble() - 0.5) * 0.6), -direction.Y);
                    back /= back.Length;
                    if (_random.NextDouble() < 0.5)
                    {
                        var down = ray.To + (back * ((layout.Ground - 10 - ray.To.Y) / Math.Max(0.2, back.Y)));
                        down = down.WithX(Math.Clamp(down.X, layout.HouseLeft + 4, layout.HouseRight - 4));
                        AddRay(new GlassRay { From = ray.To, To = down, Fate = RayFate.Vanish, Alpha = 0.7 }, GlassInfraredSpeed);
                    }
                    else
                    {
                        AddRay(new GlassRay { From = ray.To, To = ray.To + (direction * 120), Fate = RayFate.Vanish, Alpha = 0.45 }, GlassInfraredSpeed);
                    }
                }
            }
            else if (ray.Fate == RayFate.GlassFlash)
            {
                _flashes.Add((ray.To, _t, Ink.White, 5));
                Vector direction = ray.To - ray.From;
                AddRay(new GlassRay { Sunlight = true, From = ray.To, To = ray.To + (new Vector(direction.X, -direction.Y) / direction.Length * 200), Fate = RayFate.Vanish, Alpha = 0.7 },
                    RaySpeed);
            }
        }

        _flashes.RemoveAll(f => _t - f.Time > 0.7);
        if (_rays.Count > 400) _rays.RemoveRange(0, _rays.Count - 400);

        StepAir(layout, state, step);
    }

    private void AddRay(GlassRay ray, double speed)
    {
        ray.Hour = _hour;
        ray.Start = _t;
        ray.Duration = Math.Max(0.05, ((Vector)(ray.To - ray.From)).Length / speed);
        ray.Phase = _random.NextDouble() * Math.PI * 2;
        _rays.Add(ray);
    }

    /// <summary>The roof's underside height at an x inside the greenhouse.</summary>
    private static double RoofAt(GlassLayout layout, double x)
    {
        var t = x <= layout.Ridge.X
            ? (x - layout.HouseLeft) / (layout.Ridge.X - layout.HouseLeft)
            : (layout.HouseRight - x) / (layout.HouseRight - layout.Ridge.X);
        return Mix(layout.Eaves, layout.Ridge.Y, Math.Clamp(t, 0, 1));
    }

    /// <summary>Warm air rising off the floor, gathering under the roof, and leaving by the vents when they are open.</summary>
    private void StepAir(GlassLayout layout, DayState state, double step)
    {
        var lift = Math.Max(0, state.Floor - state.Greenhouse);
        _blobsDue += step * ((lift * 0.5) + (state.Sunlight > 50 ? 0.6 : 0));
        while (_blobsDue >= 1)
        {
            _blobsDue -= 1;
            var x = Mix(layout.HouseLeft + 14, layout.HouseRight - 14, _random.NextDouble());
            _airBlobs.Add(new AirBlob { At = new Point(x, layout.Ground - 14), Born = _t, Size = 5 + (5 * _random.NextDouble()) });
        }

        if (VentsOpen && _random.NextDouble() < step * 1.2)
        {
            // Cooler outside air sinks in through the vents.
            var (hinge, free) = Vents(layout)[_random.Next(2)];
            _airBlobs.Add(new AirBlob { At = Mix(hinge, free, 0.5) + new Vector(0, 8), Born = _t, Cool = true, Size = 5 });
        }

        var vents = Vents(layout);
        var speed = 26 + (lift * 2);
        foreach (var blob in _airBlobs)
        {
            if (blob.Cool)
            {
                blob.At += new Vector(Math.Sin((_t * 2) + blob.Born) * 6 * step, 22 * step);
                continue;
            }

            if (blob.Leaving)
            {
                blob.At += new Vector(Math.Sin((_t * 2) + blob.Born) * 8 * step, -speed * step);
                continue;
            }

            var roof = RoofAt(layout, blob.At.X) + blob.Size + 2;
            if (blob.At.Y > roof)
            {
                blob.At = new Point(blob.At.X + (Math.Sin((_t * 1.7) + (blob.Born * 3)) * 5 * step), Math.Max(roof, blob.At.Y - (speed * step)));
            }
            else
            {
                // Under the roof: drift up the slope towards the ridge.
                var toRidge = Math.Sign(layout.Ridge.X - blob.At.X);
                blob.At = new Point(blob.At.X + (toRidge * speed * 0.6 * step), RoofAt(layout, blob.At.X) + blob.Size + 2);
                if (VentsOpen && vents.Any(v => Math.Abs(blob.At.X - Mix(v.Hinge.X, v.Free.X, 0.5)) < 14)) blob.Leaving = true;
            }
        }

        _airBlobs.RemoveAll(b => _t - b.Born > (b.Leaving ? 9 : 7) || b.At.Y < layout.Stage.Top - 20 || (b.Cool && b.At.Y > layout.Ground - 10));
        if (_airBlobs.Count > 160) _airBlobs.RemoveRange(0, _airBlobs.Count - 160);
    }

    private void DrawGlassHouseMode(PaintContext dc, double width, double height)
    {
        var layout = Glass();
        var state = GlassHouseModel.At(Options, _hour);
        var night = 1 - Smooth(-10, 4, state.SunAltitude);

        DrawSky(dc, layout, state, width, height, night);
        var sun = SunPoint(layout, state);
        if (state.SunAltitude > -2)
        {
            dc.DrawEllipse(GlassInk.SunGlow, null, sun, 40, 40);
            dc.DrawEllipse(FadedBrush(Quantize(Lerp(Color.FromRgb(0xFF, 0x9A, 0x4A), Color.FromRgb(0xFF, 0xF0, 0xB0), Smooth(0, 25, state.SunAltitude))), 1), null, sun, 15, 15);
        }

        // The ground.
        var groundRect = new Rect(0, layout.Ground, width, height - layout.Ground);
        dc.DrawRectangle(GlassInk.Grass, null, groundRect);
        dc.DrawLine(GlassInk.GroundEdge, new Point(0, layout.Ground), new Point(width, layout.Ground));

        DrawGreenhouse(dc, layout, state);
        DrawCar(dc, layout, state);
        DrawPost(dc, layout, state);

        // The rays over everything (sunlight straight, infrared wavy).
        foreach (var ray in _rays)
        {
            if (ray.Sunlight && (Math.Abs(ray.Hour - _hour) > 0.4 || state.SunAltitude <= 0)) continue;
            var progress = Math.Clamp((_t - ray.Start) / ray.Duration, 0, 1);
            var head = Mix(ray.From, ray.To, progress);
            Vector v = ray.To - ray.From;
            if (v.Length < 1e-6) continue;
            var direction = v / v.Length;
            var travelled = ((Vector)(head - ray.From)).Length;
            if (ray.Sunlight)
            {
                var length = Math.Min(20, travelled);
                if (length > 2) _batch.Add(Ink.Sun, 0.9 * ray.Alpha, [head - (direction * length), head]);
            }
            else
            {
                var length = Math.Min(24, travelled);
                if (length > 4) _batch.Add(Ink.Infrared, 0.9 * ray.Alpha, Wiggle(head, direction, length, 8, 3, ray.Phase - (_t * 14)));
            }
        }

        _batch.Draw(dc, this, 1.7);
        foreach (var (at, time, color, size) in _flashes)
        {
            var age = (_t - time) / 0.7;
            dc.DrawEllipse(FadedBrush(color, 0.6 * (1 - age)), null, at, size * (0.6 + age), size * (0.6 + age));
        }

        // Night falls over the ground and what stands on it (the sky is dark already).
        if (night > 0.02)
        {
            var shade = new GeometryGroup { FillRule = FillRule.NonZero };
            shade.Children.Add(new RectangleGeometry(groundRect));
            shade.Children.Add(Shape(GlassOutline(layout)));
            shade.Children.Add(CarOutline(layout));
            dc.DrawGeometry(FadedBrush(Color.FromRgb(0x02, 0x04, 0x0C), 0.45 * night), null, shade);
        }

        DrawGlassLabels(dc, layout, state, sun);
        AddGlassTargets(layout, state, sun);
    }

    /// <summary>The things themselves as targets, after their labels so a walkthrough rings the thing.</summary>
    private void AddGlassTargets(GlassLayout layout, DayState state, Point sun)
    {
        if (state.SunAltitude > -2) _targets.Add("sun", sun, 20);
        var post = layout.Post;
        var height = Math.Min(90, (layout.Ground - layout.Ridge.Y) * 0.7);
        AddBoxTarget("outside", new Rect(post.X - 14, post.Y - height - 6, 28, height + 6));
        AddBoxTarget("car", layout.Car);
        AddBoxTarget("greenhouse", new Rect(layout.HouseLeft, layout.Ridge.Y, layout.HouseRight - layout.HouseLeft, layout.Ground - layout.Ridge.Y));
        AddBoxTarget("floor", new Rect(layout.HouseLeft + 6, layout.Ground - 12, layout.HouseRight - layout.HouseLeft - 12, 12));
        AddBoxTarget("warmair", new Rect(layout.HouseLeft + 12, layout.Ridge.Y + 12, layout.HouseRight - layout.HouseLeft - 24, layout.Eaves - layout.Ridge.Y + 8));
        var window = CarWindow(layout);
        window = window.Inflate(new Thickness(6, 6));
        AddBoxTarget("carwindow", window);
        foreach (var (hinge, free) in Vents(layout))
        {
            var box = new Rect(hinge, free);
            box = box.Inflate(new Thickness(14, 14));
            AddBoxTarget("vents", box);
        }
    }

    private void DrawSky(PaintContext dc, GlassLayout layout, DayState state, double width, double height, double night)
    {
        var day = Smooth(-6, 20, state.SunAltitude);
        var top = Lerp(Color.FromRgb(0x05, 0x08, 0x14), Color.FromRgb(0x2E, 0x6C, 0xC0), day);
        var bottom = Lerp(Color.FromRgb(0x0C, 0x12, 0x24), Color.FromRgb(0x9C, 0xCB, 0xF0), day);

        // Dawn and dusk: an orange glow low down.
        var dusk = Math.Max(0, 1 - (Math.Abs(state.SunAltitude - 1) / 9));
        bottom = Lerp(bottom, Color.FromRgb(0xFF, 0x9A, 0x5A), dusk * 0.7);
        var sky = SpaceDrawing.LinearGradient(Quantize(top), Quantize(bottom), 90);
        dc.DrawRectangle(sky, null, new Rect(0, 0, width, layout.Ground));
        if (night > 0.05) _stars.Draw(dc, width, layout.Ground, RenderScale(), _clock.Elapsed.TotalSeconds, night);
    }

    private void DrawGreenhouse(PaintContext dc, GlassLayout layout, DayState state)
    {
        var left = layout.HouseLeft;
        var right = layout.HouseRight;
        var ground = layout.Ground;
        var inside = Shape(new Point(left, ground), new Point(left, layout.Eaves), layout.Ridge, new Point(right, layout.Eaves), new Point(right, ground));

        // The air inside, tinted by how much warmer it is than outside, warmest under the roof.
        var warm = Math.Clamp((state.Greenhouse - state.Outside) / 25, -0.4, 1);
        var tint = warm >= 0 ? Color.FromRgb(0xFF, 0x7A, 0x4A) : Color.FromRgb(0x5A, 0xA8, 0xFF);
        var strength = Math.Abs(warm);
        var air = SpaceDrawing.LinearGradient(90,
            (Color.FromArgb((byte)(160 * strength), tint.R, tint.G, tint.B), 0),
            (Color.FromArgb((byte)(50 * strength), tint.R, tint.G, tint.B), 1));
        dc.DrawGeometry(GlassInk.Inside, null, inside);
        dc.DrawGeometry(air, null, inside);

        // The bed of soil and the plants.
        var bed = new Rect(left + 6, ground - 12, right - left - 12, 12);
        dc.DrawRectangle(FadedBrush(Quantize(Lerp(Color.FromRgb(0x5A, 0x3E, 0x2A), Color.FromRgb(0xC0, 0x5A, 0x3A), Math.Clamp((state.Floor - 20) / 40, 0, 1))), 1), null, bed);
        var plants = Math.Max(4, (int)((right - left) / 34));
        for (var i = 0; i < plants; i++)
        {
            var x = left + 18 + (i * (right - left - 36) / (plants - 1.0));
            var h = (layout.Eaves - layout.Ridge.Y) * (0.55 + (0.3 * Hash(i, 3.3)));
            var sway = Math.Sin((_t * 0.8) + i) * 1.5;
            dc.DrawLine(GlassInk.Stem, new Point(x, ground - 12), new Point(x + sway, ground - 12 - h));
            for (var k = 0; k < 3; k++)
            {
                var y = ground - 12 - (h * (0.35 + (0.25 * k)));
                var side = k % 2 == 0 ? 1 : -1;
                dc.DrawEllipse(GlassInk.Leaf, null, new Point(x + (side * 7) + sway, y), 7, 3.5);
            }

            if (i % 2 == 0) dc.DrawEllipse(GlassInk.Tomato, null, new Point(x + 4 + sway, ground - 12 - (h * 0.5)), 3, 3);
        }

        // Warm air rising, cool air sinking in.
        foreach (var blob in _airBlobs)
        {
            var age = Math.Clamp((_t - blob.Born) / 7, 0, 1);
            var alpha = (blob.Leaving ? 0.35 : 0.3) * (1 - age) * Math.Min(1, (_t - blob.Born) * 3);
            var color = blob.Cool ? Ink.Cyan : Ink.Orange;
            dc.DrawEllipse(FadedBrush(color, alpha), null, blob.At, blob.Size, blob.Size * 0.85);
        }

        // The panes: glass, or rock salt with a faint crystal sparkle.
        var paneBrush = SaltPane ? GlassInk.Salt : GlassInk.Pane;
        var vents = Vents(layout);
        dc.DrawGeometry(paneBrush, null, Shape(new Point(left, ground), new Point(left, layout.Eaves), new Point(right, layout.Eaves), new Point(right, ground)));
        dc.DrawGeometry(paneBrush, null, Shape(new Point(left, layout.Eaves), layout.Ridge, new Point(right, layout.Eaves)));

        // The frame: walls, roof, glazing bars.
        var frame = GlassInk.Frame;
        foreach (var (a, b) in new[]
                 {
                     (new Point(left, ground), new Point(left, layout.Eaves)),
                     (new Point(right, ground), new Point(right, layout.Eaves)),
                     (new Point(left, layout.Eaves), layout.Ridge),
                     (new Point(right, layout.Eaves), layout.Ridge),
                     (new Point(left, layout.Eaves), new Point(right, layout.Eaves)),
                 })
        {
            dc.DrawLine(frame, a, b);
        }

        for (var i = 1; i < 4; i++)
        {
            var x = Mix(left, right, i / 4.0);
            dc.DrawLine(GlassInk.Bar, new Point(x, ground), new Point(x, layout.Eaves));
            dc.DrawLine(GlassInk.Bar, new Point(x, layout.Eaves), new Point(x, RoofAt(layout, x)));
        }

        // The vents: shut, flush with the roof, or propped open about their hinges by the ridge.
        foreach (var (hinge, free) in vents)
        {
            var open = VentsOpen ? 0.45 : 0;
            var arm = free - hinge;
            // Each panel swings its upper edge up and out: the left one anticlockwise, the right one clockwise.
            var (s, c) = Math.SinCos(open * (hinge.X < layout.Ridge.X ? -1 : 1));
            var turned = new Vector((arm.X * c) - (arm.Y * s), (arm.X * s) + (arm.Y * c));
            if (VentsOpen) dc.DrawLine(GlassInk.Gap, hinge, free);
            dc.DrawLine(GlassInk.Vent, hinge, hinge + turned);
            if (VentsOpen) dc.DrawLine(GlassInk.Bar, Mix(hinge, free, 0.6), hinge + (turned * 0.6));
        }
    }

    /// <summary>The car's outline (cabin, body and wheels), for the night's shade.</summary>
    private static Geometry CarOutline(GlassLayout layout)
    {
        var car = layout.Car;
        var wheel = car.Height * 0.2;
        var bodyTop = car.Top + (car.Height * 0.48);
        var bottom = car.Bottom - (wheel * 0.5);
        var group = new GeometryGroup { FillRule = FillRule.NonZero };
        group.Children.Add(Shape(
            new Point(car.Left + (car.Width * 0.2), bodyTop),
            new Point(car.Left + (car.Width * 0.32), car.Top),
            new Point(car.Left + (car.Width * 0.7), car.Top),
            new Point(car.Left + (car.Width * 0.84), bodyTop)));
        group.Children.Add(new RectangleGeometry(new Rect(car.Left, bodyTop, car.Width, bottom - bodyTop), 8, 8));
        foreach (var x in new[] { car.Left + (car.Width * 0.2), car.Left + (car.Width * 0.8) }) group.Children.Add(SpaceDrawing.Ellipse(new Point(x, bottom), wheel, wheel));
        return group;
    }

    private void DrawCar(PaintContext dc, GlassLayout layout, DayState state)
    {
        var car = layout.Car;
        var wheel = car.Height * 0.2;
        var bodyTop = car.Top + (car.Height * 0.48);
        var bottom = car.Bottom - (wheel * 0.5);

        // Cabin and windows.
        var cabin = Shape(
            new Point(car.Left + (car.Width * 0.2), bodyTop),
            new Point(car.Left + (car.Width * 0.32), car.Top),
            new Point(car.Left + (car.Width * 0.7), car.Top),
            new Point(car.Left + (car.Width * 0.84), bodyTop));
        dc.DrawGeometry(GlassInk.CarBody, GlassInk.CarEdge, cabin);
        var heat = Math.Clamp((state.Car - state.Outside) / 30, 0, 1);
        var windowTint = FadedBrush(Quantize(Lerp(Color.FromRgb(0x2A, 0x3A, 0x52), Color.FromRgb(0xFF, 0x6A, 0x4A), heat)), 0.85);
        var back = Shape(
            new Point(car.Left + (car.Width * 0.25), bodyTop - 2),
            new Point(car.Left + (car.Width * 0.34), car.Top + 5),
            new Point(car.Left + (car.Width * 0.5), car.Top + 5),
            new Point(car.Left + (car.Width * 0.5), bodyTop - 2));
        var front = Shape(
            new Point(car.Left + (car.Width * 0.53), bodyTop - 2),
            new Point(car.Left + (car.Width * 0.53), car.Top + 5),
            new Point(car.Left + (car.Width * 0.68), car.Top + 5),
            new Point(car.Left + (car.Width * 0.79), bodyTop - 2));
        dc.DrawGeometry(windowTint, null, back);
        dc.DrawGeometry(windowTint, null, front);
        if (CarWindowOpen)
        {
            // Open a crack: a dark gap at the top of the rear side window.
            dc.DrawRectangle(GlassInk.CarGap, null, new Rect(car.Left + (car.Width * 0.34), car.Top + 5, car.Width * 0.16, 5));
        }

        dc.DrawGeometry(GlassInk.WindowShine, null, Shape(
            new Point(car.Left + (car.Width * 0.36), car.Top + 7),
            new Point(car.Left + (car.Width * 0.42), car.Top + 7),
            new Point(car.Left + (car.Width * 0.33), bodyTop - 4),
            new Point(car.Left + (car.Width * 0.28), bodyTop - 4)));

        // Body and wheels.
        dc.DrawRoundedRectangle(GlassInk.CarBody, GlassInk.CarEdge, new Rect(car.Left, bodyTop, car.Width, bottom - bodyTop), 8, 8);
        dc.DrawLine(GlassInk.CarLine, new Point(car.Left + (car.Width * 0.52), bodyTop + 3), new Point(car.Left + (car.Width * 0.52), bottom - 4));
        dc.DrawRectangle(GlassInk.Lamp, null, new Rect(car.Right - 6, bodyTop + 5, 5, 6));
        foreach (var x in new[] { car.Left + (car.Width * 0.2), car.Left + (car.Width * 0.8) })
        {
            dc.DrawEllipse(GlassInk.Tyre, null, new Point(x, bottom), wheel, wheel);
            dc.DrawEllipse(GlassInk.Hub, null, new Point(x, bottom), wheel * 0.45, wheel * 0.45);
        }

        // A hot car shimmers.
        if (heat > 0.25)
        {
            for (var i = 0; i < 4; i++)
            {
                var x = car.Left + (car.Width * (0.35 + (0.1 * i)));
                var points = Enumerable.Range(0, 10).Select(k => new Point(x + (Math.Sin((_t * 3) + (k * 0.8) + i) * 3), car.Top - 4 - (k * 3))).ToArray();
                dc.DrawGeometry(null, FadedPen(Ink.Orange, 0.35 * heat, 1.3), Line(points));
            }
        }
    }

    private void DrawPost(PaintContext dc, GlassLayout layout, DayState state)
    {
        var post = layout.Post;
        var height = Math.Min(90, (layout.Ground - layout.Ridge.Y) * 0.7);
        dc.DrawLine(GlassInk.PostPen, post, post + new Vector(0, -height));
        dc.DrawRectangle(GlassInk.Shade, null, new Rect(post.X - 14, post.Y - height - 6, 28, 8));
        DrawThermometer(dc, new Point(post.X + 9, post.Y - (height * 0.25)), height * 0.55, state.Outside, -10, 60);
    }

    private void DrawGlassLabels(PaintContext dc, GlassLayout layout, DayState state, Point sun)
    {
        var ridgeLabel = new Point(layout.Ridge.X, layout.Ridge.Y - 22);
        AddBoxTarget("greenhouse", DrawPill(dc, LocFormat("Green_Glass_InsideTemp", Number(state.Greenhouse, "0")), ridgeLabel, Quantize(HeatColor(state.Greenhouse)), 13));
        AddBoxTarget("car", DrawPill(dc, LocFormat("Green_Glass_CarTemp", Number(state.Car, "0")), new Point(layout.Car.Left + (layout.Car.Width / 2), layout.Car.Top - 24), Quantize(HeatColor(state.Car)), 13));
        var postTop = layout.Post.Y - Math.Min(90, (layout.Ground - layout.Ridge.Y) * 0.7);
        AddBoxTarget("outside", DrawPill(dc, LocFormat("Green_Glass_OutsideTemp", Number(state.Outside, "0")), new Point(layout.Post.X, postTop - 18), Quantize(HeatColor(state.Outside)), 12));

        if (state.SunAltitude > 0)
        {
            AddBoxTarget("sun", DrawPill(dc, LocFormat("Green_Glass_Sunlight", Number(state.Sunlight, "0")), sun + new Vector(0, 30), Ink.Sun, 11));
        }

        if (!ShowLabels) return;

        var (hinge, free) = Vents(layout)[1];
        var ventLabel = Loc(VentsOpen ? "Green_Glass_VentsOpen" : "Green_Glass_VentsShut");
        AddBoxTarget("vents", DrawText(dc, ventLabel, Mix(hinge, free, 0.5) + new Vector(18, -10), Ink.Text, 10.5));

        var pane = Loc(SaltPane ? "Green_Glass_SaltPane" : "Green_Glass_Pane");
        AddBoxTarget("pane", DrawText(dc, pane, new Point((layout.HouseLeft + layout.HouseRight) / 2, layout.Ground + 16), Ink.Text, 10.5, center: true));

        AddBoxTarget("carwindow", DrawText(dc, Loc(CarWindowOpen ? "Green_Glass_WindowOpen" : "Green_Glass_WindowShut"),
            new Point(layout.Car.Left + (layout.Car.Width / 2), layout.Car.Bottom + 16), Ink.Text, 10.5, center: true));

        // The legend: sunlight and infrared.
        var y = layout.Stage.Top + 12;
        var x = layout.Stage.Right - 24;
        var texts = new[] { Loc("Green_Legend_Sunlight"), Loc("Green_Legend_Infrared"), Loc("Green_Legend_WarmAir") };
        var widths = texts.Select(t => _labels.Text(t, 11, Ink.Text).Width).ToArray();
        var total = widths.Sum() + (3 * 30) + 36;
        var left = x - total;
        dc.DrawRoundedRectangle(GlassInk.LegendBack, null, new Rect(left - 8, y - 11, total + 10, 22), 11, 11);
        dc.DrawLine(FadedPen(Ink.Sun, 1, 2, flat: true), new Point(left, y), new Point(left + 20, y));
        AddBoxTarget("sunlight", new Rect(left, y - 6, 20, 12).Union(DrawText(dc, texts[0], new Point(left + 25, y), Ink.Text, 11)));
        var left2 = left + 25 + widths[0] + 16;
        dc.DrawGeometry(null, FadedPen(Ink.Infrared, 1, 1.8, flat: true), Line(Wiggle(new Point(left2 + 20, y), new Vector(1, 0), 20, 7, 3, 0)));
        AddBoxTarget("infrared", new Rect(left2, y - 6, 20, 12).Union(DrawText(dc, texts[1], new Point(left2 + 25, y), Ink.Text, 11)));
        var left3 = left2 + 25 + widths[1] + 16;
        dc.DrawEllipse(FadedBrush(Ink.Orange, 0.6), null, new Point(left3 + 8, y), 6, 5);
        AddBoxTarget("warmair", new Rect(left3, y - 6, 16, 12).Union(DrawText(dc, texts[2], new Point(left3 + 20, y), Ink.Text, 11)));
    }

    private static class GlassInk
    {
        public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(140, 0xFF, 0xE0, 0x7A), 0),
            (Color.FromArgb(0, 0xFF, 0xE0, 0x7A), 1));

        public static readonly IBrush Grass = SpaceDrawing.LinearGradient(Color.FromRgb(0x4A, 0x7A, 0x3A), Color.FromRgb(0x24, 0x3A, 0x1E), 90);
        public static readonly IPen GroundEdge = MakePen(Solid(160, 0x6A, 0x9A, 0x4A), 1.5);
        public static readonly IBrush Inside = Solid(40, 0x10, 0x18, 0x10);
        public static readonly IBrush Pane = Solid(30, 0xD8, 0xF0, 0xFF);
        public static readonly IBrush Salt = Solid(48, 0xF4, 0xF0, 0xE8);
        public static readonly IPen Frame = MakePen(Solid(255, 0xD0, 0xD6, 0xDE), 3);
        public static readonly IPen Bar = MakePen(Solid(190, 0xD0, 0xD6, 0xDE), 1.4);
        public static readonly IPen Vent = MakePen(Solid(255, 0xE8, 0xEC, 0xF2), 3.4);
        public static readonly IPen Gap = MakePen(Solid(220, 0x10, 0x14, 0x1C), 3);
        public static readonly IPen Stem = MakePen(Solid(255, 0x4E, 0x8A, 0x3A), 1.6);
        public static readonly IBrush Leaf = Solid(255, 0x5A, 0xA8, 0x4A);
        public static readonly IBrush Tomato = Solid(255, 0xE8, 0x4A, 0x3A);

        public static readonly IBrush CarBody = SpaceDrawing.LinearGradient(Color.FromRgb(0x3A, 0x5A, 0x9A), Color.FromRgb(0x1E, 0x2E, 0x52), 90);
        public static readonly IPen CarEdge = MakePen(Solid(255, 0x14, 0x1C, 0x30), 1.4);
        public static readonly IPen CarLine = MakePen(Solid(150, 0x14, 0x1C, 0x30), 1.2);
        public static readonly IBrush CarGap = Solid(255, 0x06, 0x08, 0x0E);
        public static readonly IBrush WindowShine = Solid(50, 255, 255, 255);
        public static readonly IBrush Lamp = Solid(255, 0xFF, 0xD3, 0x6B);
        public static readonly IBrush Tyre = Solid(255, 0x14, 0x16, 0x1A);
        public static readonly IBrush Hub = Solid(255, 0x9A, 0xA2, 0xAE);

        public static readonly IPen PostPen = MakePen(Solid(255, 0x8A, 0x6A, 0x4A), 4);
        public static readonly IBrush Shade = Solid(255, 0xE8, 0xEC, 0xF2);
        public static readonly IBrush LegendBack = Solid(180, 8, 12, 22);
    }
}
