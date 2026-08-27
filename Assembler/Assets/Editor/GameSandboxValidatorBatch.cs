using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Assembler.Generation.Verification;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace Editor
{
	// Headless + menu entry points for the deeper, semantic descriptor check: each YAML file is run through
	// the full load pipeline (structure → deserialise → parse → resolve → instantiate) in a throwaway sandbox
	// via Assembler.Generation.Verification.SandboxValidator, confirming the descriptor actually boots a game.
	// The structural-only counterpart lives in YamlValidatorBatch.
	//
	// Invoked from Tools/validate-game.sh via:
	//   Unity -batchmode -quit -nographics -projectPath <project>
	//         -executeMethod Editor.GameSandboxValidatorBatch.Validate -logFile -
	//         [-yamlPath <file-or-dir> ...]
	//
	// With no -yamlPath args it validates everything under Assets/ExampleGameDescriptors. Exits 0 when every
	// file boots cleanly and 1 when any file fails, so the script and Claude can detect the outcome from the
	// exit code as well as the logged per-stage report.
	public static class GameSandboxValidatorBatch
	{
		private const string DefaultDescriptorDir = "Assets/ExampleGameDescriptors";

		// Command-line entry point.
		public static void Validate()
		{
			EditorBatchCli.SuppressLogStackTraces();
			try
			{
				string[] args = Environment.GetCommandLineArgs();
				List<string> targets = EditorBatchCli.ArgValues(args, "-yamlPath");
				if (targets.Count == 0)
				{
					targets.Add(DefaultDescriptorDir);
				}

				bool ok = Run(targets, out string report);
				EditorBatchCli.LogReport(report, ok);
				EditorApplication.Exit(ok ? 0 : 1);
			}
			catch (Exception e)
			{
				Debug.LogError("GameSandboxValidatorBatch failed: " + e);
				EditorApplication.Exit(1);
			}
		}

		// Pipeline entry point, reached as `unity command validate_game`. Nominally the most expensive of
		// the validators — each descriptor is built through the whole pipeline in a throwaway sandbox —
		// but against a warm editor the whole example corpus (55 descriptors) measures ~1s, comfortably
		// inside the 60s main-thread budget a command gets. Should a much larger corpus ever approach
		// that, `unity command --detach` runs unbounded and is polled via `unity job`.
		[CliCommand("validate_game",
			"Sandbox-build descriptors through structure → deserialise → parse → resolve → instantiate. "
			+ "Fails the command when any descriptor fails, with the per-stage report as the error message.",
			Tags = new[] { "assembler/validation" })]
		public static PipelineReport ValidateGameCommand(
			[CliArg("targets", "Comma-separated descriptor files or directories to sandbox-build. "
				+ "Defaults to sweeping Assets/ExampleGameDescriptors.")]
			string? targets = null)
		{
			EditorPipelineCli.RequireFreshAssets();

			List<string> resolved = EditorPipelineCli.SplitTargets(targets);
			if (resolved.Count == 0)
			{
				resolved.Add(DefaultDescriptorDir);
			}

			bool ok = Run(resolved, out string report);
			return EditorPipelineCli.Complete(report, ok);
		}

		// In-editor convenience: sandbox-build the example descriptors and log the report to the console.
		[MenuItem("Assembler/Validate Game (sandbox build)")]
		private static void ValidateGamesMenu()
		{
			bool ok = Run(new List<string> { DefaultDescriptorDir }, out string report);
			EditorBatchCli.LogReport(report, ok);
		}

		// Sandbox-builds every YAML file under the given files/directories, building a combined report.
		// Returns true when every file boots cleanly.
		private static bool Run(IReadOnlyList<string> targets, out string report)
		{
			List<string> files;
			try
			{
				files = EditorBatchCli.CollectYamlFiles(targets);
			}
			catch (Exception e)
			{
				report = "GameSandboxValidatorBatch: " + e.Message;
				return false;
			}

			var sb = new StringBuilder();
			sb.AppendLine("============== Game sandbox validation ==============");

			if (files.Count == 0)
			{
				sb.AppendLine("No .yaml/.yml files found in: " + string.Join(", ", targets));
				sb.AppendLine("=====================================================");
				report = sb.ToString();
				return false;
			}

			int failed = 0;
			foreach (string file in files)
			{
				string rel = EditorBatchCli.ToProjectRelative(file);

				SandboxValidationResult result;
				try
				{
					// Block at this synchronous -executeMethod entry point. Local content resolves immediately;
					// only remote Addressables assets would genuinely wait here.
					result = SandboxValidator.ValidateAsync(File.ReadAllText(file)).GetAwaiter().GetResult();
				}
				catch (Exception e)
				{
					failed++;
					sb.AppendLine($"FAIL  {rel}  (could not read or validate)");
					sb.AppendLine("        " + e.Message);
					continue;
				}

				if (result.Success)
				{
					sb.AppendLine("OK    " + rel);
					continue;
				}

				failed++;
				string where = result.FailedStage is { } stage
					? $"  (failed at {SandboxValidationResult.StageName(stage)})"
					: string.Empty;
				sb.AppendLine($"FAIL  {rel}{where}");
				sb.AppendLine(result.FormatReport());
			}

			sb.AppendLine();
			sb.AppendLine(failed == 0
				? $"All {files.Count} file(s) boot cleanly."
				: $"{failed} of {files.Count} file(s) failed to build.");
			sb.AppendLine("=====================================================");

			report = sb.ToString();
			return failed == 0;
		}
	}
}
