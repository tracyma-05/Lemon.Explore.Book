using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Earthquakes;

/// <summary>
/// The boundaries view: four cross-sections. A ridge (new floor carrying the magnetic field's flips away on both sides,
/// sinking as it ages, the mantle welling up under it); a plate sinking under another (trench, quakes down the slab,
/// water driven off at 100 km melting the mantle into a line of volcanoes, the stuck edge dragged down and sprung back
/// with a tsunami); India crossing an ocean and running into Asia (the Himalaya and Tibet rising); and a sliding fault
/// from above (fences bending until the fault snaps, Reid's elastic rebound).
/// </summary>
public sealed partial class QuakeDiagram
{
    /// <summary>Years between the sinking plate's great quakes, as shown (real ones: a century or several).</summary>
    public const double MegathrustCycle = 220;

    /// <summary>Years between the fault's quakes, as shown.</summary>
    public const double FaultCycle = 150;

    /// <summary>How fast the sliding fault's two sides pass each other, cm a year (the San Andreas: about 3.5).</summary>
    public const double FaultRate = 3.5;

    /// <summary>How fast the ocean plate goes under, cm a year.</summary>
    public const double ConvergenceRate = 7;

    /// <summary>Boundaries: how far the fault (or the stuck plate edge) slipped in its last quake, m. Pushed out.</summary>
    public static readonly StyledProperty<double> LastSlipProperty =
        AvaloniaProperty.Register<QuakeDiagram, double>(nameof(LastSlip), 0.0, defaultBindingMode: BindingMode.TwoWay);

    // Redraws when these change. A field rather than a static constructor: the class is split over files.
    private static readonly bool s_redrawsOnBoundaries = RedrawOnBoundaries();

    private static bool RedrawOnBoundaries()
    {
        AffectsRender<QuakeDiagram>(LastSlipProperty);
        return true;
    }

    public double LastSlip
    {
        get => GetValue(LastSlipProperty);
        set => SetValue(LastSlipProperty, value);
    }

    /// <summary>When (on the diagram's clock) the last quake of the section went off, for its flash and its waves.</summary>
    private double _quakeWall = -100;

    /// <summary>The fault's or the plate edge's quakes so far, in section years, with their slip in metres.</summary>
    private readonly List<(double Year, double Slip)> _slips = [];

    private double _lastQuakeYear;
    private double _quakeY;
    private readonly Random _sectionRandom = new(11);
    private IReadOnlyList<(double S, double Into, double Magnitude)>? _slabQuakes;
    private double _slabQuakesDip = double.NaN;

    private void OnSectionChanged()
    {
        ResetCycle();
        _drag = DragKind.None;
    }

    private void ResetCycle()
    {
        _slips.Clear();
        _lastQuakeYear = 0;
        _quakeWall = -100;
    }

    private void StepSection(double dt, double now)
    {
        if (SectionTime + 1e-9 < _lastQuakeYear && Section is SectionKind.Subduction or SectionKind.Transform) ResetCycle();
        if (!IsPlaying) return;

        switch (Section)
        {
            case SectionKind.Ridge:
            case SectionKind.Collision:
            {
                // Counting down to today: 0.4 million years a second at the ridge, 2.4 for India's journey.
                var rate = Section == SectionKind.Ridge ? 0.4 : 2.4;
                var t = SectionTime - (rate * dt);
                if (t <= 0)
                {
                    t = 0;
                    SetCurrentValue(IsPlayingProperty, false);
                }

                SetCurrentValue(SectionTimeProperty, t);
                break;
            }

            case SectionKind.Subduction:
            {
                var years = SectionTime + (18 * dt);
                SetCurrentValue(SectionTimeProperty, years);
                if (years - _lastQuakeYear >= MegathrustCycle) Rupture(now, years);
                break;
            }

            default:
            {
                var years = SectionTime + (25 * dt);
                SetCurrentValue(SectionTimeProperty, years);
                if (!Creep && years - _lastQuakeYear >= FaultCycle) Rupture(now, years);
                break;
            }
        }
    }

    /// <summary>The section's quake now (from the toolbar), if enough has built up since the last one.</summary>
    private void QuakeNow()
    {
        if (Mode != QuakeMode.Boundaries || Section is not (SectionKind.Subduction or SectionKind.Transform)) return;
        if (Section == SectionKind.Transform && Creep) return;
        if (SectionTime - _lastQuakeYear < 5) return;
        Rupture(_clock.Elapsed.TotalSeconds, SectionTime);
    }

    private void Rupture(double now, double year)
    {
        var rate = Section == SectionKind.Transform ? FaultRate : ConvergenceRate;
        var slip = rate / 100 * (year - _lastQuakeYear);
        _slips.Add((year, slip));
        _lastQuakeYear = year;
        _quakeWall = now;
        _quakeY = (_sectionRandom.NextDouble() - 0.5) * 30;
        SetCurrentValue(LastSlipProperty, slip);
        SetCurrentValue(QuakeCountProperty, QuakeCount + 1);
    }

    /// <summary>How far through its cycle the stuck fault or plate edge is, 0 (just slipped) … 1 (due).</summary>
    private double CycleShare => Section == SectionKind.Transform
        ? Math.Clamp((SectionTime - _lastQuakeYear) / FaultCycle, 0, 1)
        : Math.Clamp((SectionTime - _lastQuakeYear) / MegathrustCycle, 0, 1);

    private Rect _section;

    private void DrawBoundariesMode(PaintContext dc, double now)
    {
        var stage = Stage();
        _section = new Rect(stage.X + 28, stage.Y + 6, Math.Max(100, stage.Width - 56), Math.Max(120, stage.Height - 12));

        // A quake shakes the picture for a moment.
        var shake = now - _quakeWall;
        var shaking = shake is >= 0 and < 1.0 && Section is SectionKind.Subduction or SectionKind.Transform;
        if (shaking)
        {
            var a = 3 * (1 - shake);
            dc.PushTransform(new TranslateTransform(Math.Sin(shake * 71) * a, Math.Cos(shake * 53) * a));
        }

        switch (Section)
        {
            case SectionKind.Subduction:
                DrawSubduction(dc, now);
                break;

            case SectionKind.Collision:
                DrawCollision(dc, now);
                break;

            case SectionKind.Transform:
                DrawTransform(dc, now);
                break;

            default:
                DrawRidge(dc, now);
                break;
        }

        if (shaking) dc.Pop();
    }

    #region the ridge

    /// <summary>The ridge section shows 400 km each side.</summary>
    private const double RidgeHalfWidth = 400;

    private double RidgeX(double km) => _section.X + ((km + RidgeHalfWidth) / (2 * RidgeHalfWidth) * _section.Width);

    /// <summary>Depth to screen: the top 10 km (sea and crust) get 40% of the height, the next 150 km the rest.</summary>
    private double RidgeY(double km) => km <= 10
        ? _section.Y + 40 + (km / 10 * (_section.Height - 40) * 0.4)
        : _section.Y + 40 + ((_section.Height - 40) * (0.4 + ((Math.Min(km, 160) - 10) / 150 * 0.6)));

