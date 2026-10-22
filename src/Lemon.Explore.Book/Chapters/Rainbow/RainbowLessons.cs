using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Rainbow;

/// <summary>
/// The rainbow page's spoken walkthroughs, one per view. Each stops the sweep and sets up what it talks about (the
/// beam's angle and colour, where the ray enters the drop, the Sun's height) so the picture matches the words.
/// Highlight ids are <see cref="RainbowDiagram"/>'s.
/// </summary>
internal static class RainbowLessons
{
    public static IReadOnlyList<LessonStep> For(RainbowViewModel page) => page.Mode switch
    {
        RainbowMode.Drop => Drop(page),
        RainbowMode.Sky => Sky(page),
        _ => Refraction(page),
    };

    /// <summary>Running, with the sweep stopped so the walkthrough sets the view's main value.</summary>
    private static void Hold(RainbowViewModel page)
    {
        page.IsPlaying = true;
        page.IsSweeping = false;
    }

    private static IReadOnlyList<LessonStep> Refraction(RainbowViewModel page)
    {
        // The task: aim the lamp somewhere else.
        var angleAtTask = 0.0;
        return
        [
            new("Narr_Rain_Refraction_01")
            {
                Enter = () =>
                {
                    page.Mode = RainbowMode.Refraction;
                    Hold(page);
                    page.Scene = RefractionScene.Surface;
                    page.Medium = OpticalMedium.Water;
                    page.Colour = LightColour.Red;
                    page.FromInside = false;
                    page.ShowWaves = true;
                    page.Exaggerate = true;
                    page.Angle = 40;
                },
                Highlight = "lamp",
            },
            new("Narr_Rain_Refraction_02") { Highlight = "crests" },
            new("Narr_Rain_Refraction_03") { Highlight = "refracted" },
            new("Narr_Rain_Refraction_04") { Highlight = "reflected" },
            new("Narr_Rain_Refraction_05")
            {
                Choices = ["Rain_Lesson_Refraction_Choice1", "Rain_Lesson_Refraction_Choice2", "Rain_Lesson_Refraction_Choice3"],
                Answer = 0,
            },
            new("Narr_Rain_Refraction_06")
            {
                Enter = () =>
                {
                    page.FromInside = true;
                    page.Angle = 60;
                },
                Highlight = "total",
            },
            new("Narr_Rain_Refraction_07")
            {
                Enter = () =>
                {
                    page.FromInside = false;
                    page.Colour = LightColour.White;
                    page.Angle = 50;
                },
                Highlight = "refracted",
            },
            new("Narr_Rain_Refraction_08")
            {
                Enter = () =>
                {
                    page.Scene = RefractionScene.Prism;
                    page.Medium = OpticalMedium.Glass;
                    page.Angle = 50;
                },
                Highlight = "spectrum",
            },
            new("Narr_Rain_Refraction_09")
            {
                Enter = () => angleAtTask = page.Angle,
                Highlight = "lamp",
                TryIt = () => Math.Abs(page.Angle - angleAtTask) >= 3,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Drop(RainbowViewModel page)
    {
        // The task: move where the ray enters the drop.
        var impactAtTask = 0.0;
        return
        [
            new("Narr_Rain_Drop_01")
            {
                Enter = () =>
                {
                    page.Mode = RainbowMode.Drop;
                    Hold(page);
                    page.Colour = LightColour.White;
                    page.Order = BowOrder.Primary;
                    page.ShowSheet = false;
                    page.ShowLosses = false;
                    page.Impact = 0.5;
                },
                Highlight = "drop",
            },
            new("Narr_Rain_Drop_02") { Highlight = "entry" },
            new("Narr_Rain_Drop_03")
            {
                Enter = () => page.SetRainbowRayCommand.Execute(null),
                Highlight = "exitangle",
            },
            new("Narr_Rain_Drop_04")
            {
                Enter = () =>
                {
                    // The followed ray off the rainbow ray, so the glowing one stands out.
                    page.Impact = 0.5;
                    page.ShowSheet = true;
                },
                Highlight = "bowray",
            },
            new("Narr_Rain_Drop_05")
            {
                Choices = ["Rain_Lesson_Drop_Choice1", "Rain_Lesson_Drop_Choice2", "Rain_Lesson_Drop_Choice3"],
                Answer = 0,
            },
            new("Narr_Rain_Drop_06")
            {
                Enter = () => page.ShowLosses = true,
                Highlight = "losses",
            },
            new("Narr_Rain_Drop_07")
            {
                Enter = () =>
                {
                    page.ShowLosses = false;
                    page.Order = BowOrder.Both;
                },
                Highlight = "crowd",
            },
            new("Narr_Rain_Drop_08")
            {
                Enter = () =>
                {
                    page.Order = BowOrder.Primary;
                    page.ShowSheet = false;
                    impactAtTask = page.Impact;
                },
                Highlight = "drop",
                TryIt = () => Math.Abs(page.Impact - impactAtTask) >= 0.1,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Sky(RainbowViewModel page)
    {
        // The task: move the Sun.
        var sunAtTask = 0.0;
        return
        [
            new("Narr_Rain_Sky_01")
            {
                Enter = () =>
                {
                    page.Mode = RainbowMode.Sky;
                    Hold(page);
                    page.ShowSecondary = true;
                    page.ShowGuides = true;
                    page.DropSize = DropSize.Rain;
                    page.SunAltitude = 15;
                },
                Highlight = "antisolar",
            },
            new("Narr_Rain_Sky_02") { Highlight = "bowangle" },
            new("Narr_Rain_Sky_03") { Highlight = "primary" },
            new("Narr_Rain_Sky_04") { Highlight = "brightinside" },
            new("Narr_Rain_Sky_05") { Highlight = "secondary" },
            new("Narr_Rain_Sky_06")
            {
                Choices = ["Rain_Lesson_Sky_Choice1", "Rain_Lesson_Sky_Choice2", "Rain_Lesson_Sky_Choice3"],
                Answer = 0,
            },
            new("Narr_Rain_Sky_07")
            {
                Enter = () => page.SunAltitude = 35,
                Highlight = "primary",
            },
            new("Narr_Rain_Sky_08")
            {
                Enter = () =>
                {
                    page.SunAltitude = 20;
                    sunAtTask = page.SunAltitude;
                },
                TryIt = () => Math.Abs(page.SunAltitude - sunAtTask) >= 3,
            },
        ];
    }
}
