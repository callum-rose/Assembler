using System.Collections.Generic;
using Assembler.Deserialisation.Dtos;

namespace Assembler.Generation.Verification
{
	public sealed record BuildResult(bool Success, IReadOnlyList<string> Errors, GameDto? ParsedDto);

	// Thin adapter over SandboxValidator that flattens the staged result into the flat shape the generation
	// loop consumes. Delegating here means the generator and the headless validate-game tool share one
	// pipeline (and one teardown), and the fix prompt gets each error tagged with the stage it came from.
	public static class BuildHarness
	{
		public static BuildResult TryBuild(string yaml)
		{
			var result = SandboxValidator.Validate(yaml);

			var errors = new List<string>();
			foreach (var stage in result.Stages)
			{
				if (!stage.Ran || stage.Success)
					continue;

				foreach (var error in stage.Errors)
					errors.Add($"[{stage.Stage}] {error}");
			}

			// ParsedDto is no longer surfaced (no caller reads it); kept on the record for source compat.
			return new BuildResult(result.Success, errors, null);
		}
	}
}
