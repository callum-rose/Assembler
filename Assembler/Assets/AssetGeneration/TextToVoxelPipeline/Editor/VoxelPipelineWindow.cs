#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assembler.AssetGeneration.ImageToMesh;
using Assembler.AssetGeneration.MeshToVoxels;
using Assembler.AssetGeneration.MeshToVoxel;
using Assembler.AssetGeneration.MeshToVoxel.Generation;
using Assembler.AssetGeneration.TextToImage;
using UnityEditor;
using UnityEngine;
using Assembler.AssetGeneration.Colour;

namespace Assembler.AssetGeneration.TextToVoxelPipeline.Editor
{
    /// <summary>
    /// Editor window for the full text → voxel pipeline: type a prompt and get a <c>.vox</c>,
    /// driving the shared <see cref="VoxelPipeline.RunAsync"/> so the window and any headless caller
    /// take an identical path. The gap between stages is optionally reviewable — tick "Review image"
    /// / "Review mesh" and the run pauses after that stage (showing the image preview / the mesh path)
    /// until you press Continue, Retry (re-run that stage), or Cancel, so you can sanity-check an
    /// intermediate before paying for the next stage. Stage 3 mirrors the standalone Mesh → Voxel
    /// Mesh to Voxel window's full control set. All inputs are persisted in <see cref="EditorPrefs"/>.
    /// </summary>
    public sealed class VoxelPipelineWindow : EditorWindow
    {
        private const string Pref = "Assembler.TextToVoxel.";
        private const int FineNodeWarningDim = 120;

        private static readonly string[] MeshyModels = { "meshy-6", "meshy-5", "meshy-4" };

        // API keys are stored per provider so swapping providers keeps each key.
        private static string ImageApiKeyPref(ImageProvider provider) => $"{Pref}ImageApiKey.{provider}";

        private readonly VoxelPipelineSettings _settings = new();

        // ---- Stage-3 (mesh → voxel) controls, held as window fields and assembled into
        //      _settings.Vox at run time. Seeded from the core's defaults. ----

        private ResolutionInput _resolutionInput = ResolutionInput.MaxDimSlider;
        private int _maxDimVoxels = 24;
        private float _voxelWorldSize = 0.1f;
        private float _targetWorldSize = 2f;

        private bool _gridSearch = true;
        private bool _scaleFlex = true;
        private bool _thinFeatureKeep = true;
        private int _fineFactor = 3;
        private float _coverage = 0.5f;
        private bool _removeFloaters = true;
        private int _cleanupStrength = 1;
        private bool _fillCorners;
        private bool _fillCornersRecursive;
        private float _cornerFillColourTolerance = 0.1f;
        private SymmetryAxes _symmetry = SymmetryAxes.None;
        private bool _forceMirror;

        private bool _showAdvancedWeights;
        private float _faceWeight = 1f;
        private float _iouWeight = 1f;
        private float _gapWeight = 2f;
        private float _colWeight;

        private bool _uvDilate = true;
        private int _uvDilatePasses = UvIslandDilation.DefaultPasses;
        private bool _multiSampleColour = true;
        private float _pottsStrength = 0.5f;
        private ColourMode _colourMode = ColourMode.PerModelPalette;
        private int _paletteSize = 8;
        private float _consolidateTolerance = 0.06f;
        private int _consolidateMaxColours;
        private bool _normalConsistency;

        private int _taubinPasses = 5;
        private float _taubinLambda = 0.5f;
        private float _taubinMu = 0.53f;
        private bool _surfaceReproject;

        [SerializeField] private VoxMasterPalette? _masterPalette;

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
        // (prompt + voxel settings + Meshy settings). A pasted value is parsed on demand — nothing is
        // shared between the two windows, so there are no fragile pref keys.
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
                var json = ConfigExtractor.Extract(text) ?? text;
                var config = ConfigParser.ParseJson(json);

                if (!string.IsNullOrEmpty(config.ImagePrompt))
                    _settings.Prompt = config.ImagePrompt;
                if (!string.IsNullOrEmpty(config.BaseName))
                    _settings.BaseName = config.BaseName;

