using System;
using System.Linq;
using Lemon.Explore.Book.Chapters.Volcanoes;
using Xunit;

namespace Lemon.Explore.Book.Tests;

/// <summary>The volcanoes and rocks page's science: magma, eruption styles, the rock cycle's journeys, settling, burial and metamorphism.</summary>
public sealed class VolcanoModelTests
{
    [Fact]
    public void Viscosity_RisesWithSilica_ToTheTypicalValues()
    {
        // Basalt at 1200 °C about 10² Pa·s, andesite about 10⁴·³, rhyolite at 800 °C about 10⁷·⁵.
        Assert.Equal(1200, VolcanoModel.Temperature(50), 0);
        Assert.Equal(800, VolcanoModel.Temperature(72), 0);
        Assert.InRange(VolcanoModel.LogViscosity(50), 1.8, 2.2);
        Assert.InRange(VolcanoModel.LogViscosity(60), 4.0, 4.6);
        Assert.InRange(VolcanoModel.LogViscosity(72), 7.2, 7.8);
        for (var s = VolcanoModel.MinSilica; s < VolcanoModel.MaxSilica; s += 0.5)
        {
            Assert.True(VolcanoModel.LogViscosity(s + 0.5) > VolcanoModel.LogViscosity(s), $"{s}");
        }
    }

    [Fact]
    public void Water_ComesOutOfTheMeltAsItRises()
    {
        // Rhyolite holds about 5.8 % water at 200 MPa (about 8 km); 5 % starts to bubble near 6 km, 0.5 % basalt only near the top.
        Assert.InRange(VolcanoModel.Solubility(72, 200), 5.6, 6.0);
        Assert.InRange(VolcanoModel.Pressure(1), 25, 26.5);
        Assert.InRange(VolcanoModel.SaturationDepth(72, 5), 5.0, 6.5);
        Assert.InRange(VolcanoModel.SaturationDepth(50, 0.5), 0, 0.2);

        // Below the saturation depth no gas; above it more and more, nearly all of it at the vent.
        Assert.Equal(0, VolcanoModel.GasFraction(72, 5, 7));
        var previous = 0.0;
        for (var depth = 6.0; depth >= 0; depth -= 0.25)
        {
            var phi = VolcanoModel.GasFraction(72, 5, depth);
            Assert.True(phi >= previous, $"{depth}");
            previous = phi;
        }

        Assert.True(previous > 0.99);
    }

    [Fact]
    public void Froth_TearsApart_HundredsOfMetresDown()
    {
        var depth = VolcanoModel.FragmentationDepth(72, 5);
        Assert.NotNull(depth);
        Assert.InRange(depth!.Value, 0.2, 1.2);
        Assert.Equal(VolcanoModel.FragmentationFraction, VolcanoModel.GasFraction(72, 5, depth.Value), 3);

        // More water: deeper.
        Assert.True(VolcanoModel.FragmentationDepth(72, 6)!.Value > depth.Value);
    }

    [Theory]
    [InlineData("Kilauea", EruptionStyle.Hawaiian)]
    [InlineData("Wudalianchi", EruptionStyle.Strombolian)]
    [InlineData("Stromboli", EruptionStyle.Strombolian)]
    [InlineData("Merapi", EruptionStyle.Dome)]
    [InlineData("StHelens", EruptionStyle.Plinian)]
    [InlineData("Pinatubo", EruptionStyle.Plinian)]
    [InlineData("Changbaishan", EruptionStyle.Plinian)]
    public void RealVolcanoes_EruptInTheirOwnStyle(string id, EruptionStyle style)
    {
        var preset = VolcanoModel.Preset(id);
        Assert.Equal(style, VolcanoModel.StyleOf(preset.Silica, preset.Water));
    }

