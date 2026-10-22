using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Earthquakes;

/// <summary>
/// The earthquake page's spoken walkthroughs, one per view. The map's steps switch layers and pick the quakes being
/// talked about; the boundary steps move from section to section and set their clocks so the picture shows what is said
/// (a plate edge nearly due to break, India just after it met Asia); the inside steps hold the waves at the moment
/// talked about. The last step of each hands over to the viewer. Highlight ids are <see cref="QuakeDiagram"/>'s.
/// </summary>
internal static class QuakeLessons
{
    public static IReadOnlyList<LessonStep> For(QuakeViewModel page) => page.Mode switch
    {
        QuakeMode.Boundaries => Boundaries(page),
        QuakeMode.Inside => Inside(page),
        _ => Plates(page),
    };

    /// <summary>The view set up with labels, playing.</summary>
    private static void Setup(QuakeViewModel page, QuakeMode mode)
    {
        page.Mode = mode;
        page.ShowLabels = true;
        page.IsPlaying = true;
    }

    private static IReadOnlyList<LessonStep> Plates(QuakeViewModel page)
    {
        // The task: a boundary, a plate or a famous quake picked.
        var atTask = (Boundary: -1, Plate: (string?)null, Famous: (string?)null);
        return
        [
            new("Narr_Quake_Plates_01")
            {
                Enter = () =>
                {
                    Setup(page, QuakeMode.Plates);
                    page.SixPlates = false;
                    page.ShowQuakes = true;
                    page.ShowVolcanoes = true;
                    page.ShowArrows = false;
                    page.ClearPicksCommand.Execute(null);
                },
                Highlight = "depth",
            },
            new("Narr_Quake_Plates_02") { Highlight = "plate:PA" },
            new("Narr_Quake_Plates_03")
            {
                Enter = () => page.SixPlates = true,
                Highlight = "group:Indian",
            },
            new("Narr_Quake_Plates_04")
            {
                Enter = () =>
                {
                    page.SixPlates = false;
                    page.ShowArrows = true;
                },
                Highlight = "plate:PA",
            },
            new("Narr_Quake_Plates_05") { Highlight = "ridge" },
            new("Narr_Quake_Plates_06") { Highlight = "trench" },
            new("Narr_Quake_Plates_07")
            {
                Choices = ["Quake_Lesson_Plates_Choice1", "Quake_Lesson_Plates_Choice2", "Quake_Lesson_Plates_Choice3"],
                Answer = 0,
                Highlight = "depth",
            },
            new("Narr_Quake_Plates_08")
            {
                Enter = () => page.Famous = "Wenchuan2008",
                Highlight = "collision",
            },
            new("Narr_Quake_Plates_09")
            {
                Enter = () => page.Famous = "SanFrancisco1906",
                Highlight = "transform",
            },
            new("Narr_Quake_Plates_10")
            {
                Enter = () => page.Famous = null,
                Highlight = "hawaii",
            },
            new("Narr_Quake_Plates_11")
            {
                Enter = () =>
                {
                    page.ClearPicksCommand.Execute(null);
                    atTask = (page.SelectedBoundary, page.SelectedPlate, page.Famous);
                },
                Highlight = "place",
                TryIt = () => page.SelectedBoundary != atTask.Boundary || page.SelectedPlate != atTask.Plate || page.Famous != atTask.Famous,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Boundaries(QuakeViewModel page)
    {
        // The task: the fault made to break.
        var atTask = 0;
        return
        [
            new("Narr_Quake_Boundaries_01")
            {
                Enter = () =>
                {
                    Setup(page, QuakeMode.Boundaries);
                    page.Section = SectionKind.Ridge;
                    page.HalfRate = 2.5;
                    page.RestartSectionCommand.Execute(null);
                },
                Highlight = "magma",
            },
            new("Narr_Quake_Boundaries_02") { Highlight = "field" },
            new("Narr_Quake_Boundaries_03") { Highlight = "stripes" },
            new("Narr_Quake_Boundaries_04")
            {
                Enter = () =>
                {
                    page.SectionTime = 0;
                    page.IsPlaying = false;
                },
                Choices = ["Quake_Lesson_Boundaries_Choice1", "Quake_Lesson_Boundaries_Choice2", "Quake_Lesson_Boundaries_Choice3"],
                Answer = 0,
            },
            new("Narr_Quake_Boundaries_05")
            {
                Enter = () =>
                {
                    page.Section = SectionKind.Subduction;
                    page.Dip = 40;
                    page.OverOcean = false;
                    page.RestartSectionCommand.Execute(null);
                },
                Highlight = "trench",
            },
            new("Narr_Quake_Boundaries_06") { Highlight = "benioff" },
            new("Narr_Quake_Boundaries_07") { Highlight = "arcvolcano" },
            new("Narr_Quake_Boundaries_08")
            {
                // Nearly due: it breaks a few seconds into the step.
                Enter = () =>
                {
                    page.IsPlaying = true;
                    page.SectionTime = Math.Max(page.SectionTime, QuakeDiagram.MegathrustCycle - 60);
                },
                Highlight = "locked",
            },
            new("Narr_Quake_Boundaries_09")
            {
                Enter = () =>
                {
                    page.Section = SectionKind.Collision;
                    page.RestartSectionCommand.Execute(null);
                },
                Highlight = "india",
            },
            new("Narr_Quake_Boundaries_10")
            {
                Enter = () =>
                {
                    page.SectionTime = 12;
                    page.IsPlaying = true;
                },
                Highlight = "himalaya",
            },
            new("Narr_Quake_Boundaries_11")
            {
                Enter = () =>
                {
                    page.Section = SectionKind.Transform;
                    page.Creep = false;
                    page.RestartSectionCommand.Execute(null);
                },
                Highlight = "fence",
            },
            new("Narr_Quake_Boundaries_12")
            {
                Enter = () => atTask = page.QuakeCount,
                Highlight = "fault",
                TryIt = () => page.QuakeCount > atTask,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Inside(QuakeViewModel page)
    {
        // The task: a station put somewhere else, or the quake moved.
        var atTask = (Station: 0.0, Angle: 0.0);
        return
        [
            new("Narr_Quake_Inside_01")
            {
                Enter = () =>
                {
                    Setup(page, QuakeMode.Inside);
                    page.ShowRays = true;
                    page.QuakeAngle = 0;
                    page.Magnitude = 7;
                    page.SetStationCommand.Execute("60");
                    page.WaveTime = 0;
                    page.IsPlaying = false;
                },
                Highlight = "quake",
            },
            new("Narr_Quake_Inside_02")
            {
                Enter = () =>
                {
                    page.WaveTime = 120;
                    page.IsPlaying = true;
                },
                Highlight = "pwave",
            },
            new("Narr_Quake_Inside_03") { Highlight = "swave" },
            new("Narr_Quake_Inside_04")
            {
                Enter = () =>
                {
                    page.WaveTime = 330;
                    page.IsPlaying = false;
                },
                Highlight = "pcurve",
            },
            new("Narr_Quake_Inside_05")
            {
                // The S front has just met the core.
                Enter = () =>
                {
                    page.WaveTime = 700;
                    page.IsPlaying = false;
                },
                Highlight = "gutenberg",
            },
            new("Narr_Quake_Inside_06")
            {
                Choices = ["Quake_Lesson_Inside_Choice1", "Quake_Lesson_Inside_Choice2", "Quake_Lesson_Inside_Choice3"],
                Answer = 0,
            },
            new("Narr_Quake_Inside_07")
            {
                Enter = () => page.WaveTime = 1320,
                Highlight = "shadows",
            },
            new("Narr_Quake_Inside_08") { Highlight = "shadowp" },
            new("Narr_Quake_Inside_09") { Highlight = "innercore" },
            new("Narr_Quake_Inside_10") { Highlight = "asthenosphere" },
            new("Narr_Quake_Inside_11")
            {
                Enter = () =>
                {
                    page.FromFamousCommand.Execute("Tohoku2011");
                    atTask = (page.StationDistance, page.QuakeAngle);
                },
                Highlight = "station",
                TryIt = () => page.StationDistance != atTask.Station || page.QuakeAngle != atTask.Angle,
            },
        ];
    }
}
