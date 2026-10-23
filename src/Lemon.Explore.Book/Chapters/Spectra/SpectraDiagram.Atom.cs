using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Spectra;

/// <summary>
/// The atom: hydrogen's nucleus with its electron on one of six orbits (or its electron cloud), a lamp firing photons at
/// it, photons flying in and out as wave packets of their own colour and length, the energy ladder with the recent jumps,
/// and the lines given out so far from ultraviolet to infrared.
/// </summary>
public sealed partial class SpectraDiagram
{
    /// <summary>How long the electron stays up before it falls, s.</summary>
    private const double Dwell = 1.3;

    /// <summary>How long a freed electron flies before it is caught again, s.</summary>
    private const double FreeTime = 2.0;

    /// <summary>A photon's flight across the picture, s.</summary>
    private const double Flight = 0.8;

    /// <summary>The hydrogen strip's range, nm (logarithmic).</summary>
    private const double StripFrom = 90;

    private const double StripTo = 2000;

    private enum PhotonKind
    {
        /// <summary>Fired by the lamp: taken in, or not, when it arrives.</summary>
        Fired,

        /// <summary>Sent in to lift the electron to <see cref="Photon.Result"/> (a click on a level).</summary>
        Lifting,

        /// <summary>Given out by a falling electron, flying away.</summary>
        Given,

        /// <summary>Not taken in: flying on past the atom.</summary>
        Passing,
    }

    private sealed class Photon
    {
        public required double Nm { get; init; }

        public required PhotonKind Kind { get; init; }

        public required Point From { get; init; }

        public required Point To { get; init; }

        public required double Start { get; init; }

        public double Duration { get; init; } = Flight;

        public int Result { get; init; }
    }

    private readonly List<Photon> _photons = [];
    private readonly Dictionary<(int Lower, int Upper), int> _counts = [];
    private readonly List<(int Lower, int Upper, bool Given, double Time)> _jumps = [];
    private readonly Random _random = new(11);
    private AtomView _atom;
    private double _atomTime;
    private double _angle;
    private double _dwell;
    private int _level = 1;
    private int _fromLevel = 1;
    private double _jumpStart = -10;
    private bool _pushing;
    private WriteableBitmap? _cloud;
    private (int N, int L, int Size) _cloudKey;

    private readonly record struct AtomView(Point Centre, double Radius, Rect Ladder, Rect Strip, Point Lamp, Rect Stage)
    {
        public double Orbit(int n) => Radius * Math.Max(n, 1) / SpectraModel.TopLevel;

        /// <summary>The ladder's height for an energy, eV: a square-root scale so the upper levels do not crowd.</summary>
        public double EnergyY(double energy) => Ladder.Top + 34 + ((Ladder.Height - 54) * Math.Sqrt(Math.Max(0, -energy) / SpectraModel.Ionisation));

        public double StripX(double nm) => Strip.X + (Strip.Width * (Math.Log(Math.Clamp(nm, StripFrom, StripTo)) - Math.Log(StripFrom)) / (Math.Log(StripTo) - Math.Log(StripFrom)));
    }

    private AtomView AtomLayout()
    {
        var stage = Stage();
        var strip = new Rect(stage.Left + 50, stage.Bottom - 50, stage.Width - 100, 30);
        var top = stage.Top + 6;
        var bottom = strip.Top - 34;
        var radius = Math.Max(60, Math.Min(stage.Width * 0.22, ((bottom - top) / 2) - 10));
        var centre = new Point(stage.Left + (stage.Width * 0.3), (top + bottom) / 2);
        var ladder = new Rect(stage.Left + (stage.Width * 0.58), top, (stage.Width * 0.42) - 30, bottom - top);
        // On the book's narrower page the atom is as wide as it may be, so the lamp stands a little in from the plate's edge.
        return new AtomView(centre, radius, ladder, strip, new Point(Math.Max(stage.Left + 36, centre.X - radius - 70), centre.Y), stage);
    }

