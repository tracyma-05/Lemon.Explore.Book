using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Lemon.Explore.Book.Chapters.Rainbow;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Lenses;

/// <summary>The three views of the lenses page.</summary>
public enum LensMode
{
    /// <summary>An optical bench: a candle, a lens, the rays through it, the image and a screen to catch it.</summary>
    Bench,

    /// <summary>The eye in cross-section: cornea, iris, lens and retina, focusing near and far, with glasses.</summary>
    Eye,

    /// <summary>A camera: lens, aperture, sensor and the photo it takes (focus, depth of field, shutter, exposure).</summary>
    Camera,
}

public enum LensKind
{
    /// <summary>Thicker in the middle: brings light together.</summary>
    Convex,

    /// <summary>Thinner in the middle: spreads light out.</summary>
    Concave,
}

/// <summary>What the lens makes of the candle.</summary>
public enum ImageKind
{
    /// <summary>The light really meets again: the image can be caught on a screen.</summary>
    Real,

    /// <summary>The light only seems to come from it: seen by looking through the lens.</summary>
    Virtual,

    /// <summary>The candle at the focal point: the light leaves parallel and never meets.</summary>
    None,
}

/// <summary>What is worn in front of the eye.</summary>
public enum Spectacles
{
    None,

    /// <summary>Glasses made for this eye's distance vision (cancelling its short or long sight).</summary>
    Distance,

    /// <summary>+2.5 D reading glasses.</summary>
    Reading,
}

/// <summary>How bright it is round the eye, which sets the pupil.</summary>
public enum LightLevel
{
    Bright,
    Indoor,
    Dim,
}

/// <summary>The light on the camera's scene.</summary>
public enum SceneLight
{
    Sun,
    Cloud,

    /// <summary>After sunset, the sky still light.</summary>
    Dusk,

    /// <summary>A street at night, lit by lamps.</summary>
    Night,
}

/// <summary>
/// A lens of glass with two spherical faces, in centimetres: the radii (positive when the centre of curvature is to the
/// right, light going left to right), the thickness on the axis, the half-height of the clear part and the index.
/// The middle of the lens is at x = 0.
/// </summary>
public readonly record struct LensShape(double R1, double R2, double Thickness, double Aperture, double N)
{
    /// <summary>Where the left face crosses the axis.</summary>
    public double Front => -Thickness / 2;

    /// <summary>Where the right face crosses the axis.</summary>
    public double Back => Thickness / 2;

    /// <summary>The left face's x at height <paramref name="y"/>.</summary>
    public double FrontAt(double y) => Front + LensModel.Sag(R1, y);

    /// <summary>The right face's x at height <paramref name="y"/>.</summary>
    public double BackAt(double y) => Back + LensModel.Sag(R2, y);
}

/// <summary>A ray through a real lens: where it goes in, where it comes out and which way it then goes.</summary>
public readonly record struct LensRay(Point Entry, Point Exit, Vector Out);

/// <summary>
/// The optics of the lenses page. The bench: the thin-lens equation 1/u + 1/v = 1/f (u, v and f in the same unit,
/// distances to the right of the lens positive for the image), the blur a screen shows away from the image, and rays
/// traced through a real glass lens with two spherical faces (Snell's law at each), which shows spherical aberration
/// and, with the glass's index by colour, chromatic aberration. The eye: a two-surface model (cornea and crystalline
/// lens) worked with vergences in dioptres. The camera: a thin lens with a full-frame sensor, depth of field, field of
/// view and exposure.
/// </summary>
public static class LensModel
{
    #region the bench (thin lens)

    /// <summary>The image's distance behind the lens, or −∞…∞ for a candle at the focal point (positive: real, to the right).</summary>
    public static double ImageDistance(double f, double u)
    {
        var vergence = (1 / f) - (1 / u);
        return Math.Abs(vergence) < 1e-12 ? double.PositiveInfinity : 1 / vergence;
    }

    /// <summary>The image's size over the object's, negative when it is upside down: f / (f − u).</summary>
    public static double Magnification(double f, double u) =>
        Math.Abs(f - u) < 1e-12 ? double.PositiveInfinity : f / (f - u);

