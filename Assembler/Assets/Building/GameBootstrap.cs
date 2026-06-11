using System.IO;
using UnityEngine;

namespace Assembler.Building
{
	/// <summary>
	/// Runtime entry point for player builds (iOS, etc.) where the editor's Game Launcher window is
	/// unavailable. Drop this on a GameObject in the boot scene and it loads a single descriptor from
	/// <see cref="Application.streamingAssetsPath"/> on <c>Start</c> and runs it through the normal
	/// <see cref="Builder.Build(string, Input.InputPlatform?)"/> pipeline — the same path the editor uses.
	///
	/// The descriptor must be shipped under <c>Assets/StreamingAssets/</c> so it is packaged into the build;
	/// on iOS those files sit inside the app bundle and are readable synchronously with <see cref="File"/>.
	/// </summary>
	public sealed class GameBootstrap : MonoBehaviour
	{
		[Tooltip("Descriptor file name under StreamingAssets to load on start, e.g. \"MiniRacer3D.yaml\".")]
		[SerializeField] private string _descriptorFileName = "MiniRacer3D.yaml";

		private void Start()
		{
			var path = Path.Combine(Application.streamingAssetsPath, _descriptorFileName);

			if (!File.Exists(path))
			{
				// Fully qualified: Assembler.Building has a nested `Debug` namespace that shadows the simple name.
				UnityEngine.Debug.LogError($"GameBootstrap: descriptor not found at '{path}'. " +
					"Ensure it is copied into Assets/StreamingAssets and included in the build.");
				return;
			}

			try
			{
				// overridePlatform left null: PlatformSelector resolves to Mobile on iOS, which falls back to
				// the descriptor's desktop bindings (see PlatformFallback). The point of this build is to
				// exercise the runtime expression compiler under iOS AOT, so surface any failure loudly.
				Builder.Build(path);
				UnityEngine.Debug.Log($"GameBootstrap: built '{_descriptorFileName}' successfully.");
			}
			catch (System.Exception e)
			{
				UnityEngine.Debug.LogError($"GameBootstrap: failed to build '{_descriptorFileName}': {e}");
			}
		}
	}
}