                // Mesh → voxel settings map onto the individual stage-3 knobs (the master palette is the
                // window's, never the AI's).
                Settings v = config.Settings;
                _resolutionInput = v.ResolutionInput;
                _maxDimVoxels = v.MaxDimVoxels;
                _voxelWorldSize = v.VoxelWorldSize;
                _targetWorldSize = v.TargetWorldSize;
                _gridSearch = v.GridSearch;
                _scaleFlex = v.ScaleFlex;
                _thinFeatureKeep = v.ThinFeatureKeep;
                _fineFactor = v.FineFactor;
                _coverage = v.Coverage;
                _removeFloaters = v.RemoveFloaters;
                _cleanupStrength = v.CleanupStrength;
                _fillCorners = v.FillCorners;
                _fillCornersRecursive = v.FillCornersRecursive;
                _cornerFillColourTolerance = v.CornerFillColourTolerance;
                _symmetry = v.Symmetry;
                _forceMirror = v.ForceMirror;
                _faceWeight = v.FaceWeight;
                _iouWeight = v.IouWeight;
                _gapWeight = v.GapWeight;
                _colWeight = v.ColWeight;
                _uvDilate = v.UvDilate;
                _uvDilatePasses = v.UvDilatePasses;
                _multiSampleColour = v.MultiSampleColour;
                _pottsStrength = v.PottsStrength;
                _colourMode = v.ColourMode;
                _paletteSize = v.PaletteSize;
                _consolidateTolerance = v.ConsolidateTolerance;
                _consolidateMaxColours = v.ConsolidateMaxColours;
                _normalConsistency = v.NormalConsistency;
                _taubinPasses = v.TaubinPasses;
                _taubinLambda = v.TaubinLambda;
                _taubinMu = v.TaubinMu;
                _surfaceReproject = v.SurfaceReproject;

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
                SetStatus($"Imported AI config — {config.Settings.MaxDimVoxels} voxels.");
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

        // ---- Stage 3: mesh → voxels (the full Mesh → Voxel control set) ----

        private void DrawVoxelStage()
        {
            EditorGUILayout.LabelField("3 · Mesh → Voxels", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Voxelisation runs synchronously — the editor blocks while stage 3 runs.", EditorStyles.miniLabel);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Resolution", EditorStyles.boldLabel);
            DrawResolution();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);
            DrawShape();
            DrawTaubin();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Colour", EditorStyles.boldLabel);
            DrawColour();
        }

        private void DrawResolution()
        {
            _resolutionInput = (ResolutionInput)EditorGUILayout.EnumPopup(
                new GUIContent("Input mode",
                    "Max dim slider: set the voxel budget along the longest axis directly. World size: derive it "
                    + "from the model's intended in-game size ÷ the shared global voxel size, so every asset shares "
                    + "one voxel scale (the mode to use for a cohesive set)."),
                _resolutionInput);

            using (new EditorGUI.IndentLevelScope())
            {
                if (_resolutionInput == ResolutionInput.WorldSize)
                {
                    _voxelWorldSize = EditorGUILayout.FloatField(
                        new GUIContent("Voxel world size",
                            "Edge length of one voxel in world units, shared across every asset. Smaller = finer/"
                            + "more voxels. This is the global scale the whole set is quantised to."),
                        _voxelWorldSize);
                    _targetWorldSize = EditorGUILayout.FloatField(
                        new GUIContent("Target world size",
                            "How big this model's longest axis should be in-game, world units. Divided by the voxel "
                            + "world size to pick the voxel budget — so a bigger prop gets more voxels."),
                        _targetWorldSize);
                    EditorGUILayout.LabelField(
                        new GUIContent(" ", "The resulting voxel budget for the longest axis, after rounding and clamping to the supported 4–96 range."),
                        new GUIContent($"→ {BuildVoxSettings().ResolveMaxDimVoxels()} voxels (longest axis, clamped 4–96)"));
                }
                else
                {
                    _maxDimVoxels = EditorGUILayout.IntSlider(
                        new GUIContent("Max dimension (voxels)",
                            "Voxels along the longest bounding-box axis; the other axes scale to match. Keep it low "
                            + "(~10–16 for characters) for the chunky stylised read; the pipeline is designed to "
                            + "behave across the whole 4–96 range."),
                        _maxDimVoxels, 4, 96);
                }
            }

            Settings settings = BuildVoxSettings();
            int fineDim = settings.ResolveMaxDimVoxels() * settings.ResolveFineFactor();
            if (fineDim > FineNodeWarningDim)
            {
                EditorGUILayout.HelpBox(
                    $"Fine grid is ~{fineDim}³ nodes — the fast-winding-number occupancy pass will take tens of "
                    + "seconds. Lower the resolution or the fine factor.",
                    MessageType.Warning);
            }
        }

