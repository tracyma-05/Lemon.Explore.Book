using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Greenhouse;

/// <summary>
/// Planet mode: a slice of the world from the ground up into space. Sunlight comes in (some reflected by clouds and the
/// ground, some taken in by the air); the ground glows in the infrared; greenhouse-gas molecules catch those rays and
/// send them on in any direction, half of them back down, so the ground ends up warmer than sunlight alone could make
/// it. On the right the air's temperature against height, with the height the heat finally leaves from.
/// </summary>
public sealed partial class GreenhouseDiagram
{
    /// <summary>The air column drawn, km (up to about the top of the weather).</summary>
    private const double AirKm = 12;

    /// <summary>Sun photons per second of playback at 1×.</summary>
    private const double SunRate = 7;

    /// <summary>How thin the picture's greenhouse gases get with height (share of the column), for where rays are caught.</summary>
    private const double GasScale = 0.4;

    private const double SunSpeed = 230;
    private const double InfraredSpeed = 150;

    /// <summary>The slant of the sunlight from straight down, degrees.</summary>
    private const double SunSlant = 18;

    private PlanetLayout? _planetLayout;
    private readonly List<Photon> _photons = [];
    private readonly List<(double At, Point From, int Molecule)> _pending = [];
    private readonly List<(Point At, double Time, Color Color, double Size)> _flashes = [];
    private readonly Dictionary<int, double> _excited = [];
    private double _sunDue;
    private double _infraredDue;
    private double _shownTemperature = double.NaN;

    private enum PhotonFate
    {
        Vanish,
        Escape,
        Ground,
        Caught,
        ReflectAir,
        AbsorbAir,
        ReflectGround,
        AbsorbGround,
    }

    private sealed class Photon
    {
        public bool Sunlight;
        public Point From;
        public Point To;
        public double Start;
        public double Duration;
        public PhotonFate Fate;
        public int Molecule = -1;
        public double Phase;
    }

    /// <summary>Where things go in planet mode: the scene, the top of the air, the ground, the profile panel.</summary>
    private sealed record PlanetLayout(Rect Scene, double AirTop, double Ground, Rect Profile, Point Sun, MoleculeSpot[] Spots);

    /// <summary>A molecule of the air in the picture: where, which gas, and its place in its gas's pool (shown if below the count).</summary>
    private readonly record struct MoleculeSpot(Point At, Gas Gas, int Rank, double Turn);

    private void ResetPlanetPhotons()
    {
        _photons.Clear();
        _pending.Clear();
        _flashes.Clear();
        _excited.Clear();
    }

    private PlanetLayout Planet()
    {
        if (_planetLayout is not null) return _planetLayout;

        var stage = Stage();
        var profileWidth = Math.Clamp(stage.Width * 0.16, 110, 160);
        var scene = new Rect(stage.Left + 24, stage.Top + 4, Math.Max(200, stage.Width - 48 - profileWidth - 46), Math.Max(150, stage.Height - 8));
        var airTop = scene.Top + (scene.Height * 0.16);
        var ground = scene.Bottom - (scene.Height * 0.16);
        var profile = new Rect(scene.Right + 46, airTop, profileWidth, ground - airTop);

        // Fixed pools of molecules; the ones shown depend on how much of each gas there is. Water stays low.
        var spots = new List<MoleculeSpot>();
        void Pool(Gas gas, int count, double scale, double salt)
        {
            for (var i = 0; i < count; i++)
            {
                var u = Hash(i, salt);
                var r = Hash(i, salt + 0.37);
                var v = -scale * Math.Log(1 - (r * (1 - Math.Exp(-0.95 / scale))));
                var at = new Point(scene.Left + 8 + (u * (scene.Width - 16)), ground - 6 - (Math.Min(0.95, v) * (ground - airTop - 10)));
                spots.Add(new MoleculeSpot(at, gas, i, Hash(i, salt + 0.71) * Math.PI));
            }
        }

        Pool(Gas.N2, 46, 0.5, 1.1);
        Pool(Gas.O2, 14, 0.5, 2.3);
        Pool(Gas.CO2, 60, GasScale, 3.7);
        Pool(Gas.H2O, 16, 0.25, 4.9);
        Pool(Gas.CH4, 4, GasScale, 6.1);

        _planetLayout = new PlanetLayout(scene, airTop, ground, profile, new Point(scene.Left + 34, scene.Top + ((airTop - scene.Top) * 0.5)), [.. spots]);
        return _planetLayout;
    }