    /// <summary>The sea's depth over the floor at a distance from the ridge, km: the floor sinks with age; a rift valley or a rise on top.</summary>
    private double RidgeFloor(double km)
    {
        var depth = BoundaryModel.SeaDepth(BoundaryModel.Age(km, HalfRate));
        return BoundaryModel.HasRiftValley(HalfRate)
            ? depth + (1.2 * Math.Exp(-Math.Pow(km / 22, 2))) - (0.5 * Math.Exp(-Math.Pow((Math.Abs(km) - 38) / 14, 2)))
            : depth - (0.25 * Math.Exp(-Math.Pow(km / 30, 2)));
    }

    private void DrawRidge(PaintContext dc, double now)
    {
        var t = SectionTime;
        var top = _section.Y + 40;
        var step = Math.Max(2, _section.Width / 260);

        // The sea, the striped crust, the cold plate and the hot mantle, column by column.
        var water = new StreamGeometry();
        var plate = new StreamGeometry();
        using (var w = water.Open())
        using (var p = plate.Open())
        {
            var floor = new List<Point>();
            var lithosphere = new List<Point>();
            for (var x = _section.Left; x <= _section.Right + 0.1; x += step)
            {
                var km = ((x - _section.X) / _section.Width * 2 * RidgeHalfWidth) - RidgeHalfWidth;
                floor.Add(new Point(x, RidgeY(RidgeFloor(km))));
                var age = BoundaryModel.Age(km, HalfRate);
                lithosphere.Add(new Point(x, RidgeY(Math.Max(RidgeFloor(km) + 7, BoundaryModel.LithosphereThickness(age)))));
            }

            w.BeginFigure(new Point(_section.Left, top), true);
            w.LineTo(new Point(_section.Right, top));
            foreach (var point in Enumerable.Reverse(floor)) w.LineTo(point);
            w.EndFigure(true);

            p.BeginFigure(floor[0], true);
            foreach (var point in floor.Skip(1)) p.LineTo(point);
            foreach (var point in Enumerable.Reverse(lithosphere)) p.LineTo(point);

            p.EndFigure(true);
        }
        dc.DrawRectangle(Ink.Mantle, null, new Rect(_section.Left, top, _section.Width, _section.Bottom - top));
        dc.DrawGeometry(Ink.Lithosphere, null, plate);

        // The partial melt under the ridge: a warm glow narrowing upwards.
        var axis = RidgeX(0);
        var meltTop = RidgeY(8);
        var melt = Polygon(new Point(axis - (_section.Width * 0.12), RidgeY(110)), new Point(axis + (_section.Width * 0.12), RidgeY(110)), new Point(axis + 6, meltTop), new Point(axis - 6, meltTop));
        dc.DrawGeometry(FadedBrush(Ink.Magma, 0.32), null, melt);

        // Magnetic stripes in the crust: each column's polarity when it formed.
        var normalBrush = FadedBrush(Color.FromRgb(0x24, 0x26, 0x30), 1);
        var reversedBrush = FadedBrush(Color.FromRgb(0xE8, 0xE4, 0xD8), 1);
        for (var x = _section.Left; x < _section.Right; x += step)
        {
            var km = ((x + (step / 2) - _section.X) / _section.Width * 2 * RidgeHalfWidth) - RidgeHalfWidth;
            var normal = BoundaryModel.IsNormal(t + BoundaryModel.Age(km, HalfRate));
            var y0 = RidgeY(RidgeFloor(km));
            var y1 = RidgeY(RidgeFloor(km) + 6);
            dc.DrawRectangle(normal ? normalBrush : reversedBrush, null, new Rect(x, y0, step + 0.6, Math.Max(1, y1 - y0)));
        }

        dc.DrawGeometry(Ink.Sea, null, water);
        dc.DrawLine(FadedPen(Ink.Cyan, 0.6, 1.2), new Point(_section.Left, top), new Point(_section.Right, top));

        // The magma chamber and the black smoker on the crest.
        var chamber = new Point(axis, RidgeY(RidgeFloor(0) + 3.2));
        dc.DrawEllipse(FadedBrush(Ink.Magma, 0.9), FadedPen(Ink.Gold, 0.6, 1), chamber, 14, 5);
        AddBoxTarget("magma", new Rect(chamber.X - 16, chamber.Y - 7, 32, 14));
        DrawSmoker(dc, new Point(axis + (_section.Width * 0.02), RidgeY(RidgeFloor(_section.Width * 0.02 / _section.Width * 2 * RidgeHalfWidth))), now);

        // The mantle welling up under the ridge and turning out under the plates.
        DrawUpwelling(dc, axis, now);

        // Which way the plates move, and how fast.
        var arrowY = top - 16;
        var arrowPen = FadedPen(Ink.Text, 0.9, 1.8);
        var reach = Math.Min(_section.Width * 0.18, 140);
        DrawArrow(dc, arrowPen, new Point(axis - 18, arrowY), new Point(axis - 18 - reach, arrowY), 8);
        DrawArrow(dc, arrowPen, new Point(axis + 18, arrowY), new Point(axis + 18 + reach, arrowY), 8);
        if (ShowLabels)
        {
            var label = LocFormat("Quake_Value_CmYear", Number(HalfRate, "0.0"));
            DrawText(dc, label, new Point(axis - 22 - (reach / 2), arrowY - 12), Ink.Text, 11, TextAnchor.Center);
            DrawText(dc, label, new Point(axis + 22 + (reach / 2), arrowY - 12), Ink.Text, 11, TextAnchor.Center);
        }

        // Age marks along the floor every 1, 2, 5 or 10 Ma: as often as leaves room for their labels on this page.
        var pixelsPerMa = RidgeX(HalfRate * 10) - RidgeX(0);
        var every = new[] { 1.0, 2, 5, 10 }.FirstOrDefault(step => step * pixelsPerMa >= 52, 10);
        for (var age = every; age * HalfRate * 10 < RidgeHalfWidth; age += every)
        {
            foreach (var side in new[] { -1, 1 })
            {
                var km = side * age * HalfRate * 10;
                var at = new Point(RidgeX(km), RidgeY(RidgeFloor(km) + 6) + 2);
                dc.DrawLine(Ink.Tick, at, at + new Vector(0, 4));
                if (ShowLabels) DrawText(dc, LocFormat("Quake_Value_Ma", Number(age, "0")), at + new Vector(0, 11), Ink.Dim, 9.5, TextAnchor.Center);
            }
        }

        // The field now, as a compass needle in the corner.
        DrawFieldCompass(dc, new Point(_section.Left + 34, top + 34), BoundaryModel.IsNormal(t));

        if (ShowLabels)
        {
            DrawPill(dc, Loc("Quake_Label_Sea"), new Point(_section.Left + (_section.Width * 0.82), (top + RidgeY(3)) / 2), Ink.Cyan, 10.5);
            AddBoxTarget("stripes", DrawPill(dc, Loc("Quake_Label_Stripes"), new Point(RidgeX(-RidgeHalfWidth * 0.55), RidgeY(9.4)), Ink.Text, 10.5));
            AddBoxTarget("oldfloor", DrawPill(dc, Loc("Quake_Label_OldFloor"), new Point(RidgeX(RidgeHalfWidth * 0.68), RidgeY(RidgeFloor(RidgeHalfWidth * 0.68)) - 12), Ink.Text, 10.5));
            AddBoxTarget("lithosphere", DrawPill(dc, Loc("Quake_Label_Lithosphere"), new Point(RidgeX(RidgeHalfWidth * 0.6), RidgeY(45)), Ink.Text, 10.5));
            AddBoxTarget("asthenosphere", DrawPill(dc, Loc("Quake_Label_Asthenosphere"), new Point(RidgeX(-RidgeHalfWidth * 0.6), RidgeY(135)), Ink.Orange, 10.5));
            AddBoxTarget("axis", DrawPill(dc, Loc(BoundaryModel.HasRiftValley(HalfRate) ? "Quake_Label_RiftValley" : "Quake_Label_Crest"), new Point(axis, top + 14), Ink.Red, 10.5));
            DrawText(dc, Loc("Quake_Note_RidgeScale"), new Point(_section.Right - 6, _section.Bottom - 10), Ink.Dim, 10, TextAnchor.Right);
        }
        else
        {
            AddBoxTarget("axis", new Rect(axis - 12, top, 24, RidgeY(4) - top));
        }

        // Small quakes along the crest.
        var flash = (now * 0.7) % 1;
        if (flash < 0.15)
        {
            var at = new Point(axis + ((Math.Sin(Math.Floor(now * 0.7) * 12.9898) * 0.5) * 30), RidgeY(RidgeFloor(0) + 5));
            var r = 4 + (flash * 60);
            dc.DrawEllipse(null, FadedPen(Ink.Shallow, 1 - (flash / 0.15), 1.5), at, r, r);
        }
    }

