using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Assembler.Voxels.Scripting;
using Assembler.Voxels.Terrain;
using UnityEngine;
using YamlDotNet.Serialization;

namespace Assembler.Voxels.Editor.Terrain
{
	/// <summary>
	/// Parses a <c>*.terrain.yaml</c> recipe into a <see cref="TerrainSpec"/>. Plain
	/// YAML only — vectors as <c>{ X, Y, Z }</c> maps and colours as <c>"#rrggbb"</c>
	/// strings — so it depends on nothing beyond YamlDotNet and the runtime
	/// <c>Assembler.Voxels</c> types. Unknown keys are ignored; missing keys fall back
	/// to sensible defaults. The spec records are built via their constructors, never
	/// init-setters, so this assembly needs no <c>IsExternalInit</c> support.
	/// </summary>
	public static class TerrainSpecYamlReader
	{
		private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
		private static readonly Color32 DefaultBaseColour = new(0x5a, 0x8f, 0x3c, 255);
		private static readonly Color32 DefaultWallColour = new(0x6b, 0x6b, 0x6b, 255);
		private static readonly Color32 White = new(255, 255, 255, 255);

		public static TerrainSpec ReadFile(string path)
			=> Read(File.ReadAllText(path), DefaultName(path));

		public static TerrainSpec Read(string yaml, string fallbackName)
		{
			var root = new DeserializerBuilder().Build().Deserialize<Dictionary<object, object>>(yaml);
			if (root == null)
			{
				throw new InvalidDataException("Terrain YAML is empty or not a mapping.");
			}

			var name = GetString(root, "Name", fallbackName);
			var seed = GetInt(root, "Seed", 0);
			var size = GetVec3Int(root, "Size", new Vector3Int(64, 64, 32));
			var skin = GetInt(root, "SkinThickness", 2);
			var enclosure = GetEnum(root, "Enclosure", Enclosure.Open);
			var wallHeight = GetInt(root, "WallHeight", 16);
			var wallThickness = GetInt(root, "WallThickness", 2);
			var wallColour = GetColour(root, "WallColour", DefaultWallColour);

			return new TerrainSpec(
				name, seed, size, skin, enclosure, wallHeight, wallThickness, wallColour,
				ReadBase(root), ReadOps(root));
		}

		private static BaseOp ReadBase(Dictionary<object, object> root)
		{
			if (!TryGet(root, "Base", out var node) || !(node is Dictionary<object, object> map))
			{
				return new BaseOp(BaseKind.Flat, DefaultBaseColour, 1, null);
			}

			var type = GetEnum(map, "Type", BaseKind.Flat);
			var colour = GetColour(map, "Colour", DefaultBaseColour);
			var baseHeight = GetInt(map, "BaseHeight", GetInt(map, "Height", 1));
			var noise = type == BaseKind.Noise ? ReadNoise(map) : null;
			return new BaseOp(type, colour, baseHeight, noise);
		}

		private static NoiseSettings ReadNoise(Dictionary<object, object> baseMap)
		{
			if (!TryGet(baseMap, "Noise", out var node) || !(node is Dictionary<object, object> map))
			{
				return new NoiseSettings(NoiseKind.Fbm, 4, 0.02f, 16f, 2f, 0.5f, 0f);
			}

			return new NoiseSettings(
				GetEnum(map, "Kind", NoiseKind.Fbm),
				GetInt(map, "Octaves", 4),
				GetFloat(map, "Frequency", 0.02f),
				GetFloat(map, "Amplitude", 16f),
				GetFloat(map, "Lacunarity", 2f),
				GetFloat(map, "Gain", 0.5f),
				GetFloat(map, "DomainWarp", 0f));
		}

