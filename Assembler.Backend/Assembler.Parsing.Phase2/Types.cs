namespace Assembler.Parsing2;

public readonly struct Vector2(float x, float y) : IEquatable<Vector2>
{
	public float X { get; init; } = x;
	public float Y { get; init; } = y;

	public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
	public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
	public static Vector2 operator *(Vector2 a, float b) => new(a.X * b, a.Y * b);

	public bool Equals(Vector2 other) => X.Equals(other.X) && Y.Equals(other.Y);
	public override bool Equals(object? obj) => obj is Vector2 other && Equals(other);
	public override int GetHashCode() => HashCode.Combine(X, Y);
	public override string ToString() => $"({X}, {Y})";
}

public readonly struct Vector3(float x, float y, float z) : IEquatable<Vector3>
{
	public float X { get; init; } = x;
	public float Y { get; init; } = y;
	public float Z { get; init; } = z;

	public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
	public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
	public static Vector3 operator *(Vector3 a, float b) => new(a.X * b, a.Y * b, a.Z * b);

	public bool Equals(Vector3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
	public override bool Equals(object? obj) => obj is Vector3 other && Equals(other);
	public override int GetHashCode() => HashCode.Combine(X, Y, Z);
	public override string ToString() => $"({X}, {Y}, {Z})";
}