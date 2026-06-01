using System;
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
	public static class DocsBatch
	{
		public static void GenerateAll()
		{
			try
			{
				BehaviourDocs.WriteDocs();
				LibraryDocs.WriteDocs();
				AssetDatabase.Refresh();
				Debug.Log("DocsBatch: generated Behaviours.md and Libraries.md.");
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
