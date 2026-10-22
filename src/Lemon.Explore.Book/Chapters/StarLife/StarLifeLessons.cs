using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.StarLife;

/// <summary>
/// The life of a star page's spoken walkthroughs, one per view. Each step holds the star at the stage it talks about
/// (<see cref="StarLifeViewModel.GoToStage"/>), since playback runs through a whole life in about a minute. Highlight ids
/// are <see cref="StarDiagram"/>'s.
/// </summary>
internal static class StarLifeLessons
{
    public static IReadOnlyList<LessonStep> For(StarLifeViewModel page) => page.Mode switch
    {
        StarMode.Diagram => Diagram(page),
        StarMode.Fates => Fates(page),
        _ => Life(page),
    };

    private static string Stage(StarStage stage) => StarDiagram.StageTarget(stage);

    private static IReadOnlyList<LessonStep> Life(StarLifeViewModel page)
    {
        // The task: travel some way along the strip of stages.
        var storyAtTask = 0.0;
        return
        [
            new("Narr_Star_Life_01")
            {
                Enter = () =>
                {
                    page.Mode = StarMode.Life;
                    page.Mass = 1;
                    page.ShowSun = true;
                    page.ShowOrbits = true;
                    page.GoToStage(StarStage.Protostar);
                },
                Highlight = Stage(StarStage.Protostar),
            },
            new("Narr_Star_Life_02")
            {
                Enter = () => page.GoToStage(StarStage.MainSequence, 0.45),
                Highlight = Stage(StarStage.MainSequence),
            },
            new("Narr_Star_Life_03") { Highlight = "orbits" },
            new("Narr_Star_Life_04")
            {
                Choices = ["Star_Lesson_Life_Choice1", "Star_Lesson_Life_Choice2", "Star_Lesson_Life_Choice3"],
                Answer = 0,
            },
            new("Narr_Star_Life_05")
            {
                Enter = () => page.GoToStage(StarStage.RedGiant, 0.9),
                Highlight = Stage(StarStage.RedGiant),
            },
            new("Narr_Star_Life_06")
            {
                Enter = () => page.GoToStage(StarStage.PlanetaryNebula),
                Highlight = Stage(StarStage.PlanetaryNebula),
            },
            new("Narr_Star_Life_07")
            {
                Enter = () => page.GoToStage(StarStage.WhiteDwarf),
                Highlight = Stage(StarStage.WhiteDwarf),
            },
            new("Narr_Star_Life_08")
            {
                Enter = () => storyAtTask = page.Story,
                Highlight = "strip",
                TryIt = () => Math.Abs(page.Story - storyAtTask) > 3,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Diagram(StarLifeViewModel page)
    {
        // The task: pick a noticeably lighter or heavier star.
        var massAtTask = 1.0;
        return
        [
            new("Narr_Star_Diagram_01")
            {
                Enter = () =>
                {
                    page.Mode = StarMode.Diagram;
                    page.Mass = 1;
                    page.ShowKnownStars = true;
                    page.GoToStage(StarStage.MainSequence, 0.45);
                },
            },
            new("Narr_Star_Diagram_02") { Highlight = "mainsequence" },
            new("Narr_Star_Diagram_03") { Highlight = "known-Sun" },
            new("Narr_Star_Diagram_04") { Highlight = "giants" },
            new("Narr_Star_Diagram_05") { Highlight = "whitedwarfs" },
            new("Narr_Star_Diagram_06")
            {
                Choices = ["Star_Lesson_Diagram_Choice1", "Star_Lesson_Diagram_Choice2", "Star_Lesson_Diagram_Choice3"],
                Answer = 0,
            },
            new("Narr_Star_Diagram_07")
            {
                Enter = () =>
                {
                    page.GoToStage(StarStage.Subgiant, 0.2);
                    page.IsPlaying = true;
                },
                Highlight = "track",
            },
            new("Narr_Star_Diagram_08")
            {
                Enter = () => massAtTask = page.Mass,
                TryIt = () => page.Mass > massAtTask * 1.4 || page.Mass < massAtTask / 1.4,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Fates(StarLifeViewModel page)
    {
        // The task: pick another kind of star.
        var kindAtTask = StarClass.SunLike;
        return
        [
            new("Narr_Star_Fates_01")
            {
                Enter = () =>
                {
                    page.Mode = StarMode.Fates;
                    page.Mass = 1;
                    page.IsPlaying = true;
                },
                Highlight = Stage(StarStage.Protostar),
            },
            new("Narr_Star_Fates_02") { Highlight = "class-" + StarClass.RedDwarf },
            new("Narr_Star_Fates_03") { Highlight = "class-" + StarClass.SunLike },
            new("Narr_Star_Fates_04")
            {
                Choices = ["Star_Lesson_Fates_Choice1", "Star_Lesson_Fates_Choice2", "Star_Lesson_Fates_Choice3"],
                Answer = 0,
            },
            new("Narr_Star_Fates_05")
            {
                Enter = () =>
                {
                    page.Mass = 12;
                    page.GoToStage(StarStage.Supernova, 0.3);
                },
                Highlight = Stage(StarStage.Supernova),
            },
            new("Narr_Star_Fates_06")
            {
                Enter = () => page.GoToStage(StarStage.NeutronStar),
                Highlight = Stage(StarStage.NeutronStar),
            },
            new("Narr_Star_Fates_07")
            {
                Enter = () =>
                {
                    page.Mass = 30;
                    page.GoToStage(StarStage.BlackHole);
                },
                Highlight = Stage(StarStage.BlackHole),
            },
            new("Narr_Star_Fates_08")
            {
                Enter = () => kindAtTask = StarModel.ClassOf(page.Mass),
                TryIt = () => StarModel.ClassOf(page.Mass) != kindAtTask,
            },
            new("Narr_Star_Fates_09"),
        ];
    }
}
