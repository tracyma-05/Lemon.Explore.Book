using Xunit;
using Lemon.Explore.Book.Chapters.Expansion;

namespace Lemon.Explore.Book.Tests;

/// <summary>
/// The expanding-universe page's model against published ΛCDM numbers (Planck 2018: H0 = 67.4, Ωm = 0.315, flat) and
/// textbook closed forms: the age, look-back times and distances, the start of the speed-up, a matter-only universe's
/// 2 / (3 H0), and a closed universe's turnaround and crunch.
/// </summary>
public sealed class ExpansionModelTests
{
    private static readonly Universe Ours = Universe.Ours;

    [Fact]
    public void OurUniverse_Is13_8BillionYearsOld()
    {
        Assert.InRange(Ours.Age, 13.75, 13.85);
    }

    [Fact]
    public void LightFromRedshiftOne_Left7_9BillionYearsAgo()
    {
        // Wright's calculator for these parameters: 7.93 Gyr; comoving distance 3.40 Gpc.
        Assert.InRange(Ours.LookbackTime(Universe.ScaleForRedshift(1)), 7.85, 8.0);
        Assert.InRange(Ours.ComovingDistance(0.5), 3350, 3450);
    }

    [Fact]
    public void TheEdgeOfWhatWeSee_IsAbout46BillionLightYearsAway()
    {
        var horizon = Ours.Horizon * Universe.GlyPerMpc;
        Assert.InRange(horizon, 45, 48);

        // The microwave background comes from just inside it.
        var cmb = Ours.ComovingDistance(Universe.ScaleForRedshift(Universe.CmbRedshift)) * Universe.GlyPerMpc;
        Assert.InRange(cmb, 44.5, horizon);
    }

    [Fact]
    public void TheMicrowaveBackground_WasReleasedAbout380ThousandYearsAfterTheStart()
    {
        var years = Ours.TimeAt(Universe.ScaleForRedshift(Universe.CmbRedshift)) * 1e9;
        Assert.InRange(years, 340_000, 420_000);
        Assert.InRange(Universe.Temperature(Universe.ScaleForRedshift(Universe.CmbRedshift)), 2900, 3100);
    }

    [Fact]
    public void TheFirstNuclei_FormedInTheFirstMinutes()
    {
        var seconds = Ours.TimeAt(Universe.CmbToday / 1e9) * Universe.SecondsPerGyr;
        Assert.InRange(seconds, 100, 300);
    }

    [Fact]
    public void TheSpeedUp_StartedAtRedshift0_63()
    {
        var a = Ours.AccelerationStart();
        Assert.NotNull(a);
        Assert.InRange(Universe.RedshiftForScale(a.Value), 0.6, 0.67);
        Assert.True(Ours.Deceleration(1) < 0);
        Assert.True(Ours.Deceleration(0.3) > 0);
        Assert.InRange(Ours.LookbackTime(a.Value), 5.5, 6.5);
    }

    [Fact]
    public void ScaleAndTime_AreInverse()
    {
        foreach (var a in new[] { 1e-10, 1e-6, 1e-3, 0.01, 0.1, 0.5, 1, 2, 5 })
        {
            var t = Ours.TimeAt(a);
            Assert.Equal(a, Ours.ScaleAt(t), a * 1e-3);
        }

        Assert.Equal(1, Ours.ScaleAt(Ours.Age), 1e-6);
        Assert.Equal(Ours.H0, Ours.Hubble(1), 1e-9);
    }

    [Fact]
    public void FarAway_MovesAwayFasterThanLight()
    {
        // Above z ≈ 1.5 a source moves away faster than light today; the most distant galaxies at more than twice.
        Assert.True(Ours.RecessionSpeedNow(Universe.ScaleForRedshift(1)) < Universe.C);
        Assert.True(Ours.RecessionSpeedNow(Universe.ScaleForRedshift(2)) > Universe.C);
        Assert.InRange(Ours.RecessionSpeedNow(Universe.ScaleForRedshift(14.18)) / Universe.C, 2.0, 2.4);
    }

    [Fact]
    public void AMatterOnlyUniverse_IsTwoThirdsOfAHubbleTimeOld_AndSlowsForEver()
    {
        var u = Universe.MatterOnly;
        Assert.Equal(2.0 / 3 * u.HubbleTime, u.Age, 0.02);
        Assert.Null(u.AccelerationStart());
        Assert.Null(u.CrunchTime);
        Assert.True(u.Hubble(2) < u.Hubble(1));
    }

