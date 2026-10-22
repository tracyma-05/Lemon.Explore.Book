using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Expansion;

/// <summary>
/// The expanding-universe page's spoken walkthroughs, one per view. Each sets the patch, the light or the moment so the
/// picture matches the words, and the last step hands over to the viewer. Highlight ids are <see cref="ExpansionDiagram"/>'s.
/// </summary>
internal static class ExpansionLessons
{
    public static IReadOnlyList<LessonStep> For(ExpansionViewModel page) => page.Mode switch
    {
        ExpansionMode.Redshift => Redshift(page),
        ExpansionMode.History => History(page),
        _ => Hubble(page),
    };

    /// <summary>The view set up with labels, playing.</summary>
    private static void Setup(ExpansionViewModel page, ExpansionMode mode)
    {
        page.Mode = mode;
        page.ShowLabels = true;
        page.IsPlaying = true;
    }

    private static IReadOnlyList<LessonStep> Hubble(ExpansionViewModel page)
    {
        // The task: home moved to another galaxy, or time run by hand.
        var atTask = (Home: GalaxyCell.MilkyWay, Progress: 0.0);
        return
        [
            new("Narr_Expand_Hubble_01")
            {
                Enter = () =>
                {
                    Setup(page, ExpansionMode.Hubble);
                    page.Home = GalaxyCell.MilkyWay;
                    page.ShowArrows = true;
                    page.ShowRulers = true;
                    page.ShowOverlay = false;
                    page.ShowGrid = false;
                    page.HubbleProgress = 0;
                },
                Highlight = "home",
            },
            new("Narr_Expand_Hubble_02") { Highlight = "galaxy" },
            new("Narr_Expand_Hubble_03") { Highlight = "ruler" },
            new("Narr_Expand_Hubble_04") { Highlight = "law" },
            new("Narr_Expand_Hubble_05")
            {
                Choices = ["Expand_Lesson_Hubble_Choice1", "Expand_Lesson_Hubble_Choice2", "Expand_Lesson_Hubble_Choice3"],
                Answer = 0,
            },
            new("Narr_Expand_Hubble_06")
            {
                Enter = () =>
                {
                    page.ShowOverlay = true;
                    page.IsPlaying = false;
                    page.HubbleProgress = 1;
                },
                Highlight = "ghost",
            },
            new("Narr_Expand_Hubble_07")
            {
                Enter = () =>
                {
                    page.ShowOverlay = false;
                    page.Home = new GalaxyCell(-2, 1);
                    page.HubbleProgress = 0;
                    page.IsPlaying = true;
                },
                Highlight = "home",
            },
            new("Narr_Expand_Hubble_08")
            {
                Enter = () =>
                {
                    page.Home = GalaxyCell.MilkyWay;
                    page.ShowGrid = true;
                    page.ShowArrows = false;
                    page.HubbleProgress = 0;
                    page.IsPlaying = true;
                },
                Highlight = "grid",
            },
            new("Narr_Expand_Hubble_09")
            {
                Enter = () => page.ShowArrows = true,
                Highlight = "clock",
            },
            new("Narr_Expand_Hubble_10")
            {
                Enter = () =>
                {
                    page.ShowGrid = false;
                    atTask = (page.Home, page.HubbleProgress);
                },
                Highlight = "galaxy",
                TryIt = () => page.Home != atTask.Home || (!page.IsPlaying && Math.Abs(page.HubbleProgress - atTask.Progress) > 0.05),
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Redshift(ExpansionViewModel page)
    {
        // The task: another galaxy or another redshift picked.
        var atTask = 0.0;
        return
        [
            new("Narr_Expand_Redshift_01")
            {
                Enter = () =>
                {
                    Setup(page, ExpansionMode.Redshift);
                    page.ChooseSource("Halfway");
                    page.Journey = 0;
                },
                Highlight = "source",
            },
            new("Narr_Expand_Redshift_02") { Highlight = "light" },
            new("Narr_Expand_Redshift_03") { Highlight = "space" },
            new("Narr_Expand_Redshift_04") { Highlight = "distnow" },
            new("Narr_Expand_Redshift_05")
            {
                Enter = () =>
                {
                    page.IsPlaying = false;
                    page.Journey = 1;
                },
                Highlight = "arriving",
            },
            new("Narr_Expand_Redshift_06") { Highlight = "line" },
            new("Narr_Expand_Redshift_07")
            {
                Choices = ["Expand_Lesson_Redshift_Choice1", "Expand_Lesson_Redshift_Choice2", "Expand_Lesson_Redshift_Choice3"],
                Answer = 0,
            },
            new("Narr_Expand_Redshift_08")
            {
                Enter = () =>
                {
                    page.ChooseSource("Jades");
                    page.IsPlaying = false;
                    page.Journey = 1;
                },
                Highlight = "jwst",
            },
            new("Narr_Expand_Redshift_09") { Highlight = "distnow" },
            new("Narr_Expand_Redshift_10")
            {
                Enter = () =>
                {
                    page.ChooseSource("Quasar");
                    atTask = page.Redshift;
                },
                Highlight = "source",
                TryIt = () => Math.Abs(page.Redshift - atTask) > 1e-9,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> History(ExpansionViewModel page)
    {
        // The task: the moment moved or another universe picked.
        var atTask = (Time: 0.0, Kind: UniverseKind.Ours);
        double TimeOf(string id) => CosmicEvent.All.First(e => e.Id == id).TimeIn(Universe.Ours) ?? Universe.Ours.Age;
        return
        [
            new("Narr_Expand_History_01")
            {
                Enter = () =>
                {
                    Setup(page, ExpansionMode.History);
                    page.UniverseKind = UniverseKind.Ours;
                    page.Axis = TimeAxis.Linear;
                    page.IsPlaying = false;
                    page.CosmicTime = Universe.Ours.Age;
                },
                Highlight = "size",
            },
            new("Narr_Expand_History_02")
            {
                Enter = () => page.JumpTo("Start"),
                Highlight = "bigbang",
            },
            new("Narr_Expand_History_03")
            {
                Enter = () =>
                {
                    page.Axis = TimeAxis.Log;
                    page.CosmicTime = TimeOf("Nuclei");
                },
                Highlight = "nuclei",
            },
            new("Narr_Expand_History_04")
            {
                Enter = () => page.CosmicTime = TimeOf("Cmb") * 0.8,
                Highlight = "patch",
            },
            new("Narr_Expand_History_05")
            {
                Enter = () => page.CosmicTime = TimeOf("Cmb") * 1.5,
                Highlight = "cmb",
            },
            new("Narr_Expand_History_06")
            {
                Enter = () =>
                {
                    page.Axis = TimeAxis.Linear;
                    page.CosmicTime = 0.6;
                },
                Highlight = "firststars",
            },
            new("Narr_Expand_History_07")
            {
                Enter = () => page.CosmicTime = TimeOf("Speedup") + 1,
                Highlight = "speedup",
            },
            new("Narr_Expand_History_08")
            {
                Choices = ["Expand_Lesson_History_Choice1", "Expand_Lesson_History_Choice2", "Expand_Lesson_History_Choice3"],
                Answer = 0,
            },
            new("Narr_Expand_History_09")
            {
                Enter = () =>
                {
                    page.UniverseKind = UniverseKind.Closed;
                    page.CosmicTime = (Universe.Closed.CrunchTime ?? 28) - 0.15;
                },
                Highlight = "crunch",
            },
            new("Narr_Expand_History_10")
            {
                Enter = () =>
                {
                    page.UniverseKind = UniverseKind.Ours;
                    page.CosmicTime = 26;
                },
                Highlight = "patch",
            },
            new("Narr_Expand_History_11")
            {
                Enter = () =>
                {
                    page.CosmicTime = Universe.Ours.Age;
                    atTask = (page.CosmicTime, page.UniverseKind);
                },
                Highlight = "cursor",
                TryIt = () => Math.Abs(page.CosmicTime - atTask.Time) > 0.01 || page.UniverseKind != atTask.Kind,
            },
        ];
    }
}
