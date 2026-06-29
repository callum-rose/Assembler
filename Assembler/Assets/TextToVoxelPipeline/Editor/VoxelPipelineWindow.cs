#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assembler.AssetGeneration.ImageGeneration;
using Assembler.AssetGeneration.MeshyImageTo3D;
using UnityEditor;
using UnityEngine;
using Assembler.AssetGeneration.VoxelPipeline;
using Assembler.AssetGeneration.VoxelPipeline.Generation;

namespace Assembler.AssetGeneration.TextToVoxelPipeline
{
    /// <summary>
    /// Editor window for the full text → voxel pipeline: type a prompt and get a <c>.vox</c>,
    /// driving the shared <see cref="VoxelPipeline.RunAsync"/> so the window and any headless caller
    /// take an identical path. The gap between stages is optionally reviewable — tick "Review image"
    /// / "Review mesh" and the run pauses after that stage (showing the image preview / the mesh path)
    /// until you press Continue, Retry (re-run that stage), or Cancel, so you can sanity-check an
    /// intermediate before paying for the next stage. All inputs are persisted in <see cref="EditorPrefs"/>.
    /// </summary>
    public sealed class VoxelPipelineWindow : EditorWindow
    {
        private const string Pref = "Assembler.TextToVoxel.";
        private const string DefaultPaletteAssetPath = "Assets/VoxelPipeline/MasterPalette.asset";

        private static readonly string[] MeshyModels = { "meshy-6", "meshy-5", "meshy-4" };

        // API keys are stored per provider so swapping providers keeps each key.
        private static string ImageApiKeyPref(ImageProvider provider) => $"{Pref}ImageApiKey.{provider}";

        private readonly VoxelPipelineSettings _settings = new();

        // Window-only voxel-stage state (the palette reference + preset are not part of the headless
        // settings: the palette is shared across assets, the preset is just a starting point for VoxSettings).
        [SerializeField] private VoxPipelinePreset _preset = VoxPipelinePreset.Creature;
        [SerializeField] private VoxMasterPalette? _palette;

        private bool _reviewImage;
        private bool _reviewMesh;

        // AI-config import (paste JSON from the AI Model Config window).
        private bool _showImport;
        private string _aiConfigPaste = "";

        private bool _running;
        private string _status = "Idle.";
        private CancellationTokenSource? _cts;
        private Vector2 _scroll;
        private Texture2D? _preview;

        // Review-gate state: while a stage is awaiting Continue, the window shows the intermediate.
        private enum ReviewStage { None, Image, Mesh }
        private ReviewStage _reviewStage = ReviewStage.None;
        private TaskCompletionSource<VoxelPipeline.ReviewDecision>? _reviewGate;
        private CancellationTokenRegistration _reviewRegistration;
        private string _reviewMeshPath = "";

        [MenuItem("Assembler/Text to Voxels (pipeline)")]
        public static void Open()
        {
            var window = GetWindow<VoxelPipelineWindow>("Text to Voxels");
            window.minSize = new Vector2(480, 640);
        }

        private void OnEnable() => LoadState();

        private void OnDisable()
        {
            SaveState();
            if (_preview != null)
                DestroyImmediate(_preview);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            using (new EditorGUI.DisabledScope(_running))
            {
                DrawImportFromAi();
                EditorGUILayout.Space();
                DrawPromptStage();
                EditorGUILayout.Space();
                DrawMeshStage();
                EditorGUILayout.Space();
                DrawVoxelStage();
                EditorGUILayout.Space();
                DrawOutput();
                EditorGUILayout.Space();
                DrawReviewToggles();
            }

            EditorGUILayout.Space();
            DrawActions();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(_status, _running ? MessageType.Info : MessageType.None);

            DrawReviewPanel();
            DrawPreview();

            EditorGUILayout.EndScrollView();
        }

        // ---- Import from the AI Model Config window --------------------------

