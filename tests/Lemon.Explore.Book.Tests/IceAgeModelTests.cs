using System;
using System.Linq;
using Lemon.Explore.Book.Chapters.IceAges;
using Xunit;

namespace Lemon.Explore.Book.Tests;

/// <summary>The ice-age page's astronomy (Berger 1978 main terms) and its ice model (Paillard 1998).</summary>
public sealed class IceAgeModelTests
{
    [Fact]
    public void Elements_In1950_AreThePublishedValues()
    {
        var now = IceAgeModel.Elements(0);
        Assert.Equal(0.016726, now.Eccentricity, 6);
        Assert.Equal(23.446, now.Obliquity, 6);
        Assert.Equal(102.04, now.Perihelion, 6);
        Assert.Equal(0, now.Precession, 9);

        // e·sin ϖ is about +0.0164: the far point falls in northern summer.
        Assert.InRange(now.PrecessionIndex, 0.0160, 0.0168);
    }

    [Fact]
    public void Elements_StayInTheirKnownRanges()
    {
        double eMin = 1, eMax = 0, oMin = 90, oMax = 0;
        for (var kyr = -1000.0; kyr <= 100; kyr += 0.5)
        {
            var o = IceAgeModel.Elements(kyr);
            eMin = Math.Min(eMin, o.Eccentricity);
            eMax = Math.Max(eMax, o.Eccentricity);
            oMin = Math.Min(oMin, o.Obliquity);
            oMax = Math.Max(oMax, o.Obliquity);
        }

        Assert.InRange(eMin, 0, 0.005);
        Assert.InRange(eMax, 0.045, 0.065);
        Assert.InRange(oMin, 21.9, 22.4);
        Assert.InRange(oMax, 24.2, 24.6);
    }

    [Fact]
    public void Tilt_PeakedAbout10000YearsAgo_AndRepeatsEvery41000()
    {
        var peak = Enumerable.Range(0, 300).Select(i => -i * 0.1).MaxBy(k => IceAgeModel.Elements(k).Obliquity);
        Assert.InRange(peak, -11, -8.5);
        Assert.InRange(IceAgeModel.Elements(peak).Obliquity, 24.1, 24.35);

        // The next peak back is about 41,000 years earlier.
        var earlier = Enumerable.Range(0, 200).Select(i => peak - 30 - (i * 0.1)).MaxBy(k => IceAgeModel.Elements(k).Obliquity);
        Assert.InRange(peak - earlier, 39, 43);
    }

    [Fact]
    public void Perihelion_IsInEarlyJanuaryNow_AndWasInNorthernSummer11000YearsAgo()
    {
        var day = IceAgeModel.PerihelionDay(IceAgeModel.Elements(0));
        Assert.InRange(day, 0.5, 5);

        var then = IceAgeModel.Elements(-11);
        Assert.InRange(then.PerihelionLongitude, 60, 120);
        Assert.True(then.PrecessionIndex < -0.015);
    }

    [Fact]
    public void NorthernSummerHalf_IsLongerNow()
    {
        // Spring to autumn equinox is about 186 days now, because Earth is far from the Sun then and moves slowly.
        var days = IceAgeModel.NorthernSummerDays(IceAgeModel.Elements(0));
        Assert.InRange(days, 185.5, 187.5);
    }

    [Fact]
    public void Sunshine_At65North_MatchesTheKnownValues()
    {
        // About 478 W/m² at midsummer now, ~525 at the Holocene's start, ~440 at 115,000 years ago, ~535 at 125,000.
        Assert.InRange(IceAgeModel.SummerSunshine(0), 472, 484);
        Assert.InRange(IceAgeModel.SummerSunshine(-11), 515, 535);
        Assert.InRange(IceAgeModel.SummerSunshine(-115), 432, 450);
        Assert.InRange(IceAgeModel.SummerSunshine(-125), 525, 545);
    }

    [Fact]
    public void Sunshine_AtTheEquinox_IsTheSameEverywhereOnTheEquatorAsAFlatCosineLaw()
    {
        // At the equator on the equinox the Sun is overhead at noon: S/π/r² over the day.
        var orbit = new OrbitalElements(0, 23.44, 102, 0);
        Assert.Equal(IceAgeModel.SolarConstant / Math.PI, IceAgeModel.DailyInsolation(orbit, 0, 0), 6);

        // In the polar night it is 0; at the pole at midsummer it beats the equator.
        Assert.Equal(0, IceAgeModel.DailyInsolation(orbit, 80, 270), 6);
        Assert.True(IceAgeModel.DailyInsolation(orbit, 90, 90) > IceAgeModel.DailyInsolation(orbit, 0, 90));
    }