    public static ImageKind Kind(double f, double u)
    {
        if (f < 0) return ImageKind.Virtual;
        if (Math.Abs(u - f) < 1e-9) return ImageKind.None;
        return u > f ? ImageKind.Real : ImageKind.Virtual;
    }

    /// <summary>
    /// The width of the patch of light a screen at <paramref name="screen"/> behind the lens shows for one point of the
    /// candle, for a lens whose clear part has half-height <paramref name="aperture"/>: 0 where the image is.
    /// </summary>
    public static double BlurOnScreen(double f, double u, double aperture, double screen) =>
        2 * aperture * Math.Abs(1 - (screen * ((1 / f) - (1 / u))));

    /// <summary>
    /// How high the candle's tip lands on a screen at <paramref name="screen"/>: where its ray through the middle of the
    /// lens meets it (the centre of the blur patch), negative below the axis.
    /// </summary>
    public static double HeightOnScreen(double height, double u, double screen) => -height * screen / u;

    /// <summary>A lens's power in dioptres from its focal length in centimetres.</summary>
    public static double Dioptres(double fCentimetres) => 100 / fCentimetres;

    #endregion

    #region glass by colour

    /// <summary>Crown glass (BK7)'s refractive index at a wavelength.</summary>
    public static double GlassIndex(double nm) => RainbowOptics.Index(OpticalMedium.Glass, nm);

    /// <summary>The index at the yellow line, where a lens's focal length is usually given.</summary>
    public static double GlassYellow => GlassIndex(RainbowOptics.YellowNm);

    /// <summary>A thin lens's focal length for one colour, given its focal length in yellow light: f ∝ 1/(n − 1).</summary>
    public static double FocalAt(double fYellow, double nm) => fYellow * (GlassYellow - 1) / (GlassIndex(nm) - 1);

    #endregion

    #region a real lens

    /// <summary>How far a face of radius <paramref name="r"/> bulges at height <paramref name="y"/> (positive to the right).</summary>
    public static double Sag(double r, double y)
    {
        if (double.IsInfinity(r)) return 0;
        var inside = (r * r) - (y * y);
        if (inside < 0) inside = 0;
        return r - (Math.Sign(r) * Math.Sqrt(inside));
    }

    /// <summary>
    /// The glass lens, equal on both sides, whose focal length near the axis is <paramref name="f"/> (negative for a
    /// concave lens), clear to a half-height of <paramref name="aperture"/>, at least <paramref name="edge"/> thick at
    /// its thinnest. The thick-lens formula 1/f = (n−1)[1/R1 − 1/R2 + (n−1)t/(n R1 R2)] is solved for R with the
    /// thickness it needs, a few times round.
    /// </summary>
    public static LensShape Shape(double f, double aperture, double edge = 0.2, double? index = null)
    {
        var n = index ?? GlassYellow;
        var g = Math.Abs(f);
        var t = edge;
        double r = 2 * (n - 1) * g;
        for (var i = 0; i < 12; i++)
        {
            if (f > 0)
            {
                // R1 = R, R2 = −R: R² − 2(n−1)f R + (n−1)² f t / n = 0, the larger root.
                var root = 1 - (t / (n * g));
                r = (n - 1) * g * (1 + Math.Sqrt(Math.Max(0, root)));
                var sag = r - Math.Sqrt(Math.Max(0, (r * r) - (aperture * aperture)));
                t = (2 * sag) + edge;
            }
            else
            {
                // R1 = −R, R2 = R, thin in the middle: R² g − 2(n−1)R − (n−1)² t / n = 0 with g = 1/|f|.
                r = (n - 1) * g * (1 + Math.Sqrt(1 + (t / (n * g))));
                t = edge;
            }
        }

        return f > 0 ? new LensShape(r, -r, t, aperture, n) : new LensShape(-r, r, t, aperture, n);
    }

    /// <summary>The same lens in a glass of another index (another colour): the faces stay, only the bending changes.</summary>
    public static LensShape WithIndex(LensShape shape, double n) => shape with { N = n };

