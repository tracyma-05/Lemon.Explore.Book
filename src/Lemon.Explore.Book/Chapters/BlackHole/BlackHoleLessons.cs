using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.BlackHole;

/// <summary>
/// The black-hole page's spoken walkthroughs, one per view. Each sets the tilt, the probe or the hole's place so the
/// picture matches the words, and the last step hands over to the viewer. Highlight ids are <see cref="BlackHoleDiagram"/>'s.
/// </summary>
internal static class BlackHoleLessons
{
    public static IReadOnlyList<LessonStep> For(BlackHoleViewModel page) => page.Mode switch
    {
        BlackHoleMode.Orbits => Orbits(page),
        BlackHoleMode.Lensing => Lensing(page),
        _ => Shadow(page),
    };

    /// <summary>The view set up with labels, playing.</summary>
    private static void Setup(BlackHoleViewModel page, BlackHoleMode mode)
    {
        page.Mode = mode;
        page.ShowLabels = true;
        page.IsPlaying = true;
    }

    private static IReadOnlyList<LessonStep> Shadow(BlackHoleViewModel page)
    {
        // The task: the view tilted by hand.
        var atTask = 0.0;
        return
        [
            new("Narr_Hole_Shadow_01")
            {
                Enter = () =>
                {
                    Setup(page, BlackHoleMode.Shadow);
                    page.Inclination = 80;
                    page.BendLight = true;
                    page.ShowDisk = true;
                    page.Doppler = true;
                    page.ShowStars = true;
                },
                Highlight = "shadow",
            },
            new("Narr_Hole_Shadow_02") { Highlight = "horizon" },
            new("Narr_Hole_Shadow_03") { Highlight = "photonring" },
            new("Narr_Hole_Shadow_04") { Highlight = "hat" },
            new("Narr_Hole_Shadow_05") { Highlight = "below" },
            new("Narr_Hole_Shadow_06")
            {
                Choices = ["Hole_Lesson_Shadow_Choice1", "Hole_Lesson_Shadow_Choice2", "Hole_Lesson_Shadow_Choice3"],
                Answer = 0,
            },
            new("Narr_Hole_Shadow_07") { Highlight = "coming" },
            new("Narr_Hole_Shadow_08")
            {
                Enter = () => page.BendLight = false,
                Highlight = "shadow",
            },
            new("Narr_Hole_Shadow_09")
            {
                Enter = () =>
                {
                    page.BendLight = true;
                    page.TiltTo("Above");
                },
                Highlight = "inner",
            },
            new("Narr_Hole_Shadow_10")
            {
                Enter = () => atTask = page.Inclination,
                Highlight = "shadow",
                TryIt = () => Math.Abs(page.Inclination - atTask) > 15,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Orbits(BlackHoleViewModel page) =>
    [
        new("Narr_Hole_Orbits_01")
        {
            Enter = () =>
            {
                Setup(page, BlackHoleMode.Orbits);
                page.ShowRays = true;
                page.ShowNewton = true;
                page.ChooseLaunch(ProbeLaunch.Circle);
            },
            Highlight = "horizon",
        },
        new("Narr_Hole_Orbits_02") { Highlight = "bent" },
        new("Narr_Hole_Orbits_03") { Highlight = "caught" },
        new("Narr_Hole_Orbits_04") { Highlight = "critical" },
        new("Narr_Hole_Orbits_05") { Highlight = "probe" },
        new("Narr_Hole_Orbits_06")
        {
            Enter = () => page.ChooseLaunch(ProbeLaunch.Rosette),
            Highlight = "trail",
        },
        new("Narr_Hole_Orbits_07")
        {
            Highlight = "clocks",
            Choices = ["Hole_Lesson_Orbits_Choice1", "Hole_Lesson_Orbits_Choice2", "Hole_Lesson_Orbits_Choice3"],
            Answer = 0,
        },
        new("Narr_Hole_Orbits_08")
        {
            Enter = () => page.ChooseLaunch(ProbeLaunch.Plunge),
            Highlight = "newton",
        },
        new("Narr_Hole_Orbits_09") { Highlight = "clocks" },
        new("Narr_Hole_Orbits_10")
        {
            Enter = () => page.ChooseLaunch(ProbeLaunch.Isco),
            Highlight = "isco",
        },
        new("Narr_Hole_Orbits_11")
        {
            Enter = () => page.ChooseLaunch(ProbeLaunch.Circle),
            Highlight = "arrow",
            TryIt = () => page.Launch == ProbeLaunch.Custom,
        },
    ];

    private static IReadOnlyList<LessonStep> Lensing(BlackHoleViewModel page)
    {
        // The task: the hole moved by hand.
        var atTask = (X: 0.0, Y: 0.0);
        return
        [
            new("Narr_Hole_Lensing_01")
            {
                Enter = () =>
                {
                    Setup(page, BlackHoleMode.Lensing);
                    page.LensSource = LensSource.Galaxy;
                    page.LensOn = true;
                    page.ShowRing = true;
                    page.ShowTruePosition = true;
                    page.MassSlider = 0;
                    page.LensX = -1.6;
                    page.LensY = BlackHoleDiagram.TrackY;
                },
                Highlight = "lens",
            },
            new("Narr_Hole_Lensing_02") { Highlight = "true" },
            new("Narr_Hole_Lensing_03")
            {
                Enter = () =>
                {
                    page.IsPlaying = false;
                    page.LensX = -0.42;
                    page.LensY = BlackHoleDiagram.TrackY;
                },
                Highlight = "arc",
            },
            new("Narr_Hole_Lensing_04")
            {
                Enter = page.LineUp,
                Highlight = "ring",
            },
            new("Narr_Hole_Lensing_05") { Highlight = "sideview" },
            new("Narr_Hole_Lensing_06")
            {
                Enter = () =>
                {
                    page.LensSource = LensSource.Star;
                    page.IsPlaying = false;
                    page.LensX = 0.3;
                    page.LensY = BlackHoleDiagram.TrackY;
                },
                Highlight = "image1",
            },
            new("Narr_Hole_Lensing_07")
            {
                Choices = ["Hole_Lesson_Lensing_Choice1", "Hole_Lesson_Lensing_Choice2", "Hole_Lesson_Lensing_Choice3"],
                Answer = 0,
            },
            new("Narr_Hole_Lensing_08")
            {
                Enter = () => page.MassSlider = 1,
                Highlight = "ring",
            },
            new("Narr_Hole_Lensing_09")
            {
                Enter = () =>
                {
                    page.MassSlider = 0;
                    page.LensId = LensExample.StellarHole.Id;
                    page.LensX = -1.6;
                    page.LensY = BlackHoleDiagram.TrackY;
                    page.IsPlaying = true;
                },
                Highlight = "track",
            },
            new("Narr_Hole_Lensing_10")
            {
                Enter = () =>
                {
                    page.LensSource = LensSource.Grid;
                    page.IsPlaying = false;
                    atTask = (page.LensX, page.LensY);
                },
                Highlight = "lens",
                TryIt = () => Math.Abs(page.LensX - atTask.X) + Math.Abs(page.LensY - atTask.Y) > 0.2,
            },
        ];
    }
}
