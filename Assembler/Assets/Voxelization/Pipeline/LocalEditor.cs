using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;
using UnityEngine;
using YamlDotNet.RepresentationModel;

namespace Assembler.Voxelization
{
	/// <summary>One palette recolour: change the colour an existing (or new) key maps to.</summary>
	public sealed record PaletteEdit(char Key, Color32 Colour);

	/// <summary>
	/// One part edit. Any of pivot/offset/size may be supplied; a non-empty
	/// <see cref="Note"/> (or a size change, which makes the old grid the wrong
	/// shape) triggers a re-author of that single part. A pivot/offset-only edit
	/// is applied deterministically with no model call.
	/// </summary>
	public sealed record PartEdit(string Id, Vector3Int? Pivot, Vector3Int? Offset, Vector3Int? Size, string Note);

	/// <summary>The interpreted local edit; empty when the note could not be expressed locally.</summary>
	public sealed record ModelEdit(IReadOnlyList<PaletteEdit> Palette, IReadOnlyList<PartEdit> Parts)
	{
		public static ModelEdit Empty { get; } = new(System.Array.Empty<PaletteEdit>(), System.Array.Empty<PartEdit>());

		public bool IsEmpty => Palette.Count == 0 && Parts.Count == 0;
	}

	/// <summary>
	/// The lightweight refine path (issue 307). Given an already-generated model
	/// and a short operator note ("make the car red", "move the left wheel
	/// forward 1"), one text call maps the note to a small <see cref="ModelEdit"/>
	/// — palette recolours plus per-part pivot/offset/size/note edits — that is
	/// applied to the existing model, re-authoring ONLY the parts the note
	/// touches. This sidesteps the full re-plan (which the deterministic plan
	/// gates frequently reject) so minor edits are both cheap and reliable.
	/// </summary>
	public sealed class LocalEditor
	{
		public const string Stage = "4-edit";

		/// <summary>Total interpret calls: the first plus parse-failure retries.</summary>
		public const int MaxAttempts = 3;

		private readonly IAnthropicGateway _gateway;
		private readonly VoxelizationConfig _config;

		public LocalEditor(IAnthropicGateway gateway, VoxelizationConfig config)
		{
			_gateway = gateway;
			_config = config;
		}

		public async Task<ModelEdit> InterpretAsync(VoxelRigModel model, string views, string note, CancellationToken ct)
		{
			var messages = new List<AnthropicMessage>
			{
				new("user", UserPrompt(model, views, note)),
			};

			for (var attempt = 1; ; attempt++)
			{
				var response = await _gateway.SendAsync(
					Stage, _config.PlanningModel, SystemPrompt, messages, ct).ConfigureAwait(false);

				if (TryParse(response, model, out var edit, out var feedback))
				{
					return edit;
				}

				if (attempt >= MaxAttempts)
				{
					throw new VoxelizationException($"Refining '{model.Id}' failed after {MaxAttempts} attempts: {feedback}");
				}

				messages.Add(new AnthropicMessage("assistant", response));
				messages.Add(new AnthropicMessage("user", feedback));
			}
		}

		private static bool TryParse(string response, VoxelRigModel model, out ModelEdit edit, out string feedback)
		{
			edit = ModelEdit.Empty;
			feedback = string.Empty;

			var block = FencedBlockExtractor.Extract(response, "edit");
			if (block == null)
			{
				feedback = "That response contained no ```edit fenced block. Emit exactly one.";
				return false;
			}

			// A block that holds only comments / "no local edit possible" is a
			// valid, deliberate no-op — surface it as an empty edit rather than a
			// parse error so the caller can fall back to a full regenerate.
			YamlMappingNode root;
			try
			{
				root = YamlNodes.ParseRoot(block);
			}
			catch (System.FormatException)
			{
				edit = ModelEdit.Empty;
				return true;
			}

			try
			{
				edit = new ModelEdit(ReadPalette(root), ReadParts(root, model));
				return true;
			}
			catch (System.FormatException ex)
			{
				feedback = $"That edit could not be applied: {ex.Message}\nEmit the corrected ```edit block.";
				return false;
			}
		}

		private static IReadOnlyList<PaletteEdit> ReadPalette(YamlMappingNode root)
		{
			if (YamlNodes.Find(root, "palette") is not YamlMappingNode map)
			{
				return System.Array.Empty<PaletteEdit>();
			}

			var edits = new List<PaletteEdit>();
			foreach (var kv in map.Children)
			{
				var key = YamlNodes.ScalarValue(kv.Key);
				if (key.Length != 1 || key[0] is PaletteEntry.EmptyKey or PaletteEntry.EmptyCell)
				{
					throw new System.FormatException($"Palette edit key '{key}' must be a single non-reserved character.");
				}

				edits.Add(new PaletteEdit(key[0], PaletteEntry.ParseHex(YamlNodes.ScalarValue(kv.Value))));
			}

			return edits;
		}

