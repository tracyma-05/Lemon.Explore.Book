using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.HeatEngines;

/// <summary>
/// Fridge mode: a fridge-freezer cut open from the side, standing in a room. The refrigerant (<see cref="FridgeModel"/>)
/// goes round the compressor, the condenser on the back, the long thin capillary tube and the evaporator in the freezer's
/// back wall, drawn as bubbles where it is vapour and drops where it is liquid, coloured by its temperature.
/// </summary>
public sealed partial class HeatDiagram
{
    /// <summary>Refrigerant particles round the loop.</summary>
    private const int LoopParticles = 120;

    /// <summary>A frame of the fridge in screen space: millimetres from the front of the cabinet (x, to the back) and the floor (y, up).</summary>
    private readonly record struct FridgeFrame(double X, double Y, double Scale)
    {
        public Point P(double x, double y) => new(X + (x * Scale), Y - (y * Scale));

        public double L(double mm) => mm * Scale;
    }

    /// <summary>The loop's parts as lines through the fridge (mm), in the order the refrigerant goes round.</summary>
    private static readonly Point[][] LoopPaths = BuildLoop();

    private static readonly double[][] LoopLengths = LoopPaths.Select(CumulativeLengths).ToArray();

    private IImage? _room;
    private (double Width, double Height, double Room, bool Door, bool Labels) _roomKey;
    private double _roomScale;

    private static Point[][] BuildLoop()
    {
        var condenser = new List<Point> { new(660, 1680) };
        for (var y = 1680.0; y > 330; y -= 68) condenser.Add(new Point(condenser.Count % 2 == 0 ? 690 : 625, y - 34));
        condenser.Add(new Point(660, 320));

        var evaporator = new List<Point> { new(540, 1730) };
        for (var y = 1730.0; y > 1320; y -= 40) evaporator.Add(new Point(evaporator.Count % 2 == 0 ? 540 : 468, y - 20));
        evaporator.Add(new Point(500, 1290));

        var capillary = new List<Point> { new(660, 320), new(612, 290), new(586, 300) };

        // The capillary tube is two or three metres long and thinner than a straw: shown as a coil on its way up.
        for (var i = 0; i <= 24; i++)
        {
            var a = i / 24.0 * Math.PI * 8;
            capillary.Add(new Point(588 + (Math.Sin(a) * 9), 360 + (i * 9) + (Math.Cos(a) * 9)));
        }

        capillary.Add(new Point(586, 1720));
        capillary.Add(new Point(540, 1730));

        return
        [
            [new(500, 205), new(512, 168), new(548, 168), new(560, 210)],
            [new(560, 210), new(610, 236), new(705, 262), new(705, 1650), new(660, 1680)],
            condenser.ToArray(),
            capillary.ToArray(),
            evaporator.ToArray(),
            [new(500, 1290), new(596, 1252), new(596, 262), new(500, 205)],
        ];
    }

    private static double[] CumulativeLengths(Point[] path)
    {
        var lengths = new double[path.Length];
        for (var i = 1; i < path.Length; i++) lengths[i] = lengths[i - 1] + ((Vector)(path[i] - path[i - 1])).Length;
        return lengths;
    }

    /// <summary>The point of the loop at <paramref name="s"/> (0…1), in fridge millimetres.</summary>
    private static Point LoopPoint(double s)
    {
        var part = (int)FridgeModel.PartAt(s);
        var path = LoopPaths[part];
        var lengths = LoopLengths[part];
        var target = FridgeModel.Progress(s) * lengths[^1];
        var i = 1;
        while (i < path.Length - 1 && lengths[i] < target) i++;
        var t = (target - lengths[i - 1]) / Math.Max(1e-9, lengths[i] - lengths[i - 1]);
        return Mix(path[i - 1], path[i], Math.Clamp(t, 0, 1));
    }

    private FridgeFrame FridgeLayout(double width)
    {
        var stage = Stage();

        // From the open door (−200 mm) to the wall behind (800 mm), the floor to the top (1850 mm).
        var scale = Math.Max(0.12, Math.Min(stage.Height / 1880, (width - 80) / 1350));
        var x = (width * 0.5) - (300 * scale);
        return new FridgeFrame(x, stage.Bottom - (12 * scale), scale);
    }

