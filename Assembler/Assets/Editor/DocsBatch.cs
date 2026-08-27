using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace Editor
{
	// Entry points for regenerating the behaviour and library docs, and for the drift check that keeps
	// the committed copies honest. All of them funnel through Generate(), which wraps the same
	// generation code as the Assembler menu items (BehaviourDocs.WriteDocs / LibraryDocs.WriteDocs), so
	// every route produces byte-identical output.
	//
	// Reached from the command line as `unity command generate_docs` (overwrite the committed docs) and
	// `unity command check_docs` (regenerate into a temp dir and diff, leaving the committed files
	// alone). check_docs does that whole sequence in-process: the bash version it replaces shelled out
	// to `diff -u`, and a resident editor has no shell to do the diffing for it.
	public static class DocsBatch
	{
		// Committed home of the generated markdown — the baseline check_docs compares against.
		private const string CommittedDocsDir = "Assets/docs";

		// How many differing lines per side to show before truncating a drift report. Doc drift is
		// normally a handful of lines; a full regeneration would otherwise dump the entire file.
		private const int MaxDiffLinesPerSide = 40;

		// Pipeline entry point, reached as `unity command generate_docs`. Writing to the committed
		// Assets/docs is the whole point here, so unlike the validators this command mutates the
		// project — the caller commits the result.
		[CliCommand("generate_docs",
			"Regenerate Assets/docs/Behaviours.md and Libraries.md from the behaviour catalogue and "
			+ "XML doc comments, overwriting the committed copies.",
			Tags = new[] { "assembler/docs" })]
		public static PipelineReport GenerateDocsCommand(
			[CliArg("output-dir", "Write the markdown here instead of the committed Assets/docs, "
				+ "leaving the tracked files untouched.")]
			string? outputDir = null)
		{
			EditorPipelineCli.RequireFreshAssets();
			return EditorPipelineCli.Complete(Generate(outputDir), ok: true);
		}

		// Pipeline entry point, reached as `unity command check_docs`. The drift guard: regenerate into
		// a temp dir and compare against the committed copies, without ever touching them. The bash
		// version shelled out to `diff -u`; a resident editor has no shell, so the comparison is done
		// in-process and reported as a unified-diff-style excerpt.
		[CliCommand("check_docs",
			"Verify the committed Assets/docs/*.md still match freshly generated output. Fails the "
			+ "command when they have drifted, with the differing lines as the error message.",
			Tags = new[] { "assembler/docs" })]
		public static PipelineReport CheckDocsCommand()
		{
			EditorPipelineCli.RequireFreshAssets();

			// Outside the project so the AssetDatabase never sees the scratch copies.
			string scratch = Path.Combine(Path.GetTempPath(),
				"assembler-check-docs-" + Guid.NewGuid().ToString("N"));
			try
			{
				Generate(scratch);

				var sb = new StringBuilder();
				sb.AppendLine("================ doc drift check ================");

				bool stale = false;
				foreach (string name in new[] { BehaviourDocs.FileName, LibraryDocs.FileName })
				{
					stale |= AppendComparison(sb, name, scratch);
				}

				sb.AppendLine();
				sb.AppendLine(stale
					? "Doc drift detected: committed Assets/docs/*.md is out of date. "
						+ "Run `generate_docs` and commit the result."
					: $"Docs are in sync: committed {BehaviourDocs.FileName} and {LibraryDocs.FileName} "
						+ "match generated output.");
				sb.AppendLine("=================================================");

				return EditorPipelineCli.Complete(sb.ToString(), !stale);
			}
			finally
			{
				if (Directory.Exists(scratch))
				{
					Directory.Delete(scratch, recursive: true);
				}
			}
		}

		// Writes both docs and returns the summary line. Passing outputDir redirects the write to a
		// scratch location; null writes to the committed Assets/docs path and refreshes so the editor
		// picks the new files up.
		private static string Generate(string? outputDir)
		{
			BehaviourDocs.WriteDocs(outputDir);
			LibraryDocs.WriteDocs(outputDir);

			// Only refresh when we wrote to the committed Assets/docs location; a redirected
			// scratch dir lives outside the project so the AssetDatabase has nothing to pick up.
			if (outputDir is null)
			{
				AssetDatabase.Refresh();
			}

			return $"DocsBatch: generated {BehaviourDocs.FileName} and {LibraryDocs.FileName}"
				+ (outputDir is null ? "." : $" into {outputDir}.");
		}

		// Compares one freshly generated file against its committed copy, appending a report section.
		// Returns true when they differ. A committed file that was never generated counts as drift too.
		private static bool AppendComparison(StringBuilder sb, string name, string freshDir)
		{
			string fresh = Path.Combine(freshDir, name);
			string committed = Path.Combine(CommittedDocsDir, name);

			if (!File.Exists(fresh))
			{
				sb.AppendLine($"FAIL  {name}  (the generator did not produce it)");
				return true;
			}

			if (!File.Exists(committed))
			{
				sb.AppendLine($"FAIL  {name}  (no committed copy at {committed})");
				return true;
			}

			string freshText = File.ReadAllText(fresh);
			string committedText = File.ReadAllText(committed);
			if (string.Equals(freshText, committedText, StringComparison.Ordinal))
			{
				sb.AppendLine($"OK    {name}");
				return false;
			}

			sb.AppendLine($"FAIL  {name}  (drifted)");
			AppendDiff(sb, name, committedText, freshText);
			return true;
		}

		// Renders the differing region of two texts. Trims the common prefix and suffix so the report
		// shows only what actually changed, then lists the committed lines as "-" and the generated
		// lines as "+" — enough to see what regenerating would do without reproducing whole files.
		private static void AppendDiff(StringBuilder sb, string name, string committedText, string freshText)
		{
			string[] committed = SplitLines(committedText);
			string[] fresh = SplitLines(freshText);

			int prefix = 0;
			while (prefix < committed.Length && prefix < fresh.Length
				&& string.Equals(committed[prefix], fresh[prefix], StringComparison.Ordinal))
			{
				prefix++;
			}

			// Stop the suffix scan before it overlaps the prefix, or the two ends would double-count
			// the same lines on a pure insertion or deletion.
			int suffix = 0;
			int maxSuffix = Math.Min(committed.Length, fresh.Length) - prefix;
			while (suffix < maxSuffix
				&& string.Equals(committed[committed.Length - 1 - suffix], fresh[fresh.Length - 1 - suffix],
					StringComparison.Ordinal))
			{
				suffix++;
			}

			sb.AppendLine($"--- committed/{name}");
			sb.AppendLine($"+++ generated/{name}");
			sb.AppendLine($"@@ line {prefix + 1} @@");
			AppendSide(sb, "-", committed, prefix, suffix);
			AppendSide(sb, "+", fresh, prefix, suffix);
		}

		private static void AppendSide(StringBuilder sb, string marker, string[] lines, int prefix, int suffix)
		{
			int end = lines.Length - suffix;
			int shown = 0;
			for (int i = prefix; i < end; i++)
			{
				if (shown == MaxDiffLinesPerSide)
				{
					sb.AppendLine($"{marker} … {end - i} more line(s) elided");
					return;
				}

				sb.AppendLine(marker + " " + lines[i]);
				shown++;
			}
		}

		// Splits on newlines without normalising them away, so a pure line-ending change still reads as
		// drift rather than silently comparing equal.
		private static string[] SplitLines(string text) =>
			text.Split('\n');
	}
}
