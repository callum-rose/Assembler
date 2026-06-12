using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using YamlDotNet.RepresentationModel;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Small reading/writing helpers over YamlDotNet's representation model for
	/// the hand-mapped voxelization formats (vmodel / manifest / brief). Reading
	/// is tolerant of style (flow vs block); writing is deterministic block
	/// style so files diff cleanly.
	/// </summary>
	internal static class YamlNodes
	{
		public static YamlMappingNode ParseRoot(string text)
		{
			var stream = new YamlStream();
			stream.Load(new StringReader(text));
			if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode map)
			{
				throw new FormatException("Expected a YAML mapping at the document root.");
			}

			return map;
		}

		public static YamlNode? Find(YamlMappingNode map, string key) =>
			map.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;

		public static string GetString(YamlMappingNode map, string key, string fallback = "") =>
			Find(map, key) is YamlScalarNode scalar ? scalar.Value ?? fallback : fallback;

		public static float GetFloat(YamlMappingNode map, string key, float fallback = 0f) =>
			Find(map, key) is YamlScalarNode { Value: { } v } &&
			float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
				? parsed
				: fallback;

		public static int GetInt(YamlMappingNode map, string key, int fallback = 0) =>
			Find(map, key) is YamlScalarNode { Value: { } v } &&
			int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
				? parsed
				: fallback;

		public static bool GetBool(YamlMappingNode map, string key, bool fallback = false) =>
			Find(map, key) is YamlScalarNode { Value: { } v } && bool.TryParse(v, out var parsed)
				? parsed
				: fallback;

		public static Vector3Int GetVector3Int(YamlMappingNode map, string key, Vector3Int fallback)
		{
			if (Find(map, key) is not YamlSequenceNode seq || seq.Children.Count < 3)
			{
				return fallback;
			}

			var values = seq.Children
				.Take(3)
				.Select(n => n is YamlScalarNode { Value: { } v } &&
							 int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
					? i
					: 0)
				.ToArray();
			return new Vector3Int(values[0], values[1], values[2]);
		}

		public static Vector3 GetVector3(YamlMappingNode map, string key, Vector3 fallback)
		{
			if (Find(map, key) is not YamlSequenceNode seq || seq.Children.Count < 3)
			{
				return fallback;
			}

			var values = seq.Children
				.Take(3)
				.Select(n => n is YamlScalarNode { Value: { } v } &&
							 float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)
					? f
					: 0f)
				.ToArray();
			return new Vector3(values[0], values[1], values[2]);
		}

		public static string ScalarValue(YamlNode node) =>
			node is YamlScalarNode { Value: { } v } ? v : string.Empty;

		// ---- Writing -------------------------------------------------------

		public static string Float(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);

		public static string Vector(Vector3Int v) => $"[{v.x}, {v.y}, {v.z}]";

		public static string Vector(Vector3 v) => $"[{Float(v.x)}, {Float(v.y)}, {Float(v.z)}]";

		/// <summary>Double-quotes a scalar, escaping the characters YAML requires.</summary>
		public static string Quote(string value) =>
			"\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

		/// <summary>
		/// Appends <c>key: |-</c> followed by the text as a block scalar whose
		/// content lines sit at <paramref name="indent"/> spaces.
		/// </summary>
		public static void AppendBlockScalar(StringBuilder sb, string prefix, string text, int indent)
		{
			sb.Append(prefix).Append("|-").Append('\n');
			var pad = new string(' ', indent);
			foreach (var line in text.Replace("\r", string.Empty).Split('\n'))
			{
				if (line.Length == 0)
				{
					sb.Append('\n');
				}
				else
				{
					sb.Append(pad).Append(line).Append('\n');
				}
			}
		}
	}
}
