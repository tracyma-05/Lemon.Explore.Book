using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.DayNight;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.WaterCycle;

/// <summary>Stores mode: all of Earth's water as balls beside Earth, and the yearly flows between sea, air and land.</summary>
public sealed partial class WaterCycleDiagram
{
    /// <summary>The globe looks down on North America, like the USGS's picture of the same balls.</summary>
    private static readonly GlobeCamera StoresCamera = GlobeCamera.Above(36, -98);

    /// <summary>Light from the upper left, so the globe looks round.</summary>
    private static readonly SunPosition StoresSun = new(52, -135, 0, 0);

    private static readonly GeoPoint BallsAt = new(39, -100);

    private GlobeRenderer? _globe;
    private int _globeSize;
    private bool _globeHasLand;

    private void DrawStoresMode(PaintContext dc, double width, double height)
    {
        if (StoresView == StoresView.Flows)
        {
            DrawFlows(dc, width, height);
            return;
        }

        DrawSpheres(dc, width, height);
    }

    #region the balls of water

    private void DrawSpheres(PaintContext dc, double width, double height)
    {
        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
        _stars.Draw(dc, width, height, RenderScale(), _clock.Elapsed.TotalSeconds, 0.8);

        var stage = Stage(TitleSpace);
        // A narrow window keeps room beside Earth for the enlarged balls (a book page more so).
        var radius = Math.Max(60, Math.Min(stage.Height * 0.44, stage.Width * (stage.Width < 700 ? 0.15 : stage.Width < 900 ? 0.19 : 0.26)));
        var center = new Point(stage.Left + 40 + radius, stage.Top + (stage.Height / 2) + 6);

        if (_globe is null)
        {
            WorldMap.WarmUp();
            _globe = new GlobeRenderer();
        }

        // The globe never turns: render it again only for a new size, or once the coastlines have loaded.
        var dpi = RenderScale();
        var size = (int)Math.Ceiling(2 * radius * dpi);
        var hasLand = WorldMap.LandMaskIfReady is not null;
        if (size != _globeSize || hasLand != _globeHasLand)
        {
            _globe.Render(size, StoresCamera, StoresSun);
            _globeSize = size;
            _globeHasLand = hasLand;
        }
        dc.DrawEllipse(GlobeHalo, null, center, radius * 1.06, radius * 1.06);
        if (_globe.Bitmap is { } bitmap) dc.DrawImage(bitmap, new Rect(center.X - radius, center.Y - radius, 2 * radius, 2 * radius));
        _targets.Add("earth", center, radius);

        // The three balls, to scale with Earth, standing over North America.
        var (px, py, _) = StoresCamera.Project(BallsAt);
        var spot = new Point(center.X + (px * radius), center.Y - (py * radius));
        var balls = Balls();
        var perKm = 2 * radius / WaterCycleModel.EarthDiameter;
        var x = spot.X;
        var onEarth = new Point[balls.Length];
        for (var i = 0; i < balls.Length; i++)
        {
            var r = balls[i].Diameter / 2 * perKm * Grow(i);
            onEarth[i] = new Point(x + (i == 0 ? 0 : r), spot.Y + (balls[0].Diameter / 2 * perKm) - Math.Max(r, 0.6));
            DrawWaterBall(dc, onEarth[i], Math.Max(r, i == 2 ? 0.9 : 0));
            x = onEarth[i].X + r + (i == 0 ? (balls[0].Diameter / 2 * perKm) + 4 : 3);
        }

        // Enlarged beside Earth: the big ball, and the two small ones in a column beside it, labelled on their right.
        var inset = new Rect(center.X + radius + 48, stage.Top + 18, Math.Max(80, stage.Right - (center.X + radius + 48) - 16), stage.Height - 36);
        var sizeTexts = balls.Select(b => LocFormat("Water_Ball_Size", Number(b.Diameter, "N0"), WaterFormat.Volume(b.Volume))).ToArray();
        var labelWidth = Enumerable.Range(1, 2).Max(i => Math.Max(
            _labels.Text(Loc($"Water_Ball_{balls[i].Key}"), 12, Ink.Text, true).Width, _labels.Text(sizeTexts[i], 11, Ink.Dim).Width));
        var bigRadius = Math.Min(inset.Height * 0.3, (inset.Width - labelWidth - 50) / 2.3);
        if (bigRadius < 30) return;

        var scale = bigRadius / (balls[0].Diameter / 2);
        DrawText(dc, LocFormat("Water_Spheres_Enlarged", Number(scale / perKm, "0")), new Point(inset.Left, inset.Top), Ink.Dim, 11.5);

        var big = new Point(inset.Left + bigRadius + 4, inset.Top + 30 + bigRadius);
        var column = big.X + bigRadius + 30;
        var centers = new[] { big, new Point(column, big.Y - (bigRadius * 0.4)), new Point(column, big.Y + (bigRadius * 0.45)) };
        var earthRadius = balls[0].Diameter / 2 * perKm;
        dc.DrawLine(Ink.Leader, onEarth[0] + new Vector(0, -earthRadius), big + new Vector(0, -bigRadius));
        dc.DrawLine(Ink.Leader, onEarth[0] + new Vector(0, earthRadius), big + new Vector(0, bigRadius));

        for (var i = 0; i < balls.Length; i++)
        {
            var r = balls[i].Diameter / 2 * scale * Grow(i);
            DrawWaterBall(dc, centers[i], Math.Max(r, i == 2 ? 1.2 : 0));
            var alpha = Smooth(0.6, 1, Grow(i));
            if (alpha <= 0.02) continue;

            dc.PushOpacity(alpha);
            var name = Loc($"Water_Ball_{balls[i].Key}");
            var id = "b-" + balls[i].Key;
            if (i == 0)
            {
                var below = big.Y + bigRadius + 16;
                AddBoxTarget(id, DrawText(dc, name, new Point(big.X, below), Ink.Text, 12, center: true, bold: true).Union(DrawText(dc, sizeTexts[i], new Point(big.X, below + 17), Ink.Dim, 11, center: true)));
            }
            else
            {
                var textLeft = centers[i].X + Math.Max(r, 3) + 10;
                AddBoxTarget(id, DrawText(dc, name, new Point(textLeft, centers[i].Y - 8), Ink.Text, 12, bold: true).Union(DrawText(dc, sizeTexts[i], new Point(textLeft, centers[i].Y + 9), Ink.Dim, 11)));
            }

            // The ball itself, added last so the walkthrough's ring goes round it.
            _targets.Add(id, centers[i], Math.Max(r, 6) + 3);
            dc.Pop();
        }

        // A scale bar under them.
        var barKm = ProjectileNiceStep(balls[0].Diameter / 2.5);
        var barY = big.Y + bigRadius + 62;
        var barLeft = big.X - bigRadius;
        var barRight = barLeft + (barKm * scale);
        dc.DrawLine(Ink.Axis, new Point(barLeft, barY), new Point(barRight, barY));
        dc.DrawLine(Ink.Axis, new Point(barLeft, barY - 4), new Point(barLeft, barY + 4));
        dc.DrawLine(Ink.Axis, new Point(barRight, barY - 4), new Point(barRight, barY + 4));
        DrawText(dc, LocFormat("Water_Km_Value", Number(barKm, "N0")), new Point(barRight + 8, barY), Ink.Dim, 11);
    }

