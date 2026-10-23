using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Lemon.Explore.Book.Controls.Explore;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Lenses;

/// <summary>
/// The camera: four things at different distances on a (squeezed) track, the depth of field and the focus mark, the light
/// from each through the lens and the aperture to the sensor (where the out-of-focus ones make blur circles), and the
/// photo with a ball thrown across, the settings and an exposure meter.
/// </summary>
public sealed partial class LensDiagram
{
    /// <summary>Distances on the camera's track: from 30 cm (right, by the camera) to 100 m, then infinity.</summary>
    internal const double CameraNear = 0.3;

    internal const double CameraFar = 100;

    /// <summary>The subjects' colours, nearest first: branch, person, trees, mountains.</summary>
    private static readonly Color[] SubjectColours =
    [
        Color.FromRgb(0x7C, 0xE0, 0x6A), Color.FromRgb(0xFF, 0x7A, 0x6E), Color.FromRgb(0x4E, 0xD0, 0xB8), Color.FromRgb(0x8A, 0xB4, 0xFF),
    ];

    /// <summary>How high above (or below) the axis each subject's point is drawn on the track, px.</summary>
    private static readonly double[] SubjectHeights = [34, 14, -12, -30];

    private readonly CameraPhoto _photo = new();
    private CameraView _camera;

    private readonly record struct CameraView(Rect Stage, double Y0, double TrackLeft, double TrackRight, double LensX, double SensorX, double Throw, Rect Photo, Rect Meter)
    {
        public double TrackX(double distance)
        {
            if (double.IsPositiveInfinity(distance) || distance >= CameraFar * 1.5) return TrackLeft + 8;
            var t = (Math.Log10(Math.Clamp(distance, CameraNear, CameraFar)) - Math.Log10(CameraNear)) / (Math.Log10(CameraFar) - Math.Log10(CameraNear));
            return TrackRight - (t * (TrackRight - TrackLeft - 40));
        }

        public double TrackDistance(double x)
        {
            if (x < TrackLeft + 24) return double.PositiveInfinity;
            var t = Math.Clamp((TrackRight - x) / (TrackRight - TrackLeft - 40), 0, 1);
            return Math.Pow(10, Math.Log10(CameraNear) + (t * (Math.Log10(CameraFar) - Math.Log10(CameraNear))));
        }

        /// <summary>A pixel of the photo on screen.</summary>
        public Point PhotoPoint(Point pixel) => new(Photo.X + (pixel.X / CameraPhoto.Width * Photo.Width), Photo.Y + (pixel.Y / CameraPhoto.Height * Photo.Height));

        /// <summary>The photo's pixel under a point on screen.</summary>
        public Point PhotoPixel(Point point) => new((point.X - Photo.X) / Photo.Width * CameraPhoto.Width, (point.Y - Photo.Y) / Photo.Height * CameraPhoto.Height);
    }

    /// <summary>
    /// How far behind the lens (as a share of the drawn lens-to-sensor length) light focuses that a real lens focuses
    /// <paramref name="ratio"/> further back than its focal length: stretched a lot, so the differences show.
    /// </summary>
    private static double Stretch(double ratio) => 0.62 * Math.Tanh(ratio * 9);

    private CameraView CameraLayout()
    {
        var stage = Stage();
        var band = Math.Clamp(stage.Height * 0.4, 150, 250);
        var y0 = stage.Top + (band / 2) + 4;
        var sensorX = stage.Right - 70;
        var throwPx = Math.Min(200, stage.Width * 0.18);
        var lensX = sensorX - (throwPx * (1 + Stretch((LensModel.SensorDistance(FocalMm, FocusDistance) - FocalMm) / FocalMm)));

        var top = stage.Top + band + 14;
        var height = Math.Max(120, stage.Bottom - top - 30);
        var width = Math.Min(stage.Width - 200, height * 1.5);
        height = width / 1.5;
        // The photo and its meter, centred under the track.
        var photo = new Rect(stage.Left + Math.Max(30, (stage.Width - width - 110) / 2), top, width, height);
        var meter = new Rect(photo.Right + 44, top + 8, 18, height - 16);
        return new CameraView(stage, y0, stage.Left + 24, sensorX - throwPx - 150, lensX, sensorX, throwPx, photo, meter);
    }

    private void DrawCameraMode(PaintContext dc, double now)
    {
        var v = _camera = CameraLayout();
        DrawCameraTrack(dc, v);
        DrawCameraBody(dc, v, now);
        DrawPhoto(dc, v);
    }