    /// <summary>
    /// Follows a ray from <paramref name="origin"/> along <paramref name="direction"/> through the lens: Snell's law at
    /// each face. Null when it misses the clear part or cannot get out.
    /// </summary>
    public static LensRay? Trace(LensShape lens, Point origin, Vector direction)
    {
        direction = direction.Normalize();
        var entry = HitFace(lens.R1, lens.Front, lens.Aperture, origin, direction);
        if (entry is null) return null;
        var inside = Refract(direction, entry.Value.Normal, 1, lens.N);
        if (inside is null) return null;

        var exit = HitFace(lens.R2, lens.Back, lens.Aperture, entry.Value.Point + (inside.Value * 1e-9), inside.Value);
        if (exit is null) return null;
        var outside = Refract(inside.Value, exit.Value.Normal, lens.N, 1);
        if (outside is null) return null;
        return new LensRay(entry.Value.Point, exit.Value.Point, outside.Value);
    }

    /// <summary>Where a ray parallel to the axis at height <paramref name="height"/> crosses the axis after the lens (x), or NaN.</summary>
    public static double AxisCrossing(LensShape lens, double height)
    {
        if (Trace(lens, new Point(-100, height), new Vector(1, 0)) is not { } ray) return double.NaN;
        if (Math.Abs(ray.Out.Y) < 1e-12) return double.PositiveInfinity;
        var t = -ray.Exit.Y / ray.Out.Y;
        return ray.Exit.X + (ray.Out.X * t);
    }

    /// <summary>The face of radius <paramref name="r"/> whose vertex is at x = <paramref name="vertex"/>: where a ray first meets it.</summary>
    private static (Point Point, Vector Normal)? HitFace(double r, double vertex, double aperture, Point origin, Vector direction)
    {
        Point hit;
        Vector normal;
        if (double.IsInfinity(r))
        {
            if (Math.Abs(direction.X) < 1e-12) return null;
            var t = (vertex - origin.X) / direction.X;
            if (t <= 0) return null;
            hit = origin + (direction * t);
            normal = new Vector(-1, 0);
        }
        else
        {
            var centre = new Point(vertex + r, 0);
            Vector oc = origin - centre;
            var b = (oc.X * direction.X) + (oc.Y * direction.Y);
            var c = oc.SquaredLength - (r * r);
            var disc = (b * b) - c;
            if (disc < 0) return null;
            var root = Math.Sqrt(disc);

            // Of the two crossings, the one on the cap round the vertex (the same side of the centre as the vertex).
            Point? found = null;
            foreach (var t in new[] { -b - root, -b + root })
            {
                if (t <= 1e-9) continue;
                var p = origin + (direction * t);
                if ((p.X - centre.X) * (vertex - centre.X) <= 0) continue;
                found = p;
                break;
            }

            if (found is null) return null;
            hit = found.Value;
            normal = (hit - centre) / Math.Abs(r);
        }

        if (Math.Abs(hit.Y) > aperture + 1e-9) return null;

        // The normal on the side the light comes from.
        if ((normal.X * direction.X) + (normal.Y * direction.Y) > 0) normal = -normal;
        return (hit, normal);
    }

    private static Vector? Refract(Vector direction, Vector normal, double n1, double n2) =>
        RainbowOptics.Refract(direction, normal, n1, n2);

    #endregion

    #region the eye

    /// <summary>The cornea's power, dioptres (Gullstrand's eye has about 43).</summary>
    public const double CorneaPower = 43.0;

    /// <summary>The crystalline lens's power when relaxed (looking far), dioptres.</summary>
    public const double LensPowerRelaxed = 19.0;

    /// <summary>From the cornea to the crystalline lens's principal plane, m.</summary>
    public const double LensDepth = 0.0057;

    /// <summary>The index of the clear jelly inside the eye.</summary>
    public const double EyeIndex = 1.336;

    /// <summary>How far in front of the cornea glasses sit, m.</summary>
    public const double GlassesGap = 0.012;

    /// <summary>Reading glasses' power, dioptres.</summary>
    public const double ReadingPower = 2.5;

    /// <summary>
    /// Vergence after light travels a (reduced) distance: V / (1 − d·V). Vergence is n over the distance to where the
    /// light is heading (positive: converging), in dioptres; inside the eye the distance is divided by its index.
    /// </summary>
    public static double Transfer(double vergence, double distance) => vergence / (1 - (distance * vergence));

    /// <summary>The eye's length, cornea to retina, m, for an eye whose far point has vergence <paramref name="error"/> (its short or long sight, D).</summary>
    public static double AxialLength(double error)
    {
        var atLens = Transfer(error + CorneaPower, LensDepth / EyeIndex) + LensPowerRelaxed;
        return LensDepth + (EyeIndex / atLens);
    }

