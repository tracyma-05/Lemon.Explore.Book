using Lemon.Explore.Book.Chapters.HeatEngines;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class HeatModelTests
{
    [Fact]
    public void TheEngineIsATwoLitreFour()
    {
        Assert.Equal(0.4996e-3, EngineModel.Displacement, 6);
        Assert.Equal(2.0, EngineModel.Displacement * EngineModel.Cylinders * 1000, 1);

        // The piston travels the whole stroke from top to bottom, and is a little past half-way at 90° (the rod's angle).
        Assert.Equal(0, EngineModel.PistonTravel(0), 9);
        Assert.Equal(EngineModel.Stroke, EngineModel.PistonTravel(180), 9);
        Assert.InRange(EngineModel.PistonTravel(90), 0.5 * EngineModel.Stroke, 0.6 * EngineModel.Stroke);

        // Bottom over top is the compression ratio.
        Assert.Equal(10.5, EngineModel.Volume(180, 10.5) / EngineModel.Volume(0, 10.5), 9);
    }

    [Theory]
    [InlineData(0, EngineStroke.Intake)]
    [InlineData(179.9, EngineStroke.Intake)]
    [InlineData(180, EngineStroke.Compression)]
    [InlineData(400, EngineStroke.Power)]
    [InlineData(600, EngineStroke.Exhaust)]
    [InlineData(725, EngineStroke.Intake)]
    public void FourStrokesEachHalfATurn(double angle, EngineStroke stroke) => Assert.Equal(stroke, EngineModel.StrokeAt(angle));

    [Fact]
    public void ThePressureRisesWithCompressionAndPeaksJustAfterTheTop()
    {
        var states = Enumerable.Range(0, 7200).Select(i => EngineModel.At(i / 10.0)).ToList();
        var peak = states.MaxBy(s => s.Pressure);
        Assert.InRange(peak.Angle, 362, 385);
        Assert.InRange(peak.Pressure, 50, 90);
        Assert.InRange(peak.Temperature, 2200, 2900);

        // Compression alone, at the spark (20° before the top): about 14 bar and 630 K.
        var compressed = EngineModel.At(339);
        Assert.InRange(compressed.Pressure, 12, 16);
        Assert.InRange(compressed.Temperature, 580, 680);
        Assert.Equal(0, compressed.Burned);

        // The pumping strokes stay close to the outside pressure, and nothing jumps.
        Assert.InRange(EngineModel.At(90).Pressure, 0.9, 1.0);
        Assert.InRange(EngineModel.At(630).Pressure, 1.05, 1.15);
        for (var i = 1; i < states.Count; i++) Assert.True(Math.Abs(states[i].Pressure - states[i - 1].Pressure) < 1.5, $"jump at {states[i].Angle}");
        Assert.True(Math.Abs(EngineModel.At(719.99).Pressure - EngineModel.At(0).Pressure) < 0.02);
    }

    [Fact]
    public void ValvesOpenInTheirStrokesAndOverlapAtTheTop()
    {
        Assert.Equal(0, EngineModel.At(300).IntakeLift);
        Assert.Equal(0, EngineModel.At(300).ExhaustLift);
        Assert.True(EngineModel.At(90).IntakeLift > 0.9);
        Assert.True(EngineModel.At(630).ExhaustLift > 0.9);
        Assert.Equal(0, EngineModel.At(400).ExhaustLift);
        Assert.True(EngineModel.At(520).ExhaustLift > 0);
        Assert.True(EngineModel.At(715).IntakeLift > 0 && EngineModel.At(715).ExhaustLift > 0);
    }

    [Fact]
    public void WorkEfficiencyAndPowerAreThoseOfARealEngine()
    {
        var work = EngineModel.WorkPerCycle();
        Assert.InRange(EngineModel.MeanEffectivePressure(), 10, 15);
        Assert.InRange(EngineModel.Efficiency(), 0.40, 0.52);
        Assert.True(EngineModel.Efficiency() < EngineModel.OttoEfficiency(10.5));
        Assert.Equal(0.610, EngineModel.OttoEfficiency(10.5), 3);

        // Four cylinders at 3000 rpm, full throttle: about 50–60 kW on the pistons.
        Assert.InRange(EngineModel.Power(3000) / 1000, 48, 65);
        Assert.Equal(work * 4 * 3000 / 120, EngineModel.Power(3000), 6);

        // Squeezing harder burns the same fuel better.
        Assert.True(EngineModel.Efficiency(13) > EngineModel.Efficiency(9));
    }

    [Fact]
    public void ThinAirHighUpGivesLessPower()
    {
        Assert.Equal(1.01325, EngineModel.AmbientPressure(0), 5);
        Assert.Equal(1.225, EngineModel.AirDensity(0), 3);

        // Kunming, 1890 m: about four fifths of the pressure, so about four fifths of the power.
        var ratio = EngineModel.AmbientPressure(1890) / EngineModel.SeaLevelPressure;
        Assert.InRange(ratio, 0.78, 0.81);
        Assert.Equal(ratio, EngineModel.Power(3000, ambient: EngineModel.AmbientPressure(1890)) / EngineModel.Power(3000), 6);
    }

    [Fact]
    public void IsobutaneBoilsWhereItShould()
    {
        Assert.Equal(1.013, FridgeModel.SaturationPressure(-11.7), 3);
        Assert.InRange(FridgeModel.SaturationPressure(20), 2.9, 3.1);
        Assert.InRange(FridgeModel.SaturationPressure(0), 1.5, 1.65);
        Assert.InRange(FridgeModel.LowPressure, 0.5, 0.7);
        Assert.InRange(FridgeModel.HighPressure(25), 5.0, 5.6);
    }

    [Fact]
    public void TheRefrigerantGoesRoundHotHighAndColdLow()
    {
        var points = Enumerable.Range(0, 1000).Select(i => FridgeModel.At(i / 1000.0)).ToList();
        foreach (var point in points.Where(p => p.Part == FridgePart.Evaporator))
        {
            Assert.True(point.Temperature < FridgeModel.FreezerTemperature, "colder than the freezer, so heat flows in");
        }

        foreach (var point in points.Where(p => p.Part == FridgePart.Condenser))
        {
            Assert.True(point.Temperature > FridgeModel.DefaultRoom, "warmer than the room, so heat flows out");
        }

        Assert.Equal(1, FridgeModel.At(0.01).Vapour);
        Assert.Equal(0, FridgeModel.At(FridgeModel.PartEnd(FridgePart.Condenser) - 0.001).Vapour);
        Assert.InRange(FridgeModel.At(FridgeModel.PartEnd(FridgePart.Capillary) - 0.001).Vapour, 0.2, 0.26);
        Assert.Equal(FridgeModel.Discharge(25), points.Max(p => p.Temperature), 6);
        Assert.Equal(FridgeModel.Evaporating, points.Min(p => p.Temperature), 6);

        // The loop closes on itself.
        Assert.True(Math.Abs(FridgeModel.At(0.9999).Temperature - FridgeModel.At(0).Temperature) < 0.1);
        Assert.True(Math.Abs(FridgeModel.At(0.9999).Pressure - FridgeModel.At(0).Pressure) < 0.01);
    }

    [Fact]
    public void AWarmerRoomCostsMoreElectricity()
    {
        Assert.InRange(FridgeModel.CarnotCop(25), 3.6, 4.0);
        Assert.InRange(FridgeModel.RealCop(25), 1.5, 2.0);
        Assert.InRange(FridgeModel.DutyCycle(25, false), 0.35, 0.5);
        Assert.InRange(FridgeModel.DailyEnergy(25), 0.5, 0.9);
        Assert.True(FridgeModel.DailyEnergy(35) > 1.4 * FridgeModel.DailyEnergy(25));
        Assert.True(FridgeModel.DailyEnergy(15) < FridgeModel.DailyEnergy(25));
        Assert.Equal(1, FridgeModel.DutyCycle(30, doorOpen: true));
    }

    [Fact]
    public void TheCarnotLoopClosesAndObeysTheLimits()
    {
        const double hot = 500, cold = 300;
        Assert.Equal(0.4, CarnotModel.Efficiency(hot, cold), 9);
        Assert.Equal(1.5, CarnotModel.FridgeCop(hot, cold), 9);
        Assert.Equal(2.5, CarnotModel.HeatPumpCop(hot, cold), 9);
        Assert.Equal(CarnotModel.Efficiency(hot, cold), CarnotModel.Work(hot, cold) / CarnotModel.HotHeat(hot), 9);

        var start = CarnotModel.At(0, hot, cold);
        Assert.Equal((1.0, 1.0, hot), (start.Volume, start.Pressure, start.Temperature));
        var corners = Enumerable.Range(0, 5).Select(i => CarnotModel.At(i - 1e-9, hot, cold)).ToList();
        var (a, b, c, d) = CarnotModel.Volumes(hot, cold);
        Assert.Equal(b, CarnotModel.At(1, hot, cold).Volume, 6);
        Assert.Equal(c, CarnotModel.At(2, hot, cold).Volume, 6);
        Assert.Equal(d, CarnotModel.At(3, hot, cold).Volume, 6);
        Assert.Equal(a, CarnotModel.At(4 - 1e-9, hot, cold).Volume, 6);

        // Adiabats: p·V^γ stays the same.
        var b1 = CarnotModel.At(1, hot, cold);
        var c1 = CarnotModel.At(1.999999, hot, cold);
        Assert.Equal(b1.Pressure * Math.Pow(b1.Volume, CarnotModel.Gamma), c1.Pressure * Math.Pow(c1.Volume, CarnotModel.Gamma), 6);

        // The work is the area of the loop: ∮p dV, numerically.
        var area = 0.0;
        var previous = CarnotModel.At(0, hot, cold);
        for (var i = 1; i <= 40000; i++)
        {
            var next = CarnotModel.At(4.0 * i / 40000, hot, cold);
            area += (previous.Pressure + next.Pressure) / 2 * (next.Volume - previous.Volume);
            previous = next;
        }

        Assert.Equal(CarnotModel.Work(hot, cold) / hot, area, 4);
    }
}
