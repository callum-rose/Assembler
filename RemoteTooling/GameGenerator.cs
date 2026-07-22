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

	// The model emits the descriptor first, then this delimiter on its own line, then any catalogue
	// feedback the generate-game-descriptor skill volunteered. Chosen to never occur in valid YAML.
	private const string FeedbackDelimiter = "===BEHAVIOUR CATALOGUE FEEDBACK===";

	// Prepended to every prompt. The generate-game-descriptor skill makes it a *hard requirement* to
	// self-verify by writing the YAML to disk and running the headless validators (validate-game.sh /
	// check-expression.sh). That path is both impossible and undesirable here: this runs non-interactively
	// under `claude -p` with only read tools granted (writes and Bash are denied), and — even if they
	// weren't — N concurrent generation runs each booting validate-game.sh would race the one Unity project
	// Library and corrupt it (why PublishCommand serialises validation behind a single gate). The daemon
	// validates the output itself afterwards and loops the report back via Fix, so the model must NOT try
	// to self-validate. Without this override the model follows the skill, hits the write/Bash denials, and
	// gives up by emitting a prose apology — which is then captured as the "descriptor" and fails validation
	// at line 1 (the failure this preamble exists to prevent).
	private const string OperatingConstraints =
		"OPERATING CONTEXT — read first, it overrides the skill's self-verification requirement: You are "
		+ "running non-interactively with read-only tools. You CAN read files (the behaviour catalogue, the "
		+ "schema, the libraries doc, example descriptors) — do that to author correctly. You CANNOT write "
		+ "files and CANNOT run any command, script or Bash. Do NOT write a descriptor file, and do NOT run "
		+ "the headless validators (validate-game.sh / check-expression.sh) or any other tool to self-verify "
		+ "— those are unavailable here and attempting them only wastes the run. Author the descriptor purely "
		+ "by reasoning against the catalogue docs and output it as text. It is validated externally after you "
		+ "finish; if it fails to build you will be handed the validator's report to correct and try again. Do "
		+ "not refuse, stall, or apologise about being unable to self-validate — always produce your complete "
		+ "best-effort YAML.\n\n";

	public static GenerationResult GenerateNew(string brief, Logger log) => Invoke(
		$"Use the generate-game-descriptor skill to author a complete, runnable Assembler game descriptor "
		+ $"for this idea: \"{brief}\". Hard constraint: the game must use ONLY built-in primitive renderers — "
		+ "it must NOT declare a top-level Assets: block or reference any voxel/sprite/audio assets. "
		+ "It must declare a reachable !gameover path. "
		+ "First output the final YAML document — no prose, no code fences, nothing before it. "
		+ $"Then, on its own line, output the exact delimiter {FeedbackDelimiter} followed by any feedback the "
		+ "skill surfaces about the behaviour catalogue (missing, faulty, awkwardly-composed or misnamed "
		+ "behaviours you hit while authoring this game), or the single word none if you have nothing to report.",
		log);

	public static GenerationResult Fix(string validationReport, string currentDescriptor, Logger log) => Invoke(
		"Use the generate-game-descriptor skill to fix an existing Assembler game descriptor that FAILED sandbox "
		+ "validation. Below is the descriptor and the validator's report — it pinpoints the stage that broke "
		+ "(structure → deserialise → parse → resolve → instantiate) and the thrown exception or logged error. "
		+ "Diagnose the reported failure and correct the descriptor so it builds cleanly. Change only what the "
		+ "errors require; keep the game's intent intact. Hard constraints unchanged: built-in primitive renderers "
		+ "only (no Assets: block), and a reachable !gameover path must remain. "
		+ "First output the full corrected YAML document — no prose, no code fences, nothing before it. "
		+ $"Then, on its own line, output the exact delimiter {FeedbackDelimiter} followed by any feedback the "
		+ "skill surfaces about the behaviour catalogue, or the single word none if you have nothing to report.\n\n"
		+ $"Validation report:\n{validationReport}\n\n"
		+ $"Current descriptor:\n{currentDescriptor}",
		log);

	public static GenerationResult Refine(string change, string currentDescriptor, Logger log) => Invoke(
		$"Use the generate-game-descriptor skill to revise an existing Assembler game descriptor. "
		+ $"Apply this change: \"{change}\". Keep everything else intact. Hard constraints unchanged: "
		+ "built-in primitive renderers only (no Assets: block), and a reachable !gameover path must remain. "
		+ "First output the full revised YAML document — no prose, no code fences, nothing before it. "
		+ $"Then, on its own line, output the exact delimiter {FeedbackDelimiter} followed by any feedback the "
		+ "skill surfaces about the behaviour catalogue, or the single word none if you have nothing to report.\n\n"
		+ $"Current descriptor:\n{currentDescriptor}",
		log);

	private static GenerationResult Invoke(string prompt, Logger log)
	{
		prompt = OperatingConstraints + prompt;
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
			return Split(state.Result);
		}

		throw new AppException(state.Error is not null
			? $"claude generation failed: {state.Error}"
			: $"claude produced no result (exit {exit}) — no success event in the stream");
	}

	// Split the model's final message into descriptor + optional catalogue feedback on the LAST delimiter
	// (so a stray earlier match can't truncate the descriptor). With no delimiter — an older model slip —
	// the whole payload is the descriptor and there's no feedback, preserving the pure-YAML contract.
	private static GenerationResult Split(string raw)
	{
		var lines = raw.Replace("\r\n", "\n").Split('\n');
		var marker = Array.FindLastIndex(lines, line => line.Trim() == FeedbackDelimiter);
		if (marker < 0)
		{
			return new GenerationResult(raw.Trim(), null);
		}

		var descriptor = string.Join('\n', lines[..marker]).Trim();
		var feedback = string.Join('\n', lines[(marker + 1)..]).Trim();
		return new GenerationResult(
			descriptor,
			feedback.Length == 0 || feedback.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : feedback);
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

/// <summary>A generated descriptor plus any behaviour-catalogue feedback the skill volunteered (null when
/// it had nothing to report).</summary>
public sealed record GenerationResult(string Descriptor, string? Feedback);