    [Fact]
    public void Kepler_AnomalyAfterPerihelion_RoundTrips()
    {
        foreach (var e in new[] { 0.0, 0.0167, 0.05 })
        {
            Assert.Equal(0, IceAgeModel.AnomalyAfterPerihelion(e, 0), 6);
            Assert.Equal(180, IceAgeModel.AnomalyAfterPerihelion(e, 0.5), 6);
        }

        // Faster near perihelion: a quarter year after it Earth is past 90°.
        Assert.True(IceAgeModel.AnomalyAfterPerihelion(0.05, 0.25) > 95);
    }

    [Fact]
    public void Pole_IsNearPolarisNow_NearVegaIn12000Years_NearThubanAbout5000YearsAgo()
    {
        var stars = IceAgeModel.PoleStars.ToDictionary(s => s.Id, s => IceAgeModel.Ecliptic(s.Ra, s.Dec));
        Assert.InRange(IceAgeModel.Separation(IceAgeModel.Pole(0.05), stars["polaris"]), 0, 1.2);
        var vega = Enumerable.Range(0, 400).Select(i => i * 0.05).MinBy(k => IceAgeModel.Separation(IceAgeModel.Pole(k), stars["vega"]));
        Assert.InRange(vega, 11, 14.5);
        Assert.InRange(IceAgeModel.Separation(IceAgeModel.Pole(vega), stars["vega"]), 2, 7);
        var thuban = Enumerable.Range(0, 200).Select(i => -i * 0.05).MinBy(k => IceAgeModel.Separation(IceAgeModel.Pole(k), stars["thuban"]));
        Assert.InRange(thuban, -5.5, -4);
    }

    [Fact]
    public void OneCycle_HoldsTheOthersAtTodaysValues()
    {
        var now = IceAgeModel.Elements(0);
        var tilt = IceAgeModel.Elements(-30, IceAgeCycle.Tilt);
        Assert.Equal(now.Eccentricity, tilt.Eccentricity, 9);
        Assert.Equal(now.Perihelion, tilt.Perihelion, 9);
        Assert.Equal(IceAgeModel.Elements(-30).Obliquity, tilt.Obliquity, 9);

        var wobble = IceAgeModel.Elements(-11, IceAgeCycle.Precession);
        Assert.Equal(now.Eccentricity, wobble.Eccentricity, 9);
        Assert.Equal(now.Obliquity, wobble.Obliquity, 9);
    }

    [Fact]
    public void IceModel_FindsTheLastDeglaciations()
    {
        // The ocean record's terminations: about 14–19, 130–136, 243–252, 335–340, 424–430 thousand years ago.
        var ends = IceRecord.Instance.Terminations.Where(t => t > -450).OrderByDescending(t => t).ToList();
        Assert.True(ends.Count >= 5, string.Join(", ", ends));
        Assert.InRange(ends[0], -20, -13);
        Assert.InRange(ends[1], -138, -128);
        Assert.InRange(ends[2], -254, -240);
        Assert.InRange(ends[3], -344, -333);
        Assert.InRange(ends[4], -434, -420);
    }

    [Fact]
    public void IceModel_HasALastGlacialMaximumAndAWarmHolocene()
    {
        var record = IceRecord.Instance;
        var lgm = record.At(-21);
        Assert.Equal(IceState.FullGlacial, lgm.State);
        Assert.InRange(lgm.SeaLevel, -135, -100);
        Assert.InRange(lgm.Co2, 180, 200);

        var now = record.At(0);
        Assert.Equal(IceState.Interglacial, now.State);
        Assert.InRange(now.SeaLevel, -5, 5);
        Assert.InRange(now.Co2, 270, 290);
        Assert.Equal(IceState.Interglacial, record.At(-125).State);

        // Without people's CO₂ the model's next ice age starts within about 60,000 years.
        var next = Enumerable.Range(1, 1000).Select(i => i * 0.1).First(k => record.At(k).State != IceState.Interglacial);
        Assert.InRange(next, 35, 65);
    }

    [Fact]
    public void IceModel_GroundLagsTheIce()
    {
        var record = IceRecord.Instance;

        // During the fast melt after the last maximum the ground is still pressed down by ice that is already gone.
        var melt = record.At(-12);
        Assert.True(melt.Ground > melt.Ice);
        Assert.True(record.IceTrend(-12) < 0);
        Assert.True(record.IceTrend(-40) > -0.05);
    }
}
