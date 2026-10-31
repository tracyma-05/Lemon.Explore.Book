using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Lemon.Book.Shell.Controls;
using Lemon.Book.Shell.Explore.Lessons;
using Lemon.Book.Shell.Infrastructures.Localization;
using Lemon.Explore.Book.Chapters.PeriodicTable;
using Xunit;
using static Lemon.Explore.Book.Tests.ChapterTestKit;

namespace Lemon.Explore.Book.Tests;

public sealed class PeriodicTableChapterTests
{
    /// <summary>Every target id the diagram registers that has a card, over its two views.</summary>
    private static readonly string[] TargetIds =
    [
        "nucleus", "proton", "neutron", "electron", "shell", "tile", "massnumber", "charge", "stability",
        .. AtomDiagram.Trays.SelectMany(t => new[] { t.Tray, t.Add, t.Remove }),
        "periods", "groups", "fblock", "detail", "minishells", "outer",
        .. Enum.GetValues<ElementCategory>().Select(c => "cat_" + c),
        .. Enum.GetValues<ElementState>().Select(s => "state_" + s),
    ];

    /// <summary>Everything a walkthrough may point at: the targets with cards, and the elements' cells.</summary>
    private static HashSet<string> Known() =>
        [.. TargetIds, .. ElementData.Elements.Select(e => AtomDiagram.CellId(e.Number))];

