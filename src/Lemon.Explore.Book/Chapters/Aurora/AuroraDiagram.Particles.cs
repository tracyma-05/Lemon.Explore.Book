using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Chapters.DayNight;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Aurora;

/// <summary>
/// Particles mode: Earth and its dipole seen from a little above the magnetic equator (drag to turn round), one electron or
/// proton spiralling round a field line, bouncing between its mirror points and drifting round Earth on its shell. A
/// particle inside the loss cone reaches the air and is lost there, lighting it. An inset shows the pitch angle against
/// the loss cone. With the ring current on, many particles drift at once, protons west and electrons east.
/// </summary>
public sealed partial class AuroraDiagram
{
    /// <summary>Seconds one bounce takes at 1×.</summary>
    private const double BounceSeconds = 2.6;

    private TrappedParticle? _particle;
    private double _lostFor;
    private double _ringTime;

    /// <summary>The view: turned round the magnetic axis and raised above the equator, degrees.</summary>
    private double _azimuth = 25;
    private double _elevation = 16;

    // The projection of the last frame.
    private Point _pc;
    private double _ps = 40;
    private Vec3 _toward;
    private Vec3 _right;
    private Vec3 _up;

    private void RestartParticle()
    {
        // Sent off on the side facing the viewer, a little to the left, so its first loops are in front.
        _particle = new TrappedParticle(Species, Math.Clamp(Shell, 1.5, 7), Math.Clamp(PitchAngle, 1, 89.5), _azimuth - 20);
        _lostFor = 0;
    }

    private void StepParticle(double seconds)
    {
        if (_particle is null) RestartParticle();
        var particle = _particle!;
        if (particle.IsLost)
        {
            _lostFor += seconds;
            if (_lostFor > 2.4) RestartParticle();
            return;
        }

        particle.Advance(seconds * particle.BounceTime / BounceSeconds);
    }

    private void SetView()
    {
        var (sa, ca) = Math.SinCos(_azimuth * Math.PI / 180);
        var (se, ce) = Math.SinCos(_elevation * Math.PI / 180);
        _toward = new Vec3(ce * ca, ce * sa, se);
        _right = new Vec3(-sa, ca, 0);
        _up = _toward.Cross(_right);
    }

    /// <summary>A point in Earth radii on screen, and how far it is towards the viewer (behind Earth when negative).</summary>
    private (Point At, double Depth) P3(Vec3 p) =>
        (new Point(_pc.X + (p.Dot(_right) * _ps), _pc.Y - (p.Dot(_up) * _ps)), p.Dot(_toward));

    /// <summary>A point of the dipole field line of shell <paramref name="shell"/> at magnetic latitude and longitude, degrees.</summary>
    private static Vec3 OnLine(double shell, double latitude, double longitude)
    {
        var (sl, cl) = Math.SinCos(latitude * Math.PI / 180);
        var (so, co) = Math.SinCos(longitude * Math.PI / 180);
        var r = shell * cl * cl;
        return new Vec3(r * cl * co, r * cl * so, r * sl);
    }