    /// <summary>How far a ball has grown in since the view was shown, one after the other, 0–1.</summary>
    private double Grow(int index)
    {
        var t = Math.Clamp((_storesSeconds - 0.3 - (index * 0.7)) / 1.1, 0, 1);
        return 1 - Math.Pow(1 - t, 3);
    }

    private static (string Key, double Volume, double Diameter)[] Balls() =>
    [
        ("All", WaterCycleModel.TotalWater, WaterCycleModel.SphereDiameter(WaterCycleModel.TotalWater)),
        ("FreshLiquid", WaterCycleModel.FreshLiquidWater, WaterCycleModel.SphereDiameter(WaterCycleModel.FreshLiquidWater)),
        ("LakesRivers", WaterCycleModel.LakesAndRivers, WaterCycleModel.SphereDiameter(WaterCycleModel.LakesAndRivers)),
    ];

    private static void DrawWaterBall(PaintContext dc, Point center, double radius)
    {
        if (radius <= 0.2) return;
        dc.DrawEllipse(BallBrush, radius > 6 ? BallRim : null, center, radius, radius);
    }

    private static double ProjectileNiceStep(double raw)
    {
        var power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var mantissa = raw / power;
        return (mantissa <= 1 ? 1 : mantissa <= 2 ? 2 : mantissa <= 5 ? 5 : 10) * power;
    }

