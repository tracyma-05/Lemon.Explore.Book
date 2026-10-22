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
"""
        out = tool.convert_points(source)
        self.assertIn("Vector away = center - moon;", out)
        self.assertIn("topLeft = topLeft.WithX(Math.Max(0, x));", out)


if __name__ == "__main__":
    unittest.main()
