using Lemon.Explore.Book.Chapters.Projectile;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class ProjectilePhysicsTests
{
    private const double G = 9.81;

    [Theory]
    [InlineData(20, 45, 0)]
    [InlineData(20, 30, 0)]
    [InlineData(35, 60, 12)]
    [InlineData(8, 0, 20)]
    [InlineData(50, 85, 3)]
    public void WithoutAirTheFlightMatchesTheFormulas(double speed, double angle, double height)
    {
        var flight = ProjectilePhysics.Fly(speed, angle, height, G, 0);

        Assert.Equal(ProjectilePhysics.IdealRange(speed, angle, height, G), flight.Range, 6);
        Assert.Equal(ProjectilePhysics.IdealPeak(speed, angle, height, G), flight.Peak, 6);
        Assert.Equal(ProjectilePhysics.IdealTime(speed, angle, height, G), flight.Duration, 6);

        // Energy is kept: it lands at √(v² + 2gh), as fast as if it had been thrown straight down.
        Assert.Equal(Math.Sqrt((speed * speed) + (2 * G * height)), flight.Landing.Speed, 6);

        // Across, it never slows down.
        Assert.All(flight.Points, p => Assert.Equal(flight.Launch.Vx, p.Vx, 9));
    }

    [Fact]
    public void FromTheGroundFortyFiveDegreesGoesFarthestAndComplementaryAnglesLandTogether()
    {
        Assert.Equal(45, ProjectilePhysics.IdealBestAngle(25, 0, G), 9);
        Assert.Equal(25 * 25 / G, ProjectilePhysics.IdealMaxRange(25, 0, G), 9);
        Assert.Equal(ProjectilePhysics.IdealRange(25, 30, 0, G), ProjectilePhysics.IdealRange(25, 60, 0, G), 9);
        Assert.Equal(ProjectilePhysics.IdealRange(25, 15, 0, G), ProjectilePhysics.IdealRange(25, 75, 0, G), 9);

        // The search agrees with the formula, also from up high (where a flatter throw goes farther).
        var high = ProjectilePhysics.IdealBestAngle(15, 20, G);
        Assert.InRange(high, 30, 40);
        Assert.Equal(ProjectilePhysics.IdealMaxRange(15, 20, G), ProjectilePhysics.IdealRange(15, high, 20, G), 9);
        Assert.True(ProjectilePhysics.IdealRange(15, high, 20, G) > ProjectilePhysics.IdealRange(15, 45, 20, G));
    }

    [Fact]
    public void NoPathGoesAboveTheEnvelopeAndSteepOnesTouchIt()
    {
        const double speed = 20;
        foreach (var angle in new[] { 20.0, 45, 60, 75 })
        {
            var flight = ProjectilePhysics.Fly(speed, angle, 0, G, 0);
            var gaps = flight.Points.Select(p => ProjectilePhysics.Envelope(p.X, speed, 0, G) - p.Y).ToList();
            Assert.All(gaps, gap => Assert.True(gap > -1e-6));

            // A path touches it at x = v²/(g·tan θ): in the air from 45° up, below the ground for flatter throws.
            Assert.Equal(angle >= 45, gaps.Min() < 0.01);
        }
    }

    [Fact]
    public void AirShortensTheThrowAndLowersTheBestAngle()
    {
        var baseball = ProjectilePhysics.DragFactor(ProjectileBall.Baseball, ProjectilePlace.Earth, true);
        var pingPong = ProjectilePhysics.DragFactor(ProjectileBall.PingPong, ProjectilePlace.Earth, true);
        var shot = ProjectilePhysics.DragFactor(ProjectileBall.ShotPut, ProjectilePlace.Earth, true);

        var ideal = ProjectilePhysics.IdealRange(40, 45, 0, G);
        var withAir = ProjectilePhysics.Range(40, 45, 0, G, baseball);
        Assert.InRange(withAir / ideal, 0.4, 0.8);

        var bestBaseball = ProjectilePhysics.BestAngle(40, 0, G, baseball);
        var bestPingPong = ProjectilePhysics.BestAngle(40, 0, G, pingPong);
        var bestShot = ProjectilePhysics.BestAngle(14, 0, G, shot);
        Assert.InRange(bestBaseball, 33, 43);
        Assert.InRange(bestPingPong, 15, bestBaseball - 3);
        Assert.InRange(bestShot, 44.5, 45);

        // The best angle beats its neighbours.
        var best = ProjectilePhysics.Range(40, bestBaseball, 0, G, baseball);
        Assert.True(best >= ProjectilePhysics.Range(40, bestBaseball - 2, 0, G, baseball));
        Assert.True(best >= ProjectilePhysics.Range(40, bestBaseball + 2, 0, G, baseball));

        // With air the way down is steeper than the way up.
        var flight = ProjectilePhysics.Fly(40, 45, 0, G, baseball);
        Assert.True(flight.LandingAngle > 50);
        Assert.True(flight.PeakPoint.X > flight.Range / 2);
    }

    [Fact]
    public void TerminalSpeedsAreAboutRight()
    {
        double Terminal(ProjectileBall ball) =>
            ProjectilePhysics.TerminalSpeed(G, ProjectilePhysics.DragFactor(ball, ProjectilePlace.Earth, true));

        Assert.InRange(Terminal(ProjectileBall.Baseball), 36, 44);
        Assert.InRange(Terminal(ProjectileBall.PingPong), 7, 10);
        Assert.InRange(Terminal(ProjectileBall.Shuttlecock), 5, 7.5);
        Assert.InRange(Terminal(ProjectileBall.Soccer), 22, 32);
        Assert.True(Terminal(ProjectileBall.ShotPut) > 120);
        Assert.True(double.IsPositiveInfinity(
            ProjectilePhysics.TerminalSpeed(1.62, ProjectilePhysics.DragFactor(ProjectileBall.PingPong, ProjectilePlace.Moon, true))));

        // A shuttlecock dropped from 50 m falls most of the way at its terminal speed.
        var k = ProjectilePhysics.DragFactor(ProjectileBall.Shuttlecock, ProjectilePlace.Earth, true);
        var drop = ProjectilePhysics.Fly(0, 0, 50, G, k);
        Assert.Equal(Terminal(ProjectileBall.Shuttlecock), drop.Landing.Speed, 2);
    }

    [Fact]
    public void BetweenStepsThePositionFollowsTheCurve()
    {
        var flight = ProjectilePhysics.Fly(30, 50, 5, G, 0);
        foreach (var t in new[] { 0.0, 0.123, 1.5, 2.71828, flight.Duration * 0.999 })
        {
            var at = flight.At(t);
            var vx = 30 * Math.Cos(50 * Math.PI / 180);
            var vy = 30 * Math.Sin(50 * Math.PI / 180);
            Assert.Equal(vx * t, at.X, 6);
            Assert.Equal(5 + (vy * t) - (G * t * t / 2), at.Y, 6);
        }

        Assert.Equal(flight.Landing, flight.At(flight.Duration + 3));
        Assert.Equal(FlightStage.Rising, ProjectilePhysics.Stage(flight, 0.2));
        Assert.Equal(FlightStage.Top, ProjectilePhysics.Stage(flight, flight.PeakPoint.Time));
        Assert.Equal(FlightStage.Falling, ProjectilePhysics.Stage(flight, flight.Duration - 0.3));
        Assert.Equal(FlightStage.Landed, ProjectilePhysics.Stage(flight, flight.Duration));
    }

    [Fact]
    public void ThrownLevelOrDownFromTheGroundIsDownAlready()
    {
        var flight = ProjectilePhysics.Fly(20, 0, 0, G, 0);
        Assert.Equal(0, flight.Duration);
        Assert.Equal(0, flight.Range);
        Assert.Single(flight.Points);
    }

    [Fact]
    public void FallingDistancesInEqualTimesGoOneThreeFiveSeven()
    {
        var steps = Enumerable.Range(0, 5).Select(i => ProjectilePhysics.FallDistance(i * 0.3, G)).ToList();
        var first = steps[1] - steps[0];
        for (var i = 1; i < 4; i++)
        {
            Assert.Equal((2 * i) + 1, (steps[i + 1] - steps[i]) / first, 9);
        }

        Assert.Equal(2.0193, ProjectilePhysics.FallTime(20, G), 4);
    }

    [Fact]
    public void TheDartAlwaysMeetsTheMonkeyIfItIsFastEnough()
    {
        var min = ProjectilePhysics.MonkeyMinSpeed(G);
        Assert.InRange(min, 19, 19.5);

        foreach (var speed in new[] { min + 0.01, 25, 40 })
        {
            var (time, outcome) = ProjectilePhysics.MonkeyEnd(speed, G, letsGo: true);
            Assert.Equal(MonkeyOutcome.Hit, outcome);
            var dart = ProjectilePhysics.Dart(speed, G, time);
            Assert.Equal(ProjectilePhysics.MonkeyDistance, dart.X, 9);
            Assert.Equal(ProjectilePhysics.MonkeyY(G, time, letsGo: true), dart.Y, 9);
        }

        Assert.Equal(MonkeyOutcome.Short, ProjectilePhysics.MonkeyEnd(min - 0.5, G, letsGo: true).Outcome);

        // Holding on, the monkey stays put and the dart passes below by the distance it fell.
        var (held, below) = ProjectilePhysics.MonkeyEnd(30, G, letsGo: false);
        Assert.Equal(MonkeyOutcome.Below, below);
        var passing = ProjectilePhysics.Dart(30, G, held);
        Assert.Equal(ProjectilePhysics.MonkeyHeight - ProjectilePhysics.FallDistance(held, G), passing.Y, 9);
    }

    [Fact]
    public void StrobeAndPlaybackKeepLongFlightsWatchable()
    {
        Assert.Equal(0.2, ProjectilePhysics.StrobeInterval(2.9));
        Assert.Equal(0.5, ProjectilePhysics.StrobeInterval(10));
        Assert.InRange(10 / ProjectilePhysics.StrobeInterval(10), 12, 24);
        Assert.Equal(1, ProjectilePhysics.PlaybackBoost(4));
        Assert.Equal(9, ProjectilePhysics.PlaybackBoost(52));
    }
}
