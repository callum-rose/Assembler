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
                new GUIContent("Reveal intermediates", "Lay every stage out in the scene, not just the blocky output."),
                _revealIntermediates);
            using (new EditorGUI.DisabledScope(!_revealIntermediates))
            {
                _rowSpacing = EditorGUILayout.Slider(
                    new GUIContent("Row spacing", "Gap between stages in the preview row."), _rowSpacing, 0.25f, 4f);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_converting || string.IsNullOrEmpty(_meshPath)))
            {
                if (GUILayout.Button(_converting ? "Converting…" : "Convert", GUILayout.Height(32)))
                {
                    Convert(export: false);
                }
                if (GUILayout.Button(_converting ? "Converting…" : "Convert & Save .vox…", GUILayout.Height(32)))
                {
                    Convert(export: true);
                }
            }
            using (new EditorGUI.DisabledScope(_converting))
            {
                if (GUILayout.Button("Run test set…", GUILayout.Height(24)))
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
                EditorGUILayout.LabelField("Mesh", GUILayout.Width(40));
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(_meshPath) ? "(none selected)" : _meshPath,
                    EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Browse…", GUILayout.Width(80)))
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
                new GUIContent("Input mode", "Set the voxel budget directly, or derive it from in-game size ÷ shared voxel size."),
                _resolutionInput);

            using (new EditorGUI.IndentLevelScope())
            {
                if (_resolutionInput == ResolutionInput.WorldSize)
                {
                    _voxelWorldSize = EditorGUILayout.FloatField(
                        new GUIContent("Voxel world size", "Shared global voxel edge length, world units."), _voxelWorldSize);
                    _targetWorldSize = EditorGUILayout.FloatField(
                        new GUIContent("Target world size", "Intended in-game size of the model's longest axis."), _targetWorldSize);
                    EditorGUILayout.LabelField(" ", $"→ {BuildSettings().ResolveMaxDimVoxels()} voxels (longest axis, clamped 4–96)");
                }
                else
                {
                    _maxDimVoxels = EditorGUILayout.IntSlider(
                        new GUIContent("Max dimension (voxels)", "Longest axis gets this many voxels. Keep it low for the chunky stylised read."),
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
                        new GUIContent("Scale flex", "Also snap model extents to whole voxel counts (stretch clamped ±10%)."),
                        _scaleFlex);
                }
            }

            _thinFeatureKeep = EditorGUILayout.ToggleLeft(
                new GUIContent("Thin-feature keep",
                    "Force-keep sub-voxel silhouette features (legs, ears, antennae) that are connected to the main body; disconnected specks still die."),
                _thinFeatureKeep);

            using (new EditorGUI.DisabledScope(!_gridSearch && !_thinFeatureKeep))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _fineFactor = EditorGUILayout.IntSlider(
                        new GUIContent("Fine factor", "Voxelise at this multiple of the target for the search/thin-keep analysis."),
                        _fineFactor, 2, 4);
                }
            }

            _coverage = EditorGUILayout.Slider(
                new GUIContent("Coverage threshold", "Block occupied-fraction needed to fill an output voxel (unless a thin feature forces it)."),
                _coverage, 0f, 1f);

            _removeFloaters = EditorGUILayout.ToggleLeft(
                new GUIContent("Remove floaters", "Drop voxel islands whose fine support never touches the model's main component."),
                _removeFloaters);

            _cleanupStrength = EditorGUILayout.IntSlider(
                new GUIContent("Cleanup strength",
                    "Morphological close→open radius: fills one-voxel notches and shaves lone bumps. Never erodes kept thin features, never welds real air gaps. 0 = off."),
                _cleanupStrength, 0, 2);

            _showAdvancedWeights = EditorGUILayout.Foldout(_showAdvancedWeights, "Advanced: search score weights", toggleOnLabelClick: true);
            if (_showAdvancedWeights)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _faceWeight = EditorGUILayout.Slider(new GUIContent("Face economy (S_face)"), _faceWeight, 0f, 4f);
                    _iouWeight = EditorGUILayout.Slider(new GUIContent("Shape IoU (S_iou)"), _iouWeight, 0f, 4f);
                    _gapWeight = EditorGUILayout.Slider(new GUIContent("Air-gap keep (S_gap)"), _gapWeight, 0f, 4f);
                    _colWeight = EditorGUILayout.Slider(
                        new GUIContent("Colour-edge align (S_col)", "Speculative and costly (samples the fine surface's colours); 0 = skipped."),
                        _colWeight, 0f, 4f);
                }
            }
        }

        private void DrawTaubin()
        {
            _taubinPasses = EditorGUILayout.IntSlider(
                new GUIContent("Taubin passes", "λ/μ smoothing passes over the isosurface (smooth output only)."),
                _taubinPasses, 0, 30);
            using (new EditorGUI.IndentLevelScope())
            {
                _taubinLambda = EditorGUILayout.Slider(new GUIContent("λ (shrink)"), _taubinLambda, 0f, 1f);
                _taubinMu = EditorGUILayout.Slider(new GUIContent("μ (inflate)", "Should exceed λ for volume preservation."), _taubinMu, 0f, 1f);
            }
            _surfaceReproject = EditorGUILayout.ToggleLeft(
                new GUIContent("SDF surface reprojection", "Nudge smoothed vertices back onto the iso=0 surface (smooth output only)."),
                _surfaceReproject);
        }

        private void DrawColour()
        {
            _uvDilate = EditorGUILayout.ToggleLeft(
                new GUIContent("UV island dilation", "Flood island colours into the texture gutters at load so samples can't land on Meshy's purple bleed."),
                _uvDilate);
            if (_uvDilate)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _uvDilatePasses = EditorGUILayout.IntSlider(
                        new GUIContent("Passes", "8-neighbour dilation passes (texels of reach into the gutter)."),
                        _uvDilatePasses, 1, 32);
                }
            }

            _multiSampleColour = EditorGUILayout.ToggleLeft(
                new GUIContent("Multi-sample voxel colour", "Sample each surface voxel's exposed faces at several points and take the Oklab medoid — robust to speckle and stray texels."),
                _multiSampleColour);

            _colourMode = (ColourMode)EditorGUILayout.EnumPopup(
                new GUIContent("Colour mode", "Raw reprojected, per-model palette (k-means), or master-palette snap."),
                _colourMode);
            using (new EditorGUI.IndentLevelScope())
            {
                switch (_colourMode)
                {
                    case ColourMode.PerModelPalette:
                        _paletteSize = EditorGUILayout.IntSlider(
                            new GUIContent("Palette size", "Number of colours to cluster the model down to."), _paletteSize, 2, 32);
                        break;
                    case ColourMode.MasterPalette:
                        _masterPalette = (VoxMasterPalette?)EditorGUILayout.ObjectField(
                            new GUIContent("Master palette", "Shared swatches to snap to. Empty = built-in starter palette."),
                            _masterPalette, typeof(VoxMasterPalette), false);
                        break;
                }
            }

            using (new EditorGUI.DisabledScope(_colourMode == ColourMode.Raw))
            {
                _pottsStrength = EditorGUILayout.Slider(
                    new GUIContent("Potts smoothing", "Edge-aware label smoothing after palette assignment: kills AO speckle, pins real colour boundaries. 0 = off."),
                    _pottsStrength, 0f, 2f);
            }

            _normalConsistency = EditorGUILayout.ToggleLeft(
                new GUIContent("Normal-consistency reject", "Discard wrong-side thin-wall texel hits during reprojection (heuristic; off by default)."),
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
                if (GUILayout.Button("Copy CSV"))
                {
                    EditorGUIUtility.systemCopyBuffer = _lastCsv;
                    ShowNotification(new GUIContent("Metrics CSV copied"));
                }
                if (GUILayout.Button("Log metrics"))
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
