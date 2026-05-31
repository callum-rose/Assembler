using System.IO;
using UnityEngine;

namespace Assembler.Voxels
{
	public static class VoxelPromptBuilder
	{
		private const string ResourceFolder = "GenerationPrompts";

		public static string Build()
		{
			return Build(includeScriptSkill: false);
		}

		/// <summary>
		/// Builds the system prompt. When <paramref name="includeScriptSkill"/> is
		/// true (a script executor is wired, so the <c>run_voxel_script</c> tool is
		/// offered) the procedural-scripting skill is appended so Claude knows the
		/// VoxelBuilder API and when to prefer it over direct goxel text.
		/// </summary>
		public static string Build(bool includeScriptSkill)
		{
			var skill = LoadRequired("VoxelSkill");
			if (!includeScriptSkill)
			{
				return skill;
			}

			var scriptSkill = LoadRequired("VoxelScriptSkill");
			return skill + "\n\n" + scriptSkill;
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
