using Lemon.Explore.Book.Chapters.Relativity;
using Xunit;

namespace Lemon.Explore.Book.Tests;

/// <summary>The relativity page's physics: γ, the light clock, Einstein's train, the twins and everyday speeds.</summary>
public sealed class RelativityModelTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(0.6, 1.25)]
    [InlineData(0.8, 5.0 / 3)]
    [InlineData(0.866025403784, 2)]
    [InlineData(0.99, 7.08881205008)]
    public void Gamma_MatchesKnownValues(double beta, double gamma)
    {
        Assert.Equal(gamma, RelativityModel.Gamma(beta), 6);
        Assert.Equal(beta, RelativityModel.BetaForGamma(gamma), 6);
    }

    [Fact]
    public void GammaMinusOne_IsAccurateAtEverydaySpeeds()
    {
        // 300 m/s (a plane): β²/2 = 5.0e-13.
        var beta = 0.3 / RelativityModel.LightKmPerSecond;
        Assert.Equal(1, RelativityModel.GammaMinusOne(beta) / (beta * beta / 2), 9);
        Assert.Equal(RelativityModel.Gamma(0.5) - 1, RelativityModel.GammaMinusOne(0.5), 12);
    }

    [Fact]
    public void LightClock_TicksGammaTimesSlower_AndTheTriangleIsPythagoras()
    {
        const double h = 1;
        foreach (var beta in new[] { 0.0, 0.3, 0.6, 0.9, 0.99 })
        {
            var g = RelativityModel.Gamma(beta);
            Assert.Equal(2 * g, RelativityModel.TickTime(h, beta), 9);
            var (up, along, slant) = RelativityModel.Triangle(h, beta);
            Assert.Equal(slant * slant, (up * up) + (along * along), 9);
        }
    }

    [Fact]
    public void LightClock_FlashGoesUpThenDown()
    {
        // Still clock, mirrors 1 apart: one tick = 2.
        Assert.Equal((0.5, 0, true), RelativityModel.Flash(0.5, 1, 0));
        var (height, ticks, up) = RelativityModel.Flash(1.5, 1, 0);
        Assert.Equal(0.5, height, 9);
        Assert.Equal(0, ticks);
        Assert.False(up);
        Assert.Equal(3, RelativityModel.Flash(6.2, 1, 0).Ticks);

        // At 0.6 c a tick is 2.5: after 5 time units, 2 ticks.
        Assert.Equal(2, RelativityModel.Flash(5.01, 1, 0.6).Ticks);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public void Train_LightReachesTheEndsAtTheirPositions(double beta)
    {
        const double length = 2;
        var (rearTime, rearX, frontTime, frontX) = RelativityModel.TrainHits(length, beta);

        // Light from x = 0, t = 0 at c: |x| = t.
        Assert.Equal(rearTime, -rearX, 9);
        Assert.Equal(frontTime, frontX, 9);
        Assert.True(rearTime <= frontTime);
        Assert.Equal(RelativityModel.HitGap(length, beta), frontTime - rearTime, 9);

        // On the train, both hits happen at the same time, at the ends L/2 from the middle.
        var rear = RelativityModel.Boost(rearTime, rearX, beta);
        var front = RelativityModel.Boost(frontTime, frontX, beta);
        Assert.Equal(rear.T, front.T, 9);
        Assert.Equal(length / 2, rear.T, 9);
        Assert.Equal(-length / 2, rear.X, 9);
        Assert.Equal(length / 2, front.X, 9);

        // The train's own clocks at the ends read the same when each is hit.
        Assert.Equal(RelativityModel.TrainClock(rearTime, -length / 2, beta), RelativityModel.TrainClock(frontTime, length / 2, beta), 9);
    }

    [Fact]
    public void Twins_ClassicTripAtEightTenths()
    {
        // 4 light-years at 0.8 c: Earth waits 10 years, the traveller lives 6, the star is 2.4 ly away for the ship.
        var trip = RelativityModel.Trip(4, 0.8);
        Assert.Equal(10, trip.EarthYears, 9);
        Assert.Equal(6, trip.ShipYears, 9);
        Assert.Equal(2.4, trip.ShipDistance, 9);
        Assert.Equal(4, trip.Younger, 9);
        Assert.Equal(4, trip.ShipX(5), 9);
        Assert.Equal(2, trip.ShipX(7.5), 9);
        Assert.Equal(0, trip.ShipX(10), 9);
    }

    [Fact]
    public void Twins_TheShipsNowJumpsAtTheTurn()
    {
        var trip = RelativityModel.Trip(4, 0.8);

        // Just before the turn (ship clock 3) the ship calls Earth's 1.8 "now"; just after, 8.2.
        Assert.Equal(1.8, trip.EarthNowForShip(3 - 1e-9), 6);
        Assert.Equal(8.2, trip.EarthNowForShip(3 + 1e-9), 6);

        // Earth's clock looks slow on both legs: τ/γ on the way out.
        Assert.Equal(1.2, trip.EarthNowForShip(2), 6);
        Assert.Equal(10, trip.EarthNowForShip(6), 6);
    }

    [Fact]
    public void Twins_BirthdaySignals_FollowTheDopplerFactor()
    {
        var trip = RelativityModel.Trip(4, 0.8);

        // Ship's first birthday (Earth time γ = 5/3, 4/3 ly out) reaches Earth 3 years after the start: k = 3 on the way out.
        Assert.Equal(3, trip.ArrivesAtEarth(trip.Gamma), 9);

        // Earth's signal sent at 1 catches the ship on the way out at t = 5.
        Assert.Equal(5, trip.ArrivesAtShip(1)!.Value, 9);

        // Sent at 2, the ship has turned: met on the way back.
        var met = trip.ArrivesAtShip(2)!.Value;
        Assert.True(met > trip.Turn);
        Assert.Equal(met - 2, trip.ShipX(met), 9);
        Assert.Null(trip.ArrivesAtShip(10));
    }

    [Fact]
    public void EverydaySpeeds_GpsAndMuons()
    {
        // A GPS satellite at about 3.87 km/s loses about 7 µs a day to its speed.
        Assert.InRange(RelativityModel.LagMicrosecondsPerDay(3.874), 7.0, 7.4);

        // A muon at 0.995 c lives about 10 times longer and goes about 6.6 km instead of 0.66.
        var (with, without) = RelativityModel.MuonRange(0.995);
        Assert.InRange(without, 0.64, 0.67);
        Assert.InRange(with, 6.4, 6.7);

        // From 15 km up: almost none would arrive without relativity, a good share with it.
        Assert.True(RelativityModel.MuonsReaching(15, 0.995, relativity: false) < 1e-9);
        Assert.InRange(RelativityModel.MuonsReaching(15, 0.995), 0.08, 0.12);
    }
}