    #region the track and the light

    private void DrawCameraTrack(PaintContext dc, CameraView v)
    {
        var y = v.Y0;
        dc.DrawLine(Ink.Axis, new Point(v.TrackLeft, y), new Point(v.LensX, y));

        // The depth of field: everything between these looks sharp.
        var (near, far) = LensModel.DepthOfField(FocalMm, FNumber, FocusDistance);
        var from = v.TrackX(Math.Max(near, CameraNear));
        var to = v.TrackX(far);
        var band = new Rect(Math.Min(from, to), y - 7, Math.Abs(from - to), 14);
        dc.DrawRoundedRectangle(FadedBrush(Ink.Green, 0.2), FadedPen(Ink.Green, 0.5, 1), band, 6, 6);
        AddBoxTarget("dof", band);
        if (ShowLabels) DrawPill(dc, Loc("Lens_Label_Dof"), new Point(band.X + (band.Width / 2), y + 64), Ink.Green, 10.5);

        foreach (var (distance, key) in new[] { (0.5, "Lens_Tick_50cm"), (1.0, "Lens_Tick_1m"), (3.0, "Lens_Tick_3m"), (10.0, "Lens_Tick_10m"), (30.0, "Lens_Tick_30m") })
        {
            var x = v.TrackX(distance);
            dc.DrawLine(Ink.Faint, new Point(x, y - 4), new Point(x, y + 4));
            if (ShowLabels) DrawText(dc, Loc(key), new Point(x, y + 18), Ink.Dim, 10.5, TextAnchor.Center);
        }

        DrawText(dc, Loc("Lens_Value_Infinity"), new Point(v.TrackLeft + 8, y + 18), Ink.Dim, 12, TextAnchor.Center);

        for (var i = 0; i < 4; i++)
        {
            var at = new Point(v.TrackX(CameraPhoto.Distances[i]), y - SubjectHeights[i]);
            DrawSubjectIcon(dc, i, at);
            _targets.Add("subject" + (char)('0' + i), at, 12);
        }

        // The focus mark: a pin on the track, dragged to focus.
        var focusX = v.TrackX(FocusDistance);
        var pin = new Point(focusX, y + 9);
        dc.DrawGeometry(Solid(255, Ink.Gold.R, Ink.Gold.G, Ink.Gold.B), null, Polygon(pin, new Point(focusX - 7, y + 21), new Point(focusX + 7, y + 21)));
        dc.DrawLine(FadedPen(Ink.Gold, 0.5, 1, dashed: true), new Point(focusX, y - 50), new Point(focusX, y + 9));
        var label = ShowLabels ? DrawPill(dc, LocFormat("Lens_Label_FocusAt", Metres(FocusDistance)), new Point(focusX, y + 36), Ink.Gold, 11) : new Rect(focusX - 8, y + 9, 16, 14);
        AddBoxTarget("focusmark", label.Union(new Rect(focusX - 8, y + 6, 16, 16)));
    }

    private void DrawSubjectIcon(PaintContext dc, int index, Point at)
    {
        var colour = SubjectColours[index];
        var brush = Solid(255, colour.R, colour.G, colour.B);
        switch (index)
        {
            case 0:
            {
                var leaf = SpaceDrawing.Ellipse(at, 9, 4.5);
                leaf.Transform = new MatrixTransform(SpaceDrawing.Rotation(-30, at.X, at.Y));
                dc.DrawGeometry(brush, null, leaf);
                dc.DrawLine(MakePen(brush, 1.4), new Point(at.X + 7, at.Y - 5), new Point(at.X + 14, at.Y - 10));
                break;
            }

            case 1:
                dc.DrawEllipse(brush, null, new Point(at.X, at.Y - 9), 3.5, 3.5);
                dc.DrawRoundedRectangle(brush, null, new Rect(at.X - 4, at.Y - 5, 8, 10), 2, 2);
                dc.DrawRectangle(brush, null, new Rect(at.X - 3.5, at.Y + 5, 3, 7));
                dc.DrawRectangle(brush, null, new Rect(at.X + 0.5, at.Y + 5, 3, 7));
                break;

            case 2:
                dc.DrawRectangle(Solid(255, 0x8A, 0x6A, 0x4A), null, new Rect(at.X - 1.5, at.Y + 2, 3, 9));
                dc.DrawEllipse(brush, null, new Point(at.X, at.Y - 3), 7, 8);
                break;

            default:
                dc.DrawGeometry(brush, null, Polygon(new Point(at.X - 13, at.Y + 8), new Point(at.X - 3, at.Y - 9), new Point(at.X + 3, at.Y - 2), new Point(at.X + 7, at.Y - 6), new Point(at.X + 15, at.Y + 8)));
                break;
        }
    }

