using System;
using System.Linq;
using Avalonia;
using Lemon.Explore.Book.Controls.Space;
using Lemon.Explore.Book.Chapters.Rainbow;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class RainbowOpticsTests
{
    [Theory]
    [InlineData(OpticalMedium.Water, 1.3330)]
    [InlineData(OpticalMedium.Glass, 1.5168)]
    [InlineData(OpticalMedium.Diamond, 2.417)]
    public void IndicesMatchTheTablesAtTheSodiumLineAndBlueBendsMore(OpticalMedium medium, double atYellow)
    {
        Assert.Equal(atYellow, RainbowOptics.Index(medium, RainbowOptics.YellowNm), 3);
        Assert.True(RainbowOptics.Index(medium, RainbowOptics.VioletNm) > RainbowOptics.Index(medium, RainbowOptics.RedNm));
    }

    [Theory]
    [InlineData(1.0, 1.333, 30)]
    [InlineData(1.0, 1.52, 60)]
    [InlineData(1.333, 1.0, 40)]
    [InlineData(2.42, 1.0, 20)]
    public void SnellsLawHolds(double n1, double n2, double incidence)
    {
        var refraction = RainbowOptics.Refraction(n1, n2, incidence);
        Assert.Equal(n1 * Math.Sin(incidence * RainbowOptics.Deg), n2 * Math.Sin(refraction * RainbowOptics.Deg), 9);
        Assert.Equal(0, RainbowOptics.Refraction(n1, n2, 0), 9);
    }

    [Fact]
    public void PastTheCriticalAngleAllLightIsReflected()
    {
        var critical = RainbowOptics.CriticalAngle(1.333, 1);
        Assert.Equal(48.6, critical, 1);
        Assert.Equal(24.4, RainbowOptics.CriticalAngle(2.4175, 1), 1);
        Assert.True(double.IsNaN(RainbowOptics.CriticalAngle(1, 1.333)));

        Assert.False(double.IsNaN(RainbowOptics.Refraction(1.333, 1, critical - 0.1)));
        Assert.True(double.IsNaN(RainbowOptics.Refraction(1.333, 1, critical + 0.1)));
        Assert.Equal(1, RainbowOptics.Reflectance(1.333, 1, critical + 0.1), 9);
        Assert.Null(RainbowOptics.Refract(Direction(critical + 0.1), new Vector(0, -1), 1.333, 1));
    }

    [Fact]
    public void FresnelReflectanceStraightOnAtGrazingAndAtBrewstersAngle()
    {
        const double n = 1.333;
        var straight = (n - 1) / (n + 1);
        Assert.Equal(straight * straight, RainbowOptics.Reflectance(1, n, 0), 9);
        Assert.True(RainbowOptics.Reflectance(1, n, 89.9) > 0.98);

        var brewster = RainbowOptics.BrewsterAngle(1, n);
        Assert.Equal(53.1, brewster, 1);
        Assert.Equal(0, RainbowOptics.Reflectances(1, n, brewster).P, 9);

        // Going in or coming out at matching angles reflects the same share.
        var inside = RainbowOptics.Refraction(1, n, 50);
        Assert.Equal(RainbowOptics.Reflectance(1, n, 50), RainbowOptics.Reflectance(n, 1, inside), 9);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(35)]
    [InlineData(70)]
    public void TheVectorFormAgreesWithSnellsLaw(double incidence)
    {
        // Light going down (+y) into a surface whose normal on the light's side points up (−y).
        var refracted = RainbowOptics.Refract(Direction(incidence), new Vector(0, -1), 1, 1.5);
        Assert.NotNull(refracted);
        var angle = Math.Atan2(refracted!.Value.X, refracted.Value.Y) / RainbowOptics.Deg;
        Assert.Equal(RainbowOptics.Refraction(1, 1.5, incidence), angle, 9);
    }

    [Fact]
    public void APrismTurnsLeastWhenTheRayGoesThroughSymmetrically()
    {
        const double n = 1.5168;
        var best = RainbowOptics.MinimumDeviationIncidence(n, 60);
        var least = RainbowOptics.MinimumDeviation(n, 60);
        Assert.Equal(38.6, least, 1);
        Assert.Equal(least, RainbowOptics.PrismDeviation(n, 60, best), 9);
        foreach (var incidence in new[] { 35.0, 45, 55, 70, 85 })
        {
            Assert.True(RainbowOptics.PrismDeviation(n, 60, incidence) > least);
        }

        // Too flat a ray cannot get out of the second face.
        Assert.True(double.IsNaN(RainbowOptics.PrismDeviation(n, 60, 20)));
    }

    [Theory]
    [InlineData(40)]
    [InlineData(49.6)]
    [InlineData(65)]
    public void TracingAPrismGivesTheSameTurnAsTheFormula(double incidence)
    {
        const double n = 1.5168;
        var triangle = new[] { new Point(0, -Math.Sqrt(3) / 2), new Point(0.5, 0), new Point(-0.5, 0) };

        // Aim at the middle of the left face from outside, at the given angle from its normal.
        var faceMiddle = new Point(-0.25, -Math.Sqrt(3) / 4);
        var outward = new Vector(-Math.Cos(30 * RainbowOptics.Deg), -Math.Sin(30 * RainbowOptics.Deg));
        var inward = -outward;
        var direction = Rotate(inward, -incidence);
        var segments = RainbowOptics.TracePolygon(triangle, faceMiddle - (direction * 2), direction, n, 3);

        Assert.Equal(3, segments.Count);
        Assert.True(segments[1].Inside);
        var exit = segments[2].To - segments[2].From;
        var turn = SpaceDrawing.AngleBetween(direction, exit);
        Assert.Equal(RainbowOptics.PrismDeviation(n, 60, incidence), Math.Abs(turn), 6);
    }

    [Fact]
    public void ThePrimaryBowIsAt42DegreesAndTheSecondaryAt51WithTheirColoursReversed()
    {
        var n = RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.YellowNm);
        Assert.InRange(RainbowOptics.RainbowAngle(1, n), 41.8, 42.3);
        Assert.InRange(RainbowOptics.RainbowAngle(2, n), 50.5, 51.3);

        var red = RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.RedNm);
        var violet = RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.VioletNm);
        Assert.True(RainbowOptics.RainbowAngle(1, red) > RainbowOptics.RainbowAngle(1, violet));
        Assert.True(RainbowOptics.RainbowAngle(2, red) < RainbowOptics.RainbowAngle(2, violet));
    }

    [Fact]
    public void NoRayComesOutWiderThanThePrimaryOrNarrowerThanTheSecondary()
    {
        var n = RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.YellowNm);
        var primary = Enumerable.Range(0, 1001).Select(i => RainbowOptics.ExitAngle(1, n, i / 1000.0)).ToArray();
        var secondary = Enumerable.Range(0, 1001).Select(i => RainbowOptics.ExitAngle(2, n, i / 1000.0)).ToArray();
        Assert.Equal(RainbowOptics.RainbowAngle(1, n), primary.Max(), 2);
        Assert.Equal(RainbowOptics.RainbowAngle(2, n), secondary.Min(), 2);
        Assert.Equal(0.86, RainbowOptics.RainbowImpact(1, n), 2);

        // Straight through the middle the light comes straight back; the secondary's goes on toward the Sun.
        Assert.Equal(0, primary[0], 6);
        Assert.Equal(180, secondary[0], 6);
    }

    [Theory]
    [InlineData(1, 0.3)]
    [InlineData(1, 0.86)]
    [InlineData(1, 0.97)]
    [InlineData(2, -0.8)]
    [InlineData(2, -0.95)]
    public void TracingADropLeavesAtTheAngleOfTheFormulaAndHeadsDown(int order, double height)
    {
        var n = RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.YellowNm);
        var path = RainbowOptics.TraceDrop(height, n, order);

        Assert.Equal(order + 2, path.Points.Count);
        Assert.All(path.Points, p => Assert.Equal(1, ((Vector)p).Length, 9));

        // Back toward the Sun's side and downward, to someone below with the Sun behind them.
        Assert.True(path.Exit.X < 0);
        Assert.True(path.Exit.Y < 0);
        var fromAntisolar = SpaceDrawing.AngleBetween(new Vector(1, 0), -path.Exit);
        Assert.Equal(RainbowOptics.ExitAngle(order, n, height), Math.Abs(fromAntisolar), 6);

        // Light is only shared out, never made: what leaks plus what leaves is less than came in.
        Assert.Equal(RainbowOptics.Reflectance(1, n, Math.Asin(Math.Abs(height)) / RainbowOptics.Deg), path.Leaks[0].Power, 9);
        Assert.True(path.Leaks.Sum(l => l.Power) + path.ExitPower < 1);
    }

    [Fact]
    public void OnlyAFewPercentOfTheLightMakesTheBowAndItIsStronglyPolarised()
    {
        var n = RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.YellowNm);
        var primary = RainbowOptics.Throughput(1, n, RainbowOptics.RainbowImpact(1, n));
        var secondary = RainbowOptics.Throughput(2, n, RainbowOptics.RainbowImpact(2, n));
        Assert.InRange(primary, 0.03, 0.06);
        Assert.True(secondary < primary);
        Assert.True(RainbowOptics.Polarisation(1, n) > 0.9);
    }

    [Fact]
    public void TheSkyIsBrightInsideThePrimaryDarkBetweenTheBowsAndTheBowPeaksAt42()
    {
        var primary = RainbowOptics.Profile(1, RainbowOptics.YellowNm, 1);
        var secondary = RainbowOptics.Profile(2, RainbowOptics.YellowNm, 1);
        int At(double degrees) => (int)Math.Round(degrees * RainbowOptics.ProfileSteps);

        var peak = Array.IndexOf(primary, primary.Max()) / (double)RainbowOptics.ProfileSteps;
        Assert.InRange(peak, 41.2, 42.3);
        var secondPeak = Array.IndexOf(secondary, secondary.Skip(At(45)).Max()) / (double)RainbowOptics.ProfileSteps;
        Assert.InRange(secondPeak, 50.6, 51.8);

        // Inside the bow the sky is lit; in Alexander's dark band neither bow sends any light.
        Assert.True(primary[At(30)] > 0.02 * primary.Max());
        Assert.Equal(0, primary[At(47)], 9);
        Assert.Equal(0, secondary[At(47)], 9);
        Assert.True(secondary.Max() < primary.Max());
    }

    [Fact]
    public void FogDropsSmearTheBowSoItsColoursRunTogether()
    {
        var spreadInRain = RainbowOptics.Smearing(RainbowOptics.YellowNm, 1);
        var spreadInFog = RainbowOptics.Smearing(RainbowOptics.YellowNm, 0.02);
        var red = RainbowOptics.RainbowAngle(1, RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.RedNm));
        var violet = RainbowOptics.RainbowAngle(1, RainbowOptics.Index(OpticalMedium.Water, RainbowOptics.VioletNm));

        // In rain the bow is narrower than the gap between red and violet; in fog it is wider.
        Assert.True(spreadInRain < (red - violet) / 2);
        Assert.True(spreadInFog > red - violet);
    }

    [Fact]
    public void ALowSunGivesAHalfCircleAndAHighSunNoBowAtAll()
    {
        Assert.Equal(0.5, RainbowOptics.ShareAboveHorizon(42, 0), 9);
        Assert.Equal(0, RainbowOptics.ShareAboveHorizon(42, 45), 9);
        Assert.InRange(RainbowOptics.ShareAboveHorizon(42, 20), 0.01, 0.49);
        Assert.Equal(22, RainbowOptics.TopAltitude(42, 20), 9);

        // The top of the bow is where the formula for the sky's angle says 42°.
        Assert.Equal(42, RainbowOptics.AngleFromAntisolar(22, 0, 20), 9);
        Assert.Equal(20, RainbowOptics.AngleFromAntisolar(0, 0, 20), 9);
    }

    /// <summary>Going down (+y) at <paramref name="incidence"/> degrees from straight down, leaning to +x.</summary>
    private static Vector Direction(double incidence) =>
        new(Math.Sin(incidence * RainbowOptics.Deg), Math.Cos(incidence * RainbowOptics.Deg));

    private static Vector Rotate(Vector v, double degrees)
    {
        var c = Math.Cos(degrees * RainbowOptics.Deg);
        var s = Math.Sin(degrees * RainbowOptics.Deg);
        return new Vector((v.X * c) - (v.Y * s), (v.X * s) + (v.Y * c));
    }
}