    #region simulation

    private void StepAtom(double dt)
    {
        if (!IsPlaying) return;
        _atomTime += dt;
        _angle += dt * 2.2 / Math.Pow(Math.Max(1, _level), 1.5);

        for (var i = _photons.Count - 1; i >= 0; i--)
        {
            var photon = _photons[i];
            if (_atomTime < photon.Start + photon.Duration) continue;
            _photons.RemoveAt(i);
            switch (photon.Kind)
            {
                case PhotonKind.Lifting:
                    Jump(photon.Result, $"absorb {_level} {photon.Result}", given: false);
                    break;

                case PhotonKind.Fired:
                    Arrive(photon);
                    break;
            }
        }

        var lifting = _photons.Any(p => p.Kind is PhotonKind.Lifting or PhotonKind.Fired);
        if (lifting || _atomTime - _jumpStart < 0.4) return;
        _dwell += dt;
        if (_level >= 2 && _dwell > Dwell && !Hold)
        {
            Fall();
        }
        else if (_level == 0 && _dwell > FreeTime)
        {
            // Caught again by the nucleus, onto a high level, and on down from there.
            var catchLevel = FallMode switch
            {
                FallMode.OneStep => 3,
                FallMode.ToGround => 2,
                _ => 3 + _random.Next(4),
            };
            Jump(catchLevel, $"capture {catchLevel}", given: false);
        }
    }

    /// <summary>A fired photon reaches the atom: it lifts the electron if its colour is just right, frees it if it has energy to spare, or flies on.</summary>
    private void Arrive(Photon photon)
    {
        var result = _level >= 1 ? SpectraModel.Absorb(_level, photon.Nm) : -1;
        if (result >= 2 && result <= 12)
        {
            Jump(Math.Min(result, 12), $"absorb {_level} {result}", given: false);
        }
        else if (result == 0)
        {
            Jump(0, $"free {_level}", given: false);
        }
        else
        {
            var v = _atom;
            _photons.Add(new Photon { Nm = photon.Nm, Kind = PhotonKind.Passing, From = photon.To, To = new Point(v.Stage.Right - 20, photon.To.Y - 30), Start = _atomTime, Duration = 1.0 });
            Push(LastEventProperty, $"pass {SpectraDiagram.Number(photon.Nm, "0.0")}");
        }
    }

    /// <summary>The electron falls (by the fall mode) and gives out a photon of the gap's colour.</summary>
    private void Fall()
    {
        var upper = _level;
        var lower = SpectraModel.FallTo(upper, FallMode, _random.NextDouble());
        Give(upper, lower);
        Jump(lower, $"emit {upper} {lower}", given: true);
    }

    private void Give(int upper, int lower)
    {
        var nm = SpectraModel.Wavelength(lower, upper);
        var at = ElectronPoint();
        Vector direction = at - _atom.Centre;
        if (direction.Length < 1) direction = new Vector(1, -0.4);
        direction = direction.Normalize();
        _photons.Add(new Photon { Nm = nm, Kind = PhotonKind.Given, From = at, To = at + (direction * (_atom.Radius * 1.4)), Start = _atomTime, Duration = 1.2 });
        _counts[(lower, upper)] = _counts.GetValueOrDefault((lower, upper)) + 1;
    }

    /// <summary>The electron moves to <paramref name="level"/>; the page is told.</summary>
    private void Jump(int level, string what, bool given)
    {
        if (level >= 1 && _level >= 1 && level != _level)
        {
            _jumps.Add((Math.Min(level, _level), Math.Max(level, _level), given, _atomTime));
            if (_jumps.Count > 8) _jumps.RemoveAt(0);
        }

        _fromLevel = _level;
        _level = level;
        _jumpStart = _atomTime;
        _dwell = 0;
        Push(LevelProperty, level);
        Push(LastEventProperty, what);
    }

