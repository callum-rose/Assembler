using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
			try
			{
				string[] args = Environment.GetCommandLineArgs();
				List<string> targets = ArgValues(args, "-yamlPath");
				if (targets.Count == 0)
					targets.Add(DefaultDescriptorDir);

				bool ok = Run(targets, out string report);
				if (ok)
					Debug.Log(report);
				else
					Debug.LogError(report);

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
			if (ok)
				Debug.Log(report);
			else
				Debug.LogError(report);
		}

		// Sandbox-builds every YAML file under the given files/directories, building a combined report.
		// Returns true when every file boots cleanly.
		private static bool Run(IReadOnlyList<string> targets, out string report)
		{
			List<string> files;
			try
			{
				files = CollectYamlFiles(targets);
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
				string rel = ToProjectRelative(file);

				SandboxValidationResult result;
				try
				{
					result = SandboxValidator.Validate(File.ReadAllText(file));
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
				var failedStage = result.FailedStage;
				string where = failedStage != null ? $"  (failed at {Name(failedStage.Stage)})" : string.Empty;
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

		private static string Name(BuildStage stage) => stage switch
		{
			BuildStage.Structure => "structure",
			BuildStage.Deserialise => "deserialise",
			BuildStage.Parse => "parse",
			BuildStage.Resolve => "resolve",
			BuildStage.Instantiate => "instantiate",
			_ => stage.ToString().ToLowerInvariant()
		};

		// Expands the given files/directories into a stable, de-duplicated list of YAML files.
		private static List<string> CollectYamlFiles(IReadOnlyList<string> targets)
		{
			var result = new List<string>();
			foreach (string target in targets)
			{
				if (Directory.Exists(target))
				{
					result.AddRange(Directory.EnumerateFiles(target, "*.yaml", SearchOption.AllDirectories));
					result.AddRange(Directory.EnumerateFiles(target, "*.yml", SearchOption.AllDirectories));
				}
				else if (File.Exists(target))
				{
					result.Add(target);
				}
				else
				{
					throw new FileNotFoundException("no such file or directory: " + target);
				}
			}

			return result
				.Select(Path.GetFullPath)
				.Distinct()
				.OrderBy(p => p, StringComparer.Ordinal)
				.ToList();
		}

		// Collects every value that immediately follows an occurrence of flag, e.g. for
		// "-yamlPath A -yamlPath B" returns { "A", "B" }.
		private static List<string> ArgValues(string[] args, string flag)
		{
			var values = new List<string>();
			for (int i = 0; i < args.Length - 1; i++)
			{
				if (args[i] == flag)
					values.Add(args[i + 1]);
			}

			return values;
		}

		private static string ToProjectRelative(string fullPath)
		{
			string root = Directory.GetCurrentDirectory();
			string full = Path.GetFullPath(fullPath);
			return full.StartsWith(root, StringComparison.Ordinal)
				? full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
				: full;
		}
	}
}
