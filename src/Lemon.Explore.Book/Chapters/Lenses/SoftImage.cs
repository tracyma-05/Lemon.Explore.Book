using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Lenses;

/// <summary>
/// Pictures as premultiplied BGRA bytes: drawn with WPF into a bitmap, blurred as an out-of-focus lens blurs (a disc,
/// approximated by three box passes each way), laid over one another and shown.
/// </summary>
internal static class SoftImage
{
    /// <summary>Draws into a new premultiplied BGRA buffer of <paramref name="width"/> × <paramref name="height"/> pixels.</summary>
    public static byte[] Draw(int width, int height, Action<PaintContext> draw)
    {
        using var bitmap = SpaceDrawing.RenderPixels(width, height, 96, draw);
        return SpaceDrawing.ReadPixels(bitmap);
    }

    /// <summary>
    /// Blurs <paramref name="source"/> as a disc of <paramref name="diameter"/> pixels would, into a new buffer (or a
    /// copy when the blur is under a pixel). Three box passes of width d/2 give a spread like the disc's (σ = d/4).
    /// </summary>
    public static byte[] Blur(byte[] source, int width, int height, double diameter)
    {
        var result = (byte[])source.Clone();
        var radius = (int)Math.Round(Math.Min(diameter, 400) / 4);
        if (radius < 1) return result;
        var scratch = new byte[source.Length];
        for (var pass = 0; pass < 3; pass++)
        {
            BoxRows(result, scratch, width, height, radius);
            BoxColumns(scratch, result, width, height, radius);
        }

        return result;
    }

    private static void BoxRows(byte[] source, byte[] target, int width, int height, int radius)
    {
        var span = (2 * radius) + 1;
        for (var y = 0; y < height; y++)
        {
            var row = y * width * 4;
            for (var c = 0; c < 4; c++)
            {
                var sum = 0;
                for (var i = -radius; i <= radius; i++) sum += source[row + (Math.Clamp(i, 0, width - 1) * 4) + c];
                for (var x = 0; x < width; x++)
                {
                    target[row + (x * 4) + c] = (byte)((sum + (span / 2)) / span);
                    var add = Math.Min(x + radius + 1, width - 1);
                    var drop = Math.Max(x - radius, 0);
                    sum += source[row + (add * 4) + c] - source[row + (drop * 4) + c];
                }
            }
        }
    }

    private static void BoxColumns(byte[] source, byte[] target, int width, int height, int radius)
    {
        var span = (2 * radius) + 1;
        var stride = width * 4;
        for (var x = 0; x < width; x++)
        {
            for (var c = 0; c < 4; c++)
            {
                var column = (x * 4) + c;
                var sum = 0;
                for (var i = -radius; i <= radius; i++) sum += source[(Math.Clamp(i, 0, height - 1) * stride) + column];
                for (var y = 0; y < height; y++)
                {
                    target[(y * stride) + column] = (byte)((sum + (span / 2)) / span);
                    var add = Math.Min(y + radius + 1, height - 1);
                    var drop = Math.Max(y - radius, 0);
                    sum += source[(add * stride) + column] - source[(drop * stride) + column];
                }
            }
        }
    }

    /// <summary>Lays <paramref name="layer"/> (premultiplied) over <paramref name="target"/> in place.</summary>
    public static void Over(byte[] target, byte[] layer)
    {
        for (var i = 0; i < target.Length; i += 4)
        {
            var alpha = layer[i + 3];
            if (alpha == 0) continue;
            var keep = 255 - alpha;
            target[i] = (byte)(layer[i] + ((target[i] * keep) + 127) / 255);
            target[i + 1] = (byte)(layer[i + 1] + ((target[i + 1] * keep) + 127) / 255);
            target[i + 2] = (byte)(layer[i + 2] + ((target[i + 2] * keep) + 127) / 255);
            target[i + 3] = (byte)(alpha + ((target[i + 3] * keep) + 127) / 255);
        }
    }

    /// <summary>A bitmap to show, made once per size and written into again.</summary>
    public static WriteableBitmap Show(ref WriteableBitmap? bitmap, byte[] pixels, int width, int height)
    {
        if (bitmap is null || bitmap.PixelSize.Width != width || bitmap.PixelSize.Height != height)
        {
            bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        }

        SpaceDrawing.WritePixels(bitmap, pixels, width, height);
        return bitmap;
    }

    #region light

    private static readonly float[] ToLinear = BuildToLinear();
    private static readonly byte[] ToDisplay = BuildToDisplay();

    private static float[] BuildToLinear()
    {
        var table = new float[256];
        for (var i = 0; i < 256; i++)
        {
            var c = i / 255.0;
            table[i] = (float)(c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4));
        }

        return table;
    }

    private static byte[] BuildToDisplay()
    {
        var table = new byte[4097];
        for (var i = 0; i <= 4096; i++)
        {
            var l = i / 4096.0;
            var c = l <= 0.0031308 ? l * 12.92 : (1.055 * Math.Pow(l, 1 / 2.4)) - 0.055;
            table[i] = (byte)Math.Round(Math.Clamp(c, 0, 1) * 255);
        }

        return table;
    }

    /// <summary>A display value (0–255) with <paramref name="gain"/> times the light, clipped at white.</summary>
    public static byte Expose(byte value, double gain)
    {
        var l = ToLinear[value] * gain;
        return ToDisplay[(int)(Math.Clamp(l, 0, 1) * 4096)];
    }

    public static Color Expose(Color color, double gain) =>
        Color.FromArgb(color.A, Expose(color.R, gain), Expose(color.G, gain), Expose(color.B, gain));

    /// <summary>
    /// An opaque picture given <paramref name="gain"/> times the light (stops of over or under exposure) and grain of
    /// <paramref name="grain"/> (the spread of the noise, in display levels), in place. The grain pattern is fixed.
    /// </summary>
    public static void Develop(byte[] pixels, double gain, double grain)
    {
        var lut = new byte[256];
        for (var i = 0; i < 256; i++) lut[i] = Expose((byte)i, gain);
        var noise = Noise(pixels.Length / 4);
        for (int i = 0, p = 0; i < pixels.Length; i += 4, p++)
        {
            var n = grain * noise[p];
            pixels[i] = Clip(lut[pixels[i]] + n);
            pixels[i + 1] = Clip(lut[pixels[i + 1]] + n + (0.35 * grain * noise[(p * 7) % noise.Length]));
            pixels[i + 2] = Clip(lut[pixels[i + 2]] + n);
            pixels[i + 3] = 255;
        }
    }

    private static byte Clip(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);

    private static float[]? _noise;

    /// <summary>A fixed field of normally distributed numbers (mean 0, spread 1), one per pixel.</summary>
    private static float[] Noise(int count)
    {
        if (_noise is { } cached && cached.Length >= count) return cached;
        var random = new Random(20261010);
        var noise = new float[count];
        for (var i = 0; i < count; i++)
        {
            // Box–Muller.
            var u1 = 1 - random.NextDouble();
            var u2 = random.NextDouble();
            noise[i] = (float)(Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2));
        }

        _noise = noise;
        return noise;
    }

    #endregion
}
