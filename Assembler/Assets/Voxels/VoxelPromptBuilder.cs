using System.IO;
using UnityEngine;

namespace Assembler.Voxels
{
	public static class VoxelPromptBuilder
	{
		private const string ResourceFolder = "GenerationPrompts";

		public static string Build()
		{
			var skill = LoadRequired("VoxelSkill");
			return skill;
		}

		private static string LoadRequired(string name)
		{
			var asset = Resources.Load<TextAsset>($"{ResourceFolder}/{name}");
			return asset != null
				? asset.text
				: throw new FileNotFoundException(
					$"Voxel prompt resource '{ResourceFolder}/{name}' is missing.");
		}
	}
}