    /// <summary>The fridge's (lower) door: on the front, or swung out towards the viewer when open.</summary>
    private Rect DoorRect(FridgeFrame f) => DoorOpen
        ? new Rect(f.P(-190, 1230), f.P(-150, 40))
        : new Rect(f.P(-60, 1230), f.P(0, 40));

    private void DrawFridgeMode(PaintContext dc, double width, double height)
    {
        var f = FridgeLayout(width);
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var key = (width, height, Math.Round(Room, 1), DoorOpen, ShowLabels);
        if (_room is null || key != _roomKey || scale != _roomScale)
        {
            _room = BuildFridgeStatic(f, width, height, scale);
            _roomKey = key;
            _roomScale = scale;
        }

        SpaceDrawing.DrawWhole(dc, (Bitmap)_room, new Rect(0, 0, width, height));

        DrawRefrigerant(dc, f);
        DrawCompressor(dc, f);
        DrawFridgeHeat(dc, f);
        if (ShowLabels) DrawFridgeLabels(dc, f);
        AddFridgeTargets(f);
    }

    /// <summary>The parts themselves as targets, after their labels so a walkthrough rings the part.</summary>
    private void AddFridgeTargets(FridgeFrame f)
    {
        _targets.Add("room", f.P(-415, 700), f.L(110));
        _targets.Add("freezer", f.P(140, 1370), f.L(80));
        _targets.Add("fridge", f.P(140, 590), f.L(80));
        _targets.Add("insulation", f.P(25, 1400), f.L(30));
        AddBoxTarget("heatout", new Rect(f.P(712, 1690), f.P(790, 440)));
        AddBoxTarget("condenser", new Rect(f.P(615, 1690), f.P(700, 320)));
        AddBoxTarget("capillary", new Rect(f.P(574, 1640), f.P(602, 350)));
        AddBoxTarget("evaporator", new Rect(f.P(455, 1745), f.P(548, 1290)));
        _targets.Add("compressor", f.P(530, 150), f.L(80));
        var door = DoorRect(f);
        door = door.Inflate(new Thickness(4, 4));
        AddBoxTarget("door", door);
    }

    #region the room and the cabinet (drawn once per size and room temperature)

    private IImage BuildFridgeStatic(FridgeFrame f, double width, double height, double scale)
    {
        return SpaceDrawing.RenderToBitmap(width, height, scale, dc =>
        {
            // The room: a wall warming in colour with its temperature, and the floor.
            var warm = Math.Clamp((Room - 10) / 30, 0, 1);
            var wall = Lerp(Color.FromRgb(0x2A, 0x33, 0x44), Color.FromRgb(0x4A, 0x34, 0x2C), warm);
            dc.DrawRectangle(SpaceDrawing.LinearGradient(Lerp(wall, Colors.Black, 0.35), wall, 90), null, new Rect(0, 0, width, height));
            var floor = f.P(0, 0).Y;
            dc.DrawRectangle(Ink.Floor, null, new Rect(0, floor, width, Math.Max(0, height - floor)));
            dc.DrawLine(Ink.FloorEdge, new Point(0, floor), new Point(width, floor));

            // The wall behind the fridge, and the socket its cable goes to.
            var wallX = f.P(800, 0).X;
            dc.DrawRectangle(Ink.BackWall, null, new Rect(new Point(wallX, 0), new Point(width, floor)));
            dc.DrawLine(Ink.FloorEdge, new Point(wallX, 0), new Point(wallX, floor));
            dc.DrawRoundedRectangle(Ink.Socket, Ink.Edge, new Rect(f.P(790, 380), f.P(806, 300)), 2, 2);

            DrawCabinet(dc, f);
        });
    }

