using Lemon.Explore.Book.Chapters.PowerGrid;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class GridModelTests
{
    [Fact]
    public void TheClassroomGeneratorMakesAFewVoltsAtATurnASecond()
    {
        // N·B·A·ω = 200 × 0.3 T × 0.01 m² × 2π ≈ 3.8 V.
        Assert.Equal(3.77, GeneratorModel.PeakEmf(200, 0.3, 1), 2);

        // Twice as fast, twice the turns or twice the field: twice the voltage.
        Assert.Equal(2 * GeneratorModel.PeakEmf(200, 0.3, 1), GeneratorModel.PeakEmf(200, 0.3, 2), 9);
        Assert.Equal(2 * GeneratorModel.PeakEmf(200, 0.3, 1), GeneratorModel.PeakEmf(400, 0.3, 1), 9);
    }

    [Fact]
    public void TheVoltageIsTheRateOfChangeOfTheFlux()
    {
        const int turns = 200;
        const double field = 0.3;
        const double speed = 1.5;
        var omega = 2 * Math.PI * speed;
        for (var angle = 0.0; angle < 2 * Math.PI; angle += 0.1)
        {
            // emf = −N·dΦ/dt, with dθ/dt = ω.
            const double d = 1e-6;
            var rate = (GeneratorModel.Flux(field, angle + d) - GeneratorModel.Flux(field, angle - d)) / (2 * d) * omega;
            Assert.Equal(-turns * rate, GeneratorModel.Emf(turns, field, speed, angle), 6);
        }

        // Facing the field: most flux, no voltage. Along the field: no flux, most voltage.
        Assert.Equal(0, GeneratorModel.Emf(turns, field, speed, 0), 9);
        Assert.Equal(0, GeneratorModel.Flux(field, Math.PI / 2), 9);
        Assert.Equal(GeneratorModel.PeakEmf(turns, field, speed), GeneratorModel.Emf(turns, field, speed, Math.PI / 2), 9);
    }

    [Fact]
    public void TheCommutatorKeepsTheCurrentOneWay()
    {
        Assert.Equal(-2, GeneratorModel.Output(GeneratorKind.Ac, -2));
        Assert.Equal(2, GeneratorModel.Output(GeneratorKind.Dc, -2));
    }

    [Fact]
    public void LightingTheLampMakesItHarderToTurn()
    {
        var emf = GeneratorModel.PeakEmf(200, 0.3, 2);
        Assert.Equal(0, GeneratorModel.HandForce(200, emf, lampOn: false, 2));
        Assert.True(GeneratorModel.HandForce(200, emf, lampOn: true, 2) > 0);

        // Energy is conserved: what the hand puts in is what the lamp and the coil's wire get.
        var current = GeneratorModel.Current(200, emf, lampOn: true);
        var heat = current * current * GeneratorModel.CoilResistance(200);
        Assert.Equal(GeneratorModel.InputPower(200, emf, true), GeneratorModel.LampPower(200, emf, true) + heat, 9);

        // At about 2 turns a second the 6 V 3 W lamp is lit about properly.
        Assert.InRange(GeneratorModel.AverageLampPower(200, 0.3, 2, true), 1.5, 3.5);
    }

    [Fact]
    public void ThreePhasesAlwaysAddUpToNothing()
    {
        for (var angle = 0.0; angle < 7; angle += 0.07)
        {
            foreach (var pairs in new[] { 1, 2 })
            {
                var (a, b, c) = GeneratorModel.ThreePhase(10, angle, pairs);
                Assert.Equal(0, a + b + c, 9);
            }
        }

        // Two poles at 3000 rpm and four at 1500 both make 50 Hz; a big slow hydro set needs many poles.
        Assert.Equal(50, GeneratorModel.Frequency(1, 3000), 9);
        Assert.Equal(1500, GeneratorModel.SynchronousRpm(2, 50), 9);
        Assert.Equal(75, GeneratorModel.SynchronousRpm(40, 50), 9);
    }

    [Fact]
    public void HigherVoltageCutsTheLossWithTheSquare()
    {
        var at500 = TransmissionModel.Send(1000, 500, 300);
        var at1000 = TransmissionModel.Send(1000, 1000, 300);

        // 1000 MW at 500 kV is about 1150 A; 300 km of 0.02 Ω/km loses about 2.4%.
        Assert.InRange(at500.Current, 1150, 1160);
        Assert.InRange(at500.LossFraction, 0.023, 0.025);
        Assert.Equal(at500.Loss / 4, at1000.Loss, 6);
        Assert.Equal(at500.Current / 2, at1000.Current, 6);
        Assert.Equal(1000 - at500.Loss, at500.Delivered, 9);
        Assert.False(at500.Overloaded);
        Assert.False(at500.Impossible);

        // At 10 kV the same power needs tens of thousands of amps: it cannot be done.
        var at10 = TransmissionModel.Send(1000, 10, 300);
        Assert.True(at10.Overloaded);
        Assert.True(at10.Impossible);
        Assert.Equal(0, at10.Delivered);

        // Loss grows with the distance and with the square of the power.
        Assert.Equal(2 * at500.Loss, TransmissionModel.Send(1000, 500, 600).Loss, 6);
        Assert.Equal(4 * at500.Loss, TransmissionModel.Send(2000, 500, 300).Loss, 6);
    }

    [Fact]
    public void TheVoltageLadderComesDownToAHome()
    {
        Assert.Equal([220, 110, 10, 0.4], TransmissionModel.StepsDown(500));
        Assert.Equal([10, 0.4], TransmissionModel.StepsDown(35));
        Assert.Equal(25, TransmissionModel.StepUpRatio(500), 9);
    }

    [Theory]
    [InlineData(GridSeason.Summer)]
    [InlineData(GridSeason.Winter)]
    public void EveryHourIsBalanced(GridSeason season)
    {
        for (var hour = 0.0; hour < 24; hour += 0.25)
        {
            var mix = GridDayModel.Mix(season, 28.2, hour, GridDayModel.DefaultSolar);
            Assert.True(Math.Abs(mix.Demand - mix.Generation - mix.Shortfall) < 1e-9, $"{hour}");
            Assert.InRange(mix.Coal, GridDayModel.CoalMinimum, GridDayModel.CoalCapacity);
            Assert.InRange(mix.Pumped, -GridDayModel.PumpedCapacity, GridDayModel.PumpedCapacity);
            Assert.True(mix.Solar >= 0 && mix.Wind >= 0 && mix.Gas >= 0 && mix.Curtailed >= 0);
            Assert.Equal(0, mix.Shortfall, 6);
        }
    }

    [Fact]
    public void TheDayHasItsPeaksAndTheSunItsHours()
    {
        // The evening is the summer peak; the small hours the trough.
        var demand = Enumerable.Range(0, 96).Select(i => GridDayModel.Demand(GridSeason.Summer, i / 4.0)).ToList();
        Assert.InRange(demand.IndexOf(demand.Max()) / 4.0, 19.5, 21);
        Assert.InRange(demand.IndexOf(demand.Min()) / 4.0, 2.5, 5);
        Assert.Equal(GridDayModel.Demand(GridSeason.Summer, 0), GridDayModel.Demand(GridSeason.Summer, 24), 9);

        // No sun at midnight; more at noon in summer than in winter, at both ends of the Earth.
        Assert.Equal(0, GridDayModel.Solar(GridSeason.Summer, 28.2, 0, 10));
        Assert.True(GridDayModel.Solar(GridSeason.Summer, 28.2, 12, 10) > GridDayModel.Solar(GridSeason.Winter, 28.2, 12, 10));
        Assert.True(GridDayModel.Solar(GridSeason.Summer, -33.9, 12, 10) > GridDayModel.Solar(GridSeason.Winter, -33.9, 12, 10));
        Assert.True(GridDayModel.Solar(GridSeason.Summer, 28.2, 7, 10) > 0);
    }

    [Fact]
    public void MiddaySunFillsThePumpsAndThinsTheSpinningMass()
    {
        var noon = GridDayModel.Mix(GridSeason.Summer, 28.2, 12, GridDayModel.MaxSolar);
        var evening = GridDayModel.Mix(GridSeason.Summer, 28.2, 20, GridDayModel.MaxSolar);

        // Lots of sun at noon: coal turned down to its minimum and pumped storage pumping.
        Assert.Equal(GridDayModel.CoalMinimum, noon.Coal, 6);
        Assert.True(noon.Pumped < 0);

        // In the evening peak coal runs flat out, and hydro, pumped storage and gas are all called on.
        Assert.Equal(GridDayModel.CoalCapacity, evening.Coal, 6);
        Assert.True(evening.Pumped > 0 && evening.Gas > 0 && evening.Hydro > GridDayModel.HydroBase(GridSeason.Summer));
        Assert.True(GridDayModel.Inertia(noon) < GridDayModel.Inertia(evening));
        Assert.True(noon.InverterShare > evening.InverterShare);
    }

    [Fact]
    public void ATripDipsTheFrequencyAndTheControlCentreBringsItBack()
    {
        var mix = GridDayModel.Mix(GridSeason.Summer, 28.2, 20, GridDayModel.DefaultSolar);
        var inertia = GridDayModel.Inertia(mix);
        var trace = FrequencyModel.Simulate(1.0, mix.Demand, inertia, GridDayModel.GovernedCapacity(mix));

        // Falls at first by f₀·ΔP/(2H·S), bottoms out a few tenths of a hertz down within seconds, no shedding.
        var early = (trace[0].Frequency - trace[2].Frequency) / (2 * FrequencyModel.Step);
        Assert.Equal(FrequencyModel.InitialFall(1.0, mix.Demand, inertia), early, 2);
        var lowest = FrequencyModel.Lowest(trace);
        Assert.InRange(lowest.Frequency, 49.6, 49.92);
        Assert.InRange(lowest.Time, 2, 15);
        Assert.Equal(0, trace[^1].Shed);

        // Three minutes later it is back within 0.02 Hz, and the control centre has taken over from the governors.
        Assert.InRange(trace[^1].Frequency, 49.98, 50.01);
        Assert.True(trace[^1].Control > 0.8);
        Assert.True(Math.Abs(trace[^1].Governors) < 0.2);
    }

    [Fact]
    public void LessSpinningMassFallsFasterAndABigLossShedsLoad()
    {
        var noon = GridDayModel.Mix(GridSeason.Summer, 28.2, 12, GridDayModel.MaxSolar);
        var evening = GridDayModel.Mix(GridSeason.Summer, 28.2, 20, GridDayModel.MaxSolar);
        double Nadir(GridMix mix, double lost) => FrequencyModel.Lowest(
            FrequencyModel.Simulate(lost, mix.Demand, GridDayModel.Inertia(mix), GridDayModel.GovernedCapacity(mix))).Frequency;

        Assert.True(Nadir(noon, 1) < Nadir(evening, 1));
        Assert.True(FrequencyModel.InitialFall(1, noon.Demand, GridDayModel.Inertia(noon)) >
                    FrequencyModel.InitialFall(1, evening.Demand, GridDayModel.Inertia(evening)));

        // Losing a 3000 MW link at noon pulls the frequency to the first shedding stage, which catches it.
        var trace = FrequencyModel.Simulate(3, noon.Demand, GridDayModel.Inertia(noon), GridDayModel.GovernedCapacity(noon));
        Assert.True(trace[^1].Shed > 0);
        Assert.True(FrequencyModel.Lowest(trace).Frequency > 48.6);
    }
}
