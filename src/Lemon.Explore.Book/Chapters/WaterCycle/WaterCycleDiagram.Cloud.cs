using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.WaterCycle;

/// <summary>Cloud mode: a parcel of air lifted until it cools to its dew point, the cloud it builds, and its rain.</summary>
public sealed partial class WaterCycleDiagram
{
    /// <summary>Pixels on the left for the height scale.</summary>
    private const double AxisSpace = 58;

    /// <summary>Pixels of ground drawn below sea level.</summary>
    private const double GroundBand = 30;

    private ParcelAscent _ascent = WaterCycleModel.Lift(20, 0.6, 6000);
    private IReadOnlyList<ParcelPoint> _descent = [];
    private CloudLift _cloudLift;
    private double _cloudTime;
    private double _held;

    /// <summary>The cloud view's coordinates on screen: kilometres across, metres up.</summary>
    private readonly record struct CloudView(Rect Stage)
    {
        public double X(double km) => Stage.Left + AxisSpace + (km / CloudScene.Width * (Stage.Width - AxisSpace - 12));

        public double Y(double height) => Stage.Top + ((CloudScene.SkyTop - height) / CloudScene.SkyTop * (Stage.Height - GroundBand));

        public Point P(double km, double height) => new(X(km), Y(height));

        public double KmAt(double x) => (x - Stage.Left - AxisSpace) / (Stage.Width - AxisSpace - 12) * CloudScene.Width;
    }

    /// <summary>The air's temperature, humidity or way of lifting changed: lift the parcel again.</summary>
    private void OnCloudChanged()
    {
        _ascent = WaterCycleModel.Lift(GroundTemperature, Humidity, WaterCycleModel.LiftTop(Lift));
        _descent = Lift == CloudLift.Mountain ? WaterCycleModel.OverMountain(_ascent) : [];
        if (_cloudLift == Lift) return;
        _cloudLift = Lift;
        _cloudTime = 0;
        Push(TimeProperty, 0.0);
    }

    private void OnTimeChanged(double value)
    {
        if (_pushing) return;
        _cloudTime = Math.Clamp(value, 0, CloudScene.Duration);
        _held = 0;
    }

    private void AdvanceCloud(double step, double dt)
    {
        if (_cloudTime < CloudScene.Duration)
        {
            _cloudTime = Math.Min(CloudScene.Duration, _cloudTime + step);
            Push(TimeProperty, _cloudTime);
            return;
        }

        // A short pause on the last frame, then up again.
        _held += dt;
        if (_held < 0.6) return;
        _held = 0;
        _cloudTime = 0;
        Push(TimeProperty, 0.0);
    }

    private void DrawCloudMode(PaintContext dc, double width, double height)
    {
        var view = new CloudView(Stage(TitleSpace + 22));
        var lift = Lift;
        var ascent = _ascent;
        var progress = Math.Clamp(_cloudTime / CloudScene.MoveSeconds, 0, 1);
        var place = CloudScene.At(lift, progress);
        var state = CloudScene.State(ascent, _descent, place);

        dc.DrawRectangle(Ink.Sky, null, new Rect(0, 0, width, height));
        if (lift == CloudLift.Thermal) DrawSun(dc, new Point(view.X(17.5), view.Y(7000)));
        DrawCloudGround(dc, view, lift, width, height);
        DrawHeightScale(dc, view, width);

        // The cloud base and the freezing level of the rising air.
        if (ascent.HasCloud)
        {
            var y = view.Y(ascent.CloudBase);
            dc.DrawLine(Ink.Leader, new Point(view.X(0), y), new Point(width, y));
            AddBoxTarget("cloudbase", DrawPill(dc, LocFormat("Water_CloudBase_Label", Number(ascent.CloudBase, "N0")), new Point(view.X(18.2), y), Ink.Text, 11));
        }

        if (!double.IsNaN(ascent.FreezingLevel) && ascent.FreezingLevel > 0)
        {
            var y = view.Y(ascent.FreezingLevel);
            dc.DrawLine(Ink.Freezing, new Point(view.X(0), y), new Point(width, y));
            AddBoxTarget("freezing", DrawPill(dc, LocFormat("Water_Freezing_Label", Number(ascent.FreezingLevel, "N0")), new Point(view.X(18.2), y), Ink.Cyan, 10.5));
        }

        var (cloudLeft, cloudRight) = DrawBuiltCloud(dc, view, lift, ascent, progress);
        DrawCloudRain(dc, view, lift, ascent, place, cloudLeft, cloudRight);
        DrawLiftArrows(dc, view, lift, place);
        DrawParcel(dc, view, lift, ascent, place, state);
    }

