using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Lightning;

/// <summary>
/// The lightning page's spoken walkthroughs, one per view. The bench's hand keeps repeating the part of its round being
/// talked about, the storm flashes when asked to, and the strike is held at the moment the words are about, so the
/// picture matches the words. Highlight ids are <see cref="LightningDiagram"/>'s.
/// </summary>
internal static class LightningLessons
{
    public static IReadOnlyList<LessonStep> For(LightningViewModel page) => page.Mode switch
    {
        LightningMode.Cloud => Cloud(page),
        LightningMode.Strike => Strike(page),
        _ => Static(page),
    };

    private static void Labels(LightningViewModel page)
    {
        page.SpeedChoice.Selected = "1";
        page.ShowLabels = true;
    }

    /// <summary>The demonstrating hand shows one part of its round, over and over.</summary>
    private static void Show(LightningViewModel page, HandScene scene)
    {
        page.AutoHand = true;
        page.HandScene = scene;
        page.IsPlaying = true;
    }

    /// <summary>Holds the strike at a moment of its story.</summary>
    private static void Hold(LightningViewModel page, double story)
    {
        page.StrikeTime = story;
        page.IsPlaying = false;
    }

    private static IReadOnlyList<LessonStep> Static(LightningViewModel page)
    {
        // The task: other materials, other air, or the rod in your own hand.
        var atTask = (Rod: Material.Balloon, Cloth: Material.Hair, Humidity: 0.0);
        return
        [
            new("Narr_Lightning_Static_01")
            {
                Enter = () =>
                {
                    page.Mode = LightningMode.Static;
                    Labels(page);
                    page.RodChoice.Selected = nameof(Material.Balloon);
                    page.ClothChoice.Selected = nameof(Material.Hair);
                    page.Humidity = 30;
                    page.DischargeCommand.Execute(null);
                    Show(page, HandScene.Rub);
                },
                Highlight = "rod",
            },
            new("Narr_Lightning_Static_02") { Highlight = "cloth" },
            new("Narr_Lightning_Static_03")
            {
                Choices = ["Lightning_Lesson_Static_Choice1", "Lightning_Lesson_Static_Choice2", "Lightning_Lesson_Static_Choice3"],
                Answer = 0,
            },
            new("Narr_Lightning_Static_04") { Highlight = "rod" },
            new("Narr_Lightning_Static_05")
            {
                Enter = () => Show(page, HandScene.Paper),
                Highlight = "paper",
            },
            new("Narr_Lightning_Static_06")
            {
                Enter = () => Show(page, HandScene.Stream),
                Highlight = "water",
            },
            new("Narr_Lightning_Static_07")
            {
                Enter = () => Show(page, HandScene.Ball),
                Highlight = "ball",
            },
            new("Narr_Lightning_Static_08")
            {
                Enter = () =>
                {
                    page.SetHumidityCommand.Execute("85");
                    Show(page, HandScene.Rub);
                },
                Highlight = "humidity",
            },
            new("Narr_Lightning_Static_09")
            {
                Enter = () =>
                {
                    page.SetHumidityCommand.Execute("30");
                    Show(page, HandScene.Round);
                    atTask = (page.Rod, page.Cloth, page.Humidity);
                },
                TryIt = () => page.Rod != atTask.Rod || page.Cloth != atTask.Cloth || Math.Abs(page.Humidity - atTask.Humidity) >= 5 || !page.AutoHand,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Cloud(LightningViewModel page)
    {
        // The task: a storm of another height, or the meter somewhere else.
        var atTask = (Top: 0.0, Meter: 0.0);
        return
        [
            new("Narr_Lightning_Cloud_01")
            {
                Enter = () =>
                {
                    page.Mode = LightningMode.Cloud;
                    Labels(page);
                    page.ShowCharges = true;
                    page.SetTopCommand.Execute("12");
                    page.MeterX = 2;
                    page.IsPlaying = true;
                },
                Highlight = "top",
            },
            new("Narr_Lightning_Cloud_02") { Highlight = "updraft" },
            new("Narr_Lightning_Cloud_03") { Highlight = "reversal" },
            new("Narr_Lightning_Cloud_04") { Highlight = "main" },
            new("Narr_Lightning_Cloud_05")
            {
                Choices = ["Lightning_Lesson_Cloud_Choice1", "Lightning_Lesson_Cloud_Choice2", "Lightning_Lesson_Cloud_Choice3"],
                Answer = 0,
            },
            new("Narr_Lightning_Cloud_06") { Highlight = "meter" },
            new("Narr_Lightning_Cloud_07")
            {
                Enter = () => page.FlashRequest = FlashKind.CloudToGround,
                Highlight = "main",
            },
            new("Narr_Lightning_Cloud_08")
            {
                Enter = () => page.SetTopCommand.Execute("16"),
                Highlight = "top",
            },
            new("Narr_Lightning_Cloud_09")
            {
                Enter = () => atTask = (page.CloudTop, page.MeterX),
                TryIt = () => Math.Abs(page.CloudTop - atTask.Top) >= 0.5 || Math.Abs(page.MeterX - atTask.Meter) >= 1,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Strike(LightningViewModel page)
    {
        // The task: another current, rod, distance or strike.
        var atTask = (Current: 0.0, Rod: true, Distance: 0.0, Seed: 0);
        return
        [
            new("Narr_Lightning_Strike_01")
            {
                Enter = () =>
                {
                    page.Mode = LightningMode.Strike;
                    Labels(page);
                    page.SetCurrentCommand.Execute("30");
                    page.LightningRod = true;
                    page.ShowSphere = true;
                    page.Distance = 3;
                    page.StrokeChoice.Selected = "3";
                    page.StrikeSeed = 7;
                    Hold(page, 1.5);
                },
                Highlight = "cloudbase",
            },
            new("Narr_Lightning_Strike_02")
            {
                Enter = () => Hold(page, 3.5),
                Highlight = "leader",
            },
            new("Narr_Lightning_Strike_03")
            {
                Enter = () => Hold(page, 5.7),
                Choices = ["Lightning_Lesson_Strike_Choice1", "Lightning_Lesson_Strike_Choice2", "Lightning_Lesson_Strike_Choice3"],
                Answer = 0,
            },
            new("Narr_Lightning_Strike_04")
            {
                Enter = () => Hold(page, 6.45),
                Highlight = "sphere",
            },
            new("Narr_Lightning_Strike_05")
            {
                Enter = () => Hold(page, 7.4),
                Highlight = "stroke",
            },
            new("Narr_Lightning_Strike_06")
            {
                Enter = () =>
                {
                    page.LightningRod = false;
                    Hold(page, 7.4);
                },
                Highlight = "hit",
            },
            new("Narr_Lightning_Strike_07")
            {
                Enter = () =>
                {
                    page.LightningRod = true;
                    Hold(page, 6.45);
                },
                Highlight = "zone",
            },
            new("Narr_Lightning_Strike_08")
            {
                Enter = () => Hold(page, 9.8),
                Highlight = "dart",
            },
            new("Narr_Lightning_Strike_09")
            {
                Enter = () =>
                {
                    page.StrikeTime = StrikeTimeline.FlashStory(page.Strokes);
                    page.IsPlaying = true;
                },
                Highlight = "strip",
            },
            new("Narr_Lightning_Strike_10")
            {
                Enter = () => atTask = (page.PeakCurrent, page.LightningRod, page.Distance, page.StrikeSeed),
                TryIt = () => Math.Abs(page.PeakCurrent - atTask.Current) >= 3 || page.LightningRod != atTask.Rod ||
                              Math.Abs(page.Distance - atTask.Distance) >= 0.5 || page.StrikeSeed != atTask.Seed,
            },
        ];
    }
}
