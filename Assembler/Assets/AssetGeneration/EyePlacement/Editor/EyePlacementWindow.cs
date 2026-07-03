using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;
using Assembler.Voxels;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.EyePlacement
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

        private ViewPreset _view = ViewPreset.Isometric;
        private int _eyeCount = 2;
        private int _imageSize = 512;
        private float _surfaceOffset = 0.5f;

        private VoxelModel? _model3d;
        private Texture2D? _resultPreview;
        private EyePlacementResult? _result;
        private VoxelViewProjection? _resultProjection;

        private string _status = string.Empty;
        private Vector2 _scroll;
        private bool _isRunning;
        private CancellationTokenSource? _cts;

        [MenuItem("Assembler/Eye Placement")]
        public static void Open()
        {
            var window = GetWindow<EyePlacementWindow>("Eye Placement");
            window.minSize = new Vector2(440, 620);
            window.Show();
        }

        private void OnEnable()
        {
            _apiKey = EditorPrefs.GetString(ApiKeyPref, string.Empty);
            _model = EditorPrefs.GetString(ModelPref, ImageEyePlacer.DefaultModel);
            _voxPath = EditorPrefs.GetString(VoxPathPref, string.Empty);
            LoadModel();
        }

        private void OnDisable() => _cts?.Cancel();

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
            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                _model = EditorGUILayout.TextField("Vision model", _model);
                if (scope.changed)
                {
                    EditorPrefs.SetString(ModelPref, _model);
                }
            }

            EditorGUILayout.Space();
            DrawVoxSelector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            _view = (ViewPreset)EditorGUILayout.EnumPopup("View", _view);
            _eyeCount = Mathf.Max(1, EditorGUILayout.IntField("Eye count", _eyeCount));
            _imageSize = Mathf.Clamp(EditorGUILayout.IntField("Render size (px)", _imageSize), 64, 2048);
            _surfaceOffset = EditorGUILayout.FloatField("Surface offset (voxels)", _surfaceOffset);

            EditorGUILayout.Space();
            DrawImageOverride();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_isRunning || _model3d == null))
            {
                if (GUILayout.Button(_isRunning ? "Asking Claude..." : "Place eyes (Claude vision)"))
                {
                    StartPlaceAI();
                }
                if (GUILayout.Button("Place eyes (geometric, offline)"))
                {
                    RunGeometric();
                }
            }
            using (new EditorGUI.DisabledScope(!_isRunning))
            {
                if (GUILayout.Button("Cancel"))
                {
                    _cts?.Cancel();
                }
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }

            DrawResult(wrapLabel);

            EditorGUILayout.EndScrollView();
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

            if (_resultPreview != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Render + picks", EditorStyles.miniBoldLabel);
                var rect = GUILayoutUtility.GetRect(256, 256, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, _resultPreview, ScaleMode.ScaleToFit);
                DrawPickMarkers(rect, result);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Raw response", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(result.RawResponse, wrapLabel, GUILayout.Height(40));
        }

        // Overlay each resolved anchor on the render by reprojecting it through the same view,
        // so it's obvious where the eyes landed. The texture is drawn ScaleToFit, so mirror
        // that letterboxing here.
        private void DrawPickMarkers(Rect rect, EyePlacementResult result)
        {
            if (_resultProjection is not { } projection || _resultPreview == null)
            {
                return;
            }

            float texAspect = (float)_resultPreview.width / _resultPreview.height;
            float rectAspect = rect.width / rect.height;
            Rect fitted = rect;
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
                EditorGUI.DrawRect(new Rect(px - r, py - r, r * 2f, r * 2f), Color.red);
            }
        }

        private void StartPlaceAI()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _status = "ERROR: API key is required.";
                return;
            }
            if (_model3d is null)
            {
                _status = "ERROR: load a .vox file first.";
                return;
            }

            _result = null;
            _status = "Contacting Claude...";
            _isRunning = true;
            _cts = new CancellationTokenSource();
            PlaceAsync(_model3d, _cts.Token);
        }

        private async void PlaceAsync(VoxelModel model, CancellationToken ct)
        {
            try
            {
                var options = BuildOptions();
                _resultProjection = new VoxelViewProjection(options.View, model);

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
                LoadResultPreview(result);
                _status = $"Done — {result.Eyes.Count} eye(s) placed.";
            }
            catch (OperationCanceledException)
            {
                _status = "Cancelled.";
            }
            catch (Exception ex)
            {
                _status = "Error: " + ex.Message;
            }
            finally
            {
                _isRunning = false;
                _cts?.Dispose();
                _cts = null;
                Repaint();
            }
        }

        private void RunGeometric()
        {
            if (_model3d is null)
            {
                _status = "ERROR: load a .vox file first.";
                return;
            }

            try
            {
                var options = BuildOptions();
                _resultProjection = new VoxelViewProjection(options.View, _model3d);
                _result = EyePlacer.PlaceGeometric(_model3d, options);
                // The geometric path doesn't render, so give the preview a render for context.
                byte[] png = VoxelIsometricRenderer.RenderPng(_model3d, _resultProjection, options.ImageSize);
                LoadResultPreview(_result with { RenderPng = png });
                _status = $"Done (geometric) — {_result.Eyes.Count} eye(s) placed.";
            }
            catch (Exception ex)
            {
                _status = "Error: " + ex.Message;
            }
            finally
            {
                Repaint();
            }
        }

        private EyePlacementOptions BuildOptions() => new()
        {
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
                _status = string.Empty;
            }
            catch (Exception ex)
            {
                _status = "Failed to read .vox: " + ex.Message;
            }
        }

        private void LoadResultPreview(EyePlacementResult result)
        {
            _resultPreview = null;
            if (result.RenderPng is { Length: > 0 } png)
            {
                var texture = new Texture2D(2, 2);
                if (texture.LoadImage(png))
                {
                    _resultPreview = texture;
                }
            }
        }

        private static string Format(Vector3 v) =>
            $"({v.x:0.##}, {v.y:0.##}, {v.z:0.##})";
    }
}