    /// <summary>Kilometres up the left, with faint lines across.</summary>
    private void DrawHeightScale(PaintContext dc, CloudView view, double width)
    {
        var left = view.X(0);
        dc.DrawLine(Ink.Axis, new Point(left, view.Y(0)), new Point(left, view.Y(CloudScene.SkyTop)));
        for (var km = 0; km <= 8; km++)
        {
            var y = view.Y(km * 1000);
            dc.DrawLine(Ink.Tick, new Point(left - 6, y), new Point(left, y));
            if (km > 0) dc.DrawLine(GridPen, new Point(left, y), new Point(width, y));
            DrawTextRight(dc, LocFormat("Water_Km", km), new Point(left - 9, y), Ink.Dim, 10.5);
        }
    }

    private static readonly IPen GridPen = MakePen(Solid(20, 255, 255, 255), 1);

    /// <summary>The ground of each way of lifting: a sunny field, a mountain, or a wedge of cold air.</summary>
    private void DrawCloudGround(PaintContext dc, CloudView view, CloudLift lift, double width, double height)
    {
        var groundY = view.Y(0);
        if (lift == CloudLift.Mountain)
        {
            var mountain = new StreamGeometry();
            using (var context = mountain.Open())
            {
                context.BeginFigure(new Point(view.X(0), height), true);
                for (var i = 0; i <= 160; i++)
                {
                    var x = CloudScene.Width * i / 160;
                    context.LineTo(view.P(x, CloudScene.MountainGround(x)));
                }

                context.LineTo(new Point(width, height));

                context.EndFigure(true);
            }
            dc.DrawRectangle(Ink.Rock, null, new Rect(0, groundY, width, height - groundY));
            dc.DrawGeometry(Ink.Rock, Ink.GroundLine, mountain);
            dc.DrawGeometry(null, Ink.Grass, MountainLine(view, 0, CloudScene.Width / 2 - 3.6));
            dc.DrawGeometry(null, Ink.Grass, MountainLine(view, (CloudScene.Width / 2) + 3.6, CloudScene.Width));
            AddBoxTarget("wind", DrawText(dc, Loc("Water_Cloud_Windward"), view.P(4.2, 520), Ink.Text, 11.5, center: true));
            AddBoxTarget("leeward", DrawText(dc, Loc("Water_Cloud_Leeward"), view.P(15.8, 520), Ink.Orange, 11.5, center: true));
            return;
        }

        dc.DrawRectangle(Ink.Rock, null, new Rect(0, groundY, width, height - groundY));
        dc.DrawLine(Ink.Grass, new Point(view.X(0), groundY), new Point(width, groundY));

        if (lift == CloudLift.Thermal)
        {
            // The sunny field the bubble is warmed over, the air above it shimmering.
            var left = view.X(CloudScene.ThermalX - 1.4);
            var right = view.X(CloudScene.ThermalX + 1.4);
            dc.DrawRectangle(FieldBrush, null, new Rect(left, groundY - 1.5, right - left, 6));
            for (var k = 0; k < 5; k++)
            {
                var x0 = WaterCycleModel.Lerp(left + 10, right - 10, k / 4.0);
                var shimmer = new StreamGeometry();
                using (var context = shimmer.Open())
                {
                    context.BeginFigure(new Point(x0, groundY - 4), false);
                    for (var y = 4.0; y < 46; y += 3)
                    {
                        context.LineTo(new Point(x0 + (3 * Math.Sin((y / 6) - (_t * 4) + k)), groundY - y));
                    }

                    context.EndFigure(false);
                }
                dc.DrawGeometry(null, ShimmerPen, shimmer);
            }

            AddBoxTarget("thermal", DrawText(dc, Loc("Water_Cloud_SunnyField"), new Point((left + right) / 2, groundY + 15), Ink.Gold, 11.5, center: true));
            return;
        }

        // The wedge of cold air, the warm air sliding up over it.
        var wedge = new StreamGeometry();
        using (var context = wedge.Open())
        {
            context.BeginFigure(view.P(CloudScene.FrontStart, 0), true);
            context.LineTo(view.P(CloudScene.Width, CloudScene.FrontSurface(CloudScene.Width)));
            context.LineTo(view.P(CloudScene.Width, 0));

            context.EndFigure(true);
        }
        dc.DrawGeometry(ColdAirBrush, FrontPen, wedge);
        AddBoxTarget("front", DrawText(dc, Loc("Water_Cloud_WarmAir"), view.P(2.6, 900), Ink.Orange, 12, center: true));
        AddBoxTarget("front", DrawText(dc, Loc("Water_Cloud_ColdAir"), view.P(16.5, 1600), Ink.Cyan, 12, center: true));
    }