		private static IReadOnlyList<ModifierOp> ReadOps(Dictionary<object, object> root)
		{
			var ops = new List<ModifierOp>();
			if (!TryGet(root, "Ops", out var node) || !(node is List<object> list))
			{
				return ops;
			}

			foreach (var item in list)
			{
				if (!(item is Dictionary<object, object> map))
				{
					continue;
				}

				ops.Add(new ModifierOp(
					GetEnum(map, "Op", OpKind.Stamp),
					GetEnum(map, "Shape", ShapeKind.Box),
					GetColour(map, "Colour", White),
					GetEnum(map, "Combine", CombineMode.Replace),
					GetVec3Int(map, "Min", Vector3Int.zero),
					GetVec3Int(map, "Max", Vector3Int.zero),
					GetVec3Int(map, "Centre", GetVec3Int(map, "Center", Vector3Int.zero)),
					GetInt(map, "Radius", 1),
					GetInt(map, "Height", 1),
					GetEnum(map, "Axis", VoxelAxis.Z)));
			}

			return ops;
		}

		// ---- Scalar / node helpers ----------------------------------------

		private static bool TryGet(Dictionary<object, object> map, string key, out object value)
		{
			foreach (var kv in map)
			{
				if (kv.Key is string s && string.Equals(s, key, StringComparison.OrdinalIgnoreCase))
				{
					value = kv.Value;
					return true;
				}
			}

			value = null;
			return false;
		}

		private static string GetString(Dictionary<object, object> map, string key, string fallback)
			=> TryGet(map, key, out var v) && v != null ? Convert.ToString(v, Inv) ?? fallback : fallback;

		private static int GetInt(Dictionary<object, object> map, string key, int fallback)
			=> TryGet(map, key, out var v) && v != null
				&& int.TryParse(Convert.ToString(v, Inv), NumberStyles.Integer, Inv, out var n)
				? n
				: fallback;

		private static float GetFloat(Dictionary<object, object> map, string key, float fallback)
			=> TryGet(map, key, out var v) && v != null
				&& float.TryParse(Convert.ToString(v, Inv), NumberStyles.Float, Inv, out var f)
				? f
				: fallback;

		private static Vector3Int GetVec3Int(Dictionary<object, object> map, string key, Vector3Int fallback)
		{
			if (!TryGet(map, key, out var v) || !(v is Dictionary<object, object> m))
			{
				return fallback;
			}

			return new Vector3Int(
				GetInt(m, "X", fallback.x),
				GetInt(m, "Y", fallback.y),
				GetInt(m, "Z", fallback.z));
		}

		private static Color32 GetColour(Dictionary<object, object> map, string key, Color32 fallback)
			=> TryGet(map, key, out var v) && v != null ? ParseHex(Convert.ToString(v, Inv), fallback) : fallback;

		private static T GetEnum<T>(Dictionary<object, object> map, string key, T fallback) where T : struct
			=> TryGet(map, key, out var v) && v != null
				&& Enum.TryParse<T>(Convert.ToString(v, Inv), true, out var parsed)
				? parsed
				: fallback;

		private static Color32 ParseHex(string css, Color32 fallback)
		{
			var hex = (css ?? string.Empty).Trim();
			if (hex.StartsWith("#", StringComparison.Ordinal))
			{
				hex = hex.Substring(1);
			}

			if (hex.Length != 6 && hex.Length != 8)
			{
				return fallback;
			}

			byte r = fallback.r, g = fallback.g, b = fallback.b, a = 255;
			var ok = TryHexByte(hex, 0, ref r) & TryHexByte(hex, 2, ref g) & TryHexByte(hex, 4, ref b);
			if (hex.Length == 8)
			{
				ok &= TryHexByte(hex, 6, ref a);
			}

			return ok ? new Color32(r, g, b, a) : fallback;
		}

		private static bool TryHexByte(string s, int offset, ref byte value)
		{
			if (byte.TryParse(s.AsSpan(offset, 2), NumberStyles.HexNumber, Inv, out var parsed))
			{
				value = parsed;
				return true;
			}

			return false;
		}

		private static string DefaultName(string path)
		{
			var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
			if (name.EndsWith(".terrain", StringComparison.OrdinalIgnoreCase))
			{
				name = name.Substring(0, name.Length - ".terrain".Length);
			}

			return name;
		}
	}
}
