using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assembler.Building;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Controls;
using Assembler.Parsing.Info;
using Assembler.Validation;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Assembler.Generation.Verification
{
	/// <summary>The five load-pipeline stages a descriptor passes through to boot a game.</summary>
	public enum BuildStage
	{
		Structure,
		Deserialise,
		Parse,
		Resolve,
		Instantiate
	}

	/// <summary>
	/// Outcome of one pipeline stage — a discriminated union of <see cref="RanResult"/> (the stage executed)
	/// and <see cref="NotRunResult"/> (skipped because an earlier stage failed; the pipeline stops at the first
	/// failure).
	/// </summary>
	public abstract record StageResult(BuildStage Stage);

	/// <summary>
	/// A stage that executed. <see cref="Errors"/> collects both thrown exceptions and any
	/// <c>Debug.LogError</c>s emitted while the stage was running; <see cref="Success"/> is true iff it's empty.
	/// </summary>
	public sealed record RanResult(BuildStage Stage, bool Success, IReadOnlyList<string> Errors) : StageResult(Stage);

	/// <summary>A stage that never ran because an earlier stage failed.</summary>
	public sealed record NotRunResult(BuildStage Stage) : StageResult(Stage);

	/// <summary>
	/// Result of running a descriptor through the full load pipeline in a sandbox: a per-stage breakdown plus
	/// an overall success flag. Mirrors <c>YamlValidationResult</c> in spirit — call <see cref="FormatReport"/>
	/// for a human/Claude-readable summary that pinpoints the failing stage.
	/// </summary>
	public sealed record SandboxValidationResult(bool Success, IReadOnlyList<StageResult> Stages)
	{
		/// <summary>The first stage that ran and failed, or null when every stage that ran succeeded.</summary>
		public BuildStage? FailedStage => Stages
			.OfType<RanResult>()
			.Where(s => !s.Success)
			.Select(s => (BuildStage?)s.Stage)
			.FirstOrDefault();

		public string FormatReport()
		{
			var sb = new StringBuilder();
			foreach (var stage in Stages)
			{
				switch (stage)
				{
					case NotRunResult notRun:
						sb.Append("  -     ").Append(StageName(notRun.Stage)).Append(" (not run)").Append('\n');
						break;

					case RanResult ran:
						sb.Append(ran.Success ? "  OK    " : "  FAIL  ").Append(StageName(ran.Stage)).Append('\n');
						foreach (var error in ran.Errors)
						{
							foreach (var line in error.Split('\n'))
							{
								sb.Append("        ").Append(line).Append('\n');
							}
						}

						break;
				}
			}

			return sb.ToString().TrimEnd('\n');
		}

		/// <summary>The lower-case display name of a stage, shared by the report and by callers.</summary>
		public static string StageName(BuildStage stage) => stage switch
		{
			BuildStage.Structure => "structure",
			BuildStage.Deserialise => "deserialise",
			BuildStage.Parse => "parse",
			BuildStage.Resolve => "resolve",
			BuildStage.Instantiate => "instantiate",
			_ => stage.ToString().ToLowerInvariant()
		};
	}

	/// <summary>
	/// Runs a descriptor through every load-pipeline stage — structural YAML validation, deserialisation,
	/// parsing/transforming, resolving and entity instantiation — inside a throwaway sandbox, and reports
	/// per-stage success with detailed errors. This is the deeper companion to the structural-only
	/// <see cref="YamlStructureValidator"/>: it confirms a descriptor actually <em>boots</em> a game.
	///
	/// Instantiation creates real GameObjects in the active scene (so behaviour <c>Awake</c>/<c>OnEnable</c>
	/// and initialisation wiring run, surfacing real startup errors); the validator destroys everything it
	/// created before returning, so repeated calls and directory-wide runs don't leak or interfere. It does
	/// not run <c>Start</c>/<c>Update</c>, so it validates boot, not per-frame runtime behaviour.
	/// </summary>
	public static class SandboxValidator
	{
		private static readonly BuildStage[] Order =
		{
			BuildStage.Structure,
			BuildStage.Deserialise,
			BuildStage.Parse,
			BuildStage.Resolve,
			BuildStage.Instantiate
		};

		// Async because the resolve stage is async (Addressables assets load asynchronously). Sync top-level
		// callers (the batch validator's -executeMethod, the generation BuildHarness) block with
		// GetAwaiter().GetResult(); local content completes immediately so blocking is cheap there.
		public static async Task<SandboxValidationResult> ValidateAsync(string yaml)
		{
			var run = new PipelineRun();

			// Stage 1 — Structure. Pure, no engine state, so it runs outside the log hook. A structurally
			// invalid document means downstream stages would silently build from dropped/garbled data, so a
			// failure here aborts the rest of the pipeline.
			run.Record(ValidateStructure(yaml));

			// Stages 2-5 touch live engine state; run them under a log hook with teardown.
			if (!run.Aborted)
			{
				await RunBuildStages(yaml, run);
			}

			run.FillNotRun(Order);

			var success = run.Stages.All(s => s is RanResult { Success: true });
			return new SandboxValidationResult(success, run.Stages);
		}

		// Stage 1: structural well-formedness. Reported as a failure when the document is structurally invalid.
		private static RanResult ValidateStructure(string yaml)
		{
			var errors = new List<string>();
			try
			{
				var structure = YamlStructureValidator.Validate(yaml);
				if (!structure.IsValid)
				{
					errors.Add(structure.FormatReport());
				}
			}
			catch (Exception ex)
			{
				errors.Add("Structural validation threw: " + ex);
			}

			return new RanResult(BuildStage.Structure, errors.Count == 0, errors);
		}

		// Stages 2-5: deserialise → parse → resolve → instantiate. Each stage's work runs under a log hook
		// (so behaviours that report failures via Debug.LogError rather than throwing are still captured) and
		// is skipped once an earlier stage has failed. Everything instantiated is torn down before returning.
		private static async Task RunBuildStages(string yaml, PipelineRun run)
		{
			void OnLog(string condition, string stackTrace, LogType type)
			{
				if (run.Sink == null)
				{
					return;
				}

				if (type is LogType.Error or LogType.Exception or LogType.Assert)
				{
					run.Sink.Add(condition + FilterFrames(stackTrace));
				}
			}

			// Snapshot existing scene roots so teardown destroys only what this build created — even if
			// instantiation throws part-way through.
			var preexisting = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());

			Application.logMessageReceivedThreaded += OnLog;
			try
			{
				var dto = run.Run(BuildStage.Deserialise, "Deserialisation failed",
					() => new GameFileParser().Parse(yaml));

				var parsed = run.Run(BuildStage.Parse, "Parsing failed",
					() => new ParsedGame(Transformer.Transform(dto!), ControlsTransformer.Transform(dto!.Controls)));

				var resolved = await run.RunAsync(BuildStage.Resolve, "Resolving failed",
					() => parsed!.GameInfo.ResolveAsync(parsed.Controls, null));

				run.Run(BuildStage.Instantiate, "Entity instantiation failed",
					() => resolved!.Instantiate());
			}
			finally
			{
				Application.logMessageReceivedThreaded -= OnLog;
				Teardown(preexisting);
			}
		}

		// Destroys every root GameObject created since the snapshot, unloading the sandboxed game (the "Game"
		// root, its EventSystem, and any partially-built objects) and resetting EventSystem.current.
		private static void Teardown(HashSet<GameObject> preexisting)
		{
			foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
			{
				if (!preexisting.Contains(root))
				{
					Object.DestroyImmediate(root);
				}
			}
		}

		// Compactly formats a thrown exception for an LLM reader: the message chain (including inner
		// exceptions) plus only the stack frames in our own code. A raw exception trace here is dominated by
		// dozens of YamlDotNet/BCL internal frames that don't help locate the fault in a descriptor.
		private static string Summarise(Exception ex)
		{
			var sb = new StringBuilder();
			for (Exception? e = ex; e != null; e = e.InnerException)
			{
				if (!ReferenceEquals(e, ex))
				{
					sb.Append("\n  caused by ");
				}

				sb.Append(e.GetType().Name).Append(": ").Append(e.Message.Trim());
			}

			sb.Append(FilterFrames(ex.StackTrace));
			return sb.ToString();
		}

		// Drops framework/engine internals from a stack trace, keeping only frames in our own code (Assembler
		// types or Assets/ paths). Returns the kept frames as indented lines each prefixed with a newline, or
		// an empty string when none qualify — so callers can append it directly after a message.
		private static string FilterFrames(string? stackTrace)
		{
			if (string.IsNullOrEmpty(stackTrace))
			{
				return string.Empty;
			}

			var sb = new StringBuilder();
			foreach (var line in stackTrace.Split('\n'))
			{
				var trimmed = line.Trim();
				if (trimmed.Length == 0)
				{
					continue;
				}

				if (trimmed.Contains("Assembler.") || trimmed.Contains("Assets/"))
				{
					sb.Append("\n    ").Append(trimmed);
				}
			}

			return sb.ToString();
		}

		// Carries the parse stage's two products (game + controls) to the resolve stage.
		private sealed record ParsedGame(GameInfo GameInfo, ControlsInfo Controls);

		// Drives the staged pipeline: records each stage's result, captures logged errors via the moving Sink,
		// and short-circuits every remaining stage once one fails.
		private sealed class PipelineRun
		{
			public readonly List<StageResult> Stages = new();

			// The error list of the stage currently executing; the log hook appends to it. Null between stages.
			public List<string>? Sink;

			public bool Aborted { get; private set; }

			public void Record(RanResult result)
			{
				Stages.Add(result);
				if (!result.Success)
				{
					Aborted = true;
				}
			}

			// Runs one stage's work under the log hook, recording a RanResult. Returns the produced artifact,
			// or null if the pipeline has already aborted or this stage failed (so callers can guard the next
			// stage's lambda with `!`, knowing it won't run when the prior artifact is null).
			public T? Run<T>(BuildStage stage, string failPrefix, Func<T> work) where T : class
			{
				if (Aborted)
				{
					return null;
				}

				var errors = new List<string>();
				Sink = errors;
				T? artifact = null;
				try
				{
					artifact = work();
				}
				catch (Exception ex)
				{
					errors.Add(failPrefix + ": " + Summarise(ex));
				}
				finally
				{
					Sink = null;
				}

				Record(new RanResult(stage, errors.Count == 0, errors));
				return errors.Count == 0 ? artifact : null;
			}

			// Async sibling of Run for stages whose work is asynchronous (resolve, via Addressables). Same
			// exception/log capture and short-circuit-on-abort behaviour; the only difference is awaiting the work.
			// Distinct name (not an overload) because a Func<Task<T>> lambda is convertible to both Func<T> and
			// Func<Task<T>>, which would make an overloaded Run ambiguous.
			public async Task<T?> RunAsync<T>(BuildStage stage, string failPrefix, Func<Task<T>> work) where T : class
			{
				if (Aborted)
				{
					return null;
				}

				var errors = new List<string>();
				Sink = errors;
				T? artifact = null;
				try
				{
					artifact = await work();
				}
				catch (Exception ex)
				{
					errors.Add(failPrefix + ": " + Summarise(ex));
				}
				finally
				{
					Sink = null;
				}

				Record(new RanResult(stage, errors.Count == 0, errors));
				return errors.Count == 0 ? artifact : null;
			}

			// Appends a NotRunResult for every stage that never ran (always the trailing stages after a failure).
			public void FillNotRun(IReadOnlyList<BuildStage> order)
			{
				foreach (var stage in order)
				{
					if (Stages.All(s => s.Stage != stage))
					{
						Stages.Add(new NotRunResult(stage));
					}
				}
			}
		}
	}
}
