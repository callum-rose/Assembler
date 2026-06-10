using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Editor
{
	// Shared command-line plumbing for the headless editor batch tools (YamlValidatorBatch,
	// GameSandboxValidatorBatch, TestBatch): parsing repeatable flags, expanding file/dir targets into a
	// stable list of YAML files, rendering project-relative paths, and logging the final report cleanly.
	// Kept in one place so the tools stay in sync rather than each carrying its own copy.
	internal static class EditorBatchCli
	{
		// In batch mode Unity appends a script stack trace to every Debug.Log/LogError
		// (Application.GetStackTraceLogType defaults to ScriptOnly). On a *successful* run that stamps a
		// trace right next to the OK summary, which reads like a failure and buries the result among the
		// boot noise (see issue #209). Each CLI entry point calls this once up front so its report — and
		// any caught-exception message, which already carries its own trace via Exception.ToString() —
		// logs as a clean block.
		//
		// EDITOR-ONLY, PROCESS-WIDE side effect: only sound in batch mode, where the process exits straight
		// after. Never call it from a [MenuItem] — it would mute stack traces for the whole live editor
		// session.
		public static void SuppressLogStackTraces()
		{
			Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
			Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);
		}

		// Logs a batch tool's final report as a single block: Debug.Log on success, Debug.LogError on
		// failure (so a failing run still trips the editor console's error flag and the CLI's log scan).
		// Pair with SuppressLogStackTraces in CLI entry points so the block isn't trailed by a stack trace.
		public static void LogReport(string report, bool ok)
		{
			if (ok)
			{
				Debug.Log(report);
			}
			else
			{
				Debug.LogError(report);
			}
		}

		// Collects every value that immediately follows an occurrence of flag, e.g. for
		// "-yamlPath A -yamlPath B" returns { "A", "B" }.
		public static List<string> ArgValues(string[] args, string flag)
		{
			var values = new List<string>();
			for (int i = 0; i < args.Length - 1; i++)
			{
				if (args[i] == flag)
				{
					values.Add(args[i + 1]);
				}
			}

			return values;
		}

		// Expands the given files/directories into a stable, de-duplicated list of YAML files.
		// Throws FileNotFoundException if a target doesn't exist.
		public static List<string> CollectYamlFiles(IReadOnlyList<string> targets)
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

		// Renders an absolute path relative to the project root (the editor's working directory).
		public static string ToProjectRelative(string fullPath)
		{
			string root = Directory.GetCurrentDirectory();
			string full = Path.GetFullPath(fullPath);
			return full.StartsWith(root, StringComparison.Ordinal)
				? full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
				: full;
		}
	}
}
