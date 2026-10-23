using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.PowerGrid;

/// <summary>
/// Balance mode. Left, the grid's stations one per row with what each makes against what it could; their lines join a
/// bus feeding the city on the right, under a sky that follows the hour. In the middle the grid's frequency on a dial and
/// the machines' spinning mass as a wheel (as big as the energy it stores); after a trip, the frequency over the next
/// three minutes, while the stations that answer it show how much more they give.
/// </summary>
public sealed partial class GridDiagram
{
    /// <summary>The kinds of station, in the order of their rows (and the target ids).</summary>
    internal static readonly string[] Sources = ["nuclear", "coal", "gas", "hydro", "pumped", "wind", "solar", "import"];

    /// <summary>Each kind's colour, shared with the side panel's charts.</summary>
    internal static readonly Color[] SourceColors =
    [
        Color.FromRgb(0xB3, 0x9D, 0xFF),
        Color.FromRgb(0xA8, 0x92, 0x80),
        Color.FromRgb(0xFF, 0x9F, 0x5A),
        Color.FromRgb(0x5A, 0xA8, 0xFF),
        Color.FromRgb(0x6F, 0xD8, 0xFF),
        Color.FromRgb(0x8B, 0xE0, 0x8F),
        Color.FromRgb(0xFF, 0xD3, 0x6B),
        Color.FromRgb(0xFF, 0x8A, 0xC8),
    ];

    /// <summary>Each kind's capacity, GW (solar's is the slider's).</summary>
    private double Capacity(int source) => source switch
    {
        0 => GridDayModel.NuclearCapacity,
        1 => GridDayModel.CoalCapacity,
        2 => GridDayModel.GasCapacity,
        3 => GridDayModel.HydroCapacity,
        4 => GridDayModel.PumpedCapacity,
        5 => GridDayModel.WindCapacity * (Season == GridSeason.Winter ? 1.2 : 1),
        6 => Math.Max(0.1, SolarCapacity),
        _ => GridDayModel.ImportCapacity,
    };

    internal static double Output(GridMix mix, int source) => source switch
    {
        0 => mix.Nuclear,
        1 => mix.Coal,
        2 => mix.Gas,
        3 => mix.Hydro,
        4 => mix.Pumped,
        5 => mix.Wind,
        6 => mix.Solar,
        _ => mix.Import,
    };

    /// <summary>Whether a kind of station answers a change in frequency (its governors and the control centre's calls).</summary>
    private static bool Answers(int source) => source is 1 or 2 or 3 or 4;

    /// <summary>The spinning wheel's angle, turned at a pace that follows the frequency (slowing exaggerated).</summary>
    private double _wheel;

    private double _lastWheelTime;

    private void DrawBalanceMode(PaintContext dc, double width, double height)
    {
        var mix = GridDayModel.Mix(Season, Latitude, Hour, SolarCapacity);
        var sun = GridDayModel.SunHeight(Season, Latitude, Hour);
        DrawSkyBackdrop(dc, width, height, sun);
        var stage = Stage();

        var running = EventKind != GridEvent.None && EventTrace is { Count: > 0 };
        var point = running ? FrequencyModel.At(EventTrace!, _eventTime) : default;
        var frequency = running ? point.Frequency : FrequencyModel.Nominal + (0.012 * Math.Sin(_t * 0.7)) + (0.006 * Math.Sin(_t * 2.3));

        var rowsWidth = Math.Clamp(stage.Width * 0.36, 230, 420);
        var rows = new Rect(stage.Left + 22, stage.Top + 4, rowsWidth, stage.Height - 8);
        var busX = rows.Right + 26;
        var midWidth = Math.Clamp(stage.Width * 0.26, 170, 300);
        var middle = new Rect(busX + 26, stage.Top, midWidth, stage.Height);
        var city = new Rect(middle.Right + 18, stage.Top + 4, Math.Max(80, stage.Right - middle.Right - 40), stage.Height - 8);

        var (busTop, busBottom) = DrawStationRows(dc, rows, busX, mix, running ? point : null);
        DrawBus(dc, busX, busTop, busBottom, new Point(city.Left + 6, city.Bottom - (city.Height * 0.25)), mix);
        DrawCity(dc, city, mix, sun, running ? point : null);
        DrawFrequency(dc, middle, frequency, mix, running ? point : null);
    }

