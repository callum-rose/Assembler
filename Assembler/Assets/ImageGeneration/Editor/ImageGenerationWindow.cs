#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Assembler.ImageGeneration
{
    /// <summary>
    /// Spike editor window: type a prompt, pick a provider, and write the
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

        // API keys are stored per provider so swapping providers keeps each key.
        private static string ApiKeyPref(ImageProvider provider) => $"Assembler.ImageGen.ApiKey.{provider}";

        private ImageProvider _provider = ImageProvider.GoogleGemini;
        private string _apiKey = "";
        private string _model = "";
        private string _prompt = "";
        private string _outputDir = "";
        private string _outputFile = "";

        private bool _running;
        private string _status = "Idle.";
        private CancellationTokenSource? _cts;
        private Texture2D? _preview;
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
            _apiKey = EditorPrefs.GetString(ApiKeyPref(_provider), "");
        }

        private void OnDisable()
        {
            if (_preview != null)
                DestroyImmediate(_preview);
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
            EditorPrefs.SetString(ApiKeyPref(_provider), _apiKey);

            _running = true;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                if (string.IsNullOrWhiteSpace(_prompt))
                    throw new ImageGenerationException("Enter a prompt.");
                if (string.IsNullOrWhiteSpace(_outputDir))
                    throw new ImageGenerationException("Set an output directory.");

                using var generator = ImageGeneratorFactory.Create(_provider, _apiKey);

                SetStatus($"Generating with {generator.DisplayName}…");
                var image = await generator.GenerateAsync(new ImageGenerationRequest(_prompt, _model), ct);

                // No filename given → fall back to a default base name; the extension
                // is always derived from the returned image's MIME type.
                var fileName = string.IsNullOrWhiteSpace(_outputFile) ? "image" : _outputFile.Trim();
                var path = EnsureExtension(Path.Combine(_outputDir, fileName), image.MimeType);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                File.WriteAllBytes(path, image.Bytes);

                LoadPreview(image.Bytes);
                SetStatus($"Done ({image.Bytes.Length / 1024} KB). Saved to {path}");

                if (IsUnderAssets(path))
                    AssetDatabase.Refresh();
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

        private static string EnsureExtension(string path, string mimeType)
        {
            if (!string.IsNullOrEmpty(Path.GetExtension(path)))
                return path;

            var ext = mimeType switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/webp" => ".webp",
                _ => ".png",
            };
            return path + ext;
        }

        private static bool IsUnderAssets(string path)
        {
            var full = Path.GetFullPath(path);
            var assets = Path.GetFullPath(Application.dataPath);
            return full.StartsWith(assets, StringComparison.OrdinalIgnoreCase);
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
