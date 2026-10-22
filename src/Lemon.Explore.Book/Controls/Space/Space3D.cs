namespace Lemon.Explore.Book.Controls.Space;

/// <summary>
/// A point in space, for the diagrams that project a small 3-D scene themselves: the part of WPF's Media3D.Point3D they
/// use (Avalonia has no 3-D types).
/// </summary>
public readonly record struct Point3D(double X, double Y, double Z)
{
    public static Point3D operator +(Point3D p, Vector3D v) => new(p.X + v.X, p.Y + v.Y, p.Z + v.Z);

    public static Point3D operator -(Point3D p, Vector3D v) => new(p.X - v.X, p.Y - v.Y, p.Z - v.Z);

    public static Vector3D operator -(Point3D a, Point3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>The point as the vector to it from the origin.</summary>
    public static explicit operator Vector3D(Point3D p) => new(p.X, p.Y, p.Z);
}

/// <summary>A direction and length in space: the part of WPF's Media3D.Vector3D the diagrams use.</summary>
public readonly record struct Vector3D(double X, double Y, double Z)
{
    public double Length => Math.Sqrt(SquaredLength);

    public double SquaredLength => (X * X) + (Y * Y) + (Z * Z);

    /// <summary>The same direction, length 1. Unlike WPF's, it returns the new vector and leaves this one as it is.</summary>
    public Vector3D Normalize() => this / Length;

    public static Vector3D operator +(Vector3D a, Vector3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3D operator -(Vector3D a, Vector3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3D operator -(Vector3D v) => new(-v.X, -v.Y, -v.Z);

    public static Vector3D operator *(Vector3D v, double s) => new(v.X * s, v.Y * s, v.Z * s);

    public static Vector3D operator *(double s, Vector3D v) => v * s;

    public static Vector3D operator /(Vector3D v, double s) => new(v.X / s, v.Y / s, v.Z / s);

    public static double DotProduct(Vector3D a, Vector3D b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    public static Vector3D CrossProduct(Vector3D a, Vector3D b) =>
        new((a.Y * b.Z) - (a.Z * b.Y), (a.Z * b.X) - (a.X * b.Z), (a.X * b.Y) - (a.Y * b.X));
}
