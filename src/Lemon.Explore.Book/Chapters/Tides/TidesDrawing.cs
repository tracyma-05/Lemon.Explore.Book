using Avalonia.Media;
using Avalonia.Media.Immutable;
using static Lemon.Explore.Book.Controls.Space.SpaceDrawing;
using Lemon.Explore.Book.Controls.Space;

namespace Lemon.Explore.Book.Chapters.Tides;

/// <summary>Colours and pens shared by the tides views.</summary>
internal static class TidesDrawing
{
    public static readonly Color Text = Color.FromRgb(0xEE, 0xF1, 0xF6);
    public static readonly Color DimText = Color.FromArgb(0xC0, 0xC9, 0xD1, 0xDC);
    public static readonly Color Gold = Color.FromRgb(0xFF, 0xD3, 0x6B);
    public static readonly Color Water = Color.FromRgb(0x6F, 0xC3, 0xFF);
    public static readonly Color MoonBlue = Color.FromRgb(0xB8, 0xC7, 0xFF);
    public static readonly Color Force = Color.FromRgb(0xFF, 0x8A, 0x65);
    public static readonly Color High = Color.FromRgb(0x7F, 0xE0, 0xFF);
    public static readonly Color Low = Color.FromRgb(0xE8, 0xC3, 0x8A);

    /// <summary>The water round Earth in the views from above.</summary>
    public static readonly IBrush WaterFill = SpaceDrawing.Solid(Color.FromArgb(120, 0x2E, 0x8C, 0xF0));

    public static readonly IPen WaterEdge = MakePen(Solid(230, 0x8F, 0xD3, 0xFF), 1.6);

    /// <summary>The Moon's tide alone and the Sun's alone, as outlines over the water.</summary>
    public static readonly IPen LunarPen = MakePen(SpaceDrawing.Solid(MoonBlue), 1.6, dashed: true);

    public static readonly IPen SolarPen = MakePen(SpaceDrawing.Solid(Gold), 1.6, dashed: true);

    public static readonly IPen ForcePen = MakePen(SpaceDrawing.Solid(Force), 2);
    public static readonly IBrush ForceBrush = SpaceDrawing.Solid(Force);

    /// <summary>The line of a curve of water height.</summary>
    public static readonly IPen CurvePen = MakePen(SpaceDrawing.Solid(Water), 2);

    public static readonly IBrush HighBrush = SpaceDrawing.Solid(High);
    public static readonly IBrush LowBrush = SpaceDrawing.Solid(Low);
    public static readonly IPen DotOutline = MakePen(Solid(255, 10, 14, 24), 1.2);

    public static readonly IPen OrbitPen = MakePen(Solid(90, 255, 255, 255), 1.1, dashed: true);
    public static readonly IBrush ArrowBrush = Solid(150, 255, 255, 255);

    /// <summary>A whole number of hours and minutes, e.g. "12 h 25 min" (the format comes from the strings).</summary>
    public static (int Hours, int Minutes) HoursMinutes(double hours)
    {
        var minutes = (int)Math.Round(hours * 60);
        return (minutes / 60, minutes % 60);
    }
}