    /// <summary>How much the eye can add by accommodating at an age, dioptres (Hofstetter's average, 18.5 − 0.3 × age).</summary>
    public static double Amplitude(double age) => Math.Clamp(18.5 - (0.3 * age), 0.5, 18.5);

    /// <summary>The pupil's diameter in this light, m.</summary>
    public static double Pupil(LightLevel light) => light switch
    {
        LightLevel.Bright => 0.0025,
        LightLevel.Dim => 0.0065,
        _ => 0.004,
    };

    /// <summary>Glasses that let an eye with this error see far things sharp, relaxed (at the usual gap): E / (1 + g E).</summary>
    public static double DistanceGlasses(double error) => error / (1 + (GlassesGap * error));

    public static double GlassesPower(Spectacles glasses, double error) => glasses switch
    {
        Spectacles.Distance => DistanceGlasses(error),
        Spectacles.Reading => ReadingPower,
        _ => 0,
    };

    /// <summary>
    /// The vergence of light from a point <paramref name="distance"/> m in front of the cornea as it reaches the cornea,
    /// through glasses of power <paramref name="glasses"/> (0 for none), D. Infinity is a far object (0).
    /// </summary>
    public static double VergenceAtEye(double distance, double glasses)
    {
        if (glasses == 0) return double.IsPositiveInfinity(distance) ? 0 : -1 / distance;
        var atGlasses = double.IsPositiveInfinity(distance) ? 0 : -1 / (distance - GlassesGap);
        return Transfer(atGlasses + glasses, GlassesGap);
    }

    /// <summary>
    /// The object distance (m in front of the cornea) whose light reaches the cornea with vergence
    /// <paramref name="vergence"/> through the glasses: +∞ for 0, and NaN when no real object does (it would have to be
    /// "beyond infinity", converging light).
    /// </summary>
    public static double DistanceFor(double vergence, double glasses)
    {
        if (glasses == 0)
        {
            if (Math.Abs(vergence) < 1e-12) return double.PositiveInfinity;
            return vergence < 0 ? -1 / vergence : double.NaN;
        }

        var leaving = Transfer(vergence, -GlassesGap);
        var arriving = leaving - glasses;
        if (Math.Abs(arriving) < 1e-12) return double.PositiveInfinity;
        return arriving < 0 ? GlassesGap - (1 / arriving) : double.NaN;
    }

    /// <summary>The eye's focus now: how much it accommodates (D, within its amplitude) and what is left out of focus.</summary>
    public static EyeFocus Focus(double error, double age, double vergence)
    {
        var amplitude = Amplitude(age);
        var used = Math.Clamp(error - vergence, 0, amplitude);
        var focused = error - used;
        return new EyeFocus(used, amplitude, focused, vergence - focused);
    }

    /// <summary>The crystalline lens's power when the eye focuses light of corneal vergence <paramref name="focused"/> on its retina.</summary>
    public static double LensPower(double length, double focused) =>
        (EyeIndex / (length - LensDepth)) - Transfer(focused + CorneaPower, LensDepth / EyeIndex);

    /// <summary>
    /// Where light of corneal vergence <paramref name="vergence"/> comes to a point behind the cornea, m, with the lens at
    /// <paramref name="lensPower"/>; +∞ if it never does.
    /// </summary>
    public static double FocusDepth(double vergence, double lensPower)
    {
        var atLens = Transfer(vergence + CorneaPower, LensDepth / EyeIndex) + lensPower;
        return atLens <= 0 ? double.PositiveInfinity : LensDepth + (EyeIndex / atLens);
    }

    /// <summary>The angle a point is smeared over by a focus error of <paramref name="defocus"/> D through a pupil, radians.</summary>
    public static double BlurAngle(double defocus, double pupil) => Math.Abs(defocus) * pupil;

    /// <summary>
    /// The smallest detail that can be made out, arc minutes: 1′ for a sharp eye, about a fifth of the blur when out of
    /// focus (a rough rule; real acuity also depends on the eye's own aberrations and the light).
    /// </summary>
    public static double SmallestDetail(double blurArcMinutes) => Math.Max(1, blurArcMinutes / 5);