    [Fact]
    public void StickyGassyMagma_IsExplosive_RunnyIsNot()
    {
        Assert.Equal(EruptionStyle.Hawaiian, VolcanoModel.Style(2, 0.5));
        Assert.Equal(EruptionStyle.Dome, VolcanoModel.Style(6, 1));
        Assert.Equal(EruptionStyle.Vulcanian, VolcanoModel.Style(4.3, 3));
        Assert.Equal(EruptionStyle.Plinian, VolcanoModel.Style(7.5, 5));
        Assert.Equal(ConeShape.Shield, VolcanoModel.Shape(50, 0.5));
        Assert.Equal(ConeShape.Strato, VolcanoModel.Shape(51, 2));
        Assert.Equal(ConeShape.Strato, VolcanoModel.Shape(62, 3));
        Assert.Equal(ConeShape.Caldera, VolcanoModel.Shape(72, 5));
        Assert.Equal("Basalt", VolcanoModel.RockName(50));
        Assert.Equal("Andesite", VolcanoModel.RockName(60));
        Assert.Equal("Rhyolite", VolcanoModel.RockName(72));
    }

    [Fact]
    public void Columns_HaveTheirRealHeights()
    {
        // Mastin et al. 2009: 10⁴ m³/s about 18 km. St Helens 1980 about 24 km, Pinatubo 1991 about 35 km.
        Assert.InRange(VolcanoModel.ColumnHeightKm(1e4), 18, 19);
        var stHelens = VolcanoModel.Preset("StHelens");
        var pinatubo = VolcanoModel.Preset("Pinatubo");
        Assert.InRange(VolcanoModel.EruptionHeightKm(EruptionStyle.Plinian, stHelens.Water), 22, 28);
        Assert.InRange(VolcanoModel.EruptionHeightKm(EruptionStyle.Plinian, pinatubo.Water), 31, 38);
        Assert.InRange(VolcanoModel.EruptionHeightKm(EruptionStyle.Vulcanian, 3), 5, 10);
        Assert.True(VolcanoModel.LavaFlowLengthKm(2) > 20 * VolcanoModel.LavaFlowLengthKm(5));
    }

    [Fact]
    public void Journeys_EndWhereTheirNextRoundStarts()
    {
        foreach (var journey in VolcanoModel.Journeys)
        {
            Assert.Equal(RockProcess.Start, journey.Steps[0].Process);
            Assert.All(journey.Steps.Skip(1), s => Assert.NotEqual(RockProcess.Start, s.Process));
            Assert.True(journey.YearsTo(journey.Steps.Count - 1) > 1e6, journey.Id);
        }

        // The grain of sand goes through all three families? It starts igneous and ends up sedimentary.
        var sand = VolcanoModel.JourneyOf("Sand");
        Assert.Equal(RockFamily.Igneous, VolcanoModel.FamilyOf(sand.Steps[0].State));
        Assert.Contains(sand.Steps, s => VolcanoModel.FamilyOf(s.State) == RockFamily.Sedimentary);

        // The mud goes all the way round: igneous, sedimentary, metamorphic, magma and igneous again.
        var mud = VolcanoModel.JourneyOf("Mud");
        Assert.Equal(
            [RockFamily.Igneous, RockFamily.Sedimentary, RockFamily.Metamorphic],
            mud.Steps.Select(s => VolcanoModel.FamilyOf(s.State)).OfType<RockFamily>().Distinct().ToArray());
    }

    [Fact]
    public void Grains_SettleAtTheirRealSpeeds()
    {
        // Sand of 0.25 mm about 3 cm/s; clay of 2 µm about 3.6 µm/s, i.e. days for a metre.
        Assert.InRange(VolcanoModel.SettlingSpeed(2.5e-4), 0.025, 0.04);
        Assert.InRange(VolcanoModel.SettlingSpeed(2e-6), 3.0e-6, 4.0e-6);
        Assert.InRange(VolcanoModel.SecondsPerMetre(2e-6) / 86400, 2, 5);
        Assert.InRange(VolcanoModel.SettlingSpeed(4e-3), 0.25, 0.5);

        // On the screen in the same order, clay still the slowest by far.
        Assert.True(SedimentSim.ScreenFallSeconds(SedimentSim.Gravel) < SedimentSim.ScreenFallSeconds(SedimentSim.Sand));
        Assert.True(SedimentSim.ScreenFallSeconds(SedimentSim.Silt) < SedimentSim.ScreenFallSeconds(SedimentSim.Clay));
        Assert.InRange(SedimentSim.ScreenFallSeconds(SedimentSim.Clay), 15, 40);
    }

