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
			var libraries = LoadRequired("Libraries");
			var compilerSyntax = LoadRequired("CompilerSyntax");

			return $$"""
			         You are generating YAML game descriptors for the Assembler Unity framework.

			         Reply with EXACTLY two fenced code blocks, in this order and nothing else:
			         1. A ```yaml block containing the complete game descriptor.
			         2. A ```feedback block containing free-form commentary about the request,
			            missing behaviours, awkward authoring, or suggestions. May be empty but the
			            block must still be present.

			         ==== SKILL: generate-game-descriptor ====
			         {{skill}}

			         ==== BEHAVIOUR CATALOGUE ====
			         {{behaviours}}

			         ==== EXPRESSION COMPILER SYNTAX REFERENCE ====
			         {{compilerSyntax}}

			         ==== LIBRARY CATALOGUE ====
			         {{libraries}}
			         """;
		}

		private static string LoadRequired(string name)
		{
			var asset = Resources.Load<TextAsset>($"{ResourceFolder}/{name}");
			return asset != null
				? asset.text
				: throw new System.IO.FileNotFoundException(
					$"Generation prompt resource '{ResourceFolder}/{name}' is missing. " +
					"Run 'Assembler > Sync Generation Prompts' to populate it.");
		}
	}
}