    private void Push(AvaloniaProperty property, object value)
    {
        _pushing = true;
        try
        {
            SetCurrentValue(property, value);
        }
        finally
        {
            _pushing = false;
        }
    }

    /// <summary>The level was set from outside: lift the electron there with a photon of just the right colour, or drop it.</summary>
    private void OnLevelChanged(int oldValue, int newValue)
    {
        if (_pushing || newValue == _level) return;
        var v = _atom;
        _photons.RemoveAll(p => p.Kind == PhotonKind.Lifting);
        if (_level >= 1 && (newValue > _level || newValue == 0))
        {
            var nm = newValue == 0 ? 0.85 * SpectraModel.PhotonConstant / -SpectraModel.Energy(_level) : SpectraModel.Wavelength(_level, newValue);
            _photons.Add(new Photon
            {
                Nm = nm, Kind = PhotonKind.Lifting, Result = newValue, Start = _atomTime,
                From = v.Lamp, To = new Point(v.Centre.X - v.Orbit(_level), v.Centre.Y),
            });
        }
        else if (newValue >= 1 && _level >= 1)
        {
            Give(_level, newValue);
            Jump(newValue, $"emit {_level} {newValue}", given: true);
        }
        else if (newValue >= 1)
        {
            Jump(newValue, $"capture {newValue}", given: false);
        }

        _ = oldValue;
    }

    private void FirePhoton()
    {
        var v = _atom;
        if (v.Radius <= 0) v = _atom = AtomLayout();
        _photons.Add(new Photon
        {
            Nm = PhotonNm, Kind = PhotonKind.Fired, Start = _atomTime,
            From = v.Lamp, To = new Point(v.Centre.X - v.Orbit(Math.Max(1, _level)), v.Centre.Y),
        });
    }

    private void ClearLines()
    {
        _counts.Clear();
        _jumps.Clear();
    }

    /// <summary>Where the electron is: on its orbit, moving between two, or flying away when free.</summary>
    private Point ElectronPoint()
    {
        var v = _atom;
        var t = Math.Clamp((_atomTime - _jumpStart) / 0.35, 0, 1);
        t = t * t * (3 - (2 * t));
        double radius;
        if (_level == 0)
        {
            radius = v.Orbit(Math.Max(1, _fromLevel)) + ((_atomTime - _jumpStart) * v.Radius * 0.6);
        }
        else
        {
            var from = _fromLevel == 0 ? v.Radius * 1.3 : v.Orbit(_fromLevel);
            radius = from + ((v.Orbit(_level) - from) * t);
        }

        return v.Centre + new Vector(Math.Cos(_angle) * radius, -Math.Sin(_angle) * radius);
    }

    #endregion

    #region drawing

    private void DrawAtomMode(PaintContext dc, double now)
    {
        var v = _atom = AtomLayout();
        DrawAtom(dc, v, now);
        DrawLadder(dc, v, now);
        DrawHydrogenStrip(dc, v);
        DrawPhotons(dc, v);
    }

