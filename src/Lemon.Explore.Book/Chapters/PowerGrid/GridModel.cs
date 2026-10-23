namespace Lemon.Explore.Book.Chapters.PowerGrid;

/// <summary>The three views of the generator-and-grid page.</summary>
public enum GridMode
{
    /// <summary>A coil turning between a magnet's poles (or a magnet turning inside three coils), lighting a lamp.</summary>
    Generator,

    /// <summary>A power station's output stepped up, sent along a long line, and stepped down for a town.</summary>
    Transmission,

    /// <summary>A day on a regional grid: what is generated hour by hour, and the frequency when something trips.</summary>
    Balance,
}

/// <summary>How the generator's coil is connected out, or which machine is shown.</summary>
public enum GeneratorKind
{
    /// <summary>Two slip rings: the current flips each half turn.</summary>
    Ac,

    /// <summary>A split ring (commutator): the connections swap each half turn, so the current always flows one way.</summary>
    Dc,

    /// <summary>A power station's machine: a magnet turning inside three coils a third of a turn apart.</summary>
    ThreePhase,
}

/// <summary>Something sudden on the grid, whose frequency is then followed for a few minutes.</summary>
public enum GridEvent
{
    None,

    /// <summary>A 1000 MW generating unit trips off.</summary>
    Trip,

    /// <summary>A long-distance DC link bringing 3000 MW in from far away is lost.</summary>
    Link,

    /// <summary>Demand jumps by 800 MW (everyone switches on at once).</summary>
    Surge,
}

/// <summary>The season of the grid's day: its demand shape and the Sun's height.</summary>
public enum GridSeason
{
    Summer,
    Winter,
}

/// <summary>
/// A classroom generator: a square coil 10 cm across turning in a uniform field, its ends to a 6 V 3 W lamp through
/// slip rings or a commutator. Angles are radians measured from the coil facing the field square on (most flux, no
/// voltage), so the flux goes as cos and the voltage as sin.
/// </summary>
public static class GeneratorModel
{
    /// <summary>The coil's area, m² (10 cm × 10 cm).</summary>
    public const double CoilArea = 0.01;

    /// <summary>The lamp: 6 V 3 W, so 12 Ω hot.</summary>
    public const double LampVolts = 6;

    public const double LampWatts = 3;

    public static double LampResistance => LampVolts * LampVolts / LampWatts;

    /// <summary>The coil's own resistance per turn of wire, Ω.</summary>
    public const double OhmsPerTurn = 0.01;

    /// <summary>The crank handle's radius, m.</summary>
    public const double CrankRadius = 0.1;

    public const int DefaultTurns = 200;

    public const double DefaultField = 0.3;

    public const double DefaultSpeed = 2;

    /// <summary>The coil's resistance, Ω.</summary>
    public static double CoilResistance(int turns) => turns * OhmsPerTurn;

    /// <summary>The peak voltage, N·B·A·ω, V.</summary>
    public static double PeakEmf(int turns, double field, double turnsPerSecond) =>
        turns * field * CoilArea * 2 * Math.PI * turnsPerSecond;

    /// <summary>The magnetic flux through one turn of the coil at <paramref name="angle"/>, Wb.</summary>
    public static double Flux(double field, double angle) => field * CoilArea * Math.Cos(angle);

    /// <summary>The voltage the turning coil makes at <paramref name="angle"/> (minus the rate of change of N·Φ), V.</summary>
    public static double Emf(int turns, double field, double turnsPerSecond, double angle) =>
        PeakEmf(turns, field, turnsPerSecond) * Math.Sin(angle);

    /// <summary>The voltage at the brushes: as made through slip rings, always one way through a commutator.</summary>
    public static double Output(GeneratorKind kind, double emf) => kind == GeneratorKind.Dc ? Math.Abs(emf) : emf;

    /// <summary>The current round the coil and the lamp, A (none with the switch open).</summary>
    public static double Current(int turns, double emf, bool lampOn) => lampOn ? emf / (LampResistance + CoilResistance(turns)) : 0;

    /// <summary>The power the lamp gets, W.</summary>
    public static double LampPower(int turns, double emf, bool lampOn)
    {
        var current = Current(turns, emf, lampOn);
        return current * current * LampResistance;
    }

    /// <summary>The power the hand (or turbine) must put in to keep turning: the voltage times the current, W.</summary>
    public static double InputPower(int turns, double emf, bool lampOn) => emf * Current(turns, emf, lampOn);

