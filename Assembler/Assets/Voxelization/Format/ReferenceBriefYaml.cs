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
				RealWorldDims = ReadDims(root),
				Palette = ReadPalette(root),
				Proportions = ReadProportions(root),
				SignatureFeatures = ReadFeatures(root),
				Silhouettes = ReadSilhouettes(root),
			};
		}

		public static string Write(ReferenceBrief brief)
		{
			var sb = new StringBuilder();
			sb.Append("reference_brief:\n");
			sb.Append("  source: ").Append(brief.Source).Append('\n');
			sb.Append("  real_world_dims: { height: ").Append(YamlNodes.Float(brief.RealWorldDims.Height))
				.Append(", width: ").Append(YamlNodes.Float(brief.RealWorldDims.Width))
				.Append(", depth: ").Append(YamlNodes.Float(brief.RealWorldDims.Depth)).Append(" }\n");

			var palette = brief.Palette.Select(e => $"{e.Key}: {YamlNodes.Quote(e.ToHex())}");
			sb.Append("  palette: { ").Append(string.Join(", ", palette)).Append(" }\n");

			var proportions = brief.Proportions.Select(kv => $"{kv.Key}: {YamlNodes.Float(kv.Value)}");
			sb.Append("  proportions: { ").Append(string.Join(", ", proportions)).Append(" }\n");

			var features = brief.SignatureFeatures.Select(YamlNodes.Quote);
			sb.Append("  signature_features: [").Append(string.Join(", ", features)).Append("]\n");

			if (brief.Silhouettes.Count > 0)
			{
				sb.Append("  silhouettes:\n");
				foreach (var silhouette in brief.Silhouettes)
				{
					sb.Append("    - face: ").Append(silhouette.Face).Append('\n');
					sb.Append("      size: [").Append(silhouette.Size.x).Append(", ").Append(silhouette.Size.y).Append("]\n");
					sb.Append("      rows:\n");
					foreach (var row in silhouette.Rows)
					{
						sb.Append("        - ").Append(YamlNodes.Quote(row)).Append('\n');
					}
				}
			}

			return sb.ToString();
		}

		private static RealWorldDims ReadDims(YamlMappingNode root)
		{
			if (YamlNodes.Find(root, "real_world_dims") is not YamlMappingNode dims)
			{
				return RealWorldDims.None;
			}

			return new RealWorldDims(
				YamlNodes.GetFloat(dims, "height"),
				YamlNodes.GetFloat(dims, "width"),
				YamlNodes.GetFloat(dims, "depth"));
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

		/// <summary>
		/// Reads the <c>silhouettes:</c> sequence (one block per face). The brief is
		/// always freshly generated within a run, never persisted/authored, so there
		/// is no legacy single <c>silhouette:</c> form to fall back to.
		/// </summary>
		private static IReadOnlyList<SilhouetteSpec> ReadSilhouettes(YamlMappingNode root)
		{
			var silhouettes = new List<SilhouetteSpec>();
			if (YamlNodes.Find(root, "silhouettes") is not YamlSequenceNode seq)
			{
				return silhouettes;
			}

			foreach (var node in seq.Children)
			{
				if (node is YamlMappingNode map)
				{
					silhouettes.Add(ReadSilhouette(map));
				}
			}

			return silhouettes;
		}

		private static SilhouetteSpec ReadSilhouette(YamlMappingNode map)
		{
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
