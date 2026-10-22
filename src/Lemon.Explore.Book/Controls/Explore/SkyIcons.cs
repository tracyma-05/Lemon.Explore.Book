using Avalonia.Media;

namespace Lemon.Explore.Book.Controls.Explore;

/// <summary>Small 24x24 icons for the Sun's state, chosen by view models (drawn for this project).</summary>
public static class SkyIcons
{
    public static Geometry Sun { get; } = Geometry.Parse(
        "M12,8 A4,4 0 1 1 12,16 A4,4 0 1 1 12,8 Z M11,1 H13 V5 H11 Z M11,19 H13 V23 H11 Z M1,11 H5 V13 H1 Z M19,11 H23 V13 H19 Z " +
        "M5.6,4.2 L8.4,7 L7,8.4 L4.2,5.6 Z M18.4,4.2 L19.8,5.6 L17,8.4 L15.6,7 Z M4.2,18.4 L7,15.6 L8.4,17 L5.6,19.8 Z M15.6,17 L17,15.6 L19.8,18.4 L18.4,19.8 Z");

    /// <summary>Half a Sun on the horizon with an arrow up.</summary>
    public static Geometry Sunrise { get; } = Geometry.Parse("M2,18 H22 V20 H2 Z M6,16 A6,6 0 0 1 18,16 Z M11,5 H13 V9 H11 Z M8.5,6 L12,2.5 L15.5,6 Z");

    /// <summary>Half a Sun on the horizon with an arrow down.</summary>
    public static Geometry Sunset { get; } = Geometry.Parse("M2,18 H22 V20 H2 Z M6,16 A6,6 0 0 1 18,16 Z M11,2 H13 V6 H11 Z M8.5,5 L15.5,5 L12,8.5 Z");

    /// <summary>A crescent Moon.</summary>
    public static Geometry Night { get; } = Geometry.Parse("M14,3 A9,9 0 1 0 21,15 A7,7 0 0 1 14,3 Z");
}