    private void DrawParticlesMode(PaintContext dc)
    {
        var stage = Stage();
        if (_particle is null) RestartParticle();
        var particle = _particle!;
        var shell = particle.Shell;
        _ps = Math.Clamp(Math.Min(stage.Width * 0.5 / (Math.Max(shell, 4) + 1.6), stage.Height / (2 * (0.62 * Math.Max(shell, 4) + 1.2))), 14, 120);
        _pc = new Point(stage.Left + (stage.Width * 0.46), stage.Top + (stage.Height * 0.5));
        SetView();
        var now = _clock.Elapsed.TotalSeconds;

        var backLines = new StreamGeometry();
        var frontLines = new StreamGeometry();
        var ownBack = new StreamGeometry();
        var ownFront = new StreamGeometry();
        var foot = AuroraModel.FootLatitude(shell);
        var ownLongitude = particle.Longitude;
        using (var back = backLines.Open())
        using (var front = frontLines.Open())
        {
            for (var k = 0; k < 8; k++)
            {
                AddSplitLine(back, front, Enumerable.Range(0, 61).Select(i => OnLine(shell, -foot + (2 * foot * i / 60), k * 45.0)));
            }
        }

        using (var back = ownBack.Open())
        using (var front = ownFront.Open())
        {
            AddSplitLine(back, front, Enumerable.Range(0, 81).Select(i => OnLine(shell, -foot + (2 * foot * i / 80), ownLongitude)));
        }
        // The drift shell's equator, behind and in front of Earth.
        var ringBack = new StreamGeometry();
        var ringFront = new StreamGeometry();
        using (var back = ringBack.Open())
        using (var front = ringFront.Open())
        {
            AddSplitLine(back, front, Enumerable.Range(0, 121).Select(i => OnLine(shell, 0, i * 3.0)));
        }
        var linePen = FadedPen(Ink.Field, 0.22, 1);
        var ownPen = FadedPen(Ink.Field, 0.75, 1.6);
        var shellPen = FadedPen(Ink.Cyan, 0.45, 1.2, dashed: true);
        if (ShowBelts) DrawBeltsCut(dc);
        dc.DrawGeometry(null, linePen, backLines);
        if (Glows("shell")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 7), ringBack);
        dc.DrawGeometry(null, shellPen, ringBack);
        if (Glows("line")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 7), ownBack);
        dc.DrawGeometry(null, ownPen, ownBack);
        if (ShowRingCurrent) DrawRingCurrent(dc, front: false);
        DrawTrail(dc, particle, front: false, now);

        DrawEarthTilted(dc, _pc, _ps, _elevation, (_azimuth / 360) + (_ringTime * 0.01));
        _targets.Add("earth", _pc, _ps * 0.7);

        dc.DrawGeometry(null, linePen, frontLines);
        if (Glows("shell")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 7), ringFront);
        dc.DrawGeometry(null, shellPen, ringFront);
        if (Glows("line")) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 7), ownFront);
        dc.DrawGeometry(null, ownPen, ownFront);
        if (ShowRingCurrent) DrawRingCurrent(dc, front: true);
        DrawTrail(dc, particle, front: true, now);

        DrawMirrorsAndFeet(dc, particle, foot, ownLongitude, now);
        DrawParticleDot(dc, particle, now);
        DrawPitchInset(dc, stage, particle);
        DrawParticleLabels(dc, particle, shell);
    }

    /// <summary>Adds a 3D line as figures, the parts behind Earth's middle plane to one geometry and in front to the other.</summary>
    private void AddSplitLine(StreamGeometryContext back, StreamGeometryContext front, IEnumerable<Vec3> points)
    {
        StreamGeometryContext? current = null;
        Point last = default;
        foreach (var p in points)
        {
            var (at, depth) = P3(p);
            var target = depth < 0 ? back : front;
            if (!ReferenceEquals(target, current))
            {
                // Start the new part where the old one ended, so the line has no gap.
                current?.EndFigure(false);
                target.BeginFigure(current is null ? at : last, false);
                if (current is not null) target.LineTo(at);
                current = target;
            }
            else
            {
                target.LineTo(at);
            }

            last = at;
        }
    }

    /// <summary>The path, oldest faint, newest bright; the part behind Earth's middle plane or the part in front.</summary>
    private void DrawTrail(PaintContext dc, TrappedParticle particle, bool front, double now)
    {
        const int buckets = 8;
        var window = particle.BounceTime * 2.4;
        var geometries = new StreamGeometry[buckets];
        var contexts = new StreamGeometryContext[buckets];
        for (var i = 0; i < buckets; i++)
        {
            geometries[i] = new StreamGeometry();
            contexts[i] = geometries[i].Open();
        }

        var previous = (At: default(Point), Bucket: -1, Valid: false);
        foreach (var (point, time) in particle.Trail())
        {
            var age = (particle.Time - time) / window;
            if (age > 1)
            {
                previous.Valid = false;
                continue;
            }

            var (at, depth) = P3(point);
            var bucket = Math.Clamp((int)((1 - age) * buckets), 0, buckets - 1);
            var mine = depth >= 0 == front;
            if (mine && previous.Valid)
            {
                contexts[bucket].BeginFigure(previous.At, false);
                contexts[bucket].LineTo(at);
                contexts[bucket].EndFigure(false);
            }

            previous = (at, bucket, true);
        }

        var color = particle.Species == AuroraSpecies.Electron ? Ink.Cyan : Ink.Orange;
        for (var i = 0; i < buckets; i++)
        {
            contexts[i].Dispose();
            var alpha = 0.12 + (0.88 * (i + 1) / buckets);
            if (Glows("spiral") && i == buckets - 1) dc.DrawGeometry(null, ExploreTargets.GlowPen(now, 7), geometries[i]);
            dc.DrawGeometry(null, FadedPen(color, alpha, 1.1 + (0.9 * i / buckets), flat: true), geometries[i]);
        }
    }

    private void DrawParticleDot(PaintContext dc, TrappedParticle particle, double now)
    {
        var color = particle.Species == AuroraSpecies.Electron ? Ink.Cyan : Ink.Orange;
        var (at, depth) = P3(particle.Position);
        var hidden = depth < 0 && ((Vector)(at - _pc)).Length < _ps;
        if (particle.IsLost)
        {
            var strength = Math.Clamp(1 - (_lostFor / 2.4), 0, 1);
            dc.DrawEllipse(Glow(Ink.Green, strength), null, at, 26 + (20 * (1 - strength)), 26 + (20 * (1 - strength)));
            dc.DrawEllipse(Glow(Ink.Red, strength * 0.6), null, at, 12, 12);
            _targets.Add("foot", at, 14);
            if (!hidden) DrawPill(dc, Loc("Aurora_Label_Lost"), new Point(at.X, at.Y - 30), Ink.Green);
            return;
        }

        if (hidden) return;
        dc.DrawEllipse(Glow(color, 0.9), null, at, 10, 10);
        dc.DrawEllipse(Solid(255, 255, 255, 255), null, at, 3, 3);
        _targets.Add("particle", at, 10);

        // Which way it gyrates: its velocity across the field, as a short arrow.
        var b = TrappedParticle.Field(particle.Position);
        var v = particle.Velocity;
        var across = v - (b * (v.Dot(b) / b.Dot(b)));
        var (tip, _) = P3(particle.Position + (across * 0.5));
        if (((Vector)(tip - at)).Length > 4) DrawArrow(dc, at, tip, FadedBrush(color, 0.9), 1.6, 7);
        _ = now;
    }

    /// <summary>The mirror points on the particle's line (where it turns back), and where the line meets the air.</summary>
    private void DrawMirrorsAndFeet(PaintContext dc, TrappedParticle particle, double foot, double longitude, double now)
    {
        var mirror = AuroraModel.MirrorLatitude(particle.PitchDegrees);
        var footAir = AuroraModel.FootLatitude(particle.Shell, AuroraModel.AtmosphereKm);
        var brush = FadedBrush(Ink.Gold, 0.95);
        if (mirror < footAir)
        {
            foreach (var sign in new[] { 1, -1 })
            {
                var (at, depth) = P3(OnLine(particle.Shell, sign * mirror, longitude));
                if (depth < 0 && ((Vector)(at - _pc)).Length < _ps) continue;
                dc.DrawEllipse(null, MakePen(brush, 2), at, 6, 6);
                dc.DrawLine(FadedPen(Ink.Gold, 0.9, 1.6), at + new Vector(-9, 0), at + new Vector(9, 0));
                if (sign > 0) _targets.Add("mirror", at, 10);
            }
        }

        // The line's feet in the air: where a particle in the loss cone ends up.
        foreach (var sign in new[] { 1, -1 })
        {
            var (at, depth) = P3(OnLine(particle.Shell, sign * Math.Min(foot, footAir), longitude));
            if (depth < 0 && ((Vector)(at - _pc)).Length < _ps * 0.98) continue;
            dc.DrawEllipse(Glow(Ink.Green, 0.35 + (0.15 * Math.Sin(now * 3))), null, at, 9, 9);
            if (sign > 0 && !particle.IsLost) _targets.Add("foot", at, 9);
        }
    }

    /// <summary>The Van Allen belts cut through in the plane across the view: inner (protons) and outer (electrons).</summary>
    private void DrawBeltsCut(PaintContext dc)
    {
        var side = _azimuth + 90;
        foreach (var (inner, outer, alpha, id) in new[] { (1.3, 2.2, 0.16, "inner"), (3.2, 6.0, 0.09, "outer") })
        {
            foreach (var longitude in new[] { side, side + 180 })
            {
                var limit = Math.Min(48, AuroraModel.FootLatitude(inner));
                var points = new List<Point>();
                for (var i = 0; i <= 30; i++) points.Add(P3(OnLine(outer, -limit + (2 * limit * i / 30), longitude)).At);
                for (var i = 30; i >= 0; i--) points.Add(P3(OnLine(inner, -limit + (2 * limit * i / 30), longitude)).At);
                dc.DrawGeometry(FadedBrush(Ink.Belt, alpha), null, Line(points, closed: true, filled: true));
            }

            var (at, _) = P3(OnLine((inner + outer) / 2, 0, side + 180));
            _targets.Add(id, at, Math.Max(8, (outer - inner) * _ps * 0.4));
        }
    }

    /// <summary>
    /// Many trapped particles at once, shown by their guiding centres: protons (orange) drifting west, electrons (blue) east.
    /// Their drifts add up to a current flowing west round Earth, the ring current.
    /// </summary>
    private void DrawRingCurrent(PaintContext dc, bool front)
    {
        var protons = FadedBrush(Ink.Orange, 0.95);
        var electrons = FadedBrush(Ink.Cyan, 0.95);
        for (var i = 0; i < 48; i++)
        {
            var proton = i % 2 == 0;
            var shell = 2.8 + ((i * 0.6180339887) % 1 * 2.4);
            var mirror = 12 + ((i * 0.7548776662) % 1 * 22);
            var drift = 360 * _ringTime / (16 * 4 / shell);
            var longitude = (i * 137.5) + (proton ? -drift : drift);
            var latitude = mirror * Math.Sin((_ringTime * 2.4) + (i * 1.7));
            var (at, depth) = P3(OnLine(shell, latitude, longitude));
            if (depth >= 0 != front) continue;
            dc.DrawEllipse(proton ? protons : electrons, null, at, 2.6, 2.6);
        }

        if (!front) return;

        // The current's direction: arrows going west round the equator.
        var brush = FadedBrush(Ink.Gold, 0.85);
        for (var k = 0; k < 6; k++)
        {
            var longitude = (k * 60) - (_ringTime * 6);
            var (a, da) = P3(OnLine(4, 0, longitude));
            var (b, _) = P3(OnLine(4, 0, longitude - 8));
            if (da < 0) continue;
            DrawArrow(dc, a, b, brush, 2, 9);
            if (k == 0) _targets.Add("ring", a, 12);
        }
    }

    /// <summary>The inset: the field's direction, the particle's velocity at the pitch angle, and the loss cones along the field.</summary>
    private void DrawPitchInset(PaintContext dc, Rect stage, TrappedParticle particle)
    {
        var size = Math.Clamp(Math.Min(stage.Width, stage.Height) * 0.28, 120, 180);
        var box = new Rect(stage.Right - size - 18, stage.Bottom - size - 10, size, size);
        dc.DrawRoundedRectangle(Solid(170, 6, 9, 18), Ink.Frame, box, 12, 12);
        var center = new Point(box.Left + (size / 2), box.Top + (size * 0.55));
        var radius = size * 0.34;
        var cone = AuroraModel.LossCone(particle.Shell);
        var lost = particle.PitchDegrees < cone;

        // The loss cones, up and down the field.
        foreach (var sign in new[] { -1, 1 })
        {
            var a = cone * Math.PI / 180;
            var tip = center;
            var left = center + new Vector(-Math.Sin(a) * radius, -sign * Math.Cos(a) * radius);
            var right = center + new Vector(Math.Sin(a) * radius, -sign * Math.Cos(a) * radius);
            dc.DrawGeometry(FadedBrush(Ink.Green, lost ? 0.55 : 0.3), FadedPen(Ink.Green, 0.8, 1), Polygon(tip, left, right));
        }

        // The field, straight up, and the velocity at the pitch angle.
        DrawArrow(dc, center + new Vector(0, radius * 1.05), center - new Vector(0, radius * 1.12), FadedBrush(Ink.Field, 0.9), 1.6, 8);
        var p = particle.PitchDegrees * Math.PI / 180;
        var velocity = center + new Vector(Math.Sin(p) * radius * 0.9, -Math.Cos(p) * radius * 0.9);
        var color = particle.Species == AuroraSpecies.Electron ? Ink.Cyan : Ink.Orange;
        DrawArrow(dc, center, velocity, FadedBrush(color, 1), 2.4, 10);
        var arc = new List<Point>();
        for (var i = 0; i <= 16; i++)
        {
            var t = p * i / 16;
            arc.Add(center + new Vector(Math.Sin(t) * radius * 0.42, -Math.Cos(t) * radius * 0.42));
        }

        dc.DrawGeometry(null, FadedPen(Ink.Gold, 0.9, 1.4), Line(arc));
        DrawText(dc, Loc("Aurora_Inset_Pitch"), new Point(box.Left + 10, box.Top + 13), Ink.Text, 11.5, bold: true);
        DrawText(dc, LocFormat("Aurora_Inset_Values", Number(particle.PitchDegrees, "0"), Number(cone, "0.0")),
            new Point(box.Left + 10, box.Bottom - 12), lost ? Ink.Green : Ink.Dim, 10.5);
        _targets.Add("cone", center - new Vector(0, radius * 0.7), 12);
    }

    private void DrawParticleLabels(PaintContext dc, TrappedParticle particle, double shell)
    {
        if (!ShowLabels) return;
        var (lineAt, _) = P3(OnLine(shell, -30, particle.Longitude));
        DrawTargetPill(dc, Loc("Aurora_Label_Line"), lineAt + new Vector(46, 0), Ink.Field, "line");
        var (ring, _) = P3(OnLine(shell, 0, _azimuth + 60));
        DrawTargetPill(dc, Loc("Aurora_Label_Shell"), ring + new Vector(0, 16), Ink.Cyan, "shell");
        if (!particle.IsLost)
        {
            var (at, depth) = P3(particle.Position);
            if (depth >= 0 || ((Vector)(at - _pc)).Length > _ps)
            {
                DrawTargetPill(dc, Loc(particle.Species == AuroraSpecies.Electron ? "Aurora_Label_Electron" : "Aurora_Label_Proton"),
                    at + new Vector(0, -22), particle.Species == AuroraSpecies.Electron ? Ink.Cyan : Ink.Orange, "particle");
            }

            var (spiral, _) = P3(OnLine(shell, -12, particle.Longitude));
            DrawTargetPill(dc, Loc("Aurora_Label_Spiral"), spiral + new Vector(-60, 0), Ink.Text, "spiral");
        }

        var mirror = AuroraModel.MirrorLatitude(particle.PitchDegrees);
        if (mirror < AuroraModel.FootLatitude(shell, AuroraModel.AtmosphereKm))
        {
            var (m, _) = P3(OnLine(shell, mirror, particle.Longitude));
            DrawTargetPill(dc, Loc("Aurora_Label_Mirror"), m + new Vector(-62, -4), Ink.Gold, "mirror");
        }

        var drift = Loc(particle.Species == AuroraSpecies.Electron ? "Aurora_Label_DriftEast" : "Aurora_Label_DriftWest");
        var (driftAt, _) = P3(OnLine(shell, 0, _azimuth - 50));
        DrawPill(dc, drift, driftAt + new Vector(0, 18), Ink.Dim, 11);
        if (ShowBelts)
        {
            var (inner, _) = P3(OnLine(1.75, 0, _azimuth + 270));
            DrawTargetPill(dc, Loc("Aurora_Label_Inner"), inner + new Vector(-6, 12), Ink.Belt, "inner");
            var (outer, _) = P3(OnLine(4.6, 26, _azimuth + 270));
            DrawTargetPill(dc, Loc("Aurora_Label_Outer"), outer + new Vector(-10, -6), Ink.Belt, "outer");
        }

        if (ShowRingCurrent)
        {
            var (ring2, _) = P3(OnLine(4, 0, _azimuth));
            DrawTargetPill(dc, Loc("Aurora_Label_Ring"), ring2 + new Vector(0, 22), Ink.Gold, "ring");
        }
    }
}
