using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Editor
{
	// Shared plumbing for the [CliCommand] entry points that expose the editor batch tools over
	// com.unity.pipeline (see Assembler/CLAUDE.md › Build & Test). The pipeline counterpart of
	// EditorBatchCli: that one serves one-shot -batchmode processes, this one serves a *resident*
	// editor, and the difference in lifetime is what everything here is about.
	//
	// Three rules follow from the editor being resident, and every command respects them:
	//
	//  1. Never call EditorBatchCli.SuppressLogStackTraces() — it is a process-wide setting that a
	//     batch process gets away with only because it exits immediately afterwards. From a resident
	//     editor it would silently mute stack traces for the rest of the session.
	//  2. Never call EditorApplication.Exit() — it would take the user's editor down with it.
	//  3. Read nothing without refreshing first. A batch process boots against the on-disk truth; a
	//     resident editor holds imported state that a `git checkout` or a hand-edited .yaml has
	//     already invalidated. Staleness here is worse than a crash because the answer comes back
	//     confident and wrong, so RequireFreshAssets() is mandatory, not advisory.
	internal static class EditorPipelineCli
	{
		// Refreshes the AssetDatabase so the command reads what is actually on disk, and refuses to
		// run at all if the editor is mid-import or mid-compile — the two states in which any answer
		// would describe a project that no longer exists.
		//
		// Bails rather than waiting: a queued script recompile ends in a domain reload that would tear
		// down this very call, and blocking for it would burn the command's main-thread budget (60s by
		// default; see the class docs on each command). The caller drives `recompile` /
		// `recompile_status` and retries, which is the same sequence the pipeline's own commands ask
		// for when they hit uncompiled types.
		public static void RequireFreshAssets()
		{
			if (EditorApplication.isCompiling || EditorApplication.isUpdating)
			{
				throw new InvalidOperationException(
					"editor is busy importing or compiling — poll `recompile_status` until it reports "
					+ "completed, then retry.");
			}

			AssetDatabase.Refresh();

			// Refresh picked up changed source: compilation is now queued and a domain reload is
			// imminent, so anything read from here would be pre-change state.
			if (EditorApplication.isCompiling || EditorApplication.isUpdating)
			{
				throw new InvalidOperationException(
					"refreshing picked up on-disk changes and queued a recompile — poll "
					+ "`recompile_status` until it reports completed, then retry.");
			}
		}

		// Turns a command's report into its result, mapping a failed check onto a thrown exception so
		// the pipeline answers `success: false` and the CLI exits non-zero. That keeps the exit-code
		// contract the Tools/*.sh scripts had: callers that only look at the exit status (CI, and
		// RemoteTooling's publish loop) still see pass/fail without parsing the report. The report
		// itself travels as the exception message, so a failure is never silent.
		public static PipelineReport Complete(string report, bool ok)
		{
			if (!ok)
			{
				throw new PipelineCheckFailedException(report);
			}

			return new PipelineReport(report);
		}

		// Splits a comma-separated CLI argument into targets, dropping blanks. Commands take their
		// file/directory lists this way because [CliArg] carries scalars, not arrays; a caller with
		// many targets passes "a.yaml,b.yaml" or falls back to a containing directory.
		public static List<string> SplitTargets(string? value) =>
			string.IsNullOrWhiteSpace(value)
				? new List<string>()
				: value.Split(',')
					.Select(t => t.Trim())
					.Where(t => t.Length > 0)
					.ToList();
	}

	// The uniform success payload. A single `report` field keeps the pipeline's JSON shape identical
	// to the text the scripts printed, so existing per-stage reports stay readable verbatim.
	public sealed class PipelineReport
	{
		public PipelineReport(string report)
		{
			this.report = report;
		}

		// Lower-case to match the JSON the pipeline serialises for every other command.
		public string report { get; }
	}

	// A check that ran to completion and came back negative — a failing descriptor, drifted docs, an
	// expression that would not compile. Distinct from an unexpected exception so the message can be
	// the report itself rather than a stack trace.
	public sealed class PipelineCheckFailedException : Exception
	{
		public PipelineCheckFailedException(string report)
			: base(report)
		{
		}
	}
}
