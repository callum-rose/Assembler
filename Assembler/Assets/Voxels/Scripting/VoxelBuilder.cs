using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using UnityEngine;

namespace Assembler.Voxels.Scripting
{
	/// <summary>
	/// Host API a procedural voxel script builds against. A script body receives
	/// an instance as <c>b</c>, places voxels via the methods below, and returns
	/// <c>b.Build()</c>. Coordinates are integers in final pipeline space
	/// (Z-up, == GoxelTextZUp), so a model built here serialises straight to
	/// goxel text with no axis swap.
	///
	/// All methods have distinct names and a single signature (the expression
	/// compiler resolves overloads by exact argument type, so colours come from
	/// the <c>Rgb</c>/<c>Hex</c>/<c>Hsv</c> helpers and coordinates are always
	/// <c>int</c>). Mutating methods funnel through a single internal setter that
	/// enforces the voxel-count cap and wall-clock budget from
	/// <see cref="VoxelScriptLimits"/>.
	/// </summary>
	public sealed class VoxelBuilder
	{
		private readonly Dictionary<Vector3Int, Color32> _voxels = new();
		private readonly VoxelScriptLimits _limits;
		private readonly CancellationToken _ct;
		private readonly System.Diagnostics.Stopwatch _stopwatch;
		private int _opsSinceDeadlineCheck;

		public VoxelBuilder() : this(VoxelScriptLimits.Default)
		{
		}

		public VoxelBuilder(VoxelScriptLimits limits, CancellationToken ct = default)
		{
			_limits = limits ?? VoxelScriptLimits.Default;
			_ct = ct;
			_stopwatch = System.Diagnostics.Stopwatch.StartNew();
		}

		// ---- Placement -----------------------------------------------------

		public void Set(int x, int y, int z, Color32 c) => SetInternal(x, y, z, c);

		public void Clear(int x, int y, int z) => _voxels.Remove(new Vector3Int(x, y, z));

		public Color32 Get(int x, int y, int z)
			=> _voxels.TryGetValue(new Vector3Int(x, y, z), out var c) ? c : new Color32(0, 0, 0, 0);

		public bool Has(int x, int y, int z) => _voxels.ContainsKey(new Vector3Int(x, y, z));

		public int Count => _voxels.Count;

		// ---- Solids --------------------------------------------------------

		public void Box(int x0, int y0, int z0, int x1, int y1, int z1, Color32 c)
		{
			Order(ref x0, ref x1);
			Order(ref y0, ref y1);
			Order(ref z0, ref z1);
			for (var x = x0; x <= x1; x++)
			{
				for (var y = y0; y <= y1; y++)
				{
					for (var z = z0; z <= z1; z++)
					{
						SetInternal(x, y, z, c);
					}
				}
			}
		}

		public void HollowBox(int x0, int y0, int z0, int x1, int y1, int z1, Color32 c)
		{
			Order(ref x0, ref x1);
			Order(ref y0, ref y1);
			Order(ref z0, ref z1);
			for (var x = x0; x <= x1; x++)
			{
				for (var y = y0; y <= y1; y++)
				{
					for (var z = z0; z <= z1; z++)
					{
						var onShell = x == x0 || x == x1 || y == y0 || y == y1 || z == z0 || z == z1;
						if (onShell)
						{
							SetInternal(x, y, z, c);
						}
					}
				}
			}
		}

		public void Sphere(int cx, int cy, int cz, int r, Color32 c)
			=> Ellipsoid(cx, cy, cz, r, r, r, c);

		public void Ellipsoid(int cx, int cy, int cz, int rx, int ry, int rz, Color32 c)
		{
			if (rx < 0 || ry < 0 || rz < 0)
			{
				return;
			}

			for (var x = -rx; x <= rx; x++)
			{
				for (var y = -ry; y <= ry; y++)
				{
					for (var z = -rz; z <= rz; z++)
					{
						var fx = rx == 0 ? 0f : (float)x / (rx + 0.5f);
						var fy = ry == 0 ? 0f : (float)y / (ry + 0.5f);
						var fz = rz == 0 ? 0f : (float)z / (rz + 0.5f);
						if (fx * fx + fy * fy + fz * fz <= 1f)
						{
							SetInternal(cx + x, cy + y, cz + z, c);
						}
					}
				}
			}
		}

		/// <summary>
		/// Solid cylinder of the given radius and height. The cylinder is centred
		/// on (cx, cy, cz) in the plane perpendicular to <paramref name="axis"/>
		/// and extends <paramref name="height"/> cells along that axis, centred on
		/// the centre cell.
		/// </summary>
		public void Cylinder(int cx, int cy, int cz, int radius, int height, VoxelAxis axis, Color32 c)
		{
			if (radius < 0 || height <= 0)
			{
				return;
			}

			var half = (height - 1) / 2;
			for (var h = -half; h <= height - 1 - half; h++)
			{
				for (var u = -radius; u <= radius; u++)
				{
					for (var v = -radius; v <= radius; v++)
					{
						if (u * u + v * v > radius * radius)
						{
							continue;
						}

						PlacePlanar(axis, cx, cy, cz, h, u, v, c);
					}
				}
			}
		}