    /// <summary>The lamp's average power over a turn, W (half the peak with a sine).</summary>
    public static double AverageLampPower(int turns, double field, double turnsPerSecond, bool lampOn)
    {
        if (!lampOn) return 0;
        var peak = PeakEmf(turns, field, turnsPerSecond) / (LampResistance + CoilResistance(turns));
        return peak * peak * LampResistance / 2;
    }

    /// <summary>The push on the crank handle at this moment, N: the input power over the handle's speed.</summary>
    public static double HandForce(int turns, double emf, bool lampOn, double turnsPerSecond)
    {
        var speed = 2 * Math.PI * CrankRadius * Math.Max(0.01, turnsPerSecond);
        return InputPower(turns, emf, lampOn) / speed;
    }

    /// <summary>
    /// The three coils' voltages for a magnet with <paramref name="polePairs"/> pairs of poles at mechanical angle
    /// <paramref name="angle"/>: each lags the one before by a third of a cycle.
    /// </summary>
    public static (double A, double B, double C) ThreePhase(double peak, double angle, int polePairs)
    {
        var electrical = angle * polePairs;
        return (
            peak * Math.Sin(electrical),
            peak * Math.Sin(electrical - (2 * Math.PI / 3)),
            peak * Math.Sin(electrical - (4 * Math.PI / 3)));
    }

    /// <summary>The frequency, Hz, of a machine with <paramref name="polePairs"/> pole pairs at <paramref name="rpm"/>.</summary>
    public static double Frequency(int polePairs, double rpm) => polePairs * rpm / 60;

    /// <summary>The speed, rpm, a machine with <paramref name="polePairs"/> pole pairs needs for <paramref name="hertz"/>.</summary>
    public static double SynchronousRpm(int polePairs, double hertz) => hertz * 60 / polePairs;
}

/// <summary>What happens to the power on a three-phase line at one voltage.</summary>
/// <param name="Current">The current in each phase wire, A.</param>
/// <param name="Loss">Power turned to heat in the wires, MW.</param>
/// <param name="LossFraction">The share of the power sent that is lost (also the share of the voltage dropped).</param>
/// <param name="Delivered">What arrives, MW (0 when it cannot be done).</param>
/// <param name="Overloaded">The current is more than the wires can carry without overheating.</param>
/// <param name="Impossible">Half or more would be lost: the voltage at the far end would collapse.</param>
public readonly record struct LineState(double Current, double Loss, double LossFraction, double Delivered, bool Overloaded, bool Impossible);

/// <summary>
/// Sending power along a three-phase line. The same wires are used at every voltage (four-conductor bundles of
/// aluminium-steel cable, about 0.02 Ω and 3200 A per phase), so only the voltage changes the story: for the same
/// power the current goes as 1/V and the heat in the wires as 1/V².
/// </summary>
public static class TransmissionModel
{
    /// <summary>A power station's generator voltage, kV.</summary>
    public const double GeneratorKv = 20;

    /// <summary>Resistance of one phase per km, Ω.</summary>
    public const double OhmsPerKm = 0.02;

    /// <summary>What one phase's bundle can carry without overheating, A.</summary>
    public const double Ampacity = 3200;

    /// <summary>The standard line voltages to choose from, kV (China's ladder).</summary>
    public static IReadOnlyList<double> Levels { get; } = [10, 35, 110, 220, 500, 1000];

    public const double DefaultKv = 500;

    public const double DefaultPower = 1000;

    public const double DefaultDistance = 300;

    /// <summary>The voltages on the way down to a home, kV, after a line at <paramref name="lineKv"/>.</summary>
    public static IReadOnlyList<double> StepsDown(double lineKv) => new double[] { 220, 110, 10, 0.4 }.Where(v => v < lineKv).ToList();

    /// <summary>One phase's resistance over <paramref name="km"/>, Ω.</summary>
    public static double Resistance(double km) => OhmsPerKm * km;

    /// <summary>The line carrying <paramref name="megawatts"/> at <paramref name="kv"/> (line to line) over <paramref name="km"/>.</summary>
    public static LineState Send(double megawatts, double kv, double km)
    {
        var watts = megawatts * 1e6;
        var volts = kv * 1e3;
        var current = watts / (Math.Sqrt(3) * volts);
        var resistance = Resistance(km);
        var lossWatts = 3 * current * current * resistance;
        var fraction = lossWatts / watts;
        var impossible = fraction >= 0.5;
        return new LineState(
            current,
            lossWatts / 1e6,
            fraction,
            impossible ? 0 : megawatts - (lossWatts / 1e6),
            current > Ampacity,
            impossible);
    }

