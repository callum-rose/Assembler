using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YamlDotNet.RepresentationModel;

namespace Assembler.Voxelization
{
	/// <summary>Reads and writes the set manifest / scale bible (*.manifest.yaml).</summary>
	public static class ManifestYaml
	{
		public static SetManifest Read(string text)
		{
			var root = YamlNodes.ParseRoot(text);
			var assets = new List<ManifestAsset>();

			if (YamlNodes.Find(root, "assets") is YamlSequenceNode seq)
			{
				foreach (var node in seq.Children)
				{
					if (node is not YamlMappingNode assetMap)
					{
						throw new FormatException("Each entry under 'assets' must be a mapping.");
					}

					var id = YamlNodes.GetString(assetMap, "id");
					if (id.Length == 0)
					{
						throw new FormatException("An asset is missing its 'id'.");
					}

					if (assets.Any(a => a.Id == id))
					{
						throw new FormatException($"Duplicate asset id '{id}'.");
					}

					assets.Add(new ManifestAsset
					{
						Id = id,
						Description = YamlNodes.GetString(assetMap, "description"),
						RealWorldHeight = YamlNodes.GetFloat(assetMap, "height",
							YamlNodes.GetFloat(assetMap, "real_world_height")),
						Length = YamlNodes.GetFloat(assetMap, "length"),
						Width = YamlNodes.GetFloat(assetMap, "width"),
						Tolerance = YamlNodes.GetInt(assetMap, "tolerance", 1),
						Symmetry = YamlNodes.GetString(assetMap, "symmetry", "none"),
						Rig = YamlNodes.GetBool(assetMap, "rig"),
						References = ReadReferences(assetMap),
					});
				}
			}

			return new SetManifest
			{
				Game = YamlNodes.GetString(root, "game"),
				Unit = YamlNodes.GetFloat(root, "unit", 1f),
				Assets = assets,
			};
		}

		/// <summary>
		/// Parses the per-asset <c>references:</c> sequence of <c>{ file, view }</c>
		/// maps. The legacy scalar <c>reference:</c> key is no longer read — the
		/// manifest is the single source of truth and must use the list form.
		/// </summary>
		private static IReadOnlyList<ReferenceImage> ReadReferences(YamlMappingNode assetMap)
		{
			var references = new List<ReferenceImage>();
			if (YamlNodes.Find(assetMap, "references") is not YamlSequenceNode seq)
			{
				return references;
			}

			foreach (var node in seq.Children)
			{
				if (node is not YamlMappingNode map)
				{
					throw new FormatException("Each entry under 'references' must be a mapping with 'file' and 'view'.");
				}

				var file = YamlNodes.GetString(map, "file");
				var view = YamlNodes.GetString(map, "view");
				if (file.Length == 0 || view.Length == 0)
				{
					throw new FormatException("Each 'references' entry needs both a 'file' and a 'view'.");
				}

				if (!ReferenceImage.IsValidFace(view))
				{
					throw new FormatException(
						$"Reference view '{view}' is not one of: {string.Join(", ", ReferenceImage.Faces)}.");
				}

				references.Add(new ReferenceImage(file, view.ToLowerInvariant()));
			}

			return references;
		}

		public static string Write(SetManifest manifest)
		{
			var sb = new StringBuilder();
			sb.Append("game: ").Append(manifest.Game).Append('\n');
			sb.Append("unit: ").Append(YamlNodes.Float(manifest.Unit)).Append('\n');
			sb.Append("assets:\n");
			foreach (var asset in manifest.Assets)
			{
				sb.Append("  - id: ").Append(asset.Id).Append('\n');
				if (asset.Description.Length > 0)
				{
					sb.Append("    description: ").Append(YamlNodes.Quote(asset.Description)).Append('\n');
				}

				sb.Append("    height: ").Append(YamlNodes.Float(asset.RealWorldHeight)).Append('\n');
				if (asset.Length > 0f)
				{
					sb.Append("    length: ").Append(YamlNodes.Float(asset.Length)).Append('\n');
				}

				if (asset.Width > 0f)
				{
					sb.Append("    width: ").Append(YamlNodes.Float(asset.Width)).Append('\n');
				}

				if (asset.Tolerance != 1)
				{
					sb.Append("    tolerance: ").Append(asset.Tolerance).Append('\n');
				}

				sb.Append("    symmetry: ").Append(asset.Symmetry).Append('\n');
				sb.Append("    rig: ").Append(asset.Rig ? "true" : "false").Append('\n');
				if (asset.HasReference)
				{
					sb.Append("    references:\n");
					foreach (var reference in asset.References)
					{
						sb.Append("      - file: ").Append(YamlNodes.Quote(reference.File))
							.Append('\n').Append("        view: ").Append(reference.Face).Append('\n');
					}
				}
			}

			return sb.ToString();
		}
	}
}