		/// <summary>
		/// Cone with its base of <paramref name="radius"/> at the negative end and
		/// its apex at the positive end along <paramref name="axis"/>, centred on
		/// (cx, cy, cz) and <paramref name="height"/> cells tall.
		/// </summary>
		public void Cone(int cx, int cy, int cz, int radius, int height, VoxelAxis axis, Color32 c)
		{
			if (radius < 0 || height <= 0)
			{
				return;
			}

			var half = (height - 1) / 2;
			for (var i = 0; i < height; i++)
			{
				var h = i - half;
				var t = 1f - (float)i / Mathf.Max(1, height - 1);
				var ringRadius = Mathf.RoundToInt(radius * t);
				for (var u = -ringRadius; u <= ringRadius; u++)
				{
					for (var v = -ringRadius; v <= ringRadius; v++)
					{
						if (u * u + v * v > ringRadius * ringRadius)
						{
							continue;
						}

						PlacePlanar(axis, cx, cy, cz, h, u, v, c);
					}
				}
			}
		}

		public void Torus(int cx, int cy, int cz, int majorR, int minorR, VoxelAxis axis, Color32 c)
		{
			if (majorR < 0 || minorR < 0)
			{
				return;
			}

			var extent = majorR + minorR;
			for (var h = -minorR; h <= minorR; h++)
			{
				for (var u = -extent; u <= extent; u++)
				{
					for (var v = -extent; v <= extent; v++)
					{
						var ring = Mathf.Sqrt(u * u + v * v) - majorR;
						if (ring * ring + h * h <= minorR * minorR)
						{
							PlacePlanar(axis, cx, cy, cz, h, u, v, c);
						}
					}
				}
			}
		}

		// ---- Lines / planes ------------------------------------------------

		public void Line(int x0, int y0, int z0, int x1, int y1, int z1, Color32 c)
		{
			var dx = Math.Abs(x1 - x0);
			var dy = Math.Abs(y1 - y0);
			var dz = Math.Abs(z1 - z0);
			var sx = x0 < x1 ? 1 : -1;
			var sy = y0 < y1 ? 1 : -1;
			var sz = z0 < z1 ? 1 : -1;

			var x = x0;
			var y = y0;
			var z = z0;

			if (dx >= dy && dx >= dz)
			{
				var p1 = 2 * dy - dx;
				var p2 = 2 * dz - dx;
				while (true)
				{
					SetInternal(x, y, z, c);
					if (x == x1)
					{
						break;
					}

					if (p1 >= 0) { y += sy; p1 -= 2 * dx; }
					if (p2 >= 0) { z += sz; p2 -= 2 * dx; }
					p1 += 2 * dy;
					p2 += 2 * dz;
					x += sx;
				}
			}
			else if (dy >= dx && dy >= dz)
			{
				var p1 = 2 * dx - dy;
				var p2 = 2 * dz - dy;
				while (true)
				{
					SetInternal(x, y, z, c);
					if (y == y1)
					{
						break;
					}

					if (p1 >= 0) { x += sx; p1 -= 2 * dy; }
					if (p2 >= 0) { z += sz; p2 -= 2 * dy; }
					p1 += 2 * dx;
					p2 += 2 * dz;
					y += sy;
				}
			}
			else
			{
				var p1 = 2 * dy - dz;
				var p2 = 2 * dx - dz;
				while (true)
				{
					SetInternal(x, y, z, c);
					if (z == z1)
					{
						break;
					}

					if (p1 >= 0) { y += sy; p1 -= 2 * dz; }
					if (p2 >= 0) { x += sx; p2 -= 2 * dz; }
					p1 += 2 * dy;
					p2 += 2 * dx;
					z += sz;
				}
			}
		}

		/// <summary>
		/// Fills an axis-aligned rectangle on the plane perpendicular to
		/// <paramref name="axis"/> at coordinate <paramref name="plane"/>. The
		/// (u, v) range spans the other two axes in their natural order (for
		/// axis X: u=Y, v=Z; for Y: u=X, v=Z; for Z: u=X, v=Y).
		/// </summary>
		public void RectFill(VoxelAxis axis, int plane, int u0, int v0, int u1, int v1, Color32 c)
		{
			Order(ref u0, ref u1);
			Order(ref v0, ref v1);
			for (var u = u0; u <= u1; u++)
			{
				for (var v = v0; v <= v1; v++)
				{
					switch (axis)
					{
						case VoxelAxis.X:
							SetInternal(plane, u, v, c);
							break;
						case VoxelAxis.Y:
							SetInternal(u, plane, v, c);
							break;
						default:
							SetInternal(u, v, plane, c);
							break;
					}
				}
			}
		}

