using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Lenses;

/// <summary>
/// The lenses page's spoken walkthroughs, one per view. Each sets the lens and candle, the eye, or the camera's settings
/// so the picture matches the words, and the last step hands over to the viewer. Highlight ids are
/// <see cref="LensDiagram"/>'s.
/// </summary>
internal static class LensesLessons
{
    public static IReadOnlyList<LessonStep> For(LensesViewModel page) => page.Mode switch
    {
        LensMode.Eye => Eye(page),
        LensMode.Camera => Camera(page),
        _ => Bench(page),
    };

    /// <summary>The view set up with labels, playing.</summary>
    private static void Setup(LensesViewModel page, LensMode mode)
    {
        page.Mode = mode;
        page.ShowLabels = true;
        page.IsPlaying = true;
    }

    private static IReadOnlyList<LessonStep> Bench(LensesViewModel page)
    {
        // The task: the candle, the screen or the lens changed.
        var atTask = (Candle: 0.0, Screen: 0.0, Focal: 0.0);
        return
        [
            new("Narr_Lens_Bench_01")
            {
                Enter = () =>
                {
                    Setup(page, LensMode.Bench);
                    page.LensKind = LensKind.Convex;
                    page.RayStyle = RayStyle.Principal;
                    page.ShowColours = false;
                    page.FocalLength = 15;
                    page.ObjectHeight = 3;
                    page.ObjectDistance = 45;
                    page.ShowScreen = true;
                    page.ScreenDistance = 22.5;
                },
                Highlight = "lens",
            },
            new("Narr_Lens_Bench_02") { Highlight = "ray1" },
            new("Narr_Lens_Bench_03") { Highlight = "focus" },
            new("Narr_Lens_Bench_04") { Highlight = "ray2" },
            new("Narr_Lens_Bench_05") { Highlight = "ray3" },
            new("Narr_Lens_Bench_06") { Highlight = "image" },
            new("Narr_Lens_Bench_07")
            {
                Choices = ["Lens_Lesson_Bench_Choice1", "Lens_Lesson_Bench_Choice2", "Lens_Lesson_Bench_Choice3"],
                Answer = 0,
            },
            new("Narr_Lens_Bench_08")
            {
                Enter = () =>
                {
                    page.ObjectDistance = 22.5;
                    page.ScreenDistance = 45;
                },
                Highlight = "image",
            },
            new("Narr_Lens_Bench_09")
            {
                Enter = () => page.ObjectDistance = 9,
                Highlight = "virtual",
            },
            new("Narr_Lens_Bench_10")
            {
                Enter = () =>
                {
                    page.ObjectDistance = 45;
                    page.ScreenDistance = 36;
                },
                Highlight = "screenview",
            },
            new("Narr_Lens_Bench_11")
            {
                Enter = () =>
                {
                    page.LensKind = LensKind.Concave;
                    page.ObjectDistance = 30;
                },
                Highlight = "lens",
            },
            new("Narr_Lens_Bench_12")
            {
                Enter = () =>
                {
                    page.LensKind = LensKind.Convex;
                    page.ObjectDistance = 40;
                    page.ScreenDistance = 30;
                    atTask = (page.ObjectDistance, page.ScreenDistance, page.FocalLength);
                },
                Highlight = "object",
                TryIt = () => page.ObjectDistance != atTask.Candle || page.ScreenDistance != atTask.Screen || page.FocalLength != atTask.Focal,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Eye(LensesViewModel page)
    {
        // The task: the chart moved, or the age, the eye or the glasses changed.
        var atTask = (Distance: 0.0, Age: 0.0, Error: 0.0, Glasses: Spectacles.None);
        return
        [
            new("Narr_Lens_Eye_01")
            {
                Enter = () =>
                {
                    Setup(page, LensMode.Eye);
                    page.Error = 0;
                    page.Age = 20;
                    page.Glasses = Spectacles.None;
                    page.Light = LightLevel.Indoor;
                    page.Distance = double.PositiveInfinity;
                },
                Highlight = "cornea",
            },
            new("Narr_Lens_Eye_02") { Highlight = "iris" },
            new("Narr_Lens_Eye_03") { Highlight = "eyelens" },
            new("Narr_Lens_Eye_04") { Highlight = "retinalimage" },
            new("Narr_Lens_Eye_05")
            {
                Enter = () => page.Distance = 0.25,
                Highlight = "ciliary",
            },
            new("Narr_Lens_Eye_06") { Highlight = "nearpoint" },
            new("Narr_Lens_Eye_07")
            {
                Enter = () =>
                {
                    page.Age = 60;
                    page.Distance = 0.33;
                },
                Highlight = "nearpoint",
            },
            new("Narr_Lens_Eye_08")
            {
                Enter = () =>
                {
                    page.Age = 20;
                    page.Distance = double.PositiveInfinity;
                },
                Choices = ["Lens_Lesson_Eye_Choice1", "Lens_Lesson_Eye_Choice2", "Lens_Lesson_Eye_Choice3"],
                Answer = 0,
            },
            new("Narr_Lens_Eye_09")
            {
                Enter = () => page.Error = -3,
                Highlight = "farpoint",
            },
            new("Narr_Lens_Eye_10")
            {
                Enter = () => page.Glasses = Spectacles.Distance,
                Highlight = "glasses",
            },
            new("Narr_Lens_Eye_11")
            {
                Enter = () => atTask = (page.Distance, page.Age, page.Error, page.Glasses),
                Highlight = "chart",
                TryIt = () => !Same(page.Distance, atTask.Distance) || page.Age != atTask.Age || page.Error != atTask.Error || page.Glasses != atTask.Glasses,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Camera(LensesViewModel page)
    {
        // The task: focus, aperture, shutter or focal length changed.
        var atTask = (Focus: 0.0, FNumber: 0.0, Shutter: 0.0, Focal: 0.0);
        return
        [
            new("Narr_Lens_Camera_01")
            {
                Enter = () =>
                {
                    Setup(page, LensMode.Camera);
                    page.Scene = SceneLight.Cloud;
                    page.FocalMm = 50;
                    page.FNumber = 5.6;
                    page.FocusDistance = 5;
                    page.Iso = 100;
                    page.Shutter = 1.0 / 125;
                },
                Highlight = "sensor",
            },
            new("Narr_Lens_Camera_02") { Highlight = "camlens" },
            new("Narr_Lens_Camera_03") { Highlight = "focusmark" },
            new("Narr_Lens_Camera_04") { Highlight = "blurcircle" },
            new("Narr_Lens_Camera_05")
            {
                Enter = () =>
                {
                    page.FNumber = 1.4;
                    page.AutoExposeCommand.Execute(null);
                },
                Highlight = "aperture",
            },
            new("Narr_Lens_Camera_06")
            {
                Enter = () =>
                {
                    page.FNumber = 11;
                    page.AutoExposeCommand.Execute(null);
                },
                Highlight = "dof",
            },
            new("Narr_Lens_Camera_07")
            {
                Choices = ["Lens_Lesson_Camera_Choice1", "Lens_Lesson_Camera_Choice2", "Lens_Lesson_Camera_Choice3"],
                Answer = 0,
            },
            new("Narr_Lens_Camera_08")
            {
                Enter = () =>
                {
                    page.FNumber = 5.6;
                    page.Scene = SceneLight.Dusk;
                    page.Shutter = 1.0 / 15;
                },
                Highlight = "ball",
            },
            new("Narr_Lens_Camera_09")
            {
                Enter = () => page.Shutter = 1.0 / 1000,
                Highlight = "meter",
            },
            new("Narr_Lens_Camera_10")
            {
                Enter = () => page.Iso = 6400,
                Highlight = "photo",
            },
            new("Narr_Lens_Camera_11")
            {
                Enter = () =>
                {
                    page.Iso = 100;
                    page.Scene = SceneLight.Cloud;
                    page.Shutter = 1.0 / 125;
                    page.FocalMm = 200;
                },
                Highlight = "fov",
            },
            new("Narr_Lens_Camera_12")
            {
                Enter = () =>
                {
                    page.FocalMm = 50;
                    atTask = (page.FocusDistance, page.FNumber, page.Shutter, page.FocalMm);
                },
                Highlight = "photo",
                TryIt = () => !Same(page.FocusDistance, atTask.Focus) || page.FNumber != atTask.FNumber || page.Shutter != atTask.Shutter || page.FocalMm != atTask.Focal,
            },
        ];
    }

    /// <summary>Two distances the same (both infinity, or equal).</summary>
    private static bool Same(double a, double b) => a.Equals(b);
}
