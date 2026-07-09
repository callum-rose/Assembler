using System.IO;
using Assembler.Anthropic;
using Assembler.AssetGeneration.EditorCommon;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.ImageOrientation.Editor
{
	/// <summary>
	/// Editor window that takes an image and asks Claude which direction the front
	/// of the main object in it is facing, returning one of the eight compass codes
	/// (L, R, U, D, LU, LD, RU, RD). The model is selectable and defaults to Haiku.
	/// </summary>
	public sealed class ImageOrientationWindow : EditorWindow
	{
		// Shared with the other generation windows so the key is entered once.
		private const string ApiKeyPref = "Assembler.Generation.ApiKey";
		private const string ModelPref = "Assembler.AssetGeneration.ImageOrientation.Model";
		private const string ImagePathPref = "Assembler.AssetGeneration.ImageOrientation.ImagePath";

		private const string DefaultModel = "claude-haiku-4-5-20251001";

		private string _apiKey = string.Empty;
		private string _model = DefaultModel;
		private string _imagePath = string.Empty;

		private readonly AnthropicModelSelector _modelSelector = new();
		private Texture2D? _preview;

		private OrientationResult? _result;
		private Vector2 _scroll;
		private WindowRunState _run = null!;

		[MenuItem("Assembler/Voxelisation/Image Facing Direction")]
		public static void Open()
		{
			var window = GetWindow<ImageOrientationWindow>("Image Facing Direction");
			window.minSize = new Vector2(420, 520);
			window.Show();
		}

		private void OnEnable()
		{
			_run = new WindowRunState(Repaint, string.Empty);
			_apiKey = EditorPrefs.GetString(ApiKeyPref, string.Empty);
			_model = EditorPrefs.GetString(ModelPref, DefaultModel);
			_imagePath = EditorPrefs.GetString(ImagePathPref, string.Empty);
			LoadPreview();
		}

		private void OnDisable() => _run.Cancel();

		private void OnGUI()
		{
			var wrapLabel = new GUIStyle(EditorStyles.label) { wordWrap = true };

			_scroll = EditorGUILayout.BeginScrollView(_scroll);

			EditorGUILayout.LabelField("API key (stored in EditorPrefs)", EditorStyles.boldLabel);
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				_apiKey = EditorGUILayout.PasswordField(_apiKey);
				if (scope.changed)
				{
					EditorPrefs.SetString(ApiKeyPref, _apiKey);
				}
			}

			EditorGUILayout.Space();
			_model = _modelSelector.Draw("Model", _model, ModelPref, RefreshModels);

			EditorGUILayout.Space();
			DrawImageSelector();

			EditorGUILayout.Space();
			using (new EditorGUI.DisabledScope(_run.IsRunning))
			{
				if (GUILayout.Button(_run.IsRunning ? "Asking Claude..." : "Determine facing direction"))
				{
					StartClassify();
				}
			}
			using (new EditorGUI.DisabledScope(!_run.IsRunning))
			{
				if (GUILayout.Button("Cancel"))
				{
					_run.Cancel();
				}
			}

			if (!string.IsNullOrEmpty(_run.Status))
			{
				EditorGUILayout.Space();
				EditorGUILayout.HelpBox(_run.Status, MessageType.Info);
			}

			if (_result is { } result)
			{
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);

				var bigCode = new GUIStyle(EditorStyles.boldLabel) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
				EditorGUILayout.LabelField(result.Code, bigCode, GUILayout.Height(40));

				switch (result.Answer)
				{
					case OrientationAnswer.Facing { Direction: var direction }:
						var caption = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
						EditorGUILayout.LabelField($"The front is {direction.Describe()}.", caption);
						break;
					case OrientationAnswer.Unsure:
						EditorGUILayout.HelpBox(
							"Claude was unsure which direction the front is facing.",
							MessageType.Info);
						break;
					default:
						EditorGUILayout.HelpBox(
							"Claude's reply didn't contain a recognisable code. Raw response below.",
							MessageType.Warning);
						break;
				}

				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Raw response", EditorStyles.miniBoldLabel);
				EditorGUILayout.SelectableLabel(result.RawResponse, wrapLabel, GUILayout.Height(40));
			}

			EditorGUILayout.EndScrollView();
		}

		private void DrawImageSelector()
		{
			EditorGUILayout.LabelField("Image", EditorStyles.boldLabel);

			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				var texture = (Texture2D?)EditorGUILayout.ObjectField(
					"In-project texture", _preview, typeof(Texture2D), allowSceneObjects: false);
				if (scope.changed && texture != null)
				{
					var path = AssetDatabase.GetAssetPath(texture);
					if (!string.IsNullOrEmpty(path))
					{
						SetImagePath(path);
					}
				}
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Browse..."))
				{
					var path = EditorUtility.OpenFilePanel(
						"Select image", PathField.GuessStartDir(_imagePath), "png,jpg,jpeg,gif,webp");
					if (!string.IsNullOrEmpty(path))
					{
						SetImagePath(path);
					}
				}
				if (!string.IsNullOrEmpty(_imagePath) && GUILayout.Button("Clear", GUILayout.Width(60)))
				{
					SetImagePath(string.Empty);
				}
			}

			if (!string.IsNullOrEmpty(_imagePath))
			{
				EditorGUILayout.LabelField(_imagePath, EditorStyles.miniLabel);
			}

			if (_preview != null)
			{
				var rect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true));
				GUI.DrawTexture(rect, _preview, ScaleMode.ScaleToFit);
			}
		}

		private void SetImagePath(string path)
		{
			_imagePath = path;
			EditorPrefs.SetString(ImagePathPref, _imagePath);
			LoadPreview();
		}

		private void LoadPreview()
		{
			_preview = null;
			if (string.IsNullOrEmpty(_imagePath) || !File.Exists(_imagePath))
			{
				return;
			}

			// Prefer the imported asset (respects its import settings); fall back to
			// decoding the raw bytes for images that live outside the project.
			var assetPath = AssetPaths.ToAssetRelative(_imagePath);
			if (assetPath != null)
			{
				_preview = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
			}

			if (_preview == null)
			{
				var texture = new Texture2D(2, 2);
				if (texture.LoadImage(File.ReadAllBytes(_imagePath)))
				{
					_preview = texture;
				}
			}
		}

		private async void RefreshModels()
		{
			if (string.IsNullOrWhiteSpace(_apiKey))
			{
				_run.SetStatus("ERROR: enter an API key before refreshing models.");
				return;
			}

			try
			{
				_run.SetStatus("Fetching available models...");
				_run.SetStatus(await _modelSelector.RefreshAsync(_apiKey, key => AnthropicClient.ListModelsAsync(key)));
			}
			catch (System.Exception ex)
			{
				_run.SetStatus("Error fetching models: " + ex.Message);
			}
		}

		private void StartClassify()
		{
			if (string.IsNullOrWhiteSpace(_apiKey))
			{
				_run.SetStatus("ERROR: API key is required.");
				return;
			}
			if (string.IsNullOrEmpty(_imagePath) || !File.Exists(_imagePath))
			{
				_run.SetStatus("ERROR: select an image first.");
				return;
			}

			_result = null;
			_run.SetStatus("Contacting Claude...");
			_ = _run.RunAsync(async ct =>
			{
				var bytes = File.ReadAllBytes(_imagePath);
				var mediaType = AnthropicImage.MediaTypeFromExtension(Path.GetExtension(_imagePath));
				_result = await ImageFacingDirection.DetermineAsync(_apiKey, bytes, mediaType, _model, ct);
				_run.SetStatus(_result.Answer switch
				{
					OrientationAnswer.Facing { Direction: var d } => $"Done — {d.ToCode()}.",
					OrientationAnswer.Unsure => "Done — Claude was unsure.",
					_ => "Done, but no code recognised.",
				});
			});
		}
	}
}