		// ---- Bulk transforms ----------------------------------------------

		/// <summary>
		/// Reflects every voxel across the centre plane of the current bounding
		/// box on <paramref name="axis"/>, keeping the originals. Build one half
		/// of a symmetric model, then mirror to complete it.
		/// </summary>
		public void Mirror(VoxelAxis axis)
		{
			if (_voxels.Count == 0)
			{
				return;
			}

			ComputeBounds(out var min, out var max);
			var sum = axis switch
			{
				VoxelAxis.X => min.x + max.x,
				VoxelAxis.Y => min.y + max.y,
				_ => min.z + max.z,
			};

			var snapshot = new List<KeyValuePair<Vector3Int, Color32>>(_voxels);
			foreach (var kv in snapshot)
			{
				var p = kv.Key;
				var mirrored = axis switch
				{
					VoxelAxis.X => new Vector3Int(sum - p.x, p.y, p.z),
					VoxelAxis.Y => new Vector3Int(p.x, sum - p.y, p.z),
					_ => new Vector3Int(p.x, p.y, sum - p.z),
				};
				SetInternal(mirrored.x, mirrored.y, mirrored.z, kv.Value);
			}
		}

		public void Translate(int dx, int dy, int dz)
		{
			if (dx == 0 && dy == 0 && dz == 0)
			{
				return;
			}

			var moved = new Dictionary<Vector3Int, Color32>(_voxels.Count);
			foreach (var kv in _voxels)
			{
				var p = kv.Key;
				moved[new Vector3Int(p.x + dx, p.y + dy, p.z + dz)] = kv.Value;
			}

			_voxels.Clear();
			foreach (var kv in moved)
			{
				_voxels[kv.Key] = kv.Value;
			}
		}

		/// <summary>
		/// Flood-fills connected empty cells with <paramref name="c"/>, starting
		/// from (x, y, z) and clamped to the current bounding box (so it always
		/// terminates). Use it to fill an enclosed cavity. No-op if the start
		/// cell is already occupied or the model is empty.
		/// </summary>
		public void Fill(int x, int y, int z, Color32 c)
		{
			if (_voxels.Count == 0 || Has(x, y, z))
			{
				return;
			}

			ComputeBounds(out var min, out var max);
			var start = new Vector3Int(x, y, z);
			if (start.x < min.x || start.x > max.x ||
				start.y < min.y || start.y > max.y ||
				start.z < min.z || start.z > max.z)
			{
				return;
			}

			var visited = new HashSet<Vector3Int>();
			var stack = new Stack<Vector3Int>();
			stack.Push(start);
			visited.Add(start);

			while (stack.Count > 0)
			{
				var p = stack.Pop();
				SetInternal(p.x, p.y, p.z, c);

				PushIfFillable(stack, visited, new Vector3Int(p.x + 1, p.y, p.z), min, max);
				PushIfFillable(stack, visited, new Vector3Int(p.x - 1, p.y, p.z), min, max);
				PushIfFillable(stack, visited, new Vector3Int(p.x, p.y + 1, p.z), min, max);
				PushIfFillable(stack, visited, new Vector3Int(p.x, p.y - 1, p.z), min, max);
				PushIfFillable(stack, visited, new Vector3Int(p.x, p.y, p.z + 1), min, max);
				PushIfFillable(stack, visited, new Vector3Int(p.x, p.y, p.z - 1), min, max);
			}
		}

		// ---- Colour helpers ------------------------------------------------

		public Color32 Rgb(int r, int g, int b)
			=> new((byte)Mathf.Clamp(r, 0, 255), (byte)Mathf.Clamp(g, 0, 255), (byte)Mathf.Clamp(b, 0, 255), 255);

		public Color32 Hex(string css)
		{
			var hex = css?.Trim() ?? string.Empty;
			if (hex.StartsWith("#", StringComparison.Ordinal))
			{
				hex = hex.Substring(1);
			}

			byte r = 0, g = 0, b = 0, a = 255;
			if (hex.Length == 6 || hex.Length == 8)
			{
				TryHexByte(hex, 0, ref r);
				TryHexByte(hex, 2, ref g);
				TryHexByte(hex, 4, ref b);
				if (hex.Length == 8)
				{
					TryHexByte(hex, 6, ref a);
				}
			}

			return new Color32(r, g, b, a);
		}

