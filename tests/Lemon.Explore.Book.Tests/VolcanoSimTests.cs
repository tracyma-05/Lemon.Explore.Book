using System.Linq;
using Lemon.Explore.Book.Chapters.Volcanoes;
using Xunit;

namespace Lemon.Explore.Book.Tests;

/// <summary>The magnifier's simulations: crystals growing in cooling magma, and mud settling in a tank.</summary>
public sealed class VolcanoSimTests
{
    private static CrystalSim Cooled(MagmaKind magma, double rate, bool gas = false, bool twoStage = false)
    {
        var sim = new CrystalSim(magma, rate, gas, twoStage);
        sim.AdvanceTo(CrystalSim.Steps);
        return sim;
    }

    [Fact]
    public void SlowCooling_MakesFewBigCrystals_FastMakesManySmall()
    {
        var granite = Cooled(MagmaKind.Felsic, -3);
        var rhyolite = Cooled(MagmaKind.Felsic, 3.5);
        Assert.True(granite.CrystalShare > 0.97, $"{granite.CrystalShare}");
        Assert.True(rhyolite.CrystalShare > 0.9, $"{rhyolite.CrystalShare}");
        Assert.True(granite.MeanSizeMm > 1.5, $"{granite.MeanSizeMm}");
        Assert.True(granite.MeanSizeMm > 8 * rhyolite.MeanSizeMm, $"{granite.MeanSizeMm} vs {rhyolite.MeanSizeMm}");
        Assert.True(rhyolite.CrystalCount > 30 * granite.CrystalCount);
    }

    [Fact]
    public void QuenchedMagma_FreezesAsGlass()
    {
        var obsidian = Cooled(MagmaKind.Felsic, 7);
        Assert.True(obsidian.GlassShare > 0.9, $"{obsidian.GlassShare}");
        Assert.Equal(0, obsidian.BubbleShare);
        Assert.Equal("Obsidian", CrystalSim.RockName(MagmaKind.Felsic, 7, gas: false, twoStage: false));

        var pumice = Cooled(MagmaKind.Felsic, 7, gas: true);
        Assert.InRange(pumice.BubbleShare, 0.5, 0.7);
        Assert.Equal("Pumice", CrystalSim.RockName(MagmaKind.Felsic, 7, gas: true, twoStage: false));

        // Slowly cooled magma lets its gas go: no bubbles.
        Assert.Equal(0, Cooled(MagmaKind.Felsic, -2, gas: true).BubbleShare);
    }

    [Fact]
    public void Minerals_CrystalliseInBowensOrder_ToTheirShares()
    {
        var gabbro = new CrystalSim(MagmaKind.Mafic, -2);

        // Olivine first: just below its temperature, only olivine.
        gabbro.AdvanceTo(20);
        Assert.True(gabbro.Temperature < 1240);
        Assert.True(gabbro.MineralShare(0) > 0);
        Assert.Equal(0, gabbro.MineralShare(1) + gabbro.MineralShare(2));

        gabbro.AdvanceTo(CrystalSim.Steps);
        for (var m = 0; m < gabbro.Minerals.Count; m++)
        {
            Assert.InRange(gabbro.MineralShare(m), gabbro.Minerals[m].Share - 0.06, gabbro.Minerals[m].Share + 0.02);
        }

        // In granite quartz is last.
        var granite = new CrystalSim(MagmaKind.Felsic, -2);
        granite.AdvanceTo(CrystalSim.Steps / 2);
        Assert.True(granite.Temperature > 740);
        Assert.Equal(0, granite.MineralShare(3));
    }

    [Fact]
    public void TwoStages_GiveBigCrystalsInAFineGround()
    {
        var porphyry = Cooled(MagmaKind.Felsic, 0, twoStage: true);
        var fine = Cooled(MagmaKind.Felsic, CrystalSim.FastStage);
        Assert.True(porphyry.LargestSizeMm > 2 * fine.LargestSizeMm, $"{porphyry.LargestSizeMm} vs {fine.LargestSizeMm}");
        Assert.Equal("Porphyry", CrystalSim.RockName(MagmaKind.Felsic, 0, gas: false, twoStage: true));
    }

    [Fact]
    public void TheSameSettings_GiveTheSameRock()
    {
        var a = Cooled(MagmaKind.Felsic, 1);
        var b = new CrystalSim(MagmaKind.Felsic, 1);
        b.AdvanceTo(100);
        b.AdvanceTo(CrystalSim.Steps);
        Assert.True(a.Cells.SequenceEqual(b.Cells));
    }

    [Fact]
    public void Mud_SettlesCoarseFirst_InGradedLayers()
    {
        var tank = new SedimentSim();
        tank.Pour();
        var gravelDoneAt = -1.0;
        for (var t = 0.0; t < 60; t += 0.05)
        {
            tank.Step(0.05);
            if (gravelDoneAt < 0 && tank.SinkingCount(SedimentSim.Gravel) == 0) gravelDoneAt = t;
        }

        Assert.InRange(gravelDoneAt, 0, 5);
        Assert.Empty(tank.Falling);
        Assert.True(tank.Settled(SedimentSim.Clay) > 800);

        // In a column the bottom of the layer is coarser than its top.
        var bed = tank.Bed.ToArray();
        var column = Enumerable.Range(0, SedimentSim.Width).OrderByDescending(c => tank.Heights[c]).First(c =>
            Enumerable.Range(0, SedimentSim.Height).Any(y => bed[(y * SedimentSim.Width) + c] <= SedimentSim.Sand && bed[(y * SedimentSim.Width) + c] > 0));
        var sizes = Enumerable.Range(0, SedimentSim.Height).Reverse().Select(y => bed[(y * SedimentSim.Width) + column]).Where(s => s > 0).ToList();
        var half = sizes.Count / 2;
        Assert.True(sizes.Take(half).Average(s => (double)s) < sizes.Skip(half).Average(s => (double)s), $"{column}: {string.Concat(sizes)} h={string.Join(",", tank.Heights.ToArray().Where((_, i) => i % 10 == 0))}");

        // Pour after pour the bed rises until the tank is full.
        for (var i = 0; i < 30 && !tank.IsFull; i++)
        {
            tank.Pour();
            for (var t = 0.0; t < 30; t += 0.1) tank.Step(0.1);
        }

        Assert.True(tank.IsFull);
        Assert.True(tank.Pours >= 3);
    }
}
