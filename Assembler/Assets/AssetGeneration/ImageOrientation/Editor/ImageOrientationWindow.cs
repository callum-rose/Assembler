using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.ImageOrientation
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

        private readonly List<string> _models = new();
        private Texture2D? _preview;

        private string _status = string.Empty;
        private OrientationResult? _result;
        private Vector2 _scroll;
        private bool _isRunning;
        private CancellationTokenSource? _cts;

        [MenuItem("Assembler/Image Facing Direction")]
        public static void Open()
        {
            var window = GetWindow<ImageOrientationWindow>("Image Facing Direction");
            window.minSize = new Vector2(420, 520);
            window.Show();
        }

        private void OnEnable()
        {
            _apiKey = EditorPrefs.GetString(ApiKeyPref, string.Empty);
            _model = EditorPrefs.GetString(ModelPref, DefaultModel);
            _imagePath = EditorPrefs.GetString(ImagePathPref, string.Empty);
            LoadPreview();
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

            EditorGUILayout.Space();
            DrawModelSelector();

            EditorGUILayout.Space();
            DrawImageSelector();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_isRunning))
            {
                if (GUILayout.Button(_isRunning ? "Asking Claude..." : "Determine facing direction"))
                {
                    StartClassify();
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

            if (_result is { } result)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);

                var bigCode = new GUIStyle(EditorStyles.boldLabel) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
                EditorGUILayout.LabelField(result.Code, bigCode, GUILayout.Height(40));

                switch (result.Outcome)
                {
                    case OrientationOutcome.Resolved when result.Direction is { } direction:
                        var caption = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
                        EditorGUILayout.LabelField($"The front is {direction.Describe()}.", caption);
                        break;
                    case OrientationOutcome.Unsure:
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

        private void DrawModelSelector()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Model", EditorStyles.boldLabel);
                if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                {
                    RefreshModels();
                }
            }

            // Popup over whatever models we know about; always include the current
            // selection so a stored/typed id stays selectable before any Refresh.
            var options = _models.Count > 0 ? new List<string>(_models) : new List<string>();
            if (!options.Contains(_model))
            {
                options.Insert(0, _model);
            }

            var index = options.IndexOf(_model);
            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                var picked = EditorGUILayout.Popup(index, options.ToArray());
                if (scope.changed && picked >= 0 && picked < options.Count)
                {
                    _model = options[picked];
                    EditorPrefs.SetString(ModelPref, _model);
                }
            }
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
                        "Select image", string.Empty, "png,jpg,jpeg,gif,webp");
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
            var assetPath = ToProjectRelativePath(_imagePath);
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

        private static string? ToProjectRelativePath(string absoluteOrRelative)
        {
            var full = Path.GetFullPath(absoluteOrRelative).Replace('\\', '/');
            var projectRoot = Path.GetFullPath(Application.dataPath + "/..").Replace('\\', '/') + "/";
            if (!full.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var relative = full.Substring(projectRoot.Length);
            return relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ? relative : null;
        }

        private async void RefreshModels()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _status = "ERROR: enter an API key before refreshing models.";
                Repaint();
                return;
            }

            try
            {
                _status = "Fetching available models...";
                Repaint();
                var ids = await AnthropicClient.ListModelsAsync(_apiKey);
                _models.Clear();
                _models.AddRange(ids);
                _status = _models.Count > 0 ? $"Loaded {_models.Count} models." : "No models returned.";
            }
            catch (Exception ex)
            {
                _status = "Error fetching models: " + ex.Message;
            }
            finally
            {
                Repaint();
            }
        }

        private void StartClassify()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _status = "ERROR: API key is required.";
                return;
            }
            if (string.IsNullOrEmpty(_imagePath) || !File.Exists(_imagePath))
            {
                _status = "ERROR: select an image first.";
                return;
            }

            _result = null;
            _status = "Contacting Claude...";
            _isRunning = true;
            _cts = new CancellationTokenSource();
            ClassifyAsync(_cts.Token);
        }

        private async void ClassifyAsync(CancellationToken ct)
        {
            try
            {
                var bytes = File.ReadAllBytes(_imagePath);
                var mediaType = AnthropicImage.MediaTypeFromExtension(Path.GetExtension(_imagePath));
                _result = await ImageFacingDirection.DetermineAsync(_apiKey, bytes, mediaType, _model, ct);
                _status = _result.Outcome switch
                {
                    OrientationOutcome.Resolved when _result.Direction is { } d => $"Done — {d.ToCode()}.",
                    OrientationOutcome.Unsure => "Done — Claude was unsure.",
                    _ => "Done, but no code recognised.",
                };
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
    }
}
