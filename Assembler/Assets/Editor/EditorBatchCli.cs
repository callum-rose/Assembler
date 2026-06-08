using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Editor
{
	// Shared command-line plumbing for the headless editor batch tools (YamlValidatorBatch,
	// GameSandboxValidatorBatch, TestBatch): parsing repeatable flags, expanding file/dir targets into a
	// stable list of YAML files, and rendering project-relative paths. Kept in one place so the tools stay
	// in sync rather than each carrying its own copy.
	internal static class EditorBatchCli
	{
		// Collects every value that immediately follows an occurrence of flag, e.g. for
		// "-yamlPath A -yamlPath B" returns { "A", "B" }.
		public static List<string> ArgValues(string[] args, string flag)
		{
			var values = new List<string>();
			for (int i = 0; i < args.Length - 1; i++)
			{
				if (args[i] == flag)
					values.Add(args[i + 1]);
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
