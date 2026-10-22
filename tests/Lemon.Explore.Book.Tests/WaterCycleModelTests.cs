using System;
using System.Linq;
using Lemon.Explore.Book.Chapters.WaterCycle;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class WaterCycleModelTests
{
    [Theory]
    [InlineData(0, 6.11)]
    [InlineData(10, 12.28)]
    [InlineData(20, 23.37)]
    [InlineData(30, 42.43)]
    public void SaturationPressureMatchesTables(double celsius, double hPa)
    {
        Assert.InRange(WaterCycleModel.SaturationPressure(celsius), hPa * 0.997, hPa * 1.003);
    }

    [Theory]
    [InlineData(25, 1.0)]
    [InlineData(25, 0.5)]
    [InlineData(5, 0.8)]
    [InlineData(-10, 0.6)]
    public void DewPointIsTheTemperatureThatSaturatesTheAir(double celsius, double humidity)
    {
        var dewPoint = WaterCycleModel.DewPoint(celsius, humidity);

        Assert.True(dewPoint <= celsius + 1e-9);
        Assert.Equal(humidity * WaterCycleModel.SaturationPressure(celsius), WaterCycleModel.SaturationPressure(dewPoint), 6);
    }

    [Fact]
    public void WarmAirHoldsAboutSevenPercentMoreVapourPerDegree()
    {
        var perDegree = WaterCycleModel.VapourFactor(15, 1) - 1;
        Assert.InRange(perDegree, 0.06, 0.075);

        // About 17 g in a cubic metre of saturated air at 20 °C, 4.8 g at 0 °C.
        Assert.Equal(17.3, WaterCycleModel.SaturationDensity(20), 0);
        Assert.Equal(4.85, WaterCycleModel.SaturationDensity(0), 1);
    }

    [Fact]
    public void StandardPressureFallsWithHeight()
    {
        Assert.Equal(1013.25, WaterCycleModel.Pressure(0), 6);
        Assert.Equal(898.7, WaterCycleModel.Pressure(1000), 0);
        Assert.Equal(540.4, WaterCycleModel.Pressure(5000), 0);
    }

    [Theory]
    [InlineData(25, 0.5)]
    [InlineData(30, 0.3)]
    [InlineData(12, 0.8)]
    [InlineData(5, 0.95)]
    public void CloudBaseIsAbout125MetresPerDegreeOfDewPointSpread(double celsius, double humidity)
    {
        var ascent = WaterCycleModel.Lift(celsius, humidity, 8000);
        var rule = WaterCycleModel.CloudBaseRuleOfThumb(celsius, WaterCycleModel.DewPoint(celsius, humidity));

        Assert.True(ascent.HasCloud);
        Assert.InRange(ascent.CloudBase, rule * 0.92, (rule * 1.08) + 20);

        // At the cloud base the parcel's temperature is its dew point.
        var atBase = ascent.At(ascent.CloudBase + 1);
        Assert.Equal(atBase.Temperature, atBase.DewPoint, 0);
    }

    [Fact]
    public void BelowTheCloudTheParcelCoolsAtTheDryRateAndInsideMoreSlowly()
    {
        var ascent = WaterCycleModel.Lift(25, 0.5, 6000);
        var below = ascent.At(ascent.CloudBase - 200);
        var ground = ascent.Ground;
        var dryRate = (ground.Temperature - below.Temperature) / below.Height * 1000;
        Assert.Equal(9.77, dryRate, 1);

        // Its dew point falls only about 1.8 °C per km on the way up.
        var dewRate = (ground.DewPoint - below.DewPoint) / below.Height * 1000;
        Assert.InRange(dewRate, 1.5, 2.1);

        // In the cloud: in warm air about half the dry rate.
        var a = ascent.At(ascent.CloudBase + 100);
        var b = ascent.At(ascent.CloudBase + 1100);
        var moistRate = a.Temperature - b.Temperature;
        Assert.InRange(moistRate, 3.8, 6.0);
        Assert.True(b.Saturated);
        Assert.True(b.Condensed > a.Condensed);
    }

    [Fact]
    public void MoistRateNearsTheDryRateInColdAir()
    {
        var warm = WaterCycleModel.MoistLapseRate(25, 1000) * 1000;
        var cold = WaterCycleModel.MoistLapseRate(-30, 500) * 1000;

        Assert.InRange(warm, 3.5, 4.2);
        Assert.InRange(cold, 8.5, 9.6);
        Assert.True(cold < WaterCycleModel.DryLapseRate * 1000);
    }

    [Fact]
    public void DryAirMakesNoCloud()
    {
        var ascent = WaterCycleModel.Lift(30, 0.15, WaterCycleModel.MountainHeight);

        Assert.False(ascent.HasCloud);
        Assert.False(WaterCycleModel.Rains(ascent));
        Assert.Equal(0, ascent.Top.Condensed);
    }

    [Fact]
    public void AirComingDownTheFarSideIsWarmerAndDrierAfterRainingOnTheNearSide()
    {
        var up = WaterCycleModel.Lift(15, 0.8, WaterCycleModel.MountainHeight);
        var down = WaterCycleModel.Descend(up.Top, 0);
        var lee = down[^1];

        Assert.True(up.HasCloud);
        Assert.True(lee.Temperature > up.Ground.Temperature + 3);
        Assert.True(lee.Humidity < up.Ground.Humidity / 2);
        Assert.Equal(0, lee.Height, 6);

        // With no cloud on the way up, it comes down just as it went.
        var dry = WaterCycleModel.Lift(15, 0.15, WaterCycleModel.MountainHeight);
        Assert.False(dry.HasCloud);
        var dryLee = WaterCycleModel.Descend(dry.Top, 0)[^1];
        Assert.Equal(dry.Ground.Temperature, dryLee.Temperature, 1);
        Assert.Equal(dry.Ground.DewPoint, dryLee.DewPoint, 1);
    }

    [Fact]
    public void AThinCloudThatDoesNotRainMakesNoFoehn()
    {
        // Cloud base between 2 and 3 km: a cap cloud on the crest, too thin to rain.
        var up = WaterCycleModel.Lift(20, 0.33, WaterCycleModel.MountainHeight);
        Assert.True(up.HasCloud);
        Assert.False(WaterCycleModel.Rains(up));

        var lee = WaterCycleModel.OverMountain(up)[^1];
        Assert.Equal(up.Ground.Temperature, lee.Temperature, 6);

        // A deep, raining cloud does.
        var wet = WaterCycleModel.Lift(20, 0.9, WaterCycleModel.MountainHeight);
        Assert.True(WaterCycleModel.Rains(wet));
        Assert.True(WaterCycleModel.OverMountain(wet)[^1].Temperature > wet.Ground.Temperature + 3);
    }

    [Fact]
    public void FreezingLevelIsWhereTheParcelReachesZero()
    {
        var ascent = WaterCycleModel.Lift(20, 0.6, 7000);

        Assert.False(double.IsNaN(ascent.FreezingLevel));
        Assert.Equal(0, ascent.At(ascent.FreezingLevel).Temperature, 1);
        Assert.True(WaterCycleModel.Rains(ascent));
    }

    [Fact]
    public void TheParcelSwellsAsItRises()
    {
        var ascent = WaterCycleModel.Lift(20, 0.5, 6000);
        var swelling = WaterCycleModel.Swelling(ascent.Ground, ascent.Top);

        Assert.InRange(swelling, 1.5, 1.9);
    }

    [Fact]
    public void TheStoresAddUpToAllTheWater()
    {
        var sum = Enum.GetValues<WaterStore>().Sum(WaterCycleModel.Volume);

        Assert.InRange(sum / WaterCycleModel.TotalWater, 0.999, 1.001);
        Assert.Equal(0.965, WaterCycleModel.Share(WaterStore.Oceans), 3);
        Assert.InRange(WaterCycleModel.FreshWater / WaterCycleModel.TotalWater, 0.024, 0.026);
        Assert.InRange(WaterCycleModel.Volume(WaterStore.IceAndSnow) / WaterCycleModel.FreshWater, 0.68, 0.70);
    }

    [Fact]
    public void TheWaterBallsAreAsLargeAsTheUsgsDrawsThem()
    {
        Assert.InRange(WaterCycleModel.SphereDiameter(WaterCycleModel.TotalWater), 1380, 1390);
        Assert.InRange(WaterCycleModel.SphereDiameter(WaterCycleModel.FreshLiquidWater), 272, 274);
        Assert.InRange(WaterCycleModel.SphereDiameter(WaterCycleModel.LakesAndRivers), 56, 56.5);
    }

    [Fact]
    public void TheYearlyFlowsBalanceAndGiveTheStays()
    {
        Assert.Equal(WaterCycleModel.VapourToLand, WaterCycleModel.Runoff);
        Assert.Equal(WaterCycleModel.OceanEvaporation + WaterCycleModel.LandEvaporation,
            WaterCycleModel.OceanPrecipitation + WaterCycleModel.LandPrecipitation);
        Assert.InRange(WaterCycleModel.AtmosphereDays, 9, 10);
        Assert.InRange(WaterCycleModel.OceanYears, 3100, 3300);
        Assert.InRange(WaterCycleModel.RiverDays, 15, 25);
        Assert.InRange(WaterCycleModel.FallsBackOnSea, 0.89, 0.91);

        // About a metre of water over the whole Earth each year.
        var earthArea = 4 * Math.PI * Math.Pow(WaterCycleModel.EarthDiameter / 2, 2);
        Assert.InRange(WaterCycleModel.YearlyCycle / earthArea * 1000, 0.9, 1.0);
    }

    [Fact]
    public void TheLandscapeHasACoastAPeakAndGroundwaterBelowTheGround()
    {
        Assert.Equal(0, CycleScene.Ground(CycleScene.Coast), 6);
        Assert.True(CycleScene.Ground(0.1) < -1000);
        Assert.True(CycleScene.Ground(CycleScene.PeakX) > 4000);

        for (var x = CycleScene.Coast; x <= 1; x += 0.005)
        {
            Assert.True(CycleScene.WaterTable(x) <= CycleScene.Ground(x) + 1e-9, $"water table above ground at {x}");
        }

        // From the coast up to the peak, the ground and the water table only rise.
        for (var x = CycleScene.Coast; x < CycleScene.PeakX; x += 0.005)
        {
            Assert.True(CycleScene.Ground(x + 0.005) >= CycleScene.Ground(x));
            Assert.True(CycleScene.WaterTable(x + 0.005) >= CycleScene.WaterTable(x) - 1e-9);
        }
    }

    [Theory]
    [InlineData(-4)]
    [InlineData(0)]
    [InlineData(4)]
    public void TheRiverRisesAtTheSnowLineAndRunsDownToTheSea(double warming)
    {
        var river = CycleScene.River(warming);

        Assert.Equal(CycleScene.Coast, river[^1].X, 9);
        Assert.Equal(0, river[^1].Z, 6);
        for (var i = 1; i < river.Count; i++) Assert.True(river[i].Z <= river[i - 1].Z + 1e-9);

        // A warmer world: the snow line and the river's source move up.
        Assert.True(CycleScene.SnowLine(warming + 1) - CycleScene.SnowLine(warming) > 150);
        Assert.True(CycleScene.RiverSource(warming + 1) >= CycleScene.RiverSource(warming));
    }

    [Theory]
    [InlineData(DropRoute.River)]
    [InlineData(DropRoute.Snow)]
    [InlineData(DropRoute.Ground)]
    [InlineData(DropRoute.Tree)]
    [InlineData(DropRoute.Sea)]
    public void EveryJourneyIsJoinedUpAndEndsInTheSea(DropRoute route)
    {
        var steps = CycleScene.Journey(route, 0);

        Assert.Equal(DropLeg.Ocean, steps[0].Leg);
        Assert.Equal(DropLeg.Ocean, steps[^1].Leg);
        Assert.True(steps[^1].Path[^1].Z < 0);
        for (var i = 1; i < steps.Count; i++)
        {
            var end = steps[i - 1].Path[^1];
            var start = steps[i].Path[0];
            Assert.True(Math.Abs(end.X - start.X) < 1e-9 && Math.Abs(end.Z - start.Z) < 1e-6, $"{route}: step {i} ({steps[i].Leg}) does not start where {steps[i - 1].Leg} ended");
        }

        // Nothing in the air goes through the ground.
        foreach (var step in steps.Where(s => s.Leg is DropLeg.Evaporation or DropLeg.Transport or DropLeg.Condensation or DropLeg.Transpiration))
        {
            foreach (var point in step.Path) Assert.True(point.Z >= CycleScene.Ground(point.X) - 1, $"{route} {step.Leg} under ground at {point}");
        }

        Assert.All(steps, s => Assert.True(s.Seconds > 0));
        Assert.Equal(steps[1].Path[0], CycleScene.Along(steps[1], 0));
        Assert.Equal(steps[1].Path[^1], CycleScene.Along(steps[1], steps[1].Seconds));
    }
}
