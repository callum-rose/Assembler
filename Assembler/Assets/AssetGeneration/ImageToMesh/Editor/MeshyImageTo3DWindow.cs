#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private const string ApiKeyPref = Pref + "ApiKey";
        private const string ImagePathPref = Pref + "ImagePath";
        private const string OutputDirPref = Pref + "OutputDir";
        private const string OutputFilePref = Pref + "OutputFile";
        private const string AiModelPref = Pref + "AiModel";
        private const string FormatPref = Pref + "Format";
        private const string TexturePref = Pref + "Texture";
        private const string PbrPref = Pref + "Pbr";
        private const string HdTexturePref = Pref + "HdTexture";
        private const string RemeshPref = Pref + "Remesh";
        private const string TopologyPref = Pref + "Topology";
        private const string DecimationPref = Pref + "Decimation";
        private const string PolycountPref = Pref + "Polycount";
        private const string SavePreRemeshPref = Pref + "SavePreRemesh";
        private const string RemoveLightingPref = Pref + "RemoveLighting";
        private const string ModerationPref = Pref + "Moderation";
        private const string AutoSizePref = Pref + "AutoSize";
        private const string OriginAtPref = Pref + "OriginAt";
        private const string MultiViewThumbsPref = Pref + "MultiViewThumbs";
        private const string AlphaThumbPref = Pref + "AlphaThumb";

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
        private bool _hdTexture;
        private bool _remesh = true;
        private MeshyTopology _topology = MeshyTopology.Triangle;
        private DecimationMode _decimation = DecimationMode.None;
        private int _targetPolycount = 30000;
        private bool _savePreRemeshedModel;
        private bool _removeLighting = true;
        private bool _moderation;
        private bool _autoSize;
        private ModelOrigin _originAt = ModelOrigin.Bottom;
        private bool _multiViewThumbnails;
        private bool _alphaThumbnail;

        private bool _running;
        private string _status = "Idle.";
        private CancellationTokenSource? _cts;

        [MenuItem("Assembler/Image to Mesh")]
        public static void Open()
        {
            var window = GetWindow<MeshyImageTo3DWindow>("Image to Mesh");
            window.minSize = new Vector2(460, 600);
        }

        private void OnEnable()
        {
            _apiKey = EditorPrefs.GetString(ApiKeyPref, "");
            _imagePath = EditorPrefs.GetString(ImagePathPref, "");
            _outputDir = EditorPrefs.GetString(OutputDirPref, "Assets/MeshyOutput");
            _outputFile = EditorPrefs.GetString(OutputFilePref, "");
            _aiModel = EditorPrefs.GetString(AiModelPref, DefaultAiModel);
            _format = (ModelFormat)EditorPrefs.GetInt(FormatPref, (int)ModelFormat.Obj);
            _generateTexture = EditorPrefs.GetBool(TexturePref, true);
            _enablePbr = EditorPrefs.GetBool(PbrPref, true);
            _hdTexture = EditorPrefs.GetBool(HdTexturePref, false);
            _remesh = EditorPrefs.GetBool(RemeshPref, true);
            _topology = (MeshyTopology)EditorPrefs.GetInt(TopologyPref, (int)MeshyTopology.Triangle);
            _decimation = (DecimationMode)EditorPrefs.GetInt(DecimationPref, (int)DecimationMode.None);
            _targetPolycount = EditorPrefs.GetInt(PolycountPref, 30000);
            _savePreRemeshedModel = EditorPrefs.GetBool(SavePreRemeshPref, false);
            _removeLighting = EditorPrefs.GetBool(RemoveLightingPref, true);
            _moderation = EditorPrefs.GetBool(ModerationPref, false);
            _autoSize = EditorPrefs.GetBool(AutoSizePref, false);
            _originAt = (ModelOrigin)EditorPrefs.GetInt(OriginAtPref, (int)ModelOrigin.Bottom);
            _multiViewThumbnails = EditorPrefs.GetBool(MultiViewThumbsPref, false);
            _alphaThumbnail = EditorPrefs.GetBool(AlphaThumbPref, false);
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
                _format = (ModelFormat)EditorGUILayout.EnumPopup(
                    new GUIContent("Output Format", "Model format to generate and download (sent as target_formats)."), _format);

                EditorGUILayout.LabelField("Texture", EditorStyles.boldLabel);
                _generateTexture = EditorGUILayout.Toggle(
                    new GUIContent("Generate Texture", "Generate a texture for the model."), _generateTexture);
                using (new EditorGUI.DisabledScope(!_generateTexture))
                {
                    _enablePbr = EditorGUILayout.Toggle(
                        new GUIContent("Enable PBR Maps", "Also generate metallic/roughness/normal maps."), _enablePbr);
                    _hdTexture = EditorGUILayout.Toggle(
                        new GUIContent("HD Texture", "Generate a higher-resolution texture (hd_texture)."), _hdTexture);
                }

                EditorGUILayout.LabelField("Geometry", EditorStyles.boldLabel);
                _remesh = EditorGUILayout.Toggle(
                    new GUIContent("Remesh", "Let Meshy clean up the topology (should_remesh)."), _remesh);
                using (new EditorGUI.DisabledScope(!_remesh))
                {
                    _topology = (MeshyTopology)EditorGUILayout.EnumPopup(
                        new GUIContent("Topology", "Target face topology when remeshing (topology)."), _topology);
                    _decimation = (DecimationMode)EditorGUILayout.EnumPopup(
                        new GUIContent("Decimation", "Remesh decimation preset (decimation_mode). 'None' lets Meshy decide, or set a target polycount instead."),
                        _decimation);
                    // target_polycount is the alternative to a decimation preset, so only offer it when no preset is chosen.
                    using (new EditorGUI.DisabledScope(_decimation != DecimationMode.None))
                    {
                        _targetPolycount = EditorGUILayout.IntSlider(
                            new GUIContent("Target Polycount", "Target triangle count when remeshing (target_polycount, 100–300000)."),
                            _targetPolycount, 100, 300000);
                    }
                    _savePreRemeshedModel = EditorGUILayout.Toggle(
                        new GUIContent("Save Pre-Remeshed Model", "Also keep the model before remeshing (save_pre_remeshed_model)."),
                        _savePreRemeshedModel);
                }

                EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
                // remove_lighting is only supported on meshy-6; grey it out (and force false) for other models.
                var supportsRemoveLighting = _aiModel == "meshy-6";
                if (!supportsRemoveLighting)
                    _removeLighting = false;
                using (new EditorGUI.DisabledScope(!supportsRemoveLighting))
                {
                    _removeLighting = EditorGUILayout.Toggle(
                        new GUIContent("Remove Lighting", "Bake out baked-in lighting from the source image (remove_lighting). Only available on meshy-6."), _removeLighting);
                }
                _autoSize = EditorGUILayout.Toggle(
                    new GUIContent("Auto Size", "Auto-scale the model to a realistic size (auto_size)."), _autoSize);
                _originAt = (ModelOrigin)EditorGUILayout.EnumPopup(
                    new GUIContent("Origin At", "Where the model's pivot sits (origin_at)."), _originAt);
                _moderation = EditorGUILayout.Toggle(
                    new GUIContent("Moderation", "Run content moderation on the input (moderation)."), _moderation);
                _multiViewThumbnails = EditorGUILayout.Toggle(
                    new GUIContent("Multi-View Thumbnails", "Generate thumbnails from several angles (multi_view_thumbnails)."), _multiViewThumbnails);
                _alphaThumbnail = EditorGUILayout.Toggle(
                    new GUIContent("Alpha Thumbnail", "Generate a thumbnail with a transparent background (alpha_thumbnail)."), _alphaThumbnail);
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
            EditorPrefs.SetInt(FormatPref, (int)_format);
            EditorPrefs.SetBool(TexturePref, _generateTexture);
            EditorPrefs.SetBool(PbrPref, _enablePbr);
            EditorPrefs.SetBool(HdTexturePref, _hdTexture);
            EditorPrefs.SetBool(RemeshPref, _remesh);
            EditorPrefs.SetInt(TopologyPref, (int)_topology);
            EditorPrefs.SetInt(DecimationPref, (int)_decimation);
            EditorPrefs.SetInt(PolycountPref, _targetPolycount);
            EditorPrefs.SetBool(SavePreRemeshPref, _savePreRemeshedModel);
            EditorPrefs.SetBool(RemoveLightingPref, _removeLighting);
            EditorPrefs.SetBool(ModerationPref, _moderation);
            EditorPrefs.SetBool(AutoSizePref, _autoSize);
            EditorPrefs.SetInt(OriginAtPref, (int)_originAt);
            EditorPrefs.SetBool(MultiViewThumbsPref, _multiViewThumbnails);
            EditorPrefs.SetBool(AlphaThumbPref, _alphaThumbnail);

            _running = true;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                // Core submit/poll/download lives in MeshyConversionCore so it can be driven
                // headlessly or as one stage of the image → mesh → voxels pipeline.
                var request = new MeshyRequest
                {
                    ImagePath = _imagePath,
                    Format = _format,
                    AiModel = _aiModel,
                    GenerateTexture = _generateTexture,
                    EnablePbr = _enablePbr,
                    HdTexture = _hdTexture,
                    Remesh = _remesh,
                    Topology = _topology,
                    Decimation = _decimation,
                    TargetPolycount = _targetPolycount,
                    SavePreRemeshedModel = _savePreRemeshedModel,
                    RemoveLighting = _removeLighting,
                    Moderation = _moderation,
                    AutoSize = _autoSize,
                    OriginAt = _originAt,
                    MultiViewThumbnails = _multiViewThumbnails,
                    AlphaThumbnail = _alphaThumbnail,
                };
                await MeshyConversionCore.ConvertAsync(
                    _apiKey, request, _outputDir, _outputFile, ct, SetStatus);
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
