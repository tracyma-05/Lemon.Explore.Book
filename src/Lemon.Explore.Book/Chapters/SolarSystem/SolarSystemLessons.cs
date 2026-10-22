using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.SolarSystem;

/// <summary>
/// The Solar System chapter's spoken tour, from the Sun out to Neptune, then to true distances and a visit to Earth.
/// Highlight ids are bodies' <see cref="CelestialBody.Id"/>s, or <c>belt</c> (see <see cref="SolarSystemCanvas.Highlight"/>).
/// </summary>
internal static class SolarSystemLessons
{
    public static IReadOnlyList<LessonStep> Tour(SolarSystemViewModel page)
    {
        // The task: fly to a body other than the one chosen when the task starts.
        CelestialBody? chosenAtTask = null;
        return
        [
            new("Narr_SolarSystem_Tour_01")
            {
                Enter = () =>
                {
                    page.IsRealScale = false;
                    page.ShowOrbits = true;
                    page.ShowLabels = true;
                    page.IsReversed = false;
                    page.SpeedIndex = 2;
                    page.IsPlaying = true;
                    page.ResetView();
                },
                Highlight = "Sun",
            },
            new("Narr_SolarSystem_Tour_02") { Highlight = "Earth" },
            new("Narr_SolarSystem_Tour_03") { Highlight = "belt" },
            new("Narr_SolarSystem_Tour_04") { Highlight = "Jupiter" },
            new("Narr_SolarSystem_Tour_05") { Highlight = "Saturn" },
            new("Narr_SolarSystem_Tour_06") { Highlight = "Neptune" },
            new("Narr_SolarSystem_Tour_07")
            {
                Choices = ["SolarSystem_Lesson_Tour_Choice1", "SolarSystem_Lesson_Tour_Choice2", "SolarSystem_Lesson_Tour_Choice3"],
                Answer = 0,
            },
            new("Narr_SolarSystem_Tour_08")
            {
                Enter = () => page.IsRealScale = true,
                Highlight = "Neptune",
            },
            new("Narr_SolarSystem_Tour_09")
            {
                Enter = () =>
                {
                    page.IsRealScale = false;
                    page.SelectedBody = SolarSystemCatalog.Bodies.First(b => b.Id == "Earth");
                },
                Highlight = "Moon",
            },
            new("Narr_SolarSystem_Tour_10")
            {
                Enter = () => chosenAtTask = page.SelectedBody,
                TryIt = () => page.SelectedBody is not null && page.SelectedBody != chosenAtTask,
            },
        ];
    }
}