    private void DrawAtom(PaintContext dc, AtomView v, double now)
    {
        var glow = SpaceDrawing.RadialGradient((Color.FromArgb(40, 0x6F, 0xD8, 0xFF), 0), (Color.FromArgb(0, 0x6F, 0xD8, 0xFF), 1));
        dc.DrawEllipse(glow, null, v.Centre, v.Radius * 1.15, v.Radius * 1.15);

        if (ShowCloud && _level >= 1)
        {
            var l = Math.Min(CloudShape, _level - 1);
            var size = (int)Math.Min(420, v.Radius * 2.1);
            dc.DrawImage(CloudImage(_level, l, size), new Rect(v.Centre.X - (size / 2.0), v.Centre.Y - (size / 2.0), size, size));
            _targets.Add("cloud", new Point(v.Centre.X + (size * 0.28), v.Centre.Y - (size * 0.18)), 14);
            if (ShowLabels)
            {
                var shape = l switch
                {
                    1 => Loc("Spec_Shape_P"),
                    2 => Loc("Spec_Shape_D"),
                    _ => Loc("Spec_Shape_S"),
                };
                DrawPill(dc, LocFormat("Spec_Label_Orbital", _level, shape), new Point(v.Centre.X, v.Centre.Y + (size / 2.0) - 6), Ink.Cyan, 11);
            }
        }
        else
        {
            for (var n = 1; n <= SpectraModel.TopLevel; n++)
            {
                var r = v.Orbit(n);
                var current = n == _level;
                if (current && Glows("orbit")) dc.DrawEllipse(null, ExploreTargets.GlowPen(now), v.Centre, r, r);
                dc.DrawEllipse(null, current ? FadedPen(Ink.Cyan, 0.7, 1.4) : FadedPen(Ink.Text, 0.18, 1, dashed: true), v.Centre, r, r);
                if (ShowLabels && (current || v.Radius / SpectraModel.TopLevel >= 22))
                {
                    var at = v.Centre + new Vector(r * 0.72, r * 0.72);
                    DrawText(dc, LocFormat("Spec_Label_N", n), at, current ? Ink.Cyan : Ink.Dim, 10.5, TextAnchor.Center);
                }
            }

            if (_level >= 1) _targets.Add("orbit", v.Centre + new Vector(-v.Orbit(_level) * 0.71, -v.Orbit(_level) * 0.71), 9);
        }

        // The nucleus: one proton.
        dc.DrawEllipse(Solid(255, 0xFF, 0x6A, 0x5A), MakePen(Solid(255, 0xFF, 0xB0, 0xA0), 1), v.Centre, 7, 7);
        _targets.Add("nucleus", v.Centre, 9);
        if (ShowLabels) DrawPill(dc, Loc("Spec_Label_Proton"), new Point(v.Centre.X, v.Centre.Y - 22), Color.FromRgb(0xFF, 0x9A, 0x8A), 10.5);

        // The electron (hidden inside its cloud).
        if (!ShowCloud || _level == 0)
        {
            var e = ElectronPoint();
            if (v.Stage.Contains(e))
            {
                var halo = SpaceDrawing.RadialGradient((Color.FromArgb(160, 0x6F, 0xD8, 0xFF), 0), (Color.FromArgb(0, 0x6F, 0xD8, 0xFF), 1));
                dc.DrawEllipse(halo, null, e, 14, 14);
                dc.DrawEllipse(Solid(255, 0xD8, 0xF4, 0xFF), null, e, 5, 5);
                _targets.Add("electron", e, 9);
            }
        }

        // The lamp that fires photons.
        var lamp = new Rect(v.Lamp.X - 22, v.Lamp.Y - 13, 30, 26);
        dc.DrawRoundedRectangle(Solid(255, 0x2A, 0x30, 0x40), MakePen(Solid(255, 0x5A, 0x64, 0x78), 1), lamp, 5, 5);
        var colour = PhotonColour(PhotonNm);
        dc.DrawEllipse(Solid(255, colour.R, colour.G, colour.B), null, new Point(lamp.Right, v.Lamp.Y), 4, 6);
        AddBoxTarget("lamp", lamp);
        if (ShowLabels) DrawPill(dc, LocFormat("Spec_Label_Lamp", Nanometres(PhotonNm)), new Point(lamp.X + (lamp.Width / 2), lamp.Bottom + 16), colour, 10.5);
    }

