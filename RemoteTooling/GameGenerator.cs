using System.Diagnostics;
using System.Text.Json;

namespace Assembler.RemoteTooling;

/// <summary>
/// Drives the <c>claude</c> CLI to author (or revise) a descriptor via the <c>generate-game-descriptor</c>
/// skill. Runs plan-billed: <c>ANTHROPIC_API_KEY</c>/<c>ANTHROPIC_AUTH_TOKEN</c> are stripped so the CLI
/// uses the local Claude subscription rather than an API key.
///
/// Generation is a multi-minute agentic run (the skill reads the behaviour catalogue, authors YAML and
/// iterates). We drive it with <c>--output-format stream-json</c> so each step is visible live — with
/// the default <c>text</c> format the CLI prints nothing at all until the whole run finishes, which
/// reads as a frozen terminal.
/// </summary>
public static class GameGenerator
{
	private static readonly IReadOnlyDictionary<string, string?> PlanBilled = new Dictionary<string, string?>
	{
		["ANTHROPIC_API_KEY"] = null,
		["ANTHROPIC_AUTH_TOKEN"] = null,
	};

	public static string GenerateNew(string brief, Logger log) => Invoke(
		$"Use the generate-game-descriptor skill to author a complete, runnable Assembler game descriptor "
		+ $"for this idea: \"{brief}\". Hard constraint: the game must use ONLY built-in primitive renderers — "
		+ "it must NOT declare a top-level Assets: block or reference any voxel/sprite/audio assets. "
		+ "It must declare a reachable !gameover path. Output ONLY the final YAML document, with no prose, "
		+ "no code fences, and nothing before or after it.",
		log);

	public static string Refine(string change, string currentDescriptor, Logger log) => Invoke(
		$"Use the generate-game-descriptor skill to revise an existing Assembler game descriptor. "
		+ $"Apply this change: \"{change}\". Keep everything else intact. Hard constraints unchanged: "
		+ "built-in primitive renderers only (no Assets: block), and a reachable !gameover path must remain. "
		+ "Output ONLY the full revised YAML document — no prose, no code fences.\n\n"
		+ $"Current descriptor:\n{currentDescriptor}",
		log);

	private static string Invoke(string prompt, Logger log)
	{
		var claude = Config.ClaudeBin;
		if (!Proc.Which(claude))
		{
			throw new AppException($"claude CLI not found (set CLAUDE_CLI_PATH; looked for '{claude}')");
		}

		log.Info("Calling claude (plan-billed) — this is a multi-minute agentic run; steps stream below:");
		var stopwatch = Stopwatch.StartNew();
		var state = new GenerationState();

		// Fallback heartbeat: if the model spends a long stretch thinking with no streamed step, still
		// show it's alive. Suppressed while steps are arriving so it doesn't double up on the event lines.
		using var heartbeat = new Timer(
			_ =>
			{
				if (!state.Done && stopwatch.ElapsedMilliseconds - Interlocked.Read(ref state.LastActivityMs) > 9_000)
				{
					log.Info($"  …still working ({stopwatch.Elapsed.TotalSeconds:F0}s elapsed)");
				}
			},
			state: null,
			dueTime: TimeSpan.FromSeconds(10),
			period: TimeSpan.FromSeconds(10));

		// --verbose is required to stream stream-json under --print.
		var exit = Proc.StreamLines(
			claude,
			["-p", "--output-format", "stream-json", "--verbose", prompt],
			PlanBilled,
			onStdout: line =>
			{
				Interlocked.Exchange(ref state.LastActivityMs, stopwatch.ElapsedMilliseconds);
				HandleEvent(line, log, state);
			},
			onStderr: log.Raw);

		heartbeat.Dispose();

		if (state.Result is not null)
		{
			log.Info($"claude finished in {stopwatch.Elapsed.TotalSeconds:F0}s.");
			return state.Result;
		}

		throw new AppException(state.Error is not null
			? $"claude generation failed: {state.Error}"
			: $"claude produced no result (exit {exit}) — no success event in the stream");
	}

	// Parse one line of claude's stream-json (newline-delimited JSON) and surface concise progress.
	// The final YAML arrives in the terminating "result" event's "result" field.
	private static void HandleEvent(string line, Logger log, GenerationState state)
	{
		line = line.Trim();
		if (line.Length == 0)
		{
			return;
		}

		JsonElement root;
		try
		{
			using var doc = JsonDocument.Parse(line);
			root = doc.RootElement.Clone();
		}
		catch (JsonException)
		{
			return; // non-JSON noise on stdout — ignore
		}

		if (root.ValueKind is not JsonValueKind.Object || !root.TryGetProperty("type", out var typeEl))
		{
			return;
		}

		switch (typeEl.GetString())
		{
			case "assistant" when root.TryGetProperty("message", out var message)
				&& message.TryGetProperty("content", out var content)
				&& content.ValueKind is JsonValueKind.Array:
				foreach (var block in content.EnumerateArray())
				{
					if (block.TryGetProperty("type", out var bt) && bt.GetString() == "tool_use")
					{
						log.Info($"  → {DescribeTool(block)}");
					}
				}

				break;

			case "rate_limit_event" when root.TryGetProperty("rate_limit_info", out var info)
				&& info.TryGetProperty("status", out var status)
				&& status.GetString() != "allowed":
				var reason = info.TryGetProperty("overageDisabledReason", out var r) && r.ValueKind is JsonValueKind.String
					? $" ({r.GetString()})"
					: "";
				log.Info($"  ⚠ rate limit: {status.GetString()}{reason}");
				break;

			case "result":
				var isError = root.TryGetProperty("is_error", out var err) && err.ValueKind is JsonValueKind.True;
				var subtype = root.TryGetProperty("subtype", out var st) ? st.GetString() : null;
				var text = root.TryGetProperty("result", out var res) && res.ValueKind is JsonValueKind.String
					? res.GetString()
					: null;
				state.Done = true;
				if (!isError && subtype == "success" && text is not null)
				{
					state.Result = text;
				}
				else
				{
					state.Error = text ?? subtype ?? "unknown error";
				}

				break;
		}
	}

	private static string DescribeTool(JsonElement toolUse)
	{
		var name = toolUse.TryGetProperty("name", out var n) ? n.GetString() ?? "?" : "?";
		if (toolUse.TryGetProperty("input", out var input) && input.ValueKind is JsonValueKind.Object)
		{
			foreach (var key in new[] { "skill", "command", "description", "file_path", "path", "pattern", "url" })
			{
				if (input.TryGetProperty(key, out var v) && v.ValueKind is JsonValueKind.String)
				{
					return $"{name}: {Truncate(v.GetString()!, 70)}";
				}
			}
		}

		return name;
	}

	private static string Truncate(string value, int max)
	{
		var oneLine = value.ReplaceLineEndings(" ");
		return oneLine.Length > max ? oneLine[..max] + "…" : oneLine;
	}

	private sealed class GenerationState
	{
		public string? Result;
		public string? Error;
		public bool Done;
		public long LastActivityMs;
	}
}
