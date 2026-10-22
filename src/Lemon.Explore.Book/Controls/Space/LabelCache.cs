using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Lemon.Explore.Book.Infrastructures.Localization;

namespace Lemon.Explore.Book.Controls.Space;

/// <summary>Localized labels drawn on a diagram, formatted once and dropped when the language changes.</summary>
internal sealed class LabelCache(Control owner)
{
    private readonly Dictionary<(string Key, double Size, Color Color, bool Bold), FormattedText> _cache = [];
    private readonly Dictionary<(string Text, double Size, Color Color, bool Bold), FormattedText> _texts = [];

    public void Clear()
    {
        _cache.Clear();
        _texts.Clear();
    }

    /// <summary>Text that is already localized or formatted (e.g. a label with a value in it), cached like a label.</summary>
    public FormattedText Text(string value, double size, Color color, bool bold = false)
    {
        if (_texts.TryGetValue((value, size, color, bold), out var text))
        {
            return text;
        }

        // Values change as the animation runs; keep only the recent ones.
        if (_texts.Count > 256)
        {
            _texts.Clear();
        }

        text = Format(value, LocalizationService.Instance.CurrentCulture, size, color, bold);
        _texts[(value, size, color, bold)] = text;
        return text;
    }

    /// <summary>Like <see cref="DrawPill"/>, for text already localized.</summary>
    public void DrawTextPill(DrawingContext dc, string value, Point topLeft, double size, Color color)
    {
        var text = Text(value, size, color);
        var rect = new Rect(topLeft.X, topLeft.Y, text.Width + 12, text.Height + 4);
        dc.DrawRectangle(PillBrush, null, rect, rect.Height / 2, rect.Height / 2);
        dc.DrawText(text, new Point(rect.X + 6, rect.Y + 2));
    }

    public FormattedText Get(string key, double size, Color color, bool bold = false)
    {
        if (_cache.TryGetValue((key, size, color, bold), out var text))
        {
            return text;
        }

        text = Format(LocalizationService.Instance.GetString(key), LocalizationService.Instance.CurrentCulture, size, color, bold);
        _cache[(key, size, color, bold)] = text;
        return text;
    }

    /// <summary>Plain (not localized) text such as an axis value.</summary>
    public FormattedText Plain(string text, double size, Color color) =>
        Format(text, CultureInfo.InvariantCulture, size, color, bold: false);

    /// <summary>Draws a label centred horizontally on <paramref name="anchor"/>, its top at the anchor.</summary>
    public void DrawCentered(DrawingContext dc, string key, Point anchor, double size, Color color, bool bold = false)
    {
        var text = Get(key, size, color, bold);
        dc.DrawText(text, new Point(anchor.X - (text.Width / 2), anchor.Y));
    }

    /// <summary>A label on a dark rounded pill, so it reads over a busy background.</summary>
    public void DrawPill(DrawingContext dc, string key, Point topLeft, double size, Color color)
    {
        var text = Get(key, size, color);
        var rect = new Rect(topLeft.X, topLeft.Y, text.Width + 12, text.Height + 4);
        dc.DrawRectangle(PillBrush, null, rect, rect.Height / 2, rect.Height / 2);
        dc.DrawText(text, new Point(rect.X + 6, rect.Y + 2));
    }

    private FormattedText Format(string value, CultureInfo culture, double size, Color color, bool bold)
    {
        var typeface = new Typeface(owner.GetValue(TextElement.FontFamilyProperty), FontStyle.Normal,
            bold ? FontWeight.SemiBold : FontWeight.Normal);
        return new FormattedText(value, culture, FlowDirection.LeftToRight, typeface, size, SpaceDrawing.Solid(color));
    }

    private static readonly IBrush PillBrush = SpaceDrawing.Solid(200, 8, 12, 22);
}
