using System;
using System.Linq;
using Lemon.Explore.Book.Chapters.SeaBreeze;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class BreezeModelTests
{
    [Fact]
    public void LandIsColdestAtSunriseAndWarmestInTheEarlyAfternoon()
    {
        var hours = Enumerable.Range(0, 24 * 20).Select(i => i / 20.0).ToList();
        var coldest = hours.MinBy(h => BreezeModel.LandTemperature(h));
        var warmest = hours.MaxBy(h => BreezeModel.LandTemperature(h));

        Assert.InRange(coldest, BreezeModel.Sunrise - 0.1, BreezeModel.Sunrise + 0.1);
        Assert.InRange(warmest, 13.5, 14.5);
        Assert.Equal(20, BreezeModel.LandTemperature(BreezeModel.Sunrise), 6);
        Assert.Equal(31, BreezeModel.LandTemperature(warmest), 1);
    }

    [Fact]
    public void LandTemperatureIsContinuousRoundTheClock()
    {
        for (var h = 0.0; h < 24; h += 0.01)
        {
            Assert.InRange(Math.Abs(BreezeModel.LandTemperature(h + 0.01) - BreezeModel.LandTemperature(h)), 0, 0.05);
        }

        Assert.Equal(BreezeModel.LandTemperature(0), BreezeModel.LandTemperature(24), 9);
    }

    [Fact]
    public void TheSeaHardlyChangesWhileTheLandSwingsByTenDegrees()
    {
        var hours = Enumerable.Range(0, 48).Select(i => i / 2.0).ToList();
        var sea = hours.Max(h => BreezeModel.SeaTemperature(h)) - hours.Min(h => BreezeModel.SeaTemperature(h));
        var land = hours.Max(h => BreezeModel.LandTemperature(h)) - hours.Min(h => BreezeModel.LandTemperature(h));

        Assert.InRange(sea, 0.5, 1);
        Assert.InRange(land, 10, 11.5);
    }

    [Theory]
    [InlineData(3, -3.0, -0.5)]
    [InlineData(5, -3.0, -0.5)]
    [InlineData(14, 3.5, 7.0)]
    [InlineData(16, 3.0, 7.0)]
    public void SeaBreezeByDayLandBreezeByNight(double hour, double low, double high)
    {
        Assert.InRange(BreezeModel.ShoreWind(hour), low, high);
    }

    [Fact]
    public void TheBreezeTurnsAroundInTheMorningAndTheEvening()
    {
        Assert.Equal(CoastPhase.LandBreeze, BreezeModel.Phase(3));
        Assert.Equal(CoastPhase.MorningCalm, BreezeModel.Phase(8.5));
        Assert.Equal(CoastPhase.SeaBreezeBuilding, BreezeModel.Phase(10.5));
        Assert.Equal(CoastPhase.SeaBreeze, BreezeModel.Phase(14.5));
        Assert.Equal(CoastPhase.EveningCalm, BreezeModel.Phase(21));
        Assert.Equal(CoastPhase.LandBreeze, BreezeModel.Phase(23.5));

        // The sea breeze is the stronger of the two.
        var hours = Enumerable.Range(0, 240).Select(i => i / 10.0).ToList();
        Assert.True(hours.Max(h => BreezeModel.ShoreWind(h)) > 2 * -hours.Min(h => BreezeModel.ShoreWind(h)));
    }

    [Fact]
    public void AnOvercastDayHardlyStirsTheAir()
    {
        for (var h = 0.0; h < 24; h += 0.25)
        {
            Assert.InRange(BreezeModel.ShoreWind(h, BreezeSky.Overcast), -1, 1);
            Assert.Equal(CoastPhase.Overcast, BreezeModel.Phase(h, BreezeSky.Overcast));
        }
    }

    [Fact]
    public void TheSeaBreezeFrontMovesInlandThroughTheAfternoon()
    {
        Assert.Equal(0, BreezeModel.FrontDistance(8));
        var previous = 0.0;
        for (var h = 11.0; h <= 18; h += 0.5)
        {
            var front = BreezeModel.FrontDistance(h);
            Assert.True(front > previous, $"front at {h}: {front}");
            previous = front;
        }

        // Typical sea breezes reach a few tens of kilometres inland.
        Assert.InRange(previous, 20, 50);
        Assert.InRange(BreezeModel.LandBreezeReach(5), 5, 25);
        Assert.Equal(0, BreezeModel.LandBreezeReach(14));
    }

    [Fact]
    public void ThicknessFollowsTheHypsometricEquation()
    {
        // 1000 to 500 hPa in air averaging 0 °C is about 5.5 km; each degree warmer adds about 3 m between 1000 and 900 hPa.
        Assert.InRange(BreezeModel.Thickness(273.15, 1000, 500), 5500, 5560);
        var perDegree = BreezeModel.Thickness(301, 1000, 900) - BreezeModel.Thickness(300, 1000, 900);
        Assert.InRange(perDegree, 3.0, 3.15);
    }

    [Fact]
    public void PressureFallsAboutOneHectopascalPerEightMetresNearTheGround()
    {
        var p = BreezeModel.PressureAt(100, 1013.25, _ => 288.15);
        Assert.InRange(1013.25 - p, 11.5, 12.5);

        // The standard atmosphere has 898.7 hPa at 1000 m.
        var standard = BreezeModel.PressureAt(1000, 1013.25, z => 288.15 - (0.0065 * z));
        Assert.Equal(898.7, standard, 0);
    }

    [Fact]
    public void WarmLandRaisesPressureAloftThenLowersItAtTheGround()
    {
        var aloft = BreezeModel.AloftDifference(32, 24);
        Assert.InRange(aloft, 0.8, 2.0);
        Assert.Equal(0, BreezeModel.PressureDifference(0, 32, 24, settled: false), 9);

        var surface = BreezeModel.SurfaceDifference(32, 24);
        Assert.InRange(surface, -1.0, -0.4);
        Assert.Equal(-aloft / 2, surface, 6);

        // Once settled, the difference changes sign part of the way up, and is positive at the top of the heated layer.
        Assert.True(BreezeModel.PressureDifference(BreezeModel.HeatedDepth, 32, 24, settled: true) > 0.3);

        // At night the land is colder and everything turns round.
        Assert.True(BreezeModel.AloftDifference(20, 24) < 0);
        Assert.True(BreezeModel.SurfaceDifference(20, 24) > 0);
    }

    [Fact]
    public void WarmerColumnsReachAPressureLevelHigherUp()
    {
        var sea = BreezeModel.HeightOf(900, 1013.25, z => BreezeModel.SeaColumn(24, z));
        var land = BreezeModel.HeightOf(900, 1013.25, z => BreezeModel.LandColumn(32, 24, z));
        Assert.InRange(sea, 950, 1050);
        Assert.InRange(land - sea, 8, 20);
    }

    [Fact]
    public void StagesRunInOrder()
    {
        Assert.Equal(CellStage.Even, BreezeModel.StageAt(0));
        Assert.Equal(CellStage.Heating, BreezeModel.StageAt(BreezeModel.StageSeconds + 0.1));
        Assert.Equal(CellStage.Circulation, BreezeModel.StageAt(BreezeModel.CellDuration + 5));
        Assert.Equal(0, BreezeModel.Progress(BreezeModel.StageSeconds, CellStage.Heating));
        Assert.Equal(1, BreezeModel.Progress(BreezeModel.StageSeconds * 2, CellStage.Heating));
    }

    [Fact]
    public void SiberianHighInWinterAsianLowInSummer()
    {
        var siberian = BreezeModel.Systems.First(s => s.Key == "Siberian");
        var asian = BreezeModel.Systems.First(s => s.Key == "AsianLow");

        Assert.InRange(BreezeModel.CentralPressure(siberian, 0.5), 1030, 1042);
        Assert.InRange(BreezeModel.CentralPressure(asian, 6.5), 990, 1002);
        Assert.True(BreezeModel.CentralPressure(siberian, 6.5) < 1015);
    }

    [Theory]
    // Mumbai in July: the south-west monsoon.
    [InlineData(19.1, 72.9, 6.5, new[] { 4, 5, 6 })]
    // Kolkata in January: the north-east (winter) monsoon.
    [InlineData(22.6, 88.4, 0.5, new[] { 0, 1, 7 })]
    // Beijing in January: cold north to north-west winds from the Siberian high.
    [InlineData(39.9, 116.4, 0.5, new[] { 0, 7 })]
    // Shanghai in July: the south-east (summer) monsoon from the Pacific.
    [InlineData(31.2, 121.5, 6.5, new[] { 3, 4, 5 })]
    // Guangzhou in January: north to north-east.
    [InlineData(23.1, 113.3, 0.5, new[] { 0, 1 })]
    // Darwin in January: the north-west monsoon of northern Australia.
    [InlineData(-12.5, 130.8, 0.5, new[] { 6, 7 })]
    // South of India in July, the trades still blow from the south-east before they cross the equator.
    [InlineData(-10, 65, 6.5, new[] { 2, 3 })]
    public void MonsoonWindsBlowFromTheRightDirection(double latitude, double longitude, double month, int[] compass)
    {
        var (u, v) = BreezeModel.Wind(latitude, longitude, month);
        var from = BreezeModel.CompassFrom(u, v);
        Assert.Contains(from, compass);
        Assert.InRange(Math.Sqrt((u * u) + (v * v)), 1, 20);
    }

    [Theory]
    [InlineData(0, 1, 4)]   // wind blowing north comes from the south
    [InlineData(-1, 0, 2)]  // blowing west comes from the east
    [InlineData(1, 1, 5)]   // blowing north-east comes from the south-west
    [InlineData(0, -1, 0)]
    public void CompassNamesWhereTheWindComesFrom(double u, double v, int expected)
    {
        Assert.Equal(expected, BreezeModel.CompassFrom(u, v));
    }

    [Fact]
    public void MonsoonCitiesGetMostRainInTheirSummer()
    {
        var mumbai = BreezeModel.City(MonsoonCity.Mumbai);
        Assert.InRange(mumbai.Rain.Skip(5).Take(4).Sum() / mumbai.YearRain, 0.9, 1);
        var darwin = BreezeModel.City(MonsoonCity.Darwin);
        Assert.True(darwin.Rain[0] > 100 * darwin.Rain[6]);
        Assert.All(BreezeModel.Cities, c => Assert.Equal(12, c.Rain.Count));
        Assert.Equal(840, BreezeModel.RainIn(MonsoonCity.Mumbai, 6.2));
        Assert.Equal(12, BreezeModel.MonthNumber(11.99));
        Assert.Equal(1, BreezeModel.MonthNumber(12));
    }

    [Fact]
    public void BeaufortScale()
    {
        Assert.Equal(0, BreezeModel.Beaufort(0.2));
        Assert.Equal(2, BreezeModel.Beaufort(3));
        Assert.Equal(3, BreezeModel.Beaufort(5));
        Assert.Equal(4, BreezeModel.Beaufort(6));
    }
}