        // Paste the config JSON the AI Model Config window produced and apply it to the run
        // (prompt + preset + post-processing settings + resolution). A pasted value is parsed
        // on demand — nothing is shared between the two windows, so there are no fragile pref keys.
        private void DrawImportFromAi()
        {
            _showImport = EditorGUILayout.Foldout(_showImport, "Import AI config (paste JSON)", true);
            if (!_showImport)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField(
                    "Paste the config JSON from the AI Model Config window (the whole reply or just the json).",
                    EditorStyles.miniLabel);
                var wrap = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
                _aiConfigPaste = EditorGUILayout.TextArea(_aiConfigPaste, wrap, GUILayout.MinHeight(70));
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_aiConfigPaste)))
                {
                    if (GUILayout.Button("Import"))
                        ImportFromAi();
                }
            }
        }

        private void ImportFromAi()
        {
            try
            {
                // Accept either a full fenced assistant reply or the bare json object.
                var text = _aiConfigPaste.Trim();
                var json = VoxConfigExtractor.Extract(text) ?? text;
                var config = VoxConfigParser.ParseJson(json);

                if (!string.IsNullOrEmpty(config.ImagePrompt))
                    _settings.Prompt = config.ImagePrompt;
                _preset = config.Preset;
                _settings.VoxSettings = config.Settings;
                _settings.MaxDimVoxels = config.Resolution;

                // Image → mesh (Meshy) generation parameters.
                var m = config.Meshy;
                _settings.MeshAiModel = m.MeshAiModel;
                _settings.MeshFormat = m.MeshFormat;
                _settings.GenerateTexture = m.GenerateTexture;
                _settings.EnablePbr = m.EnablePbr;
                _settings.HdTexture = m.HdTexture;
                _settings.Remesh = m.Remesh;
                _settings.Topology = m.Topology;
                _settings.Decimation = m.Decimation;
                _settings.TargetPolycount = m.TargetPolycount;
                _settings.SavePreRemeshedModel = m.SavePreRemeshedModel;
                _settings.RemoveLighting = m.RemoveLighting;
                _settings.AutoSize = m.AutoSize;
                _settings.OriginAt = m.OriginAt;
                _settings.Moderation = m.Moderation;
                _settings.MultiViewThumbnails = m.MultiViewThumbnails;
                _settings.AlphaThumbnail = m.AlphaThumbnail;

                // Drop keyboard focus so the prompt text field repaints with the imported value.
                GUI.FocusControl(null);
                SaveState();
                SetStatus($"Imported AI config — preset {config.Preset}, {config.Resolution} voxels.");
            }
            catch (Exception e)
            {
                SetStatus($"Import failed: {e.Message}");
            }
        }

        // ---- Stage 1: prompt → image ----------------------------------------

        private void DrawPromptStage()
        {
            EditorGUILayout.LabelField("1 · Text → Image", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _settings.ImageProvider = (ImageProvider)EditorGUILayout.EnumPopup("Provider", _settings.ImageProvider);
            if (EditorGUI.EndChangeCheck())
            {
                _settings.ImageApiKey = EditorPrefs.GetString(ImageApiKeyPref(_settings.ImageProvider), "");
                _settings.ImageModel = ImageGeneratorFactory.DefaultModelFor(_settings.ImageProvider);
            }

            _settings.ImageModel = ModelPopup(
                "Image Model", _settings.ImageModel, ImageGeneratorFactory.AvailableModelsFor(_settings.ImageProvider));

            DrawApiKeyRow("Image API Key", ref _settings.ImageApiKey, ImageApiKeyPref(_settings.ImageProvider));

            EditorGUILayout.LabelField("Prompt");
            var wrap = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            _settings.Prompt = EditorGUILayout.TextArea(_settings.Prompt, wrap, GUILayout.MinHeight(70));
        }

        // ---- Stage 2: image → mesh ------------------------------------------

        private void DrawMeshStage()
        {
            EditorGUILayout.LabelField("2 · Image → Mesh (Meshy.ai)", EditorStyles.boldLabel);

            DrawApiKeyRow("Meshy API Key", ref _settings.MeshyApiKey, Pref + "MeshyApiKey");

            _settings.MeshAiModel = ModelPopup("Meshy Model", _settings.MeshAiModel, MeshyModels);
            _settings.MeshFormat = (ModelFormat)EditorGUILayout.EnumPopup(
                new GUIContent("Output Format", "Model format to generate and download (sent as target_formats)."), _settings.MeshFormat);

            EditorGUILayout.LabelField("Texture", EditorStyles.boldLabel);
            _settings.GenerateTexture = EditorGUILayout.Toggle(
                new GUIContent("Generate Texture", "Generate a texture for the model."), _settings.GenerateTexture);
            using (new EditorGUI.DisabledScope(!_settings.GenerateTexture))
            {
                _settings.EnablePbr = EditorGUILayout.Toggle(
                    new GUIContent("Enable PBR Maps", "Also generate metallic/roughness/normal maps."), _settings.EnablePbr);
                _settings.HdTexture = EditorGUILayout.Toggle(
                    new GUIContent("HD Texture", "Generate a higher-resolution texture (hd_texture)."), _settings.HdTexture);
            }

            EditorGUILayout.LabelField("Geometry", EditorStyles.boldLabel);
            _settings.Remesh = EditorGUILayout.Toggle(
                new GUIContent("Remesh", "Let Meshy clean up the topology (should_remesh)."), _settings.Remesh);
            using (new EditorGUI.DisabledScope(!_settings.Remesh))
            {
                _settings.Topology = (MeshyTopology)EditorGUILayout.EnumPopup(
                    new GUIContent("Topology", "Target face topology when remeshing (topology)."), _settings.Topology);
                _settings.Decimation = (DecimationMode)EditorGUILayout.EnumPopup(
                    new GUIContent("Decimation", "Remesh decimation preset (decimation_mode). 'None' lets Meshy decide, or set a target polycount instead."),
                    _settings.Decimation);
                // target_polycount is the alternative to a decimation preset, so only offer it when no preset is chosen.
                using (new EditorGUI.DisabledScope(_settings.Decimation != DecimationMode.None))
                {
                    _settings.TargetPolycount = EditorGUILayout.IntSlider(
                        new GUIContent("Target Polycount", "Target triangle count when remeshing (target_polycount, 100–300000)."),
                        _settings.TargetPolycount, 100, 300000);
                }
                _settings.SavePreRemeshedModel = EditorGUILayout.Toggle(
                    new GUIContent("Save Pre-Remeshed Model", "Also keep the model before remeshing (save_pre_remeshed_model)."),
                    _settings.SavePreRemeshedModel);
            }

            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            // remove_lighting is only supported on meshy-6; grey it out (and force false) for other models.
            var supportsRemoveLighting = _settings.MeshAiModel == "meshy-6";
            if (!supportsRemoveLighting)
                _settings.RemoveLighting = false;
            using (new EditorGUI.DisabledScope(!supportsRemoveLighting))
            {
                _settings.RemoveLighting = EditorGUILayout.Toggle(
                    new GUIContent("Remove Lighting", "Bake out baked-in lighting from the source image (remove_lighting). Only available on meshy-6."),
                    _settings.RemoveLighting);
            }
            _settings.AutoSize = EditorGUILayout.Toggle(
                new GUIContent("Auto Size", "Auto-scale the model to a realistic size (auto_size)."), _settings.AutoSize);
            _settings.OriginAt = (ModelOrigin)EditorGUILayout.EnumPopup(
                new GUIContent("Origin At", "Where the model's pivot sits (origin_at)."), _settings.OriginAt);
            _settings.Moderation = EditorGUILayout.Toggle(
                new GUIContent("Moderation", "Run content moderation on the input (moderation)."), _settings.Moderation);
            _settings.MultiViewThumbnails = EditorGUILayout.Toggle(
                new GUIContent("Multi-View Thumbnails", "Generate thumbnails from several angles (multi_view_thumbnails)."), _settings.MultiViewThumbnails);
            _settings.AlphaThumbnail = EditorGUILayout.Toggle(
                new GUIContent("Alpha Thumbnail", "Generate a thumbnail with a transparent background (alpha_thumbnail)."), _settings.AlphaThumbnail);
        }

        // ---- Stage 3: mesh → voxels -----------------------------------------

        private void DrawVoxelStage()
        {
            EditorGUILayout.LabelField("3 · Mesh → Voxels", EditorStyles.boldLabel);

            _settings.MaxDimVoxels = EditorGUILayout.IntSlider(
                new GUIContent("Max dimension (voxels)", "Longest bounding-box axis gets this many voxels; the others scale proportionally."),
                _settings.MaxDimVoxels, 1, 256);
            if (_settings.MaxDimVoxels >= 96)
            {
                EditorGUILayout.HelpBox(
                    "High resolutions run millions of winding-number queries and can take a while (runs on a background thread, with a cancelable progress bar).",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();
            DrawPipelineControls();
        }

        // Preset picker + per-step overrides, ported from MeshToVoxelsWindow: choosing a preset
        // loads its settings; the toggles below are the per-asset override on top.
        private void DrawPipelineControls()
        {
            var s = _settings.VoxSettings;

            using (new EditorGUILayout.HorizontalScope())
            {
                var newPreset = (VoxPipelinePreset)EditorGUILayout.EnumPopup(
                    new GUIContent("Preset", "Category starting point. Selecting one loads its step settings, which you can then tweak below."),
                    _preset);
                if (newPreset != _preset)
                {
                    _preset = newPreset;
                    _settings.VoxSettings = s = VoxPipelinePresets.For(_preset);
                }
                if (GUILayout.Button("Reset to preset", GUILayout.Width(120)))
                {
                    _settings.VoxSettings = s = VoxPipelinePresets.For(_preset);
                }
            }

            EditorGUILayout.Space();

            s.removeFloaters = EditorGUILayout.ToggleLeft(
                new GUIContent("Remove floaters", "Delete small disconnected components (voxelization specks). Substantial detached parts are kept."),
                s.removeFloaters);
            if (s.removeFloaters)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    s.floaterMinPercent = EditorGUILayout.Slider(
                        new GUIContent("Min component %", "A component covering less than this % of voxels (and < 2 voxels) is removed."),
                        s.floaterMinPercent, 0f, 10f);
                }
            }

            s.mirror = EditorGUILayout.ToggleLeft(
                new GUIContent("Mirror (force symmetry)", "Mirror one half about a plane onto the other. Off by default — erases intentional asymmetry."),
                s.mirror);
            if (s.mirror)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    s.mirrorAxis = (SymmetryAxis)EditorGUILayout.EnumPopup(
                        new GUIContent("Mirror axis", "Axis the mirror plane is perpendicular to. Left/right (X) is the usual bilateral plane."),
                        s.mirrorAxis);
                    s.mirrorConfidence = EditorGUILayout.Slider(
                        new GUIContent("Confidence gate", "Min mirror-overlap score to auto-apply. Below this the model is left as-is."),
                        s.mirrorConfidence, 0f, 1f);
                    s.mirrorForce = EditorGUILayout.ToggleLeft(
                        new GUIContent("Force past gate", "Apply at the best-scoring plane even when the confidence gate fails."),
                        s.mirrorForce);
                }
            }

            s.revolve = EditorGUILayout.ToggleLeft(
                new GUIContent("Revolve (force roundness)", "Revolve the radial profile into a true solid of revolution. For standalone wheels/cylinders only."),
                s.revolve);
            if (s.revolve)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    s.revolveAxis = (SymmetryAxis)EditorGUILayout.EnumPopup(
                        new GUIContent("Spin axis", "Axis the profile is revolved about. Up (Y) is the usual wheel axle."),
                        s.revolveAxis);
                    s.revolveFillThreshold = EditorGUILayout.Slider(
                        new GUIContent("Ring fill threshold", "A ring is filled when at least this fraction of its cells were occupied."),
                        s.revolveFillThreshold, 0f, 1f);
                }
            }

            s.deLight = EditorGUILayout.ToggleLeft(
                new GUIContent("De-light", "Flatten baked shading: grow material regions of similar colour and collapse each to one flat colour."),
                s.deLight);
            if (s.deLight)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    s.deLightThreshold = EditorGUILayout.Slider(
                        new GUIContent("Region similarity (Oklab)", "Max perceptual distance between adjacent voxels to join one region. Higher = larger, flatter regions."),
                        s.deLightThreshold, 0f, 0.5f);
                }
            }

            s.snapToHistogramPeaks = EditorGUILayout.ToggleLeft(
                new GUIContent("Snap to histogram peaks", "Reduce to the model's own dominant colours before the master-palette snap."),
                s.snapToHistogramPeaks);
            if (s.snapToHistogramPeaks)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    s.histogramPeakVariety = EditorGUILayout.Slider(
                        new GUIContent("Variety threshold (Oklab)", "Keep adding peaks while each new colour is at least this distinct. Higher = fewer, more distinct colours."),
                        s.histogramPeakVariety, 0f, 0.5f);
                    s.histogramPeakCount = EditorGUILayout.IntSlider(
                        new GUIContent("Max peaks (cap)", "Upper bound on how many distinct colours to keep."),
                        s.histogramPeakCount, 1, 64);
                }
            }

            s.snapToPalette = EditorGUILayout.ToggleLeft(
                new GUIContent("Snap to master palette", "Snap each colour to the nearest swatch in a shared master palette (Oklab) for cross-asset cohesion."),
                s.snapToPalette);
            if (s.snapToPalette)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _palette = (VoxMasterPalette?)EditorGUILayout.ObjectField(
                        new GUIContent("Master palette", "Hand-authored swatches. Leave empty to use the built-in starter palette."),
                        _palette, typeof(VoxMasterPalette), false);
                    if (_palette == null)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("Using built-in starter palette.", EditorStyles.miniLabel);
                            if (GUILayout.Button("Create starter palette…", GUILayout.Width(170)))
                                _palette = CreateStarterPalette();
                        }
                    }
                }
            }

            s.morphology = EditorGUILayout.ToggleLeft(
                new GUIContent("Despeckle / fill (morphology)", "Mild geometric despeckle/fill. Best left off for organic models — can erode thin features."),
                s.morphology);
        }

        // ---- Output + review toggles ----------------------------------------

        private void DrawOutput()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _settings.OutputDir = EditorGUILayout.TextField("Output Directory", _settings.OutputDir);
                if (GUILayout.Button("Browse", GUILayout.Width(70)))
                {
                    var picked = EditorUtility.OpenFolderPanel("Output directory", GuessStartDir(_settings.OutputDir), "");
                    if (!string.IsNullOrEmpty(picked))
                        _settings.OutputDir = picked;
                }
            }
            _settings.BaseName = EditorGUILayout.TextField(
                new GUIContent("Base Name", "Shared by all three files (image/mesh/.vox). Leave blank to slug it from the prompt."),
                _settings.BaseName);
            _settings.AutoSubfolderPerRun = EditorGUILayout.ToggleLeft(
                new GUIContent("Subfolder per run",
                    "Put each run's files in their own <base>_<timestamp> subfolder so runs don't overwrite each other. Off: write straight into the output directory."),
                _settings.AutoSubfolderPerRun);

            var baseName = VoxelPipeline.ResolveBaseName(_settings);
            var prefix = _settings.AutoSubfolderPerRun ? $"{baseName}_<timestamp>/" : "";
            EditorGUILayout.LabelField(" ", $"→ {prefix}{baseName}.png / .obj / .vox", EditorStyles.miniLabel);
        }

        private void DrawReviewToggles()
        {
            EditorGUILayout.LabelField("Review gates", EditorStyles.boldLabel);
            _reviewImage = EditorGUILayout.ToggleLeft(
                new GUIContent("Review image before meshing", "Pause after stage 1 to inspect the generated image."), _reviewImage);
            _reviewMesh = EditorGUILayout.ToggleLeft(
                new GUIContent("Review mesh before voxelizing", "Pause after stage 2 to inspect the generated mesh."), _reviewMesh);
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_running))
                {
                    if (GUILayout.Button("Run pipeline", GUILayout.Height(30)))
                        _ = RunAsync();
                }
                using (new EditorGUI.DisabledScope(!_running))
                {
                    if (GUILayout.Button("Cancel", GUILayout.Height(30), GUILayout.Width(100)))
                        _cts?.Cancel();
                }
            }
        }

        // ---- Review panel (shown while a stage awaits Continue) --------------

        private void DrawReviewPanel()
        {
            if (_reviewStage == ReviewStage.None)
                return;

            EditorGUILayout.Space();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var what = _reviewStage == ReviewStage.Image ? "image" : "mesh";
                EditorGUILayout.LabelField($"Review the {what}", EditorStyles.boldLabel);

                if (_reviewStage == ReviewStage.Mesh)
                {
                    EditorGUILayout.SelectableLabel(_reviewMeshPath, EditorStyles.textField,
                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    if (MeshyConversionCore.IsUnderAssets(_reviewMeshPath) && GUILayout.Button("Select in Project", GUILayout.Width(140)))
                        PingAsset(_reviewMeshPath);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Continue ▶", GUILayout.Height(26)))
                        ResolveReview(VoxelPipeline.ReviewDecision.Continue);
                    if (GUILayout.Button(new GUIContent($"↻ Retry {what}", $"Discard this {what} and run the stage again."),
                            GUILayout.Height(26), GUILayout.Width(120)))
                        ResolveReview(VoxelPipeline.ReviewDecision.Retry);
                    if (GUILayout.Button("Cancel", GUILayout.Height(26), GUILayout.Width(100)))
                        _cts?.Cancel();
                }
            }
        }

        private void DrawPreview()
        {
            if (_preview == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Image preview", EditorStyles.boldLabel);
            var width = Mathf.Min(position.width - 30, _preview.width);
            var height = width * _preview.height / Mathf.Max(1, _preview.width);
            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(rect, _preview, ScaleMode.ScaleToFit);
        }

        // ---- Run -------------------------------------------------------------

        private async Task RunAsync()
        {
            SaveState();
            _settings.Palette = _palette != null ? _palette.ToColor32() : DefaultMasterPalette.Colors;

            _running = true;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            VoxelPipeline.ReviewGate<ImageGenerationCore.Result>? imageGate = _reviewImage ? ImageReviewGate : null;
            VoxelPipeline.ReviewGate<MeshyConversionCore.Result>? meshGate = _reviewMesh ? MeshReviewGate : null;

            try
            {
                var result = await VoxelPipeline.RunAsync(
                    _settings, ct, SetStatus,
                    reviewImage: imageGate,
                    reviewMesh: meshGate,
                    voxelProgress: new EditorProgressReporter(),
                    pipelineProgress: (name, fraction) =>
                        EditorUtility.DisplayProgressBar("Mesh → VOX", $"Post-processing: {name}…", 0.9f + 0.09f * fraction));

                // The pipeline reports every outcome through the result. On success the in-run "Done."
                // status already stands; otherwise surface the case's message, and for a per-stage
                // failure also dump the carried exception's stack.
                var stageError = result switch
                {
                    VoxelPipeline.Result.ImageFailed f => f.Error,
                    VoxelPipeline.Result.MeshFailed f => f.Error,
                    VoxelPipeline.Result.VoxelizationFailed f => f.Error,
                    _ => null,
                };

                if (result is not VoxelPipeline.Result.Success)
                    SetStatus(result.ToString());
                if (stageError is not null)
                    Debug.LogException(stageError);
            }
            catch (Exception e)
            {
                SetStatus($"Error: {e.Message}");
                Debug.LogException(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EndReview();
                _running = false;
                _cts?.Dispose();
                _cts = null;
                Repaint();
            }
        }

        private Task<VoxelPipeline.ReviewDecision> ImageReviewGate(ImageGenerationCore.Result image, CancellationToken ct)
        {
            LoadPreview(image.Image.Bytes);
            return BeginReview(ReviewStage.Image, ct);
        }

        private Task<VoxelPipeline.ReviewDecision> MeshReviewGate(MeshyConversionCore.Result mesh, CancellationToken ct)
        {
            _reviewMeshPath = mesh.OutputPath;
            return BeginReview(ReviewStage.Mesh, ct);
        }

        // Hand control to the user: park on a TaskCompletionSource the Continue/Retry buttons complete
        // (Cancel cancels the run's token, which fails the gate the same way as throwing would).
        private Task<VoxelPipeline.ReviewDecision> BeginReview(ReviewStage stage, CancellationToken ct)
        {
            _reviewStage = stage;
            _reviewGate = new TaskCompletionSource<VoxelPipeline.ReviewDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
            _reviewRegistration = ct.Register(() => _reviewGate?.TrySetCanceled(ct));
            Repaint();
            return _reviewGate.Task;
        }

        private void ResolveReview(VoxelPipeline.ReviewDecision decision)
        {
            _reviewRegistration.Dispose();
            _reviewStage = ReviewStage.None;
            var gate = _reviewGate;
            _reviewGate = null;
            gate?.TrySetResult(decision);
            Repaint();
        }

        private void EndReview()
        {
            _reviewRegistration.Dispose();
            _reviewStage = ReviewStage.None;
            _reviewGate = null;
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

        // ---- Helpers ---------------------------------------------------------

        // Dropdown of known model ids that keeps any previously-saved custom id selectable.
        private static string ModelPopup(string label, string current, string[] models)
        {
            var options = Array.IndexOf(models, current) >= 0 || string.IsNullOrEmpty(current)
                ? models
                : new[] { current }.Concat(models).ToArray();
            if (options.Length == 0)
                return current;
            var index = Mathf.Max(0, Array.IndexOf(options, current));
            index = EditorGUILayout.Popup(new GUIContent(label), index, options.Select(m => new GUIContent(m)).ToArray());
            return options[index];
        }

        private static void DrawApiKeyRow(string label, ref string apiKey, string prefKey)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                apiKey = EditorGUILayout.PasswordField(label, apiKey);
                if (GUILayout.Button("Save", GUILayout.Width(60)))
                    EditorPrefs.SetString(prefKey, apiKey);
            }
        }

        private static void PingAsset(string path)
        {
            var rel = ToAssetRelative(path);
            var obj = AssetDatabase.LoadMainAssetAtPath(rel);
            if (obj != null)
            {
                EditorGUIUtility.PingObject(obj);
                Selection.activeObject = obj;
            }
        }

        private static string ToAssetRelative(string path)
        {
            var full = Path.GetFullPath(path).Replace('\\', '/');
            var project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/') + "/";
            return full.StartsWith(project, StringComparison.OrdinalIgnoreCase) ? full[project.Length..] : path;
        }

        private static string GuessStartDir(string path)
        {
            if (string.IsNullOrEmpty(path))
                return Application.dataPath;
            var dir = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(dir) ? Application.dataPath : dir;
        }

        private VoxMasterPalette CreateStarterPalette()
        {
            var palette = CreateInstance<VoxMasterPalette>();
            palette.SetColors(DefaultMasterPalette.Colors);

            var dir = Path.GetDirectoryName(DefaultPaletteAssetPath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var path = AssetDatabase.GenerateUniqueAssetPath(DefaultPaletteAssetPath);
            AssetDatabase.CreateAsset(palette, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(palette);
            return palette;
        }

        // ---- EditorPrefs persistence ----------------------------------------

        private void LoadState()
        {
            _settings.ImageProvider = (ImageProvider)EditorPrefs.GetInt(Pref + "Provider", (int)ImageProvider.GoogleGemini);
            _settings.ImageModel = EditorPrefs.GetString(Pref + "ImageModel." + _settings.ImageProvider, ImageGeneratorFactory.DefaultModelFor(_settings.ImageProvider));
            _settings.ImageApiKey = EditorPrefs.GetString(ImageApiKeyPref(_settings.ImageProvider), "");
            _settings.Prompt = EditorPrefs.GetString(Pref + "Prompt", "");

            _settings.MeshyApiKey = EditorPrefs.GetString(Pref + "MeshyApiKey", "");
            _settings.MeshAiModel = EditorPrefs.GetString(Pref + "MeshModel", "meshy-6");
            _settings.MeshFormat = (ModelFormat)EditorPrefs.GetInt(Pref + "MeshFormat", (int)ModelFormat.Obj);
            _settings.GenerateTexture = EditorPrefs.GetBool(Pref + "GenerateTexture", true);
            _settings.EnablePbr = EditorPrefs.GetBool(Pref + "EnablePbr", true);
            _settings.HdTexture = EditorPrefs.GetBool(Pref + "HdTexture", false);
            _settings.Remesh = EditorPrefs.GetBool(Pref + "Remesh", true);
            _settings.Topology = (MeshyTopology)EditorPrefs.GetInt(Pref + "Topology", (int)MeshyTopology.Triangle);
            _settings.Decimation = (DecimationMode)EditorPrefs.GetInt(Pref + "Decimation", (int)DecimationMode.None);
            _settings.TargetPolycount = EditorPrefs.GetInt(Pref + "TargetPolycount", 30000);
            _settings.SavePreRemeshedModel = EditorPrefs.GetBool(Pref + "SavePreRemeshedModel", false);
            _settings.RemoveLighting = EditorPrefs.GetBool(Pref + "RemoveLighting", true);
            _settings.Moderation = EditorPrefs.GetBool(Pref + "Moderation", false);
            _settings.AutoSize = EditorPrefs.GetBool(Pref + "AutoSize", false);
            _settings.OriginAt = (ModelOrigin)EditorPrefs.GetInt(Pref + "OriginAt", (int)ModelOrigin.Bottom);
            _settings.MultiViewThumbnails = EditorPrefs.GetBool(Pref + "MultiViewThumbnails", false);
            _settings.AlphaThumbnail = EditorPrefs.GetBool(Pref + "AlphaThumbnail", false);

            _settings.MaxDimVoxels = EditorPrefs.GetInt(Pref + "MaxDim", 32);
            _preset = (VoxPipelinePreset)EditorPrefs.GetInt(Pref + "Preset", (int)VoxPipelinePreset.Creature);
            _settings.VoxSettings = VoxPipelinePresets.For(_preset);
            var settingsJson = EditorPrefs.GetString(Pref + "VoxSettings", "");
            if (!string.IsNullOrEmpty(settingsJson))
                JsonUtility.FromJsonOverwrite(settingsJson, _settings.VoxSettings);

            var paletteGuid = EditorPrefs.GetString(Pref + "PaletteGuid", "");
            if (!string.IsNullOrEmpty(paletteGuid))
            {
                var path = AssetDatabase.GUIDToAssetPath(paletteGuid);
                _palette = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<VoxMasterPalette>(path);
            }

            _settings.OutputDir = EditorPrefs.GetString(Pref + "OutputDir", "Assets/TextToVoxel");
            _settings.BaseName = EditorPrefs.GetString(Pref + "BaseName", "");
            _settings.AutoSubfolderPerRun = EditorPrefs.GetBool(Pref + "AutoSubfolderPerRun", true);
            _reviewImage = EditorPrefs.GetBool(Pref + "ReviewImage", false);
            _reviewMesh = EditorPrefs.GetBool(Pref + "ReviewMesh", false);
        }

        private void SaveState()
        {
            EditorPrefs.SetInt(Pref + "Provider", (int)_settings.ImageProvider);
            EditorPrefs.SetString(Pref + "ImageModel." + _settings.ImageProvider, _settings.ImageModel);
            EditorPrefs.SetString(ImageApiKeyPref(_settings.ImageProvider), _settings.ImageApiKey);
            EditorPrefs.SetString(Pref + "Prompt", _settings.Prompt);

            EditorPrefs.SetString(Pref + "MeshyApiKey", _settings.MeshyApiKey);
            EditorPrefs.SetString(Pref + "MeshModel", _settings.MeshAiModel);
            EditorPrefs.SetInt(Pref + "MeshFormat", (int)_settings.MeshFormat);
            EditorPrefs.SetBool(Pref + "GenerateTexture", _settings.GenerateTexture);
            EditorPrefs.SetBool(Pref + "EnablePbr", _settings.EnablePbr);
            EditorPrefs.SetBool(Pref + "HdTexture", _settings.HdTexture);
            EditorPrefs.SetBool(Pref + "Remesh", _settings.Remesh);
            EditorPrefs.SetInt(Pref + "Topology", (int)_settings.Topology);
            EditorPrefs.SetInt(Pref + "Decimation", (int)_settings.Decimation);
            EditorPrefs.SetInt(Pref + "TargetPolycount", _settings.TargetPolycount);
            EditorPrefs.SetBool(Pref + "SavePreRemeshedModel", _settings.SavePreRemeshedModel);
            EditorPrefs.SetBool(Pref + "RemoveLighting", _settings.RemoveLighting);
            EditorPrefs.SetBool(Pref + "Moderation", _settings.Moderation);
            EditorPrefs.SetBool(Pref + "AutoSize", _settings.AutoSize);
            EditorPrefs.SetInt(Pref + "OriginAt", (int)_settings.OriginAt);
            EditorPrefs.SetBool(Pref + "MultiViewThumbnails", _settings.MultiViewThumbnails);
            EditorPrefs.SetBool(Pref + "AlphaThumbnail", _settings.AlphaThumbnail);

            EditorPrefs.SetInt(Pref + "MaxDim", _settings.MaxDimVoxels);
            EditorPrefs.SetInt(Pref + "Preset", (int)_preset);
            EditorPrefs.SetString(Pref + "VoxSettings", JsonUtility.ToJson(_settings.VoxSettings));

            var assetPath = _palette != null ? AssetDatabase.GetAssetPath(_palette) : "";
            EditorPrefs.SetString(Pref + "PaletteGuid",
                string.IsNullOrEmpty(assetPath) ? "" : AssetDatabase.AssetPathToGUID(assetPath));

            EditorPrefs.SetString(Pref + "OutputDir", _settings.OutputDir);
            EditorPrefs.SetString(Pref + "BaseName", _settings.BaseName);
            EditorPrefs.SetBool(Pref + "AutoSubfolderPerRun", _settings.AutoSubfolderPerRun);
            EditorPrefs.SetBool(Pref + "ReviewImage", _reviewImage);
            EditorPrefs.SetBool(Pref + "ReviewMesh", _reviewMesh);
        }

        private sealed class EditorProgressReporter : IProgressReporter
        {
            public bool Report(float fraction, string message) =>
                !EditorUtility.DisplayCancelableProgressBar("Mesh → VOX", message, fraction);
        }
    }
}
