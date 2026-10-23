using Lemon.Explore.Book.Chapters.AcidsBases;
using Xunit;

namespace Lemon.Explore.Book.Tests;

/// <summary>The acids and bases page's chemistry against textbook values.</summary>
public sealed class AcidBaseModelTests
{
    [Fact]
    public void PureWater_IsNeutral()
    {
        Assert.Equal(7.0, AcidBaseModel.Ph(Solution.Water), 3);
        Assert.Equal("Neutral", AcidBaseModel.Nature(AcidBaseModel.Ph(Solution.Water)));
    }

    [Theory]
    [InlineData(1e-1, 1.00)]
    [InlineData(1e-3, 3.00)]
    [InlineData(1e-6, 6.00)]

    // Very dilute: water's own ions matter, and an acid never makes water basic.
    [InlineData(1e-8, 6.98)]
    public void StrongAcid_GivesUpEveryProton(double molar, double expected)
    {
        Assert.Equal(expected, AcidBaseModel.Ph(new Solution(molar, 0, 0, 0)), 2);
    }

    [Fact]
    public void StrongBase_AndWeakOnes_MatchTheTextbook()
    {
        Assert.Equal(11.00, AcidBaseModel.Ph(new Solution(0, 0, 1e-3, 0)), 2);

        // 0.1 mol/L acetic acid: pH 2.88; 0.1 mol/L ammonia: pH 11.12 (11.13 by the usual square-root shortcut).
        Assert.Equal(2.88, AcidBaseModel.Ph(new Solution(0, 0.1, 0, 0)), 2);
        Assert.Equal(11.12, AcidBaseModel.Ph(new Solution(0, 0, 0, 0.1)), 2);
    }

    [Fact]
    public void Species_KeepTheWaterProductAndTheChargeBalance()
    {
        var random = new Random(3);
        for (var i = 0; i < 200; i++)
        {
            double Amount() => random.NextDouble() < 0.3 ? 0 : Math.Pow(10, -1 - (random.NextDouble() * 7));
            var s = new Solution(Amount(), Amount(), Amount(), Amount());
            var sp = AcidBaseModel.SpeciesOf(s);
            Assert.Equal(-14, Math.Log10(sp.Hydronium * sp.Hydroxide), 6);
            var plus = sp.Hydronium + sp.Sodium + sp.BaseIons;
            var minus = sp.Hydroxide + sp.Chloride + sp.AcidIons;
            Assert.True(Math.Abs(plus - minus) <= 1e-9 * Math.Max(plus, 1e-12), $"{s}: {plus} vs {minus}");
            Assert.True(sp.AcidMolecules >= -1e-15 && sp.BaseMolecules >= -1e-15);
        }
    }

    [Fact]
    public void WeakAcid_IsMostlyWholeMolecules()
    {
        var sp = AcidBaseModel.SpeciesOf(new Solution(0, 1e-3, 0, 0));

        // About one in eight lets go at 0.001 mol/L.
        Assert.InRange(sp.AcidIons / 1e-3, 0.10, 0.14);
        Assert.InRange(sp.Ph, 3.85, 3.95);
    }

    [Fact]
    public void Beaker_DropsAndWatering()
    {
        var beaker = AcidBaseModel.Beaker.Water().Add(Reagent.StrongAcid, 2 * AcidBaseModel.DropMl);
        Assert.Equal(4.00, AcidBaseModel.Ph(beaker.Solution), 2);
        beaker = AcidBaseModel.Beaker.Water().Add(Reagent.StrongAcid, 20 * AcidBaseModel.DropMl);
        Assert.Equal(3.00, AcidBaseModel.Ph(beaker.Solution), 2);
        Assert.Equal(4.00, AcidBaseModel.Ph(beaker.DilutedTenTimes().Solution), 2);

        // Twenty drops of base, then twenty of acid: neutral.
        beaker = AcidBaseModel.Beaker.Water().Add(Reagent.StrongBase, 1).Add(Reagent.StrongAcid, 1);
        Assert.Equal(7.0, AcidBaseModel.Ph(beaker.Solution), 3);
    }

    [Fact]
    public void StrongTitration_MatchesTheTextbookCurve()
    {
        Assert.Equal(1.00, AcidBaseModel.TitrationPh(TitrationAcid.Strong, 0), 2);
        Assert.Equal(1.60, AcidBaseModel.TitrationPh(TitrationAcid.Strong, 15), 2);
        Assert.Equal(2.69, AcidBaseModel.TitrationPh(TitrationAcid.Strong, 24), 2);
        Assert.Equal(7.00, AcidBaseModel.TitrationPh(TitrationAcid.Strong, 25), 2);
        Assert.Equal(11.29, AcidBaseModel.TitrationPh(TitrationAcid.Strong, 26), 2);
        Assert.Equal(12.52, AcidBaseModel.TitrationPh(TitrationAcid.Strong, 50), 2);

        var last = 0.0;
        for (var ml = 0.0; ml <= 50; ml += 0.05)
        {
            var ph = AcidBaseModel.TitrationPh(TitrationAcid.Weak, ml);
            Assert.True(ph >= last - 1e-9);
            last = ph;
        }
    }

