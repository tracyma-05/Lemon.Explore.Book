using Avalonia;
using Avalonia.Media;

namespace Lemon.Explore.Book.Controls.Space;

/// <summary>
/// A WPF-style face on Avalonia's <see cref="DrawingContext"/>: <c>Push…</c> calls are undone by <see cref="Pop"/>,
/// in reverse order, instead of by disposing what they return. The diagrams ported from WPF push and pop across
/// branches and loops; keeping that shape keeps their code line for line comparable with the original.
/// </summary>
/// <remarks>Dispose it at the end of a render to pop anything still pushed.</remarks>
internal sealed class PaintContext(DrawingContext context) : IDisposable
{
    private readonly Stack<DrawingContext.PushedState> _pushed = new();

    public DrawingContext Context { get; } = context;

    public static implicit operator DrawingContext(PaintContext paint) => paint.Context;

    public void PushOpacity(double opacity) => _pushed.Push(Context.PushOpacity(opacity));

    public void PushClip(Rect rect) => _pushed.Push(Context.PushClip(rect));

    public void PushClip(Geometry geometry) => _pushed.Push(Context.PushGeometryClip(geometry));

    public void PushTransform(Matrix matrix) => _pushed.Push(Context.PushTransform(matrix));

    public void PushTransform(Transform transform) => _pushed.Push(Context.PushTransform(transform.Value));

    public void Pop() => _pushed.Pop().Dispose();

    public void DrawRectangle(IBrush? brush, IPen? pen, Rect rect) => Context.DrawRectangle(brush, pen, rect);

    public void DrawRoundedRectangle(IBrush? brush, IPen? pen, Rect rect, double radiusX, double radiusY) =>
        Context.DrawRectangle(brush, pen, rect, radiusX, radiusY);

    public void DrawEllipse(IBrush? brush, IPen? pen, Point center, double radiusX, double radiusY) =>
        Context.DrawEllipse(brush, pen, center, radiusX, radiusY);

    public void DrawLine(IPen pen, Point from, Point to) => Context.DrawLine(pen, from, to);

    public void DrawGeometry(IBrush? brush, IPen? pen, Geometry geometry) => Context.DrawGeometry(brush, pen, geometry);

    public void DrawText(FormattedText text, Point origin) => Context.DrawText(text, origin);

    public void DrawImage(IImage? image, Rect destination)
    {
        if (image is not null)
        {
            Context.DrawImage(image, destination);
        }
    }

    public void Dispose()
    {
        while (_pushed.Count > 0)
        {
            Pop();
        }
    }
}
