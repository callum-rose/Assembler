#nullable enable

using Assembler.AssetGeneration.EditorCommon;
using Assembler.AssetGeneration.TextToImage;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.TextToImage.Editor
{
	/// <summary>
	/// Editor window: type a prompt, pick a provider, and write the
	/// generated image to a chosen path. Everything (provider, model, per-provider
	/// API key, prompt, output path) is persisted in <see cref="EditorPrefs"/>.
	/// </summary>
	public sealed class ImageGenerationWindow : EditorWindow
	{
		private const string ProviderPref = "Assembler.ImageGen.Provider";
		private const string ModelPref = "Assembler.ImageGen.Model";
		private const string PromptPref = "Assembler.ImageGen.Prompt";
		private const string OutputDirPref = "Assembler.ImageGen.OutputDir";
		private const string OutputFilePref = "Assembler.ImageGen.OutputFile";
		private const string ReferenceImagePref = "Assembler.ImageGen.ReferenceImage";

		// Canonical per-provider key id (shared with the pipeline window), plus the two legacy pref
		// keys the key may still live under so an already-entered key isn't lost.
		private static string KeyId(ImageProvider provider) => $"Image.{provider}";

		private static string[] LegacyKeys(ImageProvider provider) => new[]
		{
			$"Assembler.ImageGen.ApiKey.{provider}",
			$"Assembler.TextToVoxel.ImageApiKey.{provider}",
		};

		private ImageProvider _provider = ImageProvider.GoogleGemini;
		private string _apiKey = "";
		private string _model = "";
		private string _prompt = "";
		private string _outputDir = "";
		private string _outputFile = "";
		private string _referenceImage = "";

		private WindowRunState _run = null!;
		private readonly TexturePreview _preview = new();
		private readonly TexturePreview _referencePreview = new();
		private string _referencePreviewPath = "";
		private Vector2 _windowScroll;

		[MenuItem("Assembler/Voxelisation/Text to Image")]
		public static void Open()
		{
			var window = GetWindow<ImageGenerationWindow>("Text to Image");
			window.minSize = new Vector2(460, 520);
		}

		private void OnEnable()
		{
			_run = new WindowRunState(Repaint);
			_provider = (ImageProvider)EditorPrefs.GetInt(ProviderPref, (int)ImageProvider.GoogleGemini);
			_model = EditorPrefs.GetString(ModelPref, ImageGeneratorFactory.DefaultModelFor(_provider));
			_prompt = EditorPrefs.GetString(PromptPref, "");
			_outputDir = EditorPrefs.GetString(OutputDirPref, "Assets/GeneratedImages");
			_outputFile = EditorPrefs.GetString(OutputFilePref, "");
			_referenceImage = EditorPrefs.GetString(ReferenceImagePref, "");
			_apiKey = ApiKeyStore.Load(KeyId(_provider), LegacyKeys(_provider));
		}

		private void OnDisable()
		{
			_preview.Clear();
			_referencePreview.Clear();
		}

		private void OnGUI()
		{
			_windowScroll = EditorGUILayout.BeginScrollView(_windowScroll);

			using (new EditorGUI.DisabledScope(_run.IsRunning))
			{
				DrawProvider();
				DrawModel();
				EditorGUILayout.Space();
				DrawApiKey();
				EditorGUILayout.Space();
				DrawPrompt();
				DrawReferenceImage();
				DrawOutputPicker();
			}

			EditorGUILayout.Space();
			DrawActions();
			EditorGUILayout.Space();
			EditorGUILayout.HelpBox(_run.Status, _run.IsRunning ? MessageType.Info : MessageType.None);
			DrawPreview();

			EditorGUILayout.EndScrollView();
		}

		private void DrawProvider()
		{
			EditorGUI.BeginChangeCheck();
			_provider = (ImageProvider)EditorGUILayout.EnumPopup("Provider", _provider);
			if (EditorGUI.EndChangeCheck())
			{
				// Reload the key/model that belong to the newly-selected provider.
				_apiKey = ApiKeyStore.Load(KeyId(_provider), LegacyKeys(_provider));
				_model = EditorPrefs.GetString(ModelPref + "." + _provider, ImageGeneratorFactory.DefaultModelFor(_provider));
			}
		}

		private void DrawModel() =>
			_model = ModelPopup.Draw("Model", _model, ImageGeneratorFactory.AvailableModelsFor(_provider), "Provider model id.");

		private void DrawApiKey() =>
			_apiKey = ApiKeyField.Draw("API Key", _apiKey, key =>
			{
				ApiKeyStore.Save(KeyId(_provider), key);
				_run.SetStatus("API key saved to EditorPrefs.");
			});

		private void DrawPrompt()
		{
			EditorGUILayout.LabelField("Prompt");
			var wrapStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
			_prompt = EditorGUILayout.TextArea(_prompt, wrapStyle, GUILayout.MinHeight(90));
		}

		private void DrawReferenceImage()
		{
			EditorGUILayout.Space();
			using (new EditorGUILayout.HorizontalScope())
			{
				_referenceImage = EditorGUILayout.TextField(
					new GUIContent("Reference Image", "Optional image to condition generation on (style reference / edit). Drag an image asset here, browse for a file, or leave blank for pure text-to-image."),
					_referenceImage);
				if (GUILayout.Button("Browse", GUILayout.Width(70)))
				{
					var picked = EditorUtility.OpenFilePanel(
						"Reference image", PathField.GuessStartDir(_referenceImage), "png,jpg,jpeg,webp");
					if (!string.IsNullOrEmpty(picked))
					{
						_referenceImage = picked;
					}
				}
				if (!string.IsNullOrEmpty(_referenceImage) && GUILayout.Button("Clear", GUILayout.Width(50)))
				{
					_referenceImage = "";
				}
			}
			_referenceImage = PathField.HandleDrop(GUILayoutUtility.GetLastRect(), _referenceImage);

			DrawReferencePreview();
		}

		private void DrawReferencePreview()
		{
			if (string.IsNullOrEmpty(_referenceImage))
			{
				return;
			}

			// (Re)load the thumbnail only when the path changes, not every repaint.
			if (_referencePreviewPath != _referenceImage)
			{
				_referencePreviewPath = _referenceImage;
				_referencePreview.LoadFile(_referenceImage);
			}

			if (!_referencePreview.HasTexture)
			{
				EditorGUILayout.HelpBox("Reference image not found or unreadable.", MessageType.Warning);
				return;
			}

			_referencePreview.Draw(140);
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
				new GUIContent("File Name", "Leave blank to use a default name. The extension is set from the returned image type."),
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

		private void DrawPreview()
		{
			if (!_preview.HasTexture)
			{
				return;
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
			_preview.Draw(position.width - 30);
		}

		private void Generate()
		{
			// Persist inputs so the next session keeps them.
			EditorPrefs.SetInt(ProviderPref, (int)_provider);
			EditorPrefs.SetString(ModelPref, _model);
			EditorPrefs.SetString(ModelPref + "." + _provider, _model);
			EditorPrefs.SetString(PromptPref, _prompt);
			EditorPrefs.SetString(OutputDirPref, _outputDir);
			EditorPrefs.SetString(OutputFilePref, _outputFile);
			EditorPrefs.SetString(ReferenceImagePref, _referenceImage);
			ApiKeyStore.Save(KeyId(_provider), _apiKey);

			_ = _run.RunAsync(async ct =>
			{
				// Core generation/saving lives in ImageGenerationCore so it can be driven
				// headlessly or as one stage of the image → mesh → voxels pipeline.
				var result = await ImageGenerationCore.GenerateAsync(
					_provider, _apiKey, _model, _prompt, _outputDir, _outputFile, ct, _run.SetStatus,
					string.IsNullOrWhiteSpace(_referenceImage) ? null : _referenceImage);

				// The core is editor-agnostic, so surfacing the freshly-written file in the Project
				// view is the window's job (only when it lands inside Assets/).
				if (AssetPaths.IsUnderAssets(result.OutputPath))
				{
					AssetDatabase.Refresh();
				}

				_preview.Load(result.Image.Bytes);
			});
		}
	}
}
