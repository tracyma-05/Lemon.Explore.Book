using Lemon.Book.Shell.Explore.Lessons;

namespace Lemon.Explore.Book.Chapters.PeriodicTable;

/// <summary>
/// The atoms page's spoken walkthroughs, one per view. Each sets the atom or the element picked so the picture matches
/// the words (a question is asked before the picture shows its answer), and the last step hands over to the viewer.
/// Highlight ids are <see cref="AtomDiagram"/>'s.
/// </summary>
internal static class AtomLessons
{
    public static IReadOnlyList<LessonStep> For(AtomViewModel page) => page.Mode switch
    {
        AtomMode.Table => Table(page),
        _ => Build(page),
    };

    private static IReadOnlyList<LessonStep> Build(AtomViewModel page) =>
    [
        // Hydrogen: one proton, one electron.
        new("Narr_Atom_Build_01")
        {
            Enter = () =>
            {
                page.Mode = AtomMode.Build;
                page.ShowLabels = true;
                page.SetAtom(1, 0, 1);
            },
            Highlight = "nucleus",
        },
        new("Narr_Atom_Build_02") { Highlight = "proton" },

        // A second proton (and two neutrons): helium.
        new("Narr_Atom_Build_03")
        {
            Enter = () => page.SetAtom(2, 2, 2),
            Highlight = "tile",
        },
        new("Narr_Atom_Build_04")
        {
            Enter = () => page.SetAtom(6, 6, 6),
            Choices = ["Atom_Lesson_Build_Choice1", "Atom_Lesson_Build_Choice2", "Atom_Lesson_Build_Choice3"],
            Answer = 0,
        },

        // Two protons more: oxygen.
        new("Narr_Atom_Build_05")
        {
            Enter = () => page.SetAtom(8, 8, 8),
            Highlight = "tile",
        },

        // Sodium: 2, 8 and one lone electron in a third shell.
        new("Narr_Atom_Build_06")
        {
            Enter = () => page.SetAtom(11, 12, 11),
            Highlight = "shell",
        },

        // That electron given away: Na⁺.
        new("Narr_Atom_Build_07")
        {
            Enter = () => page.SetAtom(11, 12, 10),
            Highlight = "charge",
        },

        // Chlorine takes one: Cl⁻ with argon's full shells.
        new("Narr_Atom_Build_08")
        {
            Enter = () => page.SetAtom(17, 18, 18),
            Highlight = "charge",
        },

        // Carbon-14: two neutrons more, radioactive.
        new("Narr_Atom_Build_09")
        {
            Enter = () => page.SetAtom(6, 8, 6),
            Highlight = "stability",
        },
        new("Narr_Atom_Build_10")
        {
            Choices = ["Atom_Lesson_Build2_Choice1", "Atom_Lesson_Build2_Choice2", "Atom_Lesson_Build2_Choice3"],
            Answer = 0,
        },
        new("Narr_Atom_Build_11")
        {
            Highlight = "bin_p",
            TryIt = () => page.Protons == 10 && page.Neutrons == 10 && page.Electrons == 10,
        },
    ];

    private static IReadOnlyList<LessonStep> Table(AtomViewModel page) =>
    [
        new("Narr_Atom_Table_01")
        {
            Enter = () =>
            {
                page.Mode = AtomMode.Table;
                page.ShowLabels = true;
                page.Colouring = TableColouring.Category;
                page.ElementZ = 1;
            },
            Highlight = "el_1",
        },
        new("Narr_Atom_Table_02")
        {
            Enter = () => page.ElementZ = 11,
            Highlight = "periods",
        },

        // Coloured by outer electrons, each main column is one colour.
        new("Narr_Atom_Table_03")
        {
            Enter = () =>
            {
                page.Colouring = TableColouring.Outer;
                page.ElementZ = 3;
            },
            Highlight = "groups",
        },
        new("Narr_Atom_Table_04")
        {
            Enter = () =>
            {
                page.Colouring = TableColouring.Category;
                page.ElementZ = 19;
            },
            Highlight = "cat_AlkaliMetal",
        },
        new("Narr_Atom_Table_05")
        {
            Enter = () => page.ElementZ = 17,
            Choices = ["Atom_Lesson_Table_Choice1", "Atom_Lesson_Table_Choice2", "Atom_Lesson_Table_Choice3"],
            Answer = 0,
        },
        new("Narr_Atom_Table_06")
        {
            Enter = () => page.ElementZ = 10,
            Highlight = "cat_NobleGas",
        },
        new("Narr_Atom_Table_07")
        {
            Enter = () => page.ElementZ = 26,
            Highlight = "cat_Transition",
        },
        new("Narr_Atom_Table_08")
        {
            Enter = () =>
            {
                page.Colouring = TableColouring.State;
                page.ElementZ = 35;
            },
            Highlight = "state_Liquid",
        },
        new("Narr_Atom_Table_09")
        {
            Enter = () =>
            {
                page.Colouring = TableColouring.Category;
                page.ElementZ = 92;
            },
            Highlight = "fblock",
        },
        new("Narr_Atom_Table_10")
        {
            Enter = () => page.ElementZ = 118,
            Highlight = "el_118",
        },
        new("Narr_Atom_Table_11")
        {
            Enter = () => page.ElementZ = 1,
            Highlight = "detail",
            TryIt = () => page.ElementZ == 79,
        },
    ];
}
