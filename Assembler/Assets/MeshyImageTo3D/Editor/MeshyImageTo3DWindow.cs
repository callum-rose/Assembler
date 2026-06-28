#nullable enable

using System;
using System.IO;
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
        private const string OutputPathPref = "Meshy.ImageTo3D.OutputPath";

        private string _apiKey = "";
        private string _imagePath = "";
        private string _outputPath = "";
        private ModelFormat _format = ModelFormat.Obj;
        private bool _enablePbr = true;
        private bool _remesh = true;

        private bool _running;
        private string _status = "Idle.";
        private CancellationTokenSource? _cts;

        [MenuItem("Assembler/Meshy Image → 3D")]
        public static void Open()
        {
            var window = GetWindow<MeshyImageTo3DWindow>("Meshy Image → 3D");
            window.minSize = new Vector2(460, 320);
        }

        private void OnEnable()
        {
            _apiKey = EditorPrefs.GetString(ApiKeyPref, "");
            _imagePath = EditorPrefs.GetString(ImagePathPref, "");
            _outputPath = EditorPrefs.GetString(OutputPathPref, "Assets/MeshyOutput/model.obj");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Meshy.ai · Image to 3D", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_running))
            {
                DrawApiKey();
                EditorGUILayout.Space();
                DrawImagePicker();
                DrawOutputPicker();
                EditorGUILayout.Space();

                _format = (ModelFormat)EditorGUILayout.EnumPopup("Output Format", _format);
                _enablePbr = EditorGUILayout.Toggle(
                    new GUIContent("Enable PBR Maps", "Also generate metallic/roughness/normal maps."), _enablePbr);
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

        private void DrawOutputPicker()
        {
            EditorGUILayout.BeginHorizontal();
            _outputPath = EditorGUILayout.TextField("Output Model Path", _outputPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                var ext = _format == ModelFormat.Obj ? "obj" : "fbx";
                var dir = GuessStartDir(_outputPath);
                var name = string.IsNullOrEmpty(_outputPath)
                    ? "model"
                    : Path.GetFileNameWithoutExtension(_outputPath);
                var picked = EditorUtility.SaveFilePanel("Output model", dir, name, ext);
                if (!string.IsNullOrEmpty(picked))
                    _outputPath = picked;
            }
            EditorGUILayout.EndHorizontal();
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
            EditorPrefs.SetString(OutputPathPref, _outputPath);

            _running = true;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                if (string.IsNullOrWhiteSpace(_outputPath))
                    throw new MeshyException("Set an output model path.");

                using var client = new MeshyApiClient(_apiKey);
                var request = new MeshyRequest
                {
                    ImagePath = _imagePath,
                    Format = _format,
                    GenerateTexture = true,
                    EnablePbr = _enablePbr,
                    Remesh = _remesh,
                    AiModel = "meshy-5",
                };

                SetStatus("Submitting image…");
                var taskId = await client.CreateTaskAsync(request, ct);

                SetStatus($"Queued (task {taskId}). Generating…");
                var task = await client.PollUntilCompleteAsync(
                    taskId, (p, s) => SetStatus($"{s} — {p}%"), ct);

                await DownloadResultsAsync(client, task, ct);

                SetStatus($"Done. Saved to {_outputPath}");
                if (IsUnderAssets(_outputPath))
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

        private async Task DownloadResultsAsync(
            MeshyApiClient client, MeshyApiClient.TaskResponse task, CancellationToken ct)
        {
            var urls = task.model_urls
                ?? throw new MeshyException("Task succeeded but returned no model URLs.");

            var dir = Path.GetDirectoryName(Path.GetFullPath(_outputPath))!;
            var baseName = Path.GetFileNameWithoutExtension(_outputPath);

            var modelUrl = _format == ModelFormat.Obj ? urls.obj : urls.fbx;
            if (string.IsNullOrEmpty(modelUrl))
                throw new MeshyException($"No {_format} URL in the result.");

            SetStatus($"Downloading {_format} model…");
            await client.DownloadAsync(modelUrl!, _outputPath, ct);

            // OBJ ships its material library separately; keep it beside the model.
            if (_format == ModelFormat.Obj && !string.IsNullOrEmpty(urls.mtl))
                await client.DownloadAsync(urls.mtl!, Path.Combine(dir, $"{baseName}.mtl"), ct);

            // Texture maps are delivered as separate files; download them alongside.
            var tex = task.texture_urls is { Length: > 0 } ? task.texture_urls[0] : null;
            if (tex != null)
            {
                SetStatus("Downloading texture maps…");
                await SaveMapAsync(client, tex.base_color, dir, $"{baseName}_basecolor", ct);
                if (_enablePbr)
                {
                    await SaveMapAsync(client, tex.metallic, dir, $"{baseName}_metallic", ct);
                    await SaveMapAsync(client, tex.roughness, dir, $"{baseName}_roughness", ct);
                    await SaveMapAsync(client, tex.normal, dir, $"{baseName}_normal", ct);
                }
            }
        }

        private static async Task SaveMapAsync(
            MeshyApiClient client, string? url, string dir, string baseName, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(url))
                return;

            // Meshy serves PNGs; strip any query string before reading the extension.
            var clean = url!.Split('?')[0];
            var ext = Path.GetExtension(clean);
            if (string.IsNullOrEmpty(ext))
                ext = ".png";

            await client.DownloadAsync(url, Path.Combine(dir, baseName + ext), ct);
        }

        private void SetStatus(string message)
        {
            _status = message;
            Repaint();
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