    private static StreamGeometry MountainLine(CloudView view, double from, double to)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var i = 0; i <= 40; i++)
            {
                var x = from + ((to - from) * i / 40);
                var point = view.P(x, CloudScene.MountainGround(x));
                if (i == 0) context.BeginFigure(point, false);
                else context.LineTo(point);
            }

            context.EndFigure(false);
        }
        return geometry;
    }

    private static readonly IBrush FieldBrush = Solid(255, 0xC8, 0xA0, 0x48);
    private static readonly IPen ShimmerPen = MakePen(Solid(90, 0xFF, 0xD3, 0x6B), 1.2);
    private static readonly IBrush ColdAirBrush = Solid(60, 0x6F, 0xB8, 0xFF);
    private static readonly IPen FrontPen = MakePen(Solid(170, 0x6F, 0xB8, 0xFF), 1.6);

    /// <summary>
    /// The cloud the parcel has built so far: puffs wherever its way has been above the cloud base (on the way up). Returns
    /// how far across it reaches on screen, for the rain under it.
    /// </summary>
    private (double Left, double Right) DrawBuiltCloud(PaintContext dc, CloudView view, CloudLift lift, ParcelAscent ascent, double progress)
    {
        if (!ascent.HasCloud) return (double.NaN, double.NaN);
        var rains = WaterCycleModel.Rains(ascent);
        var unit = Math.Min(view.Stage.Width, view.Stage.Height * 1.6);

        if (lift == CloudLift.Thermal)
        {
            // A cumulus as tall as the parcel has climbed above the cloud base.
            var reached = CloudScene.At(lift, progress);
            if (reached.Height <= ascent.CloudBase) return (double.NaN, double.NaN);
            var baseY = view.Y(ascent.CloudBase);
            var topY = view.Y(reached.Height) - 14;
            var centerX = view.X(CloudScene.ThermalX + 0.5);
            DrawCumulus(dc, centerX, baseY, topY, unit, 57, dark: rains && reached.Height >= ascent.CloudBase + WaterCycleModel.RainingDepth);
            var half = CumulusWidth(unit, baseY - topY) * 0.4;
            return (centerX - half, centerX + half);
        }

        // Over the mountain or the front: puffs wherever the way has been above the cloud base, merged into one cloud.
        double left = double.MaxValue, right = double.MinValue;
        const int samples = 60;
        var depth = Math.Max(1, ascent.Top.Height - ascent.CloudBase);
        var last = new Point(double.NaN, double.NaN);
        var lower = new List<Puff>();
        var upper = new List<Puff>();
        for (var i = 0; i <= samples; i++)
        {
            var place = CloudScene.At(lift, progress * i / samples);
            if (place.Descending || place.Height < ascent.CloudBase) continue;

            // A puff every so far along, not one on top of another.
            var at = view.P(place.X, place.Height);
            if (!double.IsNaN(last.X) && ((Vector)(at - last)).Length < 20 && i < samples) continue;
            last = at;

            var width = lift == CloudLift.Front ? 74 : 64;
            var tall = lift == CloudLift.Front ? width * 0.42 : width * 0.62;
            var center = at - new Vector(0, tall * 0.3);
            var above = place.Height - ascent.CloudBase;
            AddCloudPuffs(rains && above < depth * 0.35 ? lower : upper, center, width, tall, 50 + i);
            left = Math.Min(left, center.X - (width * 0.4));
            right = Math.Max(right, center.X + (width * 0.4));
        }

        dc.PushOpacity(0.96);
        DrawPuffs(dc, upper, dark: false);
        DrawPuffs(dc, lower, dark: true);
        dc.Pop();
        return left <= right ? (left, right) : (double.NaN, double.NaN);
    }

    /// <summary>Rain (or snow) from a cloud deep enough, once the parcel has built it that deep.</summary>
    private void DrawCloudRain(PaintContext dc, CloudView view, CloudLift lift, ParcelAscent ascent, ParcelPlace place, double left, double right)
    {
        if (!WaterCycleModel.Rains(ascent) || double.IsNaN(left)) return;
        var startsAt = ascent.CloudBase + WaterCycleModel.RainingDepth;
        var reached = _cloudTime >= CloudScene.MoveSeconds || place.Descending || place.Height >= startsAt;
        if (!reached) return;

        var snow = WaterCycleModel.SnowsAtGround(GroundTemperature);
        var freezing = double.IsNaN(ascent.FreezingLevel) ? double.PositiveInfinity : ascent.FreezingLevel;
        var count = (int)Math.Clamp((right - left) / 5, 8, 70);
        for (var i = 0; i < count; i++)
        {
            var x = WaterCycleModel.Lerp(left, right, Frac(i * 0.6180339887));
            var km = view.KmAt(x);
            var ground = CloudScene.Ground(lift, km);
            var baseHeight = Math.Max(ascent.CloudBase, lift == CloudLift.Front ? CloudScene.FrontSurface(km) + 300 : ascent.CloudBase);
            if (ground >= baseHeight - 50)
            {
                // Inside the cloud on the slope: fall from a little above the ground.
                baseHeight = ground + 500;
            }

            var age = Frac((_t / (snow ? 3.4 : 1.4)) + Frac(i * 0.7548776662));
            var z = WaterCycleModel.Lerp(baseHeight, ground, age);
            var at = new Point(x + (snow ? 2.5 * Math.Sin((age * 10) + i) : 0), view.Y(z));
            if (snow || z > freezing) DrawFlake(dc, at, 2.6);
            else DrawRainStreak(dc, at, 10);
        }
    }

    /// <summary>What lifts the air: rising warm air, wind against the mountain, warm air sliding up the front.</summary>
    private void DrawLiftArrows(PaintContext dc, CloudView view, CloudLift lift, ParcelPlace place)
    {
        switch (lift)
        {
            case CloudLift.Mountain:
                for (var k = 0; k < 3; k++)
                {
                    var from = view.P(0.6 + (k * 0.2), 250 + (k * 380));
                    DrawArrow(dc, from, from + new Vector(52, 0), Ink.Wind, 1.6, 8);
                }

                AddBoxTarget("wind", DrawText(dc, Loc("Water_Cloud_Wind"), view.P(1.15, 1350), Ink.Text, 11.5, center: true));
                break;

            case CloudLift.Front:
                var a = view.P(CloudScene.FrontStart + 1.5, CloudScene.FrontSurface(CloudScene.FrontStart + 1.5) + 450);
                var b = view.P(CloudScene.FrontStart + 4.5, CloudScene.FrontSurface(CloudScene.FrontStart + 4.5) + 450);
                DrawArrow(dc, a, b, Ink.Wind, 1.8, 9);
                break;

            default:
                var center = view.P(place.X, place.Height);
                for (var k = -1; k <= 1; k += 2)
                {
                    var from = center + new Vector(k * 36, 26);
                    DrawArrow(dc, from, from + new Vector(0, -26), Ink.Wind, 1.5, 7);
                }

                break;
        }
    }

    /// <summary>The parcel: a bubble swelling as it rises, its vapour turning into droplets in the cloud, its thermometer beside it.</summary>
    private void DrawParcel(PaintContext dc, CloudView view, CloudLift lift, ParcelAscent ascent, ParcelPlace place, ParcelPoint state)
    {
        var radius = 15 * Math.Cbrt(WaterCycleModel.Swelling(ascent.Ground, state));
        var center = view.P(place.X, place.Height);
        if (lift != CloudLift.Thermal || place.Height < 30) center -= new Vector(0, radius + 3);

        var warm = place.Descending;
        dc.DrawEllipse(warm ? ParcelWarm : state.Saturated ? ParcelWet : ParcelDry, warm ? ParcelEdgeWarm : ParcelEdge, center, radius, radius);

        // Vapour (small, faint) and droplets (bigger, white) inside; the share condensed turns into droplets.
        var vapour = WaterCycleModel.MixingRatio(WaterCycleModel.SaturationPressure(ascent.Ground.DewPoint), ascent.Ground.Pressure);
        var condensed = place.Descending ? 0 : Math.Clamp(state.Condensed / Math.Max(1e-6, vapour), 0, 1);
        const int dots = 16;
        var droplets = (int)Math.Round(dots * condensed);
        for (var i = 0; i < dots; i++)
        {
            var angle = (i * 2.399963) + (_t * (0.7 + (0.2 * Frac(i * 0.618))));
            var r = radius * 0.72 * Math.Sqrt(Frac((i * 0.381966) + 0.2));
            var p = center + new Vector(Math.Cos(angle) * r, Math.Sin(angle) * r);
            if (i < droplets) dc.DrawEllipse(Ink.DropCore, null, p, 2.4, 2.4);
            else dc.DrawEllipse(Ink.Vapour, null, p, 1.3, 1.3);
        }

        var text = LocFormat("Water_Parcel_Label", Number(state.Temperature, "0.0"), Number(state.DewPoint, "0.0"));
        var formatted = _labels.Text(text, 11.5, warm ? Ink.Orange : Ink.Text);
        var labelAt = center + new Vector(radius + 10 + (formatted.Width / 2) + 6, -radius * 0.4);
        if (labelAt.X + (formatted.Width / 2) + 8 > ActualWidth) labelAt = center - new Vector(radius + 10 + (formatted.Width / 2) + 6, radius * 0.4);
        AddBoxTarget("parcel", DrawPill(dc, text, labelAt, warm ? Ink.Orange : Ink.Text, 11.5));
        _targets.Add("parcel", center, radius + 2);
    }

    private static readonly IBrush ParcelDry = Solid(36, 0xFF, 0xD3, 0x6B);
    private static readonly IBrush ParcelWet = Solid(46, 0xEE, 0xF1, 0xF6);
    private static readonly IBrush ParcelWarm = Solid(60, 0xFF, 0x9F, 0x5A);
    private static readonly IPen ParcelEdge = MakePen(Solid(230, 0xEE, 0xF1, 0xF6), 1.5, dashed: true);
    private static readonly IPen ParcelEdgeWarm = MakePen(Solid(230, 0xFF, 0x9F, 0x5A), 1.5, dashed: true);
}
