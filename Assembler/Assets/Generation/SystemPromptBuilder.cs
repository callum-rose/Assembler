using System.Text;
using UnityEngine;

namespace Assembler.Generation
{
	public static class SystemPromptBuilder
	{
		private const string ResourceFolder = "GenerationPrompts";

		public static string Build()
		{
			var skill = LoadRequired("Skill");
			var behaviours = LoadRequired("Behaviours");
			var compilerSyntax = LoadRequired("CompilerSyntax");

			var sb = new StringBuilder();
			sb.AppendLine("You are generating YAML game descriptors for the Assembler Unity framework.");
			sb.AppendLine();
			sb.AppendLine("Reply with EXACTLY two fenced code blocks, in this order and nothing else:");
			sb.AppendLine("1. A ```yaml block containing the complete game descriptor.");
			sb.AppendLine("2. A ```feedback block containing free-form commentary about the request,");
			sb.AppendLine("   missing behaviours, awkward authoring, or suggestions. May be empty but the");
			sb.AppendLine("   block must still be present.");
			sb.AppendLine();
			sb.AppendLine("==== SKILL: generate-game-descriptor ====");
			sb.AppendLine(skill);
			sb.AppendLine();
			sb.AppendLine("==== BEHAVIOUR CATALOGUE ====");
			sb.AppendLine(behaviours);
			sb.AppendLine();
			sb.AppendLine("==== EXPRESSION COMPILER SYNTAX REFERENCE ====");
			sb.AppendLine(compilerSyntax);
			return sb.ToString();
		}

		private static string LoadRequired(string name)
		{
			var asset = Resources.Load<TextAsset>($"{ResourceFolder}/{name}");
			if (asset == null)
			{
				throw new System.IO.FileNotFoundException(
					$"Generation prompt resource '{ResourceFolder}/{name}' is missing. " +
					"Run 'Assembler > Sync Generation Prompts' to populate it.");
			}
			return asset.text;
		}
	}
}
