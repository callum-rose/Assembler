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
	/// earlier ones where they overlap; a `cut` shape removes voxels instead of
	/// adding them), everything clipped to the declared window. Coordinates are
	/// grid cells (cell 0,0,0 sits at the part's declared offset); centres and
	/// radii may be fractional so even-sized shapes can be centred between cells.
	///
	/// Grammar (tokens whitespace-separated; `#` starts a comment):
	///   box      KEY minX minY minZ sizeX sizeY sizeZ [round R [SEL ...]]
	///   sphere   KEY cx cy cz r [half +x|-x|+y|-y|+z|-z]
	///   cylinder KEY x|y|z baseX baseY baseZ r h [half +x|-x|+y|-y|+z|-z]
	///   cut SHAPE ...   carves a shape out (same args as above minus KEY)
	///
	/// `cut` reuses any shape's geometry but drops every voxel it covers rather
	/// than filling it — applied in document order, so it removes whatever a
	/// prior line placed and a later line can fill back in.
	///
	/// `round R` with no selectors rounds all twelve edges. Each optional SEL
	/// targets the rounding: a face (`+y`) rounds that face's four edges, an
	/// edge (`+y+z`, two perpendicular face directions) rounds that one edge.
	/// Rounding both edges of a face that sit only `2R` voxels apart would
	/// collapse the shared face and shrink the box, so that combination is
	/// rejected — round at most one, grow the box, or shrink R.
	/// </summary>
	public static class PrimitivesCodec
	{
		private const float Epsilon = 1e-4f;

		// A `cut` line carries no palette key, so we splice this placeholder into
		// the key slot to reuse the shape parsers unchanged. It maps to colour 0
		// (empty) — a value real palette keys never take, since they are 1-based —
		// so the colour a cut "fills" with is harmless and ignored.
		private const char CutPlaceholderKey = '\0';

		public static Assembler.Voxels.VoxelModel Decode(PrimitivesPartData data, IReadOnlyList<PaletteEntry> palette)
		{
			var keyToIndex = new Dictionary<char, byte> { [CutPlaceholderKey] = 0 };
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

			// `cut SHAPE ...` carves the shape out instead of filling it. It has no
			// palette key, so drop the `cut` verb and splice a placeholder into the
			// key slot, leaving exactly the token layout the shape parsers expect.
			var cut = tokens[0].Equals("cut", StringComparison.OrdinalIgnoreCase);
			if (cut)
			{
				if (tokens.Length < 2)
				{
					throw Error(lineNumber, line, "'cut' must be followed by a shape (box, sphere, or cylinder)");
				}

				tokens = tokens.Skip(1).Take(1)
					.Append(CutPlaceholderKey.ToString())
					.Concat(tokens.Skip(2))
					.ToArray();
			}

			var shape = tokens[0].ToLowerInvariant();
			var (predicate, boundsMin, boundsMax, colour) = shape switch
			{
				"box" => ParseBox(tokens, lineNumber, keyToIndex),
				"sphere" => ParseSphere(tokens, lineNumber, keyToIndex),
				"cylinder" => ParseCylinder(tokens, lineNumber, keyToIndex),
				_ => throw Error(lineNumber, line, $"unknown shape '{tokens[0]}' (expected box, sphere, or cylinder, optionally prefixed with 'cut')"),
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
						if (!predicate(cell))
						{
							continue;
						}

						if (cut)
						{
							voxels.Remove(cell);
						}
						else
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
			if (Array.FindIndex(tokens, t => t.Equals("half", StringComparison.OrdinalIgnoreCase)) >= 0)
			{
				throw Error(lineNumber, tokens, "box does not take a 'half' clip — use sphere or cylinder");
			}

			var colour = ColourOf(tokens, lineNumber, keyToIndex);

			// box KEY <6 numbers> [round R selectors...]
			var args = tokens.Skip(2).ToArray();
			var roundIndex = Array.FindIndex(args, t => t.Equals("round", StringComparison.OrdinalIgnoreCase));
			var numberTokens = roundIndex >= 0 ? args.Take(roundIndex).ToArray() : args;
			var roundTokens = roundIndex >= 0 ? args.Skip(roundIndex + 1).ToArray() : Array.Empty<string>();

			if (numberTokens.Length != 6)
			{
				throw Error(lineNumber, tokens, "box needs 6 numbers: minX minY minZ sizeX sizeY sizeZ (then optional 'round R [faces/edges]')");
			}

			var numbers = numberTokens.Select(t => RequireInt(ParseNumber(t, lineNumber, tokens), lineNumber, tokens)).ToArray();
			var boxMin = new Vector3Int(numbers[0], numbers[1], numbers[2]);
			var boxSize = new Vector3Int(numbers[3], numbers[4], numbers[5]);
			if (boxSize.x <= 0 || boxSize.y <= 0 || boxSize.z <= 0)
			{
				throw Error(lineNumber, tokens, "box size must be positive on every axis");
			}

			var boxMax = boxMin + boxSize - Vector3Int.one;
			if (roundIndex < 0)
			{
				return (_ => true, boxMin, boxMax, colour);
			}

			if (roundTokens.Length == 0)
			{
				throw Error(lineNumber, tokens, "'round' must be followed by a radius R (then optional faces/edges to round)");
			}

			var round = ParseNumber(roundTokens[0], lineNumber, tokens);
			if (round <= 0f)
			{
				return (_ => true, boxMin, boxMax, colour);
			}

			var selectors = roundTokens.Skip(1).ToArray();
			var edges = selectors.Length == 0 ? AllEdges() : ParseRoundSelectors(selectors, lineNumber, tokens);
			GuardAgainstFaceCollapse(edges, boxSize, round, lineNumber, tokens);

			return (RoundedBoxPredicate(edges, boxMin, boxMax, round), boxMin, boxMax, colour);
		}

		/// <summary>
		/// Rounding carves a quarter-round along each selected edge: a cell is
		/// dropped when it lies beyond the inset point on both of the edge's
		/// faces and outside the radius-R quarter circle between them. The union
		/// over the selected edges gives per-edge / per-face rounding; rounding
		/// all twelve edges reproduces a uniformly rounded box.
		/// </summary>
		private static Func<Vector3Int, bool> RoundedBoxPredicate(
			IReadOnlyCollection<(int, int)> edges, Vector3Int boxMin, Vector3Int boxMax, float round)
		{
			var centre = new Vector3(
				(boxMin.x + boxMax.x) / 2f, (boxMin.y + boxMax.y) / 2f, (boxMin.z + boxMax.z) / 2f);
			var radiusSq = (round + Epsilon) * (round + Epsilon);
			var carves = edges.Select(edge =>
			{
				var (axisU, positiveU) = Direction(edge.Item1);
				var (axisV, positiveV) = Direction(edge.Item2);
				// Inset point on each face, clamped to the centre so a large R
				// never carves past the half-way plane.
				var insetU = positiveU ? Mathf.Max(boxMax[axisU] - round, centre[axisU]) : Mathf.Min(boxMin[axisU] + round, centre[axisU]);
				var insetV = positiveV ? Mathf.Max(boxMax[axisV] - round, centre[axisV]) : Mathf.Min(boxMin[axisV] + round, centre[axisV]);
				return (axisU, positiveU, insetU, axisV, positiveV, insetV);
			}).ToArray();

			return cell =>
			{
				foreach (var (axisU, positiveU, insetU, axisV, positiveV, insetV) in carves)
				{
					var du = cell[axisU] - insetU;
					var dv = cell[axisV] - insetV;
					var beyondU = positiveU ? du > 0f : du < 0f;
					var beyondV = positiveV ? dv > 0f : dv < 0f;
					if (beyondU && beyondV && du * du + dv * dv > radiusSq)
					{
						return false;
					}
				}

				return true;
			};
		}

		/// <summary>
		/// Rejects rounding two edges of a face that are so close their carvings
		/// meet — that would erase the strip between them and shrink the box by a
		/// voxel along the shared face's normal. For each face, the two edges
		/// parallel to one in-plane axis sit `size` cells apart along the other;
		/// if both are rounded and that span is two voxels (more generally
		/// `≤ 2R`), the face collapses. A span of one means the two edges are the
		/// same line, which rounds cleanly, so it is left alone.
		/// </summary>
		private static void GuardAgainstFaceCollapse(
			HashSet<(int, int)> edges, Vector3Int size, float round, int lineNumber, string[] tokens)
		{
			var names = new[] { "x", "y", "z" };
			for (var faceAxis = 0; faceAxis < 3; faceAxis++)
			{
				foreach (var facePositive in new[] { false, true })
				{
					for (var sepAxis = 0; sepAxis < 3; sepAxis++)
					{
						if (sepAxis == faceAxis || size[sepAxis] < 2 || size[sepAxis] > 2f * round)
						{
							continue;
						}

						var edgeLow = Edge(DirectionCode(faceAxis, facePositive), DirectionCode(sepAxis, false));
						var edgeHigh = Edge(DirectionCode(faceAxis, facePositive), DirectionCode(sepAxis, true));
						if (edges.Contains(edgeLow) && edges.Contains(edgeHigh))
						{
							var face = (facePositive ? "+" : "-") + names[faceAxis];
							throw Error(lineNumber, tokens,
								$"rounding both edges of the {face} face that are only {size[sepAxis]} voxels apart (along {names[sepAxis]}) would collapse it and shrink the box — round at most one of them, make the box thicker than {2f * round} along {names[sepAxis]}, or reduce R");
						}
					}
				}
			}
		}

		private static HashSet<(int, int)> ParseRoundSelectors(string[] selectors, int lineNumber, string[] tokens)
		{
			var edges = new HashSet<(int, int)>();
			foreach (var selector in selectors)
			{
				var directions = SplitDirections(selector, lineNumber, tokens);
				if (directions.Count == 1)
				{
					var (faceAxis, facePositive) = directions[0];
					for (var other = 0; other < 3; other++)
					{
						if (other == faceAxis)
						{
							continue;
						}

						edges.Add(Edge(DirectionCode(faceAxis, facePositive), DirectionCode(other, false)));
						edges.Add(Edge(DirectionCode(faceAxis, facePositive), DirectionCode(other, true)));
					}
				}
				else
				{
					edges.Add(Edge(DirectionCode(directions[0].Axis, directions[0].Positive), DirectionCode(directions[1].Axis, directions[1].Positive)));
				}
			}

			return edges;
		}

		private static List<(int Axis, bool Positive)> SplitDirections(string selector, int lineNumber, string[] tokens)
		{
			FormatException Invalid() => Error(lineNumber, tokens, $"'{selector}' is not a face (e.g. +y) or edge (e.g. +y+z) to round");

			var directions = new List<(int, bool)>();
			for (var i = 0; i < selector.Length; i += 2)
			{
				if (i + 2 > selector.Length || ParseDirection(selector.Substring(i, 2)) is not { } direction)
				{
					throw Invalid();
				}

				directions.Add(direction);
			}

			if (directions.Count is < 1 or > 2)
			{
				throw Invalid();
			}

			if (directions.Count == 2 && directions[0].Item1 == directions[1].Item1)
			{
				throw Error(lineNumber, tokens, $"edge selector '{selector}' must name two perpendicular faces (e.g. +y+z), not two on the {new[] { "x", "y", "z" }[directions[0].Item1]} axis");
			}

			return directions;
		}

		private static HashSet<(int, int)> AllEdges()
		{
			var edges = new HashSet<(int, int)>();
			for (var a = 0; a < 3; a++)
			{
				for (var b = a + 1; b < 3; b++)
				{
					foreach (var positiveA in new[] { false, true })
					{
						foreach (var positiveB in new[] { false, true })
						{
							edges.Add(Edge(DirectionCode(a, positiveA), DirectionCode(b, positiveB)));
						}
					}
				}
			}

			return edges;
		}

		private static (int Axis, bool Positive)? ParseDirection(string token) => token.ToLowerInvariant() switch
		{
			"+x" => (0, true),
			"-x" => (0, false),
			"+y" => (1, true),
			"-y" => (1, false),
			"+z" => (2, true),
			"-z" => (2, false),
			_ => null,
		};

		private static int DirectionCode(int axis, bool positive) => axis * 2 + (positive ? 1 : 0);

		private static (int Axis, bool Positive) Direction(int code) => (code / 2, code % 2 == 1);

		private static (int, int) Edge(int first, int second) => first < second ? (first, second) : (second, first);

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
		/// `half ±axis` clip (sphere / cylinder only).
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

				numbers.Add(ParseNumber(token, lineNumber, tokens));
			}

			return (numbers, half);
		}

		private static float ParseNumber(string token, int lineNumber, string[] tokens) =>
			float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
				? value
				: throw Error(lineNumber, tokens, $"'{token}' is not a number");

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