		/// <summary>HSV with hue in 0–360 and saturation/value in 0–100.</summary>
		public Color32 Hsv(int h, int s, int v)
		{
			var hue = Mathf.Repeat(h, 360f) / 360f;
			var sat = Mathf.Clamp(s, 0, 100) / 100f;
			var val = Mathf.Clamp(v, 0, 100) / 100f;
			var rgb = Color.HSVToRGB(hue, sat, val);
			return rgb;
		}

		// ---- Build ---------------------------------------------------------

		/// <summary>
		/// Snapshots the accumulated voxels into an immutable <see cref="VoxelModel"/>,
		/// deduping colours into a 1-based palette (mirroring
		/// <see cref="GoxelTextParser"/> so the round-trip is loss-free).
		/// </summary>
		public VoxelModel Build()
		{
			var paletteIndex = new Dictionary<Color32, byte>(new Color32Comparer());
			var palette = new List<Color32>();
			var voxels = new Dictionary<Vector3Int, byte>(_voxels.Count);

			Vector3Int min = new(int.MaxValue, int.MaxValue, int.MaxValue);
			Vector3Int max = new(int.MinValue, int.MinValue, int.MinValue);
			var hasAny = false;

			foreach (var kv in _voxels)
			{
				var colour = kv.Value;
				if (!paletteIndex.TryGetValue(colour, out var index))
				{
					if (palette.Count >= 255)
					{
						throw new VoxelScriptException(
							"Model uses more than 255 distinct colours, which exceeds the .vox palette limit.");
					}

					index = (byte)(palette.Count + 1);
					palette.Add(colour);
					paletteIndex[colour] = index;
				}

				voxels[kv.Key] = index;

				if (!hasAny)
				{
					min = max = kv.Key;
					hasAny = true;
				}
				else
				{
					min = Vector3Int.Min(min, kv.Key);
					max = Vector3Int.Max(max, kv.Key);
				}
			}

			if (!hasAny)
			{
				min = max = Vector3Int.zero;
			}

			return new VoxelModel(voxels, palette.ToArray(), min, max);
		}

		// ---- Internals -----------------------------------------------------

		private void SetInternal(int x, int y, int z, Color32 c)
		{
			var p = new Vector3Int(x, y, z);
			if (!_voxels.ContainsKey(p) && _voxels.Count >= _limits.MaxVoxels)
			{
				throw new VoxelScriptException(
					$"Voxel count would exceed the cap of {_limits.MaxVoxels}. Build a smaller or hollow model.");
			}

			_voxels[p] = c;

			// Cooperative deadline check, sampled to keep the Stopwatch read off
			// the hot path of large fills. Also checks for external cancellation so
			// the executor can signal the builder to stop without abandoning the thread.
			if ((++_opsSinceDeadlineCheck & 0x3FFF) == 0 &&
				(_ct.IsCancellationRequested || _stopwatch.Elapsed > _limits.WallClock))
			{
				throw new VoxelScriptException(
					$"Script exceeded its wall-clock budget of {_limits.WallClock.TotalSeconds:0.##}s.");
			}
		}

		private void PlacePlanar(VoxelAxis axis, int cx, int cy, int cz, int h, int u, int v, Color32 c)
		{
			switch (axis)
			{
				case VoxelAxis.X:
					SetInternal(cx + h, cy + u, cz + v, c);
					break;
				case VoxelAxis.Y:
					SetInternal(cx + u, cy + h, cz + v, c);
					break;
				default:
					SetInternal(cx + u, cy + v, cz + h, c);
					break;
			}
		}

		private void PushIfFillable(Stack<Vector3Int> stack, HashSet<Vector3Int> visited, Vector3Int p, Vector3Int min, Vector3Int max)
		{
			if (p.x < min.x || p.x > max.x || p.y < min.y || p.y > max.y || p.z < min.z || p.z > max.z)
			{
				return;
			}

			if (_voxels.ContainsKey(p) || !visited.Add(p))
			{
				return;
			}

			stack.Push(p);
		}

		private void ComputeBounds(out Vector3Int min, out Vector3Int max)
		{
			min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
			max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
			foreach (var p in _voxels.Keys)
			{
				min = Vector3Int.Min(min, p);
				max = Vector3Int.Max(max, p);
			}
		}

		private static void Order(ref int a, ref int b)
		{
			if (a > b)
			{
				(a, b) = (b, a);
			}
		}

		private static void TryHexByte(string s, int offset, ref byte value)
		{
			if (byte.TryParse(s.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
			{
				value = parsed;
			}
		}

		private sealed class Color32Comparer : IEqualityComparer<Color32>
		{
			public bool Equals(Color32 x, Color32 y) => x.r == y.r && x.g == y.g && x.b == y.b && x.a == y.a;
			public int GetHashCode(Color32 c) => (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
		}
	}
}
