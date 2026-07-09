using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assembler.Anthropic;
using Assembler.AssetGeneration.EditorCommon;
using Assembler.Voxels;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.EyePlacement.Editor
{
	/// <summary>
	/// Editor front-end for <see cref="EyePlacer"/>: pick a <c>.vox</c> file, choose a view
	/// and eye count, and either ask Claude (render → vision → reproject) or run the offline
	/// geometric fallback. Shows the render with the resolved picks drawn over it and lists
	/// the eye coordinates in the model's grid space, ready to copy.
	/// </summary>
	public sealed class EyePlacementWindow : EditorWindow
	{
		private enum ViewPreset { Isometric, IsometricLeft, Front, Top }

		// Shared with the other generation windows so the key is entered once.
		private const string ApiKeyPref = "Assembler.Generation.ApiKey";
		private const string ModelPref = "Assembler.AssetGeneration.EyePlacement.Model";
		private const string VoxPathPref = "Assembler.AssetGeneration.EyePlacement.VoxPath";

		private string _apiKey = string.Empty;
		private string _model = ImageEyePlacer.DefaultModel;
		private string _voxPath = string.Empty;
		private string _imagePath = string.Empty;

		private readonly AnthropicModelSelector _modelSelector = new();

		private bool _autoOrient = true;
		private ViewPreset _view = ViewPreset.Isometric;
		private int _eyeCount = 2;
		private int _imageSize = 1024;
		private float _surfaceOffset = 0.5f;

		private VoxelModel? _model3d;
		private readonly TexturePreview _resultPreview = new();
		private EyePlacementResult? _result;
		private VoxelViewProjection? _resultProjection;
		private readonly List<CandidateView> _candidateViews = new();

		private static readonly Color VisibleColour = new(0.2f, 0.9f, 0.3f);
		private static readonly Color MaskedColour = new(0.95f, 0.3f, 0.2f);

		// One candidate render plus, per eye, where it lands in that view and whether it's occluded.
		private sealed class CandidateView
		{
			public Texture2D? Texture;
			public float Yaw;
			public bool Chosen;
			public readonly List<(Vector2 Uv, bool Masked)> Eyes = new();
		}

		private Vector2 _scroll;
		private WindowRunState _run = null!;

		[MenuItem("Assembler/Voxelisation/Eye Placement")]
		public static void Open()
		{
			var window = GetWindow<EyePlacementWindow>("Eye Placement");
			window.minSize = new Vector2(440, 620);
			window.Show();
		}

		private void OnEnable()
		{
			_run = new WindowRunState(Repaint, string.Empty);
			_apiKey = EditorPrefs.GetString(ApiKeyPref, string.Empty);
			_model = EditorPrefs.GetString(ModelPref, ImageEyePlacer.DefaultModel);
			_voxPath = EditorPrefs.GetString(VoxPathPref, string.Empty);
			LoadModel();
		}

		private void OnDisable()
		{
			_run.Cancel();
			ClearCandidateViews();
			_resultPreview.Clear();
		}

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
			_model = _modelSelector.Draw("Vision model", _model, ModelPref, RefreshModels);

			EditorGUILayout.Space();
			DrawVoxSelector();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
			_autoOrient = EditorGUILayout.Toggle(
				new GUIContent("Auto-orient (detect front)",
					"Runs a top-down vision pass to find the model's front, then looks at it three-quarter. Costs one extra Claude call."),
				_autoOrient);
			using (new EditorGUI.DisabledScope(_autoOrient))
			{
				_view = (ViewPreset)EditorGUILayout.EnumPopup("View", _view);
			}
			_eyeCount = Mathf.Max(1, EditorGUILayout.IntField("Eye count", _eyeCount));
			_imageSize = Mathf.Clamp(EditorGUILayout.IntField("Render size (px)", _imageSize), 64, 4096);
			_surfaceOffset = EditorGUILayout.FloatField("Surface offset (voxels)", _surfaceOffset);

			EditorGUILayout.Space();
			DrawImageOverride();

			EditorGUILayout.Space();
			using (new EditorGUI.DisabledScope(_run.IsRunning || _model3d == null))
			{
				if (GUILayout.Button(_run.IsRunning ? "Asking Claude..." : "Place eyes (Claude vision)"))
				{
					StartPlaceAI();
				}
				if (GUILayout.Button("Place eyes (geometric, offline)"))
				{
					RunGeometric();
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

			DrawResult(wrapLabel);

			EditorGUILayout.EndScrollView();
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
			catch (Exception ex)
			{
				_run.SetStatus("Error fetching models: " + ex.Message);
			}
		}

		private void DrawVoxSelector()
		{
			EditorGUILayout.LabelField("Voxel model (.vox)", EditorStyles.boldLabel);
			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Browse..."))
				{
					var path = EditorUtility.OpenFilePanel("Select .vox file", string.Empty, "vox");
					if (!string.IsNullOrEmpty(path))
					{
						_voxPath = path;
						EditorPrefs.SetString(VoxPathPref, _voxPath);
						LoadModel();
					}
				}
				if (!string.IsNullOrEmpty(_voxPath) && GUILayout.Button("Clear", GUILayout.Width(60)))
				{
					_voxPath = string.Empty;
					_model3d = null;
					EditorPrefs.SetString(VoxPathPref, _voxPath);
				}
			}

			if (!string.IsNullOrEmpty(_voxPath))
			{
				EditorGUILayout.LabelField(_voxPath, EditorStyles.miniLabel);
			}
			if (_model3d is { } m)
			{
				EditorGUILayout.LabelField($"Loaded: {m.Voxels.Count} voxels, size {m.Size}", EditorStyles.miniLabel);
			}
		}

		private void DrawImageOverride()
		{
			EditorGUILayout.LabelField("Optional: use my own image instead of a render", EditorStyles.boldLabel);
			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Browse image..."))
				{
					var path = EditorUtility.OpenFilePanel("Select image", string.Empty, "png,jpg,jpeg,gif,webp");
					if (!string.IsNullOrEmpty(path))
					{
						_imagePath = path;
					}
				}
				if (!string.IsNullOrEmpty(_imagePath) && GUILayout.Button("Clear", GUILayout.Width(60)))
				{
					_imagePath = string.Empty;
				}
			}
			if (!string.IsNullOrEmpty(_imagePath))
			{
				EditorGUILayout.LabelField(_imagePath, EditorStyles.miniLabel);
				EditorGUILayout.HelpBox(
					"The image should frame the model with the chosen view, or the 3D reprojection will drift.",
					MessageType.None);
			}
		}

		private void DrawResult(GUIStyle wrapLabel)
		{
			if (_result is not { } result)
			{
				return;
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Eyes", EditorStyles.boldLabel);
			if (result.Eyes.Count == 0)
			{
				EditorGUILayout.HelpBox("No eyes resolved — the picks may have missed the model surface.", MessageType.Warning);
			}
			foreach (var eye in result.Eyes)
			{
				EditorGUILayout.SelectableLabel(
					$"pos {Format(eye.Position)}   normal {Format(eye.Normal)}",
					EditorStyles.miniLabel, GUILayout.Height(16));
			}

			if (result.Eyes.Count > 0 && GUILayout.Button("Copy coordinates"))
			{
				EditorGUIUtility.systemCopyBuffer = string.Join(
					"\n", result.Eyes.Select(e => Format(e.Position)));
			}

			if (_resultPreview.HasTexture)
			{
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Render + picks", EditorStyles.miniBoldLabel);
				var rect = GUILayoutUtility.GetRect(256, 256, GUILayout.ExpandWidth(true));
				GUI.DrawTexture(rect, _resultPreview.Texture!, ScaleMode.ScaleToFit);
				DrawPickMarkers(rect, result);
			}

			if (result.RenderPng is { Length: > 0 } renderPng && GUILayout.Button("Save render PNG..."))
			{
				string defaultName = string.IsNullOrEmpty(_voxPath)
					? "render.png"
					: Path.GetFileNameWithoutExtension(_voxPath) + "_render.png";
				string savePath = EditorUtility.SaveFilePanel("Save eye-placement render", string.Empty, defaultName, "png");
				if (!string.IsNullOrEmpty(savePath))
				{
					File.WriteAllBytes(savePath, renderPng);
					_run.SetStatus("Render saved to " + savePath);
				}
			}

			DrawCandidateGrid();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Raw response", EditorStyles.miniBoldLabel);
			EditorGUILayout.SelectableLabel(result.RawResponse, wrapLabel, GUILayout.Height(40));
		}

		// The full set of candidate views considered during auto-orient, each with the resolved eyes
		// drawn on it and coloured by whether that view can actually see them (green) or they're
		// masked/occluded (red). The chosen view is boxed in yellow.
		private void DrawCandidateGrid()
		{
			if (_candidateViews.Count == 0)
			{
				return;
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField($"Candidate views ({_candidateViews.Count})", EditorStyles.boldLabel);
			EditorGUILayout.LabelField(
				"Eyes drawn on each — green = visible, red = masked (occluded). ★ / yellow box = chosen.",
				EditorStyles.miniLabel);

			const float cell = 120f;
			const float pad = 4f;
			int columns = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 20f) / (cell + pad)));

			for (int i = 0; i < _candidateViews.Count;)
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					for (int c = 0; c < columns && i < _candidateViews.Count; c++, i++)
					{
						DrawCandidateCell(_candidateViews[i], cell);
					}
				}
			}
		}

		private static void DrawCandidateCell(CandidateView view, float size)
		{
			var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
			if (view.Texture != null)
			{
				GUI.DrawTexture(rect, view.Texture, ScaleMode.ScaleToFit);
			}
			else
			{
				EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
			}

			// Candidate renders are square and drawn into a square cell, so no letterbox to correct.
			const float r = 3.5f;
			foreach (var (uv, masked) in view.Eyes)
			{
				float px = rect.x + uv.x * rect.width;
				float py = rect.y + uv.y * rect.height;
				EditorGUI.DrawRect(new Rect(px - r, py - r, r * 2f, r * 2f), masked ? MaskedColour : VisibleColour);
			}

			EditorGUI.DropShadowLabel(
				new Rect(rect.x, rect.y + 2f, rect.width, 16f), view.Chosen ? $"★ {view.Yaw:0}°" : $"{view.Yaw:0}°");
			if (view.Chosen)
			{
				DrawOutline(rect, Color.yellow, 2f);
			}
		}

		private static void DrawOutline(Rect rect, Color colour, float t)
		{
			EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, t), colour);
			EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - t, rect.width, t), colour);
			EditorGUI.DrawRect(new Rect(rect.x, rect.y, t, rect.height), colour);
			EditorGUI.DrawRect(new Rect(rect.xMax - t, rect.y, t, rect.height), colour);
		}

		private void RebuildCandidateViews(EyePlacementResult result)
		{
			ClearCandidateViews();
			if (_model3d is not { } model)
			{
				return;
			}

			foreach (var candidate in result.Candidates)
			{
				var display = new CandidateView { Yaw = candidate.Yaw, Chosen = candidate.Chosen };
				if (candidate.Png is { Length: > 0 } png)
				{
					var texture = new Texture2D(2, 2);
					if (texture.LoadImage(png))
					{
						display.Texture = texture;
					}
					else
					{
						UnityEngine.Object.DestroyImmediate(texture);
					}
				}

				var projection = new VoxelViewProjection(candidate.View, model);
				foreach (var eye in result.Eyes)
				{
					display.Eyes.Add((projection.WorldToNormalized(eye.Position), EyeVisibility.IsMasked(model, eye, projection)));
				}

				_candidateViews.Add(display);
			}
		}

		private void ClearCandidateViews()
		{
			foreach (var view in _candidateViews)
			{
				if (view.Texture != null)
				{
					UnityEngine.Object.DestroyImmediate(view.Texture);
				}
			}

			_candidateViews.Clear();
		}

		// Overlay each resolved anchor on the render by reprojecting it through the same view,
		// so it's obvious where the eyes landed. The texture is drawn ScaleToFit, so mirror
		// that letterboxing here.
		private void DrawPickMarkers(Rect rect, EyePlacementResult result)
		{
			if (_resultProjection is not { } projection || !_resultPreview.HasTexture)
			{
				return;
			}

			var preview = _resultPreview.Texture!;
			float texAspect = (float)preview.width / preview.height;
			float rectAspect = rect.width / rect.height;
			Rect fitted;
			if (texAspect > rectAspect)
			{
				float h = rect.width / texAspect;
				fitted = new Rect(rect.x, rect.y + (rect.height - h) * 0.5f, rect.width, h);
			}
			else
			{
				float w = rect.height * texAspect;
				fitted = new Rect(rect.x + (rect.width - w) * 0.5f, rect.y, w, rect.height);
			}

			const float r = 4f;
			foreach (var eye in result.Eyes)
			{
				Vector2 n01 = projection.WorldToNormalized(eye.Position);
				float px = fitted.x + n01.x * fitted.width;
				float py = fitted.y + n01.y * fitted.height;
				bool masked = _model3d is { } m && EyeVisibility.IsMasked(m, eye, projection);
				EditorGUI.DrawRect(new Rect(px - r, py - r, r * 2f, r * 2f), masked ? MaskedColour : VisibleColour);
			}
		}

		private void StartPlaceAI()
		{
			if (string.IsNullOrWhiteSpace(_apiKey))
			{
				_run.SetStatus("ERROR: API key is required.");
				return;
			}
			if (_model3d is not { } model)
			{
				_run.SetStatus("ERROR: load a .vox file first.");
				return;
			}

			_result = null;
			_run.SetStatus("Contacting Claude...");
			_ = _run.RunAsync(async ct =>
			{
				var options = BuildOptions();

				EyePlacementResult result;
				if (!string.IsNullOrEmpty(_imagePath) && File.Exists(_imagePath))
				{
					var bytes = File.ReadAllBytes(_imagePath);
					var mediaType = AnthropicImage.MediaTypeFromExtension(Path.GetExtension(_imagePath));
					result = await EyePlacer.PlaceFromImageAsync(_apiKey, model, bytes, mediaType, options, ct);
				}
				else
				{
					result = await EyePlacer.PlaceAsync(_apiKey, model, options, ct);
				}

				_result = result;
				// Reproject markers with the view actually used (auto-orient may have changed it).
				_resultProjection = new VoxelViewProjection(result.View, model);
				LoadResultPreview(result);
				RebuildCandidateViews(result);
				var front = result.FrontCode is { } code ? $" — oriented to {code}" : string.Empty;
				_run.SetStatus($"Done — {result.Eyes.Count} eye(s) placed{front}.");
			});
		}

		private void RunGeometric()
		{
			if (_model3d is null)
			{
				_run.SetStatus("ERROR: load a .vox file first.");
				return;
			}

			try
			{
				var options = BuildOptions();
				_result = EyePlacer.PlaceGeometric(_model3d, options);
				_resultProjection = new VoxelViewProjection(_result.View, _model3d);
				// The geometric path doesn't render, so give the preview a render for context.
				byte[] png = VoxelRender.ToPng(_model3d, _resultProjection, options.ImageSize, options.Msaa);
				LoadResultPreview(_result with { RenderPng = png });
				RebuildCandidateViews(_result);
				_run.SetStatus($"Done (geometric) — {_result.Eyes.Count} eye(s) placed.");
			}
			catch (Exception ex)
			{
				_run.SetStatus("Error: " + ex.Message);
			}
		}

		private EyePlacementOptions BuildOptions() => new()
		{
			AutoOrient = _autoOrient,
			View = ViewFor(_view),
			EyeCount = _eyeCount,
			ImageSize = _imageSize,
			SurfaceOffset = _surfaceOffset,
			Model = _model,
		};

		private static OrthographicView ViewFor(ViewPreset preset) => preset switch
		{
			ViewPreset.Isometric => OrthographicView.Isometric,
			ViewPreset.IsometricLeft => OrthographicView.IsometricLeft,
			ViewPreset.Front => OrthographicView.Front,
			ViewPreset.Top => OrthographicView.Top,
			_ => OrthographicView.Isometric,
		};

		private void LoadModel()
		{
			_model3d = null;
			if (string.IsNullOrEmpty(_voxPath) || !File.Exists(_voxPath))
			{
				return;
			}

			try
			{
				_model3d = VoxReader.Read(File.ReadAllBytes(_voxPath));
				_run.SetStatus(string.Empty);
			}
			catch (Exception ex)
			{
				_run.SetStatus("Failed to read .vox: " + ex.Message);
			}
		}

		private void LoadResultPreview(EyePlacementResult result)
		{
			if (result.RenderPng is { Length: > 0 } png)
			{
				_resultPreview.Load(png);
			}
			else
			{
				_resultPreview.Clear();
			}
		}

		private static string Format(Vector3 v) =>
			$"({v.x:0.##}, {v.y:0.##}, {v.z:0.##})";
	}
}