    /// <summary>How hot the wires look, 0 cold … 1 at their limit … more when overloaded: the heat per metre against the limit's.</summary>
    public static double Glow(double current) => Math.Pow(current / Ampacity, 2);

    /// <summary>The transformer's turns ratio from the generator up to the line.</summary>
    public static double StepUpRatio(double kv) => kv / GeneratorKv;
}

/// <summary>What each kind of station makes at one hour of the grid's day, GW. Pumped storage is negative while it pumps.</summary>
public readonly record struct GridMix(
    double Hour,
    double Demand,
    double Nuclear,
    double Coal,
    double Gas,
    double Hydro,
    double Pumped,
    double Wind,
    double Solar,
    double Import,
    double Curtailed,
    double Shortfall)
{
    /// <summary>Everything made or brought in (pumping counted as using power), GW: equals the demand unless short.</summary>
    public double Generation => Nuclear + Coal + Gas + Hydro + Pumped + Wind + Solar + Import;

    /// <summary>The share of the demand met without machines spinning in step with the grid (wind, solar, the DC link).</summary>
    public double InverterShare => Demand <= 0 ? 0 : (Wind + Solar + Import) / Demand;
}

/// <summary>
/// A day on a regional grid (peak 40 GW in summer): an hourly demand shape, wind and solar as the weather gives them,
/// nuclear flat out, a long-distance DC link bringing power in from far away, and coal, hydro, gas and pumped storage
/// dispatched to fill the rest. Solar follows the Sun's height
/// at the viewer's latitude; hours are local solar time.
/// </summary>
public static class GridDayModel
{
    public const double SummerPeak = 40;

    public const double WinterPeak = 36;

    public const double NuclearCapacity = 4;

    public const double CoalCapacity = 20;

    /// <summary>Coal units cannot turn down below this without being shut down, GW.</summary>
    public const double CoalMinimum = 8;

    public const double GasCapacity = 5;

    public const double HydroCapacity = 7;

    public const double PumpedCapacity = 2.4;

    public const double WindCapacity = 8;

    public const double DefaultSolar = 12;

    /// <summary>What the DC link brings in from far away all day, GW.</summary>
    public const double ImportCapacity = 3;

    public const double MaxSolar = 30;

    private static readonly double[] SummerShape =
    [
        0.72, 0.67, 0.64, 0.62, 0.62, 0.64, 0.70, 0.78, 0.85, 0.89, 0.92, 0.93,
        0.90, 0.92, 0.95, 0.96, 0.95, 0.94, 0.95, 0.98, 1.00, 0.99, 0.92, 0.82,
    ];

    private static readonly double[] WinterShape =
    [
        0.70, 0.65, 0.62, 0.61, 0.62, 0.65, 0.72, 0.84, 0.92, 0.95, 0.94, 0.92,
        0.86, 0.86, 0.88, 0.89, 0.91, 0.96, 1.00, 1.00, 0.97, 0.92, 0.85, 0.77,
    ];

    /// <summary>The hour wrapped into 0…24.</summary>
    public static double Wrap(double hour) => ((hour % 24) + 24) % 24;

    /// <summary>The demand at <paramref name="hour"/>, GW: the hourly shape joined smoothly (Catmull–Rom, wrapping at midnight).</summary>
    public static double Demand(GridSeason season, double hour)
    {
        var shape = season == GridSeason.Summer ? SummerShape : WinterShape;
        var peak = season == GridSeason.Summer ? SummerPeak : WinterPeak;
        var h = Wrap(hour);
        var i = (int)Math.Floor(h);
        var t = h - i;
        double P(int k) => shape[((k % 24) + 24) % 24];
        var p0 = P(i - 1);
        var p1 = P(i);
        var p2 = P(i + 1);
        var p3 = P(i + 2);
        var value = 0.5 * ((2 * p1) + ((-p0 + p2) * t) + (((2 * p0) - (5 * p1) + (4 * p2) - p3) * t * t) + ((-p0 + (3 * p1) - (3 * p2) + p3) * t * t * t));
        return value * peak;
    }

    /// <summary>The Sun's declination for the season, degrees, on the viewer's side of the equator (summer is summer there).</summary>
    public static double Declination(GridSeason season, double latitude)
    {
        var northern = season == GridSeason.Summer ? 21.0 : -21.0;
        return latitude < 0 ? -northern : northern;
    }

