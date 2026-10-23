namespace Lemon.Explore.Book.Chapters.HeatEngines;

/// <summary>The three views of the engine-and-fridge page.</summary>
public enum HeatMode
{
    /// <summary>A four-stroke petrol engine: one cylinder cut open, turning.</summary>
    Engine,

    /// <summary>A household fridge: the refrigerant going round, taking heat out and putting it behind.</summary>
    Fridge,

    /// <summary>The ideal (Carnot) cycle between a hot and a cold place, run forwards or backwards.</summary>
    Cycle,
}

/// <summary>The four strokes, each half a turn of the crank.</summary>
public enum EngineStroke
{
    Intake,
    Compression,
    Power,
    Exhaust,
}

/// <summary>Which way the cycle runs: heat to work, or work moving heat uphill (to cool, or to warm).</summary>
public enum CycleDirection
{
    Engine,
    Fridge,
    HeatPump,
}

/// <summary>The part of the fridge's refrigerant loop a point is in, in the order the refrigerant goes round.</summary>
public enum FridgePart
{
    Compressor,
    Discharge,
    Condenser,
    Capillary,
    Evaporator,
    Suction,
}

/// <summary>The gas in the cylinder at one crank angle.</summary>
public readonly record struct EngineState(double Angle, EngineStroke Stroke, double Volume, double Pressure, double Temperature,
    double Burned, double PistonTravel, double IntakeLift, double ExhaustLift);

/// <summary>
/// One cylinder of a four-stroke petrol engine, worked out from the crank angle (0…720°, 0 = top dead centre at the start
/// of the intake stroke). The engine is a common 2.0-litre four-cylinder: 86 mm bore and stroke, 145 mm rods.
/// </summary>
/// <remarks>
/// Pressures in bar, temperatures in kelvin, volumes in m³. The gas is compressed and expanded polytropically
/// (n = 1.32, which allows for heat lost to the walls); the charge burns from the spark over about 50° of crank (a Wiebe
/// curve), raising its temperature by up to 2200 K; the exhaust valve opens 40° before the bottom and the pressure falls
/// to just above the outside air's. The pumping strokes sit at 0.95 and 1.08 times the outside pressure (full throttle).
/// Every pressure is proportional to the outside air's, so an engine high up, where the air is thin, gives less power in
/// proportion.
/// </remarks>
public static class EngineModel
{
    public const double Bore = 0.086;
    public const double Stroke = 0.086;
    public const double RodLength = 0.145;
    public const int Cylinders = 4;

    public const double CrankRadius = Stroke / 2;

    public const double DefaultCompressionRatio = 10.5;
    public const double MinCompressionRatio = 7;
    public const double MaxCompressionRatio = 14;

    /// <summary>The exponent of compression and expansion; below air's 1.4 because the walls take some heat.</summary>
    public const double Polytropic = 1.32;

    /// <summary>The fresh charge, warmed by the hot intake port and the gas left from the last cycle, kelvin.</summary>
    public const double IntakeTemperature = 330;

    /// <summary>How much burning the charge would heat it at constant volume, kelvin.</summary>
    public const double CombustionRise = 2200;

    /// <summary>The spark, 20° before top dead centre of compression, so half the charge has burned about 8° after it.</summary>
    public const double SparkAngle = 340;

    public const double BurnDuration = 50;

    /// <summary>The exhaust valve opens 40° before the bottom of the power stroke, letting the pressure out.</summary>
    public const double ExhaustOpens = 500;

    /// <summary>The exhaust valve closes 10° after top dead centre; the intake opens 10° before it (valve overlap).</summary>
    public const double ExhaustCloses = 730;

    public const double IntakeOpens = 710;

    /// <summary>The intake valve closes 40° after the bottom, while the inrushing charge still fills the cylinder.</summary>
    public const double IntakeCloses = 220;

    public const double IntakeFactor = 0.95;
    public const double ExhaustFactor = 1.08;

    /// <summary>Gas constant of air, J/(kg·K).</summary>
    public const double AirGasConstant = 287.05;

    public const double SeaLevelPressure = 1.01325;

    public static double PistonArea => Math.PI * Bore * Bore / 4;

    /// <summary>Swept volume of one cylinder, m³ (about half a litre).</summary>
    public static double Displacement => PistonArea * Stroke;

