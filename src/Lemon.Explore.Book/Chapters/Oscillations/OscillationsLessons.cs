using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Oscillations;

/// <summary>
/// The pendulum and spring page's spoken walkthroughs, one per view. Each sets up the experiment it talks about (the
/// length, the mass, the place) and lets go again, so the picture matches the words. Highlight ids are
/// <see cref="OscillatorDiagram"/>'s.
/// </summary>
internal static class OscillationsLessons
{
    public static IReadOnlyList<LessonStep> For(OscillationsViewModel page) => page.Mode switch
    {
        OscillationMode.Spring => Spring(page),
        OscillationMode.Wave => Wave(page),
        _ => Pendulum(page),
    };

    /// <summary>Back to plain conditions on Earth, at real speed.</summary>
    private static void OnEarth(OscillationsViewModel page)
    {
        page.Place = OscillatorPlace.Earth;
        page.SpeedChoice.Selected = "1";
        page.AirDrag = false;
        page.ShowForces = false;
    }

    private static IReadOnlyList<LessonStep> Pendulum(OscillationsViewModel page)
    {
        // The task: drag the bob aside and let go (which sets a new amplitude).
        var amplitudeAtTask = 0.0;
        return
        [
            new("Narr_Osc_Pendulum_01")
            {
                Enter = () =>
                {
                    page.Mode = OscillationMode.Pendulum;
                    OnEarth(page);
                    page.ShowFormula = false;
                    page.Length = 0.25;
                    page.Amplitude = 20;
                    page.ReleaseCommand.Execute(null);
                },
                Highlight = "bob",
            },
            new("Narr_Osc_Pendulum_02") { Highlight = "trace" },
            new("Narr_Osc_Pendulum_03") { Highlight = "length" },
            new("Narr_Osc_Pendulum_04")
            {
                Choices = ["Osc_Lesson_Pendulum_Choice1", "Osc_Lesson_Pendulum_Choice2", "Osc_Lesson_Pendulum_Choice3"],
                Answer = 0,
            },
            new("Narr_Osc_Pendulum_05")
            {
                Enter = () =>
                {
                    page.Length = 1;
                    page.ReleaseCommand.Execute(null);
                },
                Highlight = "length",
            },
            new("Narr_Osc_Pendulum_06")
            {
                Enter = () =>
                {
                    page.Amplitude = 8;
                    page.ReleaseCommand.Execute(null);
                },
                Highlight = "angle",
            },
            new("Narr_Osc_Pendulum_07")
            {
                Enter = () =>
                {
                    page.Place = OscillatorPlace.Moon;
                    page.Amplitude = 20;
                    page.ReleaseCommand.Execute(null);
                },
                Highlight = "place",
            },
            new("Narr_Osc_Pendulum_08")
            {
                Enter = () => amplitudeAtTask = page.Amplitude,
                Highlight = "bob",
                TryIt = () => Math.Abs(page.Amplitude - amplitudeAtTask) > 2,
            },
            new("Narr_Osc_Pendulum_09")
            {
                Enter = () =>
                {
                    page.Place = OscillatorPlace.Earth;
                    page.ReleaseCommand.Execute(null);
                },
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Spring(OscillationsViewModel page)
    {
        // The task: drag the mass up or down and let go (which sets a new pull).
        var pullAtTask = 0.0;
        return
        [
            new("Narr_Osc_Spring_01")
            {
                Enter = () =>
                {
                    page.Mode = OscillationMode.Spring;
                    OnEarth(page);
                    page.Shake = false;
                    page.Mass = 0.25;
                    page.Stiffness = 20;
                    page.Pull = 0.12;
                    page.ReleaseCommand.Execute(null);
                },
                Highlight = "mass",
            },
            new("Narr_Osc_Spring_02") { Highlight = "rest" },
            new("Narr_Osc_Spring_03") { Highlight = "circle" },
            new("Narr_Osc_Spring_04")
            {
                Choices = ["Osc_Lesson_Spring_Choice1", "Osc_Lesson_Spring_Choice2", "Osc_Lesson_Spring_Choice3"],
                Answer = 0,
            },
            new("Narr_Osc_Spring_05")
            {
                Enter = () =>
                {
                    page.Mass = 1;
                    page.ReleaseCommand.Execute(null);
                },
                Highlight = "trace",
            },
            new("Narr_Osc_Spring_06")
            {
                Enter = () =>
                {
                    page.Place = OscillatorPlace.Moon;
                    page.ReleaseCommand.Execute(null);
                },
                Highlight = "rest",
            },
            new("Narr_Osc_Spring_07")
            {
                Enter = () =>
                {
                    page.Place = OscillatorPlace.Earth;
                    page.Mass = 0.25;
                    page.DriveFrequency = Math.Round(page.NaturalFrequency, 2);
                    page.Shake = true;
                    page.Pull = 0.01;
                    page.ReleaseCommand.Execute(null);
                },
                Highlight = "motor",
            },
            new("Narr_Osc_Spring_08")
            {
                Enter = () =>
                {
                    page.Shake = false;
                    page.Pull = 0.12;
                    page.ReleaseCommand.Execute(null);
                    pullAtTask = page.Pull;
                },
                Highlight = "mass",
                TryIt = () => Math.Abs(page.Pull - pullAtTask) > 0.01,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Wave(OscillationsViewModel page)
    {
        // The task: move along the minute to another pattern.
        var timeAtTask = 0.0;
        return
        [
            new("Narr_Osc_Wave_01")
            {
                Enter = () =>
                {
                    page.Mode = OscillationMode.Wave;
                    OnEarth(page);
                    page.WaveTime = 0;
                    page.IsPlaying = true;
                },
                Highlight = "longest",
            },
            new("Narr_Osc_Wave_02") { Highlight = "shortest" },
            new("Narr_Osc_Wave_03") { Highlight = "snake" },
            new("Narr_Osc_Wave_04")
            {
                Choices = ["Osc_Lesson_Wave_Choice1", "Osc_Lesson_Wave_Choice2", "Osc_Lesson_Wave_Choice3"],
                Answer = 0,
            },
            new("Narr_Osc_Wave_05")
            {
                Enter = () =>
                {
                    page.WaveTime = page.WaveCycle - 3;
                    page.IsPlaying = true;
                },
                Highlight = "topview",
            },
            new("Narr_Osc_Wave_06")
            {
                Enter = () => page.JumpToCommand.Execute("0.5"),
                Highlight = "snake",
            },
            new("Narr_Osc_Wave_07")
            {
                Enter = () => timeAtTask = page.WaveTime,
                TryIt = () => Math.Abs(page.WaveTime - timeAtTask) > 3,
            },
        ];
    }
}