    private void DrawSmoker(PaintContext dc, Point at, double now)
    {
        dc.DrawRectangle(FadedBrush(Color.FromRgb(0x50, 0x40, 0x38), 1), null, new Rect(at.X - 2, at.Y - 9, 4, 9));
        for (var i = 0; i < 6; i++)
        {
            var s = ((now * 0.5) + (i / 6.0)) % 1;
            var p = new Point(at.X + (Math.Sin((now * 2) + i) * 3 * s), at.Y - 10 - (s * 26));
            dc.DrawEllipse(FadedBrush(Color.FromRgb(0x10, 0x10, 0x14), 0.75 * (1 - s)), null, p, 2.5 + (s * 4), 2.5 + (s * 4));
        }

        _targets.Add("smoker", at + new Vector(0, -10), 9);
    }

    private void DrawUpwelling(PaintContext dc, double axis, double now)
    {
        var bottom = RidgeY(158);
        var turn = RidgeY(95);
        var pen = FadedPen(Ink.Orange, 0.55, 1.3);
        foreach (var side in new[] { -1, 1 })
        {
            for (var lane = 0; lane < 3; lane++)
            {
                var start = new Point(axis + (side * (8 + (lane * 14))), bottom);
                var control = new Point(axis + (side * (10 + (lane * 16))), turn - (lane * 8) + 20);
                var end = new Point(axis + (side * _section.Width * (0.32 + (lane * 0.06))), RidgeY(75 + (lane * 20)));
                for (var k = 0; k < 4; k++)
                {
                    var s = ((now * 0.12) + (k / 4.0) + (lane * 0.13)) % 1;
                    var p0 = Bezier(start, control, end, s);
                    var p1 = Bezier(start, control, end, Math.Min(1, s + 0.05));
                    DrawArrow(dc, pen, p0, p1, 5);
                }
            }
        }
    }

    private static Point Bezier(Point a, Point c, Point b, double s) => new(
        ((1 - s) * (1 - s) * a.X) + (2 * (1 - s) * s * c.X) + (s * s * b.X),
        ((1 - s) * (1 - s) * a.Y) + (2 * (1 - s) * s * c.Y) + (s * s * b.Y));

    /// <summary>A compass needle: north end up while the field is the way it is now, down while it is reversed.</summary>
    private void DrawFieldCompass(PaintContext dc, Point at, bool normal)
    {
        dc.DrawEllipse(Ink.Panel, Ink.Frame, at, 20, 20);
        var tip = normal ? -1 : 1;
        dc.DrawGeometry(FadedBrush(Ink.Red, 1), null, Polygon(at + new Vector(0, tip * 15), at + new Vector(4, 0), at + new Vector(-4, 0)));
        dc.DrawGeometry(FadedBrush(Ink.Text, 0.9), null, Polygon(at + new Vector(0, -tip * 15), at + new Vector(4, 0), at + new Vector(-4, 0)));
        if (ShowLabels) DrawText(dc, Loc(normal ? "Quake_Value_FieldNormal" : "Quake_Value_FieldReversed"), at + new Vector(26, 0), normal ? Ink.Text : Ink.Gold, 10.5);
        _targets.Add("field", at, 20);
    }

    #endregion

    #region a sinking plate

    /// <summary>The section runs 350 km on the ocean side of the trench to 850 km past it, 700 km down.</summary>
    private const double SlabLeft = -350;

    private const double SlabRight = 850;

    private const double SlabDepth = 700;

    /// <summary>Heights and depths at the surface are drawn this many times taller than the rest.</summary>
    private const double SurfaceExaggeration = 8;

    private double SlabX(double km) => _section.X + ((km - SlabLeft) / (SlabRight - SlabLeft) * _section.Width);

    private double SlabTopY => _section.Y + 46;

    private double SlabY(double km) => SlabTopY + (km / SlabDepth * (_section.Bottom - SlabTopY));

    /// <summary>The surface (sea floor or land) at a distance from the trench, km above sea level (negative: under the sea).</summary>
    private double SubductionSurface(double x)
    {
        if (x < -10) return -5.6 + (0.4 * Math.Exp(-Math.Pow((x + 60) / 40, 2)));
        if (x < 0) return -5.6 - (2.4 * (1 + (x / 10)));
        var trench = -8 + Math.Min(1, x / 60) * 5.5;
        if (x < 60) return trench;
        var arc = BoundaryModel.ArcDistance(Dip);
        if (OverOcean)
        {
            // An island arc: sea floor with a ridge of islands.
            return -3.0 + (3.8 * Math.Exp(-Math.Pow((x - arc) / 40, 2))) - (0.6 * Math.Exp(-Math.Pow((x - arc - 220) / 80, 2)));
        }

        // A continent: a coast, the mountains over the arc, a high plain behind.
        var land = Math.Min(1, (x - 60) / 50);
        return (-2.5 * (1 - land)) + (land * (0.3 + (4.2 * Math.Exp(-Math.Pow((x - arc) / 90, 2))) + (0.8 * Math.Exp(-Math.Pow((x - arc - 250) / 160, 2)))));
    }

    private double SurfaceY(double x)
    {
        var sea = SlabY(0);
        var unit = (SlabY(1) - SlabY(0)) * SurfaceExaggeration;
        return sea - (SubductionSurface(x) * unit);
    }

