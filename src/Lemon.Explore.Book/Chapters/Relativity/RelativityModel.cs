namespace Lemon.Explore.Book.Chapters.Relativity;

/// <summary>The relativity page's three views.</summary>
public enum RelativityMode
{
    /// <summary>A light clock standing still beside one riding a fast ship: the moving one ticks slower.</summary>
    LightClock,

    /// <summary>Einstein's train: a flash from its middle reaches both ends at once on the train, not on the platform.</summary>
    Simultaneity,

    /// <summary>The twins on a spacetime diagram: one stays home, one flies to a star and back and comes home younger.</summary>
    Twins,
}

/// <summary>Whose point of view the light-clock and train views are drawn from.</summary>
public enum RelativityFrame
{
    /// <summary>Standing on the ground (the platform): the ship or train moves to the right.</summary>
    Ground,

    /// <summary>Riding the ship or train: the ground moves to the left.</summary>
    Ship,
}

/// <summary>
/// Special relativity in units where the speed of light is 1 (c = 1): speeds are fractions of light's (β), times in
/// any unit and distances in that unit times c (years and light-years in the twins' view). Everything the page draws
/// and says comes from here, so the tests check it.
/// </summary>
public static class RelativityModel
{
    /// <summary>The speed of light, km/s (exact by definition of the metre).</summary>
    public const double LightKmPerSecond = 299_792.458;

    /// <summary>A muon's mean lifetime at rest, seconds (PDG: 2.1969811 µs).</summary>
    public const double MuonLifetimeSeconds = 2.1969811e-6;

    /// <summary>The Lorentz factor γ = 1 / √(1 − β²): how many times slower a moving clock ticks and shorter a moving rod is.</summary>
    public static double Gamma(double beta)
    {
        var b = Math.Clamp(Math.Abs(beta), 0, 0.999999999);
        return 1 / Math.Sqrt(1 - (b * b));
    }

    /// <summary>The speed whose Lorentz factor is <paramref name="gamma"/> (≥ 1).</summary>
    public static double BetaForGamma(double gamma) => gamma <= 1 ? 0 : Math.Sqrt(1 - (1 / (gamma * gamma)));

    /// <summary>
    /// γ − 1 for small speeds without losing it to rounding (β²/2 + 3β⁴/8 + …): the fraction a clock on a plane or a
    /// satellite falls behind.
    /// </summary>
    public static double GammaMinusOne(double beta)
    {
        var b2 = beta * beta;
        if (b2 > 1e-4) return Gamma(beta) - 1;
        return (b2 / 2) + (3 * b2 * b2 / 8) + (5 * b2 * b2 * b2 / 16);
    }

    #region the light clock

    /// <summary>
    /// The light clock: light goes up and back down between two mirrors <paramref name="height"/> apart. Seen moving at
    /// <paramref name="beta"/>, its light runs slanted at c, so going up takes γ times as long: one tick = 2γh.
    /// </summary>
    public static double TickTime(double height, double beta) => 2 * height * Gamma(beta);

    /// <summary>
    /// Where a light clock's flash is at time <paramref name="t"/> of the frame watching it, measured from the bottom
    /// mirror (0) up to the top (1), and how many ticks (round trips) it has done. The clock moves at <paramref name="beta"/>.
    /// </summary>
    public static (double Height, int Ticks, bool GoingUp) Flash(double t, double height, double beta)
    {
        var tick = TickTime(height, beta);
        if (t <= 0) return (0, 0, true);
        var ticks = (int)Math.Floor(t / tick);
        var phase = (t - (ticks * tick)) / tick;
        return phase < 0.5 ? (phase * 2, ticks, true) : ((1 - phase) * 2, ticks, false);
    }

    /// <summary>
    /// The light-clock triangle for one way up: in the time the light goes h up its own clock, the frame sees it go the
    /// slanted side ct while the clock moves vt along. ct² = h² + (vt)², so t = γh.
    /// </summary>
    public static (double Up, double Along, double Slant) Triangle(double height, double beta)
    {
        var t = height * Gamma(beta);
        return (height, beta * t, t);
    }

    #endregion

    #region the train

    /// <summary>
    /// Einstein's train, seen from the platform: the train (rest length <paramref name="length"/>) moves at
    /// <paramref name="beta"/>, a flash leaves its middle at t = 0, x = 0, and light runs both ways at c. The train is
    /// L/γ long; its back runs into the light, its front runs away from it. Returns when (platform time) the flash hits
    /// the back and the front, and where.
    /// </summary>
    public static (double RearTime, double RearX, double FrontTime, double FrontX) TrainHits(double length, double beta)
    {
        var half = length / 2 / Gamma(beta);
        var rearTime = half / (1 + beta);
        var frontTime = half / (1 - beta);
        return (rearTime, -half + (beta * rearTime), frontTime, half + (beta * frontTime));
    }

    /// <summary>How long after the back the front is hit, seen from the platform: γβL / c. On the train it is 0.</summary>
    public static double HitGap(double length, double beta) => Gamma(beta) * beta * length;

    /// <summary>
    /// The Lorentz transformation into the frame moving at <paramref name="beta"/> along x:
    /// t' = γ(t − βx), x' = γ(x − βt).
    /// </summary>
    public static (double T, double X) Boost(double t, double x, double beta)
    {
        var g = Gamma(beta);
        return (g * (t - (beta * x)), g * (x - (beta * t)));
    }

