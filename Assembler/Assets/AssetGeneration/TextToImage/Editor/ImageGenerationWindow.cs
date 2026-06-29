#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        // API keys are stored per provider so swapping providers keeps each key.
        private static string ApiKeyPref(ImageProvider provider) => $"Assembler.ImageGen.ApiKey.{provider}";

        private ImageProvider _provider = ImageProvider.GoogleGemini;
        private string _apiKey = "";
        private string _model = "";
        private string _prompt = "";
        private string _outputDir = "";
        private string _outputFile = "";
        private string _referenceImage = "";

        private bool _running;
        private string _status = "Idle.";
        private CancellationTokenSource? _cts;
        private Texture2D? _preview;
        private Texture2D? _referencePreview;
        private string _referencePreviewPath = "";
        private Vector2 _windowScroll;

        [MenuItem("Assembler/Text to Image")]
        public static void Open()
        {
            var window = GetWindow<ImageGenerationWindow>("Text to Image");
            window.minSize = new Vector2(460, 520);
        }

        private void OnEnable()
        {
            _provider = (ImageProvider)EditorPrefs.GetInt(ProviderPref, (int)ImageProvider.GoogleGemini);
            _model = EditorPrefs.GetString(ModelPref, ImageGeneratorFactory.DefaultModelFor(_provider));
            _prompt = EditorPrefs.GetString(PromptPref, "");
            _outputDir = EditorPrefs.GetString(OutputDirPref, "Assets/GeneratedImages");
            _outputFile = EditorPrefs.GetString(OutputFilePref, "");
            _referenceImage = EditorPrefs.GetString(ReferenceImagePref, "");
            _apiKey = EditorPrefs.GetString(ApiKeyPref(_provider), "");
        }

        private void OnDisable()
        {
            if (_preview != null)
                DestroyImmediate(_preview);
            if (_referencePreview != null)
                DestroyImmediate(_referencePreview);
        }

        private void OnGUI()
        {
            _windowScroll = EditorGUILayout.BeginScrollView(_windowScroll);

            using (new EditorGUI.DisabledScope(_running))
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
            EditorGUILayout.HelpBox(_status, _running ? MessageType.Info : MessageType.None);
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
                _apiKey = EditorPrefs.GetString(ApiKeyPref(_provider), "");
                _model = EditorPrefs.GetString(ModelPref + "." + _provider, ImageGeneratorFactory.DefaultModelFor(_provider));
            }
        }

        private void DrawModel()
        {
            // Offer the provider's known models as a dropdown, but keep any
            // previously-saved custom id selectable by prepending it when missing.
            var models = ImageGeneratorFactory.AvailableModelsFor(_provider);
            var options = Array.IndexOf(models, _model) >= 0 || string.IsNullOrEmpty(_model)
                ? models
                : new[] { _model }.Concat(models).ToArray();

            var index = Mathf.Max(0, Array.IndexOf(options, _model));
            index = EditorGUILayout.Popup(
                new GUIContent("Model", "Provider model id."), index, options.Select(m => new GUIContent(m)).ToArray());
            if (options.Length > 0)
                _model = options[index];
        }

        private void DrawApiKey()
        {
            EditorGUILayout.BeginHorizontal();
            _apiKey = EditorGUILayout.PasswordField("API Key", _apiKey);
            if (GUILayout.Button("Save", GUILayout.Width(60)))
            {
                EditorPrefs.SetString(ApiKeyPref(_provider), _apiKey);
                _status = "API key saved to EditorPrefs.";
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPrompt()
        {
            EditorGUILayout.LabelField("Prompt");
            var wrapStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            _prompt = EditorGUILayout.TextArea(_prompt, wrapStyle, GUILayout.MinHeight(90));
        }

        private void DrawReferenceImage()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            _referenceImage = EditorGUILayout.TextField(
                new GUIContent("Reference Image", "Optional image to condition generation on (style reference / edit). Leave blank for pure text-to-image."),
                _referenceImage);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                var picked = EditorUtility.OpenFilePanel(
                    "Reference image", GuessStartDir(_referenceImage), "png,jpg,jpeg,webp");
                if (!string.IsNullOrEmpty(picked))
                    _referenceImage = picked;
            }
            if (!string.IsNullOrEmpty(_referenceImage) && GUILayout.Button("Clear", GUILayout.Width(50)))
                _referenceImage = "";
            EditorGUILayout.EndHorizontal();

            DrawReferencePreview();
        }

        private void DrawReferencePreview()
        {
            if (string.IsNullOrEmpty(_referenceImage))
                return;

            // (Re)load the thumbnail only when the path changes, not every repaint.
            if (_referencePreviewPath != _referenceImage)
            {
                if (_referencePreview != null)
                    DestroyImmediate(_referencePreview);
                _referencePreview = null;
                _referencePreviewPath = _referenceImage;

                if (File.Exists(_referenceImage))
                {
                    var tex = new Texture2D(2, 2);
                    if (tex.LoadImage(File.ReadAllBytes(_referenceImage)))
                        _referencePreview = tex;
                    else
                        DestroyImmediate(tex);
                }
            }

            if (_referencePreview == null)
            {
                EditorGUILayout.HelpBox("Reference image not found or unreadable.", MessageType.Warning);
                return;
            }

            var width = Mathf.Min(140, _referencePreview.width);
            var height = width * _referencePreview.height / Mathf.Max(1, _referencePreview.width);
            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(rect, _referencePreview, ScaleMode.ScaleToFit);
        }

        private void DrawOutputPicker()
        {
            EditorGUILayout.BeginHorizontal();
            _outputDir = EditorGUILayout.TextField("Output Directory", _outputDir);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                var picked = EditorUtility.OpenFolderPanel("Output directory", GuessStartDir(_outputDir), "");
                if (!string.IsNullOrEmpty(picked))
                    _outputDir = picked;
            }
            EditorGUILayout.EndHorizontal();

            _outputFile = EditorGUILayout.TextField(
                new GUIContent("File Name", "Leave blank to use a default name. The extension is set from the returned image type."),
                _outputFile);
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_running))
            {
                if (GUILayout.Button("Generate", GUILayout.Height(30)))
                    _ = RunAsync();
            }
            using (new EditorGUI.DisabledScope(!_running))
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(30), GUILayout.Width(100)))
                    _cts?.Cancel();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPreview()
        {
            if (_preview == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            var width = Mathf.Min(position.width - 30, _preview.width);
            var height = width * _preview.height / Mathf.Max(1, _preview.width);
            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(rect, _preview, ScaleMode.ScaleToFit);
        }

        private async Task RunAsync()
        {
            // Persist inputs so the next session keeps them.
            EditorPrefs.SetInt(ProviderPref, (int)_provider);
            EditorPrefs.SetString(ModelPref, _model);
            EditorPrefs.SetString(ModelPref + "." + _provider, _model);
            EditorPrefs.SetString(PromptPref, _prompt);
            EditorPrefs.SetString(OutputDirPref, _outputDir);
            EditorPrefs.SetString(OutputFilePref, _outputFile);
            EditorPrefs.SetString(ReferenceImagePref, _referenceImage);
            EditorPrefs.SetString(ApiKeyPref(_provider), _apiKey);

            _running = true;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                // Core generation/saving lives in ImageGenerationCore so it can be driven
                // headlessly or as one stage of the image → mesh → voxels pipeline.
                var result = await ImageGenerationCore.GenerateAsync(
                    _provider, _apiKey, _model, _prompt, _outputDir, _outputFile, ct, SetStatus,
                    string.IsNullOrWhiteSpace(_referenceImage) ? null : _referenceImage);

                LoadPreview(result.Image.Bytes);
            }
            catch (OperationCanceledException)
            {
                SetStatus("Cancelled.");
            }
            catch (Exception e)
            {
                SetStatus($"Error: {e.Message}");
                Debug.LogException(e);
            }
            finally
            {
                _running = false;
                _cts?.Dispose();
                _cts = null;
                Repaint();
            }
        }

        private void LoadPreview(byte[] bytes)
        {
            if (_preview != null)
                DestroyImmediate(_preview);

            _preview = new Texture2D(2, 2);
            _preview.LoadImage(bytes);
        }

        private void SetStatus(string message)
        {
            _status = message;
            Repaint();
        }

        private static string GuessStartDir(string path)
        {
            if (string.IsNullOrEmpty(path))
                return Application.dataPath;
            var dir = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(dir) ? Application.dataPath : dir;
        }
    }
}
