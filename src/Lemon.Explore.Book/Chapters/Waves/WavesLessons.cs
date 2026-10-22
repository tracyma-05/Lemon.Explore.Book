using Avalonia;
using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Waves;

/// <summary>
/// The waves page's spoken walkthroughs, one per view. Each sets up what it talks about (the dippers, the light, the
/// source's speed) so the picture matches the words. Highlight ids are <see cref="WavesDiagram"/>'s.
/// </summary>
internal static class WavesLessons
{
    public static IReadOnlyList<LessonStep> For(WavesViewModel page) => page.Mode switch
    {
        WaveMode.Slits => Slits(page),
        WaveMode.Doppler => Doppler(page),
        _ => Ripple(page),
    };

    /// <summary>Real pace, running.</summary>
    private static void Play(WavesViewModel page)
    {
        page.SpeedChoice.Selected = "1";
        page.IsPlaying = true;
    }

    /// <summary>How the two dippers' waves meet at the probe.</summary>
    internal static Meeting RippleMeeting(WavesViewModel page) =>
        WavePhysics.MeetingOf(WavePhysics.PhaseGap(WavePhysics.PathDifference(page.Probe, page.Separation), page.Wavelength, page.Opposite));

    /// <summary>How many wavelengths farther the slits probe is from one slit than the other.</summary>
    internal static double SlitWavelengths(WavesViewModel page)
    {
        var lambda = WavesDiagram.PictureWavelength(page.WhiteLight ? 550 : page.LightWavelength);
        var spacing = WavesDiagram.PictureSpacing(page.SlitSpacing);
        var probe = page.SlitProbe;
        return Math.Abs(((Vector)(probe - new Point(0, -spacing / 2))).Length - ((Vector)(probe - new Point(0, spacing / 2))).Length) / lambda;
    }

    private static IReadOnlyList<LessonStep> Ripple(WavesViewModel page) =>
    [
        new("Narr_Waves_Ripple_01")
        {
            Enter = () =>
            {
                page.Mode = WaveMode.Ripple;
                Play(page);
                page.Wavelength = 2.5;
                page.Separation = 6;
                page.Opposite = false;
                page.SecondSource = false;
                page.ShowLines = false;
                page.ShowAverage = false;
                page.RestartCommand.Execute(null);
            },
            Highlight = "dippera",
        },
        new("Narr_Waves_Ripple_02")
        {
            Enter = () => page.SecondSource = true,
            Highlight = "dipperb",
        },
        new("Narr_Waves_Ripple_03")
        {
            Enter = () => page.ShowAverage = true,
            Highlight = "tank",
        },
        new("Narr_Waves_Ripple_04")
        {
            Enter = () =>
            {
                page.ShowAverage = false;
                page.ShowLines = true;
                page.ProbeOnCommand.Execute("Quiet");
            },
            Highlight = "quiet",
        },
        new("Narr_Waves_Ripple_05")
        {
            Enter = () => page.ProbeOnCommand.Execute("Loud"),
            Highlight = "difference",
        },
        new("Narr_Waves_Ripple_06")
        {
            Choices = ["Waves_Lesson_Ripple_Choice1", "Waves_Lesson_Ripple_Choice2", "Waves_Lesson_Ripple_Choice3"],
            Answer = 0,
        },
        new("Narr_Waves_Ripple_07")
        {
            Enter = () => page.Opposite = true,
            Highlight = "quiet",
        },
        new("Narr_Waves_Ripple_08")
        {
            Enter = () =>
            {
                page.Opposite = false;
                page.Wavelength = 1.5;
            },
            Highlight = "quiet",
        },
        new("Narr_Waves_Ripple_09")
        {
            // The task: drag the probe onto still water.
            Enter = () => page.ProbeOnCommand.Execute("Loud"),
            Highlight = "probe",
            TryIt = () => page.SecondSource && RippleMeeting(page) == Meeting.OutOfStep,
        },
    ];

