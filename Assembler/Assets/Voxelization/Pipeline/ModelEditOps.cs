using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Assembler.Voxelization
{
	/// <summary>
	/// One minimal edit to an already-accepted model, the vocabulary the refine
	/// stage emits instead of re-planning from scratch. Each op names exactly what
	/// it touches; <see cref="ModelEdits.Apply"/> rewrites only that, leaving every
	/// untouched part reference-equal so the export stays bit-identical.
	/// </summary>
	public abstract record ModelEditOp;

	/// <summary>Recolour a palette key in place (and the matching brief entry, so the brief-palette gate stays quiet).</summary>
	public sealed record RecolourOp(char Key, Color32 Colour) : ModelEditOp;

	/// <summary>Append a new palette key (≤12 colours).</summary>
	public sealed record AddColourOp(char Key, Color32 Colour) : ModelEditOp;

	/// <summary>Substitute one palette key for another within a single authored part (layers/primitives only).</summary>
	public sealed record RemapPartColourOp(string PartId, char From, char To) : ModelEditOp;

	/// <summary>Shift a part's pivot; an X-mirror twin's pivot is re-reflected to follow.</summary>
	public sealed record MovePivotOp(string PartId, Vector3Int Delta) : ModelEditOp;

	/// <summary>Shift a part's geometry offset within its own local frame.</summary>
	public sealed record MoveOffsetOp(string PartId, Vector3Int Delta) : ModelEditOp;

	/// <summary>Re-author one part (optionally resizing its box). Collected, not applied — the orchestrator runs the author.</summary>
	public sealed record ReauthorOp(string PartId, string Instructions, Vector3Int? Size, Vector3Int? Offset) : ModelEditOp;

	/// <summary>Remove a part and everything that depends on it (children, mirrors/copies, pose entries).</summary>
	public sealed record DeletePartOp(string PartId) : ModelEditOp;

	/// <summary>Escape hatch: the request is structural/ambiguous — fall back to a full re-plan with this reason.</summary>
	public sealed record ReplanOp(string Reason) : ModelEditOp;

	/// <summary>
	/// Pure parse + application of the refine op vocabulary, mirroring
	/// <see cref="PlanGeometryChecks"/>' style (no I/O, no LLM, unit-testable).
	/// <see cref="Parse"/> turns the fenced `edits` block body into ops;
	/// <see cref="Apply"/> rewrites the model/brief op-by-op and surfaces the
	/// re-author ops separately. Both throw <see cref="FormatException"/> with
	/// planner-style feedback so the caller can retry-with-feedback.
	/// </summary>
	public static class ModelEdits
	{
		public static IReadOnlyList<ModelEditOp> Parse(string yaml)
		{
			var stream = new YamlStream();
			try
			{
				stream.Load(new StringReader(yaml));
			}
			catch (YamlException ex)
			{
				throw new FormatException($"The edits block is not valid YAML: {ex.Message}");
			}

			if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlSequenceNode seq)
			{
				throw new FormatException("The edits block must be a YAML sequence of edit operations (each `- { op: ... }`).");
			}

			var ops = seq.Children
				.Select(node => node is YamlMappingNode map
					? ParseOp(map)
					: throw new FormatException("Each edit must be a mapping like `{ op: recolour, ... }`."))
				.ToList();

			return ops.Count > 0
				? ops
				: throw new FormatException("The edits block declared no operations.");
		}

		/// <summary>
		/// Applies every non-reauthor op in order via record <c>with</c> rewrites,
		/// returning the edited model, the (possibly recoloured) brief, and the
		/// re-author ops for the orchestrator to run. Referential mistakes — unknown
		/// part/key, a 13th colour, a remap on a script part, an off-plane centre
		/// move — throw with feedback. <see cref="ReplanOp"/> is a signal the caller
		/// intercepts; here it is a no-op.
		/// </summary>
		public static (VoxelRigModel Model, ReferenceBrief Brief, IReadOnlyList<ReauthorOp> Reauthors) Apply(
			VoxelRigModel model, ReferenceBrief brief, IReadOnlyList<ModelEditOp> ops)
		{
			var reauthors = new List<ReauthorOp>();
			foreach (var op in ops)
			{
				switch (op)
				{
					case RecolourOp recolour:
						(model, brief) = Recolour(model, brief, recolour);
						break;
					case AddColourOp add:
						model = AddColour(model, add);
						break;
					case RemapPartColourOp remap:
						model = Remap(model, remap);
						break;
					case MovePivotOp move:
						model = MovePivot(model, move);
						break;
					case MoveOffsetOp move:
						model = MoveOffset(model, move);
						break;
					case DeletePartOp delete:
						model = Delete(model, delete);
						break;
					case ReauthorOp reauthor:
						reauthors.Add(ValidateReauthor(model, reauthor));
						break;
					case ReplanOp:
						break;
				}
			}

			return (model, brief, reauthors);
		}

		/// <summary>The part ids whose geometry an op may have invalidated — the only parts the repair loop should re-author.</summary>
		public static IReadOnlyCollection<string> EditedPartIds(IReadOnlyList<ModelEditOp> ops) => ops
			.Select(op => op switch
			{
				RemapPartColourOp remap => remap.PartId,
				MovePivotOp move => move.PartId,
				MoveOffsetOp move => move.PartId,
				ReauthorOp reauthor => reauthor.PartId,
				_ => null,
			})
			.Where(id => id != null)
			.Select(id => id!)
			.ToHashSet();

		// ---- ops --------------------------------------------------------------

		private static (VoxelRigModel, ReferenceBrief) Recolour(VoxelRigModel model, ReferenceBrief brief, RecolourOp op)
		{
			if (model.Palette.All(e => e.Key != op.Key))
			{
				throw Referential($"recolour references palette key '{op.Key}', which the model does not declare.");
			}

			var palette = model.Palette.Select(e => e.Key == op.Key ? e with { Colour = op.Colour } : e).ToArray();
			if (brief.IsEmpty)
			{
				return (model with { Palette = palette }, brief);
			}

			// Keep the new colour in the brief palette too so CheckBriefPalette stays quiet.
			var briefPalette = brief.Palette.Any(e => e.Key == op.Key)
				? brief.Palette.Select(e => e.Key == op.Key ? e with { Colour = op.Colour } : e).ToArray()
				: brief.Palette.Append(new PaletteEntry(op.Key, op.Colour)).ToArray();
			return (model with { Palette = palette }, brief with { Palette = briefPalette });
		}

		private static VoxelRigModel AddColour(VoxelRigModel model, AddColourOp op)
		{
			if (model.Palette.Any(e => e.Key == op.Key))
			{
				throw Referential($"add_colour key '{op.Key}' already exists; use recolour to change it.");
			}

			if (model.Palette.Count >= 12)
			{
				throw Referential("add_colour would exceed the 12-colour palette limit; recolour or remap instead.");
			}

			return model with { Palette = model.Palette.Append(new PaletteEntry(op.Key, op.Colour)).ToArray() };
		}

		private static VoxelRigModel Remap(VoxelRigModel model, RemapPartColourOp op)
		{
			var part = model.FindPart(op.PartId) ?? throw Referential($"remap_colour names unknown part '{op.PartId}'.");
			if (model.Palette.All(e => e.Key != op.From))
			{
				throw Referential($"remap_colour `from` key '{op.From}' is not a declared palette key.");
			}

			if (model.Palette.All(e => e.Key != op.To))
			{
				throw Referential($"remap_colour `to` key '{op.To}' is not a declared palette key — add it first with add_colour.");
			}

			PartData data = part.Data switch
			{
				LayersPartData layers => layers with { Layers = layers.Layers.Select(l => l.Replace(op.From, op.To)).ToArray() },
				PrimitivesPartData prims => prims with { Shapes = prims.Shapes.Select(s => RemapShapeKey(s, op.From, op.To)).ToArray() },
				ScriptPartData => throw Referential($"remap_colour cannot rewrite script part '{op.PartId}'; use a reauthor edit."),
				MirrorPartData => throw Referential($"remap_colour targets mirror part '{op.PartId}'; recolour its authored source instead."),
				CopyPartData => throw Referential($"remap_colour targets copy part '{op.PartId}'; recolour its authored source instead."),
				_ => throw Referential($"remap_colour cannot edit the unauthored part '{op.PartId}'."),
			};

			return model.WithPartData(op.PartId, data);
		}

		private static VoxelRigModel MovePivot(VoxelRigModel model, MovePivotOp op)
		{
			var part = model.FindPart(op.PartId) ?? throw Referential($"move_pivot names unknown part '{op.PartId}'.");
			if (model.IsBilateral && op.Delta.x != 0 && PlanGeometryChecks.WorldPivot(model, part).x == 0)
			{
				throw Referential(
					$"move_pivot cannot shift centre part '{op.PartId}' along x: it sits on the bilateral mirror plane (x=0). " +
					"Move it in y/z only, or use a replan op for a structural change.");
			}

			var newPivot = part.Pivot + op.Delta;
			var parts = model.Parts.Select(p => p.Id == op.PartId
				? p with { Pivot = newPivot }
				: p.Data is MirrorPartData mirror && mirror.Source == op.PartId
					? p with { Pivot = ReflectPivot(newPivot, mirror.Axis) }
					: p).ToArray();
			return model with { Parts = parts };
		}

		private static VoxelRigModel MoveOffset(VoxelRigModel model, MoveOffsetOp op)
		{
			var part = model.FindPart(op.PartId) ?? throw Referential($"move_offset names unknown part '{op.PartId}'.");
			PartData data = part.Data switch
			{
				LayersPartData layers => layers with { Offset = layers.Offset + op.Delta },
				PrimitivesPartData prims => prims with { Offset = prims.Offset + op.Delta },
				ScriptPartData script => script with { Offset = script.Offset + op.Delta },
				PlannedPartData planned => planned with { Offset = planned.Offset + op.Delta },
				_ => throw Referential($"move_offset cannot shift part '{op.PartId}': mirror/copy parts have no offset of their own."),
			};

			return model.WithPartData(op.PartId, data);
		}

		private static VoxelRigModel Delete(VoxelRigModel model, DeletePartOp op)
		{
			if (model.FindPart(op.PartId) == null)
			{
				throw Referential($"delete names unknown part '{op.PartId}'.");
			}

			var removed = ClosureOf(model, op.PartId);
			var parts = model.Parts.Where(p => !removed.Contains(p.Id)).ToArray();
			if (parts.Length == 0)
			{
				throw Referential($"delete of '{op.PartId}' would remove every part; use a replan op instead.");
			}

			var poses = model.Poses
				.Select(pose => pose.Rotations.Keys.Any(removed.Contains)
					? new Pose(pose.Name, pose.Rotations.Where(kv => !removed.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value))
					: pose)
				.ToArray();
			return model with { Parts = parts, Poses = poses };
		}

		private static ReauthorOp ValidateReauthor(VoxelRigModel model, ReauthorOp op)
		{
			var part = model.FindPart(op.PartId) ?? throw Referential($"reauthor names unknown part '{op.PartId}'.");
			if (part.Data is MirrorPartData or CopyPartData)
			{
				throw Referential($"reauthor targets derived part '{op.PartId}'; reauthor its authored source instead.");
			}

			return op;
		}

		// ---- helpers ----------------------------------------------------------

		private static HashSet<string> ClosureOf(VoxelRigModel model, string rootId)
		{
			var removed = new HashSet<string> { rootId };
			for (var changed = true; changed;)
			{
				changed = false;
				foreach (var p in model.Parts)
				{
					if (removed.Contains(p.Id))
					{
						continue;
					}

					var dependsOnRemoved = removed.Contains(p.Parent) ||
										   (p.Data is MirrorPartData mirror && removed.Contains(mirror.Source)) ||
										   (p.Data is CopyPartData copy && removed.Contains(copy.Source));
					if (dependsOnRemoved)
					{
						removed.Add(p.Id);
						changed = true;
					}
				}
			}

			return removed;
		}

		private static Vector3Int ReflectPivot(Vector3Int pivot, MirrorAxis axis) => axis switch
		{
			MirrorAxis.X => new Vector3Int(-pivot.x, pivot.y, pivot.z),
			MirrorAxis.Y => new Vector3Int(pivot.x, -pivot.y, pivot.z),
			_ => new Vector3Int(pivot.x, pivot.y, -pivot.z),
		};

		/// <summary>A primitives line is `&lt;shape&gt; KEY ...`; only the KEY token is a palette reference.</summary>
		private static string RemapShapeKey(string shape, char from, char to)
		{
			var tokens = shape.Split(' ');
			var seen = 0;
			for (var i = 0; i < tokens.Length; i++)
			{
				if (tokens[i].Length == 0)
				{
					continue;
				}

				if (++seen == 2)
				{
					if (tokens[i] == from.ToString())
					{
						tokens[i] = to.ToString();
					}

					break;
				}
			}

			return string.Join(" ", tokens);
		}

		private static FormatException Referential(string message) => new(message);

		// ---- parsing ----------------------------------------------------------

		private static ModelEditOp ParseOp(YamlMappingNode map) => YamlNodes.GetString(map, "op") switch
		{
			"recolour" or "recolor" => new RecolourOp(Key(map, "key"), Colour(map)),
			"add_colour" or "add_color" => new AddColourOp(Key(map, "key"), Colour(map)),
			"remap_colour" or "remap_color" => new RemapPartColourOp(Part(map), Key(map, "from"), Key(map, "to")),
			"move_pivot" => new MovePivotOp(Part(map), Delta(map)),
			"move_offset" => new MoveOffsetOp(Part(map), Delta(map)),
			"reauthor" => new ReauthorOp(Part(map), YamlNodes.GetString(map, "instructions"), OptionalVector(map, "size"), OptionalVector(map, "offset")),
			"delete" => new DeletePartOp(Part(map)),
			"replan" => new ReplanOp(YamlNodes.GetString(map, "reason")),
			"" => throw new FormatException("An edit is missing its `op` field."),
			var other => throw new FormatException(
				$"Unknown edit op '{other}'. Valid ops: recolour, add_colour, remap_colour, move_pivot, move_offset, reauthor, delete, replan."),
		};

		private static string Part(YamlMappingNode map)
		{
			var part = YamlNodes.GetString(map, "part");
			return part.Length > 0 ? part : throw new FormatException("An edit is missing its `part` field.");
		}

		private static char Key(YamlMappingNode map, string field)
		{
			var key = YamlNodes.GetString(map, field);
			return key.Length == 1
				? key[0]
				: throw new FormatException($"Edit field `{field}` must be a single palette character but was '{key}'.");
		}

		private static Color32 Colour(YamlMappingNode map)
		{
			var hex = YamlNodes.GetString(map, "colour");
			if (hex.Length == 0)
			{
				hex = YamlNodes.GetString(map, "color");
			}

			if (hex.Length == 0)
			{
				throw new FormatException("A colour edit is missing its `colour` hex value.");
			}

			try
			{
				return PaletteEntry.ParseHex(hex);
			}
			catch (FormatException ex)
			{
				throw new FormatException($"A colour edit has a bad hex value: {ex.Message}");
			}
		}

		private static Vector3Int Delta(YamlMappingNode map) =>
			YamlNodes.Find(map, "delta") != null
				? YamlNodes.GetVector3Int(map, "delta", Vector3Int.zero)
				: throw new FormatException("A move edit is missing its `delta: [x, y, z]` field.");

		private static Vector3Int? OptionalVector(YamlMappingNode map, string key) =>
			YamlNodes.Find(map, key) != null ? YamlNodes.GetVector3Int(map, key, Vector3Int.zero) : null;
	}
}