    [Fact]
    public void AClosedUniverse_TurnsRoundAndCrunches_AsTheCycloidSays()
    {
        // Matter only with Ωm = 5: a_max = Ωm / (Ωm − 1) = 1.25; crunch at π Ωm / (Ωm − 1)^1.5 Hubble times;
        // today at Ωm / (2 (Ωm − 1)^1.5) (θ − sin θ) with cos θ = (2 − Ωm) / Ωm.
        var u = Universe.Closed;
        Assert.NotNull(u.MaxScale);
        Assert.Equal(1.25, u.MaxScale!.Value, 1e-3);
        Assert.Equal(Math.PI * 5 / 8 * u.HubbleTime, u.CrunchTime!.Value, 0.05);
        var theta = Math.Acos(-0.6);
        Assert.Equal(5.0 / 16 * (theta - Math.Sin(theta)) * u.HubbleTime, u.Age, 0.03);

        var turn = u.TurnaroundTime!.Value;
        Assert.Equal(1.25, u.ScaleAt(turn), 1e-3);
        Assert.Equal(u.ScaleAt(turn - 3), u.ScaleAt(turn + 3), 1e-6);
        Assert.True(u.IsContracting(turn + 1));
        Assert.Equal(0, u.ScaleAt(u.CrunchTime.Value + 1));
        Assert.True(u.HubbleAtTime(turn + 2) < 0);
    }

    [Fact]
    public void Eras_FollowEachOtherInOrder()
    {
        var times = new[] { 1e-15, 1e-6, 0.05, 0.5, 4, 10, 13.8, 20 };
        var eras = times.Select(t => CosmicHistory.EraAt(Ours, t)).ToList();
        Assert.Equal(
            [CosmicEra.Start, CosmicEra.Plasma, CosmicEra.DarkAges, CosmicEra.Dawn, CosmicEra.Galaxies, CosmicEra.Speeding, CosmicEra.Now, CosmicEra.FutureFast],
            eras);
        Assert.Equal(CosmicEra.Contracting, CosmicHistory.EraAt(Universe.Closed, Universe.Closed.TurnaroundTime!.Value + 2));
        Assert.Equal(CosmicEra.Ended, CosmicHistory.EraAt(Universe.Closed, 29.9));
        Assert.Equal(CosmicEra.FutureSlowing, CosmicHistory.EraAt(Universe.MatterOnly, 20));
    }

    [Theory]
    [InlineData(TimeAxis.Linear)]
    [InlineData(TimeAxis.Log)]
    public void TheAxis_MapsTimeBothWays(TimeAxis axis)
    {
        foreach (var f in new[] { 0.01, 0.2, 0.5, 0.9, 1 })
        {
            Assert.Equal(f, CosmicHistory.Fraction(axis, CosmicHistory.Time(axis, f)), 1e-9);
        }

        Assert.Equal(1, CosmicHistory.Fraction(axis, CosmicHistory.End), 1e-9);
    }

    [Fact]
    public void EveryMoment_HappensInOrder_InOurUniverse()
    {
        var times = CosmicEvent.All.Select(e => e.TimeIn(Ours)).ToList();
        Assert.All(times, t => Assert.NotNull(t));
        for (var i = 1; i < times.Count; i++) Assert.True(times[i] > times[i - 1], CosmicEvent.All[i].Id);

        // The Sun formed 4.6 billion years ago, at z ≈ 0.4.
        var sun = CosmicEvent.All.Single(e => e.Id == "Sun").TimeIn(Ours)!.Value;
        Assert.InRange(Universe.RedshiftForScale(Ours.ScaleAt(sun)), 0.38, 0.46);
    }

    [Fact]
    public void TheHubbleSpan_RunsFromSixTenthsToNow()
    {
        Assert.Equal(HubbleSpan.StartScale, HubbleSpan.Scale(0), 1e-6);
        Assert.Equal(1, HubbleSpan.Scale(1), 1e-6);
        Assert.InRange(Ours.Age - HubbleSpan.Time(0), 5.8, 6.6);
    }

    [Fact]
    public void TheGalaxyField_IsTheSameEveryTime_AndHasTheMilkyWayAtTheMiddle()
    {
        var a = GalaxyField.At(new GalaxyCell(5, -3));
        var b = GalaxyField.At(new GalaxyCell(5, -3));
        Assert.Equal(a, b);
        Assert.Equal(new Avalonia.Vector(0, 0), GalaxyField.At(GalaxyCell.MilkyWay).Position);
        var count = GalaxyField.Within(new Avalonia.Rect(-400, -400, 800, 800)).Count();
        Assert.InRange(count, 81, 121);
    }

    [Fact]
    public void LightOnItsWay_StretchesWithSpace()
    {
        var start = ExpansionLightTrip(1, 0);
        var end = ExpansionLightTrip(1, 1);
        Assert.Equal(1, start.Stretch, 1e-6);
        Assert.Equal(2, end.Stretch, 1e-3);
        Assert.Equal(start.SourceComoving, start.PhotonComoving, 1e-6);
        Assert.Equal(0, end.PhotonComoving, 1e-6);

        // Light from z = 10 is first carried away by space growing faster than it crosses it.
        Assert.True(ExpansionLightTrip(10, 0.01).LosingGround);
        Assert.False(ExpansionLightTrip(10, 0.95).LosingGround);
    }

    private static ExpansionDiagram.LightTrip ExpansionLightTrip(double z, double journey) => ExpansionDiagram.LightTrip.At(z, journey);
}