    private static IReadOnlyList<LessonStep> Slits(WavesViewModel page) =>
    [
        new("Narr_Waves_Slits_01")
        {
            Enter = () =>
            {
                page.Mode = WaveMode.Slits;
                Play(page);
                page.WhiteLight = false;
                page.OneSlit = false;
                page.SetLightCommand.Execute("532");
                page.SlitSpacing = 0.25;
                page.ScreenDistance = 2;
                page.ProbeOnStripeCommand.Execute("0");
                page.RestartCommand.Execute(null);
            },
            Highlight = "laser",
        },
        new("Narr_Waves_Slits_02") { Highlight = "slits" },
        new("Narr_Waves_Slits_03") { Highlight = "screen" },
        new("Narr_Waves_Slits_04")
        {
            Enter = () => page.ProbeOnStripeCommand.Execute("1"),
            Highlight = "difference",
        },
        new("Narr_Waves_Slits_05")
        {
            Enter = () => page.ProbeOnStripeCommand.Execute("0.5"),
            Highlight = "probe",
        },
        new("Narr_Waves_Slits_06")
        {
            Choices = ["Waves_Lesson_Slits_Choice1", "Waves_Lesson_Slits_Choice2", "Waves_Lesson_Slits_Choice3"],
            Answer = 0,
        },
        new("Narr_Waves_Slits_07")
        {
            Enter = () => page.OneSlit = true,
            Highlight = "tape",
        },
        new("Narr_Waves_Slits_08")
        {
            Enter = () =>
            {
                page.OneSlit = false;
                page.SetLightCommand.Execute("650");
            },
            Highlight = "screen",
        },
        new("Narr_Waves_Slits_09")
        {
            Enter = () => page.WhiteLight = true,
            Highlight = "screen",
        },
        new("Narr_Waves_Slits_10")
        {
            // The task: find a bright stripe other than the middle one.
            Enter = () =>
            {
                page.SetLightCommand.Execute("532");
                page.ProbeOnStripeCommand.Execute("0");
            },
            Highlight = "probe",
            TryIt = () => !page.OneSlit && SlitWavelengths(page) is var n && Math.Round(n) >= 1 &&
                          WavePhysics.MeetingOf(2 * Math.PI * n) == Meeting.InStep,
        },
    ];

    private static IReadOnlyList<LessonStep> Doppler(WavesViewModel page)
    {
        // The task: move the listener somewhere else.
        var listenerAtTask = default(Point);
        return
        [
            new("Narr_Waves_Doppler_01")
            {
                Enter = () =>
                {
                    page.Mode = WaveMode.Doppler;
                    Play(page);
                    page.Listener = new Point(60, 45);
                    page.SetMachCommand.Execute("0");
                },
                Highlight = "source",
            },
            new("Narr_Waves_Doppler_02")
            {
                Enter = () => page.SetMachCommand.Execute("0.5"),
                Highlight = "bunched",
            },
            new("Narr_Waves_Doppler_03") { Highlight = "listener" },
            new("Narr_Waves_Doppler_04")
            {
                Choices = ["Waves_Lesson_Doppler_Choice1", "Waves_Lesson_Doppler_Choice2", "Waves_Lesson_Doppler_Choice3"],
                Answer = 0,
            },
            new("Narr_Waves_Doppler_05") { Highlight = "heard" },
            new("Narr_Waves_Doppler_06")
            {
                Enter = () => page.SetMachCommand.Execute("1"),
                Highlight = "source",
            },
            new("Narr_Waves_Doppler_07")
            {
                Enter = () => page.SetMachCommand.Execute("2"),
                Highlight = "cone",
            },
            new("Narr_Waves_Doppler_08") { Highlight = "listener" },
            new("Narr_Waves_Doppler_09")
            {
                Enter = () =>
                {
                    page.SetMachCommand.Execute("0.5");
                    listenerAtTask = page.Listener;
                },
                Highlight = "listener",
                TryIt = () => ((Vector)(page.Listener - listenerAtTask)).Length > 5,
            },
        ];
    }
}
