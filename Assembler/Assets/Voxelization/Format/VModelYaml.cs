using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using YamlDotNet.RepresentationModel;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Reads and writes the *.vmodel.yaml format (§4 of the design): palette,
	/// part tree with layers/script/mirror data blocks, and named poses. A
	/// mirror part may omit its pivot, in which case the source pivot is
	/// reflected (component on the mirror axis negated) at read time, so the
	/// in-memory model always carries explicit pivots.
	/// </summary>
	public static class VModelYaml
	{
		public static VoxelRigModel Read(string text)
		{
			var root = YamlNodes.ParseRoot(text);

			var palette = ReadPalette(root);
			var parts = ReadParts(root);
			var poses = ReadPoses(root);

			return new VoxelRigModel
			{
				Id = YamlNodes.GetString(root, "model"),
				Version = YamlNodes.GetInt(root, "version", 1),
				Rigged = YamlNodes.GetBool(root, "rigged"),
				Symmetry = YamlNodes.GetString(root, "symmetry", "none"),
				Unit = YamlNodes.GetFloat(root, "unit", 0.1f),
				RealWorldHeight = YamlNodes.GetFloat(root, "real_world_height"),
				Origin = YamlNodes.GetString(root, "origin", "feet_center"),
				Palette = palette,
				Parts = parts,
				Poses = poses,
			};
		}

		public static string Write(VoxelRigModel model)
		{
			var sb = new StringBuilder();
			sb.Append("model: ").Append(model.Id).Append('\n');
			sb.Append("version: ").Append(model.Version).Append('\n');
			sb.Append("rigged: ").Append(model.Rigged ? "true" : "false").Append('\n');
			sb.Append("symmetry: ").Append(model.Symmetry).Append('\n');
			sb.Append("unit: ").Append(YamlNodes.Float(model.Unit)).Append('\n');
			sb.Append("real_world_height: ").Append(YamlNodes.Float(model.RealWorldHeight)).Append('\n');
			sb.Append("origin: ").Append(model.Origin).Append('\n');

			sb.Append("palette:\n");
			sb.Append("  _: none\n");
			foreach (var entry in model.Palette)
			{
				sb.Append("  ").Append(entry.Key).Append(": ").Append(YamlNodes.Quote(entry.ToHex())).Append('\n');
			}

			sb.Append("parts:\n");
			foreach (var part in model.Parts)
			{
				WritePart(sb, part);
			}

			sb.Append("poses:\n");
			foreach (var pose in model.Poses)
			{
				WritePose(sb, pose);
			}

			return sb.ToString();
		}

		private static void WritePart(StringBuilder sb, VoxelPart part)
		{
			sb.Append("  - id: ").Append(part.Id).Append('\n');
			sb.Append("    parent: ").Append(part.Parent).Append('\n');
			sb.Append("    pivot: ").Append(YamlNodes.Vector(part.Pivot)).Append('\n');
			if (part.Loose)
			{
				sb.Append("    loose: true\n");
			}

			switch (part.Data)
			{
				case MirrorPartData mirror:
					sb.Append("    mirror: { source: ").Append(mirror.Source)
						.Append(", axis: ").Append(mirror.Axis.ToString().ToLowerInvariant()).Append(" }\n");
					break;
				case CopyPartData copy:
					sb.Append("    copy: { source: ").Append(copy.Source).Append(" }\n");
					break;
				case LayersPartData layers:
					sb.Append("    data:\n");
					sb.Append("      encoding: layers\n");
					sb.Append("      size: ").Append(YamlNodes.Vector(layers.Size)).Append('\n');
					sb.Append("      offset: ").Append(YamlNodes.Vector(layers.Offset)).Append('\n');
					sb.Append("      layers:\n");
					foreach (var layer in layers.Layers)
					{
						YamlNodes.AppendBlockScalar(sb, "        - ", layer, 10);
					}
					break;
				case ScriptPartData script:
					sb.Append("    data:\n");
					sb.Append("      encoding: script\n");
					sb.Append("      size: ").Append(YamlNodes.Vector(script.Size)).Append('\n');
					sb.Append("      offset: ").Append(YamlNodes.Vector(script.Offset)).Append('\n');
					YamlNodes.AppendBlockScalar(sb, "      source: ", script.Source, 8);
					break;
				case PrimitivesPartData primitives:
					sb.Append("    data:\n");
					sb.Append("      encoding: primitives\n");
					sb.Append("      size: ").Append(YamlNodes.Vector(primitives.Size)).Append('\n');
					sb.Append("      offset: ").Append(YamlNodes.Vector(primitives.Offset)).Append('\n');
					sb.Append("      shapes:\n");
					foreach (var shape in primitives.Shapes)
					{
						sb.Append("        - ").Append(YamlNodes.Quote(shape)).Append('\n');
					}
					break;
				case PlannedPartData planned:
					sb.Append("    data:\n");
					sb.Append("      encoding: planned\n");
					sb.Append("      planned: ").Append(planned.PlannedEncoding.ToString().ToLowerInvariant()).Append('\n');
					sb.Append("      size: ").Append(YamlNodes.Vector(planned.Size)).Append('\n');
					sb.Append("      offset: ").Append(YamlNodes.Vector(planned.Offset)).Append('\n');
					sb.Append("      note: ").Append(YamlNodes.Quote(planned.Note)).Append('\n');
					break;
			}
		}

		private static void WritePose(StringBuilder sb, Pose pose)
		{
			if (pose.Rotations.Count == 0)
			{
				sb.Append("  ").Append(pose.Name).Append(": {}\n");
				return;
			}

			var entries = pose.Rotations.Select(kv => $"{kv.Key}: {YamlNodes.Vector(kv.Value)}");
			sb.Append("  ").Append(pose.Name).Append(": { ").Append(string.Join(", ", entries)).Append(" }\n");
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
					throw new FormatException($"Palette key '{key}' must be a single character.");
				}

				if (key[0] is PaletteEntry.EmptyKey or PaletteEntry.EmptyCell)
				{
					continue;
				}

				entries.Add(new PaletteEntry(key[0], PaletteEntry.ParseHex(YamlNodes.ScalarValue(kv.Value))));
			}

			return entries;
		}

		private static IReadOnlyList<VoxelPart> ReadParts(YamlMappingNode root)
		{
			var parts = new List<VoxelPart>();
			if (YamlNodes.Find(root, "parts") is not YamlSequenceNode seq)
			{
				return parts;
			}

			var pivotDeclared = new Dictionary<string, bool>();
			foreach (var node in seq.Children)
			{
				if (node is not YamlMappingNode partMap)
				{
					throw new FormatException("Each entry under 'parts' must be a mapping.");
				}

				var id = YamlNodes.GetString(partMap, "id");
				if (id.Length == 0)
				{
					throw new FormatException("A part is missing its 'id'.");
				}

				if (parts.Any(p => p.Id == id))
				{
					throw new FormatException($"Duplicate part id '{id}'.");
				}

				pivotDeclared[id] = YamlNodes.Find(partMap, "pivot") != null;
				parts.Add(new VoxelPart
				{
					Id = id,
					Parent = YamlNodes.GetString(partMap, "parent", VoxelRigModel.RootId),
					Pivot = YamlNodes.GetVector3Int(partMap, "pivot", Vector3Int.zero),
					Loose = YamlNodes.GetBool(partMap, "loose"),
					Data = ReadPartData(partMap, id),
				});
			}

			return ResolveMirrorPivots(parts, pivotDeclared);
		}

		private static PartData ReadPartData(YamlMappingNode partMap, string id)
		{
			if (YamlNodes.Find(partMap, "mirror") is YamlMappingNode mirrorMap)
			{
				var source = YamlNodes.GetString(mirrorMap, "source");
				if (source.Length == 0)
				{
					throw new FormatException($"Part '{id}': mirror is missing its 'source'.");
				}

				return new MirrorPartData(source, ParseAxis(YamlNodes.GetString(mirrorMap, "axis", "x"), id));
			}

			if (YamlNodes.Find(partMap, "copy") is YamlMappingNode copyMap)
			{
				var source = YamlNodes.GetString(copyMap, "source");
				if (source.Length == 0)
				{
					throw new FormatException($"Part '{id}': copy is missing its 'source'.");
				}

				return new CopyPartData(source);
			}

			if (YamlNodes.Find(partMap, "data") is not YamlMappingNode dataMap)
			{
				throw new FormatException($"Part '{id}' has neither 'data', 'mirror', nor 'copy'.");
			}

			var size = YamlNodes.GetVector3Int(dataMap, "size", Vector3Int.one);
			var offset = YamlNodes.GetVector3Int(dataMap, "offset", Vector3Int.zero);

			return YamlNodes.GetString(dataMap, "encoding") switch
			{
				"layers" => new LayersPartData(size, offset, ReadLayers(dataMap, id)),
				"script" => new ScriptPartData(size, offset, YamlNodes.GetString(dataMap, "source")),
				"primitives" => new PrimitivesPartData(size, offset, ReadShapes(dataMap, id)),
				"planned" => new PlannedPartData(
					ParsePlannedEncoding(YamlNodes.GetString(dataMap, "planned", "layers")),
					size,
					offset,
					YamlNodes.GetString(dataMap, "note")),
				var other => throw new FormatException($"Part '{id}': unknown encoding '{other}'."),
			};
		}

		private static PartEncoding ParsePlannedEncoding(string planned) => planned switch
		{
			"script" => PartEncoding.Script,
			"primitives" => PartEncoding.Primitives,
			_ => PartEncoding.Layers,
		};

		private static IReadOnlyList<string> ReadShapes(YamlMappingNode dataMap, string id)
		{
			if (YamlNodes.Find(dataMap, "shapes") is not YamlSequenceNode seq)
			{
				throw new FormatException($"Part '{id}': primitives encoding requires a 'shapes' sequence.");
			}

			return seq.Children.Select(YamlNodes.ScalarValue).ToList();
		}

		private static IReadOnlyList<string> ReadLayers(YamlMappingNode dataMap, string id)
		{
			if (YamlNodes.Find(dataMap, "layers") is not YamlSequenceNode seq)
			{
				throw new FormatException($"Part '{id}': layers encoding requires a 'layers' sequence.");
			}

			return seq.Children.Select(YamlNodes.ScalarValue).ToList();
		}

		private static MirrorAxis ParseAxis(string axis, string id) => axis.ToLowerInvariant() switch
		{
			"x" => MirrorAxis.X,
			"y" => MirrorAxis.Y,
			"z" => MirrorAxis.Z,
			var other => throw new FormatException($"Part '{id}': unknown mirror axis '{other}'."),
		};

		private static IReadOnlyList<VoxelPart> ResolveMirrorPivots(List<VoxelPart> parts, IReadOnlyDictionary<string, bool> pivotDeclared)
		{
			return parts
				.Select(part => part.Data is MirrorPartData mirror && !pivotDeclared[part.Id]
					? part with { Pivot = MirroredPivot(part, mirror, parts) }
					: part)
				.ToList();
		}

		private static Vector3Int MirroredPivot(VoxelPart part, MirrorPartData mirror, List<VoxelPart> parts)
		{
			var source = parts.FirstOrDefault(p => p.Id == mirror.Source)
						 ?? throw new FormatException($"Part '{part.Id}': mirror source '{mirror.Source}' not found.");
			var pivot = source.Pivot;
			return mirror.Axis switch
			{
				MirrorAxis.X => new Vector3Int(-pivot.x, pivot.y, pivot.z),
				MirrorAxis.Y => new Vector3Int(pivot.x, -pivot.y, pivot.z),
				_ => new Vector3Int(pivot.x, pivot.y, -pivot.z),
			};
		}

		private static IReadOnlyList<Pose> ReadPoses(YamlMappingNode root)
		{
			var poses = new List<Pose>();
			if (YamlNodes.Find(root, "poses") is not YamlMappingNode posesMap)
			{
				return poses;
			}

			foreach (var kv in posesMap.Children)
			{
				var name = YamlNodes.ScalarValue(kv.Key);
				var rotations = new Dictionary<string, Vector3>();
				if (kv.Value is YamlMappingNode poseMap)
				{
					foreach (var entry in poseMap.Children)
					{
						rotations[YamlNodes.ScalarValue(entry.Key)] =
							YamlNodes.GetVector3(poseMap, YamlNodes.ScalarValue(entry.Key), Vector3.zero);
					}
				}

				poses.Add(new Pose(name, rotations));
			}

			return poses;
		}
	}
}
