"""Self-test of the porting tool on small WPF snippets: python -I -m unittest discover tools/porting"""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import wpf2avalonia as tool  # noqa: E402

ROOT = "Original.App"


class NamespaceTests(unittest.TestCase):
    def test_shared_and_mapped_namespaces(self):
        source = (f"using {ROOT}.Controls.SolarSystem;\nusing {ROOT}.Controls.Eclipse;\n"
                  f"using static {ROOT}.Controls.Eclipse.EclipseDrawing;\n\nnamespace {ROOT}.Controls.Eclipse;\n")
        out = tool.apply_namespaces(source, "Lemon.Explore.Book.Chapters.Eclipses", ROOT, ["Controls.Eclipse=Chapters.Eclipses"])
        self.assertIn("using Lemon.Explore.Book.Controls.Space;", out)
        self.assertIn("using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;", out)
        self.assertIn("namespace Lemon.Explore.Book.Chapters.Eclipses;", out)
        self.assertNotIn(ROOT, out)


class DrawingTests(unittest.TestCase):
    def test_frozen_gradients_become_immutable_helpers(self):
        source = """public static readonly Brush Glow = Freeze(new RadialGradientBrush(
        [
            new GradientStop(Color.FromArgb(200, 255, 236, 190), 0.25),
            new GradientStop(Color.FromArgb(0, 255, 170, 80), 1),
        ]));
public static readonly Brush Sky = Freeze(new LinearGradientBrush(Top, Bottom, 90));
public static readonly Brush Dot = Freeze(new SolidColorBrush(Gold));
"""
        out = tool.convert_types(source, "Lemon.Explore.Book.Chapters.Test")
        self.assertIn("IBrush Glow = SpaceDrawing.RadialGradient(", out)
        self.assertIn("(Color.FromArgb(200, 255, 236, 190), 0.25)", out)
        self.assertIn("SpaceDrawing.LinearGradient(Top, Bottom, 90)", out)
        self.assertIn("SpaceDrawing.Solid(Gold)", out)
        self.assertNotIn("Freeze(", out)

    def test_single_figure_geometry_gets_its_end_figure(self):
        source = """        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(first, false, true);
            ctx.LineTo(second, true, true);
        }
"""
        out = tool.convert_types(source, "Lemon.Explore.Book.Chapters.Test")
        self.assertIn("ctx.BeginFigure(first, false);", out)
        self.assertIn("ctx.LineTo(second);", out)
        self.assertIn("ctx.EndFigure(true);", out)

    def test_a_figure_per_pass_of_a_loop_ends_inside_the_loop(self):
        source = """        using (var context = geometry.Open())
        {
            foreach (var (from, to) in list)
            {
                context.BeginFigure(from, false, false);
                context.LineTo(to, true, false);
            }
        }
"""
        out = tool.convert_types(source, "Lemon.Explore.Book.Chapters.Test")
        self.assertIn("                context.LineTo(to);\n                context.EndFigure(false);\n            }\n        }", out)

    def test_a_line_through_a_loop_ends_after_the_loop(self):
        source = """        using (var ctx = geometry.Open())
        {
            for (var i = 0; i <= steps; i++)
            {
                if (i == 0) ctx.BeginFigure(At(i), false, false);
                else ctx.LineTo(At(i), true, false);
            }
        }
"""
        out = tool.convert_types(source, "Lemon.Explore.Book.Chapters.Test")
        self.assertIn("            }\n\n            ctx.EndFigure(false);\n        }", out)

    def test_dependency_properties_become_styled_properties(self):
        source = """public sealed class Chart : FrameworkElement
{
    public static readonly DependencyProperty DayProperty = DependencyProperty.Register(
        nameof(Day), typeof(double), typeof(Chart),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, e) => ((Chart)d).OnDayChanged((double)e.NewValue)));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(Chart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Day
    {
        get => (double)GetValue(DayProperty);
        set => SetValue(DayProperty, value);
    }

    protected override void OnRender(PaintContext dc)
    {
        dc.DrawLine(Pen, a, b);
    }
}
"""
        out = tool.convert_properties("using Avalonia;\n" + source)
        self.assertIn("StyledProperty<double> DayProperty", out)
        self.assertIn("AvaloniaProperty.Register<Chart, double>(nameof(Day), 0.0, defaultBindingMode: BindingMode.TwoWay)", out)
        self.assertIn("StyledProperty<string?> LabelProperty", out)
        self.assertIn("AffectsRender<Chart>(LabelProperty);", out)
        self.assertIn("OnDayChanged(change.GetNewValue<double>());", out)
        self.assertIn("get => GetValue(DayProperty);", out)
        self.assertIn("class Chart : Control", out)
        self.assertIn("using var dc = new PaintContext(context);", out)

    def test_mouse_handlers_become_pointer_handlers(self):
        source = """    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!CaptureMouse()) return;
        if ((point - _pressPoint).Length > 4) Go();
    }
"""
        out = tool.convert_plumbing(source)
        self.assertIn("OnPointerPressed(PointerPressedEventArgs e)", out)
        self.assertIn("IsLeftButtonPressed", out)
        self.assertIn("e.Pointer.Capture(this);", out)
        self.assertIn("((Vector)(point - _pressPoint)).Length", out)

    def test_point_differences_used_as_vectors_become_vectors(self):
        source = """        var away = center - moon;
        away /= away.Length;
        topLeft.X = Math.Max(0, x);
        if (at.X + text.Width > plot.Right) at.X = point.X - 8;
        tangent.Normalize();
"""
        out = tool.convert_points(source)
        self.assertIn("Vector away = center - moon;", out)
        self.assertIn("topLeft = topLeft.WithX(Math.Max(0, x));", out)
        self.assertIn("if (at.X + text.Width > plot.Right) at = at.WithX(point.X - 8);", out)
        self.assertIn("tangent = tangent.Normalize();", out)