    /// <summary>The camera in cross-section: the lens and its aperture, the shutter and the sensor, and the light from each subject.</summary>
    private void DrawCameraBody(PaintContext dc, CameraView v, double now)
    {
        var y = v.Y0;
        var lensHalf = Math.Min(56, (v.Stage.Height * 0.4 / 2) - 18);
        var body = new Rect(v.SensorX - 130, y - lensHalf - 22, 156, (2 * lensHalf) + 44);
        var barrel = new Rect(v.LensX - 22, y - lensHalf - 6, body.Left - v.LensX + 22, (2 * lensHalf) + 12);
        dc.DrawRoundedRectangle(Solid(255, 0x1E, 0x22, 0x2C), MakePen(Solid(255, 0x44, 0x4A, 0x58), 1.2), barrel, 6, 6);
        dc.DrawRoundedRectangle(Solid(255, 0x24, 0x28, 0x34), MakePen(Solid(255, 0x4A, 0x52, 0x62), 1.2), body, 10, 10);
        AddBoxTarget("camlens", new Rect(v.LensX - 14, y - lensHalf, 28, 2 * lensHalf));

        // The lens.
        var glass = new List<Point>();
        for (var i = 0; i <= 20; i++)
        {
            var t = -1 + (2.0 * i / 20);
            glass.Add(new Point(v.LensX - (9 * (1 - (t * t))), y - (t * lensHalf)));
        }

        for (var i = 20; i >= 0; i--)
        {
            var t = -1 + (2.0 * i / 20);
            glass.Add(new Point(v.LensX + (9 * (1 - (t * t))), y - (t * lensHalf)));
        }

        dc.DrawGeometry(FadedBrush(Ink.Glass, 0.3), FadedPen(Ink.Glass, 0.9, 1.3), Line(glass, closed: true, filled: true));

        // The aperture: blades closing in from the barrel to an opening of the focal length over the f-number.
        var opening = Math.Max(2.5, lensHalf * 0.95 * 1.4 / FNumber);
        var blade = Solid(255, 0x0C, 0x0E, 0x14);
        var bladeX = v.LensX + 14;
        dc.DrawRectangle(blade, null, new Rect(bladeX - 2.5, y - lensHalf - 4, 5, lensHalf + 4 - opening));
        dc.DrawRectangle(blade, null, new Rect(bladeX - 2.5, y + opening, 5, lensHalf + 4 - opening));
        _targets.Add("aperture", new Point(bladeX, y - opening - 8), 9);

        // The shutter in front of the sensor, and the sensor.
        var sensorHalf = Math.Min(42, lensHalf * 0.75);
        dc.DrawRectangle(FadedBrush(Color.FromRgb(0x50, 0x56, 0x66), 0.9), null, new Rect(v.SensorX - 10, y - sensorHalf - 8, 3, (2 * sensorHalf) + 16));
        _targets.Add("shutter", new Point(v.SensorX - 9, y - sensorHalf - 16), 8);
        var sensor = new Rect(v.SensorX, y - sensorHalf, 7, 2 * sensorHalf);
        dc.DrawRectangle(Solid(255, 0x1E, 0x50, 0x3C), MakePen(Solid(255, 0x5A, 0xA8, 0x80), 1), sensor);
        AddBoxTarget("sensor", sensor.Inflate(new Thickness(3, 2)));

        // The light from each subject: in through the opening, to where it comes to a point, and what reaches the sensor.
        var sensorDistance = LensModel.SensorDistance(FocalMm, FocusDistance);
        var lensRatio = (sensorDistance - FocalMm) / FocalMm;
        for (var i = 3; i >= 0; i--)
        {
            var colour = SubjectColours[i];
            var subject = new Point(v.TrackX(CameraPhoto.Distances[i]), y - SubjectHeights[i]);
            var ratio = (LensModel.SensorDistance(FocalMm, CameraPhoto.Distances[i]) - FocalMm) / FocalMm;
            var imageX = v.SensorX - (v.Throw * (1 + Stretch(lensRatio))) + (v.Throw * (1 + Stretch(ratio)));
            var imageY = y + ((y - subject.Y) * (imageX - v.LensX) / Math.Max(20, v.LensX - subject.X));
            var image = new Point(imageX, imageY);
            var edges = new List<Point>();
            foreach (var side in new[] { -1, 1 })
            {
                var atLens = new Point(v.LensX, y + (side * opening));
                dc.DrawLine(FadedPen(colour, 0.22, 1), subject, atLens);
                var along = image - atLens;
                var t = (v.SensorX - atLens.X) / along.X;
                var onSensor = atLens + (along * t);
                if (imageX <= v.SensorX)
                {
                    dc.DrawLine(FadedPen(colour, 0.75, 1.2), atLens, onSensor);
                }
                else
                {
                    dc.DrawLine(FadedPen(colour, 0.75, 1.2), atLens, onSensor);
                    dc.DrawLine(FadedPen(colour, 0.4, 1, dashed: true), onSensor, image);
                }

                edges.Add(onSensor);
            }

            // Where it lands: a point if in focus, else a blur circle (seen edge-on, a bar).
            var low = Math.Min(edges[0].Y, edges[1].Y);
            var high = Math.Max(edges[0].Y, edges[1].Y);
            dc.DrawRoundedRectangle(FadedBrush(colour, 0.85), null, new Rect(v.SensorX + 1, low - 1.5, 5, high - low + 3), 2, 2);
            if (i == 0) _targets.Add("blurcircle", new Point(v.SensorX + 4, (low + high) / 2), Math.Max(8, (high - low) / 2));
        }

        if (!ShowLabels) return;
        DrawPill(dc, LocFormat("Lens_Label_CameraLens", Number(FocalMm, "0")), new Point(v.LensX, y - lensHalf - 18), Ink.Glass, 10.5);
        DrawPill(dc, LocFormat("Lens_Label_Aperture", FText(FNumber)), new Point(bladeX + 4, y + lensHalf + 18), Ink.Text, 10.5);
        DrawPill(dc, Loc("Lens_Label_Sensor"), new Point(v.SensorX - 4, body.Bottom + 14), Color.FromRgb(0x8A, 0xE0, 0xB0), 10.5);
        _ = now;
    }