    private void DrawSubduction(PaintContext dc, double now)
    {
        var dip = Dip;
        var sea = SlabY(0);
        var cycle = CycleShare;
        var sinceQuake = now - _quakeWall;

        // The mantle, then the upper plate, then the sinking plate on top of both.
        dc.DrawRectangle(Ink.Mantle, null, new Rect(_section.Left, sea, _section.Width, _section.Bottom - sea));
        foreach (var depth in new[] { 410.0, 660 })
        {
            dc.DrawLine(FadedPen(Colors.White, 0.12, 1, dashed: true), new Point(_section.Left, SlabY(depth)), new Point(_section.Right, SlabY(depth)));
            if (ShowLabels) DrawText(dc, LocFormat("Quake_Value_Km", Number(depth, "0")), new Point(_section.Right - 4, SlabY(depth) - 8), Ink.Dim, 9.5, TextAnchor.Right);
        }

        // The upper plate: crust and its cold mantle root, the edge dragged down while stuck.
        var plateBottom = OverOcean ? 70.0 : 110;
        var crust = OverOcean ? 12.0 : 38;
        var drag = cycle * 9;
        if (sinceQuake is >= 0 and < 0.6) drag *= sinceQuake / 0.6;
        var upper = new List<Point>();
        var step = Math.Max(3, _section.Width / 240);
        for (var x = 0.0; x <= SlabRight; x += step / _section.Width * (SlabRight - SlabLeft))
        {
            var pull = drag * Math.Exp(-x / 70) * (x < 160 ? 1 : 0.6) - (drag * 0.25 * Math.Exp(-Math.Pow((x - 230) / 70, 2)));
            upper.Add(new Point(SlabX(x), SurfaceY(x) + pull));
        }

        var upperPlate = Line(upper
            .Concat(new[] { new Point(SlabX(SlabRight), SlabY(plateBottom)) })
            .Concat(Enumerable.Range(0, 40).Select(i => SlabTopPoint(BoundaryModel.SlabX(plateBottom, dip) * (1 - (i / 39.0)), dip, 0))), closed: true, filled: true);
        dc.DrawGeometry(Ink.Lithosphere, null, upperPlate);
        var crustShape = Line(upper
            .Concat(new[] { new Point(SlabX(SlabRight), SlabY(crust)) })
            .Concat(Enumerable.Range(0, 20).Select(i => SlabTopPoint(BoundaryModel.SlabX(crust, dip) * (1 - (i / 19.0)), dip, 0))), closed: true, filled: true);
        dc.DrawGeometry(OverOcean ? Ink.OceanCrust : Ink.ContinentCrust, null, crustShape);

        // The sinking plate: 80 km thick, its crust on top, carried down.
        var slab = SlabOutline(dip, 0, 80, 900);
        dc.DrawGeometry(Ink.Slab, FadedPen(Ink.Trench, 0.5, 1), slab);
        var slabCrust = SlabOutline(dip, 0, 7, 900);
        dc.DrawGeometry(Ink.OceanCrust, null, slabCrust);

        // Moving marks on the slab: the plate goes under.
        var flow = now * 22 % 60;
        for (var s = -SlabLeft - 20 + flow; s > -SlabLeft - 400; s -= 60)
        {
            // Before the trench the plate is flat: draw marks at x = −s.
            var x = -s;
            if (x > SlabLeft) dc.DrawLine(FadedPen(Colors.White, 0.35, 1.2), new Point(SlabX(x), SlabY(25)), new Point(SlabX(x) + 6, SlabY(45)));
        }

        for (var s = flow; s < 900; s += 60)
        {
            var a = SlabTopPoint(s, dip, 25);
            var b = SlabTopPoint(s + 8, dip, 50);
            dc.DrawLine(FadedPen(Colors.White, 0.35, 1.2), a, b);
        }

        // The sea, on top of everything below sea level.
        var seaShape = new StreamGeometry();
        using (var ctx = seaShape.Open())
        {
            ctx.BeginFigure(new Point(_section.Left, sea), true);
            for (var x = SlabLeft; x <= SlabRight; x += step / _section.Width * (SlabRight - SlabLeft))
            {
                var y = SurfaceY(x);
                if (x >= 0) y += drag * Math.Exp(-x / 70) * (x < 160 ? 1 : 0.6);
                ctx.LineTo(new Point(SlabX(x), Math.Max(sea, y)));
            }

            ctx.LineTo(new Point(_section.Right, sea));

            ctx.EndFigure(true);
        }
        dc.DrawGeometry(Ink.Sea, null, seaShape);
        DrawTsunami(dc, sea, sinceQuake);

        // The ocean floor before the trench.
        dc.DrawLine(FadedPen(Colors.White, 0.25, 1), new Point(_section.Left, SurfaceY(SlabLeft)), new Point(SlabX(0), SurfaceY(0)));

        // Water from the slab at 70–150 km rises; the mantle above melts at about 100 km and the melt rises to the arc.
        var arc = BoundaryModel.ArcDistance(dip);
        for (var i = 0; i < 18; i++)
        {
            var s = ((now * 0.18) + (i * 0.6180339887)) % 1;
            var depth = 70 + ((i * 0.7548776662 % 1) * 80);
            var from = SlabTopPoint(SlabDistance(depth, dip), dip, 0);
            var p = from + new Vector(s * 10, -s * 30);
            dc.DrawEllipse(FadedBrush(Ink.Cyan, 0.8 * (1 - s)), null, p, 1.8, 1.8);
        }

        var meltStart = SlabTopPoint(SlabDistance(100, dip), dip, 0) + new Vector(-6, -14);
        var vent = new Point(SlabX(arc), SurfaceY(arc));
        for (var i = 0; i < 10; i++)
        {
            var s = ((now * 0.09) + (i / 10.0)) % 1;
            var p = new Point(meltStart.X + ((vent.X - meltStart.X) * s) + (Math.Sin((now * 1.3) + i) * 4), meltStart.Y + ((vent.Y + 10 - meltStart.Y) * s));
            dc.DrawEllipse(FadedBrush(Ink.Magma, 0.85), null, p, 3.2 - (s * 1.2), 3.2 - (s * 1.2));
        }

        AddBoxTarget("melt", new Rect(meltStart.X - 12, meltStart.Y - 12, 24, 24));
        DrawVolcano(dc, vent, now);

        // The quakes in and on the slab: faint all the time, each flashing in turn.
        if (_slabQuakes is null || _slabQuakesDip != dip)
        {
            _slabQuakes = BoundaryModel.SlabQuakes(OverOcean ? 680 : 660, dip);
            _slabQuakesDip = dip;
        }

        for (var i = 0; i < _slabQuakes.Count; i++)
        {
            var (s, into, magnitude) = _slabQuakes[i];
            var at = SlabTopPoint(s, dip, into);
            var depth = SlabTop(s, dip).Depth;
            var r = 1.4 + ((magnitude - 4.5) * 0.9);
            dc.DrawEllipse(FadedBrush(DepthColour(depth), 0.55), null, at, r, r);
            var since = ((now / 9) - (i * 0.6180339887 % 1) + 1) % 1 * 9;
            if (since < 0.9) dc.DrawEllipse(null, FadedPen(DepthColour(depth), 1 - (since / 0.9), 1.3), at, r + (since * 18), r + (since * 18));
        }

        // The stuck part of the plate boundary, red when much has built up, and its rupture.
        var locked = Enumerable.Range(0, 16).Select(i => SlabTopPoint(SlabDistance(i / 15.0 * 40, dip), dip, 0)).ToList();
        var lockedPen = FadedPen(Lerp(Ink.Gold, Ink.Red, cycle), 0.6 + (0.4 * cycle), 3 + (cycle * 2));
        dc.DrawGeometry(null, lockedPen, Line(locked));
        if (sinceQuake is >= 0 and < 1.5) dc.DrawGeometry(null, FadedPen(Colors.White, 1 - (sinceQuake / 1.5), 8), Line(locked));
        AddBoxTarget("locked", BoundsOf(locked, 6));

        if (ShowLabels)
        {
            AddBoxTarget("trench", DrawPill(dc, Loc("Quake_Label_Trench"), new Point(SlabX(0), SurfaceY(0) + 18), Ink.Trench, 10.5));
            AddBoxTarget("oceanplate", DrawPill(dc, Loc("Quake_Label_OceanPlate"), new Point(SlabX(SlabLeft * 0.6), SlabY(40)), Ink.Text, 10.5));
            AddBoxTarget("upperplate", DrawPill(dc, Loc(OverOcean ? "Quake_Label_UpperOcean" : "Quake_Label_UpperContinent"), new Point(SlabX(SlabRight * 0.8), SlabY(plateBottom * 0.55)), Ink.Text, 10.5));
            AddBoxTarget("slab", DrawPill(dc, Loc("Quake_Label_Slab"), SlabTopPoint(SlabDistance(330, dip), dip, 40) + new Vector(-60, 0), Ink.Trench, 10.5));
            AddBoxTarget("benioff", DrawPill(dc, Loc("Quake_Label_Benioff"), SlabTopPoint(SlabDistance(480, dip), dip, 0) + new Vector(70, -10), Ink.Intermediate, 10.5));
            AddBoxTarget("arcvolcano", DrawPill(dc, Loc(OverOcean ? "Quake_Label_IslandArc" : "Quake_Label_VolcanicArc"), vent + new Vector(0, -34), Ink.Magma, 10.5));
            AddBoxTarget("wedge", DrawPill(dc, Loc("Quake_Label_Wedge"), new Point(SlabX(arc + 140), SlabY(170)), Ink.Orange, 10.5));
            DrawText(dc, Loc("Quake_Label_HundredKm"), new Point(meltStart.X + 14, meltStart.Y + 6), Ink.Cyan, 10);
            DrawText(dc, LocFormat("Quake_Note_SlabScale", Number(SurfaceExaggeration, "0")), new Point(_section.Right - 6, _section.Bottom - 10), Ink.Dim, 10, TextAnchor.Right);

            var lockedLabel = DrawPill(dc, LocFormat("Quake_Label_Locked", Number(cycle * 100, "0")), locked[^1] + new Vector(54, 18), Lerp(Ink.Gold, Ink.Red, cycle), 10);
            AddBoxTarget("locked", lockedLabel);
        }
        else
        {
            _targets.Add("trench", new Point(SlabX(0), SurfaceY(0)), 10);
            _targets.Add("arcvolcano", vent, 12);
        }

        // Which way the ocean plate moves.
        var arrowY = SurfaceY(SlabLeft) - 18;
        DrawArrow(dc, FadedPen(Ink.Text, 0.9, 1.8), new Point(SlabX(SlabLeft + 40), arrowY), new Point(SlabX(SlabLeft + 200), arrowY), 8);
        if (ShowLabels) DrawText(dc, LocFormat("Quake_Value_CmYear", Number(ConvergenceRate, "0")), new Point(SlabX(SlabLeft + 120), arrowY - 12), Ink.Text, 10.5, TextAnchor.Center);
    }