    private void DrawCabinet(PaintContext dc, FridgeFrame f)
    {
        // Feet, then the outer shell with the compressor's bay cut out at the bottom of the back.
        dc.DrawRectangle(Ink.MetalDark, null, new Rect(f.P(20, 30), f.P(70, 0)));
        dc.DrawRectangle(Ink.MetalDark, null, new Rect(f.P(530, 30), f.P(580, 0)));

        var shell = Shape(f.P(0, 30), f.P(0, 1800), f.P(600, 1800), f.P(600, 270), f.P(430, 270), f.P(430, 30));
        dc.DrawGeometry(Ink.Insulation, Ink.Edge, shell);

        // The insides: the freezer at the top, the fridge below, the plastic liner round both.
        var freezer = new Rect(f.P(50, 1750), f.P(550, 1250));
        var fridge = Shape(f.P(50, 1180), f.P(550, 1180), f.P(550, 330), f.P(380, 330), f.P(380, 80), f.P(50, 80));
        var freezerFill = SpaceDrawing.LinearGradient(Color.FromRgb(0x1C, 0x3A, 0x5E), Color.FromRgb(0x14, 0x2A, 0x46), 90);
        var fridgeFill = SpaceDrawing.LinearGradient(Color.FromRgb(0x24, 0x40, 0x5A), Color.FromRgb(0x1A, 0x30, 0x46), 90);
        dc.DrawRectangle(freezerFill, Ink.Liner, freezer);
        dc.DrawGeometry(fridgeFill, Ink.Liner, fridge);

        // A light inside when the door is open.
        if (DoorOpen)
        {
            var light = SpaceDrawing.RadialGradient((Color.FromArgb(90, 0xFF, 0xF4, 0xD8), 0), (Color.FromArgb(0, 0xFF, 0xF4, 0xD8), 1));
            dc.DrawEllipse(light, null, f.P(300, 1050), f.L(380), f.L(380));
        }

        // Frost on the evaporator's wall, shelves, and what is kept inside.
        dc.DrawRectangle(Ink.Frost, null, new Rect(f.P(455, 1745), f.P(548, 1290)));
        foreach (var y in new[] { 470.0, 820 })
        {
            dc.DrawLine(Ink.Shelf, f.P(52, y), f.P(548, y));
        }

        dc.DrawLine(Ink.Shelf, f.P(52, 1500), f.P(440, 1500));
        DrawFood(dc, f);

        // The doors: the freezer's on top, the fridge's below (swung out when open).
        dc.DrawRoundedRectangle(Ink.Door, Ink.Edge, new Rect(f.P(-60, 1790), f.P(0, 1240)), f.L(8), f.L(8));
        var door = DoorRect(f);
        dc.DrawRoundedRectangle(DoorOpen ? Ink.DoorAjar : Ink.Door, Ink.Edge, door, f.L(8), f.L(8));
        dc.DrawRoundedRectangle(Ink.Handle, null, new Rect(DoorOpen ? f.P(-200, 1160) : f.P(-70, 1160), DoorOpen ? f.P(-190, 980) : f.P(-60, 980)), 3, 3);
        if (!DoorOpen) dc.DrawLine(Ink.Seal, f.P(-1, 1230), f.P(-1, 40));

        // Thermometers inside, and the room's on the wall.
        DrawThermometer(dc, f.P(120, 1300), f.L(150), FridgeModel.FreezerTemperature);
        DrawThermometer(dc, f.P(120, 520), f.L(150), FridgeModel.FridgeTemperature);
        DrawThermometer(dc, f.P(-430, 620), f.L(200), Room);
        DrawText(dc, $"{Number(FridgeModel.FreezerTemperature, "0")} °C", f.P(150, 1380), Ink.Cyan, 12, bold: true);
        DrawText(dc, $"{Number(FridgeModel.FridgeTemperature, "0")} °C", f.P(150, 600), Ink.Cyan, 12, bold: true);
        DrawText(dc, $"{Number(Room, "0")} °C", f.P(-405, 710), Ink.Orange, 12, bold: true);

        // The wire grid holding the condenser, and the cable from the socket.
        dc.DrawLine(Ink.Grid2, f.P(625, 1700), f.P(625, 310));
        dc.DrawLine(Ink.Grid2, f.P(690, 1700), f.P(690, 310));
        dc.DrawGeometry(null, Ink.Cable, Line([f.P(798, 330), f.P(760, 300), f.P(700, 150), f.P(600, 110), f.P(575, 130)]));

        // The tubes, coloured along their length by the refrigerant's temperature.
        for (var part = 0; part < LoopPaths.Length; part++)
        {
            var thickness = (FridgePart)part switch
            {
                FridgePart.Capillary => 2.4,
                FridgePart.Compressor => 0,
                _ => 7,
            };
            if (thickness <= 0) continue;
            var start = FridgeModel.PartStart((FridgePart)part);
            var end = FridgeModel.PartEnd((FridgePart)part);
            const int pieces = 60;
            for (var i = 0; i < pieces; i++)
            {
                var s0 = start + ((end - start) * i / pieces);
                var s1 = start + ((end - start) * (i + 1) / pieces);
                var color = Quantize(HeatColor(FridgeModel.At((s0 + s1) / 2, Room).Temperature));
                var pen = new ImmutablePen(((IBrush)SpaceDrawing.Solid(Lerp(color, Colors.Black, 0.25))).ToImmutable(), Math.Max(1.4, f.L(thickness)), null, PenLineCap.Round, PenLineJoin.Miter);
                dc.DrawLine(pen, f.P(LoopPoint(s0).X, LoopPoint(s0).Y), f.P(LoopPoint(s1).X, LoopPoint(s1).Y));
            }
        }

        // The filter-drier where the condenser ends and the capillary starts.
        dc.DrawRoundedRectangle(Ink.MetalDark, Ink.Edge, new Rect(f.P(598, 306), f.P(632, 276)), 3, 3);
    }