    #endregion

    #region the photo

    private void DrawPhoto(PaintContext dc, CameraView v)
    {
        var error = LensModel.ExposureError(Scene, FNumber, Shutter, Iso);
        var gain = Math.Pow(2, Math.Clamp(error, -8, 8));
        var grain = 1.3 * Math.Sqrt(Iso / 100.0);
        var photo = v.Photo;
        dc.DrawRectangle(Solid(255, 0, 0, 0), MakePen(Solid(255, 0x3A, 0x40, 0x4E), 1.5), photo.Inflate(new Thickness(4, 4)));
        dc.DrawImage(_photo.Picture(FocalMm, FNumber, FocusDistance, gain, grain, Scene), photo);

        dc.PushClip(new RectangleGeometry(photo));
        DrawBall(dc, v, gain);
        DrawFocusBox(dc, v);
        dc.Pop();

        var readout = LocFormat("Lens_Readout", Number(FocalMm, "0"), FText(FNumber), ShutterText(Shutter), Iso);
        DrawText(dc, readout, new Point(photo.X, photo.Bottom + 16), Ink.Text, 12);
        var fov = LocFormat("Lens_Label_Fov", Number(LensModel.FieldOfView(FocalMm), "0"));
        var fovBox = DrawText(dc, fov, new Point(photo.Right, photo.Bottom + 16), Ink.Dim, 11.5, TextAnchor.Right);
        AddBoxTarget("fov", fovBox);
        AddBoxTarget("photo", DrawPill(dc, Loc("Lens_Label_Photo"), new Point(photo.X + 30, photo.Y + 14), Ink.Text, 11));

        DrawMeter(dc, v.Meter, error);
    }

