using System;

namespace Assembler.Parsing.Phase2.Parsing.Phase2
{
	public readonly struct Vector2 : IEquatable<Vector2>
	{
		public float X { get; init; }
		public float Y { get; init; }

		public Vector2(float x, float y)
		{
			X = x;
			Y = y;
		}

		public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);

		public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);

		public static Vector2 operator *(Vector2 a, float b) => new(a.X * b, a.Y * b);

		public bool Equals(Vector2 other) => X.Equals(other.X) && Y.Equals(other.Y);

		public override bool Equals(object? obj) => obj is Vector2 other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(X, Y);

		public override string ToString() => $"({X}, {Y})";
	}

	public readonly struct Vector3 : IEquatable<Vector3>
	{

		public float X { get; init; }
		public float Y { get; init; }
		public float Z { get; init; }

		public Vector3(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

		public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

		public static Vector3 operator *(Vector3 a, float b) => new(a.X * b, a.Y * b, a.Z * b);

		public bool Equals(Vector3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

		public override bool Equals(object? obj) => obj is Vector3 other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(X, Y, Z);

		public override string ToString() => $"({X}, {Y}, {Z})";
	}
}