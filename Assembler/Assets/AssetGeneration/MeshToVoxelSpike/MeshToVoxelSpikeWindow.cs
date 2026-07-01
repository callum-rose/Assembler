using System;
using System.IO;
using System.Threading.Tasks;
using Assembler.AssetGeneration.MeshToVoxels;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Spike window for the SDF-remesh + colour-reprojection mesh → voxel path. Pick a messy Meshy
    /// mesh (.obj/.fbx), tune the coarse voxel budget and colour mode, and Convert to reveal every
    /// intermediate stage side by side in the scene — with the Crossy-Road blocky voxel model as the
    /// primary output. Isolated from the existing <c>Window > Voxels > Mesh to Voxels</c> path so the
    /// two can be A/B compared on the same asset.
    /// </summary>
    public sealed class MeshToVoxelSpikeWindow : EditorWindow
    {
        private const string PrefPrefix = "MeshToVoxelSpike.";

        private string _meshPath = "";
        private int _maxDimVoxels = 24;

        private bool _featureAware;
        private int _featureFactor = 2;
        private float _featureCoverage = 0.5f;

        private int _taubinPasses = 5;
        private float _taubinLambda = 0.5f;
        private float _taubinMu = 0.53f;
        private bool _surfaceReproject;

        private ColourMode _colourMode = ColourMode.PerModelPalette;
        private int _paletteSize = 8;
        private bool _normalConsistency;

        private bool _revealIntermediates = true;
        private float _rowSpacing = 1f;

        private bool _converting;

        [SerializeField] private VoxMasterPalette? _masterPalette;

        [MenuItem("Window/Voxels/Mesh to Voxel Spike")]
        private static void Open() => GetWindow<MeshToVoxelSpikeWindow>("Mesh → Voxel Spike");

        private void OnEnable() => LoadState();

        private void OnDisable() => SaveState();

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Remeshes a messy .obj/.fbx via a generalized-winding-number SDF + marching cubes, then " +
                "reprojects colour from the original surface and flattens to a palette — producing a " +
                "Crossy-Road blocky voxel model plus a smooth comparison.",
                MessageType.Info);

            EditorGUILayout.Space();
            DrawMeshPicker();

            EditorGUILayout.Space();
            _maxDimVoxels = EditorGUILayout.IntSlider(
                new GUIContent("Max dimension (voxels)", "Longest axis gets this many voxels. Keep it low for the chunky stylised read."),
                _maxDimVoxels, 4, 96);
            if (_maxDimVoxels >= 64)
            {
                EditorGUILayout.HelpBox("High resolutions run synchronously and can take a while.", MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);
            DrawFeatureAware();
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
                    Convert();
                }
            }
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

        private void DrawFeatureAware()
        {
            _featureAware = EditorGUILayout.ToggleLeft(
                new GUIContent("Feature-aware downsample",
                    "Voxelise finer then downres to the target, force-keeping thin silhouette features (legs, ears, antennae) a plain coverage vote would erase."),
                _featureAware);
            if (_featureAware)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _featureFactor = EditorGUILayout.IntSlider(
                        new GUIContent("Factor", "Voxelise at this multiple of the target, then collapse each factor³ block."),
                        _featureFactor, 2, 4);
                    _featureCoverage = EditorGUILayout.Slider(
                        new GUIContent("Coverage threshold", "Block occupied-fraction needed to fill an output voxel (unless a thin feature forces it)."),
                        _featureCoverage, 0f, 1f);
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
            _normalConsistency = EditorGUILayout.ToggleLeft(
                new GUIContent("Normal-consistency reject", "Discard wrong-side thin-wall texel hits during reprojection (heuristic; off by default)."),
                _normalConsistency);
        }

        // async void: a UI event handler that can't return a Task. The whole body is wrapped in
        // try/catch (house style) so an exception can't escape unhandled.
        private async void Convert()
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

        private SpikeSettings BuildSettings() => new()
        {
            MaxDimVoxels = _maxDimVoxels,
            FeatureAware = _featureAware,
            FeatureFactor = _featureFactor,
            FeatureCoverage = _featureCoverage,
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
            _maxDimVoxels = EditorPrefs.GetInt(PrefPrefix + "MaxDim", _maxDimVoxels);
            _featureAware = EditorPrefs.GetBool(PrefPrefix + "FeatureAware", _featureAware);
            _featureFactor = EditorPrefs.GetInt(PrefPrefix + "FeatureFactor", _featureFactor);
            _featureCoverage = EditorPrefs.GetFloat(PrefPrefix + "FeatureCoverage", _featureCoverage);
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
            EditorPrefs.SetInt(PrefPrefix + "MaxDim", _maxDimVoxels);
            EditorPrefs.SetBool(PrefPrefix + "FeatureAware", _featureAware);
            EditorPrefs.SetInt(PrefPrefix + "FeatureFactor", _featureFactor);
            EditorPrefs.SetFloat(PrefPrefix + "FeatureCoverage", _featureCoverage);
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