    /// <summary>The tsunami: a hump of sea racing away both ways from above the rupture.</summary>
    private void DrawTsunami(PaintContext dc, double sea, double since)
    {
        if (since is < 0 or > 6) return;
        var fade = 1 - (since / 6);
        var origin = 40.0;
        var reach = since * 160;
        foreach (var direction in new[] { -1, 1 })
        {
            var x = origin + (direction * reach);
            if (x < SlabLeft || x > SlabRight) continue;
            var at = new Point(SlabX(x), sea);
            var wave = new StreamGeometry();
            using (var ctx = wave.Open())
            {
                ctx.BeginFigure(at + new Vector(-26, 0), true);
                for (var k = -26; k <= 26; k += 2)
                {
                    ctx.LineTo(at + new Vector(k, -10 * fade * Math.Exp(-Math.Pow(k / 11.0, 2))));
                }

                ctx.EndFigure(true);
            }
            dc.DrawGeometry(FadedBrush(Ink.Cyan, 0.85), null, wave);
            _targets.Add("tsunami", at + new Vector(0, -6), 14);
        }
    }

    private void DrawVolcano(PaintContext dc, Point vent, double now)
    {
        dc.DrawGeometry(FadedBrush(Color.FromRgb(0x5A, 0x4A, 0x40), 1), null, Polygon(vent + new Vector(-16, 12), vent + new Vector(-3, 0), vent + new Vector(3, 0), vent + new Vector(16, 12)));
        for (var i = 0; i < 6; i++)
        {
            var s = ((now * 0.35) + (i / 6.0)) % 1;
            var p = vent + new Vector(Math.Sin((now * 1.7) + i) * 5 * s, -6 - (s * 30));
            dc.DrawEllipse(FadedBrush(Color.FromRgb(0x9A, 0x96, 0x92), 0.7 * (1 - s)), null, p, 3 + (s * 6), 3 + (s * 6));
        }

        dc.DrawEllipse(FadedBrush(Ink.Magma, 0.9), null, vent + new Vector(0, -1), 3, 2);
        _targets.Add("arcvolcano", vent, 12);
    }

    private static (double X, double Depth) SlabTop(double s, double dip) => BoundaryModel.SlabTop(s, dip);

    /// <summary>The distance along the slab where its top reaches a depth, km.</summary>
    private static double SlabDistance(double depth, double dip)
    {
        double lo = 0;
        double hi = 3000;
        for (var i = 0; i < 40; i++)
        {
            var mid = (lo + hi) / 2;
            if (SlabTop(mid, dip).Depth < depth) lo = mid;
            else hi = mid;
        }

        return lo;
    }

    /// <summary>A point <paramref name="into"/> km into the slab (at right angles to its top), <paramref name="s"/> km along it.</summary>
    private Point SlabTopPoint(double s, double dip, double into)
    {
        var (x, depth) = SlabTop(s, dip);
        var (x2, depth2) = SlabTop(s + 1, dip);
        var dx = x2 - x;
        var dz = depth2 - depth;
        var length = Math.Sqrt((dx * dx) + (dz * dz));
        var nx = -dz / length;
        var nz = dx / length;
        return new Point(SlabX(x + (nx * into)), SlabY(depth + (nz * into)));
    }

    /// <summary>The slab between <paramref name="from"/> and <paramref name="to"/> km into it, from the left edge to <paramref name="length"/> km down it.</summary>
    private StreamGeometry SlabOutline(double dip, double from, double to, double length)
    {
        var top = new List<Point> { new(SlabX(SlabLeft), SlabY(from)) };
        var bottom = new List<Point> { new(SlabX(SlabLeft), SlabY(to)) };
        for (var s = 0.0; s <= length; s += 10)
        {
            if (SlabTop(s, dip).Depth > SlabDepth + 50) break;
            top.Add(SlabTopPoint(s, dip, from));
            bottom.Add(SlabTopPoint(s, dip, to));
        }

        bottom.Reverse();
        return Line(top.Concat(bottom), closed: true, filled: true);
    }

