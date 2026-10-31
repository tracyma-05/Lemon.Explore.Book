using Avalonia;
using Avalonia.Media;
using Lemon.Explore.Book.Controls.Space;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;

namespace Lemon.Explore.Book.Chapters.StatesOfMatter;

/// <summary>
/// A substance's phase diagram in a rectangle: temperature across in °C, pressure up on a scale of powers of ten from
/// 1 Pa to 100 MPa. Shared by the diagram's big plot and the side panel's small one: the coloured regions, the three
/// boundary lines, the triple and critical points and the one-atmosphere line; the owners add labels and the dot.
/// </summary>
internal sealed class PhasePlot(MatterModel.Substance substance, Rect area)
{
    public MatterModel.Substance Substance { get; } = substance;

    public Rect Area { get; } = area;

    private static readonly double LogMin = Math.Log10(MatterModel.MinPressure);
    private static readonly double LogMax = Math.Log10(MatterModel.MaxPressure);

    public double X(double kelvin) => Area.Left + ((kelvin - Substance.MinT) / (Substance.MaxT - Substance.MinT) * Area.Width);

    public double Y(double pascals) => Area.Bottom - ((Math.Log10(Math.Clamp(pascals, 1e-3, 1e12)) - LogMin) / (LogMax - LogMin) * Area.Height);

    public Point At(double kelvin, double pascals) => new(X(kelvin), Y(pascals));

    /// <summary>The temperature under <paramref name="x"/>, K, kept inside the plot.</summary>
    public double Kelvin(double x) =>
        Substance.MinT + (Math.Clamp((x - Area.Left) / Area.Width, 0, 1) * (Substance.MaxT - Substance.MinT));

    /// <summary>The pressure at <paramref name="y"/> as a power of ten of pascals, kept inside the plot.</summary>
    public double PressureLog(double y) => LogMin + (Math.Clamp((Area.Bottom - y) / Area.Height, 0, 1) * (LogMax - LogMin));

    #region the boundaries

    /// <summary>The sublimation curve from the bottom (or left) of the plot up to the triple point.</summary>
    public List<Point> SublimationLine()
    {
        var s = Substance;
        var start = Math.Max(s.MinT, MatterModel.SublimationTemperature(s, MatterModel.MinPressure));
        var points = new List<Point>();
        for (var i = 0; i <= 40; i++)
        {
            var t = start + ((s.TripleT - start) * i / 40);
            points.Add(At(t, MatterModel.SublimationPressure(s, t)));
        }

        return points;
    }

    /// <summary>The melting line from the triple point to the top of the plot.</summary>
    public List<Point> MeltingLine()
    {
        var s = Substance;
        var points = new List<Point>();
        for (var i = 0; i <= 30; i++)
        {
            var p = Math.Pow(10, Math.Log10(s.TripleP) + ((LogMax - Math.Log10(s.TripleP)) * i / 30));
            points.Add(At(MatterModel.MeltingTemperature(s, p), p));
        }

        return points;
    }

    /// <summary>The boiling curve from the triple point to the critical point.</summary>
    public List<Point> BoilingLine()
    {
        var s = Substance;
        var points = new List<Point>();
        for (var i = 0; i <= 60; i++)
        {
            var t = s.TripleT + ((s.CriticalT - s.TripleT) * i / 60);
            points.Add(At(t, MatterModel.BoilingPressure(s, t)));
        }

        return points;
    }

    /// <summary>The four regions as closed outlines (the plot is clipped to its rectangle when they are filled).</summary>
    public (Geometry Solid, Geometry Liquid, Geometry Gas, Geometry Supercritical) Regions()
    {
        var s = Substance;
        var sub = SublimationLine();
        var melt = MeltingLine();
        var boil = BoilingLine();
        var bottomLeft = new Point(Area.Left - 2, Area.Bottom + 2);
        var topLeft = new Point(Area.Left - 2, Area.Top - 2);
        var topRight = new Point(Area.Right + 2, Area.Top - 2);
        var bottomRight = new Point(Area.Right + 2, Area.Bottom + 2);
        var startsOnFloor = sub[0].X > Area.Left + 0.5;
        var critical = boil[^1];
        var meltTop = melt[^1];

        var solid = new List<Point>();
        if (startsOnFloor) solid.Add(bottomLeft);
        solid.AddRange(sub);
        solid.AddRange(melt.Skip(1));
        solid.Add(new Point(meltTop.X, topLeft.Y));
        solid.Add(topLeft);
        if (!startsOnFloor) solid.Add(new Point(topLeft.X, sub[0].Y));

        var liquid = new List<Point>(boil);
        liquid.Add(new Point(critical.X, topLeft.Y));
        liquid.Add(new Point(meltTop.X, topLeft.Y));
        liquid.AddRange(Enumerable.Reverse(melt));

        var gas = new List<Point>();
        gas.Add(startsOnFloor ? sub[0] : bottomLeft);
        if (!startsOnFloor) gas.Add(new Point(bottomLeft.X, sub[0].Y));
        gas.AddRange(startsOnFloor ? sub.Skip(1) : sub);
        gas.AddRange(boil.Skip(1));
        gas.Add(new Point(bottomRight.X, critical.Y));
        gas.Add(bottomRight);

        var super = new List<Point> { critical, new(bottomRight.X, critical.Y), topRight, new(critical.X, topRight.Y) };
        return (Polygon([.. solid]), Polygon([.. liquid]), Polygon([.. gas]), Polygon([.. super]));
    }

