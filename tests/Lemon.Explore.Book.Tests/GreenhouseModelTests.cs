using System;
using System.Linq;
using Lemon.Explore.Book.Chapters.Greenhouse;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class GreenhouseModelTests
{
    [Fact]
    public void WithoutGreenhouseGasesEarthWouldBeFrozen()
    {
        // σT⁴ = 1361 × 0.7 / 4 → 255 K, about −18 °C; the real average is 33 K warmer.
        Assert.InRange(PlanetModel.EffectiveTemperature(), 254, 256);
        Assert.InRange(PlanetModel.BareCelsius, -19.5, -17.5);
        Assert.InRange(PlanetModel.SurfaceCelsius(PlanetModel.PreindustrialCo2) - PlanetModel.BareCelsius, 31, 34);
    }

    [Fact]
    public void NasaBlackBodyTemperaturesComeOut()
    {
        var venus = PlanetModel.Worlds.Single(w => w.Id == "Venus");
        var earth = PlanetModel.Worlds.Single(w => w.Id == "Earth");
        var mars = PlanetModel.Worlds.Single(w => w.Id == "Mars");
        Assert.Equal(226.6, venus.Bare, 0);
        Assert.Equal(254.0, earth.Bare, 0);
        Assert.Equal(209.8, mars.Bare, 0);
        Assert.InRange(venus.Warming, 500, 520);
        Assert.InRange(earth.Warming, 33, 35);
        Assert.InRange(mars.Warming, 2, 8);
    }

    [Fact]
    public void DoublingCo2PushesAbout37WattsAndWarmsThreeDegrees()
    {
        Assert.Equal(0, PlanetModel.Forcing(280), 9);
        Assert.InRange(PlanetModel.Forcing(560), 3.6, 3.8);
        Assert.Equal(PlanetModel.Sensitivity, PlanetModel.SurfaceCelsius(560) - PlanetModel.SurfaceCelsius(280), 9);

        // Today's CO₂ alone, once settled: about 1.8 °C over pre-industrial; the ice-age 180 ppm about 1.9 °C under.
        Assert.InRange(PlanetModel.SurfaceCelsius(PlanetModel.TodayCo2) - PlanetModel.PreindustrialSurface, 1.7, 1.9);
        Assert.InRange(PlanetModel.SurfaceCelsius(180) - PlanetModel.PreindustrialSurface, -2.0, -1.8);
    }

    [Fact]
    public void HeatLeavesFromAboutFiveKilometresAndHigherWithMoreCo2()
    {
        var now = PlanetModel.EmissionHeight(PlanetModel.TodayCo2);
        Assert.InRange(now, 4.8, 5.6);
        Assert.InRange(PlanetModel.AirCelsius(PlanetModel.TodayCo2, now), PlanetModel.BareCelsius - 0.01, PlanetModel.BareCelsius + 0.01);
        Assert.InRange(PlanetModel.EmissionHeight(560) - PlanetModel.EmissionHeight(280), 0.44, 0.48);
    }

    [Fact]
    public void TheEnergyBudgetBalances()
    {
        // Sunlight: reflected + taken in by the air + taken in by the ground.
        Assert.Equal(EnergyBudget.Incoming, EnergyBudget.Reflected + EnergyBudget.AbsorbedByAir + EnergyBudget.AbsorbedBySurface, 0);

        // The top: in = reflected + heat out + kept (the published numbers are rounded to whole watts).
        Assert.InRange(EnergyBudget.Reflected + EnergyBudget.Outgoing + EnergyBudget.Kept - EnergyBudget.Incoming, -1, 1);

        // The ground: sunlight + back radiation = thermals + evaporation + its own glow + kept.
        Assert.Equal(EnergyBudget.AbsorbedBySurface + EnergyBudget.BackRadiation,
            EnergyBudget.Thermals + EnergyBudget.Evaporation + EnergyBudget.SurfaceRadiation + EnergyBudget.Kept, 0);

        // 396 W/m² is the glow of a 16 °C ground; only a tenth goes straight out.
        Assert.InRange(PlanetModel.SurfaceEmission(16), 393, 400);
        Assert.InRange(EnergyBudget.WindowShare, 0.09, 0.11);
    }

    [Fact]
    public void TheGroundGlowsInTheInfraredTheSunInVisibleLight()
    {
        Assert.InRange(InfraredModel.PeakWavelength(288), 10.0, 10.1);
        Assert.InRange(InfraredModel.PeakWavelength(5772), 0.49, 0.51);

        // Almost none of the ground's glow is shorter than 4 µm, and almost none of the Sun's longer.
        Assert.True(InfraredModel.Share(0.05, 4, 288) < 0.01);
        Assert.True(InfraredModel.Share(4, 2000, 5772) < 0.02);

        // The window, 8–13 µm, holds about a third of the ground's glow.
        Assert.InRange(InfraredModel.Share(8, 13, 288), 0.28, 0.36);
    }

    [Fact]
    public void OnlyMoleculesWhoseWigglesShiftChargeCatchInfrared()
    {
        foreach (var gas in new[] { Gas.N2, Gas.O2, Gas.Ar })
        {
            Assert.All(Molecules.Get(gas).Modes, m => Assert.False(m.Active));
            Assert.Equal(0, InfraredModel.CaughtShare(gas), 9);
        }

        // Each mode's dipole really changes (active) or stays (inactive) as the atoms move.
        foreach (var gas in Molecules.Shown)
        {
            var molecule = Molecules.Get(gas);
            foreach (var mode in molecule.Modes.Where(m => !m.Spin))
            {
                var still = molecule.Dipole(molecule.Positions(mode, 0));
                var moved = molecule.Dipole(molecule.Positions(mode, 1));
                var change = (moved - still).Length;
                if (mode.Active) Assert.True(change > 0.01, $"{gas} {mode.Id} should shift charge");
                else Assert.True(change < 1e-9, $"{gas} {mode.Id} should not shift charge");
            }
        }

        // CO₂'s bend is 15 µm, near the ground's brightest; it catches a big share by itself.
        var bend = Molecules.Get(Gas.CO2).Modes.Single(m => m.Id == "Bend");
        Assert.InRange(bend.Wavelength, 14.9, 15.1);
        Assert.True(bend.Catches(15));
        Assert.False(bend.Catches(10));
        Assert.InRange(InfraredModel.CaughtShare(Gas.CO2), 0.15, 0.35);
        Assert.InRange(InfraredModel.CaughtShare(Gas.H2O), 0.35, 0.7);
        Assert.InRange(InfraredModel.AirCaughtShare(), 0.6, 0.85);
    }

    [Fact]
    public void ModesKeepTheCentreOfMassStill()
    {
        foreach (var gas in Molecules.Shown)
        {
            var molecule = Molecules.Get(gas);
            foreach (var mode in molecule.Modes.Where(m => !m.Spin))
            {
                var shift = new Vec3(0, 0, 0);
                for (var i = 0; i < molecule.Atoms.Length; i++) shift += mode.Shifts[i] * molecule.Atoms[i].Mass;
                Assert.True(shift.Length < 1e-9, $"{gas} {mode.Id} moves its centre of mass by {shift.Length}");
            }
        }
    }

    [Fact]
    public void MoreCo2WidensTheBandEdges()
    {
        Assert.True(InfraredModel.Absorption(Gas.CO2, 13.0, 560) > InfraredModel.Absorption(Gas.CO2, 13.0, 280));
        Assert.Equal(1, InfraredModel.Absorption(Gas.CO2, 15, 280), 3);
        Assert.True(InfraredModel.CaughtShare(Gas.CO2, co2: 560) > InfraredModel.CaughtShare(Gas.CO2, co2: 280));
    }

    [Fact]
    public void SampledWavelengthsFollowTheGroundsGlow()
    {
        var samples = Enumerable.Range(0, 2000).Select(i => InfraredModel.SampleGround((i + 0.5) / 2000)).ToList();
        Assert.All(samples, s => Assert.InRange(s, 3, 40));
        var median = samples[1000];
        Assert.InRange(median, 10, 16);
    }

    [Fact]
    public void TheSunIsHighestAtNoonAndHigherInSummer()
    {
        const double lat = 28.2;
        Assert.InRange(GlassHouseModel.SunAltitude(12, lat, GlassHouseModel.Declination(Season.Summer, lat)), 85, 86);
        Assert.InRange(GlassHouseModel.SunAltitude(12, lat, GlassHouseModel.Declination(Season.Winter, lat)), 38, 39);
        Assert.InRange(GlassHouseModel.SunAltitude(6, lat, 0), -0.1, 0.1);

        // Down under the summer Sun is in December, but the page's "summer" is the local one.
        Assert.InRange(GlassHouseModel.SunAltitude(12, -33.9, GlassHouseModel.Declination(Season.Summer, -33.9)), 79, 80);

        // About 1000 W/m² on the ground at a high noon, none at night.
        Assert.InRange(GlassHouseModel.Sunlight(85), 950, 1100);
        Assert.Equal(0, GlassHouseModel.Sunlight(-5));
    }

    [Fact]
    public void AShutGreenhouseGetsHotAndVentsHelpMoreThanTheInfrared()
    {
        var shut = new DayOptions(28.2, Season.Summer, false, false, false);
        var day = GlassHouseModel.Day(shut);
        var peak = day.Max(s => s.Greenhouse);
        var outsidePeak = day.Max(s => s.Outside);
        Assert.InRange(peak, 45, 60);

        // Open vents: much cooler. A rock-salt pane (infrared escapes): only a few degrees cooler.
        var vented = GlassHouseModel.Day(shut with { VentsOpen = true }).Max(s => s.Greenhouse);
        var salt = GlassHouseModel.Day(shut with { SaltPane = true }).Max(s => s.Greenhouse);
        Assert.InRange(vented, outsidePeak + 2, outsidePeak + 10);
        Assert.InRange(peak - salt, 2, 9);
        Assert.True(peak - vented > 1.5 * (peak - salt));

        // The day ends as it began: the run has settled.
        Assert.InRange(day[1440].Greenhouse - day[0].Greenhouse, -0.5, 0.5);

        // At night the shut glass house stays a little warmer than outside; with salt it is not.
        var night = day[3 * 60];
        Assert.True(night.Greenhouse > night.Outside, $"{night}");
    }

    [Fact]
    public void AParkedCarWarmsAboutTwentyDegreesWithinTheHour()
    {
        var day = GlassHouseModel.Day(new DayOptions(28.2, Season.Summer, false, false, false));
        var noon = day[12 * 60];
        Assert.InRange(noon.Car - noon.Outside, 18, 30);

        // A cracked window hardly helps.
        var cracked = GlassHouseModel.Day(new DayOptions(28.2, Season.Summer, false, false, true))[12 * 60];
        Assert.InRange(noon.Car - cracked.Car, 0.5, 5);

        // Even in winter the car warms well over the air.
        var winter = GlassHouseModel.Day(new DayOptions(28.2, Season.Winter, false, false, false))[12 * 60];
        Assert.True(winter.Car - winter.Outside > 10);
    }

    [Fact]
    public void Co2RecordRisesFrom280ToAbout425()
    {
        var points = Co2Record.Points;
        Assert.Equal(1750, points[0].Year);
        Assert.Equal(2024, points[^1].Year);
        Assert.InRange(points[^1].Ppm, PlanetModel.TodayCo2 - 1, PlanetModel.TodayCo2 + 1);
        for (var i = 1; i < points.Count; i++) Assert.True(points[i].Year > points[i - 1].Year);
        Assert.True(points.Where(p => p.Year >= 1959).Zip(points.Where(p => p.Year >= 1960)).All(p => p.Second.Ppm > p.First.Ppm));
    }
}
