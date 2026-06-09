using System.Collections.Generic;
using System.Text;

namespace Assembler.Generation.Verification.Editor
{
	/// <summary>
	/// Appends an asset-generation protocol to the user's game idea. Lives in the USER
	/// message (per-run, carries the slug + supported-type list) so the cached system
	/// prompt is never touched / cache-busted. Pure string building — no I/O.
	/// </summary>
	public static class AssetAugmentedPrompt
	{
		public static string Build(string userPrompt, string gameSlug, IReadOnlyList<string> supportedTypes)
		{
			var sb = new StringBuilder();
			sb.AppendLine(userPrompt);
			sb.AppendLine();
			AppendProtocol(sb, gameSlug, supportedTypes);
			return sb.ToString();
		}

		/// <summary>
		/// Frames a user-driven revision of the previously generated game. The conversation
		/// already holds the prior descriptor, so this just states the requested change and
		/// re-states the asset protocol (same slug + supported types).
		/// </summary>
		public static string BuildRevision(string instruction, string gameSlug, IReadOnlyList<string> supportedTypes)
		{
			var sb = new StringBuilder();
			sb.AppendLine("Revise the game you produced above. Requested change:");
			sb.AppendLine();
			sb.AppendLine(instruction);
			sb.AppendLine();
			sb.AppendLine("Return the FULL updated descriptor in the usual two-fenced-block format");
			sb.AppendLine("(```yaml ...``` then ```feedback ...```). Keep everything that should stay the");
			sb.AppendLine($"same; reuse the same game slug ({gameSlug}) for asset paths. Only add an");
			sb.AppendLine("```assets block for assets that are newly referenced by this revision —");
			sb.AppendLine("assets generated for the previous version still exist and need not be re-listed.");
			sb.AppendLine();
			AppendProtocol(sb, gameSlug, supportedTypes);
			return sb.ToString();
		}

		private static void AppendProtocol(StringBuilder sb, string gameSlug, IReadOnlyList<string> supportedTypes)
		{
			var types = supportedTypes.Count is 0
				? "(none)"
				: string.Join(", ", supportedTypes);

			sb.AppendLine("================ ASSET GENERATION PROTOCOL ================");
			sb.AppendLine();
			sb.AppendLine($"Game slug for this run: {gameSlug}");
			sb.AppendLine($"Asset generators are available for these types ONLY: {types}.");
			sb.AppendLine();
			sb.AppendLine("Some entities need a generated visual/audio asset. For every such asset whose");
			sb.AppendLine("type is in the supported list above, you MUST do BOTH of the following:");
			sb.AppendLine();
			sb.AppendLine("1. Declare it in the top-level `Assets:` block of the YAML descriptor:");
			sb.AppendLine("     Assets:");
			sb.AppendLine("       - Id: <asset id>");
			sb.AppendLine("         Type: <supported type, e.g. mesh>");
			sb.AppendLine("         Source: resources");
			sb.AppendLine($"         Path: Voxels/{gameSlug}/<asset id>   # Resources-relative, NO file extension");
			sb.AppendLine("   ...and reference it from the entity's behaviour. For a `mesh` asset use the");
			sb.AppendLine("   `voxel mesh` behaviour, whose `Mesh` property takes `!asset { Id: <asset id> }`.");
			sb.AppendLine();
			sb.AppendLine("2. Emit a fenced ```assets block (in ADDITION to the usual ```yaml and ```feedback");
			sb.AppendLine("   blocks) containing a JSON array, one object per generated asset:");
			sb.AppendLine("     ```assets");
			sb.AppendLine("     [");
			sb.AppendLine("       {");
			sb.AppendLine("         \"type\": \"mesh\",");
			sb.AppendLine("         \"id\": \"<asset id>\",");
			sb.AppendLine($"         \"path\": \"Voxels/{gameSlug}/<asset id>\",");
			sb.AppendLine("         \"prompt\": \"<full standalone description of the asset>\"");
			sb.AppendLine("       }");
			sb.AppendLine("     ]");
			sb.AppendLine("     ```");
			sb.AppendLine();
			sb.AppendLine("Hard rules:");
			sb.AppendLine("- The `path` in the ```assets block MUST be byte-for-byte identical to that asset's");
			sb.AppendLine("  `Path` in the `Assets:` block. This is the link that makes the asset resolve.");
			sb.AppendLine("- Each `prompt` is handed to a generator with NO other context about the game, the");
			sb.AppendLine("  descriptor, or the other assets. Describe the asset fully and self-containedly");
			sb.AppendLine("  (shape, style, colours, scale, orientation) as if briefing an artist cold.");
			sb.AppendLine("- Only request assets whose `type` is in the supported list above. If an entity needs");
			sb.AppendLine("  an unsupported asset type, do NOT add it to the ```assets block — the user will");
			sb.AppendLine("  supply it manually; reference it in the descriptor as usual.");
			sb.AppendLine("- If the game needs no generated assets, omit the ```assets block (or emit []).");
		}
	}
}
