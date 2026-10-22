using System;
using System.Linq;
using Lemon.Explore.Book.Chapters.Typhoon;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class TyphoonModelTests
{
    [Theory]
    [InlineData(1000, StormGrade.Depression)]
    [InlineData(990, StormGrade.TropicalStorm)]
    [InlineData(980, StormGrade.SevereTropicalStorm)]
    [InlineData(965, StormGrade.Typhoon)]
    [InlineData(945, StormGrade.SevereTyphoon)]
    [InlineData(915, StormGrade.SuperTyphoon)]
    public void DeeperStormsBlowHarder(double pressure, StormGrade grade) =>
        Assert.Equal(grade, StormModel.Grade(StormModel.MaxWind(pressure)));

    [Fact]
    public void WindAndPressureGoTogether()
    {
        // Atkinson–Holliday: 950 hPa is about 48 m/s; the inverse gives the pressure back.
        Assert.InRange(StormModel.MaxWind(950), 46, 50);
        Assert.Equal(950, StormModel.CentralPressure(StormModel.MaxWind(950)), 6);
    }

    [Theory]
    [InlineData(990)]
    [InlineData(950)]
    [InlineData(910)]
    public void TheProfilePeaksAtTheRadiusOfMaxWind(double pressure)
    {
        var rmax = StormModel.RadiusOfMaxWind(pressure);
        var samples = Enumerable.Range(1, 2000).Select(i => i * 0.25).Select(r => (R: r, V: StormModel.Wind(r, pressure))).ToList();
        var peak = samples.MaxBy(s => s.V);
        Assert.InRange(peak.R, rmax * 0.9, rmax * 1.1);
        Assert.InRange(peak.V, StormModel.MaxWind(pressure) * 0.95, StormModel.MaxWind(pressure) * 1.01);

        // Calm in the eye, the pressure lowest at the centre and back to normal far away.
        Assert.True(StormModel.Wind(1, pressure) < 3);
        Assert.InRange(StormModel.Pressure(0.1, pressure), pressure - 0.01, pressure + 0.5);
        Assert.InRange(StormModel.Pressure(1500, pressure), StormModel.AmbientPressure - 3, StormModel.AmbientPressure);
        Assert.Equal(1000, StormModel.Pressure(StormModel.RadiusOfPressure(1000, pressure), pressure), 6);
    }

    [Fact]
    public void DeeperStormsAreTighter()
    {
        Assert.True(StormModel.RadiusOfMaxWind(920) < StormModel.RadiusOfMaxWind(980));
        Assert.InRange(StormModel.RadiusOfMaxWind(950), 15, 45);
        Assert.InRange(StormModel.EyeRadius(950), 10, 30);
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(20, 8)]
    [InlineData(33, 12)]
    [InlineData(45, 14)]
    [InlineData(60, 17)]
    [InlineData(70, 18)]
    public void BeaufortGoesUpTo17(double speed, int force) => Assert.Equal(force, StormModel.Beaufort(speed));

    [Fact]
    public void WarmSeaFeedsStrongerStorms()
    {
        Assert.Equal(0, StormModel.PotentialIntensity(26));
        Assert.True(StormModel.PotentialIntensity(27) > 40);
        Assert.InRange(StormModel.PotentialIntensity(30), 83, 85);
        Assert.True(StormModel.PotentialIntensity(30) > StormModel.PotentialIntensity(28));
    }

    [Fact]
    public void LandStarvesTheStorm()
    {
        // A 50 m/s storm: about 45 at the coast, under 30 after a day.
        Assert.Equal(50, StormModel.AfterLandfall(50, 0));
        Assert.InRange(StormModel.AfterLandfall(50, 0.01), 44, 45.1);
        Assert.InRange(StormModel.AfterLandfall(50, 24), 15, 20);
        Assert.InRange(StormModel.AfterLandfall(50, 96), 13.7, 14.5);
    }

    [Fact]
    public void AWestReachingHighSendsStormsIntoSouthChinaAndAWeakOneTurnsThemAway()
    {
        var west = TrackModel.Track(new TrackOptions(13.5, 144, 108, 8));
        var east = TrackModel.Track(new TrackOptions(13.5, 144, 145, 8));

        // West: a landfall south of 26° N on the China coast, west of 122° E.
        var westFalls = TrackModel.Landfalls(west);
        Assert.Contains(westFalls, f => west[f.Index].Latitude < 26 && west[f.Index].Longitude < 122 && west[f.Index].Longitude > 105);

        // East: it turns north well out at sea and never reaches the China coast.
        Assert.DoesNotContain(east, p => p.OverLand && p.Longitude < 122 && p.Latitude < 32);
        Assert.True(east.Max(p => p.Latitude) > 28);
    }

    [Fact]
    public void StormsGrowOverWarmSeaAndEnd()
    {
        var track = TrackModel.Track(new TrackOptions(13.5, 144, 125, 8));
        Assert.True(track.Length > 72);
        Assert.InRange(track.Max(p => p.Wind), 40, 75);
        Assert.True(track.Length < (14 * 24) + 1, "the storm ends");
    }

    [Fact]
    public void SteeringIsEasterlySouthOfTheRidgeAndWesterlyNorthOfIt()
    {
        var (u1, _) = TrackModel.Steering(15, 140, 120, 8);
        var (u2, _) = TrackModel.Steering(40, 140, 120, 8);
        Assert.True(u1 < -3);
        Assert.True(u2 > 3);

        // Near the high's west end the flow turns north.
        var (_, v) = TrackModel.Steering(25, 121, 120, 8);
        Assert.True(v > 3);
    }

    [Fact]
    public void ClosestPassAndDistances()
    {
        Assert.InRange(TrackModel.Distance(0, 0, 0, 1), 110, 112);
        var track = TrackModel.Track(new TrackOptions(13.5, 144, 108, 8));
        var (index, distance, wind) = TrackModel.ClosestPass(track, 28.23, 112.94);
        Assert.InRange(index, 0, track.Length - 1);
        Assert.True(distance > 0);
        Assert.True(wind >= 0);
    }

    [Fact]
    public void TwentyFiveStormsAYear() => Assert.InRange(TrackModel.StormsPerMonth.Sum(), 24, 26);
}
