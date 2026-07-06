namespace Assembler.RemoteTooling;

/// <summary>
/// Drives the <c>claude</c> CLI to author (or revise) a descriptor via the <c>generate-game-descriptor</c>
/// skill. Runs plan-billed: <c>ANTHROPIC_API_KEY</c>/<c>ANTHROPIC_AUTH_TOKEN</c> are stripped so the CLI
/// uses the local Claude subscription rather than an API key.
/// </summary>
public static class GameGenerator
{
	private static readonly IReadOnlyDictionary<string, string?> PlanBilled = new Dictionary<string, string?>
	{
		["ANTHROPIC_API_KEY"] = null,
		["ANTHROPIC_AUTH_TOKEN"] = null,
	};

	public static string GenerateNew(string brief) => Invoke(
		$"Use the generate-game-descriptor skill to author a complete, runnable Assembler game descriptor "
		+ $"for this idea: \"{brief}\". Hard constraint: the game must use ONLY built-in primitive renderers — "
		+ "it must NOT declare a top-level Assets: block or reference any voxel/sprite/audio assets. "
		+ "It must declare a reachable !gameover path. Output ONLY the final YAML document, with no prose, "
		+ "no code fences, and nothing before or after it.");

	public static string Refine(string change, string currentDescriptor) => Invoke(
		$"Use the generate-game-descriptor skill to revise an existing Assembler game descriptor. "
		+ $"Apply this change: \"{change}\". Keep everything else intact. Hard constraints unchanged: "
		+ "built-in primitive renderers only (no Assets: block), and a reachable !gameover path must remain. "
		+ "Output ONLY the full revised YAML document — no prose, no code fences.\n\n"
		+ $"Current descriptor:\n{currentDescriptor}");

	private static string Invoke(string prompt)
	{
		var claude = Config.ClaudeBin;
		if (!Proc.Which(claude))
		{
			throw new AppException($"claude CLI not found (set CLAUDE_CLI_PATH; looked for '{claude}')");
		}

		var result = Proc.Capture(claude, ["-p", "--output-format", "text", prompt], env: PlanBilled);
		if (result.ExitCode != 0)
		{
			throw new AppException($"claude generation failed (exit {result.ExitCode}): {result.StdErr.Trim()}");
		}

		return result.StdOut;
	}
}