    public static double ClearanceVolume(double ratio) => Displacement / (ratio - 1);

    /// <summary>How far the piston is below top dead centre, m.</summary>
    public static double PistonTravel(double angle)
    {
        var theta = angle * Math.PI / 180;
        var sin = Math.Sin(theta);
        return CrankRadius + RodLength - ((CrankRadius * Math.Cos(theta)) + Math.Sqrt((RodLength * RodLength) - (CrankRadius * CrankRadius * sin * sin)));
    }

    public static double Volume(double angle, double ratio) => ClearanceVolume(ratio) + (PistonArea * PistonTravel(angle));

    public static double Wrap(double angle) => ((angle % 720) + 720) % 720;

    public static EngineStroke StrokeAt(double angle) => (EngineStroke)Math.Clamp((int)(Wrap(angle) / 180), 0, 3);

    /// <summary>How much of the charge has burned: none before the spark, all of it 50° later (Wiebe, a = 5, m = 2).</summary>
    public static double BurnedFraction(double angle)
    {
        var a = Wrap(angle);
        if (a <= SparkAngle) return 0;
        var t = Math.Min(1, (a - SparkAngle) / BurnDuration);
        return 1 - Math.Exp(-5 * t * t * t);
    }

    /// <summary>The pressure rise of burning as a multiple of the compressed charge's pressure.</summary>
    private static double BurnRatio(double ratio) => CombustionRise / (IntakeTemperature * Math.Pow(ratio, Polytropic - 1));

    /// <summary>The gas in the cylinder at a crank angle, with the outside air at <paramref name="ambient"/> bar.</summary>
    public static EngineState At(double angle, double ratio = DefaultCompressionRatio, double ambient = SeaLevelPressure)
    {
        var a = Wrap(angle);
        var volume = Volume(a, ratio);
        var bottom = Volume(180, ratio);
        var intake = IntakeFactor * ambient;
        var exhaust = ExhaustFactor * ambient;
        var burn = BurnRatio(ratio);

        double pressure, temperature;
        var burned = 0.0;
        if (a >= 180 && a < ExhaustOpens)
        {
            // Closed: compression, burning and expansion.
            burned = BurnedFraction(a);
            var squeeze = bottom / volume;
            pressure = intake * Math.Pow(squeeze, Polytropic) * (1 + (burn * burned));
            temperature = IntakeTemperature * Math.Pow(squeeze, Polytropic - 1) * (1 + (burn * burned));
        }
        else if (a >= ExhaustOpens)
        {
            // Blowdown, then the exhaust stroke pushing the rest out.
            burned = 1;
            (pressure, temperature) = Blowdown(a, ratio, ambient);
        }
        else
        {
            // Intake: the gas left from the exhaust gives way to the fresh charge over the first 20°.
            var (endPressure, endTemperature) = Blowdown(720, ratio, ambient);
            var mix = Smooth(0, 20, a);
            pressure = endPressure + ((intake - endPressure) * mix);
            temperature = endTemperature + ((IntakeTemperature - endTemperature) * mix);
            burned = 1 - mix;
        }

        return new EngineState(a, StrokeAt(a), volume, pressure, temperature, burned, PistonTravel(a), ValveLift(a, IntakeOpens, IntakeCloses + 720),
            ValveLift(a, ExhaustOpens, ExhaustCloses));
    }

    private static (double Pressure, double Temperature) Blowdown(double angle, double ratio, double ambient)
    {
        var open = At(ExhaustOpens - 1e-9, ratio, ambient);
        var exhaust = ExhaustFactor * ambient;
        var pressure = exhaust + ((open.Pressure - exhaust) * Math.Exp(-(angle - ExhaustOpens) / 14));
        var temperature = open.Temperature * Math.Pow(pressure / open.Pressure, (Polytropic - 1) / Polytropic);
        return (pressure, temperature);
    }

    /// <summary>How far a valve is open, 0…1, open from <paramref name="opens"/> to <paramref name="closes"/> (angles may pass 720).</summary>
    public static double ValveLift(double angle, double opens, double closes)
    {
        var span = closes - opens;
        var into = Wrap(angle - opens);
        if (into >= span) return 0;
        return Math.Sin(Math.PI * into / span);
    }

