#nullable enable
using System;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Editor
{
	// Headless helper used by Tools/check-compile.sh ONLY in its --all mode: forces a clean recompile
	// of every script assembly so that ALL compiler warnings — not just the code that changed since
	// the last compile — are written to the log, then exits the editor. The shell script parses the
	// log for the actual diagnostics, because Unity's CompilationPipeline message callbacks do NOT
	// reliably deliver warnings in batch mode (they arrive empty even when csc logged the warning), so
	// the log is the only trustworthy source.
	//
	// The DEFAULT (incremental) mode of check-compile.sh doesn't use this at all: a plain
	// `-batchmode -quit` boot recompiles whatever changed on disk and logs those diagnostics, which is
	// the common "did the file I just edited compile cleanly?" check and keeps the report scoped to
	// the user's change rather than dumping every pre-existing project warning.
	//
	// Invoked via:  Unity -batchmode -nographics -projectPath <project>
	//                     -executeMethod Editor.CompileCheckBatch.RecompileAll -logFile -
	//
	// Do NOT pass -quit here: RequestScriptCompilation is asynchronous (compilation runs across editor
	// update frames); -quit would kill the editor before it finishes and before its diagnostics reach
	// the log. Instead this keeps the editor alive and calls EditorApplication.Exit() itself from
	// compilationFinished, once the recompile and its log output are done.
	public static class CompileCheckBatch
	{
		private static double _startTime;

		// Safety net: if compilation never reports completion, force-exit rather than hang the batch
		// process forever. Generous because a fresh worktree's first run recompiles everything cold.
		private const double TimeoutSeconds = 900;

		public static void RecompileAll()
		{
			EditorBatchCli.SuppressLogStackTraces();
			try
			{
				_startTime = EditorApplication.timeSinceStartup;
				CompilationPipeline.compilationFinished += OnCompilationFinished;
				EditorApplication.update += OnUpdate;

				Debug.Log("CompileCheckBatch: forcing a clean recompile of all script assemblies...");
				CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
				// Returns immediately; OnCompilationFinished exits the editor when compilation completes.
			}
			catch (Exception e)
			{
				Debug.LogError("CompileCheckBatch failed to start: " + e);
				EditorApplication.Exit(1);
			}
		}

		private static void OnUpdate()
		{
			if (EditorApplication.timeSinceStartup - _startTime > TimeoutSeconds)
			{
				Debug.LogError($"CompileCheckBatch: compilation did not finish within {TimeoutSeconds:0}s — aborting.");
				Finish(2);
			}
		}

		// Exit 0 unconditionally: the shell script decides pass/fail by parsing the compiler
		// diagnostics out of the log (and honours --warnings-as-errors there). Compile ERRORS abort
		// the batch before this method can ever run — Unity refuses to invoke -executeMethod when
		// scripts don't compile — so reaching here means the recompile produced no errors.
		private static void OnCompilationFinished(object context) => Finish(0);

		private static void Finish(int exitCode)
		{
			CompilationPipeline.compilationFinished -= OnCompilationFinished;
			EditorApplication.update -= OnUpdate;
			EditorApplication.Exit(exitCode);
		}
	}
}
