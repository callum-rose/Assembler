using System.Collections.Generic;
using System.IO;
using System.Text;
using Assembler.Voxels;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>An exported model as relative-path → file bytes, ready to write under a folder named after the model.</summary>
	public sealed record ExportedModel(string Id, IReadOnlyDictionary<string, byte[]> Files)
	{
		public void WriteToDisk(string directory)
		{
			foreach (var kv in Files)
			{
				var path = Path.Combine(directory, kv.Key);
				var dir = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(dir))
				{
					Directory.CreateDirectory(dir);
				}

				File.WriteAllBytes(path, kv.Value);
			}
		}
	}

	/// <summary>
	/// Pure-code Stage 5. Always emits the rig yaml, a flattened Z-up .vox +
	/// Goxel text of the composed volume, and preview PNGs; rigged models
	/// additionally get one Z-up .vox per part so a runtime assembler can build
	/// the GameObject tree. All stored voxel data crosses the Y-up → Z-up
	/// boundary here and nowhere else.
	/// </summary>
	public static class ModelExporter
	{
		public static ExportedModel Export(AssembledModel assembled) => Export(assembled, ReferenceBrief.None);

		public static ExportedModel Export(AssembledModel assembled, ReferenceBrief brief)
		{
			var model = assembled.Model;

			// The vmodel yaml IS the raw generation data — it carries every
			// authored layers block and part script, so a model can be rebuilt
			// or hand-edited later. The brief rides alongside when present.
			var files = new Dictionary<string, byte[]>
			{
				[$"{model.Id}.vmodel.yaml"] = Utf8(VModelYaml.Write(model)),
			};

			if (!brief.IsEmpty)
			{
				files["reference_brief.yaml"] = Utf8(ReferenceBriefYaml.Write(brief));
			}

			if (assembled.Composed.Voxels.Count > 0)
			{
				var composedZUp = VoxelGridConvert.SwapYZ(assembled.Composed);
				files[$"{model.Id}.vox"] = VoxWriter.Write(composedZUp);
				files[$"{model.Id}.goxel.txt"] = Utf8(GoxelTextWriter.Write(composedZUp));
				files["preview_front.png"] = VoxelPreviewRenderer.RenderFront(assembled.Composed).EncodeToPNG();
				files["preview_iso.png"] = VoxelPreviewRenderer.RenderIso(assembled.Composed).EncodeToPNG();
			}

			if (model.Rigged)
			{
				foreach (var part in assembled.Parts)
				{
					if (part.Grid.Voxels.Count > 0)
					{
						files[$"parts/{part.Part.Id}.vox"] = VoxWriter.Write(VoxelGridConvert.SwapYZ(part.Grid));
					}
				}
			}

			return new ExportedModel(model.Id, files);
		}

		private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
	}
}