    [Fact]
    public void EveryClickableThing_HasATitleAndAnExplanation_InBothLanguages()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var id in TargetIds)
            {
                Assert.Contains(AtomDiagram.ObjectTitleKey(id)!, keys);
                Assert.Contains(AtomDiagram.ObjectClipKey(id)!, keys);
            }
        }
    }

    [Fact]
    public void EveryElement_HasANameInBothLanguages_AndACell()
    {
        foreach (var keys in BothLanguages())
        {
            foreach (var element in ElementData.Elements) Assert.Contains(element.NameKey, keys);
        }

        foreach (var element in ElementData.Elements)
        {
            Assert.Equal(element.Number, AtomDiagram.CellNumber(AtomDiagram.CellId(element.Number)));
            Assert.Null(AtomDiagram.ObjectClipKey(AtomDiagram.CellId(element.Number)));
        }
    }

    [Fact]
    public void TheElements_AreInOrder_WithTheirElectronsInShells_AndOneCellEach()
    {
        Assert.Equal(118, ElementData.Elements.Count);
        Assert.Equal(Enumerable.Range(1, 118), ElementData.Elements.Select(e => e.Number));
        Assert.Equal(118, ElementData.Elements.Select(e => e.Symbol).Distinct().Count());
        Assert.Equal(118, ElementData.Elements.Select(e => e.Cell).Distinct().Count());
        foreach (var element in ElementData.Elements)
        {
            // A neutral atom has as many electrons as protons; its shells are its period, but for palladium (2, 8, 18, 18).
            Assert.Equal(element.Number, element.Shells.Sum());
            Assert.Equal(element.Symbol == "Pd" ? 4 : element.Period, element.Shells.Count);
            Assert.InRange(element.Cell.Column, 1, 18);
        }

        // Each period ends with a noble gas and (after the first) starts with an alkali metal.
        foreach (var number in new[] { 2, 10, 18, 36, 54, 86, 118 }) Assert.Equal(18, ElementData.Of(number).Group);
        foreach (var number in new[] { 3, 11, 19, 37, 55, 87 }) Assert.Equal(ElementCategory.AlkaliMetal, ElementData.Of(number).Category);

        // The lanthanides and actinides: two rows of fifteen under the table.
        Assert.Equal(15, ElementData.Elements.Count(e => e.Category == ElementCategory.Lanthanide && e.Cell.Row == 9));
        Assert.Equal(15, ElementData.Elements.Count(e => e.Category == ElementCategory.Actinide && e.Cell.Row == 10));

        // Eleven gases, two liquids at room temperature.
        Assert.Equal(11, ElementData.Elements.Count(e => e.State == ElementState.Gas));
        Assert.Equal(["Br", "Hg"], ElementData.Elements.Where(e => e.State == ElementState.Liquid).Select(e => e.Symbol));
    }

    [Fact]
    public void Atoms_HaveTheRightShellsCharge_AndStability()
    {
        Assert.Equal([2, 8, 1], new AtomBuild(11, 12, 11).Shells);

        // Na⁺ has neon's shells; Cl⁻ argon's.
        Assert.Equal([2, 8], new AtomBuild(11, 12, 10).Shells);
        Assert.Equal(1, new AtomBuild(11, 12, 10).Charge);
        Assert.Equal([2, 8, 8], new AtomBuild(17, 18, 18).Shells);
        Assert.Equal(-1, new AtomBuild(17, 18, 18).Charge);

        Assert.True(AtomModel.IsStable(6, 6));
        Assert.True(AtomModel.IsStable(6, 7));
        Assert.False(AtomModel.IsStable(6, 8));
        Assert.False(AtomModel.IsStable(19, 21));
        Assert.False(AtomModel.IsStable(0, 1));
        Assert.True(AtomModel.IsStable(26, 30));

        // Each stable nucleus up to krypton is one the builder can make.
        foreach (var (z, neutrons) in ElementData.StableNeutrons)
        {
            Assert.InRange(z, 1, AtomModel.MaxProtons);
            Assert.All(neutrons, n => Assert.InRange(n, 0, AtomModel.MaxNeutrons));
        }

        Assert.Equal("Nothing", AtomModel.StateName(new AtomBuild(0, 0, 0)));
        Assert.Equal("NoProton", AtomModel.StateName(new AtomBuild(0, 1, 0)));
        Assert.Equal("Neutral_Stable", AtomModel.StateName(new AtomBuild(6, 6, 6)));
        Assert.Equal("Neutral_Unstable", AtomModel.StateName(new AtomBuild(6, 8, 6)));
        Assert.Equal("Positive_Stable", AtomModel.StateName(new AtomBuild(11, 12, 10)));
        Assert.Equal("Negative_Stable", AtomModel.StateName(new AtomBuild(17, 18, 18)));
    }

    [AvaloniaFact]
    public void TheAtom_StaysWithinWhatAnAtomCanHold()
    {
        using var page = new AtomViewModel(new FakeNarration());
        page.SetAtom(8, 8, 8);
        page.AddElectronCommand.Execute(null);
        page.AddElectronCommand.Execute(null);
        page.AddElectronCommand.Execute(null);
        page.AddElectronCommand.Execute(null);
        Assert.Equal(8 + AtomModel.MaxExtraElectrons, page.Electrons);

        // Fewer protons take the extra electrons with them.
        page.Protons = 2;
        Assert.Equal(2 + AtomModel.MaxExtraElectrons, page.Electrons);
        page.Protons = 99;
        Assert.Equal(AtomModel.MaxProtons, page.Protons);
        page.Neutrons = -3;
        Assert.Equal(0, page.Neutrons);

        page.PickNucleusCommand.Execute(new Nucleus(26, 30));
        Assert.Equal(new AtomBuild(26, 30, 26), page.Build);
    }

    [AvaloniaTheory]
    [InlineData(AtomMode.Build)]
    [InlineData(AtomMode.Table)]
    public void Walkthroughs_UseKeysInBothLanguages_AndRunToTheEnd(AtomMode mode)
    {
        var narration = new FakeNarration();
        using var page = new AtomViewModel(narration) { Mode = mode };
        var used = WalkthroughKeys([AtomLessons.For(page)]);
        foreach (var keys in BothLanguages())
        {
            Assert.Empty(used.Except(keys));
        }

        // Each view's task (as the walkthrough asks it): a neutral neon-20 built, gold picked.
        RunToEnd(page.Lesson, narration, Known(), () => Assert.Equal(mode, page.Mode), () =>
        {
            if (mode == AtomMode.Build) page.SetAtom(10, 10, 10);
            else page.ElementZ = 79;
        });
    }

    [AvaloniaFact]
    public void BuildSteps_ShowWhatTheySay()
    {
        var narration = new FakeNarration();
        using var page = new AtomViewModel(narration);
        var seen = new Dictionary<string, AtomBuild>();
        page.Lesson.GapSeconds = 0;
        page.Lesson.Start();
        for (var guard = 0; guard < 100 && page.Lesson.State != LessonState.Done; guard++)
        {
            if (narration.CurrentKey is { } key) seen[key] = page.Build;
            if (page.Lesson.State == LessonState.Quiz) page.Lesson.AnswerCommand.Execute(page.Lesson.Choices[0]);
            else if (page.Lesson.State == LessonState.TryIt) page.SetAtom(10, 10, 10);
            narration.Finish();
            page.Lesson.Tick(0.1);
        }

        // Hydrogen; helium; carbon asked about, oxygen shown; sodium's lone third-shell electron, then given away;
        // chloride; carbon-14 radioactive with carbon-12's six electrons.
        Assert.Equal("H", seen["Narr_Atom_Build_01"].Element!.Symbol);
        Assert.Equal("He", seen["Narr_Atom_Build_03"].Element!.Symbol);
        Assert.Equal("C", seen["Narr_Atom_Build_04"].Element!.Symbol);
        Assert.Equal("O", seen["Narr_Atom_Build_05"].Element!.Symbol);
        Assert.Equal([2, 8, 1], seen["Narr_Atom_Build_06"].Shells);
        Assert.Equal(1, seen["Narr_Atom_Build_07"].Charge);
        Assert.Equal(-1, seen["Narr_Atom_Build_08"].Charge);
        Assert.Equal(8, seen["Narr_Atom_Build_08"].Shells[^1]);
        Assert.False(seen["Narr_Atom_Build_09"].IsStable);
        Assert.Equal(14, seen["Narr_Atom_Build_09"].MassNumber);
        Assert.Equal(6, seen["Narr_Atom_Build_10"].Electrons);
    }

    [AvaloniaFact]
    public void TableSteps_ShowWhatTheySay()
    {
        var narration = new FakeNarration();
        using var page = new AtomViewModel(narration) { Mode = AtomMode.Table };
        var seen = new Dictionary<string, (Element Element, TableColouring Colouring)>();
        page.Lesson.GapSeconds = 0;
        page.Lesson.Start();
        for (var guard = 0; guard < 100 && page.Lesson.State != LessonState.Done; guard++)
        {
            if (narration.CurrentKey is { } key) seen[key] = (page.Element, page.Colouring);
            if (page.Lesson.State == LessonState.Quiz) page.Lesson.AnswerCommand.Execute(page.Lesson.Choices[0]);
            else if (page.Lesson.State == LessonState.TryIt) page.ElementZ = 79;
            narration.Finish();
            page.Lesson.Tick(0.1);
        }

        Assert.Equal(1, seen["Narr_Atom_Table_01"].Element.Number);
        Assert.Equal(3, seen["Narr_Atom_Table_02"].Element.Period);
        Assert.Equal(TableColouring.Outer, seen["Narr_Atom_Table_03"].Colouring);
        Assert.Equal(1, seen["Narr_Atom_Table_03"].Element.Outer);
        Assert.Equal(ElementCategory.AlkaliMetal, seen["Narr_Atom_Table_04"].Element.Category);

        // Chlorine asked about: fluorine, above it, has its seven.
        Assert.Equal(7, seen["Narr_Atom_Table_05"].Element.Outer);
        Assert.Equal(ElementData.Of(9).Group, seen["Narr_Atom_Table_05"].Element.Group);
        Assert.Equal(7, ElementData.Of(9).Outer);
        Assert.Equal(ElementCategory.NobleGas, seen["Narr_Atom_Table_06"].Element.Category);
        Assert.Equal(ElementCategory.Transition, seen["Narr_Atom_Table_07"].Element.Category);
        Assert.Equal(ElementState.Liquid, seen["Narr_Atom_Table_08"].Element.State);
        Assert.True(seen["Narr_Atom_Table_09"].Element.IsFBlock);
        Assert.Equal(118, seen["Narr_Atom_Table_10"].Element.Number);
    }

    [AvaloniaTheory]
    [InlineData("en")]
    [InlineData("zh-CN")]
    public void TheCard_HasItsWords_InEachView(string culture)
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo(culture));
        try
        {
            using var page = new AtomViewModel(new FakeNarration());
            void Check()
            {
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseTitle));
                Assert.False(string.IsNullOrWhiteSpace(page.PhaseDescription));
                Assert.DoesNotContain("Atom_", page.PhaseTitle + page.PhaseDescription, StringComparison.Ordinal);
                foreach (var stat in page.Stats) Assert.DoesNotContain("Atom_", stat.Name + stat.Value, StringComparison.Ordinal);
                foreach (var fact in page.Facts) Assert.DoesNotContain("Atom_", fact, StringComparison.Ordinal);
                Assert.DoesNotContain("Atom_", page.Subtitle + page.ScaleNote + page.Hint + page.ChartCaption + page.ChartNote, StringComparison.Ordinal);
            }

            page.Mode = AtomMode.Build;
            var states = new HashSet<string>();
            for (var p = 0; p <= AtomModel.MaxProtons; p++)
            {
                foreach (var extra in new[] { -1, 0, 1, 2 })
                {
                    foreach (var n in new[] { 0, p, p + 2 })
                    {
                        page.SetAtom(p, n, Math.Max(0, p + extra));
                        Check();
                        states.Add(AtomModel.StateName(page.Build));
                    }
                }
            }

            Assert.Equal(8, states.Count);

            page.Mode = AtomMode.Table;
            foreach (var element in ElementData.Elements)
            {
                page.ElementZ = element.Number;
                Check();
            }

            Assert.Equal(118, page.ElementOptions.Count);
            Assert.DoesNotContain(page.ElementOptions, option => option.Label.Contains("Atom_", StringComparison.Ordinal));
        }
        finally
        {
            localization.SetCulture(original);
        }
    }

    [AvaloniaTheory]
    [InlineData("en", 11, 12, 10, "Sodium ion (Na+)")]
    [InlineData("en", 17, 18, 18, "Chloride ion (Cl−)")]
    [InlineData("en", 6, 8, 6, "Carbon-14")]
    [InlineData("en", 8, 8, 10, "Oxide ion (O2−)")]
    [InlineData("zh-CN", 11, 12, 10, "钠离子（Na+）")]
    [InlineData("zh-CN", 6, 8, 6, "碳-14")]
    public void TheCardTitle_NamesTheAtomOrIon(string culture, int protons, int neutrons, int electrons, string expected)
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo(culture));
        try
        {
            using var page = new AtomViewModel(new FakeNarration());
            Assert.Equal(expected, page.BuildTitle(new AtomBuild(protons, neutrons, electrons)));
        }
        finally
        {
            localization.SetCulture(original);
        }
    }

    [AvaloniaFact]
    public void TheSpread_Renders_InEachView()
    {
        var localization = LocalizationService.Instance;
        var original = localization.CurrentCulture;
        localization.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
        try
        {
            var (window, model) = OpenAt("Periodic-Table");
            var atoms = ((PeriodicTableTextPageViewModel)model.Pages[TextPageOf("Periodic-Table")]).Atoms;
            atoms.SetAtom(11, 12, 11);
            Frames(20);
            Snapshot(window, "atoms-build-zh");

            atoms.SetAtom(17, 18, 18);
            Frames(10);
            Snapshot(window, "atoms-build-ion-zh");

            atoms.Mode = AtomMode.Table;
            atoms.ElementZ = 26;
            Frames(20);
            Snapshot(window, "atoms-table-zh");

            atoms.Colouring = TableColouring.Outer;
            atoms.ElementZ = 17;
            Frames(10);
            Snapshot(window, "atoms-table-outer-zh");

            localization.SetCulture(CultureInfo.GetCultureInfo("en"));
            atoms.Colouring = TableColouring.State;
            Frames(10);
            Snapshot(window, "atoms-table-state-en");

            atoms.Lesson.Start();
            window.FindDescendantOfType<BookView>()!.Next();
            Frames(3);
            Assert.False(atoms.Lesson.IsActive);
            window.Close();
        }
        finally
        {
            localization.SetCulture(original);
        }
    }
}