    /// <summary>The sine of the Sun's height at <paramref name="hour"/> (local solar time).</summary>
    public static double SunHeight(GridSeason season, double latitude, double hour)
    {
        var phi = latitude * Math.PI / 180;
        var delta = Declination(season, latitude) * Math.PI / 180;
        var hourAngle = (Wrap(hour) - 12) * 15 * Math.PI / 180;
        return (Math.Sin(phi) * Math.Sin(delta)) + (Math.Cos(phi) * Math.Cos(delta) * Math.Cos(hourAngle));
    }

    /// <summary>What solar farms of <paramref name="capacity"/> GW make on a clear day, GW.</summary>
    public static double Solar(GridSeason season, double latitude, double hour, double capacity)
    {
        var height = SunHeight(season, latitude, hour);
        return height <= 0 ? 0 : capacity * 0.78 * Math.Pow(height, 1.15);
    }

    /// <summary>What the wind farms make, GW: usually more at night than in the afternoon, and more in winter.</summary>
    public static double Wind(GridSeason season, double hour)
    {
        var share = 0.28 + (0.1 * Math.Cos(2 * Math.PI * (Wrap(hour) - 3) / 24));
        return WindCapacity * share * (season == GridSeason.Winter ? 1.2 : 1);
    }

    /// <summary>Hydro running all day on the river's flow, GW (more water in summer).</summary>
    public static double HydroBase(GridSeason season) => season == GridSeason.Summer ? 2.6 : 1.4;

    /// <summary>
    /// The hour's mix: nuclear, the DC link, wind, solar and the river's hydro go first; coal fills the rest between its
    /// minimum and its maximum; above that hydro peaks, then pumped storage gives its water back, then gas; below the coal
    /// minimum pumped storage pumps, and solar and wind are turned back (curtailed).
    /// </summary>
    public static GridMix Mix(GridSeason season, double latitude, double hour, double solarCapacity)
    {
        var demand = Demand(season, hour);
        var nuclear = NuclearCapacity;
        var wind = Wind(season, hour);
        var solar = Solar(season, latitude, hour, solarCapacity);
        var hydro = HydroBase(season);
        var import = ImportCapacity;
        var rest = demand - nuclear - import - wind - solar - hydro;

        var coal = Math.Clamp(rest, CoalMinimum, CoalCapacity);
        var over = rest - coal;
        double gas = 0, pumped = 0, curtailed = 0, shortfall = 0;
        if (over > 0)
        {
            var peaking = Math.Min(over, HydroCapacity - hydro);
            hydro += peaking;
            over -= peaking;
            pumped = Math.Min(over, PumpedCapacity);
            over -= pumped;
            gas = Math.Min(over, GasCapacity);
            over -= gas;
            shortfall = Math.Max(0, over);
        }
        else if (over < 0)
        {
            // Too much: store what pumped storage can take, then turn solar and wind back.
            var spare = -over;
            pumped = -Math.Min(spare, PumpedCapacity);
            spare += pumped;
            var fromSolar = Math.Min(spare, solar);
            solar -= fromSolar;
            spare -= fromSolar;
            var fromWind = Math.Min(spare, wind);
            wind -= fromWind;
            curtailed = fromSolar + fromWind;
        }

        return new GridMix(Wrap(hour), demand, nuclear, coal, gas, hydro, pumped, wind, solar, import, curtailed, shortfall);
    }

    /// <summary>
    /// The grid's inertia constant, s: the spinning machines' stored energy over the demand. Steam sets (nuclear, coal,
    /// gas) store about 5 s of their rating, hydro about 3 s; units are taken to run at 85% of their rating; wind, solar
    /// and the DC link add nothing.
    /// </summary>
    public static double Inertia(GridMix mix)
    {
        var steam = (mix.Nuclear + mix.Coal + mix.Gas) / 0.85 * 5;
        var water = (mix.Hydro + Math.Abs(mix.Pumped)) / 0.85 * 3;
        return (steam + water) / Math.Max(1, mix.Demand);
    }

    /// <summary>The capacity online that can answer a frequency change (coal, gas and hydro), GW.</summary>
    public static double GovernedCapacity(GridMix mix) => (mix.Coal + mix.Gas + mix.Hydro + Math.Max(0, mix.Pumped)) / 0.85;
}