    private static readonly IBrush BallBrush = new ImmutableRadialGradientBrush(
    [
        new ImmutableGradientStop(0, Color.FromRgb(0xC8, 0xEC, 0xFF)),
        new ImmutableGradientStop(0.45, Color.FromRgb(0x3A, 0x9A, 0xF0)),
        new ImmutableGradientStop(1, Color.FromRgb(0x10, 0x4A, 0x9A)),
    ],
        center: new RelativePoint(0.42, 0.38, RelativeUnit.Relative),
        gradientOrigin: new RelativePoint(0.35, 0.3, RelativeUnit.Relative),
        radiusX: new RelativeScalar(0.68, RelativeUnit.Relative),
        radiusY: new RelativeScalar(0.68, RelativeUnit.Relative));

    private static readonly IPen BallRim = MakePen(Solid(120, 0x08, 0x20, 0x40), 1);
    private static readonly IBrush GlobeHalo = SpaceDrawing.RadialGradient((Color.FromArgb(70, 0x6F, 0xB8, 0xFF), 0), (Color.FromArgb(0, 0x6F, 0xB8, 0xFF), 1));

    #endregion

    #region the flows each year

    /// <summary>A yearly flow drawn as a band: where it starts and ends on screen, how much, and whether it is vapour.</summary>
    /// <remarks><see cref="LabelAt"/> is how far along the band its label sits (0–1); <see cref="LabelOffset"/> which side it is on.</remarks>
    private readonly record struct FlowBand(string Key, Point From, Point To, double Amount, bool Vapour, Vector LabelOffset, double LabelAt = 0.5);