    private static Rect BoundsOf(IReadOnlyList<Point> points, double pad)
    {
        var box = new Rect(new Point(points.Min(q => q.X), points.Min(q => q.Y)), new Point(points.Max(q => q.X), points.Max(q => q.Y)));
        box = box.Inflate(new Thickness(pad, pad));
        return box;
    }

    #endregion

    #region India and Asia

    /// <summary>The collision section runs from 1400 km south of Asia's old edge to 1000 km north of it.</summary>
    private const double CollisionLeft = -1400;

    private const double CollisionRight = 1000;

    /// <summary>Pixels per kilometre, the same across and down, so the sinking plate keeps its true slope.</summary>
    private double CollisionScale => _section.Width / (CollisionRight - CollisionLeft);

    /// <summary>Sea level on screen: below room for the mountains (drawn 15 times taller) and their labels.</summary>
    private double CollisionSea => _section.Y + 40 + (9 * 15 * CollisionScale);

    /// <summary>How deep the section goes, km.</summary>
    private double CollisionDepth => (_section.Bottom - CollisionSea) / CollisionScale;

    private double CollisionX(double km) => _section.X + ((km - CollisionLeft) * CollisionScale);

    private double CollisionY(double km) => CollisionSea + (km * CollisionScale);

    /// <summary>Height above sea level to screen, drawn 15 times taller than depth.</summary>
    private double HeightY(double km) => CollisionSea - (km * CollisionScale * 15);

    private void DrawCollision(PaintContext dc, double now)
    {
        var age = SectionTime;
        var gap = BoundaryModel.TethysWidth(age);
        var (himalaya, tibet) = BoundaryModel.Heights(age);
        var tibetCrust = BoundaryModel.TibetCrust(age);
        var sea = CollisionY(0);
        var since = Math.Max(0, BoundaryModel.ContactAge - age);

        // After contact India's crust slides under Tibet; most of the rest of the squeeze happens out of the picture.
        var under = Math.Min(520, BoundaryModel.IndiaTravel(BoundaryModel.ContactAge, Math.Min(age, BoundaryModel.ContactAge)) * 0.22);
        var indiaEdge = age > BoundaryModel.ContactAge ? -gap : 0;

        dc.DrawRectangle(Ink.Mantle, null, new Rect(_section.Left, sea, _section.Width, _section.Bottom - sea));

        // Asia: crust thickening and rising over the collision.
        var asia = new List<Point>();
        var root = new List<Point>();
        for (var x = 0.0; x <= CollisionRight; x += 10)
        {
            var plateau = tibet * Smoothstep(0, 200, x) * (1 - Smoothstep(750, 1000, x) * 0.5);
            var peak = himalaya * Math.Exp(-Math.Pow((x - 60) / 70, 2));
            var height = Math.Max(plateau, peak) + (age > BoundaryModel.ContactAge ? 0.3 : 0);
            asia.Add(new Point(CollisionX(x), HeightY(height)));
            var thickness = 35 + ((tibetCrust - 35) * Smoothstep(0, 220, x) * (1 - (Smoothstep(800, 1000, x) * 0.6)));
            root.Add(new Point(CollisionX(x), CollisionY(thickness)));
        }

        root.Reverse();
        dc.DrawRectangle(Ink.Lithosphere, null, new Rect(CollisionX(0), sea, CollisionX(CollisionRight) - CollisionX(0), CollisionY(150) - sea));
        dc.DrawGeometry(Ink.ContinentCrust, null, Line(asia.Concat(root), closed: true, filled: true));

        // India: a continent riding its plate, with the ocean floor ahead of it (the Tethys) sinking under Asia.
        if (age > BoundaryModel.ContactAge)
        {
            var dip = 45.0;
            // The ocean plate, 80 km thick, flat to Asia's edge and then down at 45°, to scale.
            var from = Math.Max(CollisionLeft, indiaEdge);
            var top = new List<Point> { new(CollisionX(from), CollisionY(6)) };
            var bottom = new List<Point> { new(CollisionX(from), CollisionY(86)) };
            for (var s = 0.0; s <= 1200; s += 10)
            {
                var (x, depth) = BoundaryModel.SlabTop(s, dip);
                if (depth > CollisionDepth + 100) break;
                var (x2, depth2) = BoundaryModel.SlabTop(s + 1, dip);
                var length = Math.Sqrt(Math.Pow(x2 - x, 2) + Math.Pow(depth2 - depth, 2));
                var (nx, nz) = (-(depth2 - depth) / length, (x2 - x) / length);
                top.Add(new Point(CollisionX(x), CollisionY(depth + 6)));
                bottom.Add(new Point(CollisionX(x + (nx * 80)), CollisionY(depth + 6 + (nz * 80))));
            }

            bottom.Reverse();
            dc.DrawGeometry(Ink.Slab, null, Line(top.Concat(bottom), closed: true, filled: true));
            dc.DrawRectangle(Ink.Sea, null, new Rect(CollisionX(from), HeightY(0.02) - 12, CollisionX(0) - CollisionX(from), 12));
            if (ShowLabels) AddBoxTarget("tethys", DrawPill(dc, Loc("Quake_Label_Tethys"), new Point((CollisionX(from) + CollisionX(0)) / 2, HeightY(0) - 24), Ink.Cyan, 10.5));

            // Arc volcanoes on Asia's edge while the ocean sinks (the Gangdese arc).
            var vent = new Point(CollisionX(170), HeightY(Math.Max(0.6, tibet * Smoothstep(0, 200, 170))));
            DrawVolcano(dc, vent, now);
            _targets.Add("gangdese", vent, 12);
            if (ShowLabels) AddBoxTarget("gangdese", DrawPill(dc, Loc("Quake_Label_Gangdese"), vent + new Vector(0, -30), Ink.Magma, 10));
        }
        else
        {
            // The old slab, broken off about 45 million years ago, sinking away.
            var sinking = Math.Min(1, Math.Max(0, (BoundaryModel.ContactAge - 5 - age) / 30));
            if (age < BoundaryModel.ContactAge - 3)
            {
                var depth = 160 + (sinking * 300);
                var blob = Polygon(new Point(CollisionX(120), CollisionY(depth)), new Point(CollisionX(200), CollisionY(depth + 40)),
                    new Point(CollisionX(250), CollisionY(depth + 200)), new Point(CollisionX(170), CollisionY(depth + 170)));
                dc.DrawGeometry(FadedBrush(Ink.Trench, 0.5 * (1 - (sinking * 0.5))), null, blob);
                var box = new Rect(new Point(CollisionX(120), CollisionY(depth)), new Point(CollisionX(250), CollisionY(depth + 200)));
                AddBoxTarget("slabbreak", box);
                if (ShowLabels && box.Top < _section.Bottom - 20) DrawPill(dc, Loc("Quake_Label_SlabBreak"), new Point(box.Right + 50, Math.Min(box.Top + 30, _section.Bottom - 12)), Ink.Trench, 10);
            }
        }

        // India's crust, sliding under Tibet after contact.
        var indiaLeft = CollisionLeft;
        var indiaFront = indiaEdge;
        if (indiaFront > CollisionLeft - 50)
        {
            var india = new List<Point>
            {
                new(CollisionX(indiaLeft), HeightY(0.4)),
                new(CollisionX(indiaFront - 40), HeightY(0.4)),
                new(CollisionX(indiaFront), HeightY(0.1)),
            };
            if (age <= BoundaryModel.ContactAge)
            {
                // Under Asia: a wedge reaching north under the plateau.
                india.Add(new Point(CollisionX(under * 0.4), CollisionY(40)));
                india.Add(new Point(CollisionX(under), CollisionY(60)));
                india.Add(new Point(CollisionX(under - 40), CollisionY(78)));
                india.Add(new Point(CollisionX(0), CollisionY(70)));
            }
            else
            {
                india.Add(new Point(CollisionX(indiaFront), CollisionY(35)));
            }

            india.Add(new Point(CollisionX(indiaLeft), CollisionY(35)));
            dc.DrawRectangle(Ink.Lithosphere, null, new Rect(CollisionX(indiaLeft), CollisionY(35), Math.Max(0, CollisionX(Math.Min(indiaFront, 0)) - CollisionX(indiaLeft)), CollisionY(120) - CollisionY(35)));
            dc.DrawGeometry(Ink.IndiaCrust, FadedPen(Ink.Gold, 0.5, 1), Line(india, closed: true, filled: true));
            AddBoxTarget("india", new Rect(CollisionX(Math.Max(indiaLeft, indiaFront - 600)), HeightY(0.4) - 4, CollisionX(indiaFront) - CollisionX(Math.Max(indiaLeft, indiaFront - 600)), CollisionY(35) - HeightY(0.4) + 4));
        }

        // The sea level line over the ocean parts.
        dc.DrawLine(FadedPen(Ink.Cyan, 0.4, 1, dashed: true), new Point(_section.Left, sea), new Point(_section.Right, sea));

        // India's push.
        var arrowY = HeightY(0.4) - 24;
        var arrowFrom = CollisionX(Math.Max(CollisionLeft + 60, Math.Min(indiaFront, 0) - 520));
        var speed = BoundaryModel.IndiaSpeed(age);
        DrawArrow(dc, FadedPen(Ink.Gold, 0.95, 2), new Point(arrowFrom, arrowY), new Point(arrowFrom + Math.Max(30, speed * 9), arrowY), 9);
        if (ShowLabels) DrawText(dc, LocFormat("Quake_Value_CmYear", Number(speed, "0")), new Point(arrowFrom, arrowY - 14), Ink.Gold, 11);

        if (indiaFront <= CollisionLeft - 50 && ShowLabels)
        {
            AddBoxTarget("india", DrawPill(dc, LocFormat("Quake_Label_IndiaAway", Number(Math.Round(gap / 100) * 100, "#,0")), new Point(_section.Left + 120, HeightY(0.4) - 4), Ink.Gold, 11));
        }

        // Shallow quakes over the whole squeezed zone after contact.
        if (age < BoundaryModel.ContactAge)
        {
            for (var i = 0; i < 14; i++)
            {
                var since2 = ((now / 5) - (i * 0.6180339887 % 1) + 1) % 1 * 5;
                if (since2 > 0.8) continue;
                var x = -100 + ((i * 0.7548776662 % 1) * 900);
                var at = new Point(CollisionX(x), CollisionY(8 + ((i * 0.38) % 1 * 30)));
                dc.DrawEllipse(null, FadedPen(Ink.Shallow, 1 - (since2 / 0.8), 1.4), at, 3 + (since2 * 16), 3 + (since2 * 16));
            }
        }

        if (ShowLabels)
        {
            AddBoxTarget("asia", DrawPill(dc, Loc("Quake_Label_Asia"), new Point(CollisionX(880), CollisionY(18)), Ink.Text, 10.5));
            if (age < BoundaryModel.ContactAge)
            {
                AddBoxTarget("himalaya", DrawPill(dc, LocFormat("Quake_Label_Himalaya", Number(himalaya, "0.0")), new Point(CollisionX(60), HeightY(himalaya) - 16), Ink.Text, 10.5));
                AddBoxTarget("tibet", DrawPill(dc, LocFormat("Quake_Label_Tibet", Number(tibet, "0.0")), new Point(CollisionX(520), HeightY(tibet) - 16), Ink.Text, 10.5));
                AddBoxTarget("suture", DrawPill(dc, Loc("Quake_Label_Suture"), new Point(CollisionX(0), CollisionY(24)), Ink.Collision, 10));
                DrawText(dc, LocFormat("Quake_Label_Crust", Number(tibetCrust, "0")), new Point(CollisionX(520), CollisionY(tibetCrust) + 12), Ink.Dim, 10, TextAnchor.Center);
            }

            DrawText(dc, age < 0.05 ? Loc("Quake_Value_Today") : LocFormat("Quake_Label_Age", Number(age, "0")), new Point(_section.Right - 8, _section.Y + 14), Ink.Gold, 15, TextAnchor.Right, bold: true);
            DrawText(dc, Loc("Quake_Note_CollisionScale"), new Point(_section.Right - 6, _section.Bottom - 10), Ink.Dim, 10, TextAnchor.Right);
        }
        else if (age < BoundaryModel.ContactAge)
        {
            _targets.Add("himalaya", new Point(CollisionX(60), HeightY(himalaya)), 14);
        }
    }