    /// <summary>How many of a gas's molecules the picture shows (CO₂ by the slider; none with no greenhouse gases).</summary>
    private int ShownCount(Gas gas) => gas switch
    {
        Gas.N2 => 46,
        Gas.O2 => 14,
        _ when NoGases => 0,
        Gas.CO2 => (int)Math.Clamp(Math.Round(16 * Co2 / PlanetModel.TodayCo2), 1, 60),
        Gas.H2O => 14,
        Gas.CH4 => 3,
        _ => 0,
    };

    private static bool Catches(Gas gas) => gas is Gas.CO2 or Gas.H2O or Gas.CH4;

    /// <summary>The picture's optical depth of the whole air column for the ground's infrared (chosen so a tenth gets straight out today).</summary>
    private double PictureDepth => NoGases ? 0 : 1.6 * (1 + (0.18 * Math.Log(Co2 / PlanetModel.TodayCo2)));

    /// <summary>The share of the gases' depth below a height (0 ground … 1 top of the air).</summary>
    private static double DepthBelow(double v) => (1 - Math.Exp(-v / GasScale)) / (1 - Math.Exp(-1 / GasScale));

    private static double HeightOfDepth(double share) => -GasScale * Math.Log(1 - (Math.Clamp(share, 0, 1) * (1 - Math.Exp(-1 / GasScale))));

    private double TargetTemperature => NoGases ? PlanetModel.BareCelsius : PlanetModel.SurfaceCelsius(Co2);

