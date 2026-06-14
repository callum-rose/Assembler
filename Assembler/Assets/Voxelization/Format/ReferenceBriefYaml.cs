using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using YamlDotNet.RepresentationModel;

namespace Assembler.Voxelization
{
	/// <summary>Reads and writes the reference brief emitted by Stage 1 image ingestion.</summary>
	public static class ReferenceBriefYaml
	{
		public static ReferenceBrief Read(string text)
		{
			var document = YamlNodes.ParseRoot(text);
			var root = YamlNodes.Find(document, "reference_brief") as YamlMappingNode ?? document;

			return new ReferenceBrief
			{
				Source = YamlNodes.GetString(root, "source"),
				Palette = ReadPalette(root),
				Proportions = ReadProportions(root),
				SignatureFeatures = ReadFeatures(root),
				Silhouette = ReadSilhouette(root),
			};
		}

		public static string Write(ReferenceBrief brief)
		{
			var sb = new StringBuilder();
			sb.Append("reference_brief:\n");
			sb.Append("  source: ").Append(brief.Source).Append('\n');

			var palette = brief.Palette.Select(e => $"{e.Key}: {YamlNodes.Quote(e.ToHex())}");
			sb.Append("  palette: { ").Append(string.Join(", ", palette)).Append(" }\n");

			var proportions = brief.Proportions.Select(kv => $"{kv.Key}: {YamlNodes.Float(kv.Value)}");
			sb.Append("  proportions: { ").Append(string.Join(", ", proportions)).Append(" }\n");

			var features = brief.SignatureFeatures.Select(YamlNodes.Quote);
			sb.Append("  signature_features: [").Append(string.Join(", ", features)).Append("]\n");

			if (!brief.Silhouette.IsEmpty)
			{
				sb.Append("  silhouette:\n");
				sb.Append("    face: ").Append(brief.Silhouette.Face).Append('\n');
				sb.Append("    size: [").Append(brief.Silhouette.Size.x).Append(", ").Append(brief.Silhouette.Size.y).Append("]\n");
				sb.Append("    rows:\n");
				foreach (var row in brief.Silhouette.Rows)
				{
					sb.Append("      - ").Append(YamlNodes.Quote(row)).Append('\n');
				}
			}

			return sb.ToString();
		}

		private static IReadOnlyList<PaletteEntry> ReadPalette(YamlMappingNode root)
		{
			var entries = new List<PaletteEntry>();
			if (YamlNodes.Find(root, "palette") is not YamlMappingNode paletteMap)
			{
				return entries;
			}

			foreach (var kv in paletteMap.Children)
			{
				var key = YamlNodes.ScalarValue(kv.Key);
				if (key.Length != 1)
				{
					throw new FormatException($"Brief palette key '{key}' must be a single character.");
				}

				if (key[0] is PaletteEntry.EmptyKey or PaletteEntry.EmptyCell)
				{
					continue;
				}

				entries.Add(new PaletteEntry(key[0], PaletteEntry.ParseHex(YamlNodes.ScalarValue(kv.Value))));
			}

			return entries;
		}

		private static IReadOnlyDictionary<string, float> ReadProportions(YamlMappingNode root)
		{
			var proportions = new Dictionary<string, float>();
			if (YamlNodes.Find(root, "proportions") is YamlMappingNode map)
			{
				foreach (var kv in map.Children)
				{
					proportions[YamlNodes.ScalarValue(kv.Key)] = YamlNodes.GetFloat(map, YamlNodes.ScalarValue(kv.Key));
				}
			}

			return proportions;
		}

		private static IReadOnlyList<string> ReadFeatures(YamlMappingNode root) =>
			YamlNodes.Find(root, "signature_features") is YamlSequenceNode seq
				? seq.Children.Select(YamlNodes.ScalarValue).ToList()
				: new List<string>();

		private static SilhouetteSpec ReadSilhouette(YamlMappingNode root)
		{
			if (YamlNodes.Find(root, "silhouette") is not YamlMappingNode map)
			{
				return SilhouetteSpec.None;
			}

			var rows = YamlNodes.Find(map, "rows") is YamlSequenceNode seq
				? seq.Children.Select(YamlNodes.ScalarValue).ToList()
				: new List<string>();

			var size = Vector3Int.zero;
			if (YamlNodes.Find(map, "size") is YamlSequenceNode sizeSeq && sizeSeq.Children.Count >= 2)
			{
				int Component(int i) =>
					int.TryParse(YamlNodes.ScalarValue(sizeSeq.Children[i]), out var v) ? v : 0;
				size = new Vector3Int(Component(0), Component(1), 0);
			}

			return new SilhouetteSpec(YamlNodes.GetString(map, "face", "front"), size, rows);
		}
	}
}
