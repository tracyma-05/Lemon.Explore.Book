using Lemon.Explore.Book.Chapters.StatesOfMatter;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class MatterModelTests
{
    private static readonly MatterModel.Substance Water = MatterModel.Water;
    private static readonly MatterModel.Substance Co2 = MatterModel.CarbonDioxide;

    private static double C(double kelvin) => kelvin - MatterModel.ZeroCelsius;

    private static double K(double celsius) => celsius + MatterModel.ZeroCelsius;

    [Fact]
    public void Water_BoilsAndMelts_WhereTheBooksSay()
    {
        Assert.Equal(100, C(MatterModel.BoilingTemperature(Water, MatterModel.Atmosphere)), 1);
        Assert.Equal(0, C(MatterModel.MeltingTemperature(Water, MatterModel.Atmosphere)), 2);

        // Everest about 70 °C, Lhasa about 88 °C, a pressure cooker 120 °C.
        Assert.InRange(C(MatterModel.BoilingTemperature(Water, MatterModel.PressureAt(HeatingPlace.Everest))), 69.5, 71.5);
        Assert.InRange(C(MatterModel.BoilingTemperature(Water, MatterModel.PressureAt(HeatingPlace.Lhasa))), 87, 89);
        Assert.Equal(120, C(MatterModel.BoilingTemperature(Water, MatterModel.PressureAt(HeatingPlace.Cooker))), 1);
        Assert.Equal(70, MatterLandmarks.EverestBoilingC, 0);

        // The boiling curve and its inverse agree; it ends at the critical point.
        foreach (var p in new[] { 1e3, 3e4, 1e5, 2e6, 2e7 })
        {
            Assert.Equal(p, MatterModel.BoilingPressure(Water, MatterModel.BoilingTemperature(Water, p)), p * 1e-9);
        }

        Assert.Equal(Water.CriticalP, MatterModel.BoilingPressure(Water, Water.CriticalT), 1);
    }

    [Fact]
    public void TheCurves_MeetAtTheTriplePoint()
    {
        foreach (var s in new[] { Water, Co2 })
        {
            Assert.Equal(s.TripleP, MatterModel.SublimationPressure(s, s.TripleT), 1e-6);
            Assert.Equal(s.TripleP, MatterModel.BoilingPressure(s, s.TripleT), 1e-6);
            Assert.Equal(s.TripleT, MatterModel.MeltingTemperature(s, s.TripleP), 9);
        }

        // Ice at −20 °C has a vapour pressure of about 103 Pa.
        Assert.InRange(MatterModel.SublimationPressure(Water, K(-20)), 95, 110);
    }

    [Fact]
    public void CarbonDioxide_SublimesAtOneAtmosphere()
    {
        Assert.Equal(-78.5, C(MatterModel.SublimationTemperature(Co2, MatterModel.Atmosphere)), 1);
        Assert.Equal(MatterState.Solid, MatterModel.StateAt(Co2, K(-90), MatterModel.Atmosphere));
        Assert.Equal(MatterState.Gas, MatterModel.StateAt(Co2, K(-60), MatterModel.Atmosphere));

        // No liquid at any temperature at one atmosphere; liquid in an extinguisher at room temperature.
        for (var c = -130.0; c <= 60; c += 1)
        {
            Assert.NotEqual(MatterState.Liquid, MatterModel.StateAt(Co2, K(c), MatterModel.Atmosphere));
        }

        Assert.Equal(MatterState.Liquid, MatterModel.StateAt(Co2, K(20), 6e6));
        Assert.Equal(MatterState.Supercritical, MatterModel.StateAt(Co2, K(40), 1e7));
    }

    [Theory]
    [InlineData(-20, 101_325, MatterState.Solid)]
    [InlineData(25, 101_325, MatterState.Liquid)]
    [InlineData(120, 101_325, MatterState.Gas)]
    [InlineData(120, 2e6, MatterState.Liquid)]
    [InlineData(400, 3e7, MatterState.Supercritical)]
    [InlineData(400, 1e6, MatterState.Gas)]
    [InlineData(-1, 3e7, MatterState.Liquid)]
    [InlineData(-60, 600, MatterState.Solid)]
    [InlineData(5, 600, MatterState.Gas)]
    public void Water_IsInTheRightState(double celsius, double pascals, MatterState expected) =>
        Assert.Equal(expected, MatterModel.StateAt(Water, K(celsius), pascals));

    [Fact]
    public void TheLandmarks_SitInTheirStates()
    {
        Assert.Equal(MatterState.Solid, MatterModel.StateAt(Water, K(-60), 600));
        Assert.Equal(MatterState.Solid, MatterModel.StateAt(Co2, K(MatterDiagram.LandmarkState("lm_marspole")!.Value.Celsius), 600));
        Assert.Equal(MatterState.Liquid, MatterModel.StateAt(Co2, K(MatterDiagram.LandmarkState("lm_extinguisher")!.Value.Celsius), MatterDiagram.LandmarkState("lm_extinguisher")!.Value.Pascals));
        foreach (var substance in Enum.GetValues<MatterSubstance>())
        {
            foreach (var id in MatterDiagram.Landmarks(substance))
            {
                var (c, p) = MatterDiagram.LandmarkState(id)!.Value;
                Assert.InRange(c, MatterModel.Of(substance).MinCelsius, MatterModel.Of(substance).MaxCelsius);
                Assert.InRange(p, MatterModel.MinPressure, MatterModel.MaxPressure);
            }
        }
    }

    [Fact]
    public void TheHeatingCurve_HasItsPlateaus_AndTheirHeats()
    {
        var curve = MatterModel.HeatingCurve(Water, MatterModel.Atmosphere);
        Assert.Equal(["WarmSolid", "Melt", "WarmLiquid", "Boil", "WarmGas"], curve.Select(s => s.Kind));
        var melt = curve.Single(s => s.Kind == "Melt");
        var boil = curve.Single(s => s.Kind == "Boil");
        Assert.Equal(33.4, melt.Kj, 6);
        Assert.Equal(225.7, boil.Kj, 6);
        Assert.InRange(boil.Kj / melt.Kj, 6.5, 7);

        // Flat while melting; fractions run from all ice to all water across it.
        Assert.Equal(MatterModel.CelsiusAt(curve, melt.StartKj + 1), MatterModel.CelsiusAt(curve, melt.EndKj - 1), 9);
        var half = MatterModel.FractionsAt(curve, melt.StartKj + (melt.Kj / 2));
        Assert.Equal(0.5, half.Solid, 6);
        Assert.Equal(0.5, half.Liquid, 6);
        Assert.Equal((0.0, 0.0, 1.0), MatterModel.FractionsAt(curve, MatterModel.TotalKj(curve)));

        // Steady warming between: temperature rises with heat.
        var liquid = curve.Single(s => s.Kind == "WarmLiquid");
        Assert.True(MatterModel.CelsiusAt(curve, liquid.EndKj - 1) > MatterModel.CelsiusAt(curve, liquid.StartKj + 1));

        // Dry ice at one atmosphere: no melting, a sublimation plateau at −78.5 °C.
        var dry = MatterModel.HeatingCurve(Co2, MatterModel.Atmosphere);
        Assert.Equal(["WarmSolid", "Sublime", "WarmGas"], dry.Select(s => s.Kind));
        Assert.Equal(-78.5, dry[1].StartC, 1);
    }

    [Fact]
    public void TheMolecules_SettleIntoEachState_AndChangeAFewAtATime()
    {
        var box = new MoleculeBox { Temperature = 300 };
        Assert.Equal(MoleculeBox.Count, box.SolidCount);
        box.SetTarget(0, 1, 0);
        box.Step(0.1);
        Assert.InRange(box.LiquidCount, 1, 3);
        for (var i = 0; i < 100; i++) box.Step(0.05);
        Assert.Equal(MoleculeBox.Count, box.LiquidCount);

        box.SetTarget(0, 0.25, 0.75);
        box.Settle();
        Assert.Equal((0, 10, 30), (box.SolidCount, box.LiquidCount, box.GasCount));
        for (var i = 0; i < 60; i++) box.Step(0.05);
        Assert.True(box.CondensedTop > 0);
    }
}