    /// <summary>Net work of the gas on the piston over one cycle (two turns), J.</summary>
    public static double WorkPerCycle(double ratio = DefaultCompressionRatio, double ambient = SeaLevelPressure)
    {
        const int steps = 2880;
        var work = 0.0;
        var previous = At(0, ratio, ambient);
        for (var i = 1; i <= steps; i++)
        {
            var next = At(720.0 * i / steps, ratio, ambient);
            work += (previous.Pressure + next.Pressure) / 2 * 1e5 * (next.Volume - previous.Volume);
            previous = next;
        }

        return work;
    }

    /// <summary>The heat the burning charge gives per cycle, J (the same model's charge and temperature rise).</summary>
    public static double HeatPerCycle(double ratio = DefaultCompressionRatio, double ambient = SeaLevelPressure)
    {
        var mass = IntakeFactor * ambient * 1e5 * Volume(180, ratio) / (AirGasConstant * IntakeTemperature);
        var cv = AirGasConstant / (Polytropic - 1);
        return mass * cv * CombustionRise;
    }

    /// <summary>The share of the fuel's heat that becomes work on the piston in this model.</summary>
    public static double Efficiency(double ratio = DefaultCompressionRatio) => WorkPerCycle(ratio) / HeatPerCycle(ratio);

    /// <summary>The textbook ideal Otto cycle with air (γ = 1.4): 1 − r^(1−γ).</summary>
    public static double OttoEfficiency(double ratio) => 1 - Math.Pow(ratio, -0.4);

    /// <summary>Indicated power of all the cylinders at <paramref name="rpm"/>, W (one power stroke per two turns).</summary>
    public static double Power(double rpm, double ratio = DefaultCompressionRatio, double ambient = SeaLevelPressure) =>
        WorkPerCycle(ratio, ambient) * Cylinders * rpm / 120;

    /// <summary>The steady pressure that would do the same work over one stroke (indicated mean effective pressure), bar.</summary>
    public static double MeanEffectivePressure(double ratio = DefaultCompressionRatio, double ambient = SeaLevelPressure) =>
        WorkPerCycle(ratio, ambient) / Displacement / 1e5;

    /// <summary>Air pressure at a height, bar (International Standard Atmosphere).</summary>
    public static double AmbientPressure(double elevation) =>
        SeaLevelPressure * Math.Pow(1 - (2.25577e-5 * Math.Max(-500, elevation)), 5.25588);

    /// <summary>Air density at a height, kg/m³ (International Standard Atmosphere).</summary>
    public static double AirDensity(double elevation) =>
        AmbientPressure(elevation) * 1e5 / (AirGasConstant * (288.15 - (0.0065 * Math.Max(-500, elevation))));

    internal static double Smooth(double a, double b, double value)
    {
        var t = Math.Clamp((value - a) / (b - a), 0, 1);
        return t * t * (3 - (2 * t));
    }
}

/// <summary>The refrigerant at one point of the loop.</summary>
public readonly record struct FridgePoint(FridgePart Part, double Temperature, double Pressure, double Vapour);

/// <summary>
/// A household fridge-freezer with isobutane (R600a) as its refrigerant, as most new fridges in China and Europe use.
/// </summary>
/// <remarks>
/// Temperatures in °C, pressures in bar (absolute). The refrigerant boils in the evaporator about 7 °C colder than the
/// freezer and condenses about 15 °C warmer than the room; its saturation pressure is a Clausius–Clapeyron fit through
/// isobutane's boiling point (−11.7 °C at 1 atm) and 5.3 bar at 40 °C (within a few percent from −30 to 60 °C). A real
/// fridge gets about 45% of the ideal (Carnot) coefficient of performance; the cabinet leaks 1.7 W per kelvin between the
/// room and the inside, and the compressor, when it runs, takes 110 W of heat out of the inside.
/// </remarks>
public static class FridgeModel
{
    public const double FreezerTemperature = -18;
    public const double FridgeTemperature = 4;

    /// <summary>The inside, freezer and fridge together, as the cabinet's walls see it, °C.</summary>
    public const double InsideAverage = -2;

    public const double EvaporatorGap = 7;
    public const double CondenserGap = 15;

