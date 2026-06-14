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
						Height = YamlNodes.GetInt(assetMap, "height"),
						Length = YamlNodes.GetInt(assetMap, "length"),
						Width = YamlNodes.GetInt(assetMap, "width"),
						Tolerance = YamlNodes.GetInt(assetMap, "tolerance", 1),
						Symmetry = YamlNodes.GetString(assetMap, "symmetry", "none"),
						Rig = YamlNodes.GetBool(assetMap, "rig"),
						Reference = YamlNodes.GetString(assetMap, "reference"),
					});
				}
			}

			return new SetManifest
			{
				Game = YamlNodes.GetString(root, "game"),
				Assets = assets,
			};
		}

		public static string Write(SetManifest manifest)
		{
			var sb = new StringBuilder();
			sb.Append("game: ").Append(manifest.Game).Append('\n');
			sb.Append("assets:\n");
			foreach (var asset in manifest.Assets)
			{
				sb.Append("  - id: ").Append(asset.Id).Append('\n');
				if (asset.Description.Length > 0)
				{
					sb.Append("    description: ").Append(YamlNodes.Quote(asset.Description)).Append('\n');
				}

				sb.Append("    height: ").Append(asset.Height).Append('\n');
				if (asset.Length > 0)
				{
					sb.Append("    length: ").Append(asset.Length).Append('\n');
				}

				if (asset.Width > 0)
				{
					sb.Append("    width: ").Append(asset.Width).Append('\n');
				}

				if (asset.Tolerance != 1)
				{
					sb.Append("    tolerance: ").Append(asset.Tolerance).Append('\n');
				}

				sb.Append("    symmetry: ").Append(asset.Symmetry).Append('\n');
				sb.Append("    rig: ").Append(asset.Rig ? "true" : "false").Append('\n');
				if (asset.HasReference)
				{
					sb.Append("    reference: ").Append(asset.Reference).Append('\n');
				}
			}

			return sb.ToString();
		}
	}
}