    [Fact]
    public void WeakTitration_HalfwayIsPka_AndEndsBasic()
    {
        Assert.Equal(2.88, AcidBaseModel.TitrationPh(TitrationAcid.Weak, 0), 2);
        Assert.Equal(4.76, AcidBaseModel.TitrationPh(TitrationAcid.Weak, 12.5), 2);
        Assert.InRange(AcidBaseModel.TitrationPh(TitrationAcid.Weak, 25), 8.65, 8.8);
        Assert.Equal("Half", AcidBaseModel.TitrationStage(TitrationAcid.Weak, 12.5));
        Assert.Equal("Before", AcidBaseModel.TitrationStage(TitrationAcid.Strong, 12.5));
        Assert.Equal("Equivalence", AcidBaseModel.TitrationStage(TitrationAcid.Weak, 25.2));
        Assert.Equal("Excess", AcidBaseModel.TitrationStage(TitrationAcid.Weak, 26));
        Assert.Equal("Start", AcidBaseModel.TitrationStage(TitrationAcid.Weak, 0));
    }

    [Fact]
    public void Neutralising_WarmsTheFlask()
    {
        // 2.5 mmol × 57.1 kJ/mol spread through 50 mL: 0.68 °C; ten times stronger, 6.8 °C.
        Assert.Equal(0.68, AcidBaseModel.TemperatureRise(TitrationAcid.Strong, 25), 2);
        Assert.Equal(6.83, AcidBaseModel.TemperatureRise(TitrationAcid.Strong, 25, 1), 2);
        Assert.True(AcidBaseModel.TemperatureRise(TitrationAcid.Strong, 40) < AcidBaseModel.TemperatureRise(TitrationAcid.Strong, 25));
        Assert.Equal(0, AcidBaseModel.TemperatureRise(TitrationAcid.Strong, 0));
    }

    [Fact]
    public void Substances_HaveTheirPh_InOrder()
    {
        var last = -1.0;
        foreach (var s in AcidBaseModel.Substances)
        {
            Assert.Equal(s.Ph, AcidBaseModel.Ph(s.Recipe), 2);
            Assert.True(s.Ph > last, s.Id);
            last = s.Ph;
        }
    }

    [Fact]
    public void WateringDown_MovesTowardsSeven()
    {
        var stomach = AcidBaseModel.Substance("Stomach");
        Assert.Equal(2.5, AcidBaseModel.DilutedPh(stomach, 1), 1);
        Assert.InRange(AcidBaseModel.DilutedPh(stomach, 6), 6.8, 7.0);
        Assert.InRange(AcidBaseModel.DilutedPh(AcidBaseModel.Substance("Drain"), 6), 7.0, 7.6);

        // A buffer hardly moves; a weak acid moves half a step.
        Assert.True(Math.Abs(AcidBaseModel.DilutedPh(AcidBaseModel.Substance("Blood"), 1) - 7.4) < 0.1);
        Assert.InRange(AcidBaseModel.DilutedPh(AcidBaseModel.Substance("Vinegar"), 1) - 2.9, 0.4, 0.6);
    }

    [Fact]
    public void Indicators_ShowTheirTextbookColours()
    {
        Assert.Equal(0, AcidBaseModel.IndicatorColor(Indicator.Phenolphthalein, 7).A);
        Assert.True(AcidBaseModel.IndicatorColor(Indicator.Phenolphthalein, 10).A > 200);
        var green = AcidBaseModel.IndicatorColor(Indicator.Bromothymol, 7);
        Assert.True(green.G > green.R && green.G > green.B);
        var red = AcidBaseModel.IndicatorColor(Indicator.Litmus, 3);
        var blue = AcidBaseModel.IndicatorColor(Indicator.Litmus, 10);
        Assert.True(red.R > red.B && blue.B > blue.R);
        var yellow = AcidBaseModel.IndicatorColor(Indicator.MethylOrange, 6);
        Assert.True(yellow.R > 200 && yellow.G > 150 && yellow.B < 80);
        Assert.Equal(AcidBaseModel.Clear, AcidBaseModel.IndicatorColor(Indicator.None, 3));
    }

    [Fact]
    public void Magnifier_DrawsTheDissolvedParticlesInProportion()
    {
        var (water, _) = ParticleBox.CountsFor(AcidBaseModel.SpeciesOf(Solution.Water));
        Assert.Equal(0, water[ParticleKind.Hydronium]);
        Assert.Equal(0, water[ParticleKind.Hydroxide]);

        var (acid, unit) = ParticleBox.CountsFor(AcidBaseModel.SpeciesOf(new Solution(1e-3, 0, 0, 0)));
        Assert.Equal(ParticleBox.MostDrawn, acid[ParticleKind.Hydronium]);
        Assert.Equal(ParticleBox.MostDrawn, acid[ParticleKind.Chloride]);
        Assert.Equal(0, acid[ParticleKind.Hydroxide]);
        Assert.Equal(1e-3 / ParticleBox.MostDrawn, unit, 8);
    }

    [Fact]
    public void Scientific_UsesSuperscripts()
    {
        Assert.Equal("1.0 × 10⁻⁷", AcidBaseDiagram.Scientific(1e-7));
        Assert.Equal("2.5 × 10⁻³", AcidBaseDiagram.Scientific(2.5e-3));
        Assert.Equal("1.0 × 10⁻¹⁴", AcidBaseDiagram.Scientific(1.0000001e-14));
        Assert.True(AcidBaseModel.Substances.All(s => s.NameKey.StartsWith("Acid_Sub_", StringComparison.Ordinal)));
    }
}
