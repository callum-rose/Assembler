#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Assembler.MeshyImageTo3D
{
    /// <summary>
    /// Spike editor window: take a reference image, send it to Meshy.ai's
    /// image-to-3D endpoint, and download the resulting textured model (OBJ or
    /// FBX) to a chosen output path.
    /// </summary>
    public sealed class MeshyImageTo3DWindow : EditorWindow
    {
        private const string ApiKeyPref = "Meshy.ImageTo3D.ApiKey";
        private const string ImagePathPref = "Meshy.ImageTo3D.ImagePath";
        private const string OutputDirPref = "Meshy.ImageTo3D.OutputDir";
        private const string OutputFilePref = "Meshy.ImageTo3D.OutputFile";
        private const string AiModelPref = "Meshy.ImageTo3D.AiModel";
        private const string TexturePref = "Meshy.ImageTo3D.Texture";

        // The Meshy image-to-3D AI models, newest first.
        private static readonly string[] AiModels = { "meshy-6", "meshy-5", "meshy-4" };
        private const string DefaultAiModel = "meshy-6";

        private string _apiKey = "";
        private string _imagePath = "";
        private string _outputDir = "";
        private string _outputFile = "";
        private ModelFormat _format = ModelFormat.Obj;
        private string _aiModel = DefaultAiModel;
        private bool _generateTexture = true;
        private bool _enablePbr = true;
        private bool _remesh = true;

        private bool _running;
        private string _status = "Idle.";
        private CancellationTokenSource? _cts;

        [MenuItem("Assembler/Image to Mesh")]
        public static void Open()
        {
            var window = GetWindow<MeshyImageTo3DWindow>("Image to Mesh");
            window.minSize = new Vector2(460, 320);
        }

        private void OnEnable()
        {
            _apiKey = EditorPrefs.GetString(ApiKeyPref, "");
            _imagePath = EditorPrefs.GetString(ImagePathPref, "");
            _outputDir = EditorPrefs.GetString(OutputDirPref, "Assets/MeshyOutput");
            _outputFile = EditorPrefs.GetString(OutputFilePref, "");
            _aiModel = EditorPrefs.GetString(AiModelPref, DefaultAiModel);
            _generateTexture = EditorPrefs.GetBool(TexturePref, true);
        }

        private void OnGUI()
        {
            using (new EditorGUI.DisabledScope(_running))
            {
                DrawApiKey();
                EditorGUILayout.Space();
                DrawImagePicker();
                DrawOutputPicker();
                EditorGUILayout.Space();

                DrawModel();
                _format = (ModelFormat)EditorGUILayout.EnumPopup("Output Format", _format);
                _generateTexture = EditorGUILayout.Toggle(
                    new GUIContent("Generate Texture", "Generate a texture for the model."), _generateTexture);
                using (new EditorGUI.DisabledScope(!_generateTexture))
                {
                    _enablePbr = EditorGUILayout.Toggle(
                        new GUIContent("Enable PBR Maps", "Also generate metallic/roughness/normal maps."), _enablePbr);
                }
                _remesh = EditorGUILayout.Toggle(
                    new GUIContent("Remesh", "Let Meshy clean up the topology."), _remesh);
            }

            EditorGUILayout.Space();
            DrawActions();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(_status, _running ? MessageType.Info : MessageType.None);
        }

        private void DrawApiKey()
        {
            EditorGUILayout.BeginHorizontal();
            _apiKey = EditorGUILayout.PasswordField("API Key", _apiKey);
            if (GUILayout.Button("Save", GUILayout.Width(60)))
            {
                EditorPrefs.SetString(ApiKeyPref, _apiKey);
                _status = "API key saved to EditorPrefs.";
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawImagePicker()
        {
            EditorGUILayout.BeginHorizontal();
            _imagePath = EditorGUILayout.TextField("Reference Image", _imagePath);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                var picked = EditorUtility.OpenFilePanel(
                    "Select reference image", GuessStartDir(_imagePath), "png,jpg,jpeg,webp");
                if (!string.IsNullOrEmpty(picked))
                    _imagePath = picked;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawModel()
        {
            // Keep any saved id selectable even if it's not in the known list.
            var options = Array.IndexOf(AiModels, _aiModel) >= 0 || string.IsNullOrEmpty(_aiModel)
                ? AiModels
                : new[] { _aiModel }.Concat(AiModels).ToArray();

            var index = Mathf.Max(0, Array.IndexOf(options, _aiModel));
            index = EditorGUILayout.Popup(
                new GUIContent("AI Model", "Meshy generation model."), index, options.Select(m => new GUIContent(m)).ToArray());
            _aiModel = options[index];
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
                new GUIContent("File Name", "Leave blank to use the downloaded model's filename. The extension is set from the output format."),
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

        private async Task RunAsync()
        {
            // Persist inputs so the next session keeps them.
            EditorPrefs.SetString(ApiKeyPref, _apiKey);
            EditorPrefs.SetString(ImagePathPref, _imagePath);
            EditorPrefs.SetString(OutputDirPref, _outputDir);
            EditorPrefs.SetString(OutputFilePref, _outputFile);
            EditorPrefs.SetString(AiModelPref, _aiModel);
            EditorPrefs.SetBool(TexturePref, _generateTexture);

            _running = true;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                // Core submit/poll/download lives in MeshyConversionCore so it can be driven
                // headlessly or as one stage of the image → mesh → voxels pipeline.
                await MeshyConversionCore.ConvertAsync(
                    _apiKey, _imagePath, _outputDir, _outputFile, _format,
                    _generateTexture, _enablePbr, _remesh, _aiModel, ct, SetStatus);
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
