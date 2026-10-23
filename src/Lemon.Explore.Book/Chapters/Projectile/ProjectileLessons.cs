using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Projectile;

/// <summary>
/// The projectile page's spoken walkthroughs, one per view. Each sets up the shot it talks about (angle, speed, height,
/// air, the scene) so the picture matches the words. Highlight ids are <see cref="ProjectileDiagram"/>'s.
/// </summary>
internal static class ProjectileLessons
{
    public static IReadOnlyList<LessonStep> For(ProjectileViewModel page) => page.Mode switch
    {
        ProjectileMode.BestAngle => BestAngle(page),
        ProjectileMode.Together => Together(page),
        _ => Launch(page),
    };

    /// <summary>A plain baseball on Earth, no air, at real speed.</summary>
    private static void Plain(ProjectileViewModel page)
    {
        page.Place = ProjectilePlace.Earth;
        page.Ball = ProjectileBall.Baseball;
        page.AirDrag = false;
        page.SpeedChoice.Selected = "1";
    }

    /// <summary>Stops on the landed scene, so what it shows at the end stays on screen while it is talked about.</summary>
    private static void ShowLanded(ProjectileViewModel page)
    {
        page.IsPlaying = false;
        page.Time = page.Duration;
    }

    private static IReadOnlyList<LessonStep> Launch(ProjectileViewModel page)
    {
        // The task: aim a shot of your own.
        var shotAtTask = (Angle: 0.0, Speed: 0.0);
        return
        [
            new("Narr_Proj_Launch_01")
            {
                Enter = () =>
                {
                    page.Mode = ProjectileMode.Launch;
                    Plain(page);
                    page.CliffHeight = 0;
                    page.ShowVectors = true;
                    page.ShowStrobe = true;
                    page.ShowShadows = false;
                    page.KeepTrails = true;
                    page.Angle = 45;
                    page.LaunchSpeed = 20;
                    page.ClearTrailsCommand.Execute(null);
                    page.LaunchCommand.Execute(null);
                },
                Highlight = "cannon",
            },
            new("Narr_Proj_Launch_02") { Highlight = "strobe" },
            new("Narr_Proj_Launch_03") { Highlight = "vx" },
            new("Narr_Proj_Launch_04") { Highlight = "vy" },
            new("Narr_Proj_Launch_05")
            {
                Enter = () => page.ShowShadows = true,
                Highlight = "groundshadow",
            },
            new("Narr_Proj_Launch_06")
            {
                Choices = ["Proj_Lesson_Launch_Choice1", "Proj_Lesson_Launch_Choice2", "Proj_Lesson_Launch_Choice3"],
                Answer = 0,
            },
            new("Narr_Proj_Launch_07")
            {
                Enter = () =>
                {
                    page.ShowShadows = false;
                    page.Angle = 30;
                    page.LaunchCommand.Execute(null);
                },
                Highlight = "path",
            },
            new("Narr_Proj_Launch_08")
            {
                Enter = () =>
                {
                    page.Angle = 60;
                    page.LaunchCommand.Execute(null);
                },
                Highlight = "range",
            },
            new("Narr_Proj_Launch_09")
            {
                Enter = () =>
                {
                    page.Angle = 45;
                    page.Ball = ProjectileBall.PingPong;
                    page.AirDrag = true;

                    // Cleared last: changing the shot keeps the one before as a path.
                    page.ClearTrailsCommand.Execute(null);
                    page.LaunchCommand.Execute(null);
                },
                Highlight = "airless",
            },
            new("Narr_Proj_Launch_10")
            {
                Enter = () =>
                {
                    Plain(page);
                    page.LaunchCommand.Execute(null);
                    shotAtTask = (page.Angle, page.LaunchSpeed);
                },
                Highlight = "cannon",
                TryIt = () => Math.Abs(page.Angle - shotAtTask.Angle) >= 3 || Math.Abs(page.LaunchSpeed - shotAtTask.Speed) >= 2,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> BestAngle(ProjectileViewModel page)
    {
        // The task: another speed.
        var speedAtTask = 0.0;
        return
        [
            new("Narr_Proj_Best_01")
            {
                Enter = () =>
                {
                    page.Mode = ProjectileMode.BestAngle;
                    Plain(page);
                    page.CliffHeight = 0;
                    page.LaunchSpeed = 20;
                    page.LaunchCommand.Execute(null);
                },
                Highlight = "launcher",
            },
            new("Narr_Proj_Best_02")
            {
                Choices = ["Proj_Lesson_Best_Choice1", "Proj_Lesson_Best_Choice2", "Proj_Lesson_Best_Choice3"],
                Answer = 0,
            },
            new("Narr_Proj_Best_03")
            {
                Enter = () => ShowLanded(page),
                Highlight = "farthest",
            },
            new("Narr_Proj_Best_04") { Highlight = "landing" },
            new("Narr_Proj_Best_05") { Highlight = "envelope" },
            new("Narr_Proj_Best_06")
            {
                Enter = () =>
                {
                    page.CliffHeight = 20;
                    ShowLanded(page);
                },
                Highlight = "farthest",
            },
            new("Narr_Proj_Best_07")
            {
                Enter = () =>
                {
                    page.CliffHeight = 0;
                    page.Ball = ProjectileBall.Soccer;
                    page.AirDrag = true;
                    ShowLanded(page);
                },
                Highlight = "farthest",
            },
            new("Narr_Proj_Best_08")
            {
                Enter = () =>
                {
                    Plain(page);
                    page.LaunchCommand.Execute(null);
                    speedAtTask = page.LaunchSpeed;
                },
                TryIt = () => Math.Abs(page.LaunchSpeed - speedAtTask) >= 2,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Together(ProjectileViewModel page)
    {
        // The task: another dart speed.
        var dartAtTask = 0.0;
        return
        [
            new("Narr_Proj_Together_01")
            {
                Enter = () =>
                {
                    page.Mode = ProjectileMode.Together;
                    Plain(page);
                    page.Scene = TogetherScene.DropAndThrow;
                    page.TowerHeight = 20;
                    page.ThrowSpeed = 8;
                    page.LaunchCommand.Execute(null);
                },
                Highlight = "dropped",
            },
            new("Narr_Proj_Together_02")
            {
                Choices = ["Proj_Lesson_Together_Choice1", "Proj_Lesson_Together_Choice2", "Proj_Lesson_Together_Choice3"],
                Answer = 0,
            },
            new("Narr_Proj_Together_03")
            {
                Enter = () => page.LaunchCommand.Execute(null),
                Highlight = "thrown",
            },
            new("Narr_Proj_Together_04") { Highlight = "oddnumbers" },
            new("Narr_Proj_Together_05")
            {
                Enter = () =>
                {
                    page.ThrowSpeed = 16;
                    page.LaunchCommand.Execute(null);
                },
                Highlight = "thrown",
            },
            new("Narr_Proj_Together_06")
            {
                Enter = () =>
                {
                    page.Scene = TogetherScene.MonkeyHunter;
                    page.DartSpeed = 25;
                    page.MonkeyLetsGo = true;
                    page.IsPlaying = false;
                    page.Time = 0;
                },
                Highlight = "monkey",
            },
            new("Narr_Proj_Together_07")
            {
                Choices = ["Proj_Lesson_Together_Choice4", "Proj_Lesson_Together_Choice5", "Proj_Lesson_Together_Choice6"],
                Answer = 0,
            },
            new("Narr_Proj_Together_08")
            {
                Enter = () => page.LaunchCommand.Execute(null),
                Highlight = "fall",
            },
            new("Narr_Proj_Together_09")
            {
                Enter = () => dartAtTask = page.DartSpeed,
                Highlight = "dart",
                TryIt = () => Math.Abs(page.DartSpeed - dartAtTask) >= 2,
            },
        ];
    }
}
