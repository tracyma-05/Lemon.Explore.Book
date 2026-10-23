using Lemon.Explore.Book.Chapters.Lenses;
using Xunit;

namespace Lemon.Explore.Book.Tests;

public sealed class LensModelTests
{
    [Fact]
    public void ThinLens_PutsTheImageWhereTheTextbooksDo()
    {
        // At 2F the image is at 2F, the same size and upside down.
        Assert.Equal(30, LensModel.ImageDistance(15, 30), 9);
        Assert.Equal(-1, LensModel.Magnification(15, 30), 9);

        // Beyond 2F: between F and 2F, smaller.
        Assert.Equal(22.5, LensModel.ImageDistance(15, 45), 9);
        Assert.Equal(-0.5, LensModel.Magnification(15, 45), 9);

        // Inside F: a virtual image on the candle's side, upright and larger (a magnifier).
        Assert.Equal(-15, LensModel.ImageDistance(15, 7.5), 9);
        Assert.Equal(2, LensModel.Magnification(15, 7.5), 9);

        // A concave lens: always virtual, upright and smaller.
        Assert.Equal(-10, LensModel.ImageDistance(-15, 30), 9);
        Assert.Equal(1.0 / 3, LensModel.Magnification(-15, 30), 9);

        Assert.Equal(ImageKind.Real, LensModel.Kind(15, 45));
        Assert.Equal(ImageKind.Virtual, LensModel.Kind(15, 9));
        Assert.Equal(ImageKind.None, LensModel.Kind(15, 15));
        Assert.Equal(ImageKind.Virtual, LensModel.Kind(-15, 45));
        Assert.True(double.IsPositiveInfinity(LensModel.ImageDistance(15, 15)));
    }

    [Fact]
    public void Screen_IsSharpOnlyAtTheImage()
    {
        Assert.Equal(0, LensModel.BlurOnScreen(15, 45, 4, 22.5), 9);

        // Right against the lens the patch is as wide as the lens; past the image it widens again.
        Assert.Equal(8, LensModel.BlurOnScreen(15, 45, 4, 0), 9);
        Assert.Equal(LensModel.BlurOnScreen(15, 45, 4, 20), LensModel.BlurOnScreen(15, 45, 4, 25), 1);
        Assert.True(LensModel.BlurOnScreen(15, 45, 4, 40) > LensModel.BlurOnScreen(15, 45, 4, 30));

        // The middle ray sets the size: half the candle's height, upside down, at the image.
        Assert.Equal(-1.5, LensModel.HeightOnScreen(3, 45, 22.5), 9);
        Assert.Equal(10, LensModel.Dioptres(10), 9);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(25)]
    public void RealConvexLens_FocusesNearItsThinLensFocalLength(double f)
    {
        var shape = LensModel.Shape(f, 4);
        Assert.True(shape.R1 > 0 && shape.R2 < 0);
        Assert.True(shape.Thickness > 0.2 && shape.Thickness < 3);

        // Near the axis the glass lens focuses where the thin lens says (within its thickness).
        var paraxial = LensModel.AxisCrossing(shape, 0.05);
        Assert.InRange(paraxial, f - (shape.Thickness / 2) - 0.3, f + 0.3);

        // Spherical aberration: rays near the edge cross nearer the lens.
        var edge = LensModel.AxisCrossing(shape, 3.8);
        Assert.True(edge < paraxial - 0.05, $"edge {edge} vs paraxial {paraxial}");
    }

    [Fact]
    public void RealConcaveLens_SpreadsLightAsIfFromItsFocalPoint()
    {
        var shape = LensModel.Shape(-15, 4);
        Assert.True(shape.R1 < 0 && shape.R2 > 0);

        // The ray leaves going away from the axis; traced back it crosses near x = −15.
        var crossing = LensModel.AxisCrossing(shape, 0.05);
        Assert.InRange(crossing, -15.5, -14.5);
    }

    [Fact]
    public void Glass_FocusesBlueNearerThanRed()
    {
        Assert.True(LensModel.FocalAt(15, 450) < 15);
        Assert.True(LensModel.FocalAt(15, 656) > 15);
        Assert.Equal(15, LensModel.FocalAt(15, 589.3), 9);

        // The spread for crown glass is about 1.5% of the focal length (Abbe number ~64).
        var spread = (LensModel.FocalAt(15, 656.3) - LensModel.FocalAt(15, 486.1)) / 15;
        Assert.InRange(spread, 0.012, 0.02);
    }

