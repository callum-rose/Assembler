using System;
using System.Collections.Generic;
using System.Text;
using Assembler.Validation;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace Editor
{
	// Pipeline + menu entry points for the structural YAML validator. The actual validation lives in
	// the runtime Assembler.Validation assembly (YamlStructureValidator) so it also runs inside player
	// builds on any platform; this class just drives it from the editor and the CLI.
	//
	// Reached from the command line as `unity command validate_yaml [--targets <file-or-dir>,...]`,
	// which validates everything under Assets/ExampleGameDescriptors when given no targets. A file with
	// errors fails the command (non-zero exit), so the outcome is readable from the exit status as well
	// as the report.
	public static class YamlValidatorBatch
	{
		private const string DefaultDescriptorDir = "Assets/ExampleGameDescriptors";

		// Pipeline entry point: the same check driven over com.unity.pipeline against a resident editor,
		// which is what `unity command validate_yaml` reaches. Structural YAML validation is cheap — the
		// whole example corpus measures well under a second — so a full sweep is nowhere near the 60s
		// main-thread budget a command gets.
		[CliCommand("validate_yaml",
			"Validate descriptor YAML structure (well-formedness + duplicate keys). Fails the command "
			+ "when any file has errors, with the per-file report as the error message.",
			Tags = new[] { "assembler/validation" })]
		public static PipelineReport ValidateYamlCommand(
			[CliArg("targets", "Comma-separated files or directories to validate. "
				+ "Defaults to Assets/ExampleGameDescriptors.")]
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

		// In-editor convenience: validate the example descriptors and log the report to the console.
		[MenuItem("Assembler/Validate Descriptor YAML")]
		private static void ValidateDescriptorsMenu()
		{
			bool ok = Run(new List<string> { DefaultDescriptorDir }, out string report);
			EditorBatchCli.LogReport(report, ok);
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
					{
						summary += $", {result.WarningCount} warning{(result.WarningCount == 1 ? "" : "s")}";
					}

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
