using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Sound;

/// <summary>
/// The sound page's spoken walkthroughs, one per view. Each sets the tone, the string or pipe, or the instrument and key,
/// so the picture matches the words; they make no sound themselves (a pluck or note set up by a step is silent), and the
/// last step hands over to the viewer. Highlight ids are <see cref="SoundDiagram"/>'s.
/// </summary>
internal static class SoundLessons
{
    public static IReadOnlyList<LessonStep> For(SoundViewModel page) => page.Mode switch
    {
        SoundMode.Strings => Strings(page),
        SoundMode.Timbre => Timbre(page),
        _ => Wave(page),
    };

    /// <summary>The view set up with labels at normal speed.</summary>
    private static void Setup(SoundViewModel page, SoundMode mode)
    {
        page.Mode = mode;
        page.SpeedChoice.Selected = "1";
        page.ShowLabels = true;
        page.IsPlaying = true;
    }

    private static IReadOnlyList<LessonStep> Wave(SoundViewModel page)
    {
        // The task: another frequency or level, or the tone switched on or off.
        var atTask = (Frequency: 440.0, Loudness: 60.0, Tone: false);
        return
        [
            new("Narr_Sound_Wave_01")
            {
                Enter = () =>
                {
                    Setup(page, SoundMode.Wave);
                    page.Medium = SoundMedium.Air;
                    page.Temperature = 20;
                    page.Loudness = 70;
                    page.Frequency = 220;
                },
                Highlight = "speaker",
            },
            new("Narr_Sound_Wave_02") { Highlight = "particle" },
            new("Narr_Sound_Wave_03") { Highlight = "compression" },
            new("Narr_Sound_Wave_04") { Highlight = "rarefaction" },
            new("Narr_Sound_Wave_05")
            {
                Choices = ["Sound_Lesson_Wave_Choice1", "Sound_Lesson_Wave_Choice2", "Sound_Lesson_Wave_Choice3"],
                Answer = 0,
            },
            new("Narr_Sound_Wave_06")
            {
                Enter = () => page.Frequency = 440,
                Highlight = "wavelength",
            },
            new("Narr_Sound_Wave_07") { Highlight = "pressure" },
            new("Narr_Sound_Wave_08")
            {
                Enter = () => page.Medium = SoundMedium.Vacuum,
                Highlight = "speaker",
            },
            new("Narr_Sound_Wave_09")
            {
                Enter = () => page.Medium = SoundMedium.Water,
                Highlight = "medium",
            },
            new("Narr_Sound_Wave_10")
            {
                Enter = () =>
                {
                    page.Medium = SoundMedium.Air;
                    atTask = (page.Frequency, page.Loudness, page.ToneOn);
                },
                Highlight = "ear",
                TryIt = () => Math.Abs(Math.Log(page.Frequency / atTask.Frequency)) > 0.1 || Math.Abs(page.Loudness - atTask.Loudness) >= 6 || page.ToneOn != atTask.Tone,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Strings(SoundViewModel page)
    {
        // The task: a pluck, a harmonic alone, or another fret.
        var atTask = (Plucks: 0, Fret: 0);
        return
        [
            new("Narr_Sound_Strings_01")
            {
                Enter = () =>
                {
                    Setup(page, SoundMode.Strings);
                    page.Resonator = Resonator.String;
                    page.ShowHarmonics = true;
                    page.TensionScale = 1;
                    page.StringIndex = 1;
                    page.Fret = 0;
                    page.PluckQuietly(0.2);
                },
                Highlight = "string",
            },
            new("Narr_Sound_Strings_02") { Highlight = "harmonic1" },
            new("Narr_Sound_Strings_03") { Highlight = "harmonic3" },
            new("Narr_Sound_Strings_04") { Highlight = "node" },
            new("Narr_Sound_Strings_05")
            {
                Choices = ["Sound_Lesson_Strings_Choice1", "Sound_Lesson_Strings_Choice2", "Sound_Lesson_Strings_Choice3"],
                Answer = 0,
            },
            new("Narr_Sound_Strings_06")
            {
                Enter = () => page.PluckQuietly(0.5),
                Highlight = "harmonic2",
            },
            new("Narr_Sound_Strings_07")
            {
                Enter = () =>
                {
                    page.Fret = 12;
                    page.PluckQuietly(0.2);
                },
                Highlight = "fret",
            },
            new("Narr_Sound_Strings_08")
            {
                Enter = () =>
                {
                    page.Fret = 0;
                    page.PipeMode = 1;
                    page.Resonator = Resonator.OpenPipe;
                },
                Highlight = "pantinode",
            },
            new("Narr_Sound_Strings_09")
            {
                Enter = () => page.Resonator = Resonator.ClosedPipe,
                Highlight = "closedend",
            },
            new("Narr_Sound_Strings_10")
            {
                Enter = () =>
                {
                    page.Resonator = Resonator.String;
                    page.PluckQuietly(0.2);
                    atTask = (page.PluckCount, page.Fret);
                },
                Highlight = "string",
                TryIt = () => page.PluckCount != atTask.Plucks || page.SoloHarmonic > 0 || page.Fret != atTask.Fret,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Timbre(SoundViewModel page)
    {
        // The task: a key played or another instrument.
        var atTask = (Notes: 0, Instrument: Instrument.Fork);
        return
        [
            new("Narr_Sound_Timbre_01")
            {
                Enter = () =>
                {
                    Setup(page, SoundMode.Timbre);
                    page.SecondNote = SecondNote.None;
                    page.Instrument = Instrument.Fork;
                    page.NoteMidi = 69;
                },
                Highlight = "keyboard",
            },
            new("Narr_Sound_Timbre_02") { Highlight = "octave" },
            new("Narr_Sound_Timbre_03") { Highlight = "waveform" },
            new("Narr_Sound_Timbre_04") { Highlight = "period" },
            new("Narr_Sound_Timbre_05")
            {
                Enter = () => page.Instrument = Instrument.Violin,
                Highlight = "timbre",
            },
            new("Narr_Sound_Timbre_06") { Highlight = "overtone" },
            new("Narr_Sound_Timbre_07")
            {
                Enter = () => page.Instrument = Instrument.Piano,
                Highlight = "envelope",
            },
            new("Narr_Sound_Timbre_08")
            {
                Choices = ["Sound_Lesson_Timbre_Choice1", "Sound_Lesson_Timbre_Choice2", "Sound_Lesson_Timbre_Choice3"],
                Answer = 0,
            },
            new("Narr_Sound_Timbre_09")
            {
                Enter = () =>
                {
                    page.Instrument = Instrument.Fork;
                    page.Detune = 3;
                    page.SecondNote = SecondNote.Beats;
                },
                Highlight = "beat",
            },
            new("Narr_Sound_Timbre_10")
            {
                Enter = () =>
                {
                    page.SecondNote = SecondNote.None;
                    atTask = (page.NoteCount, page.Instrument);
                },
                Highlight = "keyboard",
                TryIt = () => page.NoteCount != atTask.Notes || page.Instrument != atTask.Instrument,
            },
        ];
    }
}
