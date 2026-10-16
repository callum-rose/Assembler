using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spike.CompilerHarness.Editor
{
	/// <summary>
	/// Drives the harness scene through editor Play mode headlessly, so the full runner — including the
	/// descriptor half, which the batch case runner does not touch — can be verified before handover.
	///
	/// Play mode triggers a domain reload, which wipes statics, so the poller re-attaches itself after
	/// the reload via <see cref="InitializeOnLoadMethodAttribute"/> and keeps its state in
	/// <see cref="SessionState"/> (which survives the reload).
	///
	/// Editor-only, throwaway, and disposed with the rest of <c>Assets/Spike/</c>.
	/// </summary>
	public static class CompilerSpikePlayModeBatch
	{
		private const string ScenePath = "Assets/Spike/CompilerHarness/CompilerHarness.unity";
		private const string ActiveKey = "spike.playmode.active";
		private const string DeadlineKey = "spike.playmode.deadline";

		// Generous: the flat corpus compiles 233 expression trees, and the descriptor half then runs the
		// whole build pipeline. Better to over-wait in a headless run than truncate the summary.
		private const double PlaySeconds = 90;

		[MenuItem("Assembler/Spike/Run Harness In Play Mode (headless)")]
		public static void RunPlayMode()
		{
			EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

			SessionState.SetBool(ActiveKey, true);
			SessionState.SetFloat(DeadlineKey, (float)(EditorApplication.timeSinceStartup + PlaySeconds));

			EditorApplication.EnterPlaymode();
		}

		[InitializeOnLoadMethod]
		private static void ReattachAfterDomainReload()
		{
			if (!SessionState.GetBool(ActiveKey, false))
			{
				return;
			}

			EditorApplication.update += Poll;
		}

		private static void Poll()
		{
			if (!SessionState.GetBool(ActiveKey, false))
			{
				EditorApplication.update -= Poll;
				return;
			}

			if (EditorApplication.timeSinceStartup < SessionState.GetFloat(DeadlineKey, 0f))
			{
				return;
			}

			EditorApplication.update -= Poll;
			SessionState.SetBool(ActiveKey, false);

			Debug.Log("COMPILER-SPIKE PLAYMODE: deadline reached, exiting.");

			if (EditorApplication.isPlaying)
			{
				EditorApplication.ExitPlaymode();
			}

			if (Application.isBatchMode)
			{
				EditorApplication.Exit(0);
			}
		}
	}
}