    /// <summary>Share of the ideal coefficient of performance a real fridge gets.</summary>
    public const double RealShare = 0.45;

    /// <summary>Heat leaking in through the walls per kelvin between the room and the inside, W/K.</summary>
    public const double Leakage = 1.7;

    /// <summary>Heat taken out of the inside while the compressor runs, W.</summary>
    public const double CoolingPower = 110;

    /// <summary>Warm air rushing in while the door stands open, W.</summary>
    public const double OpenDoorLeak = 250;

    public const double DefaultRoom = 25;

    /// <summary>Where each part starts along the loop (0…1), in the order the refrigerant goes round.</summary>
    private static readonly double[] Starts = [0, 0.06, 0.12, 0.48, 0.58, 0.90, 1];

    public static double Evaporating => FreezerTemperature - EvaporatorGap;

    public static double Condensing(double room) => room + CondenserGap;

    /// <summary>Isobutane's boiling pressure at a temperature, bar.</summary>
    public static double SaturationPressure(double celsius) =>
        1.01325 * Math.Exp(2624 * ((1 / 261.45) - (1 / (celsius + 273.15))));

    public static double LowPressure => SaturationPressure(Evaporating);

    public static double HighPressure(double room) => SaturationPressure(Condensing(room));

    /// <summary>The compressor's outlet: the vapour heated by being squeezed, °C.</summary>
    public static double Discharge(double room) => Condensing(room) + 30;

    public static FridgePart PartAt(double s)
    {
        var x = Frac(s);
        for (var i = 0; i < Starts.Length - 1; i++)
        {
            if (x < Starts[i + 1]) return (FridgePart)i;
        }

        return FridgePart.Suction;
    }

    /// <summary>How far through its part a point of the loop is, 0…1.</summary>
    public static double Progress(double s)
    {
        var x = Frac(s);
        var part = (int)PartAt(x);
        return (x - Starts[part]) / (Starts[part + 1] - Starts[part]);
    }

    public static double PartStart(FridgePart part) => Starts[(int)part];

    public static double PartEnd(FridgePart part) => Starts[(int)part + 1];

    /// <summary>The refrigerant at a point of the loop, <paramref name="s"/> = 0…1 from the compressor's inlet.</summary>
    public static FridgePoint At(double s, double room = DefaultRoom)
    {
        var part = PartAt(s);
        var t = Progress(s);
        var low = LowPressure;
        var high = HighPressure(room);
        var evaporating = Evaporating;
        var condensing = Condensing(room);
        var discharge = Discharge(room);
        var suction = Math.Min(room - 5, 15);

        return part switch
        {
            // Squeezed: pressure and temperature climb together.
            FridgePart.Compressor => new(part, Mix(suction, discharge, t), Mix(low, high, t), 1),
            FridgePart.Discharge => new(part, Mix(discharge, discharge - 8, t), high, 1),

            // Cools to its boiling point, condenses at it, then cools a little more as a liquid.
            FridgePart.Condenser => t < 0.2
                ? new(part, Mix(discharge - 8, condensing, t / 0.2), high, 1)
                : t < 0.9
                    ? new(part, condensing, high, 1 - ((t - 0.2) / 0.7))
                    : new(part, Mix(condensing, condensing - 5, (t - 0.9) / 0.1), high, 0),

            // Through the thin tube the pressure falls; some of the liquid flashes to vapour, cooling the rest.
            FridgePart.Capillary => new(part, Mix(condensing - 5, evaporating, EngineModel.Smooth(0, 1, t)),
                Math.Exp(Mix(Math.Log(high), Math.Log(low), EngineModel.Smooth(0, 1, t))), 0.25 * EngineModel.Smooth(0.3, 1, t)),

            // Boils at the low pressure, taking heat from the inside, until only vapour is left.
            FridgePart.Evaporator => t < 0.92
                ? new(part, evaporating, low, Mix(0.25, 1, t / 0.92))
                : new(part, Mix(evaporating, evaporating + 4, (t - 0.92) / 0.08), low, 1),

            // The suction line warms it on the way back.
            _ => new(part, Mix(evaporating + 4, suction, t), low, 1),
        };
    }

    /// <summary>The best a fridge could do between the boiling and condensing temperatures: heat moved per joule of work.</summary>
    public static double CarnotCop(double room) =>
        (Evaporating + 273.15) / (Condensing(room) - Evaporating);