    /// <summary>Hydrogen's electron cloud for (n, l, m = 0), seen in a slice through the nucleus, brightest where the electron is most likely.</summary>
    private IImage CloudImage(int n, int l, int size)
    {
        if (_cloud is not null && _cloudKey == (n, l, size)) return _cloud;
        var extent = (2.5 * n * n) + 3;
        var values = new double[size * size];
        var max = 0.0;
        for (var j = 0; j < size; j++)
        {
            var z = ((size / 2.0) - j - 0.5) / (size / 2.0) * extent;
            for (var i = 0; i < size; i++)
            {
                var x = (i + 0.5 - (size / 2.0)) / (size / 2.0) * extent;
                var r = Math.Sqrt((x * x) + (z * z));
                var d = r > extent ? 0 : SpectraModel.Cloud(n, l, r, r < 1e-9 ? 1 : z / r);
                values[(j * size) + i] = d;
                max = Math.Max(max, d);
            }
        }

        var pixels = new byte[size * size * 4];
        for (var k = 0; k < values.Length; k++)
        {
            var a = max <= 0 ? 0 : Math.Pow(values[k] / max, 0.45);
            var alpha = (byte)(a * 235);
            pixels[(k * 4) + 0] = (byte)(alpha * 1.0);
            pixels[(k * 4) + 1] = (byte)(alpha * 0.86);
            pixels[(k * 4) + 2] = (byte)(alpha * (0.45 + (0.5 * a)));
            pixels[(k * 4) + 3] = alpha;
        }

        _cloud = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        SpaceDrawing.WritePixels(_cloud, pixels, size, size);
        _cloudKey = (n, l, size);
        return _cloud;
    }