    /// <summary>The page's dark backdrop, a little bluer by day.</summary>
    private void DrawSkyBackdrop(PaintContext dc, double width, double height, double sun)
    {
        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        var day = Smooth(-0.1, 0.4, sun);
        if (day > 0.02) dc.DrawRectangle(FadedBrush(Color.FromRgb(0x2A, 0x4A, 0x7A), 0.35 * day), null, new Rect(0, 0, width, height));
    }

    /// <summary>One row per kind of station; returns the top and bottom of the bus their lines join.</summary>
    private (double Top, double Bottom) DrawStationRows(PaintContext dc, Rect area, double busX, GridMix mix, FrequencyPoint? point)
    {
        var rowHeight = area.Height / Sources.Length;
        var icon = Math.Clamp(rowHeight * 0.72, 18, 40);
        var governed = GridDayModel.GovernedCapacity(mix);
        var extra = point is { } p ? p.Governors + p.Control : 0;
        double top = 0, bottom = 0;
        for (var i = 0; i < Sources.Length; i++)
        {
            var id = Sources[i];
            var color = SourceColors[i];
            var y = area.Top + (rowHeight * (i + 0.5));
            var output = Output(mix, i);

            // What the event changes: the tripped unit, the lost link, the answering stations' extra.
            var tripped = (EventKind == GridEvent.Trip && i == 1) || (EventKind == GridEvent.Link && i == 7);
            if (point is not null)
            {
                if (EventKind == GridEvent.Trip && i == 1) output -= FrequencyModel.Size(GridEvent.Trip);
                if (EventKind == GridEvent.Link && i == 7) output = 0;
            }

            var answer = point is not null && Answers(i) && governed > 0
                ? extra * (i switch { 1 => mix.Coal, 2 => mix.Gas, 3 => mix.Hydro, _ => Math.Max(0, mix.Pumped) } / 0.85) / governed
                : 0;

            var iconCenter = new Point(area.Left + (icon / 2), y);
            DrawSourceIcon(dc, i, iconCenter, icon, output / Capacity(i));
            var textLeft = area.Left + icon + 10;
            DrawText(dc, Loc(ObjectTitleKey(id)!), new Point(textLeft, y - (rowHeight * 0.18)), Ink.Text, 11.5);
            var value = $"{Number(output + answer, "0.0")} GW";
            DrawTextRight(dc, value, new Point(area.Right, y - (rowHeight * 0.18)), color, 11.5, bold: true);

            // The bar: what it makes out of what it could (pumped storage: left of the middle while pumping).
            var bar = new Rect(textLeft, y + (rowHeight * 0.06), Math.Max(30, area.Right - textLeft), Math.Clamp(rowHeight * 0.18, 4, 9));
            dc.DrawRoundedRectangle(FadedBrush(Ink.Text, 0.08), null, bar, 2, 2);
            if (i == 4)
            {
                var zero = bar.Left + (bar.Width / 2);
                var size = output / Capacity(i) * (bar.Width / 2);
                dc.DrawLine(FadedPen(Ink.Text, 0.4, 1), new Point(zero, bar.Top - 2), new Point(zero, bar.Bottom + 2));
                var fill = size >= 0 ? new Rect(zero, bar.Top, size, bar.Height) : new Rect(zero + size, bar.Top, -size, bar.Height);
                dc.DrawRectangle(FadedBrush(color, size >= 0 ? 0.9 : 0.55), null, fill);
                if (output < -0.05) DrawText(dc, Loc("Grid_Bal_Pumping"), new Point(bar.Left, bar.Bottom + 8), Ink.Cyan, 10);
            }
            else
            {
                var share = Math.Clamp(output / Capacity(i), 0, 1);
                dc.DrawRoundedRectangle(FadedBrush(color, 0.9), null, new Rect(bar.Left, bar.Top, bar.Width * share, bar.Height), 2, 2);
                if (i == 6 && mix.Curtailed > 0.05)
                {
                    var spilled = Math.Clamp(mix.Curtailed / Capacity(i), 0, 1 - share);
                    var hatch = new Rect(bar.Left + (bar.Width * share), bar.Top, bar.Width * spilled, bar.Height);
                    dc.DrawRectangle(FadedBrush(color, 0.25), FadedPen(color, 0.6, 1), hatch);
                    DrawText(dc, Loc("Grid_Bal_Curtailed"), new Point(bar.Left, bar.Bottom + 8), Ink.Gold, 10);
                }
            }

            if (answer > 0.005)
            {
                DrawTextRight(dc, $"+{Number(answer * 1000, "0")} MW", new Point(area.Right, bar.Bottom + 8), Ink.Green, 10, bold: true);
            }

            if (tripped && point is not null)
            {
                var text = EventKind == GridEvent.Trip ? Loc("Grid_Bal_Tripped") : Loc("Grid_Bal_LinkLost");
                DrawPill(dc, text, new Point(area.Left + (area.Width * 0.6), y - (rowHeight * 0.18)), Ink.Red, 10);
            }

            // The line to the bus, with dots as fast as what flows (towards the bus, or from it while pumping).
            var from = new Point(area.Right + 6, y);
            var to = new Point(busX, y);
            dc.DrawLine(FadedPen(color, 0.45, 1.4), from, to);
            var flow = output + answer;
            if (Math.Abs(flow) > 0.05)
            {
                var speed = 18 + (Math.Abs(flow) * 4);
                var offset = Frac(_t * speed / 14) * Math.Sign(flow);
                for (var k = 0; k < 3; k++)
                {
                    var s = Frac((k / 3.0) + (offset / 3));
                    dc.DrawEllipse(FadedBrush(color, 0.95), null, Mix(from, to, s), 1.8, 1.8);
                }
            }

            var rowBox = new Rect(area.Left - 4, y - (rowHeight * 0.46), area.Width + 8, rowHeight * 0.92);
            if (Glows(id)) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), rowBox, 8, 8);
            _targets.AddBox(id, rowBox);
            if (i == 0) top = y;
            bottom = y;
        }

        return (top, bottom);
    }

    /// <summary>A small picture of a kind of station; <paramref name="level"/> is how hard it is working (0…1, negative pumping).</summary>
    private void DrawSourceIcon(PaintContext dc, int source, Point c, double size, double level)
    {
        var color = SourceColors[source];
        var s = size / 2;
        var body = FadedBrush(color, 0.85);
        var dark = FadedBrush(Lerp(color, Colors.Black, 0.45), 1);
        switch (source)
        {
            case 0:
                // A reactor dome and a cooling tower.
                dc.DrawGeometry(dark, null, Shape(new Point(c.X - s, c.Y + s), new Point(c.X - (s * 0.75), c.Y - (s * 0.7)), new Point(c.X - (s * 0.15), c.Y - (s * 0.7)), new Point(c.X + (s * 0.1), c.Y + s)));
                var dome = new StreamGeometry();
                using (var context = dome.Open())
                {
                    context.BeginFigure(new Point(c.X + (s * 0.15), c.Y + s), true);
                    context.LineTo(new Point(c.X + (s * 0.15), c.Y));
                    context.ArcTo(new Point(c.X + s, c.Y), new Size(s * 0.42, s * 0.5), 0, false, SweepDirection.Clockwise);
                    context.LineTo(new Point(c.X + s, c.Y + s));

                    context.EndFigure(true);
                }
                dc.DrawGeometry(body, null, dome);
                break;

            case 1:
                // A hall and a chimney with smoke.
                dc.DrawRectangle(dark, null, new Rect(c.X - s, c.Y - (s * 0.1), s * 1.4, s * 1.1));
                dc.DrawRectangle(body, null, new Rect(c.X + (s * 0.5), c.Y - s, s * 0.32, s * 2));
                for (var k = 0; k < 2; k++)
                {
                    var age = Frac((_t * 0.3) + (k * 0.5));
                    dc.DrawEllipse(FadedBrush(Ink.Text, 0.3 * (1 - age) * Math.Clamp(level, 0.2, 1)), null, new Point(c.X + (s * 0.66) + (age * s * 0.5), c.Y - s - (age * s * 0.6)), s * (0.2 + (0.25 * age)), s * (0.16 + (0.2 * age)));
                }

                break;

            case 2:
                // A turbine hall with a short stack.
                dc.DrawRoundedRectangle(dark, null, new Rect(c.X - s, c.Y - (s * 0.2), s * 2, s * 1.2), 3, 3);
                dc.DrawRectangle(body, null, new Rect(c.X - (s * 0.6), c.Y - (s * 0.85), s * 0.4, s * 0.7));
                dc.DrawEllipse(level > 0.05 ? FadedBrush(Ink.Orange, 0.6 + (0.3 * Math.Sin(_t * 9))) : FadedBrush(Ink.Text, 0.1), null, new Point(c.X + (s * 0.4), c.Y + (s * 0.4)), s * 0.28, s * 0.28);
                break;

            case 3:
                // A dam with water behind it and a jet below.
                dc.DrawRectangle(FadedBrush(Ink.Blue, 0.5), null, new Rect(c.X - s, c.Y - (s * 0.5), s * 0.9, s * 1.5));
                dc.DrawGeometry(dark, null, Shape(new Point(c.X - (s * 0.15), c.Y - s), new Point(c.X + (s * 0.15), c.Y - s), new Point(c.X + (s * 0.55), c.Y + s), new Point(c.X - (s * 0.15), c.Y + s)));
                if (level > 0.05) dc.DrawLine(FadedPen(Ink.Field, 0.8, Math.Max(1, s * 0.25 * level)), new Point(c.X + (s * 0.45), c.Y + (s * 0.6)), new Point(c.X + s, c.Y + (s * 0.85)));
                break;

            case 4:
                // Two reservoirs, an arrow up (pumping) or down (generating).
                dc.DrawRectangle(body, null, new Rect(c.X - s, c.Y - s, s * 0.9, s * 0.5));
                dc.DrawRectangle(body, null, new Rect(c.X + (s * 0.1), c.Y + (s * 0.5), s * 0.9, s * 0.5));
                var up = level < -0.02;
                var a = new Point(c.X - (s * 0.2), up ? c.Y + (s * 0.55) : c.Y - (s * 0.45));
                var b = new Point(c.X + (s * 0.3), up ? c.Y - (s * 0.45) : c.Y + (s * 0.55));
                if (Math.Abs(level) > 0.02) DrawArrow(dc, a, b, FadedBrush(Ink.Text, 0.8), 1.6, 6);
                break;

            case 5:
                // A wind turbine, its blades turning with its output.
                var hub = new Point(c.X, c.Y - (s * 0.35));
                dc.DrawLine(FadedPen(Ink.Text, 0.75, 2), hub, new Point(c.X, c.Y + s));
                var spin = (_t * (0.4 + (2.4 * Math.Max(0, level)))) + source;
                for (var k = 0; k < 3; k++)
                {
                    var angle = spin + (k * 2 * Math.PI / 3);
                    dc.DrawLine(FadedPen(color, 0.95, 2), hub, hub + new Vector(Math.Cos(angle) * s * 0.75, Math.Sin(angle) * s * 0.75));
                }

                dc.DrawEllipse(Brushes.White, null, hub, 2, 2);
                break;

            case 6:
                // A tilted panel with its cells.
                var panel = Shape(new Point(c.X - s, c.Y + (s * 0.6)), new Point(c.X - (s * 0.5), c.Y - (s * 0.6)), new Point(c.X + s, c.Y - (s * 0.6)), new Point(c.X + (s * 0.5), c.Y + (s * 0.6)));
                dc.DrawGeometry(FadedBrush(Color.FromRgb(0x2A, 0x4A, 0x8A), 1), FadedPen(color, 0.4 + (0.6 * Math.Clamp(level, 0, 1)), 1.4), panel);
                for (var k = 1; k < 3; k++)
                {
                    var t = k / 3.0;
                    dc.DrawLine(FadedPen(Ink.Text, 0.25, 0.8), Mix(new Point(c.X - s, c.Y + (s * 0.6)), new Point(c.X + (s * 0.5), c.Y + (s * 0.6)), t), Mix(new Point(c.X - (s * 0.5), c.Y - (s * 0.6)), new Point(c.X + s, c.Y - (s * 0.6)), t));
                }

                dc.DrawLine(FadedPen(Ink.Text, 0.25, 0.8), new Point(c.X - (s * 0.75), c.Y), new Point(c.X + (s * 0.75), c.Y));
                break;

            default:
                // A pylon and the letters DC.
                dc.DrawGeometry(null, FadedPen(color, 0.9, 1.4), Shape(new Point(c.X - (s * 0.6), c.Y + s), new Point(c.X - (s * 0.15), c.Y - s), new Point(c.X + (s * 0.15), c.Y - s), new Point(c.X + (s * 0.6), c.Y + s)));
                dc.DrawLine(FadedPen(color, 0.9, 1.4), new Point(c.X - (s * 0.8), c.Y - (s * 0.5)), new Point(c.X + (s * 0.8), c.Y - (s * 0.5)));
                DrawText(dc, "DC", new Point(c.X, c.Y + (s * 0.25)), Ink.Text, Math.Max(8, s * 0.55), center: true, bold: true);
                break;
        }
    }

    /// <summary>The bus joining the stations, and the thick line from it to the city.</summary>
    private void DrawBus(PaintContext dc, double x, double top, double bottom, Point city, GridMix mix)
    {
        var pen = FadedPen(Ink.Text, 0.7, 4);
        if (Glows("bus")) dc.DrawLine(ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 12), new Point(x, top), new Point(x, bottom));
        dc.DrawLine(pen, new Point(x, top), new Point(x, bottom));
        var mid = new Point(x, (top + bottom) / 2);
        var path = new List<Point> { mid, new(mid.X + 14, mid.Y), new(mid.X + 14, city.Y), city };
        if (Glows("bus")) dc.DrawGeometry(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 12), Line(path));
        dc.DrawGeometry(null, FadedPen(Ink.Text, 0.5, 3), Line(path));
        var total = 0.0;
        for (var i = 1; i < path.Count; i++) total += ((Vector)(path[i] - path[i - 1])).Length;
        var spacing = 16.0;
        var offset = Frac(_t * (20 + mix.Demand) / spacing);
        for (var i = 0; i < (int)(total / spacing); i++) dc.DrawEllipse(FadedBrush(Ink.Gold, 0.9), null, Along(path, (i + offset) * spacing / total), 2, 2);
        _targets.AddBox("bus", new Rect(x - 6, top, 12, bottom - top));
    }

    /// <summary>The city under the sky: its windows lit by what it uses, the hour, its demand; shed blocks go dark.</summary>
    private void DrawCity(PaintContext dc, Rect area, GridMix mix, double sun, FrequencyPoint? point)
    {
        // The sky over the city, the Sun or the Moon in it.
        var day = Smooth(-0.1, 0.35, sun);
        var sky = new Rect(area.Left, area.Top, area.Width, area.Height * 0.62);
        var skyBrush = SpaceDrawing.LinearGradient(Lerp(Color.FromRgb(0x0A, 0x10, 0x22), Color.FromRgb(0x3A, 0x7A, 0xC8), day), Lerp(Color.FromRgb(0x18, 0x1E, 0x34), Lerp(Color.FromRgb(0xF0, 0x9A, 0x5A), Color.FromRgb(0x9A, 0xC8, 0xF0), Smooth(0.15, 0.5, sun)), day), 90);
        dc.DrawRoundedRectangle(skyBrush, null, sky, 10, 10);
        var arc = (GridDayModel.Wrap(Hour) - 6) / 12 * Math.PI;
        var sunAt = new Point(sky.Left + (sky.Width * (0.5 - (0.42 * Math.Cos(arc)))), sky.Bottom - (sky.Height * 0.8 * Math.Max(0, sun)) - 8);
        if (sun > -0.05)
        {
            dc.DrawEllipse(FadedBrush(Color.FromRgb(0xFF, 0xE0, 0x7A), 0.95), null, sunAt, 9, 9);
        }
        else
        {
            dc.DrawEllipse(FadedBrush(Ink.Text, 0.8), null, new Point(sky.Left + (sky.Width * 0.75), sky.Top + 22), 7, 7);
        }

        DrawText(dc, ClockText(Hour), new Point(sky.Left + 12, sky.Top + 16), Ink.Text, 18, bold: true);

        // Buildings: lit windows by demand, the shed share dark.
        var ground = area.Bottom - 6;
        var lit = Math.Clamp((mix.Demand - 18) / 24, 0.15, 1) * (1 - (0.7 * day));
        var shedShare = point is { } p && mix.Demand > 0 ? p.Shed / mix.Demand : 0;
        var widths = new[] { 0.16, 0.12, 0.2, 0.14, 0.18, 0.2 };
        var heights = new[] { 0.42, 0.62, 0.5, 0.75, 0.36, 0.55 };
        var x = area.Left + 4;
        var available = area.Width - 8;
        for (var i = 0; i < widths.Length; i++)
        {
            var w = available * widths[i];
            var h = area.Height * heights[i];
            var building = new Rect(x, ground - h, w - 3, h);
            var dark = shedShare > 0 && i >= widths.Length - (int)Math.Ceiling(shedShare * 20);
            dc.DrawRectangle(SpaceDrawing.LinearGradient(Color.FromRgb(0x34, 0x3A, 0x4A), Color.FromRgb(0x1C, 0x20, 0x2A), 90), Ink.Edge, building);
            var rows = Math.Max(1, (int)(h / 13));
            var cols = Math.Max(1, (int)(w / 11));
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var on = !dark && Frac(((r * 7) + (c * 13) + (i * 5)) * 0.6180339887) < lit;
                    var window = new Rect(building.Left + 3 + (c * (building.Width - 4) / cols), building.Top + 4 + (r * (building.Height - 6) / rows),
                        Math.Max(2, ((building.Width - 4) / cols) - 4), Math.Max(2, ((building.Height - 6) / rows) - 5));
                    dc.DrawRectangle(on ? FadedBrush(Ink.Gold, 0.85) : FadedBrush(Ink.Text, day > 0.5 ? 0.18 : 0.06), null, window);
                }
            }

            x += w;
        }

        var box = new Rect(area.Left, area.Top + (area.Height * 0.3), area.Width, area.Height * 0.7);
        if (Glows("city")) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), box, 8, 8);
        _targets.AddBox("city", box);

        var demand = mix.Demand + (EventKind == GridEvent.Surge && point is not null ? FrequencyModel.Size(GridEvent.Surge) : 0);
        DrawPill(dc, LocFormat("Grid_Bal_Demand", Number(demand, "0.0")), new Point(area.Left + (area.Width / 2), sky.Bottom - 14), Ink.Text, 12);
        if (EventKind == GridEvent.Surge && point is not null)
        {
            DrawPill(dc, Loc("Grid_Bal_SurgeLabel"), new Point(area.Left + (area.Width / 2), sky.Bottom + 12), Ink.Red, 11);
        }

        if (shedShare > 0)
        {
            DrawPill(dc, LocFormat("Grid_Bal_ShedLabel", Number(point!.Value.Shed, "0.0")), new Point(area.Right - (area.Width * 0.25), ground - (area.Height * 0.62)), Ink.Red, 11);
        }
    }

    /// <summary>The frequency dial, the spinning wheel and, after an event, the frequency over the following minutes.</summary>
    private void DrawFrequency(PaintContext dc, Rect area, double frequency, GridMix mix, FrequencyPoint? point)
    {
        // The dial: 49 to 51 Hz across the top half of a circle.
        var radius = Math.Clamp(Math.Min(area.Width * 0.44, area.Height * 0.24), 50, 130);
        var center = new Point(area.Left + (area.Width / 2), area.Top + radius + 14);
        Point On(double hz, double r)
        {
            var angle = Math.PI * (1 - Math.Clamp((hz - 49) / 2, 0, 1));
            return center + new Vector(Math.Cos(angle) * r, -Math.Sin(angle) * r);
        }

        dc.DrawGeometry(FadedBrush(Color.FromRgb(0x10, 0x16, 0x22), 0.9), Ink.Frame, Half(center, radius + 10));
        foreach (var (from, to, color) in new[]
                 {
                     (49.0, 49.5, Ink.Red), (49.5, 49.8, Ink.Orange), (49.8, 50.2, Ink.Green), (50.2, 50.5, Ink.Orange), (50.5, 51.0, Ink.Red),
                 })
        {
            var points = Enumerable.Range(0, 13).Select(i => On(from + ((to - from) * i / 12), radius * 0.9)).ToList();
            dc.DrawGeometry(null, FadedPen(color, 0.75, radius * 0.1, flat: true), Line(points));
        }

        for (var hz = 49.0; hz <= 51.001; hz += 0.1)
        {
            var major = Math.Abs((hz * 2) - Math.Round(hz * 2)) < 0.01;
            dc.DrawLine(FadedPen(Ink.Text, major ? 0.8 : 0.4, major ? 1.6 : 1), On(hz, radius * 0.78), On(hz, radius * (major ? 0.68 : 0.73)));
            if (major) DrawText(dc, Number(hz, "0.0"), On(hz, radius * 0.56), Ink.Dim, 10, center: true);
        }

        var tip = On(frequency, radius * 0.86);
        dc.DrawLine(MakePen(Brushes.White, 2.6), center, tip);
        dc.DrawEllipse(Ink.Metal, Ink.Edge, center, 6, 6);
        var color2 = Math.Abs(frequency - 50) < 0.2 ? Ink.Green : Math.Abs(frequency - 50) < 0.5 ? Ink.Orange : Ink.Red;
        DrawText(dc, $"{Number(frequency, "0.00")} Hz", center + new Vector(0, 22), color2, 20, center: true, bold: true);
        if (Glows("dial")) dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 6), center, radius + 12, radius + 12);
        _targets.Add("dial", center - new Vector(0, radius * 0.35), radius * 0.75);

        // The spinning wheel: as big as the energy the machines store, turning a little slower when the frequency falls.
        var inertia = GridDayModel.Inertia(mix);
        var energy = inertia * mix.Demand;
        var wheelRadius = Math.Clamp(radius * 0.62 * Math.Sqrt(energy / 200), radius * 0.22, radius * 0.75);
        var wheelCenter = new Point(center.X, center.Y + 48 + (radius * 0.62));
        var now = _clock.Elapsed.TotalSeconds;
        var dt = Math.Clamp(now - _lastWheelTime, 0, 0.05);
        _lastWheelTime = now;
        if (IsPlaying || IsLessonActive) _wheel += dt * Math.Clamp(Speed, 0.05, 8) * 2 * Math.PI * 0.45 * (1 + ((frequency - 50) * 1.2));
        var rim = FadedPen(Ink.Text, 0.75, Math.Max(3, wheelRadius * 0.14));
        dc.DrawEllipse(FadedBrush(Color.FromRgb(0x30, 0x36, 0x42), 0.95), rim, wheelCenter, wheelRadius, wheelRadius);
        for (var k = 0; k < 6; k++)
        {
            var angle = _wheel + (k * Math.PI / 3);
            dc.DrawLine(FadedPen(Ink.Text, 0.6, 2), wheelCenter, wheelCenter + new Vector(Math.Cos(angle) * wheelRadius * 0.9, Math.Sin(angle) * wheelRadius * 0.9));
        }

        dc.DrawEllipse(Ink.Metal, Ink.Edge, wheelCenter, wheelRadius * 0.18, wheelRadius * 0.18);
        if (Glows("flywheel")) dc.DrawEllipse(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 6), wheelCenter, wheelRadius + 6, wheelRadius + 6);
        _targets.Add("flywheel", wheelCenter, Math.Max(wheelRadius, 18));
        var below = wheelCenter.Y + Math.Max(wheelRadius, radius * 0.75 * 0.62) + 16;
        DrawText(dc, LocFormat("Grid_Bal_Inertia", Number(inertia, "0.0")), new Point(center.X, below), Ink.Dim, 11, center: true);

        // After an event: the frequency since, with the moment now marked.
        var chart = new Rect(area.Left, below + 18, area.Width, Math.Max(0, area.Bottom - below - 22));
        if (point is { } p && EventTrace is { Count: > 1 } trace && chart.Height > 50)
        {
            DrawEventTrace(dc, chart, trace, p);
        }
        else if (chart.Height > 30)
        {
            DrawText(dc, Loc("Grid_Bal_Steady"), new Point(center.X, chart.Top + 12), Ink.Dim, 11, center: true);
        }
    }

    /// <summary>The top half of a circle, as a closed shape.</summary>
    private static StreamGeometry Half(Point center, double radius)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(center + new Vector(-radius, 0), true);
            context.ArcTo(center + new Vector(radius, 0), new Size(radius, radius), 0, false, SweepDirection.Clockwise);

            context.EndFigure(true);
        }
        return geometry;
    }

    private void DrawEventTrace(PaintContext dc, Rect area, IReadOnlyList<FrequencyPoint> trace, FrequencyPoint now)
    {
        dc.DrawRoundedRectangle(FadedBrush(Color.FromRgb(0x08, 0x0C, 0x14), 0.85), Ink.Frame, area, 8, 8);
        var plot = new Rect(area.Left + 34, area.Top + 8, Math.Max(20, area.Width - 44), Math.Max(20, area.Height - 26));
        // The time axis grows with the event: its first half minute fills the box, later ones squeeze up.
        var lowest = Math.Min(49.6, Math.Floor(FrequencyModel.Lowest(trace).Frequency * 5) / 5);
        var span = Math.Max(30, now.Time);
        Point Map(double t, double hz) => new(plot.Left + (t / span * plot.Width), plot.Top + ((50.1 - hz) / (50.1 - lowest) * plot.Height));
        foreach (var hz in new[] { 50.0, lowest })
        {
            dc.DrawLine(Ink.Grid, Map(0, hz), Map(span, hz));
            DrawTextRight(dc, Number(hz, "0.0"), Map(0, hz) - new Vector(4, 0), Ink.Dim, 9.5);
        }

        var played = trace.Where(q => q.Time <= now.Time).Where((_, i) => i % 4 == 0).Select(q => Map(q.Time, q.Frequency)).ToList();
        played.Add(Map(now.Time, now.Frequency));
        if (played.Count > 1) dc.DrawGeometry(null, FadedPen(Ink.Cyan, 0.95, 1.8), Line(played));
        dc.DrawEllipse(FadedBrush(Ink.Cyan, 1), null, played[^1], 3.5, 3.5);
        DrawText(dc, LocFormat("Grid_Bal_After", Number(now.Time, "0")), new Point(plot.Left, area.Bottom - 9), Ink.Dim, 10);
        if (Glows("trace")) dc.DrawRoundedRectangle(null, ExploreTargets.GlowPen(_clock.Elapsed.TotalSeconds, 5), area, 8, 8);
        AddBoxTarget("trace", area);
    }

    /// <summary>An hour as a 24-hour clock, e.g. 07:30.</summary>
    internal static string ClockText(double hour)
    {
        var minutes = (int)Math.Round(GridDayModel.Wrap(hour) * 60) % (24 * 60);
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }
}