    [Fact]
    public void Burial_SqueezesTheWaterOut()
    {
        Assert.Equal(0.49, VolcanoModel.Porosity(false, 0), 2);
        Assert.Equal(0.63, VolcanoModel.Porosity(true, 0), 2);
        Assert.True(VolcanoModel.Porosity(true, 3) < 0.15);
        Assert.InRange(VolcanoModel.Thickness(true, 3), 0.4, 0.5);
        Assert.Equal(0, VolcanoModel.Cement(0));
        Assert.Equal(1, VolcanoModel.Cement(3));
        Assert.Equal("Sandstone", VolcanoModel.SedimentaryRock("Sand"));
        Assert.Equal("Shale", VolcanoModel.SedimentaryRock("Clay"));
    }

    [Fact]
    public void Shale_TurnsToSlateSchistAndGneiss_WithDepth()
    {
        Assert.Equal(265, VolcanoModel.TemperatureAt(10), 0);
        Assert.InRange(VolcanoModel.KilobarsAt(10), 2.4, 2.7);
        Assert.Equal("Shale", VolcanoModel.MetamorphicRock("Shale", 4));
        Assert.Equal("Slate", VolcanoModel.MetamorphicRock("Shale", 10));
        Assert.Equal("Schist", VolcanoModel.MetamorphicRock("Shale", 20));
        Assert.Equal("Gneiss", VolcanoModel.MetamorphicRock("Shale", 26));
        Assert.Equal("Migmatite", VolcanoModel.MetamorphicRock("Shale", 29));
        Assert.Equal("Marble", VolcanoModel.MetamorphicRock("Limestone", 15));
        Assert.Equal("Quartzite", VolcanoModel.MetamorphicRock("Sandstone", 15));
    }

    [Fact]
    public void SqueezedFlakes_LineUpGrowAndBand()
    {
        static double Spread(double grade) =>
            Enumerable.Range(0, SqueezeTexture.FlakeCount).Average(i => Math.Abs(SqueezeTexture.FlakeAt(i, grade).Angle));
        static double Length(double grade) =>
            Enumerable.Range(0, SqueezeTexture.FlakeCount).Average(i => SqueezeTexture.FlakeAt(i, grade).Length);

        Assert.InRange(Spread(0), 0.6, 1.0);
        Assert.True(Spread(0.35) < 0.06);
        Assert.True(Length(0.9) > 2.5 * Length(0.1));
        Assert.Empty(SqueezeTexture.Garnets(0.3));
        Assert.NotEmpty(SqueezeTexture.Garnets(0.8));
        Assert.Empty(SqueezeTexture.MeltPockets(0.8));
        Assert.True(SqueezeTexture.GrainCount(0) > 5 * SqueezeTexture.GrainCount(1));
        Assert.Equal(0, SqueezeTexture.PoreShare(0.6));

        // In gneiss dark minerals sit near their bands' middles.
        var dark = Enumerable.Range(0, SqueezeTexture.FlakeCount).Select(i => SqueezeTexture.FlakeAt(i, 1)).Where(f => f.Kind == FlakeKind.Dark).ToList();
        var offBand = dark.Average(f =>
        {
            var unsquashed = 0.5 + ((f.Y - 0.5) / 0.94);
            var phase = unsquashed / SqueezeTexture.BandSpacing;
            return Math.Abs(phase - Math.Round(phase));
        });
        Assert.True(offBand < 0.15, $"{offBand}");
    }
}
