using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxelization.Editor
{
	/// <summary>
	/// Drop-in <see cref="IAnthropicGateway"/> that funnels every pipeline LLM call
	/// through the <c>claude -p</c> CLI instead of the Anthropic API, so a run bills
	/// the operator's Claude subscription (plan) rather than API credits. Functional
	/// equivalence is the goal: <c>--system-prompt</c> fully replaces the agent's
	/// system prompt with the voxel stage prompt, and <c>--tools ""</c> neutralises
	/// the harness so each call is a single bare model turn — the same shape as the
	/// API gateway's per-call model.
	///
	/// Three transport shapes, matching the three ways the pipeline calls in:
	/// • text-only stages → one <c>claude -p</c> with tools disabled;
	/// • image stages (a message carries an <see cref="AnthropicImage"/>) → temp PNGs
	///   plus <c>--allowedTools Read --add-dir</c> so the model reads then answers;
	/// • the script stage (tools + <c>onToolUse</c>) → a <c>--resume</c> text loop that
	///   emulates the native tool loop, parsing the model's emitted script out of a
	///   fenced block and driving the same <see cref="Assembler.Voxels.Scripting.IVoxelScriptExecutor"/> in-process.
	///
	/// Deliberately BCL-only (<see cref="Process"/> + <see cref="JsonSerializer"/> +
	/// the gateway's own domain types — no <c>UnityEditor</c>/<c>UnityEngine</c>) so it
	/// can be lifted out of the editor assembly into a portable console host later.
	/// </summary>
	public sealed class ClaudeCliGateway : IAnthropicGateway
	{
		/// <summary>Default cap on concurrent <c>claude -p</c> child processes (Decision: concurrency).</summary>
		public const int DefaultConcurrency = 3;

		private readonly TokenUsageTracker _usage;
		private readonly Action<string>? _onActivity;
		private readonly IProgress<string>? _onTranscript;
		private readonly SemaphoreSlim _throttle;
		private readonly string _binary;
		private readonly string _workingDir;
		private int _callCounter;

		public ClaudeCliGateway(
			TokenUsageTracker usage,
			int concurrency = DefaultConcurrency,
			Action<string>? onActivity = null,
			IProgress<string>? onTranscript = null,
			string? claudeBinaryPath = null)
		{
			_usage = usage;
			_onActivity = onActivity;
			_onTranscript = onTranscript;
			_throttle = new SemaphoreSlim(Math.Max(1, concurrency));
			_binary = ResolveBinary(claudeBinaryPath);

			// A neutral, project-free working directory so the spawned CLI does not
			// auto-discover the Unity project's CLAUDE.md / .mcp.json (which would
			// drift the prompt from the API path); --system-prompt already replaces
			// the system prompt, but an empty cwd keeps the rest clean too.
			_workingDir = Path.Combine(Path.GetTempPath(), "voxelize-cli-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_workingDir);
		}

		public async Task<string> SendAsync(
			string stage,
			string model,
			string systemPrompt,
			IReadOnlyList<AnthropicMessage> messages,
			CancellationToken ct,
			IReadOnlyList<AnthropicTool>? tools = null,
			Func<AnthropicToolUse, CancellationToken, Task<AnthropicToolResult>>? onToolUse = null,
			int maxToolIterations = AnthropicClient.DefaultMaxToolIterations)
		{
			var alias = ModelAlias(model);
			return tools is { Count: > 0 } && onToolUse != null
				? await RunScriptLoopAsync(stage, alias, systemPrompt, messages, tools[0], onToolUse, maxToolIterations, ct)
					.ConfigureAwait(false)
				: await RunSingleAsync(stage, alias, systemPrompt, messages, ct).ConfigureAwait(false);
		}

		/// <summary>
		/// A single bare model turn. When any message carries an image the call gets
		/// the Read tool and the temp image directory so the model can view the PNGs
		/// (mirroring how the API path attaches image blocks); otherwise all tools are
		/// disabled so the harness can never wander off into an agent loop.
		/// </summary>
		private async Task<string> RunSingleAsync(
			string stage, string alias, string systemPrompt, IReadOnlyList<AnthropicMessage> messages, CancellationToken ct)
		{
			var callId = Interlocked.Increment(ref _callCounter);
			var images = messages.SelectMany(m => m.Images).Where(img => !img.IsEmpty).ToList();
			var imageDir = images.Count > 0 ? Path.Combine(_workingDir, $"img-{callId}") : string.Empty;
			var imagePaths = WriteImages(images, imageDir);
			var prompt = RenderPrompt(messages, imagePaths);

			var args = new List<string> { "-p", "--model", alias, "--output-format", "json", "--system-prompt", systemPrompt };
			if (imagePaths.Count > 0)
			{
				args.AddRange(new[] { "--allowedTools", "Read", "--add-dir", imageDir });
			}
			else
			{
				// --tools "" disables every built-in tool: there is no run_voxel_script
				// equivalent here, so the model answers in one turn with no harness.
				args.AddRange(new[] { "--tools", string.Empty });
			}

			_onActivity?.Invoke($"{stage}: calling claude ({alias})...");
			var envelope = await SpawnAsync(args, prompt, stage, alias, ct).ConfigureAwait(false);
			_usage.Record(stage, envelope.Usage);
			EmitTranscript(callId, stage, alias, systemPrompt, prompt, imagePaths, string.Empty, envelope);
			return envelope.Result;
		}

		/// <summary>
		/// Emulates the native run_voxel_script tool loop over a stateless CLI: the
		/// model is told (in an appended system-prompt addendum) to emit its script as
		/// one fenced ```json block instead of calling the absent tool. The first call
		/// establishes a session; each follow-up <c>--resume</c>s it. Every emitted
		/// script is run through the real <paramref name="onToolUse"/> (the production
		/// <see cref="Assembler.Voxels.Scripting.IVoxelScriptExecutor"/>) in-process, so <see cref="PartAuthor"/>
		/// is unchanged. The loop stops on the first non-error tool result, else feeds
		/// the executor's error back as the next turn, up to the same iteration budget.
		/// </summary>
		private async Task<string> RunScriptLoopAsync(
			string stage,
			string alias,
			string systemPrompt,
			IReadOnlyList<AnthropicMessage> messages,
			AnthropicTool tool,
			Func<AnthropicToolUse, CancellationToken, Task<AnthropicToolResult>> onToolUse,
			int maxToolIterations,
			CancellationToken ct)
		{
			var callId = Interlocked.Increment(ref _callCounter);
			var system = systemPrompt + "\n\n" + ToolEmulationAddendum(tool);
			var prompt = RenderPrompt(messages, Array.Empty<string>());
			var toolLog = new StringBuilder();
			var lastResponse = string.Empty;
			var totalUsage = AnthropicTokenUsage.Zero;
			var sessionId = string.Empty;

			for (var iteration = 0; iteration < Math.Max(1, maxToolIterations); iteration++)
			{
				// First turn carries the (augmented) system prompt and disables tools;
				// every later turn resumes the same session so prior context is retained.
				var args = new List<string> { "-p", "--model", alias, "--output-format", "json", "--tools", string.Empty };
				if (sessionId.Length == 0)
				{
					args.AddRange(new[] { "--system-prompt", system });
				}
				else
				{
					args.AddRange(new[] { "--resume", sessionId });
				}

				_onActivity?.Invoke($"{stage}: calling claude ({alias}, script turn {iteration + 1})...");
				var envelope = await SpawnAsync(args, prompt, stage, alias, ct).ConfigureAwait(false);
				sessionId = envelope.SessionId.Length > 0 ? envelope.SessionId : sessionId;
				_usage.Record(stage, envelope.Usage);
				totalUsage = totalUsage.Add(envelope.Usage);
				lastResponse = envelope.Result;

				if (!TryExtractScriptToolUse(envelope.Result, tool.Name, callId, iteration, out var use))
				{
					prompt = "I could not find your script. Emit exactly one fenced ```json block whose body is " +
							 "a JSON object with a single \"script\" string field, e.g.\n```json\n{\"script\": \"...return b.Build();\"}\n```";
					continue;
				}

				var result = await onToolUse(use, ct).ConfigureAwait(false);
				AppendToolInteraction(toolLog, use, result);
				if (!result.IsError)
				{
					break;
				}

				prompt = $"That script failed: {result.Content}\nFix it and emit a corrected ```json block " +
						 "with the single \"script\" field.";
			}

			EmitTranscript(callId, stage, alias, system, "(script tool loop — see tool calls)", Array.Empty<string>(),
				toolLog.ToString(), new CliResult(lastResponse, false, "success", sessionId, totalUsage));
			return lastResponse;
		}

		/// <summary>
		/// Spawns one <c>claude -p</c>, feeds <paramref name="prompt"/> on stdin, and
		/// parses the JSON envelope. Throttled by the shared semaphore so no more than
		/// the configured number of CLI processes run at once. The child's environment
		/// has any API-key variables stripped defensively, so it can only authenticate
		/// via the plan's OAuth — never silently fall back to billed API credits.
		/// </summary>
		private async Task<CliResult> SpawnAsync(
			IReadOnlyList<string> args, string prompt, string stage, string alias, CancellationToken ct)
		{
			await _throttle.WaitAsync(ct).ConfigureAwait(false);
			try
			{
				var psi = new ProcessStartInfo
				{
					FileName = _binary,
					WorkingDirectory = _workingDir,
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					// The prompts carry emoji silhouettes and box-drawing; pin UTF-8 on
					// every pipe so they survive the round-trip regardless of locale.
					StandardInputEncoding = new UTF8Encoding(false),
					StandardOutputEncoding = new UTF8Encoding(false),
					StandardErrorEncoding = new UTF8Encoding(false),
				};
				foreach (var arg in args)
				{
					psi.ArgumentList.Add(arg);
				}

				psi.Environment.Remove("ANTHROPIC_API_KEY");
				psi.Environment.Remove("ANTHROPIC_AUTH_TOKEN");

				using var process = new Process { StartInfo = psi };
				if (!process.Start())
				{
					throw new AnthropicRequestException(0, $"Could not start the claude CLI at '{_binary}'.");
				}

				// Read both streams concurrently while writing stdin, then wait for exit:
				// the canonical no-deadlock pattern for a redirected child.
				var stdoutTask = process.StandardOutput.ReadToEndAsync();
				var stderrTask = process.StandardError.ReadToEndAsync();

				try
				{
					await process.StandardInput.WriteAsync(prompt).ConfigureAwait(false);
					process.StandardInput.Close();
				}
				catch (IOException)
				{
					// Broken pipe — the CLI exited before reading stdin (e.g. an auth or
					// flag error). The real cause surfaces below via exit code + stderr.
				}

				using (ct.Register(() => TryKill(process)))
				{
					await Task.Run(() => process.WaitForExit(), ct).ConfigureAwait(false);
				}

				var stdout = await stdoutTask.ConfigureAwait(false);
				var stderr = await stderrTask.ConfigureAwait(false);
				ct.ThrowIfCancellationRequested();

				if (process.ExitCode != 0)
				{
					throw new AnthropicRequestException(0,
						$"claude CLI exited {process.ExitCode} for stage '{stage}' (model {alias}). " +
						$"stderr: {Trim(stderr)} stdout: {Trim(stdout)}");
				}

				var envelope = ParseEnvelope(stdout);
				if (envelope.IsError || (envelope.Subtype != null && envelope.Subtype != "success"))
				{
					throw new AnthropicRequestException(0,
						$"claude CLI reported an error for stage '{stage}' (subtype {envelope.Subtype}): {Trim(envelope.Result)}");
				}

				return envelope;
			}
			finally
			{
				_throttle.Release();
			}
		}

		private static CliResult ParseEnvelope(string stdout)
		{
			var json = IsolateJson(stdout);
			try
			{
				using var doc = JsonDocument.Parse(json);
				var root = doc.RootElement;
				var result = root.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.String
					? r.GetString() ?? string.Empty
					: string.Empty;
				var isError = root.TryGetProperty("is_error", out var e) && e.ValueKind == JsonValueKind.True;
				var subtype = root.TryGetProperty("subtype", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
				var sessionId = root.TryGetProperty("session_id", out var sid) && sid.ValueKind == JsonValueKind.String
					? sid.GetString() ?? string.Empty
					: string.Empty;

				var usage = AnthropicTokenUsage.Zero;
				if (root.TryGetProperty("usage", out var u) && u.ValueKind == JsonValueKind.Object)
				{
					usage = new AnthropicTokenUsage(
						Long(u, "input_tokens"),
						Long(u, "output_tokens"),
						Long(u, "cache_read_input_tokens"),
						Long(u, "cache_creation_input_tokens"));
				}

				return new CliResult(result, isError, subtype, sessionId, usage);
			}
			catch (JsonException ex)
			{
				throw new AnthropicRequestException(0, "Could not parse the claude CLI JSON envelope: " + ex.Message + "\n" + Trim(stdout));
			}
		}

		/// <summary>
		/// The CLI's JSON envelope is the only thing on stdout with --output-format
		/// json, but guard against a stray leading line by taking the last text that
		/// parses as a JSON object (envelopes are single-line).
		/// </summary>
		private static string IsolateJson(string stdout)
		{
			var trimmed = stdout.Trim();
			if (trimmed.StartsWith("{", StringComparison.Ordinal))
			{
				return trimmed;
			}

			var lastBrace = trimmed.LastIndexOf('{');
			return lastBrace >= 0 ? trimmed.Substring(lastBrace) : trimmed;
		}

		private static long Long(JsonElement obj, string name) =>
			obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;

		/// <summary>
		/// Renders the (usually single, occasionally multi-turn retry) message list
		/// into one prompt string for a stateless CLI call. Role headers are added
		/// only when there is genuine back-and-forth so simple stages stay verbatim.
		/// </summary>
		private static string RenderPrompt(IReadOnlyList<AnthropicMessage> messages, IReadOnlyList<string> imagePaths)
		{
			var sb = new StringBuilder();
			if (messages.Count == 1 && messages[0].Role.Equals("user", StringComparison.OrdinalIgnoreCase))
			{
				sb.Append(messages[0].Content);
			}
			else
			{
				for (var i = 0; i < messages.Count; i++)
				{
					if (i > 0)
					{
						sb.Append("\n\n");
					}

					sb.Append(messages[i].Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
						? "[Your previous reply]\n"
						: "[Instruction]\n");
					sb.Append(messages[i].Content);
				}
			}

			if (imagePaths.Count > 0)
			{
				sb.Append("\n\nReference image file(s) — open each with the Read tool before you answer:");
				for (var i = 0; i < imagePaths.Count; i++)
				{
					sb.Append($"\n  Image {i + 1}: {imagePaths[i]}");
				}
			}

			return sb.ToString();
		}

		private static IReadOnlyList<string> WriteImages(IReadOnlyList<AnthropicImage> images, string directory)
		{
			if (images.Count == 0)
			{
				return Array.Empty<string>();
			}

			Directory.CreateDirectory(directory);
			var paths = new List<string>(images.Count);
			for (var i = 0; i < images.Count; i++)
			{
				var path = Path.Combine(directory, $"image-{i + 1}.{Extension(images[i].MediaType)}");
				File.WriteAllBytes(path, images[i].Data);
				paths.Add(path);
			}

			return paths;
		}

		private static string Extension(string mediaType) => mediaType switch
		{
			"image/jpeg" => "jpg",
			"image/gif" => "gif",
			"image/webp" => "webp",
			_ => "png",
		};

		/// <summary>
		/// Maps a per-stage configured model id onto a CLI alias where one exists
		/// (opus/sonnet/haiku), else passes the id through verbatim — the CLI accepts
		/// both an alias and a full model name for <c>--model</c>.
		/// </summary>
		private static string ModelAlias(string model)
		{
			var m = model.ToLowerInvariant();
			if (m.Contains("opus"))
			{
				return "opus";
			}

			if (m.Contains("sonnet"))
			{
				return "sonnet";
			}

			if (m.Contains("haiku"))
			{
				return "haiku";
			}

			return model;
		}

		/// <summary>
		/// Tells the model how to "call" the script tool when no real tool is wired:
		/// emit one fenced ```json block carrying the tool's input object. Carries the
		/// production tool description so the model authors against the same contract.
		/// </summary>
		private static string ToolEmulationAddendum(AnthropicTool tool) =>
			$"TOOL PROTOCOL — IMPORTANT: the '{tool.Name}' tool is not directly callable in this environment. " +
			$"To invoke it, emit exactly one fenced ```json code block whose body is the tool's JSON input, and nothing else after it. " +
			$"Tool purpose: {tool.Description}\n" +
			$"Tool input schema: {tool.InputJsonSchema}\n" +
			"Example of a turn that invokes the tool:\n```json\n{\"script\": \"var b2 = b; /* ... */ return b.Build();\"}\n```\n" +
			"After you receive the tool result, if it is an error, emit a corrected ```json block; when it succeeds, stop.";

		/// <summary>
		/// Parses the model's emitted ```json block into an <see cref="AnthropicToolUse"/>
		/// the executor can handle. Tolerant: falls back to the outermost brace span when
		/// no fenced block is present, so a model that forgets the fence still drives the loop.
		/// </summary>
		private static bool TryExtractScriptToolUse(string response, string toolName, int callId, int iteration, out AnthropicToolUse use)
		{
			use = new AnthropicToolUse(string.Empty, toolName, string.Empty);
			var body = FencedBlockExtractor.Extract(response, "json") ?? string.Empty;
			if (string.IsNullOrWhiteSpace(body))
			{
				var open = response.IndexOf('{');
				var close = response.LastIndexOf('}');
				if (open < 0 || close <= open)
				{
					return false;
				}

				body = response.Substring(open, close - open + 1);
			}

			// Validate it is a JSON object carrying a script field before committing —
			// the executor will re-parse it, but a clean reject here lets us re-prompt.
			try
			{
				using var doc = JsonDocument.Parse(body);
				if (doc.RootElement.ValueKind != JsonValueKind.Object ||
					!doc.RootElement.TryGetProperty("script", out var script) ||
					script.ValueKind != JsonValueKind.String ||
					string.IsNullOrWhiteSpace(script.GetString()))
				{
					return false;
				}
			}
			catch (JsonException)
			{
				return false;
			}

			use = new AnthropicToolUse($"cli-{callId}-{iteration}", toolName, body);
			return true;
		}

		private static void AppendToolInteraction(StringBuilder sb, AnthropicToolUse use, AnthropicToolResult result)
		{
			sb.Append("\n  ┌─ TOOL CALL ").Append(use.Name).Append(" (").Append(use.Id).Append(")\n");
			sb.Append("  │  INPUT: ").Append(use.InputJson).Append('\n');
			sb.Append("  └─ RESULT [").Append(result.IsError ? "ERROR" : "ok").Append("]: ").Append(result.Content).Append('\n');
		}

		private void EmitTranscript(
			int callId,
			string stage,
			string alias,
			string systemPrompt,
			string prompt,
			IReadOnlyList<string> imagePaths,
			string toolLog,
			CliResult envelope)
		{
			if (_onTranscript == null)
			{
				return;
			}

			var sb = new StringBuilder();
			sb.Append("┏━━ CLI CALL #").Append(callId).Append(" · ").Append(stage).Append(" · ").Append(alias).Append(" (plan) ━━━\n");
			sb.Append("SYSTEM PROMPT (").Append(systemPrompt.Length).Append(" chars):\n").Append(systemPrompt).Append('\n');
			if (imagePaths.Count > 0)
			{
				sb.Append("\nIMAGES: ").Append(string.Join(", ", imagePaths)).Append('\n');
			}

			sb.Append("\nPROMPT (").Append(prompt.Length).Append(" chars):\n").Append(prompt).Append('\n');
			if (toolLog.Length > 0)
			{
				sb.Append(toolLog);
			}

			sb.Append("\nRESPONSE (").Append(envelope.Result.Length).Append(" chars):\n")
				.Append(envelope.Result.Length > 0 ? envelope.Result : "(no response text — see tool calls above)").Append('\n');
			sb.Append("USAGE: in ").Append(envelope.Usage.InputTokens.ToString("n0"))
				.Append(" · cache read ").Append(envelope.Usage.CacheReadInputTokens.ToString("n0"))
				.Append(" · cache write ").Append(envelope.Usage.CacheCreationInputTokens.ToString("n0"))
				.Append(" · out ").Append(envelope.Usage.OutputTokens.ToString("n0")).Append('\n');
			sb.Append("┗━━ END CALL #").Append(callId).Append(" ━━━");
			_onTranscript.Report(sb.ToString());
		}

		private static void TryKill(Process process)
		{
			try
			{
				if (!process.HasExited)
				{
					process.Kill();
				}
			}
			catch
			{
				// Already gone, or denied — nothing useful to do on a best-effort kill.
			}
		}

		/// <summary>
		/// Resolves the claude binary. A Unity editor launched from the Hub inherits a
		/// minimal PATH that omits the CLI, so an explicit path / env override / known
		/// install locations are tried before the bare name (which only resolves when
		/// Unity was launched from a shell with a full PATH, e.g. batch mode).
		/// </summary>
		private static string ResolveBinary(string? configured)
		{
			if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
			{
				return configured!;
			}

			var env = Environment.GetEnvironmentVariable("CLAUDE_CLI_PATH");
			if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
			{
				return env!;
			}

			var home = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
			var candidates = new[]
			{
				"/opt/homebrew/bin/claude",
				"/usr/local/bin/claude",
				Path.Combine(home, ".claude", "local", "claude"),
				Path.Combine(home, ".local", "bin", "claude"),
				Path.Combine(home, ".bun", "bin", "claude"),
				"/usr/bin/claude",
			};
			foreach (var candidate in candidates)
			{
				if (File.Exists(candidate))
				{
					return candidate;
				}
			}

			return "claude";
		}

		private static string Trim(string text, int max = 1000) =>
			text.Length <= max ? text : text.Substring(0, max) + " …";

		public void Dispose()
		{
			_throttle.Dispose();
			try
			{
				if (Directory.Exists(_workingDir))
				{
					Directory.Delete(_workingDir, recursive: true);
				}
			}
			catch
			{
				// Temp cleanup is best-effort; the OS reclaims the temp dir regardless.
			}
		}

		private readonly record struct CliResult(string Result, bool IsError, string? Subtype, string SessionId, AnthropicTokenUsage Usage);
	}
}