    private static double Smoothstep(double from, double to, double x)
    {
        var t = Math.Clamp((x - from) / (to - from), 0, 1);
        return t * t * (3 - (2 * t));
    }

    #endregion

    #region the sliding fault

    /// <summary>The fault map shows 60 km either side of the fault and 35 km along it.</summary>
    private const double FaultHalfWidth = 60;

    private const double FaultHalfLength = 35;

    /// <summary>Sideways ground movement is drawn this many pixels per metre.</summary>
    private const double PixelsPerMetre = 6;

    private Rect _faultBox;

    private Point FaultPoint(double xKm, double yKm) => new(
        _faultBox.X + ((xKm + FaultHalfWidth) / (2 * FaultHalfWidth) * _faultBox.Width),
        _faultBox.Y + ((yKm + FaultHalfLength) / (2 * FaultHalfLength) * _faultBox.Height));

    /// <summary>
    /// How far the ground at <paramref name="x"/> km from the fault has moved along it since the fences were built, m
    /// (the left side going up the screen): each quake's jump plus the bending since the last one, or a steady slide when
    /// the fault creeps.
    /// </summary>
    private double GroundShift(double x)
    {
        var years = SectionTime;
        if (Creep) return -FaultRate / 100 * years / 2 * Math.Sign(x);
        var jumps = _slips.Sum(s => s.Slip) / 2 * -Math.Sign(x);
        return jumps - BoundaryModel.Bending(x, FaultRate, years - _lastQuakeYear);
    }