    #endregion

    #region drawing

    /// <summary>The regions in their colours, the boundary lines, the one-atmosphere line and the two special points.</summary>
    public void Draw(PaintContext dc, double lineWidth = 2)
    {
        var (solid, liquid, gas, super) = Regions();
        dc.PushClip(Area);
        dc.DrawGeometry(SolidFill, null, solid);
        dc.DrawGeometry(LiquidFill, null, liquid);
        dc.DrawGeometry(GasFill, null, gas);
        dc.DrawGeometry(SuperFill, null, super);

        var atm = Y(MatterModel.Atmosphere);
        dc.DrawLine(AtmPen, new Point(Area.Left, atm), new Point(Area.Right, atm));

        var critical = At(Substance.CriticalT, Substance.CriticalP);
        dc.DrawLine(SuperEdge, critical, new Point(critical.X, Area.Top));
        dc.DrawLine(SuperEdge, critical, new Point(Area.Right, critical.Y));

        var pen = LinePen(lineWidth);
        dc.DrawGeometry(null, pen, Polyline(SublimationLine()));
        dc.DrawGeometry(null, pen, Polyline(MeltingLine()));
        dc.DrawGeometry(null, pen, Polyline(BoilingLine()));
        dc.Pop();

        var triple = At(Substance.TripleT, Substance.TripleP);
        var r = lineWidth + 2;
        dc.DrawEllipse(PointFill, PointRim, triple, r, r);
        dc.DrawEllipse(PointFill, PointRim, critical, r, r);
        dc.DrawRectangle(null, Frame, Area);
    }

    /// <summary>The temperatures marked along the bottom, °C: every 20 or 100 degrees, as the range needs.</summary>
    public IEnumerable<double> CelsiusTicks()
    {
        var step = Substance.MaxCelsius - Substance.MinCelsius > 300 ? 100 : 40;
        for (var c = Math.Ceiling(Substance.MinCelsius / step) * step; c <= Substance.MaxCelsius; c += step) yield return c == 0 ? 0 : c;
    }

    /// <summary>The pressures marked up the side: 1 Pa, 100 Pa, 10 kPa, 1 MPa, 100 MPa.</summary>
    public static IEnumerable<double> PressureTicks() => [1, 100, 1e4, 1e6, 1e8];

    /// <summary>A short label for a tick on the pressure axis.</summary>
    public static string PressureTickText(double pascals) => pascals switch
    {
        >= 1e6 => $"{pascals / 1e6:0} MPa",
        >= 1e3 => $"{pascals / 1e3:0} kPa",
        _ => $"{pascals:0} Pa",
    };

    private static StreamGeometry Polyline(List<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], false);
            foreach (var point in points.Skip(1)) context.LineTo(point);
            context.EndFigure(false);
        }

        return geometry;
    }

    private static readonly Dictionary<int, IPen> LinePens = [];

    private static IPen LinePen(double width)
    {
        var key = (int)Math.Round(width * 10);
        if (LinePens.TryGetValue(key, out var pen)) return pen;
        pen = MakePen(Solid(235, 0xEE, 0xF1, 0xF6), width);
        LinePens[key] = pen;
        return pen;
    }

    public static readonly IBrush SolidFill = Solid(120, 0x3A, 0x9F, 0xC8);
    public static readonly IBrush LiquidFill = Solid(120, 0x2A, 0x5C, 0xC8);
    public static readonly IBrush GasFill = Solid(70, 0xC8, 0x9A, 0x3A);
    public static readonly IBrush SuperFill = Solid(90, 0x7A, 0x5A, 0xC8);
    private static readonly IPen AtmPen = MakePen(Solid(150, 0xFF, 0xFF, 0xFF), 1, dashed: true);
    private static readonly IPen SuperEdge = MakePen(Solid(110, 0xB0, 0x8C, 0xFF), 1, dashed: true);
    private static readonly IBrush PointFill = Solid(255, 0xFF, 0xD3, 0x6B);
    private static readonly IPen PointRim = MakePen(Solid(255, 0x10, 0x14, 0x22), 1.5);
    private static readonly IPen Frame = MakePen(Solid(60, 255, 255, 255), 1);

    #endregion
}