        private void DrawShape()
        {
            _gridSearch = EditorGUILayout.ToggleLeft(
                new GUIContent("Grid placement search",
                    "Score candidate grid phases/scales against the fine grid (face economy, IoU, air-gap preservation) and voxelise on the winner. Off = today's fixed placement."),
                _gridSearch);
            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUI.DisabledScope(!_gridSearch))
                {
                    _scaleFlex = EditorGUILayout.ToggleLeft(
                        new GUIContent("Scale flex",
                            "Let the search also stretch the voxel grid per-axis to snap the model's extent onto a "
                            + "whole voxel count (a 7.5-voxel-long bar becomes exactly 7 or 8), clamped to ±10%. "
                            + "Removes the ragged half-voxel at the end of a run. Needs the grid search on."),
                        _scaleFlex);
                }
            }

            _thinFeatureKeep = EditorGUILayout.ToggleLeft(
                new GUIContent("Thin-feature keep",
                    "Force-keep sub-voxel silhouette features (legs, ears, antennae, a mug handle) that a plain "
                    + "coverage vote would erase — but only where they connect to the model's main body, so "
                    + "disconnected specks still die. Builds the fine-grid analysis (see Fine factor)."),
                _thinFeatureKeep);

            using (new EditorGUI.DisabledScope(!_gridSearch && !_thinFeatureKeep))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _fineFactor = EditorGUILayout.IntSlider(
                        new GUIContent("Fine factor",
                            "The grid search and thin-keep first voxelise at this multiple of the target resolution "
                            + "to analyse features, then vote down. Higher = finer analysis but the fine grid grows "
                            + "as factor³ — the main cost driver (watch the fine-grid-size warning above). Only used "
                            + "when the search or thin-keep is on."),
                        _fineFactor, 2, 4);
                }
            }

            _coverage = EditorGUILayout.Slider(
                new GUIContent("Coverage threshold",
                    "Fraction of a coarse voxel's fine cells that must be solid for it to fill (unless thin-keep "
                    + "forces it). Higher trims jagged one-voxel slivers off diagonal surfaces for a boxier read; "
                    + "lower keeps more bulk."),
                _coverage, 0f, 1f);

            _removeFloaters = EditorGUILayout.ToggleLeft(
                new GUIContent("Remove floaters",
                    "Drop disconnected voxel islands whose fine support never touches the model's main connected "
                    + "component — the stray specks left by messy geometry. The largest island is always kept, so "
                    + "the model can never vanish."),
                _removeFloaters);

            _cleanupStrength = EditorGUILayout.IntSlider(
                new GUIContent("Cleanup strength",
                    "Rank morphological close→open passes: close fills lone pits/notches, open shaves lone bumps/"
                    + "spikes — flatter faces, cleaner silhouette. Corners and edges are left intact (unlike a "
                    + "classic close→open). Never shaves kept thin features, never welds real air gaps, and "
                    + "re-bridges anything it splits. 1 = one pass, 2 = stronger, 0 = off."),
                _cleanupStrength, 0, 2);

            _fillCorners = EditorGUILayout.ToggleLeft(
                new GUIContent("Fill corners",
                    "Fill concave corners/notches so the silhouette boxes out. An empty voxel fills when 3+ of its "
                    + "6 face-neighbours share one colour (fill that colour) OR it has 4+ occupied neighbours "
                    + "(fill the modal colour). The colour-consensus gate keeps colour boundaries clean — a "
                    + "3-neighbour corner where regions meet has no clear colour and stays empty, avoiding stray "
                    + "artifacts. Real air gaps (leg gaps, handle holes) are protected from the 3-neighbour fill, "
                    + "but a 4+-neighbour pocket (walled in on most sides) fills anyway — it can't be a see-through "
                    + "gap."),
                _fillCorners);
            if (_fillCorners)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _cornerFillColourTolerance = EditorGUILayout.Slider(
                        new GUIContent("Colour tolerance",
                            "How close two neighbour colours must be (Oklab distance) to count as the same for the "
                            + "3-same-colour consensus rule. 0 = exact match (too strict — near-identical shades read "
                            + "as different); raise it so similar shades group and clean corners fill. Too high and "
                            + "distinct regions merge, blurring boundaries. ~0.1 is a good start."),
                        _cornerFillColourTolerance, 0f, 0.5f);
                    _fillCornersRecursive = EditorGUILayout.ToggleLeft(
                        new GUIContent("Recursive",
                            "Repeat the fill until nothing new qualifies, so a filled corner that creates another "
                            + "qualifying corner is chased down — deeper concavities and staircases box out fully. "
                            + "More aggressive; off = a single pass. The gap guard still protects real air gaps every "
                            + "pass."),
                        _fillCornersRecursive);
                }
            }

            _symmetry = (SymmetryAxes)EditorGUILayout.EnumFlagsField(
                new GUIContent("Force symmetry",
                    "Make the model symmetric across the centre of its occupied bounds on each ticked axis. X/Y/Z "
                    + "are the grid axes (Y is up); pick whichever gives the intended left-right symmetry. Applied "
                    + "last, so the silhouette is guaranteed symmetric. Default (Force mirror off) is a union — "
                    + "keep a voxel if it or its mirror is filled, preserving both halves' features and asymmetric "
                    + "colour where geometry already exists on both sides."),
                _symmetry);
            if (_symmetry != SymmetryAxes.None)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _forceMirror = EditorGUILayout.ToggleLeft(
                        new GUIContent("Force mirror (exact)",
                            "Reflect the dominant half (the one with more voxels) onto the other, OVERRIDING what "
                            + "was there — an exact mirror in both geometry and colour, discarding the input's "
                            + "asymmetry. Use this when you want a guaranteed-clean symmetric result; leave off for "
                            + "the union that keeps features and real colour from both sides."),
                        _forceMirror);
                }
            }

            _showAdvancedWeights = EditorGUILayout.Foldout(
                _showAdvancedWeights,
                new GUIContent("Advanced: search score weights",
                    "Relative weights of the terms the grid-placement search maximises. Defaults 1 / 1 / 2 / 0 are "
                    + "tuned so merging air gaps (the fatal failure) costs most. Leave alone unless the geometry "
                    + "terms aren't separating candidates."),
                toggleOnLabelClick: true);
            if (_showAdvancedWeights)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _faceWeight = EditorGUILayout.Slider(
                        new GUIContent("Face economy (S_face)",
                            "Rewards placements with fewer exposed faces per voxel (equivalent-cube faces ÷ actual "
                            + "faces) — favours chunky, axis-aligned blocks over stair-stepped diagonals. Default 1."),
                        _faceWeight, 0f, 4f);
                    _iouWeight = EditorGUILayout.Slider(
                        new GUIContent("Shape IoU (S_iou)",
                            "Rewards overlap between the coarse voxels and the fine occupancy — keeps the blocky "
                            + "model faithful to the source silhouette. Default 1."),
                        _iouWeight, 0f, 4f);
                    _gapWeight = EditorGUILayout.Slider(
                        new GUIContent("Air-gap keep (S_gap)",
                            "Penalises covering air-gap cells (the space between a dog's four legs, a mug's handle "
                            + "hole). Weighted 2× by default because merging a gap is the worst failure mode."),
                        _gapWeight, 0f, 4f);
                    _colWeight = EditorGUILayout.Slider(
                        new GUIContent("Colour-edge align (S_col)",
                            "Rewards placements whose block boundaries land on strong source colour edges. "
                            + "Speculative and costly — it has to sample the whole fine surface's colours — so it "
                            + "ships at 0 (skipped). Raise it only during tuning if the geometry terms aren't enough."),
                        _colWeight, 0f, 4f);
                }
            }
        }

        private void DrawTaubin()
        {
            _taubinPasses = EditorGUILayout.IntSlider(
                new GUIContent("Taubin passes",
                    "λ/μ umbrella smoothing passes over the marching-cubes isosurface. Affects ONLY the smooth "
                    + "comparison mesh — the blocky voxel .vox output is built from the occupancy grid and is "
                    + "untouched by this. More passes = smoother but softer."),
                _taubinPasses, 0, 30);
            using (new EditorGUI.IndentLevelScope())
            {
                _taubinLambda = EditorGUILayout.Slider(
                    new GUIContent("λ (shrink)",
                        "The shrinking (positive) smoothing step per pass. Larger = more smoothing per pass but "
                        + "more volume loss before μ inflates it back."),
                    _taubinLambda, 0f, 1f);
                _taubinMu = EditorGUILayout.Slider(
                    new GUIContent("μ (inflate)",
                        "The inflating (negative) step that counteracts λ's shrinkage each pass. Should exceed λ "
                        + "so the mesh keeps its volume instead of collapsing."),
                    _taubinMu, 0f, 1f);
            }
            _surfaceReproject = EditorGUILayout.ToggleLeft(
                new GUIContent("SDF surface reprojection",
                    "After smoothing, nudge each vertex back onto the SDF iso=0 surface along the gradient — "
                    + "recovers detail the smoothing rounded off. Affects only the smooth comparison mesh, not the "
                    + "blocky output."),
                _surfaceReproject);
        }

        private void DrawColour()
        {
            _uvDilate = EditorGUILayout.ToggleLeft(
                new GUIContent("UV island dilation",
                    "At load, flood each UV island's colours outward into the surrounding texture gutter so a "
                    + "nearest-surface sample can't land on Meshy's purple UV-gutter bleed. Rebuilds the texture "
                    + "snapshot; the mesh itself is untouched."),
                _uvDilate);
            if (_uvDilate)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _uvDilatePasses = EditorGUILayout.IntSlider(
                        new GUIContent("Passes",
                            "How many texels of reach to flood island colour into the gutter (one 8-neighbour "
                            + "dilation pass = one texel). 8 is plenty for typical bleed; raise it for wide gutters."),
                        _uvDilatePasses, 1, 32);
                }
            }

            _multiSampleColour = EditorGUILayout.ToggleLeft(
                new GUIContent("Multi-sample voxel colour",
                    "Colour each surface voxel from the centre plus several jittered samples per exposed face, then "
                    + "take the Oklab medoid (the sample closest to all the others). A lone stray texel or AO "
                    + "speckle loses the vote instead of tinting an average. Off = one centre sample per voxel."),
                _multiSampleColour);

            _colourMode = (ColourMode)EditorGUILayout.EnumPopup(
                new GUIContent("Colour mode",
                    "Raw: the reprojected colours untouched (truest read). Per-model palette: cluster them down to "
                    + "a fixed few colours with Oklab k-means (the Crossy-Road flat-colour look). Master palette: "
                    + "snap each to the nearest swatch of a shared palette for cross-asset cohesion. Consolidated: "
                    + "keep Raw's faithful colours but merge near-identical shades into the model's fundamental "
                    + "colours (emergent count, driven by a tolerance rather than a fixed target)."),
                _colourMode);
            using (new EditorGUI.IndentLevelScope())
            {
                switch (_colourMode)
                {
                    case ColourMode.PerModelPalette:
                        _paletteSize = EditorGUILayout.IntSlider(
                            new GUIContent("Palette size",
                                "Target number of colours to cluster the model down to. Fewer = flatter, more "
                                + "stylised; empty clusters are dropped, so the actual count may come out lower."),
                            _paletteSize, 2, 32);
                        break;
                    case ColourMode.MasterPalette:
                        _masterPalette = (VoxMasterPalette?)EditorGUILayout.ObjectField(
                            new GUIContent("Master palette",
                                "The shared swatch set to snap every colour to (Oklab nearest, with a chroma-gain "
                                + "penalty so neutrals don't turn saturated). Empty = the built-in starter palette."),
                            _masterPalette, typeof(VoxMasterPalette), false);
                        if (_masterPalette == null)
                            EditorGUILayout.LabelField(" ", "Using the built-in starter palette.", EditorStyles.miniLabel);
                        break;
                    case ColourMode.Consolidated:
                        _consolidateTolerance = EditorGUILayout.Slider(
                            new GUIContent("Merge tolerance",
                                "How close two shades must be (Oklab distance) to collapse into one fundamental "
                                + "colour. Raw scatters a solid region across dozens of near-duplicate shades; this "
                                + "merges them while leaving genuinely distinct colours apart. 0 = exact (no merge, "
                                + "same as Raw); raise it to fold more variation together. ~0.05–0.08 removes texture "
                                + "noise without blurring real regions; too high and distinct colours merge."),
                            _consolidateTolerance, 0f, 0.3f);
                        _consolidateMaxColours = EditorGUILayout.IntSlider(
                            new GUIContent("Max colours",
                                "Hard cap on the output colour count — locks the model to a known number. After the "
                                + "tolerance merge, the nearest colours keep merging (frequency-weighted, so the "
                                + "dominant shades survive — not the chromatic outliers a fixed-palette k-means "
                                + "chases) until at most this many remain. Set it to the source image's palette size "
                                + "(e.g. 5 for a 5-colour reference) to reproduce it. 0 = unlimited (tolerance only). "
                                + "Fewer distinct colours than the cap yields fewer — it never invents colours."),
                            _consolidateMaxColours, 0, 32);
                        break;
                }
            }

            using (new EditorGUI.DisabledScope(_colourMode == ColourMode.Raw))
            {
                _pottsStrength = EditorGUILayout.Slider(
                    new GUIContent("Potts smoothing",
                        "Edge-aware label smoothing after palette assignment: relabels each voxel toward its "
                        + "neighbours' colour, but the penalty melts away where the source colours genuinely "
                        + "disagree — so it erases AO-speckle faux-gradients while pinning real region boundaries. "
                        + "The knob is normalised across models; 0 = off. Needs a palette (not Raw mode)."),
                    _pottsStrength, 0f, 2f);
            }

            _normalConsistency = EditorGUILayout.ToggleLeft(
                new GUIContent("Normal-consistency reject",
                    "On a thin wall the nearest triangle can be the back face, whose texels carry the interior/AO "
                    + "colour. This discards a sampled colour whose triangle faces away from the outward SDF "
                    + "gradient, falling back to the flat colour. Heuristic; off by default."),
                _normalConsistency);
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
            _settings.Vox = BuildVoxSettings();

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
                    voxelProgress: (fraction, stage) =>
                        EditorUtility.DisplayProgressBar("Mesh → VOX", $"{stage}…", fraction));

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

        // ---- Voxel settings assembly ----------------------------------------

        private Settings BuildVoxSettings() => new()
        {
            ResolutionInput = _resolutionInput,
            MaxDimVoxels = _maxDimVoxels,
            VoxelWorldSize = _voxelWorldSize,
            TargetWorldSize = _targetWorldSize,
            GridSearch = _gridSearch,
            ScaleFlex = _scaleFlex,
            ThinFeatureKeep = _thinFeatureKeep,
            FineFactor = _fineFactor,
            Coverage = _coverage,
            RemoveFloaters = _removeFloaters,
            CleanupStrength = _cleanupStrength,
            FillCorners = _fillCorners,
            FillCornersRecursive = _fillCornersRecursive,
            CornerFillColourTolerance = _cornerFillColourTolerance,
            Symmetry = _symmetry,
            ForceMirror = _forceMirror,
            FaceWeight = _faceWeight,
            IouWeight = _iouWeight,
            GapWeight = _gapWeight,
            ColWeight = _colWeight,
            UvDilate = _uvDilate,
            UvDilatePasses = _uvDilatePasses,
            MultiSampleColour = _multiSampleColour,
            PottsStrength = _pottsStrength,
            TaubinPasses = _taubinPasses,
            TaubinLambda = _taubinLambda,
            TaubinMu = _taubinMu,
            SurfaceReproject = _surfaceReproject,
            ColourMode = _colourMode,
            PaletteSize = _paletteSize,
            ConsolidateTolerance = _consolidateTolerance,
            ConsolidateMaxColours = _consolidateMaxColours,
            MasterPalette = _colourMode == ColourMode.MasterPalette
                ? ToCorePalette(_masterPalette != null ? _masterPalette.ToColor32() : DefaultMasterPalette.Colors)
                : null,
            NormalConsistency = _normalConsistency,
        };

        // The master-palette swatches come from Unity-side types (VoxMasterPalette / DefaultMasterPalette);
        // the engine-free Settings takes the core Rgba32, so convert at this boundary.
        private static Rgba32[] ToCorePalette(IReadOnlyList<Color32> colours)
        {
            var result = new Rgba32[colours.Count];
            for (int i = 0; i < colours.Count; i++)
            {
                Color32 c = colours[i];
                result[i] = new Rgba32(c.r, c.g, c.b, c.a);
            }
            return result;
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

            _resolutionInput = (ResolutionInput)EditorPrefs.GetInt(Pref + "ResolutionInput", (int)_resolutionInput);
            _maxDimVoxels = EditorPrefs.GetInt(Pref + "MaxDim", _maxDimVoxels);
            _voxelWorldSize = EditorPrefs.GetFloat(Pref + "VoxelWorldSize", _voxelWorldSize);
            _targetWorldSize = EditorPrefs.GetFloat(Pref + "TargetWorldSize", _targetWorldSize);
            _gridSearch = EditorPrefs.GetBool(Pref + "GridSearch", _gridSearch);
            _scaleFlex = EditorPrefs.GetBool(Pref + "ScaleFlex", _scaleFlex);
            _thinFeatureKeep = EditorPrefs.GetBool(Pref + "ThinFeatureKeep", _thinFeatureKeep);
            _fineFactor = EditorPrefs.GetInt(Pref + "FineFactor", _fineFactor);
            _coverage = EditorPrefs.GetFloat(Pref + "Coverage", _coverage);
            _removeFloaters = EditorPrefs.GetBool(Pref + "RemoveFloaters", _removeFloaters);
            _cleanupStrength = EditorPrefs.GetInt(Pref + "CleanupStrength", _cleanupStrength);
            _fillCorners = EditorPrefs.GetBool(Pref + "FillCorners", _fillCorners);
            _fillCornersRecursive = EditorPrefs.GetBool(Pref + "FillCornersRecursive", _fillCornersRecursive);
            _cornerFillColourTolerance = EditorPrefs.GetFloat(Pref + "CornerFillTolerance", _cornerFillColourTolerance);
            _symmetry = (SymmetryAxes)EditorPrefs.GetInt(Pref + "Symmetry", (int)_symmetry);
            _forceMirror = EditorPrefs.GetBool(Pref + "ForceMirror", _forceMirror);
            _faceWeight = EditorPrefs.GetFloat(Pref + "FaceWeight", _faceWeight);
            _iouWeight = EditorPrefs.GetFloat(Pref + "IouWeight", _iouWeight);
            _gapWeight = EditorPrefs.GetFloat(Pref + "GapWeight", _gapWeight);
            _colWeight = EditorPrefs.GetFloat(Pref + "ColWeight", _colWeight);
            _uvDilate = EditorPrefs.GetBool(Pref + "UvDilate", _uvDilate);
            _uvDilatePasses = EditorPrefs.GetInt(Pref + "UvDilatePasses", _uvDilatePasses);
            _multiSampleColour = EditorPrefs.GetBool(Pref + "MultiSample", _multiSampleColour);
            _pottsStrength = EditorPrefs.GetFloat(Pref + "PottsStrength", _pottsStrength);
            _colourMode = (ColourMode)EditorPrefs.GetInt(Pref + "ColourMode", (int)_colourMode);
            _paletteSize = EditorPrefs.GetInt(Pref + "PaletteSize", _paletteSize);
            _consolidateTolerance = EditorPrefs.GetFloat(Pref + "ConsolidateTolerance", _consolidateTolerance);
            _consolidateMaxColours = EditorPrefs.GetInt(Pref + "ConsolidateMaxColours", _consolidateMaxColours);
            _normalConsistency = EditorPrefs.GetBool(Pref + "NormalConsistency", _normalConsistency);
            _taubinPasses = EditorPrefs.GetInt(Pref + "TaubinPasses", _taubinPasses);
            _taubinLambda = EditorPrefs.GetFloat(Pref + "TaubinLambda", _taubinLambda);
            _taubinMu = EditorPrefs.GetFloat(Pref + "TaubinMu", _taubinMu);
            _surfaceReproject = EditorPrefs.GetBool(Pref + "Reproject", _surfaceReproject);

            var paletteGuid = EditorPrefs.GetString(Pref + "PaletteGuid", "");
            if (!string.IsNullOrEmpty(paletteGuid))
            {
                var path = AssetDatabase.GUIDToAssetPath(paletteGuid);
                _masterPalette = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<VoxMasterPalette>(path);
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

            EditorPrefs.SetInt(Pref + "ResolutionInput", (int)_resolutionInput);
            EditorPrefs.SetInt(Pref + "MaxDim", _maxDimVoxels);
            EditorPrefs.SetFloat(Pref + "VoxelWorldSize", _voxelWorldSize);
            EditorPrefs.SetFloat(Pref + "TargetWorldSize", _targetWorldSize);
            EditorPrefs.SetBool(Pref + "GridSearch", _gridSearch);
            EditorPrefs.SetBool(Pref + "ScaleFlex", _scaleFlex);
            EditorPrefs.SetBool(Pref + "ThinFeatureKeep", _thinFeatureKeep);
            EditorPrefs.SetInt(Pref + "FineFactor", _fineFactor);
            EditorPrefs.SetFloat(Pref + "Coverage", _coverage);
            EditorPrefs.SetBool(Pref + "RemoveFloaters", _removeFloaters);
            EditorPrefs.SetInt(Pref + "CleanupStrength", _cleanupStrength);
            EditorPrefs.SetBool(Pref + "FillCorners", _fillCorners);
            EditorPrefs.SetBool(Pref + "FillCornersRecursive", _fillCornersRecursive);
            EditorPrefs.SetFloat(Pref + "CornerFillTolerance", _cornerFillColourTolerance);
            EditorPrefs.SetInt(Pref + "Symmetry", (int)_symmetry);
            EditorPrefs.SetBool(Pref + "ForceMirror", _forceMirror);
            EditorPrefs.SetFloat(Pref + "FaceWeight", _faceWeight);
            EditorPrefs.SetFloat(Pref + "IouWeight", _iouWeight);
            EditorPrefs.SetFloat(Pref + "GapWeight", _gapWeight);
            EditorPrefs.SetFloat(Pref + "ColWeight", _colWeight);
            EditorPrefs.SetBool(Pref + "UvDilate", _uvDilate);
            EditorPrefs.SetInt(Pref + "UvDilatePasses", _uvDilatePasses);
            EditorPrefs.SetBool(Pref + "MultiSample", _multiSampleColour);
            EditorPrefs.SetFloat(Pref + "PottsStrength", _pottsStrength);
            EditorPrefs.SetInt(Pref + "ColourMode", (int)_colourMode);
            EditorPrefs.SetInt(Pref + "PaletteSize", _paletteSize);
            EditorPrefs.SetFloat(Pref + "ConsolidateTolerance", _consolidateTolerance);
            EditorPrefs.SetInt(Pref + "ConsolidateMaxColours", _consolidateMaxColours);
            EditorPrefs.SetBool(Pref + "NormalConsistency", _normalConsistency);
            EditorPrefs.SetInt(Pref + "TaubinPasses", _taubinPasses);
            EditorPrefs.SetFloat(Pref + "TaubinLambda", _taubinLambda);
            EditorPrefs.SetFloat(Pref + "TaubinMu", _taubinMu);
            EditorPrefs.SetBool(Pref + "Reproject", _surfaceReproject);

            var assetPath = _masterPalette != null ? AssetDatabase.GetAssetPath(_masterPalette) : "";
            EditorPrefs.SetString(Pref + "PaletteGuid",
                string.IsNullOrEmpty(assetPath) ? "" : AssetDatabase.AssetPathToGUID(assetPath));

            EditorPrefs.SetString(Pref + "OutputDir", _settings.OutputDir);
            EditorPrefs.SetString(Pref + "BaseName", _settings.BaseName);
            EditorPrefs.SetBool(Pref + "AutoSubfolderPerRun", _settings.AutoSubfolderPerRun);
            EditorPrefs.SetBool(Pref + "ReviewImage", _reviewImage);
            EditorPrefs.SetBool(Pref + "ReviewMesh", _reviewMesh);
        }
    }
}