    private void DrawFlows(PaintContext dc, double width, double height)
    {
        var stage = Stage(TitleSpace + 24);
        var w = stage.Width;
        var h = stage.Height;
        var horizon = stage.Top + (h * 0.62);
        var coast = stage.Left + (w * 0.5);
        var cloudY = stage.Top + (h * 0.2);

        dc.DrawRectangle(Ink.Sky, null, new Rect(0, 0, width, height));
        DrawSun(dc, new Point(stage.Left + (w * 0.93), stage.Top + 20));

        // The sea on the left, the land with its hills on the right.
        dc.DrawRectangle(Ink.Sea, null, new Rect(0, horizon, coast, height - horizon));
        var land = new StreamGeometry();
        using (var context = land.Open())
        {
            context.BeginFigure(new Point(coast - 12, height), true);
            context.LineTo(new Point(coast - 12, horizon + 4));
            for (var x = coast; x <= width + 4; x += 6)
            {
                var f = (x - coast) / Math.Max(1, width - coast);
                var hill = (h * 0.16 * Math.Exp(-Math.Pow((f - 0.62) / 0.16, 2))) + (h * 0.05 * Math.Exp(-Math.Pow((f - 0.3) / 0.12, 2)));
                context.LineTo(new Point(x, horizon - hill));
            }

            context.LineTo(new Point(width, height));

            context.EndFigure(true);
        }
        dc.DrawGeometry(Ink.Rock, Ink.GroundLine, land);
        dc.DrawLine(Ink.Wave, new Point(0, horizon), new Point(coast - 12, horizon));

        // Clouds where the vapour gathers.
        DrawCloud(dc, new Point(stage.Left + (w * 0.3), cloudY), w * 0.16, w * 0.07, 0.9, 71);
        DrawCloud(dc, new Point(stage.Left + (w * 0.72), cloudY), w * 0.15, w * 0.07, 0.9, 72, dark: true);

        var perUnit = Math.Min(64, w * 0.07) / WaterCycleModel.OceanEvaporation;
        var bands = new[]
        {
            new FlowBand("OceanEvaporation", new Point(stage.Left + (w * 0.17), horizon - 4), new Point(stage.Left + (w * 0.17), cloudY + 26),
                WaterCycleModel.OceanEvaporation, true, new Vector(-14, 0)),
            new FlowBand("OceanPrecipitation", new Point(stage.Left + (w * 0.36), cloudY + 26), new Point(stage.Left + (w * 0.36), horizon - 4),
                WaterCycleModel.OceanPrecipitation, false, new Vector(14, 0), 0.62),
            new FlowBand("VapourToLand", new Point(stage.Left + (w * 0.42), cloudY - 4), new Point(stage.Left + (w * 0.62), cloudY - 4),
                WaterCycleModel.VapourToLand, true, new Vector(0, 14)),
            new FlowBand("LandPrecipitation", new Point(stage.Left + (w * 0.66), cloudY + 26), new Point(stage.Left + (w * 0.66), horizon - (h * 0.06)),
                WaterCycleModel.LandPrecipitation, false, new Vector(12, 0), 0.3),
            new FlowBand("LandEvaporation", new Point(stage.Left + (w * 0.86), horizon - (h * 0.05)), new Point(stage.Left + (w * 0.86), cloudY + 26),
                WaterCycleModel.LandEvaporation, true, new Vector(12, 0), 0.25),
            new FlowBand("Runoff", new Point(stage.Left + (w * 0.64), horizon + 18), new Point(coast - 40, horizon + 18),
                WaterCycleModel.Runoff, false, new Vector(0, 18)),
        };

        foreach (var band in bands) DrawFlowBand(dc, band, band.Amount * perUnit);

        // The three stores, with how long water stays in each.
        DrawStoreCaption(dc, new Point(stage.Left + (w * 0.5), stage.Top + 2), "Atmosphere", WaterFormat.Volume(WaterCycleModel.Volume(WaterStore.Atmosphere)),
            WaterFormat.Duration(WaterCycleModel.AtmosphereDays));
        DrawStoreCaption(dc, new Point(stage.Left + (w * 0.25), horizon + (h * 0.2)), "Oceans", WaterFormat.Volume(WaterCycleModel.Volume(WaterStore.Oceans)),
            WaterFormat.Duration(WaterCycleModel.OceanYears * 365.25));
        var landWater = WaterCycleModel.Volume(WaterStore.IceAndSnow) + WaterCycleModel.Volume(WaterStore.Groundwater) + WaterCycleModel.Volume(WaterStore.GroundIce)
            + WaterCycleModel.Volume(WaterStore.Lakes) + WaterCycleModel.Volume(WaterStore.SoilMoisture) + WaterCycleModel.Volume(WaterStore.Swamps)
            + WaterCycleModel.Volume(WaterStore.Rivers) + WaterCycleModel.Volume(WaterStore.Living);
        DrawStoreCaption(dc, new Point(stage.Left + (w * 0.76), horizon + (h * 0.2)), "Land", WaterFormat.Volume(landWater), null);
    }