		private static IReadOnlyList<PartEdit> ReadParts(YamlMappingNode root, VoxelRigModel model)
		{
			if (YamlNodes.Find(root, "parts") is not YamlMappingNode map)
			{
				return System.Array.Empty<PartEdit>();
			}

			var edits = new List<PartEdit>();
			foreach (var kv in map.Children)
			{
				var id = YamlNodes.ScalarValue(kv.Key);
				if (model.FindPart(id) == null)
				{
					throw new System.FormatException($"Part '{id}' is not in the model. Edit only existing parts.");
				}

				if (kv.Value is not YamlMappingNode partMap)
				{
					throw new System.FormatException($"Part '{id}': the edit must be a mapping (pivot/offset/size/note).");
				}

				edits.Add(new PartEdit(
					id,
					Vector(partMap, "pivot"),
					Vector(partMap, "offset"),
					Vector(partMap, "size"),
					YamlNodes.GetString(partMap, "note")));
			}

			return edits;
		}

		private static Vector3Int? Vector(YamlMappingNode map, string key) =>
			YamlNodes.Find(map, key) == null ? null : YamlNodes.GetVector3Int(map, key, Vector3Int.zero);

		private static string UserPrompt(VoxelRigModel model, string views, string note)
		{
			var sb = new StringBuilder();
			sb.Append("Model: ").Append(model.Id).Append('\n');
			if (model.Description.Length > 0)
			{
				sb.Append("Description: ").Append(model.Description).Append('\n');
			}

			sb.Append("Palette (key = colour — recolouring a key repaints every voxel using it):\n");
			foreach (var entry in model.Palette)
			{
				sb.Append("  ").Append(entry.Key).Append(" = ").Append(entry.ToHex()).Append('\n');
			}

			sb.Append("\nParts (id, parent, pivot, geometry):\n");
			foreach (var part in model.Parts)
			{
				sb.Append("  ").Append(part.Id)
					.Append(" parent=").Append(part.Parent)
					.Append(" pivot=").Append(YamlNodes.Vector(part.Pivot))
					.Append(' ').Append(DescribeData(part.Data)).Append('\n');
			}

			if (views.Length > 0)
			{
				sb.Append('\n').Append(views).Append('\n');
			}

			sb.Append("\nOperator's edit request (apply it as a MINIMAL local edit):\n").Append(note).Append('\n');
			sb.Append("\nEmit the ```edit block.");
			return sb.ToString();
		}

		private static string DescribeData(PartData data) => data switch
		{
			LayersPartData l => $"layers size={YamlNodes.Vector(l.Size)} offset={YamlNodes.Vector(l.Offset)}",
			PrimitivesPartData p => $"primitives size={YamlNodes.Vector(p.Size)} offset={YamlNodes.Vector(p.Offset)}",
			ScriptPartData s => $"script size={YamlNodes.Vector(s.Size)} offset={YamlNodes.Vector(s.Offset)}",
			MirrorPartData m => $"mirror of {m.Source} (follows its source — edit the source, not this)",
			CopyPartData c => $"copy of {c.Source} (follows its source — edit the source, not this)",
			PlannedPartData pl => $"planned ({pl.PlannedEncoding.ToString().ToLowerInvariant()})",
			_ => "?",
		};

		private const string SystemPrompt =
			"You edit an ALREADY-BUILT voxel model in place from a short operator note. You do NOT redesign or re-plan " +
			"it — you express the note as the SMALLEST set of local edits and touch nothing else.\n\n" +
			"Coordinates are integer voxel cells, Y-up: x = right, y = up, z = forward (towards the viewer).\n\n" +
			"Output exactly one fenced block labelled `edit`:\n" +
			"```edit\n" +
			"palette:\n" +
			"  R: \"#cc2222\"          # recolour key R — repaints every voxel using it model-wide\n" +
			"parts:\n" +
			"  body:\n" +
			"    note: \"repaint the cab roof red\"   # re-author ONLY this part to the note\n" +
			"  wheel.FL:\n" +
			"    pivot: [3, 1, 4]      # move this part's joint; geometry unchanged\n" +
			"```\n" +
			"Rules:\n" +
			"- Recolouring a whole region (\"make the car red\", \"darker windows\") is a PALETTE edit on the key that " +
			"region uses — no part needs re-authoring. Prefer this; it is the cheapest and most reliable edit.\n" +
			"- Moving/sliding a part is a `pivot` (joint in the parent's frame) and/or `offset` (grid origin in the " +
			"part's own frame) edit under `parts:` — no re-authoring.\n" +
			"- Reshaping or repainting PART of one part needs a `note` (and optionally a new `size`) under that part's " +
			"id — it re-authors that single part. List the FEWEST parts that must change.\n" +
			"- Edit only AUTHORED parts. A mirror/copy part follows its source automatically — edit the source instead.\n" +
			"- Omit `palette:` or `parts:` entirely when that kind of edit is not needed. Include only the keys that change.\n" +
			"- If the note demands a structural redesign that no local edit can express, emit an empty block " +
			"(a single comment line) so the operator knows to regenerate instead.";
	}
}
