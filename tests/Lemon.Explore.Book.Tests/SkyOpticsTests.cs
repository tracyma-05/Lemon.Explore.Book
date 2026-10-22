using System;
using System.Linq;
using Lemon.Explore.Book.Chapters.BlueSky;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class SkyOpticsTests
{
    [Fact]
    public void VioletScattersAboutNineTimesAsMuchAsRed()
    {
        Assert.Equal(Math.Pow(700.0 / 400, 4), SkyOptics.Rayleigh(400) / SkyOptics.Rayleigh(700), 9);
        Assert.InRange(SkyOptics.Rayleigh(400) / SkyOptics.Rayleigh(700), 9.3, 9.4);

        // The usual sea-level values, per metre.
        Assert.Equal(13.6e-6, SkyOptics.Rayleigh(550), 7);
        Assert.Equal(33.1e-6, SkyOptics.Rayleigh(440), 6);
    }

    [Fact]
    public void HazeScattersEveryColourNearlyAlike()
    {
        var haze = SkyOptics.Mie(400, 0.5) / SkyOptics.Mie(700, 0.5);
        Assert.InRange(haze, 1.3, 1.7);
        Assert.True(SkyOptics.Mie(550, 1) > SkyOptics.Mie(550, 0) * 10);
    }

    [Fact]
    public void StraightUpTheAirIsOneScaleHeightDeep()
    {
        var up = SkyOptics.ColumnToSpace(0, 1);
        Assert.False(up.Blocked);
        Assert.Equal(SkyOptics.RayleighScaleHeight, up.Rayleigh, SkyOptics.RayleighScaleHeight * 0.01);
        Assert.Equal(SkyOptics.MieScaleHeight, up.Mie, SkyOptics.MieScaleHeight * 0.01);

        // Ozone: a layer 30 km wide peaking at 25 km holds as much as 15 km at its peak density.
        Assert.Equal(15_000, up.Ozone, 300.0);
    }

    [Fact]
    public void AlongTheHorizonTheLightCrossesAboutThirtyFiveTimesAsMuchAir()
    {
        var up = SkyOptics.ColumnToSpace(0, 1);
        var level = SkyOptics.ColumnToSpace(0, 0);
        Assert.InRange(level.Rayleigh / up.Rayleigh, 33, 40);

        // A slanting ray crosses about 1/cos of the air when the Sun is well up.
        var thirty = SkyOptics.ColumnToSpace(0, Math.Sin(30 * Math.PI / 180));
        Assert.Equal(2, thirty.Rayleigh / up.Rayleigh, 0.02);
    }

    [Fact]
    public void ARayBelowTheHorizonRunsIntoTheGround()
    {
        Assert.True(SkyOptics.ColumnToSpace(0, -0.01).Blocked);

        // From 10 km up, the horizon dips by about 3.2°, so a ray 2° down still gets out.
        Assert.False(SkyOptics.ColumnToSpace(10_000, Math.Sin(-2 * Math.PI / 180)).Blocked);
        Assert.True(SkyOptics.ColumnToSpace(10_000, Math.Sin(-4 * Math.PI / 180)).Blocked);
    }

    [Theory]
    [InlineData(90, 1.0, 0.001)]
    [InlineData(30, 1.995, 0.01)]
    [InlineData(10, 5.6, 0.05)]
    [InlineData(0, 37.9, 0.2)]
    public void AirMassFollowsKastenAndYoung(double altitude, double expected, double tolerance) =>
        Assert.Equal(expected, SkyOptics.AirMass(altitude), tolerance);

    [Fact]
    public void AtSunsetBlueIsStrippedOutOfTheSunlightAndRedComesThrough()
    {
        const double haze = 0.3;
        var noonBlue = SkyOptics.TransmittanceAt(SkyOptics.BlueNm, 60, haze);
        var noonRed = SkyOptics.TransmittanceAt(SkyOptics.RedNm, 60, haze);
        Assert.InRange(noonBlue, 0.7, 0.9);
        Assert.InRange(noonRed, 0.85, 0.97);

        var lowBlue = SkyOptics.TransmittanceAt(SkyOptics.BlueNm, 1, haze);
        var lowRed = SkyOptics.TransmittanceAt(SkyOptics.RedNm, 1, haze);
        Assert.True(lowBlue < 0.02);
        Assert.True(lowRed > lowBlue * 10);

        Assert.Equal(0, SkyOptics.TransmittanceAt(SkyOptics.BlueNm, -1, haze));
        Assert.True(SkyOptics.BlueToRed(60, haze) > 0.8);
        Assert.True(SkyOptics.BlueToRed(2, haze) < 0.2);
    }

    [Fact]
    public void SunlightInSpaceIsWhite()
    {
        var white = SkyOptics.ToRgb(Enumerable.Repeat(1.0, SkyOptics.Bands).ToArray());
        Assert.Equal(1, white.R, 9);
        Assert.Equal(1, white.G, 9);
        Assert.Equal(1, white.B, 9);
    }

    [Fact]
    public void TheSunAtNoonIsNearlyWhiteAndOnTheHorizonOrangeRed()
    {
        var noon = SkyOptics.ToRgb(SkyOptics.Transmittance(60, 0.3));
        Assert.True(noon.B / noon.R > 0.8);

        var low = SkyOptics.ToRgb(SkyOptics.Transmittance(1, 0.3));
        Assert.True(low.R > low.G && low.G > low.B);
        Assert.True(low.B / low.R < 0.15);
    }

    [Fact]
    public void ScatteredSkylightIsBlue()
    {
        var sky = SkyOptics.ToRgb(SkyOptics.Wavelengths.Select(SkyOptics.Rayleigh).ToArray());
        Assert.True(sky.B > sky.G && sky.G > sky.R);
    }

    [Fact]
    public void SpectralColoursGoFromVioletToRed()
    {
        var blue = SkyOptics.SpectralColour(450);
        var green = SkyOptics.SpectralColour(530);
        var red = SkyOptics.SpectralColour(650);
        Assert.True(blue.B > blue.R && blue.B > blue.G);
        Assert.True(green.G > green.R && green.G > green.B);
        Assert.True(red.R > red.G && red.R > red.B);
    }

    [Fact]
    public void TheDayIsAnEquinoxAtThirtyDegreesNorth()
    {
        Assert.Equal(60, SkyOptics.SunAltitude(12), 9);
        Assert.Equal(0, SkyOptics.SunAltitude(6), 9);
        Assert.Equal(0, SkyOptics.SunAltitude(18), 9);
        Assert.Equal(0, SkyOptics.SunAzimuth(12), 9);
        Assert.Equal(-90, SkyOptics.SunAzimuth(6), 6);
        Assert.Equal(90, SkyOptics.SunAzimuth(18), 6);
        Assert.True(SkyOptics.SunAltitude(SkyOptics.LastHour) < -12);
    }

    [Theory]
    [InlineData(45, false)]
    [InlineData(10, true)]
    [InlineData(-4, true)]
    [InlineData(0, false)]
    public void HourAtUndoesSunAltitude(double altitude, bool afternoon)
    {
        var hour = SkyOptics.HourAt(altitude, afternoon);
        Assert.Equal(afternoon, hour > 12);
        Assert.Equal(altitude, SkyOptics.SunAltitude(hour), 6);
    }

    [Theory]
    [InlineData(12, SkyPhase.Day)]
    [InlineData(17.6, SkyPhase.GoldenHour)]
    [InlineData(18, SkyPhase.Sunset)]
    [InlineData(6, SkyPhase.Sunrise)]
    [InlineData(18.25, SkyPhase.Twilight)]
    [InlineData(18.7, SkyPhase.NauticalTwilight)]
    [InlineData(19, SkyPhase.Night)]
    public void ThePartOfTheDayFollowsTheSun(double hour, SkyPhase expected) => Assert.Equal(expected, SkyOptics.PhaseAt(hour));
}
