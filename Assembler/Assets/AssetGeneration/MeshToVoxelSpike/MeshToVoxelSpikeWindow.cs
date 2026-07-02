using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Assembler.AssetGeneration.MeshToVoxels;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Spike window for the SDF-remesh + colour-reprojection mesh → voxel path. Pick a messy Meshy
    /// mesh (.obj/.fbx), tune the resolution / placement-search / cleanup / colour passes — every
    /// pass individually toggleable — and Convert to reveal every intermediate stage side by side in
    /// the scene, with the Crossy-Road blocky voxel model as the primary output and an objective
    /// metrics readout per run. "Run test set…" batch-runs a locked folder of meshes for the
    /// consistency eval. Isolated from the existing <c>Window > Voxels > Mesh to Voxels</c> path so
    /// the two can be A/B compared on the same asset.
    /// </summary>
    public sealed class MeshToVoxelSpikeWindow : EditorWindow
    {
        private const string PrefPrefix = "MeshToVoxelSpike.";
        private const int FineNodeWarningDim = 120;

        private string _meshPath = "";
        private string _testSetFolder = "";

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
        private bool _normalConsistency;

        private int _taubinPasses = 5;
        private float _taubinLambda = 0.5f;
        private float _taubinMu = 0.53f;
        private bool _surfaceReproject;

        private bool _revealIntermediates = true;
        private float _rowSpacing = 1f;

        private bool _converting;
        private Vector2 _scroll;

        private readonly List<SpikeTestSetRunner.Entry> _lastEntries = new();
        private string _lastCsv = "";

        [SerializeField] private VoxMasterPalette? _masterPalette;

        [MenuItem("Window/Voxels/Mesh to Voxel Spike")]
        private static void Open() => GetWindow<MeshToVoxelSpikeWindow>("Mesh → Voxel Spike");

        private void OnEnable() => LoadState();

        private void OnDisable() => SaveState();

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox(
                "Remeshes a messy .obj/.fbx via a generalized-winding-number SDF + marching cubes, votes the "
                + "occupancy onto a searched grid placement, cleans it up, then reprojects colour from the "
                + "original surface and flattens to a smoothed palette — producing a Crossy-Road blocky voxel "
                + "model plus a smooth comparison.",
                MessageType.Info);

            EditorGUILayout.Space();
            DrawMeshPicker();

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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            _revealIntermediates = EditorGUILayout.ToggleLeft(
                new GUIContent("Reveal intermediates",
                    "On: lay the full progression out along +X — original → marching-cubes isosurface → Taubin "
                    + "smoothed → (SDF reprojected) → smooth reprojected-colour → blocky voxel model — for A/B "
                    + "judgement. Off: show only the primary blocky voxel model."),
                _revealIntermediates);
            using (new EditorGUI.DisabledScope(!_revealIntermediates))
            {
                _rowSpacing = EditorGUILayout.Slider(
                    new GUIContent("Row spacing",
                        "Multiplier on the gap between stages in the preview row (scaled by each mesh's size). "
                        + "Raise it if adjacent stages overlap. Only applies when intermediates are revealed."),
                    _rowSpacing, 0.25f, 4f);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_converting || string.IsNullOrEmpty(_meshPath)))
            {
                if (GUILayout.Button(
                    new GUIContent(_converting ? "Converting…" : "Convert",
                        "Run the pipeline on the selected mesh with the settings above and show the result in the "
                        + "scene. Runs synchronously on the main thread — the editor blocks until it finishes."),
                    GUILayout.Height(32)))
                {
                    Convert(export: false);
                }
                if (GUILayout.Button(
                    new GUIContent(_converting ? "Converting…" : "Convert & Save .vox…",
                        "Run the pipeline and additionally write the blocky occupancy grid out as a MagicaVoxel "
                        + ".vox at a path you pick. Asks for the destination before the (slow) run."),
                    GUILayout.Height(32)))
                {
                    Convert(export: true);
                }
            }
            using (new EditorGUI.DisabledScope(_converting))
            {
                if (GUILayout.Button(
                    new GUIContent("Run test set…",
                        "Batch-run the pipeline over every .obj/.fbx in a folder you pick (non-recursive), with "
                        + "these same settings. Stacks one preview row per mesh and fills the metrics panel + CSV "
                        + "for the consistency eval. Failures are logged and skipped."),
                    GUILayout.Height(24)))
                {
                    RunTestSet();
                }
            }

            DrawMetrics();
            EditorGUILayout.EndScrollView();
        }

        private void DrawMeshPicker()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Mesh", "The source .obj/.fbx to voxelise — typically a messy textured Meshy export."),
                    GUILayout.Width(40));
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(_meshPath) ? "(none selected)" : _meshPath,
                    EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button(
                    new GUIContent("Browse…", "Pick the source mesh (.obj or .fbx) from disk. The choice is remembered between sessions."),
                    GUILayout.Width(80)))
                {
                    string picked = EditorUtility.OpenFilePanel("Select mesh", "", "obj,fbx");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        _meshPath = picked;
                    }
                }
            }
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
                        new GUIContent($"→ {BuildSettings().ResolveMaxDimVoxels()} voxels (longest axis, clamped 4–96)"));
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

            SpikeSettings settings = BuildSettings();
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
                    + "comparison mesh — the blocky voxel output is built from the occupancy grid and is untouched "
                    + "by this. More passes = smoother but softer."),
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
                    + "a few colours with Oklab k-means (the Crossy-Road flat-colour look). Master palette: snap "
                    + "each to the nearest swatch of a shared palette for cross-asset cohesion."),
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

        private void DrawMetrics()
        {
            if (_lastEntries.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Metrics", EditorStyles.boldLabel);
            foreach (SpikeTestSetRunner.Entry entry in _lastEntries)
            {
                EditorGUILayout.LabelField(entry.Name, EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(entry.Metrics.ToLogString(), EditorStyles.wordWrappedMiniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                    new GUIContent("Copy CSV",
                        "Copy the metrics for the last run / test set to the clipboard as CSV (one row per mesh, "
                        + "with a header) — paste it into a spreadsheet to compare runs.")))
                {
                    EditorGUIUtility.systemCopyBuffer = _lastCsv;
                    ShowNotification(new GUIContent("Metrics CSV copied"));
                }
                if (GUILayout.Button(
                    new GUIContent("Log metrics", "Print each mesh's metrics line to the Console.")))
                {
                    foreach (SpikeTestSetRunner.Entry entry in _lastEntries)
                    {
                        Debug.Log($"[MeshToVoxelSpike] {entry.Name}: {entry.Metrics.ToLogString()}");
                    }
                }
            }
        }

        // async void: a UI event handler that can't return a Task. The whole body is wrapped in
        // try/catch (house style) so an exception can't escape unhandled. When export is true the run
        // additionally writes the blocky occupancy grid out as a .vox at a user-picked path.
        private async void Convert(bool export)
        {
            if (_converting)
            {
                return;
            }

            try
            {
                if (!File.Exists(_meshPath))
                {
                    EditorUtility.DisplayDialog("Mesh → Voxel Spike", $"Mesh not found:\n{_meshPath}", "OK");
                    return;
                }

                // Ask for the destination before the slow run so a cancelled save panel costs nothing.
                string voxPath = "";
                if (export)
                {
                    voxPath = EditorUtility.SaveFilePanel(
                        "Save .vox", Path.GetDirectoryName(_meshPath) ?? "",
                        Path.GetFileNameWithoutExtension(_meshPath), "vox");
                    if (string.IsNullOrEmpty(voxPath))
                    {
                        return;
                    }
                }

                _converting = true;
                Repaint();
                // Let the "Converting…" button state paint before the synchronous run blocks the thread.
                await Task.Yield();

                SpikeSettings settings = BuildSettings();
                SpikeStageResult result = SpikePipeline.Run(
                    _meshPath, settings,
                    (fraction, stage) => EditorUtility.DisplayProgressBar("Mesh → Voxel Spike", stage + "…", fraction));

                if (_revealIntermediates)
                {
                    SpikeStagePreviewer.Show(result, _rowSpacing);
                }
                else
                {
                    SpikeStagePreviewer.ShowBlockyOnly(result);
                }

                string name = Path.GetFileNameWithoutExtension(_meshPath);
                _lastEntries.Clear();
                _lastEntries.Add(new SpikeTestSetRunner.Entry { Name = name, Metrics = result.Metrics });
                _lastCsv = SpikeTestSetRunner.BuildCsv(_lastEntries);
                Debug.Log($"[MeshToVoxelSpike] {name}: {result.Metrics.ToLogString()}");

                if (export)
                {
                    int written = SpikeVoxExport.Write(voxPath, result.Occupancy, result.VoxelColours);
                    RefreshIfInsideProject(voxPath);
                    EditorUtility.DisplayDialog(
                        "Mesh → Voxel Spike",
                        $"Wrote {written:N0} voxels ({result.GridX}×{result.GridY}×{result.GridZ}) to:\n{voxPath}", "OK");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Mesh → Voxel Spike", $"Conversion failed:\n{e.Message}", "OK");
            }
            finally
            {
                _converting = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        // async void event handler; body wrapped in try/catch per house style.
        private async void RunTestSet()
        {
            if (_converting)
            {
                return;
            }

            try
            {
                string folder = EditorUtility.OpenFolderPanel(
                    "Select test-set folder",
                    string.IsNullOrEmpty(_testSetFolder) ? "" : _testSetFolder, "");
                if (string.IsNullOrEmpty(folder))
                {
                    return;
                }
                _testSetFolder = folder;

                _converting = true;
                Repaint();
                await Task.Yield();

                SpikeTestSetRunner.BatchResult batch = SpikeTestSetRunner.Run(folder, BuildSettings(), _rowSpacing);
                _lastEntries.Clear();
                _lastEntries.AddRange(batch.Entries);
                _lastCsv = batch.Csv;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Mesh → Voxel Spike", $"Test-set run failed:\n{e.Message}", "OK");
            }
            finally
            {
                _converting = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        // Surface a freshly-written .vox in the Project window when it lands inside Assets/.
        private static void RefreshIfInsideProject(string voxPath)
        {
            string full = Path.GetFullPath(voxPath).Replace('\\', '/');
            if (full.StartsWith(Application.dataPath.Replace('\\', '/')))
            {
                AssetDatabase.Refresh();
            }
        }

        private SpikeSettings BuildSettings() => new()
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
            MasterPalette = _colourMode == ColourMode.MasterPalette
                ? (_masterPalette != null ? _masterPalette.ToColor32() : DefaultMasterPalette.Colors)
                : null,
            NormalConsistency = _normalConsistency,
        };

        // ---- EditorPrefs persistence ----------------------------------------

        private void LoadState()
        {
            _meshPath = EditorPrefs.GetString(PrefPrefix + "MeshPath", _meshPath);
            _testSetFolder = EditorPrefs.GetString(PrefPrefix + "TestSetFolder", _testSetFolder);
            _resolutionInput = (ResolutionInput)EditorPrefs.GetInt(PrefPrefix + "ResolutionInput", (int)_resolutionInput);
            _maxDimVoxels = EditorPrefs.GetInt(PrefPrefix + "MaxDim", _maxDimVoxels);
            _voxelWorldSize = EditorPrefs.GetFloat(PrefPrefix + "VoxelWorldSize", _voxelWorldSize);
            _targetWorldSize = EditorPrefs.GetFloat(PrefPrefix + "TargetWorldSize", _targetWorldSize);
            _gridSearch = EditorPrefs.GetBool(PrefPrefix + "GridSearch", _gridSearch);
            _scaleFlex = EditorPrefs.GetBool(PrefPrefix + "ScaleFlex", _scaleFlex);
            _thinFeatureKeep = EditorPrefs.GetBool(PrefPrefix + "ThinFeatureKeep", _thinFeatureKeep);
            _fineFactor = EditorPrefs.GetInt(PrefPrefix + "FineFactor", _fineFactor);
            _coverage = EditorPrefs.GetFloat(PrefPrefix + "Coverage", _coverage);
            _removeFloaters = EditorPrefs.GetBool(PrefPrefix + "RemoveFloaters", _removeFloaters);
            _cleanupStrength = EditorPrefs.GetInt(PrefPrefix + "CleanupStrength", _cleanupStrength);
            _faceWeight = EditorPrefs.GetFloat(PrefPrefix + "FaceWeight", _faceWeight);
            _iouWeight = EditorPrefs.GetFloat(PrefPrefix + "IouWeight", _iouWeight);
            _gapWeight = EditorPrefs.GetFloat(PrefPrefix + "GapWeight", _gapWeight);
            _colWeight = EditorPrefs.GetFloat(PrefPrefix + "ColWeight", _colWeight);
            _uvDilate = EditorPrefs.GetBool(PrefPrefix + "UvDilate", _uvDilate);
            _uvDilatePasses = EditorPrefs.GetInt(PrefPrefix + "UvDilatePasses", _uvDilatePasses);
            _multiSampleColour = EditorPrefs.GetBool(PrefPrefix + "MultiSample", _multiSampleColour);
            _pottsStrength = EditorPrefs.GetFloat(PrefPrefix + "PottsStrength", _pottsStrength);
            _taubinPasses = EditorPrefs.GetInt(PrefPrefix + "TaubinPasses", _taubinPasses);
            _taubinLambda = EditorPrefs.GetFloat(PrefPrefix + "TaubinLambda", _taubinLambda);
            _taubinMu = EditorPrefs.GetFloat(PrefPrefix + "TaubinMu", _taubinMu);
            _surfaceReproject = EditorPrefs.GetBool(PrefPrefix + "Reproject", _surfaceReproject);
            _colourMode = (ColourMode)EditorPrefs.GetInt(PrefPrefix + "ColourMode", (int)_colourMode);
            _paletteSize = EditorPrefs.GetInt(PrefPrefix + "PaletteSize", _paletteSize);
            _normalConsistency = EditorPrefs.GetBool(PrefPrefix + "NormalConsistency", _normalConsistency);
            _revealIntermediates = EditorPrefs.GetBool(PrefPrefix + "Reveal", _revealIntermediates);
            _rowSpacing = EditorPrefs.GetFloat(PrefPrefix + "RowSpacing", _rowSpacing);

            string guid = EditorPrefs.GetString(PrefPrefix + "PaletteGuid", "");
            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                _masterPalette = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<VoxMasterPalette>(path);
            }
        }

        private void SaveState()
        {
            EditorPrefs.SetString(PrefPrefix + "MeshPath", _meshPath);
            EditorPrefs.SetString(PrefPrefix + "TestSetFolder", _testSetFolder);
            EditorPrefs.SetInt(PrefPrefix + "ResolutionInput", (int)_resolutionInput);
            EditorPrefs.SetInt(PrefPrefix + "MaxDim", _maxDimVoxels);
            EditorPrefs.SetFloat(PrefPrefix + "VoxelWorldSize", _voxelWorldSize);
            EditorPrefs.SetFloat(PrefPrefix + "TargetWorldSize", _targetWorldSize);
            EditorPrefs.SetBool(PrefPrefix + "GridSearch", _gridSearch);
            EditorPrefs.SetBool(PrefPrefix + "ScaleFlex", _scaleFlex);
            EditorPrefs.SetBool(PrefPrefix + "ThinFeatureKeep", _thinFeatureKeep);
            EditorPrefs.SetInt(PrefPrefix + "FineFactor", _fineFactor);
            EditorPrefs.SetFloat(PrefPrefix + "Coverage", _coverage);
            EditorPrefs.SetBool(PrefPrefix + "RemoveFloaters", _removeFloaters);
            EditorPrefs.SetInt(PrefPrefix + "CleanupStrength", _cleanupStrength);
            EditorPrefs.SetFloat(PrefPrefix + "FaceWeight", _faceWeight);
            EditorPrefs.SetFloat(PrefPrefix + "IouWeight", _iouWeight);
            EditorPrefs.SetFloat(PrefPrefix + "GapWeight", _gapWeight);
            EditorPrefs.SetFloat(PrefPrefix + "ColWeight", _colWeight);
            EditorPrefs.SetBool(PrefPrefix + "UvDilate", _uvDilate);
            EditorPrefs.SetInt(PrefPrefix + "UvDilatePasses", _uvDilatePasses);
            EditorPrefs.SetBool(PrefPrefix + "MultiSample", _multiSampleColour);
            EditorPrefs.SetFloat(PrefPrefix + "PottsStrength", _pottsStrength);
            EditorPrefs.SetInt(PrefPrefix + "TaubinPasses", _taubinPasses);
            EditorPrefs.SetFloat(PrefPrefix + "TaubinLambda", _taubinLambda);
            EditorPrefs.SetFloat(PrefPrefix + "TaubinMu", _taubinMu);
            EditorPrefs.SetBool(PrefPrefix + "Reproject", _surfaceReproject);
            EditorPrefs.SetInt(PrefPrefix + "ColourMode", (int)_colourMode);
            EditorPrefs.SetInt(PrefPrefix + "PaletteSize", _paletteSize);
            EditorPrefs.SetBool(PrefPrefix + "NormalConsistency", _normalConsistency);
            EditorPrefs.SetBool(PrefPrefix + "Reveal", _revealIntermediates);
            EditorPrefs.SetFloat(PrefPrefix + "RowSpacing", _rowSpacing);

            string assetPath = _masterPalette != null ? AssetDatabase.GetAssetPath(_masterPalette) : "";
            EditorPrefs.SetString(
                PrefPrefix + "PaletteGuid",
                string.IsNullOrEmpty(assetPath) ? "" : AssetDatabase.AssetPathToGUID(assetPath));
        }
    }
}
