using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assembler.Building;
using Assembler.Deserialisation;
using Assembler.Deserialisation.Dtos;
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
	/// Outcome of one pipeline stage. <see cref="Ran"/> is false for stages skipped because an earlier stage
	/// failed (the pipeline stops at the first failure). <see cref="Errors"/> collects both thrown exceptions
	/// and any <c>Debug.LogError</c>s emitted while the stage was running.
	/// </summary>
	public sealed record StageResult(BuildStage Stage, bool Ran, bool Success, IReadOnlyList<string> Errors);

	/// <summary>
	/// Result of running a descriptor through the full load pipeline in a sandbox: a per-stage breakdown plus
	/// an overall success flag. Mirrors <c>YamlValidationResult</c> in spirit — call <see cref="FormatReport"/>
	/// for a human/Claude-readable summary that pinpoints the failing stage.
	/// </summary>
	public sealed record SandboxValidationResult(bool Success, IReadOnlyList<StageResult> Stages)
	{
		/// <summary>The first stage that failed, or null when every stage that ran succeeded.</summary>
		public StageResult? FailedStage => Stages.FirstOrDefault(s => s.Ran && !s.Success);

		public string FormatReport()
		{
			var sb = new StringBuilder();
			foreach (var stage in Stages)
			{
				if (!stage.Ran)
				{
					sb.Append("  -     ").Append(Name(stage.Stage)).Append(" (not run)").Append('\n');
					continue;
				}

				sb.Append(stage.Success ? "  OK    " : "  FAIL  ").Append(Name(stage.Stage)).Append('\n');

				foreach (var error in stage.Errors)
				{
					foreach (var line in error.Split('\n'))
						sb.Append("        ").Append(line).Append('\n');
				}
			}

			return sb.ToString().TrimEnd('\n');
		}

		private static string Name(BuildStage stage) => stage switch
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

		public static SandboxValidationResult Validate(string yaml)
		{
			var stages = new List<StageResult>();

			// Stage 1 — Structure. Pure, no engine state, so it runs outside the log hook. A structurally
			// invalid document means downstream stages would silently build from dropped/garbled data, so we
			// stop here.
			var structureErrors = new List<string>();
			try
			{
				var structure = YamlStructureValidator.Validate(yaml);
				if (!structure.IsValid)
					structureErrors.Add(structure.FormatReport());
			}
			catch (Exception ex)
			{
				structureErrors.Add("Structural validation threw: " + ex);
			}

			stages.Add(new StageResult(BuildStage.Structure, true, structureErrors.Count == 0, structureErrors));
			if (structureErrors.Count > 0)
				return Stop(stages, BuildStage.Structure);

			// Stages 2-5 run against live engine state. Hook the log so behaviours that report failures via
			// Debug.LogError (rather than throwing) are still captured, attributed to the running stage via
			// the moving `sink` reference.
			List<string>? sink = null;

			void OnLog(string condition, string stackTrace, LogType type)
			{
				if (sink == null) return;
				if (type is LogType.Error or LogType.Exception or LogType.Assert)
					sink.Add(string.IsNullOrEmpty(stackTrace) ? condition : condition + "\n" + stackTrace);
			}

			// Snapshot existing scene roots so teardown destroys only what this build created — even if
			// instantiation throws part-way through.
			var preexisting = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());

			Application.logMessageReceivedThreaded += OnLog;
			try
			{
				// Stage 2 — Deserialise.
				var deserialiseErrors = new List<string>();
				sink = deserialiseErrors;
				GameDto? dto = null;
				try
				{
					dto = new GameFileParser().Parse(yaml);
				}
				catch (Exception ex)
				{
					deserialiseErrors.Add("Deserialisation failed: " + ex);
				}

				stages.Add(new StageResult(BuildStage.Deserialise, true, deserialiseErrors.Count == 0, deserialiseErrors));
				if (deserialiseErrors.Count > 0 || dto == null)
					return Stop(stages, BuildStage.Deserialise);

				// Stage 3 — Parse / Transform (game structure + controls).
				var parseErrors = new List<string>();
				sink = parseErrors;
				GameInfo? gameInfo = null;
				ControlsInfo controls = ControlsInfo.Empty;
				try
				{
					gameInfo = Transformer.Transform(dto);
					controls = ControlsTransformer.Transform(dto.Controls);
				}
				catch (Exception ex)
				{
					parseErrors.Add("Parsing failed: " + ex);
				}

				stages.Add(new StageResult(BuildStage.Parse, true, parseErrors.Count == 0, parseErrors));
				if (parseErrors.Count > 0 || gameInfo == null)
					return Stop(stages, BuildStage.Parse);

				// Stage 4 — Resolve (registries, expression compilation, asset loading, controls validation).
				var resolveErrors = new List<string>();
				sink = resolveErrors;
				ResolvedGame? resolved = null;
				try
				{
					resolved = Builder.Resolve(gameInfo, controls, null);
				}
				catch (Exception ex)
				{
					resolveErrors.Add("Resolving failed: " + ex);
				}

				stages.Add(new StageResult(BuildStage.Resolve, true, resolveErrors.Count == 0, resolveErrors));
				if (resolveErrors.Count > 0 || resolved == null)
					return Stop(stages, BuildStage.Resolve);

				// Stage 5 — Instantiate entities + behaviours and run deferred initialisation.
				var instantiateErrors = new List<string>();
				sink = instantiateErrors;
				try
				{
					Builder.Instantiate(resolved);
				}
				catch (Exception ex)
				{
					instantiateErrors.Add("Entity instantiation failed: " + ex);
				}

				stages.Add(new StageResult(BuildStage.Instantiate, true, instantiateErrors.Count == 0, instantiateErrors));

				return new SandboxValidationResult(stages.All(s => !s.Ran || s.Success), stages);
			}
			finally
			{
				sink = null;
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
					Object.DestroyImmediate(root);
			}
		}

		// Records the stages after `failedAt` as not-run and returns the failed result.
		private static SandboxValidationResult Stop(List<StageResult> stages, BuildStage failedAt)
		{
			var failedIndex = Array.IndexOf(Order, failedAt);
			for (var i = failedIndex + 1; i < Order.Length; i++)
				stages.Add(new StageResult(Order[i], false, false, Array.Empty<string>()));

			return new SandboxValidationResult(false, stages);
		}
	}
}