    /// <summary>
    /// What a clock on the moving train reads at platform time <paramref name="t"/> if it is <paramref name="along"/> from
    /// the train's middle (positive towards the front) and the train's clocks were set together on the train, all reading
    /// 0 when the flash left the middle: t' = t/γ − β·along. The front's clock is behind the back's, seen from the platform.
    /// </summary>
    public static double TrainClock(double t, double along, double beta) => (t / Gamma(beta)) - (beta * along);

    #endregion

    #region the twins

    /// <summary>
    /// The twins' trip: the traveller flies at <paramref name="beta"/> to a star <paramref name="distance"/> light-years
    /// away and straight back. Earth's clock: 2D/β years. The traveller's: γ times less. The trip out is D/γ long on the
    /// ship, because the ship sees the distance shortened.
    /// </summary>
    public static TwinTrip Trip(double distance, double beta)
    {
        var b = Math.Clamp(beta, 0.01, 0.999);
        var g = Gamma(b);
        var earthYears = 2 * distance / b;
        return new TwinTrip(distance, b, g, earthYears, earthYears / g, distance / g);
    }

    #endregion

    #region everyday speeds

    /// <summary>
    /// How much a clock moving at <paramref name="kmPerSecond"/> falls behind in a day, from speed alone (no gravity),
    /// in microseconds: (γ − 1) × 86 400 s.
    /// </summary>
    public static double LagMicrosecondsPerDay(double kmPerSecond) => GammaMinusOne(kmPerSecond / LightKmPerSecond) * 86_400e6;

    /// <summary>How far a muon at <paramref name="beta"/> goes in its average life, km: βγ·c·τ (without relativity βcτ).</summary>
    public static (double WithRelativity, double Without) MuonRange(double beta)
    {
        var without = beta * LightKmPerSecond * MuonLifetimeSeconds;
        return (without * Gamma(beta), without);
    }

    /// <summary>
    /// The share of muons made at <paramref name="heightKm"/> that reach the ground falling straight down at
    /// <paramref name="beta"/>: e^(−t/γτ) with t the ground's time for the fall.
    /// </summary>
    public static double MuonsReaching(double heightKm, double beta, bool relativity = true)
    {
        var t = heightKm / (Math.Max(beta, 1e-6) * LightKmPerSecond);
        var life = MuonLifetimeSeconds * (relativity ? Gamma(beta) : 1);
        return Math.Exp(-t / life);
    }

    #endregion
}

/// <summary>
/// One trip of the twin paradox, in years and light-years: the star's distance, the speed, γ, the time Earth's twin
/// waits, the time the traveller lives through, and the star's distance as the ship measures it.
/// </summary>
public sealed record TwinTrip(double Distance, double Beta, double Gamma, double EarthYears, double ShipYears, double ShipDistance)
{
    /// <summary>Earth time of the turnaround.</summary>
    public double Turn => EarthYears / 2;

    /// <summary>How much younger the traveller comes home.</summary>
    public double Younger => EarthYears - ShipYears;

    /// <summary>The ship's distance from Earth at Earth time <paramref name="t"/> (light-years).</summary>
    public double ShipX(double t) => t <= 0 ? 0 : t >= EarthYears ? 0 : t <= Turn ? Beta * t : Distance - (Beta * (t - Turn));

    /// <summary>The traveller's own time at Earth time <paramref name="t"/> (years).</summary>
    public double ShipTime(double t) => Math.Clamp(t, 0, EarthYears) / Gamma;

    /// <summary>
    /// The Earth time the traveller calls "now" when its own clock reads <paramref name="tau"/>. The ship's line of "now"
    /// (t − βx constant on the way out) meets Earth's worldline at t_ship − β·x_ship, which is τ/γ: Earth's clock looks
    /// slow to the ship too. On the way back the line tilts the other way, t_ship + β·x_ship, so at the turn it jumps.
    /// </summary>
    public double EarthNowForShip(double tau)
    {
        var t = Math.Clamp(tau, 0, ShipYears) * Gamma;
        var x = ShipX(t);
        return t <= Turn ? t - (Beta * x) : t + (Beta * x);
    }

    /// <summary>
    /// The Earth time a light signal sent by the ship at Earth time <paramref name="sent"/> reaches Earth: it has the
    /// ship's distance to cover at c.
    /// </summary>
    public double ArrivesAtEarth(double sent) => sent + ShipX(sent);

    /// <summary>
    /// The Earth time a light signal sent from Earth at <paramref name="sent"/> catches the ship (it may meet the ship on
    /// its way back), or null when it only gets there after the ship is home.
    /// </summary>
    public double? ArrivesAtShip(double sent)
    {
        if (sent >= EarthYears) return null;

        // Outbound: sent + x = β t  →  t = sent / (1 − β), if before the turn.
        var outbound = sent / (1 - Beta);
        if (outbound <= Turn) return outbound;

        // Inbound: x = D − β (t − T), light x = t − sent  →  t − sent = D − β t + β T.
        var inbound = (Distance + (Beta * Turn) + sent) / (1 + Beta);
        return inbound <= EarthYears ? inbound : null;
    }
}
