using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Rasterizes the declarative primitives part encoding into a part-local
	/// Y-up grid. One shape per line, applied in order (later shapes overwrite
	/// earlier ones where they overlap), everything clipped to the declared
	/// window. Coordinates are grid cells (cell 0,0,0 sits at the part's
	/// declared offset); centres and radii may be fractional so even-sized
	/// shapes can be centred between cells.
	///
	/// Grammar (tokens whitespace-separated; `#` starts a comment):
	///   box      KEY minX minY minZ sizeX sizeY sizeZ [round R]
	///   sphere   KEY cx cy cz r [half +x|-x|+y|-y|+z|-z]
	///   cylinder KEY x|y|z baseX baseY baseZ r h [half +x|-x|+y|-y|+z|-z]
	/// </summary>
	public static class PrimitivesCodec
	{
		private const float Epsilon = 1e-4f;

		public static Assembler.Voxels.VoxelModel Decode(PrimitivesPartData data, IReadOnlyList<PaletteEntry> palette)
		{
			var keyToIndex = new Dictionary<char, byte>();
			for (var i = 0; i < palette.Count; i++)
			{
				keyToIndex[palette[i].Key] = (byte)(i + 1);
			}

			var voxels = new Dictionary<Vector3Int, byte>();
			var lineNumber = 0;
			var shapes = 0;
			foreach (var raw in data.Shapes)
			{
				lineNumber++;
				var line = StripComment(raw).Trim();
				if (line.Length == 0)
				{
					continue;
				}

				ApplyShape(line, lineNumber, data.Size, keyToIndex, voxels);
				shapes++;
			}

			if (shapes == 0)
			{
				throw new FormatException("The primitives block declares no shapes.");
			}

			return LayersCodec.ToModel(
				voxels.ToDictionary(kv => kv.Key + data.Offset, kv => kv.Value), palette);
		}

		private static void ApplyShape(
			string line,
			int lineNumber,
			Vector3Int size,
			IReadOnlyDictionary<char, byte> keyToIndex,
			Dictionary<Vector3Int, byte> voxels)
		{
			var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
			var shape = tokens[0].ToLowerInvariant();
			var (predicate, boundsMin, boundsMax, colour) = shape switch
			{
				"box" => ParseBox(tokens, lineNumber, keyToIndex),
				"sphere" => ParseSphere(tokens, lineNumber, keyToIndex),
				"cylinder" => ParseCylinder(tokens, lineNumber, keyToIndex),
				_ => throw Error(lineNumber, line, $"unknown shape '{tokens[0]}' (expected box, sphere, or cylinder)"),
			};

			var min = Vector3Int.Max(boundsMin, Vector3Int.zero);
			var max = Vector3Int.Min(boundsMax, size - Vector3Int.one);
			for (var x = min.x; x <= max.x; x++)
			{
				for (var y = min.y; y <= max.y; y++)
				{
					for (var z = min.z; z <= max.z; z++)
					{
						var cell = new Vector3Int(x, y, z);
						if (predicate(cell))
						{
							voxels[cell] = colour;
						}
					}
				}
			}
		}

		private static (Func<Vector3Int, bool>, Vector3Int, Vector3Int, byte) ParseBox(
			string[] tokens, int lineNumber, IReadOnlyDictionary<char, byte> keyToIndex)
		{
			var (numbers, half) = SplitArguments(tokens, lineNumber);
			if (half != null)
			{
				throw Error(lineNumber, tokens, "box does not take a 'half' clip — use sphere or cylinder");
			}

			var round = 0f;
			var roundIndex = Array.FindIndex(tokens, t => t.Equals("round", StringComparison.OrdinalIgnoreCase));
			if (roundIndex >= 0)
			{
				if (roundIndex != tokens.Length - 2 || numbers.Count == 0)
				{
					throw Error(lineNumber, tokens, "'round R' must be the final two tokens of the box line");
				}

				round = numbers[^1];
				numbers = numbers.Take(numbers.Count - 1).ToList();
			}

			if (numbers.Count != 6)
			{
				throw Error(lineNumber, tokens, "box needs 6 numbers: minX minY minZ sizeX sizeY sizeZ (then optional 'round R')");
			}

			var boxMin = new Vector3Int(RequireInt(numbers[0], lineNumber, tokens), RequireInt(numbers[1], lineNumber, tokens), RequireInt(numbers[2], lineNumber, tokens));
			var boxSize = new Vector3Int(RequireInt(numbers[3], lineNumber, tokens), RequireInt(numbers[4], lineNumber, tokens), RequireInt(numbers[5], lineNumber, tokens));
			if (boxSize.x <= 0 || boxSize.y <= 0 || boxSize.z <= 0)
			{
				throw Error(lineNumber, tokens, "box size must be positive on every axis");
			}

			var boxMax = boxMin + boxSize - Vector3Int.one;
			var colour = ColourOf(tokens, lineNumber, keyToIndex);
			if (round <= 0f)
			{
				return (_ => true, boxMin, boxMax, colour);
			}

			// Rounded box: cells within `round` of the inset core box. Insets
			// collapse to the centre plane when the radius exceeds an extent.
			var innerMin = new Vector3(
				Mathf.Min(boxMin.x + round, (boxMin.x + boxMax.x) / 2f),
				Mathf.Min(boxMin.y + round, (boxMin.y + boxMax.y) / 2f),
				Mathf.Min(boxMin.z + round, (boxMin.z + boxMax.z) / 2f));
			var innerMax = new Vector3(
				Mathf.Max(boxMax.x - round, (boxMin.x + boxMax.x) / 2f),
				Mathf.Max(boxMax.y - round, (boxMin.y + boxMax.y) / 2f),
				Mathf.Max(boxMax.z - round, (boxMin.z + boxMax.z) / 2f));
			var radiusSq = (round + Epsilon) * (round + Epsilon);
			return (cell =>
			{
				var clamped = Vector3.Max(innerMin, Vector3.Min(innerMax, cell));
				return ((Vector3)cell - clamped).sqrMagnitude <= radiusSq;
			}, boxMin, boxMax, colour);
		}

		private static (Func<Vector3Int, bool>, Vector3Int, Vector3Int, byte) ParseSphere(
			string[] tokens, int lineNumber, IReadOnlyDictionary<char, byte> keyToIndex)
		{
			var (numbers, half) = SplitArguments(tokens, lineNumber);
			if (numbers.Count != 4)
			{
				throw Error(lineNumber, tokens, "sphere needs 4 numbers: cx cy cz r (then optional 'half +y' etc.)");
			}

			var centre = new Vector3(numbers[0], numbers[1], numbers[2]);
			var radius = numbers[3];
			if (radius <= 0f)
			{
				throw Error(lineNumber, tokens, "sphere radius must be positive");
			}

			var radiusSq = (radius + Epsilon) * (radius + Epsilon);
			var clip = HalfPredicate(half, centre);
			return (cell => ((Vector3)cell - centre).sqrMagnitude <= radiusSq && clip(cell),
				FloorVector(centre - radius * Vector3.one),
				CeilVector(centre + radius * Vector3.one),
				ColourOf(tokens, lineNumber, keyToIndex));
		}

		private static (Func<Vector3Int, bool>, Vector3Int, Vector3Int, byte) ParseCylinder(
			string[] tokens, int lineNumber, IReadOnlyDictionary<char, byte> keyToIndex)
		{
			if (tokens.Length < 3)
			{
				throw Error(lineNumber, tokens, "cylinder needs an axis: cylinder KEY x|y|z baseX baseY baseZ r h");
			}

			var axis = tokens[2].ToLowerInvariant() switch
			{
				"x" => 0,
				"y" => 1,
				"z" => 2,
				var other => throw Error(lineNumber, tokens, $"unknown cylinder axis '{other}' (expected x, y, or z)"),
			};

			var (numbers, half) = SplitArguments(tokens, lineNumber, skip: 3);
			if (numbers.Count != 5)
			{
				throw Error(lineNumber, tokens, "cylinder needs 5 numbers after the axis: baseX baseY baseZ r h (then optional 'half +z' etc.)");
			}

			var base_ = new Vector3(numbers[0], numbers[1], numbers[2]);
			var radius = numbers[3];
			var height = RequireInt(numbers[4], lineNumber, tokens);
			if (radius <= 0f || height <= 0)
			{
				throw Error(lineNumber, tokens, "cylinder radius and height must be positive");
			}

			var (u, v) = axis switch { 0 => (1, 2), 1 => (0, 2), _ => (0, 1) };
			var radiusSq = (radius + Epsilon) * (radius + Epsilon);
			var clip = HalfPredicate(half, base_);
			bool Predicate(Vector3Int cell)
			{
				if (cell[axis] < base_[axis] - Epsilon || cell[axis] > base_[axis] + height - 1 + Epsilon)
				{
					return false;
				}

				var du = cell[u] - base_[u];
				var dv = cell[v] - base_[v];
				return du * du + dv * dv <= radiusSq && clip(cell);
			}

			var min = base_ - radius * Vector3.one;
			var max = base_ + radius * Vector3.one;
			min[axis] = base_[axis];
			max[axis] = base_[axis] + height - 1;
			return (Predicate, FloorVector(min), CeilVector(max), ColourOf(tokens, lineNumber, keyToIndex));
		}

		/// <summary>
		/// Numeric arguments after the colour key, plus the optional trailing
		/// `half ±axis` clip. The `round` keyword is left in place for box to
		/// pick up positionally (its number lands at the end of the list).
		/// </summary>
		private static (List<float> Numbers, string? Half) SplitArguments(string[] tokens, int lineNumber, int skip = 2)
		{
			var numbers = new List<float>();
			string? half = null;
			for (var i = skip; i < tokens.Length; i++)
			{
				var token = tokens[i];
				if (token.Equals("half", StringComparison.OrdinalIgnoreCase))
				{
					half = i + 1 < tokens.Length
						? tokens[++i]
						: throw Error(lineNumber, tokens, "'half' must be followed by +x, -x, +y, -y, +z, or -z");
					continue;
				}

				if (token.Equals("round", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
				{
					throw Error(lineNumber, tokens, $"'{token}' is not a number");
				}

				numbers.Add(value);
			}

			return (numbers, half);
		}

		private static Func<Vector3Int, bool> HalfPredicate(string? half, Vector3 reference)
		{
			if (half == null)
			{
				return _ => true;
			}

			var (axis, positive) = half.ToLowerInvariant() switch
			{
				"+x" => (0, true),
				"-x" => (0, false),
				"+y" => (1, true),
				"-y" => (1, false),
				"+z" => (2, true),
				"-z" => (2, false),
				var other => throw new FormatException($"Unknown half direction '{other}' (expected +x, -x, +y, -y, +z, or -z)."),
			};

			return positive
				? cell => cell[axis] >= reference[axis] - Epsilon
				: cell => cell[axis] <= reference[axis] + Epsilon;
		}

		private static byte ColourOf(string[] tokens, int lineNumber, IReadOnlyDictionary<char, byte> keyToIndex)
		{
			if (tokens.Length < 2 || tokens[1].Length != 1)
			{
				throw Error(lineNumber, tokens, "the second token must be a single-character palette key");
			}

			return keyToIndex.TryGetValue(tokens[1][0], out var index)
				? index
				: throw Error(lineNumber, tokens, $"'{tokens[1]}' is not a declared palette key");
		}

		private static int RequireInt(float value, int lineNumber, string[] tokens) =>
			Mathf.Abs(value - Mathf.Round(value)) < Epsilon
				? Mathf.RoundToInt(value)
				: throw Error(lineNumber, tokens, $"'{value}' must be a whole number (only centres and radii may be fractional)");

		private static string StripComment(string line)
		{
			for (var i = 0; i < line.Length; i++)
			{
				if (line[i] == '#' && (i == 0 || char.IsWhiteSpace(line[i - 1])))
				{
					return line[..i];
				}
			}

			return line;
		}

		private static Vector3Int FloorVector(Vector3 v) =>
			new(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y), Mathf.FloorToInt(v.z));

		private static Vector3Int CeilVector(Vector3 v) =>
			new(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y), Mathf.CeilToInt(v.z));

		private static FormatException Error(int lineNumber, string[] tokens, string message) =>
			Error(lineNumber, string.Join(" ", tokens), message);

		private static FormatException Error(int lineNumber, string line, string message) =>
			new($"Shape line {lineNumber} ('{line}'): {message}.");
	}
}
