using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spike.CompilerHarness.Editor
{
	/// <summary>
	/// One-shot setup for the spike's scene. Builds <c>CompilerHarness.unity</c> programmatically and
	/// registers it as the <b>first</b> entry in <c>EditorBuildSettings</c> so a player build boots
	/// straight into the harness.
	///
	/// Doing this in code rather than hand-authoring the <c>.unity</c> YAML keeps the script reference a
	/// real GUID binding that Unity resolves, instead of a hand-copied one that silently turns into a
	/// missing-script component in a player build — which would read as an AOT failure and isn't one.
	///
	/// The existing <c>Bootstrap.unity</c> entry is left <b>enabled and untouched</b>, just no longer
	/// first: nothing loads it, so it costs a little build size and changes no shipping configuration.
	///
	/// <b>Disposal:</b> run <c>Assembler > Spike > Remove Harness Scene From Build Settings</c>, then
	/// delete <c>Assets/Spike/</c> and <c>Assets/StreamingAssets/StressTest.yaml</c>.
	/// </summary>
	public static class CompilerSpikeSceneSetup
	{
		private const string ScenePath = "Assets/Spike/CompilerHarness/CompilerHarness.unity";

		[MenuItem("Assembler/Spike/Create Harness Scene And Add To Build Settings")]
		public static void CreateScene()
		{
			// EmptyScene, not DefaultGameObjects: the descriptor declares its own `camera` entity, and a
			// stray Main Camera would fight it for rendering. The readout is Debug.Log only, so the black
			// frame before the descriptor builds costs nothing.
			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

			var runner = new GameObject("Compiler Spike Runner");
			runner.AddComponent<CompilerSpikeRunner>();

			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene, ScenePath);

			AddSceneToBuildSettings();

			Debug.Log($"COMPILER-SPIKE SETUP: created '{ScenePath}' and made it build scene 0.");
		}

		[MenuItem("Assembler/Spike/Remove Harness Scene From Build Settings")]
		public static void RemoveSceneFromBuildSettings()
		{
			var remaining = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToArray();

			if (remaining.Length == EditorBuildSettings.scenes.Length)
			{
				Debug.Log("COMPILER-SPIKE SETUP: harness scene was not in build settings; nothing to do.");
				return;
			}

			EditorBuildSettings.scenes = remaining;
			Debug.Log($"COMPILER-SPIKE SETUP: removed '{ScenePath}' from build settings.");
		}

		private static void AddSceneToBuildSettings()
		{
			var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

			// Idempotent: drop any existing entry first, then re-insert at index 0. Re-running the setup
			// must not leave two entries or demote the harness out of the startup slot.
			scenes.RemoveAll(s => s.path == ScenePath);
			scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));

			EditorBuildSettings.scenes = scenes.ToArray();
		}
	}
}