    private void DrawTransform(PaintContext dc, double now)
    {
        var size = Math.Min(_section.Width - 120, _section.Height * 2 * FaultHalfWidth / (2 * FaultHalfLength) * 0.9);
        var height = size * FaultHalfLength / FaultHalfWidth;
        _faultBox = new Rect(_section.X + ((_section.Width - 90 - size) / 2), _section.Y + ((_section.Height - height) / 2), size, height);
        var sinceQuake = now - _quakeWall;

        // The two sides, with fields and trees.
        var left = new Rect(_faultBox.X, _faultBox.Y, _faultBox.Width / 2, _faultBox.Height);
        var right = new Rect(_faultBox.X + (_faultBox.Width / 2), _faultBox.Y, _faultBox.Width / 2, _faultBox.Height);
        dc.DrawRectangle(Ink.FieldA, null, left);
        dc.DrawRectangle(Ink.FieldB, null, right);
        var random = new Random(5);
        for (var i = 0; i < 70; i++)
        {
            var x = (random.NextDouble() * 2 * FaultHalfWidth) - FaultHalfWidth;
            var y = (random.NextDouble() * 2 * FaultHalfLength) - FaultHalfLength;
            if (Math.Abs(x) < 2.5) continue;
            var shift = GroundShift(x) * PixelsPerMetre;
            var at = FaultPoint(x, y) + new Vector(0, -shift);
            if (!_faultBox.Contains(at)) continue;
            dc.DrawEllipse(FadedBrush(Color.FromRgb(0x2E, 0x52, 0x2A), 0.8), null, at, 3, 3);
        }

        dc.PushClip(new RectangleGeometry(_faultBox));

        // Fences and a road across the fault, bent by the ground's movement.
        foreach (var (y, road) in new[] { (-22.0, false), (0.0, true), (22.0, false) })
        {
            var points = new List<Point>();
            for (var x = -FaultHalfWidth; x <= FaultHalfWidth + 0.01; x += 0.5)
            {
                var shift = GroundShift(x) * PixelsPerMetre;
                points.Add(FaultPoint(x, y) + new Vector(0, -shift));
            }

            var pen = road ? FadedPen(Color.FromRgb(0x50, 0x52, 0x58), 1, 7) : FadedPen(Color.FromRgb(0xF2, 0xE6, 0xC8), 1, 2);
            dc.DrawGeometry(null, pen, Line(points));
            if (road) dc.DrawGeometry(null, FadedPen(Ink.Gold, 0.8, 1, dashed: true), Line(points));
            var box = BoundsOf(points, 4);
            AddBoxTarget(road ? "road" : "fence", new Rect(FaultPoint(-FaultHalfWidth * 0.6, y).X, box.Y, FaultPoint(-FaultHalfWidth * 0.2, y).X - FaultPoint(-FaultHalfWidth * 0.6, y).X, box.Height));
        }

        // The fault, glowing as it breaks.
        var faultTop = FaultPoint(0, -FaultHalfLength);
        var faultBottom = FaultPoint(0, FaultHalfLength);
        dc.DrawLine(FadedPen(Ink.Transform, 0.9, 2.2, dashed: Creep), faultTop, faultBottom);
        if (sinceQuake is >= 0 and < 1.2) dc.DrawLine(FadedPen(Colors.White, 1 - (sinceQuake / 1.2), 9), faultTop, faultBottom);
        AddBoxTarget("fault", new Rect(faultTop.X - 8, faultTop.Y + 30, 16, faultBottom.Y - faultTop.Y - 60));

        // The waves going out from where it broke.
        if (sinceQuake is >= 0 and < 3)
        {
            var epicentre = FaultPoint(0, _quakeY);
            var pxPerKm = _faultBox.Width / (2 * FaultHalfWidth);
            var p = sinceQuake * 6 * pxPerKm * 8;
            var s = sinceQuake * 3.5 * pxPerKm * 8;
            var fade = 1 - (sinceQuake / 3);
            dc.DrawEllipse(null, FadedPen(Ink.Gold, fade, 1.6), epicentre, p, p);
            dc.DrawEllipse(null, FadedPen(Ink.Cyan, fade, 2.2), epicentre, s, s);
            dc.DrawEllipse(FadedBrush(Colors.White, fade), null, epicentre, 5, 5);
            _targets.Add("epicentre", epicentre, 10);
        }

        dc.Pop();
        dc.DrawRectangle(null, Ink.Frame, _faultBox);

        // The far sides' steady motion.
        var arrowPen = FadedPen(Ink.Text, 0.9, 2);
        var leftArrow = new Point(_faultBox.X + 26, _faultBox.Y + (_faultBox.Height * 0.62));
        var rightArrow = new Point(_faultBox.Right - 26, _faultBox.Y + (_faultBox.Height * 0.38));
        DrawArrow(dc, arrowPen, leftArrow, leftArrow + new Vector(0, -52), 9);
        DrawArrow(dc, arrowPen, rightArrow, rightArrow + new Vector(0, 52), 9);
        _targets.Add("farfield", leftArrow + new Vector(0, -26), 16);

        // How much is stored: a gauge beside the map.
        var gauge = new Rect(_faultBox.Right + 30, _faultBox.Y + 20, 18, _faultBox.Height - 40);
        dc.DrawRoundedRectangle(Ink.Panel, Ink.Frame, gauge, 6, 6);
        var share = Creep ? 0.05 : CycleShare;
        var fill = new Rect(gauge.X + 3, gauge.Bottom - 3 - ((gauge.Height - 6) * share), gauge.Width - 6, (gauge.Height - 6) * share);
        dc.DrawRoundedRectangle(FadedBrush(Lerp(Ink.Gold, Ink.Red, share), 0.95), null, fill, 4, 4);
        AddBoxTarget("stress", gauge);

        if (ShowLabels)
        {
            DrawText(dc, Loc("Quake_Label_Stored"), new Point(gauge.X + (gauge.Width / 2), gauge.Y - 10), Ink.Text, 10.5, TextAnchor.Center);
            DrawText(dc, LocFormat("Quake_Label_Years", Number(SectionTime - _lastQuakeYear, "0")), new Point(gauge.X + (gauge.Width / 2), gauge.Bottom + 12), Ink.Dim, 10, TextAnchor.Center);
            AddBoxTarget("fault", DrawPill(dc, Loc(Creep ? "Quake_Label_Creeping" : "Quake_Label_Fault"), new Point(faultTop.X, faultTop.Y + 14), Ink.Transform, 10.5));
            DrawText(dc, Loc("Quake_Label_SideA"), new Point(_faultBox.X + 8, _faultBox.Y + 12), Ink.Text, 10.5);
            DrawText(dc, Loc("Quake_Label_SideB"), new Point(_faultBox.Right - 8, _faultBox.Y + 12), Ink.Text, 10.5, TextAnchor.Right);
            var exaggeration = PixelsPerMetre / (_faultBox.Width / (2 * FaultHalfWidth) / 1000);
            DrawText(dc, LocFormat("Quake_Note_FaultScale", Number(Math.Round(exaggeration / 100) * 100, "#,0")), new Point(_faultBox.Right, _faultBox.Bottom + 12), Ink.Dim, 10, TextAnchor.Right);
            DrawText(dc, LocFormat("Quake_Label_YearsTotal", Number(SectionTime, "0")), new Point(_faultBox.X, _faultBox.Bottom + 12), Ink.Dim, 10);
        }
    }

    #endregion
}
