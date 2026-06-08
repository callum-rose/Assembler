using System;
using System.Collections.Generic;
using System.Text;
using Assembler.Validation;
using UnityEditor;
using UnityEngine;

namespace Editor
{
	// Headless + menu entry points for the structural YAML validator. The actual validation lives in
	// the runtime Assembler.Validation assembly (YamlStructureValidator) so it also runs inside player
	// builds on any platform; this class just drives it from the editor and the command line.
	//
	// Invoked from Tools/validate-yaml.sh via:
	//   Unity -batchmode -quit -nographics -projectPath <project>
	//         -executeMethod Editor.YamlValidatorBatch.Validate -logFile -
	//         [-yamlPath <file-or-dir> ...]
	//
	// With no -yamlPath args it validates everything under Assets/ExampleGameDescriptors. Exits 0 when
	// every file is structurally valid and 1 when any file has errors, so the script and Claude can
	// detect the outcome from the exit code as well as the logged report.
	public static class YamlValidatorBatch
	{
		private const string DefaultDescriptorDir = "Assets/ExampleGameDescriptors";

		// Command-line entry point.
		public static void Validate()
		{
			try
			{
				string[] args = Environment.GetCommandLineArgs();
				List<string> targets = EditorBatchCli.ArgValues(args, "-yamlPath");
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
				Debug.LogError("YamlValidatorBatch failed: " + e);
				EditorApplication.Exit(1);
			}
		}

		// In-editor convenience: validate the example descriptors and log the report to the console.
		[MenuItem("Assembler/Validate Descriptor YAML")]
		private static void ValidateDescriptorsMenu()
		{
			bool ok = Run(new List<string> { DefaultDescriptorDir }, out string report);
			if (ok)
				Debug.Log(report);
			else
				Debug.LogError(report);
		}

		// Validates every YAML file under the given files/directories, building a combined report.
		// Returns true when nothing has errors.
		private static bool Run(IReadOnlyList<string> targets, out string report)
		{
			List<string> files;
			try
			{
				files = EditorBatchCli.CollectYamlFiles(targets);
			}
			catch (Exception e)
			{
				report = "YamlValidatorBatch: " + e.Message;
				return false;
			}

			var sb = new StringBuilder();
			sb.AppendLine("================ YAML validation ================");

			if (files.Count == 0)
			{
				sb.AppendLine("No .yaml/.yml files found in: " + string.Join(", ", targets));
				sb.AppendLine("=================================================");
				report = sb.ToString();
				return false;
			}

			int invalid = 0;
			foreach (string file in files)
			{
				YamlValidationResult result = YamlStructureValidator.ValidateFile(file);
				string rel = EditorBatchCli.ToProjectRelative(file);

				if (result.IsValid && result.Issues.Count == 0)
				{
					sb.AppendLine("OK    " + rel);
					continue;
				}

				if (!result.IsValid)
				{
					invalid++;
					string summary = result.ErrorCount + (result.ErrorCount == 1 ? " error" : " errors");
					if (result.WarningCount > 0)
						summary += $", {result.WarningCount} warning{(result.WarningCount == 1 ? "" : "s")}";
					sb.AppendLine($"FAIL  {rel}  ({summary})");
				}
				else
				{
					sb.AppendLine("OK    " + rel);
				}

				sb.AppendLine(result.FormatReport());
			}

			sb.AppendLine();
			sb.AppendLine(invalid == 0
				? $"All {files.Count} file(s) are structurally valid."
				: $"{invalid} of {files.Count} file(s) have errors.");
			sb.AppendLine("=================================================");

			report = sb.ToString();
			return invalid == 0;
		}
	}
}