    private static void DrawFood(PaintContext dc, FridgeFrame f)
    {
        // A milk carton and a bowl of fruit in the fridge; boxes and an ice tray in the freezer.
        dc.DrawGeometry(Ink.Carton, Ink.Edge, Shape(f.P(180, 820), f.P(180, 1040), f.P(205, 1080), f.P(250, 1080), f.P(275, 1040), f.P(275, 820)));
        dc.DrawRectangle(Ink.CartonBand, null, new Rect(f.P(180, 960), f.P(275, 930)));
        foreach (var (x, y) in new[] { (330.0, 510.0), (385, 510), (357, 555) })
        {
            dc.DrawEllipse(Ink.Apple, Ink.Edge, f.P(x, y), f.L(28), f.L(28));
        }

        dc.DrawRoundedRectangle(Ink.Bowl, Ink.Edge, new Rect(f.P(300, 500), f.P(415, 472)), 4, 4);
        dc.DrawRoundedRectangle(Ink.Box, Ink.Edge, new Rect(f.P(200, 1650), f.P(380, 1505)), 4, 4);
        dc.DrawRoundedRectangle(Ink.Box2, Ink.Edge, new Rect(f.P(290, 1480), f.P(440, 1290)), 4, 4);
        dc.DrawRoundedRectangle(Ink.Bottle, Ink.Edge, new Rect(f.P(420, 1180 - 20), f.P(470, 830)), 6, 6);
    }

    #endregion

    #region what moves: the refrigerant, the compressor, the heat

    private void DrawRefrigerant(PaintContext dc, FridgeFrame f)
    {
        for (var i = 0; i < LoopParticles; i++)
        {
            var s = Frac((i / (double)LoopParticles) + _loop);
            var point = FridgeModel.At(s, Room);
            if (point.Part == FridgePart.Compressor) continue;
            var at = LoopPoint(s);
            var screen = f.P(at.X, at.Y);
            var color = Quantize(HeatColor(point.Temperature));

            // Vapour as a hollow bubble, liquid as a small solid drop, the wet mixture in between.
            var radius = Math.Max(1.6, f.L(4 + (6 * point.Vapour)));
            if (point.Vapour > 0.5)
            {
                dc.DrawEllipse(FadedBrush(color, 0.25), FadedPen(color, 0.95, 1.3), screen, radius, radius);
            }
            else
            {
                dc.DrawEllipse(FadedBrush(color, 0.95), null, screen, radius * 0.75, radius * 0.75);
            }
        }
    }