    /// <summary>The energy ladder: the levels on a square-root scale, the freed region above, the electron and the recent jumps.</summary>
    private void DrawLadder(PaintContext dc, AtomView v, double now)
    {
        var box = v.Ladder;
        var left = box.Left + 70;
        var right = box.Right - 70;
        var zero = v.EnergyY(0);

        var free = SpaceDrawing.LinearGradient(Color.FromArgb(70, 0xB0, 0x8C, 0xFF), Color.FromArgb(0, 0xB0, 0x8C, 0xFF), 90);
        var freeBox = new Rect(left, box.Top, right - left, zero - box.Top);
        dc.DrawRectangle(free, null, freeBox);
        dc.DrawLine(FadedPen(Ink.Violet, 0.8, 1.2, dashed: true), new Point(left, zero), new Point(right, zero));
        AddBoxTarget("continuum", new Rect(left, box.Top, right - left, zero - box.Top - 2));
        if (ShowLabels) DrawText(dc, Loc("Spec_Label_Free"), new Point(left + 8, box.Top + 12), Ink.Violet, 11);

        for (var n = 1; n <= SpectraModel.TopLevel; n++)
        {
            var y = v.EnergyY(SpectraModel.Energy(n));
            var current = n == _level;
            dc.DrawLine(current ? FadedPen(Ink.Cyan, 0.95, 2.2) : FadedPen(Ink.Text, 0.6, 1.4), new Point(left, y), new Point(right, y));

            // Near the top the levels crowd together: label only those with room (and the electron's).
            var room = n == 1 || y - v.EnergyY(SpectraModel.Energy(n + 1)) >= 12;
            if (room || current)
            {
                DrawText(dc, LocFormat("Spec_Label_N", n), new Point(left - 8, y), current ? Ink.Cyan : Ink.Text, 11, TextAnchor.Right);
                if (ShowLabels || n == 1) DrawText(dc, LocFormat("Spec_Value_Ev", Number(SpectraModel.Energy(n), "0.00")), new Point(right + 6, y), Ink.Dim, 10.5);
            }

            AddBoxTarget("level" + (char)('0' + n), new Rect(left, y - 6, right - left, 12));
        }

        // The jumps so far: given out (coloured, down) and taken in (dashed, up), fading.
        var x = left + 40;
        foreach (var (lower, upper, given, time) in _jumps)
        {
            var age = _atomTime - time;
            var alpha = Math.Clamp(1 - (age / 8), 0.15, 1);
            var nm = SpectraModel.Wavelength(lower, upper);
            var colour = PhotonColour(nm);
            var top = new Point(x, v.EnergyY(SpectraModel.Energy(upper)));
            var bottom = new Point(x, v.EnergyY(SpectraModel.Energy(lower)));
            dc.DrawLine(FadedPen(colour, alpha, 2, dashed: !given), top, bottom);
            var tip = given ? bottom : top;
            DrawArrowHead(dc, FadedBrush(colour, alpha), tip, given ? new Vector(0, 1) : new Vector(0, -1), 8);
            x += 26;
            if (x > right - 20) x = left + 40;
        }

        if (_jumps.Count > 0 && ShowLabels)
        {
            var (lower, upper, _, time) = _jumps[^1];
            var nm = SpectraModel.Wavelength(lower, upper);
            var y = (v.EnergyY(SpectraModel.Energy(upper)) + v.EnergyY(SpectraModel.Energy(lower))) / 2;
            var key = SpectraModel.SeriesKey(lower) switch
            {
                "Lyman" => "Spec_Series_Lyman",
                "Balmer" => "Spec_Series_Balmer",
                "Paschen" => "Spec_Series_Paschen",
                "Brackett" => "Spec_Series_Brackett",
                _ => "Spec_Series_Pfund",
            };
            var label = LocFormat("Spec_Label_Jump", Loc(key), Nanometres(nm));
            DrawPill(dc, label, new Point(Math.Min(right - 40, x + 30), y), PhotonColour(nm), 10.5);
            _ = time;
        }

        // The electron on its level.
        if (_level >= 1)
        {
            var t = Math.Clamp((_atomTime - _jumpStart) / 0.35, 0, 1);
            var from = _fromLevel >= 1 ? v.EnergyY(SpectraModel.Energy(_fromLevel)) : box.Top + 20;
            var y = from + ((v.EnergyY(SpectraModel.Energy(_level)) - from) * t);
            dc.DrawEllipse(Solid(255, 0xD8, 0xF4, 0xFF), MakePen(Solid(255, Ink.Cyan.R, Ink.Cyan.G, Ink.Cyan.B), 2), new Point(left + 16, y), 5, 5);
        }
        else
        {
            dc.DrawEllipse(Solid(255, 0xD8, 0xF4, 0xFF), null, new Point(left + 16 + (12 * Math.Sin(now * 3)), box.Top + 24), 5, 5);
        }

        if (ShowLabels) AddBoxTarget("energy", DrawPill(dc, Loc("Spec_Label_Energy"), new Point(left - 30, box.Top + 12), Ink.Dim, 10.5));
    }