    [Fact]
    public void Eye_HasTheRightLengthAndShortSightMeansLonger()
    {
        var normal = LensModel.AxialLength(0) * 1000;
        Assert.InRange(normal, 23.5, 25);

        // About a third of a millimetre per dioptre.
        var shortSighted = LensModel.AxialLength(-3) * 1000;
        Assert.InRange(shortSighted - normal, 0.9, 1.4);
        Assert.True(LensModel.AxialLength(2) * 1000 < normal);

        // Relaxed, the lens has its usual power and a far point focuses on the retina.
        Assert.Equal(LensModel.LensPowerRelaxed, LensModel.LensPower(LensModel.AxialLength(0), 0), 6);
        Assert.Equal(normal, LensModel.FocusDepth(0, LensModel.LensPowerRelaxed) * 1000, 6);
    }

    [Fact]
    public void Accommodation_ShrinksWithAge()
    {
        Assert.Equal(12.5, LensModel.Amplitude(20), 9);
        Assert.Equal(5, LensModel.Amplitude(45), 9);
        Assert.Equal(0.5, LensModel.Amplitude(60), 9);
        Assert.Equal(0.5, LensModel.Amplitude(75), 9);

        // A young eye reads a book 25 cm away, bulging its lens 4 D, and stays sharp.
        var focus = LensModel.Focus(0, 20, LensModel.VergenceAtEye(0.25, 0));
        Assert.Equal(4, focus.Used, 6);
        Assert.Equal(0, focus.Defocus, 6);
        Assert.True(LensModel.LensPower(LensModel.AxialLength(0), focus.Focused) > LensModel.LensPowerRelaxed + 4);

        // At sixty it cannot: the light meets behind the retina.
        var older = LensModel.Focus(0, 60, LensModel.VergenceAtEye(0.25, 0));
        Assert.Equal(0.5, older.Used, 6);
        Assert.True(older.Defocus < -3);

        // Near points: 8 cm at twenty, 2 m at sixty.
        Assert.Equal(0.08, LensModel.DistanceFor(-LensModel.Amplitude(20), 0), 6);
        Assert.Equal(2, LensModel.DistanceFor(-LensModel.Amplitude(60), 0), 6);
    }

    [Fact]
    public void ShortSight_BlursFarThingsAndGlassesFixIt()
    {
        // −3 D: far light meets 3 D in front of the retina; the far point is a third of a metre away.
        var bare = LensModel.Focus(-3, 20, LensModel.VergenceAtEye(double.PositiveInfinity, 0));
        Assert.Equal(3, bare.Defocus, 6);
        Assert.Equal(1.0 / 3, LensModel.DistanceFor(-3, 0), 6);

        // Glasses 12 mm in front need a little more: −3.11 D.
        var glasses = LensModel.DistanceGlasses(-3);
        Assert.InRange(glasses, -3.12, -3.10);
        var corrected = LensModel.Focus(-3, 20, LensModel.VergenceAtEye(double.PositiveInfinity, glasses));
        Assert.Equal(0, corrected.Defocus, 6);
        Assert.True(double.IsPositiveInfinity(LensModel.DistanceFor(-3, glasses)));

        // Long sight beyond the eye's amplitude: no real far point.
        Assert.True(double.IsNaN(LensModel.DistanceFor(2, 0)));
    }

    [Theory]
    [InlineData(0.4, 0.0)]
    [InlineData(0.4, 2.5)]
    [InlineData(2.0, -3.11)]
    public void VergenceAtTheEye_RoundTripsThroughGlasses(double distance, double glasses)
    {
        var vergence = LensModel.VergenceAtEye(distance, glasses);
        Assert.Equal(distance, LensModel.DistanceFor(vergence, glasses), 9);
    }

