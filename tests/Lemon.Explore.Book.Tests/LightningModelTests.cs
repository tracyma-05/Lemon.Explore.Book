using System;
using System.Linq;
using Lemon.Explore.Book.Chapters.Lightning;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class LightningModelTests
{
    [Theory]
    [InlineData(Material.Glass, Material.Silk, 1)]
    [InlineData(Material.Rubber, Material.Fur, -1)]
    [InlineData(Material.Balloon, Material.Hair, -1)]
    [InlineData(Material.Pvc, Material.Wool, -1)]
    public void TheTextbookPairsGetTheTextbookSigns(Material rod, Material cloth, int sign) =>
        Assert.Equal(sign, Math.Sign(StaticModel.SaturationCharge(rod, cloth)));

    [Fact]
    public void LikeRubbedOnLikeGivesNothingAndFarApartGivesMost()
    {
        Assert.Equal(0, StaticModel.SaturationCharge(Material.Silk, Material.Silk));
        var near = Math.Abs(StaticModel.SaturationCharge(Material.Glass, Material.Silk));
        var far = Math.Abs(StaticModel.SaturationCharge(Material.Pvc, Material.Hair));
        Assert.True(far > 2 * near);
        Assert.Equal(StaticModel.MaxCharge, far, 12);
    }

    [Fact]
    public void RubbingApproachesTheSaturationCharge()
    {
        var target = StaticModel.SaturationCharge(Material.Balloon, Material.Hair);
        var q = 0.0;
        var previous = 0.0;
        for (var i = 0; i < 20; i++)
        {
            q = StaticModel.Rub(q, 0.1, Material.Balloon, Material.Hair);
            Assert.True(Math.Abs(q) > Math.Abs(previous));
            previous = q;
        }

        Assert.InRange(q / target, 0.95, 1.0);

        // A rod charged the other way is first neutralised by rubbing.
        var flipped = StaticModel.Rub(-target, 2, Material.Balloon, Material.Hair);
        Assert.Equal(Math.Sign(target), Math.Sign(flipped));
    }

    [Fact]
    public void DampAirDrainsChargeFaster()
    {
        Assert.InRange(StaticModel.HalfLife(20), 300, 600);
        Assert.InRange(StaticModel.HalfLife(80), 0.2, 1.0);
        Assert.True(StaticModel.HalfLife(30) > 10 * StaticModel.HalfLife(60));
        Assert.Equal(50e-9, StaticModel.Leak(100e-9, StaticModel.HalfLife(45), 45), 15);
    }

    [Fact]
    public void PaschenGivesTheKnownSparkVoltages()
    {
        // About 3 kV a millimetre at a centimetre, a little more for shorter gaps, and a minimum of about 300 V.
        Assert.InRange(StaticModel.BreakdownVoltage(0.01), 30e3, 38e3);
        Assert.InRange(StaticModel.BreakdownVoltage(0.001), 4e3, 5.5e3);
        Assert.InRange(StaticModel.MinimumSparkVoltage, 280, 360);
        Assert.InRange(StaticModel.MinimumGap, 5e-6, 20e-6);
        Assert.Equal(0, StaticModel.SparkGap(200));

        // The gap is the inverse of the breakdown voltage.
        foreach (var gap in new[] { 0.0002, 0.002, 0.008 }) Assert.Equal(gap, StaticModel.SparkGap(StaticModel.BreakdownVoltage(gap)), 6);
    }

    [Fact]
    public void AWellRubbedRodSparksAFewMillimetres()
    {
        var volts = StaticModel.Voltage(StaticModel.MaxCharge);
        Assert.InRange(volts, 15e3, 25e3);
        Assert.InRange(StaticModel.SparkGap(volts), 0.004, 0.008);
        Assert.InRange(StaticModel.Electrons(100e-9), 6.2e11, 6.3e11);
    }

    [Fact]
    public void MoreChargeReachesFurtherAndBendsTheStreamEitherWay()
    {
        Assert.Equal(0.04, StaticModel.LiftReach(100e-9), 9);
        Assert.True(StaticModel.LiftReach(-200e-9) > StaticModel.LiftReach(50e-9));

        var still = StaticModel.Stream(0, 0.4, 0, 0, 0.03, 0.25);
        var pulled = StaticModel.Stream(0, 0.4, 0, 100e-9, 0.03, 0.25);
        var pushed = StaticModel.Stream(0, 0.4, 0, -100e-9, 0.03, 0.25);
        Assert.Equal(0, still[^1].X, 9);
        Assert.InRange(pulled[^1].X, 0.01, 0.2);
        Assert.Equal(pulled[^1].X, pushed[^1].X, 9);
        var closer = StaticModel.Stream(0, 0.4, 0, 100e-9, 0.02, 0.25);
        Assert.True(closer[^1].X > pulled[^1].X);
    }

    [Fact]
    public void TheMainNegativeSitsAtMinusFifteen()
    {
        var charges = CloudModel.Charges(12);
        var main = charges.Single(c => c.Charge < 0);
        Assert.Equal(-15, CloudModel.Temperature(main.Height), 9);
        Assert.True(charges.Max(c => c.Height) > main.Height);
        Assert.InRange(CloudModel.HeightOf(0), 4.4, 4.8);
    }

    [Fact]
    public void UnderTheStormTheFieldPointsUpAndFarAwayItTurnsOver()
    {
        Assert.Equal(CloudModel.FairWeatherField, CloudModel.GroundField(0, 12, 0, 0, 0), 12);
        var under = CloudModel.GroundField(0.5, 12, 1, 1, 1);
        Assert.InRange(under, 4, 15);
        var far = CloudModel.GroundField(25, 12, 1, 1, 1);
        Assert.True(far < 0);
        Assert.True(Math.Abs(far) < under);
    }

    [Fact]
    public void TallerStormsFlashMuchMoreOften()
    {
        Assert.InRange(CloudModel.FlashRate(10), 2, 3.5);
        Assert.InRange(CloudModel.FlashRate(14), 12, 16);
        Assert.InRange(CloudModel.IntracloudRatio(0), 6.2, 6.4);
        Assert.Equal(2, CloudModel.IntracloudRatio(60), 9);
        Assert.InRange(CloudModel.GroundShare(28.2), 0.17, 0.2);
    }

    [Fact]
    public void StrikingDistanceMatchesTheRollingSpheres()
    {
        Assert.InRange(StrikeModel.StrikingDistance(3), 19, 22);
        Assert.InRange(StrikeModel.StrikingDistance(10), 43, 46);
        Assert.InRange(StrikeModel.StrikingDistance(30), 85, 95);
    }

    [Fact]
    public void TheCurrentRisesFastAndHalvesSlowly()
    {
        var samples = Enumerable.Range(1, 20000).Select(i => i * 0.05).Select(t => (T: t, I: StrikeModel.Current(t, 30, true))).ToList();
        var peak = samples.MaxBy(s => s.I);
        Assert.InRange(peak.I, 29, 31);
        Assert.InRange(peak.T, 1.5, 5);
        var half = samples.First(s => s.T > peak.T && s.I < 15).T;
        Assert.InRange(half, 150, 250);
        Assert.True(StrikeModel.Current(1, 12, false) > StrikeModel.Current(1, 30, true) * 0.3);
        Assert.Equal(0, StrikeModel.Current(0, 30, true));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(2026)]
    public void TheLeaderComesDownAndEndsOnWhatItHit(int seed)
    {
        var strike = StrikeModel.Leader(seed, 30, rod: true);
        Assert.Equal(StrikeModel.CloudBase, strike.Main[0].H);
        for (var i = 1; i < strike.Main.Count; i++)
        {
            Assert.True(strike.Main[i].H < strike.Main[i - 1].H);
            Assert.Equal(i, strike.Main[i].Step);
        }

        Assert.Equal(strike.Steps, strike.Main[^1].Step);
        Assert.True(strike.Main[^1].H >= strike.TargetTop);
        Assert.All(strike.Branches, b => Assert.All(b.Nodes, n => Assert.True(n.H > 0)));

        // The same seed gives the same strike.
        Assert.Equal(strike.Main.Count, StrikeModel.Leader(seed, 30, rod: true).Main.Count);
    }

    [Fact]
    public void ARodCatchesStrikesThatComeNearTheHouse()
    {
        var caught = 0;
        var houseWithout = 0;
        for (var seed = 0; seed < 300; seed++)
        {
            var without = StrikeModel.Leader(seed, 30, rod: false);
            if (without.Target != StrikeTarget.House) continue;
            houseWithout++;
            if (StrikeModel.Leader(seed, 30, rod: true).Target == StrikeTarget.Rod) caught++;
        }

        Assert.True(houseWithout > 5);
        Assert.True(caught >= houseWithout * 0.8);
    }

    [Fact]
    public void TheRollingSphereLeavesAZoneUnderTheRod()
    {
        var things = StrikeModel.Things(rod: true);
        double[] xs = [15, 30, 60, 150];
        var heights = StrikeModel.Protected(xs, things, 45);
        Assert.True(heights[0] >= 14 - 0.5);
        Assert.True(heights[1] > 0);
        Assert.Equal(0, heights[3], 6);
    }

    [Fact]
    public void TheTimelineSlowsTheFlashAndThenRunsInRealTime()
    {
        const int strokes = 3;
        var flash = StrikeTimeline.FlashStory(strokes);
        var (part, _, real) = StrikeTimeline.At(flash + 2, strokes);
        Assert.Equal(StrikeTimeline.Phase.Thunder, part.Phase);
        Assert.Equal(StrikeTimeline.FlashReal(strokes) + 2, real, 9);
        Assert.Equal(StrikeTimeline.Phase.Leader, StrikeTimeline.At(1, strokes).Part.Phase);
        Assert.InRange(StrikeTimeline.FlashReal(strokes), 0.1, 0.3);

        // The thunder from 3 km comes after about 9 seconds, and the story waits for it.
        Assert.InRange(StrikeModel.ThunderDelay(3), 8.6, 8.9);
        Assert.True(StrikeTimeline.Length(strokes, 3) > flash + 8.7);
    }
}