    private void DrawFlowBand(PaintContext dc, FlowBand band, double thickness)
    {
        Vector v = band.To - band.From;
        var length = v.Length;
        if (length < 10) return;
        var direction = v / length;
        var normal = new Vector(-direction.Y, direction.X);
        var color = band.Vapour ? Color.FromRgb(0xE6, 0xF0, 0xFA) : Color.FromRgb(0x5A, 0xB0, 0xFF);
        var head = Math.Max(12, thickness * 0.9);
        var half = thickness / 2;
        var end = band.To - (direction * head * 0.8);

        var body = Polygon(band.From + (normal * half), end + (normal * half), end + (normal * (half + (head * 0.45))), band.To,
            end - (normal * (half + (head * 0.45))), end - (normal * half), band.From - (normal * half));
        dc.DrawGeometry(Solid(band.Vapour ? (byte)60 : (byte)85, color.R, color.G, color.B), MakePen(Solid(150, color.R, color.G, color.B), 1), body);

        // Particles running along it, as many as the band is wide.
        var count = Math.Max(3, (int)(thickness * length / 220));
        var particle = band.Vapour ? Ink.Vapour : Ink.WaterDot;
        for (var i = 0; i < count; i++)
        {
            var u = Frac((_t * 70 / length) + Frac(i * 0.6180339887));
            var across = (Frac(i * 0.7548776662) - 0.5) * thickness * 0.8;
            var p = band.From + (v * u * ((length - (head * 0.6)) / length)) + (normal * across);
            dc.PushOpacity(Smooth(0, 0.08, u) * (1 - Smooth(0.9, 1, u)));
            dc.DrawEllipse(particle, null, p, 1.8, 1.8);
            dc.Pop();
        }

        // Its name and how much a year, beside it.
        var mid = band.From + (v * band.LabelAt) + band.LabelOffset;
        var name = Loc($"Water_Flow_{band.Key}");
        var amount = WaterFormat.Flux(band.Amount);
        var nameText = _labels.Text(name, 12, Ink.Text, true);
        var amountText = _labels.Text(amount, 11, Ink.Dim);

        // Wrapped to the room beside the band (in a narrow window, the next band is close).
        var room = band.LabelOffset.X < 0 ? mid.X - half - 6 : band.LabelOffset.X > 0 ? ActualWidth - (mid.X + half) - 6 : 240;
        var maxWidth = Math.Clamp(Math.Min(room, ActualWidth * 0.17), 64, 230);
        nameText.MaxTextWidth = maxWidth;
        amountText.MaxTextWidth = maxWidth;
        var boxWidth = Math.Max(nameText.Width, amountText.Width);
        var x = band.LabelOffset.X < 0 ? mid.X - (half + boxWidth) : band.LabelOffset.X > 0 ? mid.X + half : mid.X - (boxWidth / 2);
        x = Math.Clamp(x, 4, Math.Max(4, ActualWidth - boxWidth - 4));
        var y = band.LabelOffset.Y > 0 ? mid.Y + half - 4 : band.LabelOffset.Y < 0 ? mid.Y - half - nameText.Height - amountText.Height : mid.Y - nameText.Height;
        dc.DrawText(nameText, new Point(x, y));
        dc.DrawText(amountText, new Point(x, y + nameText.Height));
        AddBoxTarget("f-" + band.Key, new Rect(x, y, boxWidth, nameText.Height + amountText.Height));
    }

    private void DrawStoreCaption(PaintContext dc, Point center, string key, string volume, string? stay)
    {
        var title = _labels.Text(Loc($"Water_Store_{key}"), 14, Ink.Gold, true);
        dc.DrawText(title, new Point(center.X - (title.Width / 2), center.Y));
        var line = stay is null ? volume : LocFormat("Water_Store_Stay", volume, stay);
        var text = _labels.Text(line, 11.5, Ink.Text);
        dc.DrawText(text, new Point(center.X - (text.Width / 2), center.Y + title.Height + 1));
        var width = Math.Max(title.Width, text.Width);
        AddBoxTarget(key.ToLowerInvariant(), new Rect(center.X - (width / 2), center.Y, width, title.Height + 1 + text.Height));
    }

    #endregion
}
