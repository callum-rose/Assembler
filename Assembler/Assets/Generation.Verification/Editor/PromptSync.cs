#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assembler.Generation.Verification.Editor
{
	public static class PromptSync
	{
		private const string DestinationFolder = "Assets/Generation/Resources/GenerationPrompts";

		private readonly static (string Source, string DestName)[] Mappings =
		{
			(".claude/skills/generate-game-descriptor/SKILL.md", "Skill.txt"),
			("Assets/docs/Behaviours.md", "Behaviours.txt"),
			("Assets/Compiler/COMPILER_SYNTAX_REFERENCE.md", "CompilerSyntax.txt"),
		};

		[MenuItem("Assembler/Sync Generation Prompts")]
		public static void Sync()
		{
			Directory.CreateDirectory(DestinationFolder);

			var copied = 0;
			var skipped = 0;
			foreach (var (source, destName) in Mappings)
			{
				if (!File.Exists(source))
				{
					Debug.LogWarning($"PromptSync: source not found at '{source}', skipping.");
					skipped++;
					continue;
				}

				var dest = Path.Combine(DestinationFolder, destName);
				var sourceText = File.ReadAllText(source);
				if (File.Exists(dest) && File.ReadAllText(dest) == sourceText)
				{
					continue;
				}

				File.WriteAllText(dest, sourceText);
				copied++;
			}

			AssetDatabase.Refresh();
			Debug.Log($"PromptSync: copied {copied} file(s), skipped {skipped}. Destination: {DestinationFolder}");
		}
	}
}
#endif