class LateTests(unittest.TestCase):
    def test_gradients_with_an_initializer_become_immutable(self):
        source = """        var core = new LinearGradientBrush(light, shade, 90)
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(0, top),
            EndPoint = new Point(0, bottom),
        };
    private static readonly IBrush Ball = Freeze(new RadialGradientBrush(
    [
        new GradientStop(Colors.White, 0),
        new GradientStop(Colors.Blue, 1),
    ])
    {
        GradientOrigin = new Point(0.35, 0.3),
        RadiusX = 0.68,
        RadiusY = 0.68,
    });
"""
        out = tool.convert_late(source)
        self.assertIn("var core = new ImmutableLinearGradientBrush(", out)
        self.assertIn("new ImmutableGradientStop(0, light),", out)
        self.assertIn("startPoint: new RelativePoint(0, top, RelativeUnit.Absolute)", out)
        self.assertIn("endPoint: new RelativePoint(0, bottom, RelativeUnit.Absolute));", out)
        self.assertIn("Ball = new ImmutableRadialGradientBrush(", out)
        self.assertIn("new ImmutableGradientStop(1, Colors.Blue),", out)
        self.assertIn("gradientOrigin: new RelativePoint(0.35, 0.3, RelativeUnit.Relative)", out)
        self.assertIn("radiusY: new RelativeScalar(0.68, RelativeUnit.Relative));", out)
        self.assertNotIn("Freeze", out)

    def test_a_gradient_with_its_points_as_arguments(self):
        source = """        var shade = new LinearGradientBrush(
            [
                new GradientStop(Colors.White, 0),
                new GradientStop(Colors.Gray, 1),
            ],
            new Point(0, top), new Point(0, bottom))
        {
            MappingMode = BrushMappingMode.Absolute,
        };
"""
        out = tool.convert_late(source)
        self.assertIn("new ImmutableGradientStop(1, Colors.Gray),", out)
        self.assertIn("startPoint: new RelativePoint(0, top, RelativeUnit.Absolute),", out)
        self.assertIn("endPoint: new RelativePoint(0, bottom, RelativeUnit.Absolute));", out)

    def test_a_visual_rendered_to_a_bitmap_draws_into_it_directly(self):
        source = """            var surface = new DrawingVisual();
            using (var context = surface.RenderOpen()) _painter.DrawSurface(context, world);
            _map = new RenderTargetBitmap(w, h, 96 * dpi, 96 * dpi, PixelFormats.Pbgra32);
            _map.Render(surface);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(brush, null, rect);
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
"""
        out = tool.convert_late(source)
        self.assertIn("            _map = SpaceDrawing.RenderPixels(w, h, 96 * dpi, context => _painter.DrawSurface(context, world));\n", out)
        self.assertIn("        var bitmap = SpaceDrawing.RenderPixels(width, height, 96, dc =>\n        {\n"
                      "            dc.DrawRectangle(brush, null, rect);\n        });\n        return bitmap;", out)
        self.assertNotIn("DrawingVisual", out)

    def test_frozen_pens_and_rect_union(self):
        source = """    Trough = Freeze(new Pen(Solid(200, 1, 2, 3), 2) { DashStyle = new DashStyle([5, 4], 0) });
    pen = flat ? Freeze(new Pen(brush, thickness)) : MakePen(brush, thickness);
    AddBox(id, Rect.Union(Text(dc, name, new Point(x, y)),
        Text(dc, size, new Point(x, y + 17))));
"""
        out = tool.convert_late(source)
        self.assertIn("Trough = new ImmutablePen(((IBrush)Solid(200, 1, 2, 3)).ToImmutable(), 2, new ImmutableDashStyle([5, 4], 0));", out)
        self.assertIn("pen = flat ? new ImmutablePen(((IBrush)brush).ToImmutable(), thickness) : MakePen", out)
        self.assertIn("AddBox(id, Text(dc, name, new Point(x, y)).Union(Text(dc, size, new Point(x, y + 17))));", out)

    def test_point_assignment_but_not_a_class_field(self):
        source = """        at.X = Math.Max(0, at.X);
        g.Z -= 1;
        g.X += 0.1;
        g.X = 2;
"""
        out = tool.convert_points(source)
        self.assertIn("at = at.WithX(Math.Max(0, at.X));", out)
        self.assertIn("        g.X = 2;", out)

    def test_normalized_point_difference_is_a_vector(self):
        out = tool.convert_points("""            var outgoing = exit.To - exit.From;
            outgoing.Normalize();
""")
        self.assertIn("Vector outgoing = exit.To - exit.From;", out)
        self.assertIn("outgoing = outgoing.Normalize();", out)

    def test_title_band_becomes_a_margin(self):
        out = tool.convert_late(
            "    private double TitleSpace => Math.Max(Math.Clamp(146 + ((1050 - ActualWidth) * 0.3), 146, 220), 22 + TitleHeight + 12);\n")
        self.assertIn("    private double TitleSpace => 22 + TitleHeight + 12;", out)
        self.assertNotIn("Math.Max", out)

    def test_helper_from_another_file_of_the_class(self):
        siblings = """
    private static DependencyProperty TwoWay<T>(string name, T value) =>
        DependencyProperty.Register(name, typeof(T), typeof(QuakeDiagram), new FrameworkPropertyMetadata(value,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));
"""
        out = tool.expand_register_helpers(
            "    public static readonly DependencyProperty LastSlipProperty = TwoWay(nameof(LastSlip), 0.0);\n"
            "    public double LastSlip { get => (double)GetValue(LastSlipProperty); set => SetValue(LastSlipProperty, value); }\n",
            siblings)
        self.assertIn("DependencyProperty.Register(nameof(LastSlip), typeof(double), typeof(QuakeDiagram)", out)
        self.assertIn("BindsTwoWayByDefault", out)

    def test_initialized_pens_empty_rects_and_polylines(self):
        out = tool.convert_late("""        pen = new Pen(brush, 2) { StartLineCap = cap, EndLineCap = cap, LineJoin = PenLineJoin.Round };
        var dash = new Pen(Solid(200, 1, 2, 3), 1.6) { DashStyle = new DashStyle([2, 6], 0), DashCap = PenLineCap.Flat };
        private Rect _box = Rect.Empty;
        if (_box.IsEmpty) return;
        g.PolyLineTo(points, true, true);
        var length = ab.LengthSquared;
""")
        self.assertIn("pen = new ImmutablePen(((IBrush)brush).ToImmutable(), 2, null, cap, PenLineJoin.Round);", out)
        self.assertIn("new ImmutablePen(((IBrush)Solid(200, 1, 2, 3)).ToImmutable(), 1.6, new ImmutableDashStyle([2, 6], 0), PenLineCap.Flat, PenLineJoin.Miter)", out)
        self.assertIn("private Rect _box = default(Rect);", out)
        self.assertIn("if (_box == default) return;", out)
        self.assertIn("foreach (var next in points) g.LineTo(next);", out)
        self.assertIn("ab.SquaredLength", out)

    def test_rect_union_and_inflate_keep_their_result(self):
        out = tool.convert_points("        box.Union(new Rect(x, y, 2, 2));\n        box.Inflate(4, 2);\n")
        self.assertIn("box = box.Union(new Rect(x, y, 2, 2));", out)
        self.assertIn("box = box.Inflate(new Thickness(4, 2));", out)

    def test_bitmap_scaling_mode(self):
        out = tool.convert_types("        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Linear);\n", "N")
        self.assertIn("RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.MediumQuality);", out)

    def test_point_gap_lengths(self):
        out = tool.point_gap_lengths(
            "        var d = ((probe - new Point(0, -s / 2)).Length - (point - SourceA(sep)).Length) / lambda;\n"
            "        var e = Distance(a, b).Length + ((Vector)(c - d)).Length + (x + y).Length;\n")
        self.assertIn("(((Vector)(probe - new Point(0, -s / 2))).Length - ((Vector)(point - SourceA(sep))).Length)", out)
        self.assertIn("Distance(a, b).Length + ((Vector)(c - d)).Length + (x + y).Length", out)
        self.assertEqual("var g = (p - q).Length; Vec3 v;", tool.point_gap_lengths("var g = (p - q).Length; Vec3 v;"))

    def test_media3d_is_the_books_own(self):
        out = tool.convert_types("using System;\nusing System.Windows.Media.Media3D;\n\nnamespace N;\n", "N")
        self.assertIn("using Lemon.Explore.Book.Controls.Space;\n", out)
        self.assertIn("using Vector3D = Lemon.Explore.Book.Controls.Space.Vector3D;\n", out)
        self.assertNotIn("Media3D", out)

    def test_explore_helpers_by_name(self):
        out = tool.convert_late("        dc.DrawLine(Explore.ExploreTargets.GlowPen(now, 7), a, b);\n")
        self.assertIn("dc.DrawLine(ExploreTargets.GlowPen(now, 7), a, b);", out)

    def test_split_top_ignores_lambda_arrow(self):
        args = tool.split_top("0.0, (d, e) => ((C)d).Changed((double)e.OldValue, (double)e.NewValue)")
        self.assertEqual(["0.0", "(d, e) => ((C)d).Changed((double)e.OldValue, (double)e.NewValue)"], args)


if __name__ == "__main__":
    unittest.main()
