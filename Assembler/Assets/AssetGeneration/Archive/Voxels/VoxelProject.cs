using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assembler.Voxels
{
	/// <summary>
	/// Sidecar JSON saved next to a .vox file ({name}.voxproj). Captures the
	/// generation context — prompt, persistent instructions, history of
	/// generate/refine/edit steps — so iteration can resume after the window or
	/// the project is closed.
	/// </summary>
	[Serializable]
	public sealed class VoxelProject
	{
		public string prompt = string.Empty;
		public string persistentInstructions = string.Empty;
		public List<HistoryEntry> history = new();

		[Serializable]
		public sealed class HistoryEntry
		{
			/// <summary>"generate", "refine-fresh", "refine-chat", "manual-edit", "load".</summary>
			public string kind = string.Empty;
			public string prompt = string.Empty;
			public string goxelText = string.Empty;
			public string timestampIso = string.Empty;

			public static HistoryEntry Create(string kind, string prompt, string goxelText)
			{
				return new HistoryEntry
				{
					kind = kind,
					prompt = prompt ?? string.Empty,
					goxelText = goxelText ?? string.Empty,
					timestampIso = DateTime.UtcNow.ToString("o"),
				};
			}
		}

		public static VoxelProject Load(string path)
		{
			var json = File.ReadAllText(path);
			var project = JsonUtility.FromJson<VoxelProject>(json);
			if (project == null)
			{
				throw new InvalidDataException(".voxproj could not be parsed.");
			}

			project.history ??= new List<HistoryEntry>();
			return project;
		}

		public static void Save(string path, VoxelProject project)
		{
			var json = JsonUtility.ToJson(project, prettyPrint: true);
			File.WriteAllText(path, json);
		}

		public static string SidecarPathFor(string voxPath)
		{
			var dir = Path.GetDirectoryName(voxPath) ?? string.Empty;
			var name = Path.GetFileNameWithoutExtension(voxPath);
			return Path.Combine(dir, name + ".voxproj");
		}
	}
}
