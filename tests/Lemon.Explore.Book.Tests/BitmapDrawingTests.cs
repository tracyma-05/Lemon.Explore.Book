using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Lemon.Explore.Book.Controls.Space;
using Xunit;

namespace Lemon.Explore.Book.Tests;

/// <summary>
/// Bitmaps made for a high-DPI screen are drawn whole. DrawImage takes its source rectangle in pixels and a bitmap's
/// Size in DIPs, so a 192 DPI bitmap drawn with DrawImage(bitmap, rect) shows only its top-left quarter.
/// </summary>
public sealed class BitmapDrawingTests
{
    [AvaloniaFact]
    public void AHighDpiBitmap_IsDrawnWhole()
    {
        // 100 × 100 pixels at 192 DPI is 50 × 50 DIPs; red over its top-left 25 × 25 DIPs (on a clear bitmap).
        var source = new RenderTargetBitmap(new PixelSize(100, 100), new Vector(192, 192));
        using (var dc = source.CreateDrawingContext(true))
        {
            dc.DrawRectangle(Brushes.Red, null, new Rect(0, 0, 25, 25));
        }

        Assert.Equal((50, 50), DrawnExtent(source));

        var target = new RenderTargetBitmap(new PixelSize(50, 50), new Vector(96, 96));
        using (var dc = target.CreateDrawingContext(true))
        {
            SpaceDrawing.DrawWhole(dc, source, new Rect(0, 0, 50, 50));
        }

        Assert.Equal((25, 25), DrawnExtent(target));
    }

    [AvaloniaFact]
    public void ReadPixels_GivesPremultipliedBgra_RowAfterRow()
    {
        // Opaque red on the left half, half-transparent white on the right half of a 4 × 2 picture.
        using var bitmap = SpaceDrawing.RenderPixels(4, 2, 96, dc =>
        {
            dc.DrawRectangle(Brushes.Red, null, new Rect(0, 0, 2, 2));
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)), null, new Rect(2, 0, 2, 2));
        });

        var pixels = SpaceDrawing.ReadPixels(bitmap);

        Assert.Equal(4 * 2 * 4, pixels.Length);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, pixels[..4]);
        var right = pixels.AsSpan(((1 * 4) + 3) * 4, 4).ToArray();
        Assert.InRange(right[3], 127, 129);
        Assert.All(right[..3], c => Assert.InRange(c, right[3] - 1, right[3] + 1));
    }

    [AvaloniaFact]
    public void RenderToBitmap_DrawsInDips_AtTheGivenScale()
    {
        // 40 × 40 DIPs at 2 pixels per DIP; red over the top-left 10 × 10 DIPs.
        var bitmap = SpaceDrawing.RenderToBitmap(40, 40, 2, dc => dc.DrawRectangle(Brushes.Red, null, new Rect(0, 0, 10, 10)));
        Assert.Equal(new PixelSize(80, 80), bitmap.PixelSize);
        Assert.Equal((20, 20), DrawnExtent(bitmap));

        // Drawn over 40 × 40 DIPs, all of it shows.
        var target = new RenderTargetBitmap(new PixelSize(40, 40), new Vector(96, 96));
        using (var dc = target.CreateDrawingContext(true))
        {
            dc.DrawImage(bitmap, new Rect(0, 0, 40, 40));
        }

        Assert.Equal((10, 10), DrawnExtent(target));
    }

    /// <summary>How far what is drawn reaches across and down, in pixels.</summary>
    private static (int Width, int Height) DrawnExtent(RenderTargetBitmap bitmap)
    {
        var (w, h) = (bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        var pixels = new byte[w * h * 4];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, w, h), handle.AddrOfPinnedObject(), pixels.Length, w * 4);
        }
        finally
        {
            handle.Free();
        }

        var (right, bottom) = (0, 0);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                // Alpha: the fourth byte in BGRA and RGBA alike (the platforms differ); the rest is clear.
                if (pixels[(((y * w) + x) * 4) + 3] > 128)
                {
                    right = Math.Max(right, x + 1);
                    bottom = Math.Max(bottom, y + 1);
                }
            }
        }

        return (right, bottom);
    }
}