    private void StepPlanet(double step)
    {
        var layout = Planet();
        if (double.IsNaN(_shownTemperature)) _shownTemperature = TargetTemperature;
        if (step <= 0) return;

        // The ground warms or cools towards its new balance over a few seconds.
        _shownTemperature += (TargetTemperature - _shownTemperature) * (1 - Math.Exp(-step / 2.5));

        _sunDue += step * SunRate;
        while (_sunDue >= 1)
        {
            _sunDue -= 1;
            SpawnSunlight(layout);
        }

        // The ground glows in proportion to σT⁴: about 1.15 rays for each sunlight one today (396 against 341).
        _infraredDue += step * SunRate * PlanetModel.SurfaceEmission(_shownTemperature) / EnergyBudget.Incoming;
        while (_infraredDue >= 1)
        {
            _infraredDue -= 1;
            var x = layout.Scene.Left + 6 + (_random.NextDouble() * (layout.Scene.Width - 12));
            var mu = Math.Max(0.3, Math.Sqrt(_random.NextDouble()));
            SpawnInfrared(layout, new Point(x, layout.Ground - 1), mu, -1);
        }

        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].At > _t) continue;
            var (_, from, molecule) = _pending[i];
            _pending.RemoveAt(i);

            // A caught ray is sent on in any direction: up and down are equally likely.
            var mu = Math.Max(0.3, _random.NextDouble()) * (_random.NextDouble() < 0.5 ? 1 : -1);
            SpawnInfrared(layout, from, mu, molecule);
        }

        for (var i = _photons.Count - 1; i >= 0; i--)
        {
            var photon = _photons[i];
            if (_t < photon.Start + photon.Duration) continue;
            _photons.RemoveAt(i);
            Arrive(layout, photon);
        }

        _flashes.RemoveAll(f => _t - f.Time > 0.7);
        if (_photons.Count > 500) _photons.RemoveRange(0, _photons.Count - 500);
    }

    private void SpawnSunlight(PlanetLayout layout)
    {
        var slant = Math.Tan(SunSlant * Math.PI / 180);
        var groundX = layout.Scene.Left + 10 + (_random.NextDouble() * (layout.Scene.Width - 20));
        var from = new Point(groundX - (slant * (layout.Ground - layout.Scene.Top)), layout.Scene.Top);
        var height = layout.Ground - layout.AirTop;

        // Of 341 W/m²: 79 reflected by clouds and air, 78 taken in by the air, 23 reflected by the ground, 161 into the ground.
        var r = _random.NextDouble() * EnergyBudget.Incoming;
        PhotonFate fate;
        double v;
        if (r < EnergyBudget.ReflectedByAir)
        {
            fate = PhotonFate.ReflectAir;
            v = 0.42 + (0.16 * _random.NextDouble());
        }
        else if (r < EnergyBudget.ReflectedByAir + EnergyBudget.AbsorbedByAir)
        {
            // With no greenhouse gases (water vapour and ozone take in sunlight too) it reaches the ground.
            fate = NoGases ? PhotonFate.AbsorbGround : PhotonFate.AbsorbAir;
            v = NoGases ? 0 : 0.15 + (0.8 * _random.NextDouble());
        }
        else if (r < EnergyBudget.Reflected + EnergyBudget.AbsorbedByAir)
        {
            fate = PhotonFate.ReflectGround;
            v = 0;
        }
        else
        {
            fate = PhotonFate.AbsorbGround;
            v = 0;
        }

        var y = layout.Ground - (v * height);
        var to = new Point(from.X + (slant * (y - from.Y)), y);
        AddPhoton(new Photon { Sunlight = true, From = from, To = to, Fate = fate }, SunSpeed);
    }

    /// <summary>
    /// An infrared ray from <paramref name="from"/> going up (<paramref name="mu"/> &gt; 0) or down at a slant whose cosine
    /// is |mu|: where it gets caught is drawn from the gases' depth along its path; caught rays end on the nearest
    /// greenhouse-gas molecule.
    /// </summary>
    private void SpawnInfrared(PlanetLayout layout, Point from, double mu, int fromMolecule)
    {
        var height = layout.Ground - layout.AirTop;
        var v0 = Math.Clamp((layout.Ground - from.Y) / height, 0, 1);
        var slope = Math.Sqrt(1 - (mu * mu)) / Math.Abs(mu) * (_random.NextDouble() < 0.5 ? 1 : -1);
        var depth = PictureDepth;

        PhotonFate fate;
        double v;
        if (depth <= 0)
        {
            fate = mu > 0 ? PhotonFate.Escape : PhotonFate.Ground;
            v = mu > 0 ? 1 + ((layout.AirTop - layout.Scene.Top - 6) / height) : 0;
        }
        else
        {
            var share = -Math.Log(Math.Max(1e-9, _random.NextDouble())) * Math.Abs(mu) / depth;
            var target = DepthBelow(v0) + (mu > 0 ? share : -share);
            if (target >= 1)
            {
                fate = PhotonFate.Escape;
                v = 1 + ((layout.AirTop - layout.Scene.Top - 6) / height);
            }
            else if (target <= 0)
            {
                fate = PhotonFate.Ground;
                v = 0;
            }
            else
            {
                fate = PhotonFate.Caught;
                v = HeightOfDepth(target);
            }
        }

        var y = layout.Ground - (v * height);
        var x = Fold(from.X + (slope * Math.Abs(y - from.Y)), layout.Scene.Left + 4, layout.Scene.Right - 4);
        var to = new Point(x, y);
        var molecule = -1;
        if (fate == PhotonFate.Caught)
        {
            molecule = NearestCatcher(layout, to, fromMolecule);
            if (molecule >= 0) to = layout.Spots[molecule].At;
        }

        AddPhoton(new Photon { From = from, To = to, Fate = fate, Molecule = molecule }, InfraredSpeed);
    }

    /// <summary>Folds an x that ran off the side back into the scene, as if the ray had bounced off a mirror there.</summary>
    private static double Fold(double x, double left, double right)
    {
        var width = right - left;
        var t = (x - left) % (2 * width);
        if (t < 0) t += 2 * width;
        return left + (t <= width ? t : (2 * width) - t);
    }

    /// <summary>The shown greenhouse-gas molecule closest to a point (within reach), or −1.</summary>
    private int NearestCatcher(PlanetLayout layout, Point at, int except)
    {
        var best = -1;
        var bestDistance = 48.0;
        for (var i = 0; i < layout.Spots.Length; i++)
        {
            var spot = layout.Spots[i];
            if (i == except || !Catches(spot.Gas) || spot.Rank >= ShownCount(spot.Gas)) continue;
            var d = ((Vector)(spot.At - at)).Length;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = i;
            }
        }

        return best;
    }

    private void AddPhoton(Photon photon, double speed)
    {
        photon.Start = _t;
        photon.Duration = Math.Max(0.05, ((Vector)(photon.To - photon.From)).Length / speed);
        photon.Phase = _random.NextDouble() * Math.PI * 2;
        _photons.Add(photon);
    }

    private void Arrive(PlanetLayout layout, Photon photon)
    {
        switch (photon.Fate)
        {
            case PhotonFate.ReflectAir:
            case PhotonFate.ReflectGround:
                var direction = photon.To - photon.From;
                var up = new Point(photon.To.X + (direction.X / direction.Y * (photon.To.Y - layout.Scene.Top)), layout.Scene.Top);
                _flashes.Add((photon.To, _t, Ink.White, 5));
                AddPhoton(new Photon { Sunlight = true, From = photon.To, To = up, Fate = PhotonFate.Vanish }, SunSpeed);
                break;

            case PhotonFate.AbsorbAir:
                _flashes.Add((photon.To, _t, Ink.Gold, 4));
                break;

            case PhotonFate.AbsorbGround:
                _flashes.Add((photon.To, _t, Ink.Gold, 5));
                break;

            case PhotonFate.Ground:
                _flashes.Add((photon.To, _t, Ink.Infrared, 5));
                break;

            case PhotonFate.Caught:
                if (photon.Molecule >= 0) _excited[photon.Molecule] = _t;
                else _flashes.Add((photon.To, _t, Ink.Infrared, 4));
                _pending.Add((_t + 0.15 + (0.35 * _random.NextDouble()), photon.To, photon.Molecule));
                break;
        }
    }

    private void DrawPlanetMode(PaintContext dc, double width, double height)
    {
        var layout = Planet();
        if (double.IsNaN(_shownTemperature)) _shownTemperature = TargetTemperature;
        var scene = layout.Scene;
        var airHeight = layout.Ground - layout.AirTop;
        var dim = ShowBudget ? 0.3 : 1.0;

        dc.DrawRectangle(Ink.Backdrop, null, new Rect(0, 0, width, height));
        _stars.Draw(dc, width, height, RenderScale(), _clock.Elapsed.TotalSeconds, 0.8);

        // The air: deep blue high up, pale near the ground (over an opaque base, so no stars show through it).
        var air = new Rect(scene.Left, layout.AirTop, scene.Width, airHeight);
        dc.DrawRectangle(PlanetInk.AirBase, null, air);
        dc.DrawRectangle(PlanetInk.Air, null, air);
        dc.DrawLine(PlanetInk.AirEdge, new Point(scene.Left, layout.AirTop), new Point(scene.Right, layout.AirTop));

        // The Sun beyond the top of the picture.
        dc.DrawEllipse(PlanetInk.SunGlow, null, layout.Sun, 34, 34);
        dc.DrawEllipse(PlanetInk.SunDisc, null, layout.Sun, 15, 15);
        _targets.Add("sun", layout.Sun, 20);

        DrawClouds(dc, layout);

        // Where the heat that reaches space leaves from: the air there is at −18 °C.
        var emission = PlanetModel.EmissionHeight(Co2);
        var emissionY = layout.Ground - (emission / AirKm * airHeight);
        if (!NoGases)
        {
            dc.DrawLine(PlanetInk.EmissionLine, new Point(scene.Left, emissionY), new Point(layout.Profile.Right, emissionY));
        }

        DrawGround(dc, layout);
        DrawAirMolecules(dc, layout);
        DrawPhotons(dc, layout, dim);

        foreach (var (at, time, color, size) in _flashes)
        {
            var age = (_t - time) / 0.7;
            dc.DrawEllipse(FadedBrush(color, 0.55 * (1 - age) * dim), null, at, size * (0.6 + age), size * (0.6 + age));
        }

        DrawProfile(dc, layout, emissionY);

        if (ShowLabels)
        {
            AddBoxTarget("space", DrawText(dc, Loc("Green_Planet_Space"), new Point(scene.Left + (scene.Width * 0.42), (scene.Top + layout.AirTop) / 2), Ink.Dim, 11.5));
            AddBoxTarget("air", DrawText(dc, Loc(NoGases ? "Green_Planet_Air_NoGases" : "Green_Planet_Air"), new Point(scene.Left + 10, layout.AirTop + 14), Ink.Text, 11.5));
            if (!NoGases && !ShowBudget)
            {
                var label = LocFormat("Green_Planet_Emission", Number(emission, "0.0"), Number(PlanetModel.BareCelsius, "0.0"));
                AddBoxTarget("emission", DrawPill(dc, label, new Point(scene.Left + (scene.Width * 0.62), emissionY - 12), Ink.Gold, 11));
            }

            DrawLegend(dc, layout);
        }

        // The flows over everything else, so their labels take the click.
        if (ShowBudget) DrawBudget(dc, layout, emissionY);
    }

    private void DrawClouds(PaintContext dc, PlanetLayout layout)
    {
        var airHeight = layout.Ground - layout.AirTop;
        (double U, double V, double Size)[] clouds = [(0.2, 0.5, 1.0), (0.55, 0.47, 0.8), (0.84, 0.53, 1.1)];
        foreach (var (u, v, size) in clouds)
        {
            var center = new Point(layout.Scene.Left + (u * layout.Scene.Width), layout.Ground - (v * airHeight));
            var w = Math.Clamp(layout.Scene.Width * 0.07, 36, 80) * size;
            var h = w * 0.32;
            for (var i = 0; i < 5; i++)
            {
                var dx = (i - 2) * w * 0.36;
                var r = w * (0.42 - (Math.Abs(i - 2) * 0.08));
                dc.DrawEllipse(PlanetInk.CloudRim, null, center + new Vector(dx, -h * 0.2 * (2 - Math.Abs(i - 2))), r * 1.12, r * 0.78);
            }

            for (var i = 0; i < 5; i++)
            {
                var dx = (i - 2) * w * 0.36;
                var r = w * (0.42 - (Math.Abs(i - 2) * 0.08));
                dc.DrawEllipse(PlanetInk.Cloud, null, center + new Vector(dx, -h * 0.2 * (2 - Math.Abs(i - 2))), r, r * 0.7);
            }

            AddBoxTarget("clouds", new Rect(center.X - (w * 0.9), center.Y - (h * 1.1), w * 1.8, h * 1.5));
        }
    }

    private void DrawGround(PaintContext dc, PlanetLayout layout)
    {
        var scene = layout.Scene;
        var bottom = scene.Bottom;
        var sea = scene.Left + (scene.Width * 0.66);
        dc.DrawRectangle(PlanetInk.Land, null, new Rect(scene.Left, layout.Ground, sea - scene.Left, bottom - layout.Ground));
        dc.DrawRectangle(PlanetInk.Sea, null, new Rect(sea, layout.Ground, scene.Right - sea, bottom - layout.Ground));

        // A few hills and trees on the land.
        for (var i = 0; i < 7; i++)
        {
            var x = scene.Left + 20 + (i * (sea - scene.Left - 40) / 6.0);
            var h = 6 + (8 * Hash(i, 9.1));
            dc.DrawRectangle(PlanetInk.Trunk, null, new Rect(x - 1, layout.Ground - h * 0.4, 2, h * 0.4));
            dc.DrawEllipse(PlanetInk.Tree, null, new Point(x, layout.Ground - (h * 0.6)), h * 0.35, h * 0.45);
        }

        // The ground takes on its temperature's colour the further it is from today's: icy blue, or warm orange.
        var glow = HeatColor(_shownTemperature);
        var tint = Math.Clamp(Math.Abs(_shownTemperature - 14) / 25, 0, 0.4);
        dc.DrawRectangle(FadedBrush(Quantize(glow), tint), null, new Rect(scene.Left, layout.Ground, scene.Width, bottom - layout.Ground));
        if (_shownTemperature < -5)
        {
            // Frozen: ice over the sea, snow on the land.
            var ice = Math.Clamp((-5 - _shownTemperature) / 10, 0, 1);
            dc.DrawRectangle(FadedBrush(Ink.White, 0.45 * ice), null, new Rect(scene.Left, layout.Ground, scene.Width, Math.Min(10, bottom - layout.Ground)));
        }

        dc.DrawLine(FadedPen(Quantize(glow), 0.9, 2), new Point(scene.Left, layout.Ground), new Point(scene.Right, layout.Ground));

        // The ground's thermometer and its temperature.
        var bulb = new Point(scene.Left + 18, bottom - 10);
        DrawThermometer(dc, bulb, Math.Min(46, bottom - layout.Ground - 4), _shownTemperature);
        var settling = Math.Abs(TargetTemperature - _shownTemperature) > 0.15;
        var text = LocFormat(settling ? "Green_Planet_Ground_Settling" : "Green_Planet_Ground", Number(_shownTemperature, "0.0"));
        var reading = DrawText(dc, text, new Point(scene.Left + 32, (layout.Ground + bottom) / 2), Ink.Text, 13, bold: true);
        AddBoxTarget("ground", new Rect(scene.Left, layout.Ground, scene.Width, bottom - layout.Ground));
        AddBoxTarget("ground", reading.Union(new Rect(bulb.X - 6, bulb.Y - Math.Min(46, bottom - layout.Ground - 4), 12, Math.Min(46, bottom - layout.Ground - 4) + 6)));
    }

    private void DrawAirMolecules(PaintContext dc, PlanetLayout layout)
    {
        for (var i = 0; i < layout.Spots.Length; i++)
        {
            var spot = layout.Spots[i];
            if (spot.Rank >= ShownCount(spot.Gas)) continue;
            var excitedAge = _excited.TryGetValue(i, out var at) ? (_t - at) / 0.8 : 9;
            var excited = excitedAge < 1;
            var jiggle = excited ? Math.Sin(_t * 40) * 1.6 * (1 - excitedAge) : 0;
            if (excited) dc.DrawEllipse(FadedBrush(Ink.Infrared, 0.55 * (1 - excitedAge)), null, spot.At, 9, 9);
            DrawTinyMolecule(dc, spot.Gas, spot.At + new Vector(jiggle, 0), spot.Turn + (0.3 * Math.Sin((_t * 0.7) + i)));
            _targets.Add(GasId(spot.Gas), spot.At, 6);
        }
    }

    /// <summary>A small ball-and-stick icon of a molecule of the air.</summary>
    private static void DrawTinyMolecule(PaintContext dc, Gas gas, Point at, double turn)
    {
        var (s, c) = Math.SinCos(turn);
        Vector Dir(double dx, double dy) => new((dx * c) - (dy * s), (dx * s) + (dy * c));
        switch (gas)
        {
            case Gas.N2:
                dc.DrawEllipse(PlanetInk.Nitrogen, null, at + Dir(-1.6, 0), 1.9, 1.9);
                dc.DrawEllipse(PlanetInk.Nitrogen, null, at + Dir(1.6, 0), 1.9, 1.9);
                break;

            case Gas.O2:
                dc.DrawEllipse(PlanetInk.OxygenDim, null, at + Dir(-1.7, 0), 2, 2);
                dc.DrawEllipse(PlanetInk.OxygenDim, null, at + Dir(1.7, 0), 2, 2);
                break;

            case Gas.CO2:
                dc.DrawEllipse(PlanetInk.Oxygen, null, at + Dir(-3.6, 0), 2.4, 2.4);
                dc.DrawEllipse(PlanetInk.Oxygen, null, at + Dir(3.6, 0), 2.4, 2.4);
                dc.DrawEllipse(PlanetInk.Carbon, PlanetInk.AtomEdge, at, 2.4, 2.4);
                break;

            case Gas.H2O:
                dc.DrawEllipse(PlanetInk.Hydrogen, null, at + Dir(-2.6, 2), 1.7, 1.7);
                dc.DrawEllipse(PlanetInk.Hydrogen, null, at + Dir(2.6, 2), 1.7, 1.7);
                dc.DrawEllipse(PlanetInk.Oxygen, null, at, 2.6, 2.6);
                break;

            case Gas.CH4:
                dc.DrawEllipse(PlanetInk.Hydrogen, null, at + Dir(0, -3), 1.5, 1.5);
                dc.DrawEllipse(PlanetInk.Hydrogen, null, at + Dir(-2.8, 1.6), 1.5, 1.5);
                dc.DrawEllipse(PlanetInk.Hydrogen, null, at + Dir(2.8, 1.6), 1.5, 1.5);
                dc.DrawEllipse(PlanetInk.Carbon, PlanetInk.AtomEdge, at, 2.3, 2.3);
                break;
        }
    }

    private readonly LineBatch _batch = new();

    private void DrawPhotons(PaintContext dc, PlanetLayout layout, double dim)
    {
        foreach (var photon in _photons)
        {
            var progress = Math.Clamp((_t - photon.Start) / photon.Duration, 0, 1);
            var head = Mix(photon.From, photon.To, progress);
            Vector v = photon.To - photon.From;
            if (v.Length < 1e-6) continue;
            var direction = v / v.Length;
            var travelled = ((Vector)(head - photon.From)).Length;
            if (photon.Sunlight)
            {
                var length = Math.Min(18, travelled);
                if (length < 2) continue;
                _batch.Add(Ink.Sun, 0.95 * dim, [head - (direction * length), head]);
            }
            else
            {
                var length = Math.Min(28, travelled);
                if (length < 4) continue;
                _batch.Add(Ink.Infrared, 0.9 * dim, Wiggle(head, direction, length, 9, 3.2, photon.Phase - (_t * 14)));
            }
        }

        _batch.Draw(dc, this, 1.8);
    }

    /// <summary>The average energy flows of Trenberth et al. (2009) as arrows as wide as their W/m².</summary>
    private void DrawBudget(PaintContext dc, PlanetLayout layout, double emissionY)
    {
        var scene = layout.Scene;
        var airHeight = layout.Ground - layout.AirTop;
        var k = Math.Clamp(scene.Width / 9000, 0.04, 0.075);
        double X(double u) => scene.Left + (u * scene.Width);
        double Y(double v) => layout.Ground - (v * airHeight);
        var top = scene.Top + 24;

        void Flow(double u, double fromY, double toY, double value, Color color, string key, bool labelAtStart)
        {
            var id = BudgetId(key["Green_Budget_".Length..]);
            var from = new Point(X(u), fromY);
            var to = new Point(X(u), toY);
            DrawFlowArrow(dc, from, to, color, Math.Max(3, value * k));
            var label = LocFormat(key, Number(value, "0"));
            var at = labelAtStart ? from : to;
            var offset = toY < fromY ? (labelAtStart ? 12 : -12) : (labelAtStart ? -12 : 12);
            var half = Math.Max(6, value * k / 2) + 4;
            AddBoxTarget(id, new Rect(Math.Min(from.X, to.X) - half, Math.Min(fromY, toY), half * 2, Math.Abs(toY - fromY)));
            AddBoxTarget(id, DrawPill(dc, label, new Point(at.X, at.Y + offset), color, 10.5));
        }

        Flow(0.07, top, Y(0.62), EnergyBudget.Incoming, Ink.Sun, "Green_Budget_In", true);
        Flow(0.07, Y(0.5), layout.Ground - 2, EnergyBudget.AbsorbedBySurface, Ink.Sun, "Green_Budget_Surface", false);
        Flow(0.19, Y(0.5), top, EnergyBudget.Reflected, Ink.White, "Green_Budget_Reflected", true);
        Flow(0.3, Y(0.64), Y(0.3), EnergyBudget.AbsorbedByAir, Ink.Gold, "Green_Budget_AirSun", false);
        Flow(0.44, layout.Ground - 2, Y(0.42), EnergyBudget.SurfaceRadiation, Ink.Infrared, "Green_Budget_Glow", true);
        Flow(0.57, Y(0.42), layout.Ground - 2, EnergyBudget.BackRadiation, Ink.Orange, "Green_Budget_Back", false);
        Flow(0.69, layout.Ground - 2, top, EnergyBudget.Window, Ink.Red, "Green_Budget_Window", false);
        Flow(0.81, emissionY, top, EnergyBudget.OutgoingFromAir, Ink.Red, "Green_Budget_FromAir", false);
        Flow(0.93, layout.Ground - 2, Y(0.3), EnergyBudget.Thermals + EnergyBudget.Evaporation, Ink.Cyan, "Green_Budget_Rising", false);
    }

    /// <summary>The air's temperature against height, with the height the heat leaves for space.</summary>
    private void DrawProfile(PaintContext dc, PlanetLayout layout, double emissionY)
    {
        var p = layout.Profile;
        const double low = -70;
        const double high = 30;
        double X(double celsius) => p.Left + ((celsius - low) / (high - low) * p.Width);
        double Y(double km) => layout.Ground - (km / AirKm * (layout.Ground - layout.AirTop));

        var panel = new Rect(p.Left - 30, p.Top - 28, p.Width + 38, p.Height + 58);
        dc.DrawRectangle(PlanetInk.Panel, Ink.Frame, panel);
        AddBoxTarget("profile", panel);
        for (var km = 0; km <= AirKm; km += 4)
        {
            dc.DrawLine(Ink.Grid, new Point(p.Left, Y(km)), new Point(p.Right, Y(km)));
            DrawTextRight(dc, Number(km, "0"), new Point(p.Left - 1, Y(km)), Ink.Dim, 9.5);
        }

        for (var c = -60; c <= 30; c += 30)
        {
            dc.DrawLine(Ink.Grid, new Point(X(c), p.Top), new Point(X(c), p.Bottom));
            DrawText(dc, Number(c, "0"), new Point(X(c), p.Bottom + 9), Ink.Dim, 9.5, center: true);
        }

        DrawText(dc, Loc("Green_Profile_Km"), new Point(p.Left - 22, p.Top - 16), Ink.Dim, 9.5);
        DrawText(dc, Loc("Green_Profile_Celsius"), new Point(p.Left + (p.Width / 2), p.Bottom + 21), Ink.Dim, 9.5, center: true);

        // 0 °C.
        dc.DrawLine(PlanetInk.Freezing, new Point(X(0), p.Top), new Point(X(0), p.Bottom));

        if (NoGases)
        {
            dc.DrawEllipse(FadedBrush(Ink.Cyan, 1), null, new Point(X(_shownTemperature), Y(0)), 4, 4);
            return;
        }

        var surface = _shownTemperature;
        var line = Line(Enumerable.Range(0, 25).Select(i => i * AirKm / 24).Select(km => new Point(X(surface - (PlanetModel.LapseRate * Math.Min(km, 11))), Y(km))));
        dc.DrawGeometry(null, PlanetInk.ProfileLine, line);
        dc.DrawEllipse(FadedBrush(Quantize(HeatColor(surface)), 1), null, new Point(X(surface), Y(0)), 4, 4);

        // The heat leaves from where the air is at the bare planet's temperature.
        var bare = PlanetModel.BareCelsius;
        var at = new Point(X(bare), emissionY);
        dc.DrawEllipse(FadedBrush(Ink.Gold, 0.35), null, at, 8, 8);
        dc.DrawEllipse(FadedBrush(Ink.Gold, 1), null, at, 4, 4);
        DrawArrow(dc, at + new Vector(0, -6), at + new Vector(0, -26), FadedBrush(Ink.Gold, 0.9), 1.6, 7);
    }

    private void DrawLegend(PaintContext dc, PlanetLayout layout)
    {
        var scene = layout.Scene;
        var x = scene.Right - 10;
        var y = scene.Bottom - 16;
        string[] keys = ["Green_Legend_Sunlight", "Green_Legend_Infrared"];
        var texts = keys.Select(Loc).ToArray();
        var widths = texts.Select(t => _labels.Text(t, 11, Ink.Text).Width).ToArray();
        var total = widths.Sum() + 2 * 34 + 18;
        var left = x - total;
        dc.DrawRoundedRectangle(PlanetInk.LegendBack, null, new Rect(left - 8, y - 11, total + 10, 22), 11, 11);
        dc.DrawLine(FadedPen(Ink.Sun, 1, 2, flat: true), new Point(left, y), new Point(left + 22, y));
        AddBoxTarget("sunlight", new Rect(left, y - 6, 22, 12).Union(DrawText(dc, texts[0], new Point(left + 28, y), Ink.Text, 11)));
        var left2 = left + 28 + widths[0] + 18;
        dc.DrawGeometry(null, FadedPen(Ink.Infrared, 1, 1.8, flat: true), Line(Wiggle(new Point(left2 + 24, y), new Vector(1, 0), 24, 8, 3, 0)));
        AddBoxTarget("infrared", new Rect(left2, y - 6, 24, 12).Union(DrawText(dc, texts[1], new Point(left2 + 30, y), Ink.Text, 11)));
    }

    private static class PlanetInk
    {
        public static readonly IBrush Air = SpaceDrawing.LinearGradient(90,
            (Color.FromArgb(40, 0x30, 0x60, 0xB0), 0),
            (Color.FromArgb(110, 0x3A, 0x7C, 0xD0), 0.6),
            (Color.FromArgb(150, 0x6A, 0xB0, 0xE8), 1));

        public static readonly IBrush AirBase = Solid(255, 0x08, 0x10, 0x20);
        public static readonly IPen AirEdge = MakePen(Solid(90, 0x8A, 0xC8, 0xFF), 1.2);
        public static readonly IBrush SunDisc = Solid(255, 0xFF, 0xE8, 0x90);

        public static readonly IBrush SunGlow = SpaceDrawing.RadialGradient(
            (Color.FromArgb(150, 0xFF, 0xD8, 0x6A), 0),
            (Color.FromArgb(0, 0xFF, 0xD8, 0x6A), 1));

        public static readonly IBrush Cloud = Solid(225, 0xF2, 0xF5, 0xFA);
        public static readonly IBrush CloudRim = Solid(70, 0xF2, 0xF5, 0xFA);
        public static readonly IBrush Land = SpaceDrawing.LinearGradient(Color.FromRgb(0x4E, 0x6A, 0x3A), Color.FromRgb(0x3A, 0x2E, 0x22), 90);
        public static readonly IBrush Sea = SpaceDrawing.LinearGradient(Color.FromRgb(0x2A, 0x5A, 0x9A), Color.FromRgb(0x12, 0x2A, 0x52), 90);
        public static readonly IBrush Tree = Solid(255, 0x3E, 0x8A, 0x46);
        public static readonly IBrush Trunk = Solid(255, 0x5A, 0x40, 0x2A);
        public static readonly IPen EmissionLine = MakePen(Solid(170, 0xFF, 0xD3, 0x6B), 1.2, dashed: true);
        public static readonly IBrush Panel = Solid(150, 0x06, 0x09, 0x10);
        public static readonly IPen Freezing = MakePen(Solid(70, 0x6F, 0xD8, 0xFF), 1, dashed: true);
        public static readonly IPen ProfileLine = MakePen(Solid(255, 0xFF, 0x9F, 0x5A), 2);
        public static readonly IBrush LegendBack = Solid(180, 8, 12, 22);

        public static readonly IBrush Nitrogen = Solid(110, 0x7A, 0x9A, 0xFF);
        public static readonly IBrush OxygenDim = Solid(110, 0xFF, 0x7A, 0x6A);
        public static readonly IBrush Oxygen = Solid(255, 0xFF, 0x5A, 0x4A);
        public static readonly IBrush Carbon = Solid(255, 0x3A, 0x3E, 0x46);
        public static readonly IPen AtomEdge = MakePen(Solid(220, 0xC9, 0xD1, 0xDC), 0.8);
        public static readonly IBrush Hydrogen = Solid(255, 0xEE, 0xF1, 0xF6);
    }
}