    public static double RealCop(double room) => RealShare * CarnotCop(room);

    /// <summary>Electric power while the compressor runs, W.</summary>
    public static double CompressorPower(double room) => CoolingPower / RealCop(room);

    /// <summary>Heat leaking into the cabinet, W.</summary>
    public static double Leak(double room, bool doorOpen) =>
        Math.Max(0, Leakage * (room - InsideAverage)) + (doorOpen ? OpenDoorLeak : 0);

    /// <summary>The share of the time the compressor has to run to keep up with the leak, 0…1.</summary>
    public static double DutyCycle(double room, bool doorOpen) => Math.Clamp(Leak(room, doorOpen) / CoolingPower, 0, 1);

    /// <summary>Electricity per day, kWh, running as much as the leak needs.</summary>
    public static double DailyEnergy(double room, bool doorOpen = false) =>
        DutyCycle(room, doorOpen) * CompressorPower(room) * 24 / 1000;

    private static double Mix(double a, double b, double t) => a + ((b - a) * t);

    private static double Frac(double value) => value - Math.Floor(value);
}

/// <summary>A point of the ideal cycle: its step (0…3), how far through it, the gas's volume, pressure and temperature.</summary>
public readonly record struct CarnotState(int Step, double Progress, double Volume, double Pressure, double Temperature);

/// <summary>
/// The Carnot cycle of a monatomic ideal gas (γ = 5/3) between <c>hot</c> and <c>cold</c> kelvin. Run as an engine it
/// goes: 0 isothermal expansion at the hot temperature (taking heat in), 1 adiabatic expansion (cooling to the cold
/// temperature), 2 isothermal compression at the cold temperature (giving heat out), 3 adiabatic compression (back to
/// hot). The gas starts at volume 1 and pressure 1 and doubles its volume in step 0. A fridge or heat pump runs the same
/// loop backwards.
/// </summary>
public static class CarnotModel
{
    public const double Gamma = 5.0 / 3;
    public const double IsothermalRatio = 2;

    public static double Efficiency(double hot, double cold) => 1 - (cold / hot);

    public static double FridgeCop(double hot, double cold) => cold / (hot - cold);

    public static double HeatPumpCop(double hot, double cold) => hot / (hot - cold);

    /// <summary>The four corners' volumes: start, end of hot isothermal, end of the adiabatic expansion, end of cold isothermal.</summary>
    public static (double A, double B, double C, double D) Volumes(double hot, double cold)
    {
        var adiabatic = Math.Pow(hot / cold, 1 / (Gamma - 1));
        return (1, IsothermalRatio, IsothermalRatio * adiabatic, adiabatic);
    }

    /// <summary>
    /// The gas at <paramref name="phase"/> (0…4) of the engine's cycle. Pressure is nRT/V with nR chosen so the start is
    /// at pressure 1.
    /// </summary>
    public static CarnotState At(double phase, double hot, double cold)
    {
        var p = ((phase % 4) + 4) % 4;
        var step = Math.Min(3, (int)p);
        var t = p - step;
        var (a, b, c, d) = Volumes(hot, cold);
        double volume, temperature;
        switch (step)
        {
            case 0:
                temperature = hot;
                volume = a * Math.Pow(b / a, t);
                break;
            case 1:
                temperature = hot + ((cold - hot) * t);
                volume = b * Math.Pow(hot / temperature, 1 / (Gamma - 1));
                break;
            case 2:
                temperature = cold;
                volume = c * Math.Pow(d / c, t);
                break;
            default:
                temperature = cold + ((hot - cold) * t);
                volume = d * Math.Pow(cold / temperature, 1 / (Gamma - 1));
                break;
        }

        return new CarnotState(step, t, volume, temperature / hot / volume, temperature);
    }

    /// <summary>Heat taken in at the hot side per cycle, in units of nR·(hot), so the engine's input is ln 2 per kelvin.</summary>
    public static double HotHeat(double hot) => hot * Math.Log(IsothermalRatio);

    public static double ColdHeat(double cold) => cold * Math.Log(IsothermalRatio);

    public static double Work(double hot, double cold) => HotHeat(hot) - ColdHeat(cold);
}