/// <summary>One moment after a sudden event: the frequency and who is making up the difference.</summary>
/// <param name="Time">Seconds after the event.</param>
/// <param name="Frequency">Hz.</param>
/// <param name="Governors">Extra output from the governors' quick response, GW.</param>
/// <param name="Control">Extra output asked for by the control centre (AGC), GW.</param>
/// <param name="LoadEase">Demand that eased because motors turn slower, GW.</param>
/// <param name="Shed">Demand cut off by under-frequency relays, GW.</param>
public readonly record struct FrequencyPoint(double Time, double Frequency, double Governors, double Control, double LoadEase, double Shed);

/// <summary>
/// The grid's frequency after an event, as one spinning mass: the swing equation 2H·S/f₀·df/dt = ΔP, with governors
/// (5% droop, a few seconds' lag, limited reserve), demand easing as motors slow (1.5% per 1%), the control centre's
/// slow correction (AGC), and two stages of under-frequency load shedding at 49.0 and 48.8 Hz.
/// </summary>
public static class FrequencyModel
{
    public const double Nominal = 50;

    /// <summary>How long an event is followed, s.</summary>
    public const double Duration = 180;

    /// <summary>The trace's time step, s.</summary>
    public const double Step = 0.05;

    public const double Droop = 0.05;

    public const double GovernorLag = 6;

    public const double LoadDamping = 1.5;

    /// <summary>The share of the governed capacity held back for the quick response.</summary>
    public const double ReserveShare = 0.08;

    /// <summary>How fast the control centre's correction works, per second, once it starts.</summary>
    public const double ControlRate = 0.04;

    /// <summary>The control centre acts only after this, s.</summary>
    public const double ControlDelay = 10;

    public static IReadOnlyList<double> SheddingStages { get; } = [49.0, 48.8];

    /// <summary>Each shedding stage cuts this share of the demand.</summary>
    public const double ShedShare = 0.05;

    /// <summary>The power lost (or demand added) by an event, GW.</summary>
    public static double Size(GridEvent kind) => kind switch
    {
        GridEvent.Trip => 1.0,
        GridEvent.Link => 3.0,
        GridEvent.Surge => 0.8,
        _ => 0,
    };

    /// <summary>The frequency every <see cref="Step"/> s for <see cref="Duration"/> s after losing <paramref name="lost"/> GW.</summary>
    public static IReadOnlyList<FrequencyPoint> Simulate(double lost, double demand, double inertia, double governed)
    {
        var points = new List<FrequencyPoint>((int)(Duration / Step) + 2);
        var deviation = 0.0;
        var governors = 0.0;
        var control = 0.0;
        var shed = 0.0;
        var stage = 0;
        var reserve = ReserveShare * governed;
        var gain = governed / (Droop * Nominal);
        var stiffness = gain + (LoadDamping * demand / Nominal);
        const int sub = 10;
        var h = Step / sub;
        for (var i = 0; ; i++)
        {
            var time = i * Step;
            var ease = LoadDamping * demand * -deviation / Nominal;
            points.Add(new FrequencyPoint(time, Nominal + deviation, governors, control, ease, shed));
            if (time >= Duration) break;
            for (var k = 0; k < sub; k++)
            {
                var t = time + (k * h);
                var target = Math.Clamp(-deviation * gain, -reserve, reserve);
                governors += (target - governors) * h / GovernorLag;
                if (t >= ControlDelay) control += -deviation * stiffness * ControlRate * h;
                var balance = governors + control + shed - lost - (LoadDamping * demand * deviation / Nominal);
                deviation += Nominal / (2 * inertia * demand) * balance * h;
                if (stage < SheddingStages.Count && Nominal + deviation <= SheddingStages[stage])
                {
                    shed += ShedShare * demand;
                    stage++;
                }
            }
        }

        return points;
    }

    /// <summary>The point of a trace at <paramref name="time"/> seconds (the nearest step).</summary>
    public static FrequencyPoint At(IReadOnlyList<FrequencyPoint> trace, double time)
    {
        if (trace.Count == 0) return new FrequencyPoint(0, Nominal, 0, 0, 0, 0);
        var i = (int)Math.Round(Math.Clamp(time, 0, Duration) / Step);
        return trace[Math.Clamp(i, 0, trace.Count - 1)];
    }

    /// <summary>The lowest point of a trace (the nadir).</summary>
    public static FrequencyPoint Lowest(IReadOnlyList<FrequencyPoint> trace) => trace.MinBy(p => p.Frequency);

    /// <summary>How fast the frequency falls at first, Hz/s: f₀·ΔP / (2H·S).</summary>
    public static double InitialFall(double lost, double demand, double inertia) => Nominal * lost / (2 * inertia * demand);
}
