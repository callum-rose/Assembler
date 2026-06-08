using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Editor
{
	// Headless entry point for regenerating the behaviour and library docs without the editor UI.
	// Invoked from Tools/generate-docs.sh via:
	//   Unity -batchmode -quit -nographics -projectPath <project>
	//         -executeMethod Editor.DocsBatch.GenerateAll -logFile -
	// Reuses the exact same generation code as the Assembler menu items (BehaviourDocs.WriteDocs /
	// LibraryDocs.WriteDocs) so the headless output is identical to running them in-editor.
	//
	// Tools/check-docs.sh invokes the same method with an extra "-outputDir <dir>" arg to regenerate
	// into a scratch directory instead of overwriting the committed Assets/docs files, then diffs the
	// two to detect drift. When redirected, the committed assets are untouched so no AssetDatabase
	// refresh is needed.
	public static class DocsBatch
	{
		public static void GenerateAll()
		{
			try
			{
				string[] args = Environment.GetCommandLineArgs();
				List<string> outputDirs = EditorBatchCli.ArgValues(args, "-outputDir");
				string? outputDir = outputDirs.Count > 0 ? outputDirs[^1] : null;

				BehaviourDocs.WriteDocs(outputDir);
				LibraryDocs.WriteDocs(outputDir);

				// Only refresh when we wrote to the committed Assets/docs location; a redirected
				// scratch dir lives outside the project so the AssetDatabase has nothing to pick up.
				if (outputDir is null)
					AssetDatabase.Refresh();

				Debug.Log($"DocsBatch: generated {BehaviourDocs.FileName} and {LibraryDocs.FileName}"
					+ (outputDir is null ? "." : $" into {outputDir}."));
				EditorApplication.Exit(0);
			}
			catch (Exception e)
			{
				// Surface the failure and propagate a non-zero exit so the runner script and Claude
				// can detect it. Doc-gen warnings are non-fatal and remain inline in the markdown.
				Debug.LogError("DocsBatch failed: " + e);
				EditorApplication.Exit(1);
			}
		}
	}
}