    private void DrawCompressor(PaintContext dc, FridgeFrame f)
    {
        // The black dome, shaking a little while it runs.
        var shake = Math.Sin(_t * 70) * f.L(1.2);
        var centre = f.P(530, 150) + new Vector(shake, 0);
        dc.DrawRoundedRectangle(Ink.CompressorFoot, null, new Rect(f.P(462, 64), f.P(598, 40)), 3, 3);
        var dome = new StreamGeometry();
        using (var context = dome.Open())
        {
            context.BeginFigure(centre + new Vector(-f.L(62), f.L(84)), true);
            context.LineTo(centre + new Vector(-f.L(62), -f.L(20)));
            context.CubicBezierTo(centre + new Vector(-f.L(62), -f.L(92)), centre + new Vector(f.L(62), -f.L(92)), centre + new Vector(f.L(62), -f.L(20)));
            context.LineTo(centre + new Vector(f.L(62), f.L(84)));

            context.EndFigure(true);
        }
        dc.DrawGeometry(Ink.Compressor, Ink.LightEdge, dome);

        // A lightning bolt: it runs on electricity.
        var k = f.L(1);
        var bolt = Shape(centre + new Vector(6 * k, -30 * k), centre + new Vector(-14 * k, 6 * k), centre + new Vector(-1 * k, 6 * k),
            centre + new Vector(-6 * k, 34 * k), centre + new Vector(14 * k, -4 * k), centre + new Vector(1 * k, -4 * k));
        dc.DrawGeometry(FadedBrush(Ink.Gold, 0.95), null, bolt);
    }

    private void DrawFridgeHeat(PaintContext dc, FridgeFrame f)
    {
        var size = Math.Max(1.4, f.L(7));

        // Heat taken out of the inside by the cold evaporator…
        foreach (var y in new[] { 1640.0, 1440 })
        {
            DrawHeatArrow(dc, f.P(250, y), f.P(445, y + 10), Ink.Gold, size, 0.85);
        }

        // …including the fridge's, carried up to it by the air.
        DrawHeatArrow(dc, f.P(300, 700), f.P(460, 1240), Ink.Gold, size, 0.6);

        // …and given out to the room from the warm condenser at the back.
        foreach (var y in new[] { 1500.0, 1150, 800, 450 })
        {
            DrawHeatArrow(dc, f.P(712, y), f.P(780, y + 170), Ink.Orange, size, 0.85);
        }

        // Warm room air pours in at the top of an open door, cold air spills out at the bottom.
        if (DoorOpen)
        {
            foreach (var y in new[] { 1100.0, 980 })
            {
                DrawHeatArrow(dc, f.P(-280, y), f.P(30, y - 40), Ink.Orange, size, 0.9);
            }

            foreach (var y in new[] { 200.0, 110 })
            {
                DrawArrow(dc, f.P(40, y + 40), f.P(-260, y - 10), FadedBrush(Ink.Cyan, 0.8), Math.Max(1.4, f.L(6)), Math.Max(7, f.L(30)));
            }
        }
    }

    private void DrawFridgeLabels(PaintContext dc, FridgeFrame f)
    {
        var room = Room;
        var high = FridgeModel.HighPressure(room);
        var low = FridgeModel.LowPressure;
        var right = f.P(830, 0).X;

        DrawCallout(dc, LocFormat("Heat_Fridge_Label_Compressor", Number(FridgeModel.Discharge(room), "0"), Number(high, "0.0")), f.P(560, 215), new Point(right, f.P(0, 200).Y), right: true, "compressor");
        DrawCallout(dc, LocFormat("Heat_Fridge_Label_Condenser", Number(FridgeModel.Condensing(room), "0")), f.P(690, 1000), new Point(right, f.P(0, 1000).Y), right: true, "condenser");
        DrawCallout(dc, LocFormat("Heat_Fridge_Label_Hot", Number(room, "0")), f.P(740, 1650), new Point(right, f.P(0, 1700).Y), right: true, "heatout");
        DrawCallout(dc, LocFormat("Heat_Fridge_Label_Capillary", Number(high, "0.0"), Number(low, "0.0")), f.P(596, 450), new Point(right, f.P(0, 520).Y), right: true, "capillary");

        var left = f.P(DoorOpen ? -230 : -100, 0).X;
        DrawCallout(dc, LocFormat("Heat_Fridge_Label_Evaporator", Number(FridgeModel.Evaporating, "0"), Number(low, "0.0")), f.P(470, 1600), new Point(left, f.P(0, 1600).Y), right: false, "evaporator");
        DrawCallout(dc, Loc("Heat_Fridge_Label_Insulation"), f.P(25, 1400), new Point(left, f.P(0, 1400).Y), right: false, "insulation");
        DrawCallout(dc, Loc(DoorOpen ? "Heat_Fridge_Label_DoorOpen" : "Heat_Fridge_Label_Door"), f.P(DoorOpen ? -170 : -30, 600), new Point(left, f.P(0, 300).Y), right: false, "door");
    }

    #endregion
}
