#nullable enable

using Assembler.AssetGeneration.EditorCommon;
using Assembler.AssetGeneration.ImageToMesh;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.ImageToMesh.Editor
{
	/// <summary>
	/// Editor window: take a reference image, send it to Meshy.ai's
	/// image-to-3D endpoint, and download the resulting textured model (OBJ or
	/// FBX) to a chosen output path.
	/// </summary>
	public sealed class MeshyImageTo3DWindow : EditorWindow
	{
		private const string Pref = "Meshy.ImageTo3D.";
		private const string ImagePathPref = Pref + "ImagePath";
		private const string OutputDirPref = Pref + "OutputDir";
		private const string OutputFilePref = Pref + "OutputFile";

		private string _apiKey = "";
		private string _imagePath = "";
		private string _outputDir = "";
		private string _outputFile = "";
		private MeshyRequest _meshy = MeshySettingsGui.Default();

		private WindowRunState _run = null!;

		[MenuItem("Assembler/Voxelisation/Image to Mesh")]
		public static void Open()
		{
			var window = GetWindow<MeshyImageTo3DWindow>("Image to Mesh");
			window.minSize = new Vector2(460, 600);
		}

		private void OnEnable()
		{
			_run = new WindowRunState(Repaint);
			_apiKey = ApiKeyStore.Load("Meshy", "Meshy.ImageTo3D.ApiKey", "Assembler.TextToVoxel.MeshyApiKey");
			_imagePath = EditorPrefs.GetString(ImagePathPref, "");
			_outputDir = EditorPrefs.GetString(OutputDirPref, "Assets/MeshyOutput");
			_outputFile = EditorPrefs.GetString(OutputFilePref, "");
			_meshy = MeshySettingsGui.Load(Pref);
		}

		private void OnGUI()
		{
			using (new EditorGUI.DisabledScope(_run.IsRunning))
			{
				DrawApiKey();
				EditorGUILayout.Space();
				DrawImagePicker();
				DrawOutputPicker();
				EditorGUILayout.Space();

				_meshy = MeshySettingsGui.Draw(_meshy);
			}

			EditorGUILayout.Space();
			DrawActions();
			EditorGUILayout.Space();
			EditorGUILayout.HelpBox(_run.Status, _run.IsRunning ? MessageType.Info : MessageType.None);
		}

		private void DrawApiKey() =>
			_apiKey = ApiKeyField.Draw("API Key", _apiKey, key =>
			{
				ApiKeyStore.Save("Meshy", key);
				_run.SetStatus("API key saved to EditorPrefs.");
			});

		private void DrawImagePicker()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				_imagePath = EditorGUILayout.TextField(
					new GUIContent("Reference Image", "Drag an image asset here, or browse for a file on disk."), _imagePath);
				if (GUILayout.Button("Browse", GUILayout.Width(70)))
				{
					var picked = EditorUtility.OpenFilePanel(
						"Select reference image", PathField.GuessStartDir(_imagePath), "png,jpg,jpeg,webp");
					if (!string.IsNullOrEmpty(picked))
					{
						_imagePath = picked;
					}
				}
			}
			_imagePath = PathField.HandleDrop(GUILayoutUtility.GetLastRect(), _imagePath);
		}

		private void DrawOutputPicker()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				_outputDir = EditorGUILayout.TextField("Output Directory", _outputDir);
				if (GUILayout.Button("Browse", GUILayout.Width(70)))
				{
					var picked = EditorUtility.OpenFolderPanel("Output directory", PathField.GuessStartDir(_outputDir), "");
					if (!string.IsNullOrEmpty(picked))
					{
						_outputDir = picked;
					}
				}
			}
			_outputDir = PathField.HandleDrop(GUILayoutUtility.GetLastRect(), _outputDir, wantFolder: true);

			_outputFile = EditorGUILayout.TextField(
				new GUIContent("File Name", "Leave blank to use the downloaded model's filename. The extension is set from the output format."),
				_outputFile);
		}

		private void DrawActions()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				using (new EditorGUI.DisabledScope(_run.IsRunning))
				{
					if (GUILayout.Button("Generate", GUILayout.Height(30)))
					{
						Generate();
					}
				}
				using (new EditorGUI.DisabledScope(!_run.IsRunning))
				{
					if (GUILayout.Button("Cancel", GUILayout.Height(30), GUILayout.Width(100)))
					{
						_run.Cancel();
					}
				}
			}
		}

		private void Generate()
		{
			// Persist inputs so the next session keeps them.
			ApiKeyStore.Save("Meshy", _apiKey);
			EditorPrefs.SetString(ImagePathPref, _imagePath);
			EditorPrefs.SetString(OutputDirPref, _outputDir);
			EditorPrefs.SetString(OutputFilePref, _outputFile);
			MeshySettingsGui.Save(Pref, _meshy);

			_ = _run.RunAsync(async ct =>
			{
				// Core submit/poll/download lives in MeshyConversionCore so it can be driven
				// headlessly or as one stage of the image → mesh → voxels pipeline.
				var request = MeshySettingsGui.WithImagePath(_meshy, _imagePath);
				var result = await MeshyConversionCore.ConvertAsync(
					_apiKey, request, _outputDir, _outputFile, ct, _run.SetStatus);

				// The core is runtime-only and no longer touches the AssetDatabase; surface a
				// download that landed inside the project ourselves so Unity imports it.
				if (AssetPaths.IsUnderAssets(result.OutputPath))
				{
					AssetDatabase.Refresh();
				}
			});
		}
	}
}