    [Fact]
    public void Blur_GivesRoughAcuity()
    {
        // 1 D through a 4 mm pupil: 4 mrad, about 13.75′.
        var blur = LensModel.BlurAngle(1, 0.004) * 180 / Math.PI * 60;
        Assert.InRange(blur, 13.7, 13.8);
        Assert.Equal(5.0, LensModel.FivePointScore(LensModel.SmallestDetail(0)), 9);
        Assert.Equal(4.0, LensModel.FivePointScore(10), 9);
    }

    [Fact]
    public void Camera_FocusesNearThingsFartherBack()
    {
        Assert.Equal(50, LensModel.SensorDistance(50, double.PositiveInfinity), 9);
        Assert.InRange(LensModel.SensorDistance(50, 5), 50.50, 50.52);
        Assert.InRange(LensModel.SensorDistance(50, 0.5), 55.5, 55.6);

        // Sharp where it is focused; a nearer subject blurs, half as much two stops (4×) down.
        Assert.Equal(0, LensModel.BlurOnSensor(50, 2.8, 5, 5), 9);
        var wide = LensModel.BlurOnSensor(50, 2.8, 5, 0.9);
        var narrow = LensModel.BlurOnSensor(50, 5.6, 5, 0.9);
        Assert.Equal(wide / 2, narrow, 9);
        Assert.True(wide > LensModel.CircleOfConfusion * 10);
    }

    [Fact]
    public void DepthOfField_MatchesTheHyperfocalRule()
    {
        // 50 mm at f/8 with a 0.03 mm circle: H = f²/(N c) + f ≈ 10.47 m.
        var h = LensModel.Hyperfocal(50, 8);
        Assert.InRange(h, 10.46, 10.47);

        // Focused at H, sharp from about H/2 to infinity.
        var (near, far) = LensModel.DepthOfField(50, 8, h);
        Assert.True(double.IsPositiveInfinity(far));
        Assert.InRange(near, (h / 2) - 0.05, (h / 2) + 0.05);

        // Opening up makes it shallower.
        var (n1, f1) = LensModel.DepthOfField(50, 1.4, 5);
        var (n2, f2) = LensModel.DepthOfField(50, 11, 5);
        Assert.True(f1 - n1 < f2 - n2);
        Assert.True(n1 < 5 && f1 > 5);
    }

    [Fact]
    public void FieldOfView_AndExposure_FollowTheUsualNumbers()
    {
        Assert.InRange(LensModel.FieldOfView(50), 39.5, 39.7);
        Assert.InRange(LensModel.FieldOfView(24), 73.6, 73.8);

        // Sunny 16: f/16 at 1/125 s and ISO 100 is right for a sunny day.
        Assert.InRange(LensModel.ExposureError(SceneLight.Sun, 16, 1.0 / 125, 100), -0.1, 0.1);

        // Doubling the time or the ISO adds a stop.
        var start = LensModel.ExposureError(SceneLight.Cloud, 5.6, 1.0 / 125, 100);
        Assert.Equal(start + 1, LensModel.ExposureError(SceneLight.Cloud, 5.6, 2.0 / 125, 100), 9);
        Assert.Equal(start + 1, LensModel.ExposureError(SceneLight.Cloud, 5.6, 1.0 / 125, 200), 9);

        // The ball crossing at 4 m/s, 4.6 m away, at 1/15 s with 50 mm: 2.9 mm on the sensor.
        Assert.InRange(LensModel.MotionBlur(50, 4, 4.6, 1.0 / 15), 2.89, 2.91);
    }

    [Fact]
    public void Blur_SpreadsLightWithoutLosingMuch()
    {
        const int Size = 60;
        var pixels = new byte[Size * Size * 4];
        for (var y = 20; y < 40; y++)
        {
            for (var x = 20; x < 40; x++)
            {
                for (var c = 0; c < 4; c++) pixels[(((y * Size) + x) * 4) + c] = 200;
            }
        }

        var blurred = SoftImage.Blur(pixels, Size, Size, 12);
        double Sum(byte[] p) => p.Where((_, i) => i % 4 == 3).Sum(v => (double)v);
        Assert.InRange(Sum(blurred) / Sum(pixels), 0.95, 1.05);

        // The edge is soft: a pixel just outside the block now has some light.
        Assert.True(blurred[(((30 * Size) + 41) * 4) + 3] > 20);
        Assert.Equal(pixels, SoftImage.Blur(pixels, Size, Size, 1));
    }
}
