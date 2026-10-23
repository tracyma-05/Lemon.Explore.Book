using Lemon.Explore.Book.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.Spectra;

/// <summary>
/// The spectra page's spoken walkthroughs, one per view. The atom's steps hold the electron up and drop it one level at
/// a time so the picture follows the words; the lamps' steps pick the line being talked about; the last step of each
/// hands over to the viewer. Highlight ids are <see cref="SpectraDiagram"/>'s.
/// </summary>
internal static class SpectraLessons
{
    public static IReadOnlyList<LessonStep> For(SpectraViewModel page) => page.Mode switch
    {
        SpectraMode.Lamps => Lamps(page),
        SpectraMode.Stars => Stars(page),
        _ => Atom(page),
    };

    /// <summary>The view set up with labels, playing.</summary>
    private static void Setup(SpectraViewModel page, SpectraMode mode)
    {
        page.Mode = mode;
        page.ShowLabels = true;
        page.IsPlaying = true;
    }

    private static IReadOnlyList<LessonStep> Atom(SpectraViewModel page)
    {
        // The task: a photon fired, or the electron lifted by a click.
        var atTask = 0;
        return
        [
            new("Narr_Spec_Atom_01")
            {
                Enter = () =>
                {
                    Setup(page, SpectraMode.Atom);
                    page.ShowCloud = false;
                    page.Hold = true;
                    page.FallMode = FallMode.OneStep;
                    page.Level = 1;
                    page.PhotonNm = 656.28;
                    page.ClearCommand.Execute(null);
                },
                Highlight = "nucleus",
            },
            new("Narr_Spec_Atom_02") { Highlight = "level1" },
            new("Narr_Spec_Atom_03")
            {
                Enter = () => page.Level = 3,
                Highlight = "level3",
            },
            new("Narr_Spec_Atom_04")
            {
                Enter = () => page.Level = 2,
                Highlight = "level2",
            },
            new("Narr_Spec_Atom_05")
            {
                Enter = () => page.Level = 1,
                Highlight = "uv",
            },
            new("Narr_Spec_Atom_06") { Highlight = "hspectrum" },
            new("Narr_Spec_Atom_07")
            {
                Enter = () => page.PhotonNm = 640,
                Choices = ["Spec_Lesson_Atom_Choice1", "Spec_Lesson_Atom_Choice2", "Spec_Lesson_Atom_Choice3"],
                Answer = 0,
            },
            new("Narr_Spec_Atom_08")
            {
                Enter = () => page.FireCommand.Execute(null),
                Highlight = "lamp",
            },
            new("Narr_Spec_Atom_09")
            {
                Enter = () =>
                {
                    page.Hold = false;
                    page.FallMode = FallMode.Chance;
                    page.PhotonNm = 80;
                    page.FireCommand.Execute(null);
                },
                Highlight = "continuum",
            },
            new("Narr_Spec_Atom_10")
            {
                Enter = () =>
                {
                    page.Hold = true;
                    page.CloudShape = 1;
                    page.ShowCloud = true;
                    page.Level = 2;
                },
                Highlight = "cloud",
            },
            new("Narr_Spec_Atom_11")
            {
                Enter = () =>
                {
                    page.ShowCloud = false;
                    page.Hold = false;
                    page.Level = 1;
                    page.PhotonNm = 121.57;
                    atTask = page.FireCount;
                },
                Highlight = "lamp",
                TryIt = () => page.FireCount != atTask || page.Level != 1,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Lamps(SpectraViewModel page)
    {
        // The task: a guess at the mystery lamp, or a gas to compare with.
        return
        [
            new("Narr_Spec_Lamps_01")
            {
                Enter = () =>
                {
                    Setup(page, SpectraMode.Lamps);
                    page.Mystery = false;
                    page.Source = LightSource.Lamp;
                    page.Gas = Gas.Hydrogen;
                    page.CompareGas = null;
                    page.Zoom = false;
                    page.SelectedLine = double.NaN;
                },
                Highlight = "gaslamp",
            },
            new("Narr_Spec_Lamps_02") { Highlight = "prism" },
            new("Narr_Spec_Lamps_03")
            {
                Enter = () => page.SelectedLine = 656.28,
                Highlight = "line",
            },
            new("Narr_Spec_Lamps_04")
            {
                Enter = () => page.Gas = Gas.Neon,
                Highlight = "gaslamp",
            },
            new("Narr_Spec_Lamps_05")
            {
                Enter = () =>
                {
                    page.Gas = Gas.Sodium;
                    page.SelectedLine = 589.3;
                    page.Zoom = true;
                },
                Highlight = "zoom",
            },
            new("Narr_Spec_Lamps_06")
            {
                Enter = () =>
                {
                    page.Zoom = false;
                    page.Source = LightSource.Bulb;
                },
                Highlight = "bulb",
            },
            new("Narr_Spec_Lamps_07")
            {
                Choices = ["Spec_Lesson_Lamps_Choice1", "Spec_Lesson_Lamps_Choice2", "Spec_Lesson_Lamps_Choice3"],
                Answer = 0,
            },
            new("Narr_Spec_Lamps_08")
            {
                Enter = () =>
                {
                    page.Source = LightSource.ThroughGas;
                    page.SelectedLine = 589.0;
                },
                Highlight = "coolgas",
            },
            new("Narr_Spec_Lamps_09")
            {
                Enter = () =>
                {
                    page.Source = LightSource.Sun;
                    page.SelectedLine = 589.0;
                },
                Highlight = "darkline",
            },
            new("Narr_Spec_Lamps_10")
            {
                Enter = () => page.StartMystery(Gas.Mercury),
                Highlight = "mystery",
                TryIt = () => page.GuessText.Length > 0 || page.CompareGas is not null,
            },
        ];
    }

    private static IReadOnlyList<LessonStep> Stars(SpectraViewModel page)
    {
        // The task: another temperature or another speed.
        var atTask = (Kelvin: 0.0, Velocity: 0.0);
        return
        [
            new("Narr_Spec_Stars_01")
            {
                Enter = () =>
                {
                    Setup(page, SpectraMode.Stars);
                    page.Kelvin = SpectraModel.SunKelvin;
                    page.Velocity = 0;
                },
                Highlight = "star",
            },
            new("Narr_Spec_Stars_02") { Highlight = "curve" },
            new("Narr_Spec_Stars_03")
            {
                Enter = () => page.Kelvin = 3_600,
                Highlight = "peak",
            },
            new("Narr_Spec_Stars_04")
            {
                Enter = () => page.Kelvin = 22_400,
                Highlight = "star",
            },
            new("Narr_Spec_Stars_05")
            {
                Choices = ["Spec_Lesson_Stars_Choice1", "Spec_Lesson_Stars_Choice2", "Spec_Lesson_Stars_Choice3"],
                Answer = 0,
            },
            new("Narr_Spec_Stars_06")
            {
                Enter = () => page.Kelvin = 9_940,
                Highlight = "balmer",
            },
            new("Narr_Spec_Stars_07")
            {
                Enter = () => page.Kelvin = SpectraModel.SunKelvin,
                Highlight = "calcium",
            },
            new("Narr_Spec_Stars_08")
            {
                Enter = () => page.Kelvin = 3_600,
                Highlight = "bands",
            },
            new("Narr_Spec_Stars_09")
            {
                Enter = () =>
                {
                    page.Kelvin = SpectraModel.SunKelvin;
                    page.Velocity = 15_000;
                },
                Highlight = "shift",
            },
            new("Narr_Spec_Stars_10")
            {
                Enter = () =>
                {
                    page.Velocity = 0;
                    atTask = (page.Kelvin, page.Velocity);
                },
                Highlight = "starspectrum",
                TryIt = () => page.Kelvin != atTask.Kelvin || page.Velocity != atTask.Velocity,
            },
        ];
    }
}
