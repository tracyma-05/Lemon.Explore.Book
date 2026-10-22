using System;
using Lemon.Explore.Book.Chapters.Aurora;
using Lemon.Explore.Book.Chapters.DayNight;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class AuroraModelTests
{
    [Fact]
    public void QuietSolarWind_PutsTheMagnetopauseAboutTenEarthRadiiOut()
    {
        // 5 protons/cm³ at 400 km/s push with about 1.3 nPa; Shue et al. put the nose near 10–11 Earth radii.
        var pressure = AuroraModel.DynamicPressure(5, 400);
        Assert.InRange(pressure, 1.25, 1.45);
        Assert.InRange(AuroraModel.Standoff(pressure, 0), 10, 11.5);
    }

    [Fact]
    public void AStrongPushAndSouthwardField_PressTheMagnetopauseInsideGeostationaryOrbit()
    {
        var pressure = AuroraModel.DynamicPressure(40, 800);
        Assert.True(AuroraModel.Standoff(pressure, -20) < AuroraModel.Geostationary);
        Assert.True(AuroraModel.Standoff(2, -10) < AuroraModel.Standoff(2, 5));
    }

    [Fact]
    public void TheBowShockStandsAFewEarthRadiiOutsideTheMagnetopause()
    {
        var nose = AuroraModel.Standoff(1.3, 0);
        var shock = AuroraModel.BowShockStandoff(nose, AuroraModel.MachNumber(5, 400, -5));
        Assert.InRange(shock - nose, 2, 5);
    }

    [Fact]
    public void ShueShape_FlaresOutTowardsTheTail()
    {
        Assert.Equal(10, AuroraModel.ShapeRadius(0, 10, 0.58), 6);
        Assert.InRange(AuroraModel.ShapeRadius(90, 10, 0.58), 14.5, 15.5);
        Assert.True(AuroraModel.ShapeRadius(150, 10, 0.58) > AuroraModel.ShapeRadius(90, 10, 0.58));
    }

    [Theory]
    [InlineData(400, 5, 5, 0, 2.5)]
    [InlineData(400, 5, -5, 2, 4.5)]
    [InlineData(500, 10, -10, 5, 8)]
    [InlineData(900, 30, -30, 9, 9)]
    public void EstimatedKp_RisesWithFastWindAndSouthwardField(double speed, double density, double bz, double low, double high)
    {
        Assert.InRange(AuroraModel.EstimatedKp(density, speed, bz), low, high);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(4, 60)]
    [InlineData(6.6, 67.1)]
    public void FieldLinesMeetTheGroundAtTheDipoleLatitude(double shell, double latitude)
    {
        Assert.Equal(latitude, AuroraModel.FootLatitude(shell), 1);
    }

    [Fact]
    public void FieldRatio_IsOneAtTheEquatorAndGrowsTowardsThePoles()
    {
        Assert.Equal(1, AuroraModel.FieldRatio(0), 9);
        // At 30°: √(1 + 3/4) / cos⁶ 30° = 1.3229 / 0.4219.
        Assert.Equal(3.1356, AuroraModel.FieldRatio(30), 3);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(45)]
    [InlineData(20)]
    [InlineData(5)]
    public void MirrorLatitude_IsWhereTheFieldIsOneOverSineSquaredOfThePitchAngle(double pitch)
    {
        var mirror = AuroraModel.MirrorLatitude(pitch);
        var s = Math.Sin(pitch * Math.PI / 180);
        Assert.Equal(1 / (s * s), AuroraModel.FieldRatio(mirror), 3);
    }

    [Theory]
    [InlineData(4, 5.5)]
    [InlineData(6.6, 2.7)]
    public void LossCone_IsAFewDegreesInTheOuterBelt(double shell, double degrees)
    {
        Assert.InRange(AuroraModel.LossCone(shell), degrees - 0.3, degrees + 0.3);
    }

    [Fact]
    public void Gyration_TakesTheTextbookTimes()
    {
        // An electron gyrates at 28 Hz per nT, a proton at 15.2 mHz per nT (slow electrons, so no relativity).
        var electron = AuroraModel.Motion(AuroraSpecies.Electron, 0.01, 4, 90);
        var field = AuroraModel.EquatorialField / 64;
        Assert.Equal(1 / (27.99 * field), electron.GyroPeriod, 6);
        var proton = AuroraModel.Motion(AuroraSpecies.Proton, 0.01, 4, 90);
        Assert.Equal(1 / (0.015245 * field), proton.GyroPeriod, 2);
    }

    [Fact]
    public void OneMeVElectronAtGeostationaryOrbit_DriftsRoundEarthInAboutTenMinutes()
    {
        var motion = AuroraModel.Motion(AuroraSpecies.Electron, 1000, 6.6, 90);
        Assert.InRange(motion.DriftPeriod / 60, 8, 12);
        Assert.InRange(motion.BouncePeriod, 0.3, 0.5);
        Assert.InRange(motion.Speed, 0.93 * 299_792, 0.95 * 299_792);
    }

    [Fact]
    public void Protons_DriftSlowerThanElectronsOfTheSameEnergyOnlyByRelativity()
    {
        var electron = AuroraModel.Motion(AuroraSpecies.Electron, 10, 4, 45);
        var proton = AuroraModel.Motion(AuroraSpecies.Proton, 10, 4, 45);
        // The drift depends on the energy, not the mass (until the speed nears light's).
        Assert.Equal(1, proton.DriftPeriod / electron.DriftPeriod, 1);
        Assert.True(proton.BouncePeriod > 40 * electron.BouncePeriod);
    }

    [Fact]
    public void GeomagneticCoordinates_RoundTrip()
    {
        foreach (var (lat, lon) in new[] { (28.23, 112.94), (-33.87, 151.21), (64.1, -21.9), (0.0, 0.0) })
        {
            var (mlat, mlon) = AuroraModel.ToMagnetic(lat, lon);
            var back = AuroraModel.FromMagnetic(mlat, mlon);
            Assert.Equal(lat, back.Latitude, 6);
            Assert.Equal(0, Math.Abs(Math.IEEERemainder(back.Longitude - lon, 360)), 6);
        }
    }

    [Theory]
    [InlineData(28.23, 112.94, 17, 21)] // Changsha: far from the oval.
    [InlineData(64.84, -147.72, 64, 67)] // Fairbanks: under it.
    [InlineData(69.65, 18.96, 66, 69)] // Tromsø.
    [InlineData(51.51, -0.13, 52, 55)] // London.
    public void MagneticLatitude_OfKnownPlaces(double lat, double lon, double low, double high)
    {
        Assert.InRange(AuroraModel.ToMagnetic(lat, lon).Latitude, low, high);
    }

    [Fact]
    public void MagneticLocalTime_IsNoonUnderTheSunAndRunsWithTheDay()
    {
        var utc = new DateTime(2025, 3, 20, 12, 0, 0, DateTimeKind.Utc);
        var sun = DayNightGeometry.Sun(utc);
        Assert.Equal(12, AuroraModel.MagneticLocalTime(utc, sun.Declination, sun.SubsolarLongitude), 6);
        var later = AuroraModel.MagneticLocalTime(utc.AddHours(6), 60, 20);
        var now = AuroraModel.MagneticLocalTime(utc, 60, 20);
        Assert.InRange(AuroraModel.Wrap24(later - now), 5.9, 6.1);
    }

    [Fact]
    public void Oval_GrowsTowardsTheEquatorWithKpAndSitsHigherOnTheDaySide()
    {
        Assert.Equal(66.5, AuroraModel.OvalEquatorward(0, 0), 6);
        Assert.Equal(48.5, AuroraModel.OvalEquatorward(9, 0), 6);
        Assert.True(AuroraModel.OvalEquatorward(3, 12) > AuroraModel.OvalEquatorward(3, 0));
        Assert.True(AuroraModel.OvalPoleward(3, 0) > AuroraModel.OvalEquatorward(3, 0));
        Assert.True(AuroraModel.OvalBrightness(5, 23) > AuroraModel.OvalBrightness(5, 11));
    }

    [Fact]
    public void Elevation_FollowsEarthsCurve()
    {
        Assert.Equal(90, AuroraModel.Elevation(110, 0), 6);
        // A light 110 km up drops below the horizon about 1180 km away.
        Assert.InRange(AuroraModel.Elevation(110, 1100), 0, 1);
        Assert.True(AuroraModel.Elevation(110, 1250) < 0);
        // And the height on a line of sight inverts it.
        var e = AuroraModel.Elevation(250, 800);
        Assert.Equal(250, AuroraModel.HeightOnSight(e, 800), 6);
        Assert.Equal(3, AuroraModel.Elevation(400, AuroraModel.SightDistance(400)), 6);
    }

    [Fact]
    public void FromFarSouth_OnlyAStormBeyondTheScaleShowsTheAurora()
    {
        Assert.True(AuroraModel.KpToSee(19) > 9);
        Assert.True(AuroraModel.KpToSee(55) < 4);
        Assert.Equal(0, AuroraModel.KpToSee(66));
    }

    [Fact]
    public void Light_IsGreenLowDownAndRedHighUp()
    {
        Assert.True(AuroraModel.GreenSurvives(120) > 0.85);
        Assert.True(AuroraModel.GreenSurvives(90) < 0.25);
        Assert.True(AuroraModel.RedSurvives(150) < 0.05);
        Assert.True(AuroraModel.RedSurvives(300) > 0.4);

        var low = AuroraModel.Light(115, 10);
        Assert.True(low.Green > 5 * low.Red);
        var high = AuroraModel.Light(320, 0.5);
        Assert.True(high.Red > high.Green);
    }

    [Fact]
    public void SlowElectrons_MakeATallerCurtainThatIsSeenFromFarther()
    {
        var soft = AuroraModel.VisibleTop(0.5);
        var hard = AuroraModel.VisibleTop(10);
        Assert.True(soft > hard + 80);
        Assert.InRange(AuroraModel.VisibleTop(5), 160, 260);
        Assert.True(AuroraModel.KpToSee(45, soft) < AuroraModel.KpToSee(45, hard));
    }

    [Fact]
    public void FasterElectrons_ReachLower()
    {
        Assert.True(AuroraModel.PeakHeight(10) < AuroraModel.PeakHeight(1));
        Assert.InRange(AuroraModel.PeakHeight(10), 100, 115);
        Assert.Equal(1, AuroraModel.Deposition(AuroraModel.PeakHeight(5), 5), 6);
        Assert.True(AuroraModel.Deposition(AuroraModel.PeakHeight(5) - 25, 5) < 0.05);
    }

    [Theory]
    [InlineData(AuroraSpecies.Electron, 30)]
    [InlineData(AuroraSpecies.Proton, 60)]
    public void TrappedParticle_KeepsItsSpeedAndTurnsBackAtTheMirrorLatitude(AuroraSpecies species, double pitch)
    {
        var particle = new TrappedParticle(species, 4, pitch);
        particle.Advance(particle.BounceTime * 1.2);
        Assert.False(particle.IsLost);
        Assert.Equal(1, particle.Velocity.Length, 9);
        var mirror = AuroraModel.MirrorLatitude(pitch);
        Assert.Equal(mirror, particle.HighestNorth, 1);
        Assert.Equal(mirror, particle.HighestSouth, 1);
        Assert.Equal(2, particle.EquatorCrossings);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(85)]
    public void TrappedParticle_BouncesInTheTimeOfSchulzAndLanzerottisFormula(double pitch)
    {
        // Their T(y) is a fit good to about 1%; the particle's bounce is integrated from the field itself.
        var particle = new TrappedParticle(AuroraSpecies.Electron, 5, pitch);
        var formula = 4 * 5 * AuroraModel.BounceIntegrals(pitch).T;
        Assert.InRange(particle.BounceTime / formula, 0.98, 1.02);
    }

    [Fact]
    public void TrappedParticles_DriftElectronsEastAndProtonsWest()
    {
        var electron = new TrappedParticle(AuroraSpecies.Electron, 4, 60);
        var proton = new TrappedParticle(AuroraSpecies.Proton, 4, 60);
        electron.Advance(electron.BounceTime * 3);
        proton.Advance(proton.BounceTime * 3);
        Assert.True(electron.Longitude > 5);
        Assert.True(proton.Longitude < -5);
    }

    [Fact]
    public void AParticleInsideTheLossCone_ReachesTheAir()
    {
        var particle = new TrappedParticle(AuroraSpecies.Electron, 4, 2);
        particle.Advance(particle.BounceTime);
        Assert.True(particle.IsLost);
    }
}