    /// <summary>The 5-point (logMAR) score used on Chinese charts, 5.0 for 1′ detail.</summary>
    public static double FivePointScore(double detailArcMinutes) => 5 - Math.Log10(detailArcMinutes);

    #endregion

    #region the camera

    /// <summary>A full-frame sensor's width and height, mm.</summary>
    public const double SensorWidth = 36;

    public const double SensorHeight = 24;

    /// <summary>The largest blur still counted as sharp on a full-frame sensor, mm (the usual circle of confusion).</summary>
    public const double CircleOfConfusion = 0.03;

    public static readonly double[] FStops = [1.4, 2, 2.8, 4, 5.6, 8, 11, 16, 22];

    /// <summary>Shutter times, s.</summary>
    public static readonly double[] Shutters = [1.0 / 4000, 1.0 / 2000, 1.0 / 1000, 1.0 / 500, 1.0 / 250, 1.0 / 125, 1.0 / 60, 1.0 / 30, 1.0 / 15, 1.0 / 8, 1.0 / 4, 1.0 / 2, 1];

    public static readonly int[] Isos = [100, 200, 400, 800, 1600, 3200, 6400];

    /// <summary>How far behind the lens a subject <paramref name="distance"/> m away is focused, mm (f for a far one).</summary>
    public static double SensorDistance(double f, double distance) =>
        double.IsPositiveInfinity(distance) ? f : 1 / ((1 / f) - (1 / (distance * 1000)));

    /// <summary>The blur circle on the sensor for a subject at <paramref name="subject"/> m when focused at <paramref name="focus"/> m, mm.</summary>
    public static double BlurOnSensor(double f, double fNumber, double focus, double subject)
    {
        var sensor = SensorDistance(f, focus);
        var image = SensorDistance(f, subject);
        return f / fNumber * Math.Abs(sensor - image) / image;
    }

    /// <summary>The focus distance beyond which everything to infinity looks sharp, m.</summary>
    public static double Hyperfocal(double f, double fNumber) => ((f * f / (fNumber * CircleOfConfusion)) + f) / 1000;

    /// <summary>The nearest and farthest distances that look sharp, m (far is +∞ at or past the hyperfocal distance).</summary>
    public static (double Near, double Far) DepthOfField(double f, double fNumber, double focus)
    {
        var h = Hyperfocal(f, fNumber) * 1000;
        if (double.IsPositiveInfinity(focus)) return (h / 1000, double.PositiveInfinity);
        var s = focus * 1000;
        var near = s * (h - f) / (h + s - (2 * f));
        var far = s < h ? s * (h - f) / (h - s) : double.PositiveInfinity;
        return (near / 1000, far / 1000);
    }

    /// <summary>The angle across the picture's width, degrees.</summary>
    public static double FieldOfView(double f, double size = SensorWidth) => 2 * Math.Atan(size / (2 * f)) * 180 / Math.PI;

    /// <summary>The exposure value of an f-number and a shutter time: log₂(N²/t).</summary>
    public static double ExposureValue(double fNumber, double shutter) => Math.Log2(fNumber * fNumber / shutter);

    /// <summary>A scene's brightness as the exposure value that suits it at ISO 100.</summary>
    public static double SceneValue(SceneLight light) => light switch
    {
        SceneLight.Sun => 15,
        SceneLight.Cloud => 12,
        SceneLight.Dusk => 9,
        _ => 5,
    };

    /// <summary>How many stops too much light the photo gets (negative: too dark).</summary>
    public static double ExposureError(SceneLight light, double fNumber, double shutter, int iso) =>
        SceneValue(light) + Math.Log2(iso / 100.0) - ExposureValue(fNumber, shutter);

    /// <summary>How far something moving across the view at <paramref name="speed"/> m/s, <paramref name="distance"/> m away, smears on the sensor, mm.</summary>
    public static double MotionBlur(double f, double speed, double distance, double shutter) => speed * shutter * f / distance;

    #endregion
}

/// <summary>
/// The eye's focus: the accommodation it uses (and has), the corneal vergence it now focuses on its retina, and what is
/// left over (positive: the light meets in front of the retina, as in short sight; negative: behind it).
/// </summary>
public readonly record struct EyeFocus(double Used, double Amplitude, double Focused, double Defocus);