    /// <summary>The lines hydrogen has given out so far, ultraviolet to infrared, brighter the more photons.</summary>
    private void DrawHydrogenStrip(PaintContext dc, AtomView v)
    {
        var strip = v.Strip;
        dc.DrawRoundedRectangle(Solid(255, 0x05, 0x06, 0x0A), Ink.Frame, strip.Inflate(new Thickness(2, 2)), 4, 4);

        // Where we can see: a faint rainbow behind the visible part.
        var from = v.StripX(SpectraModel.VisibleFrom);
        var to = v.StripX(SpectraModel.VisibleTo);
        var rainbow = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative) };
        for (var i = 0; i <= 8; i++)
        {
            var nm = SpectraModel.VisibleFrom + ((SpectraModel.VisibleTo - SpectraModel.VisibleFrom) * i / 8);
            var c = PhotonColour(nm);
            rainbow.GradientStops.Add(new GradientStop(Color.FromArgb(55, c.R, c.G, c.B), i / 8.0));
        }
        dc.DrawRectangle(rainbow, null, new Rect(from, strip.Y, to - from, strip.Height));

        var most = _counts.Count == 0 ? 1 : _counts.Values.Max();
        foreach (var ((lower, upper), count) in _counts)
        {
            var nm = SpectraModel.Wavelength(lower, upper);
            var x = v.StripX(nm);
            var strength = 0.35 + (0.65 * Math.Log(1 + count) / Math.Log(1 + most));
            var colour = PhotonColour(nm);
            dc.DrawRectangle(FadedBrush(colour, strength), null, new Rect(x - 1.5, strip.Y, 3, strip.Height));
            if (ShowLabels && count == most) DrawText(dc, Number(nm, "0"), new Point(x, strip.Y - 9), colour, 9.5, TextAnchor.Center);
        }

        foreach (var nm in new[] { 100.0, 200, 500, 1000, 2000 })
        {
            var x = v.StripX(nm);
            dc.DrawLine(Ink.Tick, new Point(x, strip.Bottom), new Point(x, strip.Bottom + 4));
            DrawText(dc, Number(nm, "0"), new Point(x, strip.Bottom + 12), Ink.Dim, 9.5, TextAnchor.Center);
        }

        AddBoxTarget("hspectrum", new Rect(from, strip.Y, to - from, strip.Height));
        var uv = DrawText(dc, Loc("Spec_Label_Ultraviolet"), new Point((strip.X + from) / 2, strip.Y + (strip.Height / 2)), Ink.Violet, 10.5, TextAnchor.Center);
        var ir = DrawText(dc, Loc("Spec_Label_Infrared"), new Point((to + strip.Right) / 2, strip.Y + (strip.Height / 2)), Color.FromRgb(0xE0, 0x80, 0x70), 10.5, TextAnchor.Center);
        AddBoxTarget("uv", uv.Inflate(new Thickness(6, 4)));
        AddBoxTarget("ir", ir.Inflate(new Thickness(6, 4)));
        DrawText(dc, Loc("Spec_Label_Nm"), new Point(strip.Right + 8, strip.Y + (strip.Height / 2)), Ink.Dim, 10);
    }

    /// <summary>Photons as short wave packets in their own colour, the wave as long as the light's (to scale with one another).</summary>
    private void DrawPhotons(PaintContext dc, AtomView v)
    {
        Photon? latestIn = null;
        Photon? latestOut = null;
        foreach (var photon in _photons)
        {
            var t = Math.Clamp((_atomTime - photon.Start) / photon.Duration, 0, 1);
            var at = photon.From + ((photon.To - photon.From) * t);
            Vector direction = photon.To - photon.From;
            if (direction.Length < 1) continue;
            direction = direction.Normalize();
            var normal = new Vector(-direction.Y, direction.X);
            var wave = 5 + (photon.Nm / 50);
            const double Length = 54;
            var points = new List<Point>();
            for (var s = -Length; s <= 0; s += 1.5)
            {
                var envelope = Math.Sin(Math.PI * (s + Length) / Length);
                points.Add(at + (direction * s) + (normal * (6 * envelope * Math.Sin(2 * Math.PI * s / wave))));
            }

            var colour = PhotonColour(photon.Nm);
            var alpha = photon.Kind is PhotonKind.Given or PhotonKind.Passing ? 1 - (t * 0.6) : 1;
            dc.DrawGeometry(null, FadedPen(colour, alpha, 2), Line(points));
            if (photon.Kind is PhotonKind.Given) latestOut = photon;
            else if (photon.Kind is PhotonKind.Fired or PhotonKind.Lifting) latestIn = photon;
        }

        foreach (var (photon, id) in new[] { (latestIn, "photonin"), (latestOut, "photonout") })
        {
            if (photon is null) continue;
            var t = Math.Clamp((_atomTime - photon.Start) / photon.Duration, 0, 1);
            var at = photon.From + ((photon.To - photon.From) * t);
            _targets.Add(id, at, 14);
            if (ShowLabels) DrawText(dc, Nanometres(photon.Nm), at + new Vector(0, -16), PhotonColour(photon.Nm), 10, TextAnchor.Center);
        }
    }

    #endregion
}
