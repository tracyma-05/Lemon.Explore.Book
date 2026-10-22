using Lemon.Explore.Book.Chapters.Kepler;
using Lemon.Explore.Book.Controls.Space;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class KeplerGeometryTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.0167)]
    [InlineData(0.5)]
    [InlineData(0.967)]
    public void KeplersEquation_IsSolved_ForEveryPhase(double e)
    {
        for (var i = 0; i < 360; i++)
        {
            var m = i * Math.PI / 180;
            var ecc = KeplerGeometry.EccentricAnomaly(m, e);
            Assert.Equal(m, KeplerGeometry.WrapAngle(ecc - (e * Math.Sin(ecc))), 9);
        }
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.967)]
    public void PhaseAt_UndoesAt(double e)
    {
        for (var phase = 0.0; phase < 1; phase += 0.0173)
        {
            var point = KeplerGeometry.At(phase, e);
            Assert.Equal(phase, KeplerGeometry.PhaseAt(point.TrueAnomaly, e), 9);
        }
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(0.9)]
    public void FirstLaw_TheDistancesToTheTwoFoci_AddUpToTheLongAxis(double e)
    {
        // The Sun is at the origin, the other focus 2c = 2ea towards aphelion.
        for (var phase = 0.0; phase < 1; phase += 0.05)
        {
            var p = KeplerGeometry.At(phase, e);
            var toSun = Math.Sqrt((p.X * p.X) + (p.Y * p.Y));
            var toOther = Math.Sqrt(((p.X + (2 * e)) * (p.X + (2 * e))) + (p.Y * p.Y));
            Assert.Equal(p.Radius, toSun, 9);
            Assert.Equal(2, toSun + toOther, 9);
        }
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.967)]
    public void SecondLaw_EqualTimes_SweepEqualAreas(double e)
    {
        // Twelve stretches of equal time, near perihelion and near aphelion alike.
        var expected = KeplerGeometry.EllipseArea(e) / 12;
        for (var k = 0; k < 12; k++)
        {
            var area = KeplerGeometry.SweptAreaBySlices(k / 12.0, (k + 1) / 12.0, e);
            Assert.Equal(expected, area, 4);
            Assert.Equal(expected, KeplerGeometry.SweptArea(k / 12.0, (k + 1) / 12.0, e), 12);
        }
    }

    [Fact]
    public void SecondLaw_MakesThePlanetFastestAtPerihelion()
    {
        const double e = 0.5;
        var perihelion = KeplerGeometry.At(0, e);
        var aphelion = KeplerGeometry.At(0.5, e);

        // Angular momentum r × v is the same: speeds go inversely with the distances, here 3 to 1.
        Assert.Equal(0.5, perihelion.Radius, 9);
        Assert.Equal(1.5, aphelion.Radius, 9);
        Assert.Equal(3, perihelion.Speed / aphelion.Speed, 9);
        Assert.Equal(perihelion.Radius * perihelion.Speed, aphelion.Radius * aphelion.Speed, 9);

        // On a circle the speed is the circular speed everywhere.
        Assert.Equal(1, KeplerGeometry.At(0.37, 0).Speed, 9);
    }

    [Fact]
    public void EarthGoesRoundAtAbout30KmS_FasterInJanuaryThanInJuly()
    {
        Assert.InRange(KeplerGeometry.CircularSpeedAt1Au, 29.7, 29.9);

        var earth = KeplerGeometry.Body("Earth");
        var fastest = KeplerGeometry.SpeedKmS(earth.SemiMajorAxisAu, 1 - earth.Eccentricity);
        var slowest = KeplerGeometry.SpeedKmS(earth.SemiMajorAxisAu, 1 + earth.Eccentricity);
        Assert.InRange(fastest, 30.2, 30.4);
        Assert.InRange(slowest, 29.2, 29.4);
        Assert.InRange(earth.PerihelionAu * KeplerGeometry.AuMetres / 1e9, 146.9, 147.3);
        Assert.InRange(earth.AphelionAu * KeplerGeometry.AuMetres / 1e9, 151.9, 152.3);
    }

    [Fact]
    public void HalleysComet_RacesPastTheSunAndCrawlsOutBeyondNeptune()
    {
        var halley = KeplerGeometry.Body("Halley");
        Assert.InRange(halley.PerihelionAu, 0.57, 0.6);
        Assert.InRange(halley.AphelionAu, 34.5, 35.5);
        Assert.InRange(halley.PeriodYears, 74.5, 76);
        Assert.InRange(KeplerGeometry.SpeedKmS(halley.SemiMajorAxisAu, 1 - halley.Eccentricity), 53, 56);
        Assert.InRange(KeplerGeometry.SpeedKmS(halley.SemiMajorAxisAu, 1 + halley.Eccentricity), 0.8, 1.0);
    }

    [Fact]
    public void ThirdLaw_OneAuIsOneYear_AndNewtonAgrees()
    {
        Assert.InRange(KeplerGeometry.PeriodDays(1), 365.2, 365.3);
        Assert.Equal(1, KeplerGeometry.PeriodYears(1), 12);
        Assert.Equal(8, KeplerGeometry.PeriodYears(4), 12);
        Assert.Equal(4, KeplerGeometry.SemiMajorAxisAu(8), 12);

        // Newton's form and T² = a³ differ only by the sidereal year (365.256 d) against 365.25 d.
        foreach (var a in new[] { 0.39, 5.2, 30.1 })
        {
            Assert.Equal(1, KeplerGeometry.PeriodDays(a) / KeplerGeometry.DaysPerYear / KeplerGeometry.PeriodYears(a), 3);
        }
    }

    [Fact]
    public void ThirdLaw_HoldsForTheMeasuredPeriodsOfEveryPlanet()
    {
        Assert.Contains(KeplerGeometry.PeriodPoints, p => p.Id == "Neptune");
        Assert.Contains(KeplerGeometry.PeriodPoints, p => p.Id == "Halley");
        foreach (var point in KeplerGeometry.PeriodPoints)
        {
            Assert.InRange(point.Ratio, 0.98, 1.02);
        }
    }

    [Theory]
    [InlineData(0.0, OrbitStage.Perihelion)]
    [InlineData(0.25, OrbitStage.Receding)]
    [InlineData(0.5, OrbitStage.Aphelion)]
    [InlineData(0.75, OrbitStage.Approaching)]
    public void Stage_FollowsTheAngleAtTheSun(double phase, OrbitStage expected)
    {
        Assert.Equal(expected, KeplerGeometry.StageAt(phase, 0.3));
        Assert.Equal(OrbitStage.Circle, KeplerGeometry.StageAt(phase, 0.005));
    }

    [Fact]
    public void EarthsNextPerihelionAndAphelion_FallInEarlyJanuaryAndEarlyJuly()
    {
        var orbit = SolarSystemCatalog.Bodies.First(b => b.Id == "Earth").Orbit!;
        var from = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);

        // Published: perihelion 2027-01-03, aphelion 2027-07-05 (the Moon moves Earth's by a day or two).
        var perihelion = KeplerGeometry.NextApsis(orbit, from, perihelion: true);
        var aphelion = KeplerGeometry.NextApsis(orbit, from, perihelion: false);
        Assert.InRange((perihelion - new DateTime(2027, 1, 3, 0, 0, 0, DateTimeKind.Utc)).TotalDays, -3, 3);
        Assert.InRange((aphelion - new DateTime(2027, 7, 5, 0, 0, 0, DateTimeKind.Utc)).TotalDays, -3, 3);

        // The next one after a perihelion is a year on.
        var next = KeplerGeometry.NextApsis(orbit, perihelion.AddHours(1), perihelion: true);
        Assert.InRange((next - perihelion).TotalDays, 365, 366);
    }
}
