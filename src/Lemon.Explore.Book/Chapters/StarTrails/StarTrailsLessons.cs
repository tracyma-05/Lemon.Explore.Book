using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.StarTrails;

/// <summary>
/// The star-trails page's spoken walkthroughs, one per view. Each points the camera, holds the exposure, travels to the
/// latitude or picks the date it talks about, so the picture matches the words; the words themselves never name the
/// viewer's own hemisphere or latitude. Highlight ids are <see cref="TrailsDiagram"/>'s.
/// </summary>
internal static class StarTrailsLessons
{
    public static IReadOnlyList<LessonStep> For(StarTrailsViewModel page) => page.Mode switch
    {
        TrailsMode.Sphere => Sphere(page),
        TrailsMode.Year => Year(page),
        _ => Camera(page),
    };

    private static void Labels(StarTrailsViewModel page)
    {
        page.SpeedChoice.Selected = "1";
        page.ShowLabels = true;
        page.ShowFigures = true;
    }

    /// <summary>The camera pointed one way and the exposure held at <paramref name="hours"/>.</summary>
    private static void Hold(StarTrailsViewModel page, string facing, double hours)
    {
        page.FaceCommand.Execute(facing);
        page.Exposure = hours;
        page.IsPlaying = false;
    }

    private static IReadOnlyList<LessonStep> Camera(StarTrailsViewModel page)
    {
        // The task: turn the camera, tilt it, or travel.
        var atTask = (Azimuth: 0.0, Altitude: 0.0, Latitude: 0.0);
        return
        [
            new("Narr_Trails_Camera_01")
            {
                Enter = () =>
                {
                    page.Mode = TrailsMode.Camera;
                    Labels(page);
                    page.UseMyLatitudeCommand.Execute(null);
                    page.FaceCommand.Execute("Pole");
                    page.RestartCommand.Execute(null);
                },
                Highlight = "shutter",
            },
            new("Narr_Trails_Camera_02") { Highlight = "pole" },
            new("Narr_Trails_Camera_03")
            {
                Enter = () => Hold(page, "Pole", 4),
                Highlight = "arc",
            },
            new("Narr_Trails_Camera_04")
            {
                Choices = ["Trails_Lesson_Camera_Choice1", "Trails_Lesson_Camera_Choice2", "Trails_Lesson_Camera_Choice3"],
                Answer = 0,
            },
            new("Narr_Trails_Camera_05")
            {
                Enter = () => Hold(page, "Pole", 7),
                Highlight = "horizon",
            },
            new("Narr_Trails_Camera_06")
            {
                Enter = () => Hold(page, "Equator", 3),
                Highlight = "compass",
            },
            new("Narr_Trails_Camera_07")
            {
                Enter = () => Hold(page, "East", 2),
                Highlight = "horizon",
            },
            new("Narr_Trails_Camera_08")
            {
                Enter = () =>
                {
                    page.Latitude = 0;
                    Hold(page, "Pole", 4);
                },
                Highlight = "pole",
            },
            new("Narr_Trails_Camera_09")
            {
                Enter = () =>
                {
                    page.Latitude = 90;
                    Hold(page, "Up", 6);
                },
                Highlight = "pole",
            },
            new("Narr_Trails_Camera_10")
            {
                Enter = () =>
                {
                    page.UseMyLatitudeCommand.Execute(null);
                    page.FaceCommand.Execute("Pole");
                    page.RestartCommand.Execute(null);
                    atTask = (page.ViewAzimuth, page.ViewAltitude, page.Latitude);
                },
                TryIt = () => Math.Abs(SkyModel.Wrap360(page.ViewAzimuth - atTask.Azimuth + 180) - 180) >= 15 ||
                              Math.Abs(page.ViewAltitude - atTask.Altitude) >= 10 || Math.Abs(page.Latitude - atTask.Latitude) >= 5,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Sphere(StarTrailsViewModel page)
    {
        // The task: travel north or south.
        var latitudeAtTask = 0.0;
        var visiblePole = page.Latitude >= 0 ? "ncp" : "scp";
        return
        [
            new("Narr_Trails_Sphere_01")
            {
                Enter = () =>
                {
                    page.Mode = TrailsMode.Sphere;
                    Labels(page);
                    page.UseMyLatitudeCommand.Execute(null);
                    page.IsPlaying = true;
                },
                Highlight = "observer",
            },
            new("Narr_Trails_Sphere_02") { Highlight = visiblePole },
            new("Narr_Trails_Sphere_03") { Highlight = "equator" },
            new("Narr_Trails_Sphere_04") { Highlight = "circumpolar" },
            new("Narr_Trails_Sphere_05") { Highlight = "never" },
            new("Narr_Trails_Sphere_06")
            {
                Choices = ["Trails_Lesson_Sphere_Choice1", "Trails_Lesson_Sphere_Choice2", "Trails_Lesson_Sphere_Choice3"],
                Answer = 0,
            },
            new("Narr_Trails_Sphere_07")
            {
                Enter = () => page.Latitude = 0,
                Highlight = "equator",
            },
            new("Narr_Trails_Sphere_08")
            {
                Enter = () => page.Latitude = 90,
                Highlight = "zenith",
            },
            new("Narr_Trails_Sphere_09")
            {
                Enter = () =>
                {
                    page.UseMyLatitudeCommand.Execute(null);
                    latitudeAtTask = page.Latitude;
                },
                TryIt = () => Math.Abs(page.Latitude - latitudeAtTask) >= 10,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Year(StarTrailsViewModel page)
    {
        // The task: another date.
        var dayAtTask = 0.0;
        return
        [
            new("Narr_Trails_Year_01")
            {
                Enter = () =>
                {
                    page.Mode = TrailsMode.Year;
                    Labels(page);
                    page.UseMyLatitudeCommand.Execute(null);
                    page.JumpToDayCommand.Execute("355");
                },
                Highlight = "night",
            },
            new("Narr_Trails_Year_02") { Highlight = TrailsDiagram.FigureId("Orion") },
            new("Narr_Trails_Year_03")
            {
                Enter = () => page.JumpToDayCommand.Execute("172"),
                Highlight = TrailsDiagram.FigureId("Scorpius"),
            },
            new("Narr_Trails_Year_04")
            {
                Choices = ["Trails_Lesson_Year_Choice1", "Trails_Lesson_Year_Choice2", "Trails_Lesson_Year_Choice3"],
                Answer = 0,
            },
            new("Narr_Trails_Year_05") { Highlight = "sidereal" },
            new("Narr_Trails_Year_06")
            {
                Enter = () => page.IsPlaying = true,
                Highlight = "panorama",
            },
            new("Narr_Trails_Year_07") { Highlight = "earth" },
            new("Narr_Trails_Year_08")
            {
                Enter = () =>
                {
                    page.IsPlaying = false;
                    dayAtTask = page.Day;
                },
                TryIt = () => Math.Abs(page.Day - dayAtTask) >= 15,
            },
        ];
    }
}
