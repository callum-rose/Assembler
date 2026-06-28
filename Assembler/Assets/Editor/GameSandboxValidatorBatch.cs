using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Assembler.Generation.Verification;
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