    /// <summary>The ball, flying across in front of the person: smeared along its path for as long as the shutter is open.</summary>
    private void DrawBall(PaintContext dc, CameraView v, double gain)
    {
        // Thrown across again and again: 6 m in 1.5 s (4 m/s), in an arc.
        const double Flight = 1.5;
        var time = _time % Flight;

        Point At(double t)
        {
            t = Math.Clamp(t, 0, Flight) / Flight;
            var metres = new Point(-3 + (6 * t), 0.9 + (2.6 * t * (1 - t)));
            return v.PhotoPoint(CameraPhoto.Project(metres, CameraPhoto.BallDistance, FocalMm));
        }

        var scale = v.Photo.Width / CameraPhoto.Width;
        var radius = CameraPhoto.BallSize / 2 * CameraPhoto.Scale(FocalMm, CameraPhoto.BallDistance) * scale;
        var blur = LensModel.BlurOnSensor(FocalMm, FNumber, FocusDistance, CameraPhoto.BallDistance) / LensModel.SensorWidth * CameraPhoto.Width * scale;
        var start = At(time - Shutter);
        var end = At(time);
        var streak = ((Vector)(end - start)).Length;
        var colour = SoftImage.Expose(CameraPhoto.BallColour(Scene), gain);

        // The light of the ball spread over its smear (and its blur): fainter the longer the smear.
        var share = Math.Clamp((2 * radius) / ((2 * radius) + streak + (blur * 0.5)), 0.08, 1);
        var halo = Math.Max(0, blur / 2);
        if (halo > 1) dc.DrawGeometry(null, FadedPen(colour, share * 0.35, (2 * radius) + halo), Line([start, end + new Vector(0.01, 0)]));
        dc.DrawGeometry(null, FadedPen(colour, share, Math.Max(1.5, (2 * radius) - Math.Min(radius, halo * 0.3))), Line([start, end + new Vector(0.01, 0)]));
        _targets.Add("ball", end, Math.Max(10, radius + 4));
    }

    /// <summary>The camera's focus box: on the subject it is focused on, or in the middle.</summary>
    private void DrawFocusBox(PaintContext dc, CameraView v)
    {
        var layer = Array.FindIndex(CameraPhoto.Distances, d => double.IsPositiveInfinity(d) ? double.IsPositiveInfinity(FocusDistance) || FocusDistance > 500 : Math.Abs(Math.Log(FocusDistance / d)) < 0.04);
        var at = layer >= 0 ? v.PhotoPoint(CameraPhoto.SubjectPoint(layer, FocalMm)) : new Point(v.Photo.X + (v.Photo.Width / 2), v.Photo.Y + (v.Photo.Height / 2));
        var box = new Rect(at.X - 16, at.Y - 12, 32, 24);
        var pen = FadedPen(Ink.Green, 0.95, 1.6);
        foreach (var (corner, dx, dy) in new[] { (box.TopLeft, 1, 1), (box.TopRight, -1, 1), (box.BottomLeft, 1, -1), (box.BottomRight, -1, -1) })
        {
            dc.DrawLine(pen, corner, new Point(corner.X + (7 * dx), corner.Y));
            dc.DrawLine(pen, corner, new Point(corner.X, corner.Y + (7 * dy)));
        }
    }

    /// <summary>The exposure meter: three stops each way, the needle where this exposure is.</summary>
    private void DrawMeter(PaintContext dc, Rect meter, double error)
    {
        dc.DrawRoundedRectangle(Solid(255, 0x14, 0x18, 0x22), Ink.Frame, meter, 6, 6);
        var mid = meter.Y + (meter.Height / 2);
        var step = meter.Height / 7;
        for (var stop = -3; stop <= 3; stop++)
        {
            var y = mid - (stop * step);
            dc.DrawLine(stop == 0 ? MakePen(Solid(255, 0x7C, 0xFF, 0x9E), 2) : Ink.Faint, new Point(meter.X + 3, y), new Point(meter.Right - 3, y));
            DrawText(dc, Number(stop, "+0;-0;0"), new Point(meter.Right + 6, y), stop == 0 ? Ink.Green : Ink.Dim, 10);
        }

        var needleY = mid - (Math.Clamp(error, -3.4, 3.4) * step);
        var colour = Math.Abs(error) < 0.5 ? Ink.Green : Math.Abs(error) < 1.5 ? Ink.Gold : Ink.Red;
        dc.DrawGeometry(Solid(255, colour.R, colour.G, colour.B), null, Polygon(new Point(meter.X - 2, needleY), new Point(meter.X - 10, needleY - 6), new Point(meter.X - 10, needleY + 6)));
        DrawText(dc, Loc("Lens_Label_Bright"), new Point(meter.X + (meter.Width / 2), meter.Y - 12), Ink.Dim, 10, TextAnchor.Center);
        DrawText(dc, Loc("Lens_Label_Dark"), new Point(meter.X + (meter.Width / 2), meter.Bottom + 12), Ink.Dim, 10, TextAnchor.Center);
        AddBoxTarget("meter", meter.Inflate(new Thickness(12, 14)));
    }

    #endregion
}
