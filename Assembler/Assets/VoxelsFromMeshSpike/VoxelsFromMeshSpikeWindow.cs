using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelsFromMeshSpike
{
    /// <summary>
    /// Spike editor window: pick a textured mesh (.obj or .fbx), choose a resolution,
    /// solid-fill it into a coloured MagicaVoxel <c>.vox</c>, then run the post-processing
    /// pipeline (<see cref="VoxPipeline"/>) over the dense <see cref="VoxModel"/>. The pipeline
    /// is driven by a category <see cref="VoxPipelinePreset"/> whose <see cref="VoxPipelineSettings"/>
    /// the per-step toggles below override. Intentionally standalone and trivially deletable.
    /// </summary>
    public sealed class VoxelsFromMeshSpikeWindow : EditorWindow
    {
        private const string DefaultPaletteAssetPath = "Assets/VoxelsFromMeshSpike/MasterPalette.asset";
        private const string PrefPrefix = "VoxelsFromMeshSpike.";

        private string _objPath = "";
        private string _voxPath = "";
        private int _maxDimVoxels = 32;

        [SerializeField] private VoxPipelinePreset _preset = VoxPipelinePreset.Creature;
        [SerializeField] private VoxPipelineSettings _settings = VoxPipelinePresets.For(VoxPipelinePreset.Creature);
        [SerializeField] private VoxMasterPalette? _palette;

        [MenuItem("Window/Voxels/Mesh → VOX (Spike)")]
        private static void Open() => GetWindow<VoxelsFromMeshSpikeWindow>("Mesh → VOX (Spike)");

        private void OnEnable() => LoadState();

        private void OnDisable() => SaveState();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Mesh → VOX (Spike)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Solid-fills a textured .obj or .fbx into a coloured MagicaVoxel .vox " +
                "using a fast-winding-number occupancy test, then cleans it up. Standalone spike — safe to delete.",
                MessageType.Info);

            EditorGUILayout.Space();

            // Mesh source.
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Mesh", GUILayout.Width(40));
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(_objPath) ? "(none selected)" : _objPath,
                    EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Browse…", GUILayout.Width(80)))
                {
                    string picked = EditorUtility.OpenFilePanel("Select mesh", "", "obj,fbx");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        _objPath = picked;
                        if (string.IsNullOrEmpty(_voxPath))
                        {
                            _voxPath = DefaultVoxPath(picked);
                        }
                    }
                }
            }

            EditorGUILayout.Space();

            // Resolution.
            _maxDimVoxels = EditorGUILayout.IntSlider(
                new GUIContent("Max dimension (voxels)", "Longest bounding-box axis gets this many voxels; the others scale proportionally."),
                _maxDimVoxels, 1, 256);
            if (_maxDimVoxels >= 96)
            {
                EditorGUILayout.HelpBox(
                    "High resolutions run millions of winding-number queries and can take a while (synchronous).",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Post-processing", EditorStyles.boldLabel);

            DrawPipelineControls();

            EditorGUILayout.Space();

            // Output.
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("VOX out", GUILayout.Width(60));
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(_voxPath) ? "(none)" : _voxPath,
                    EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Browse…", GUILayout.Width(80)))
                {
                    string startName = string.IsNullOrEmpty(_objPath)
                        ? "model.vox"
                        : Path.GetFileNameWithoutExtension(_objPath) + ".vox";
                    string startDir = string.IsNullOrEmpty(_objPath)
                        ? Application.dataPath
                        : Path.GetDirectoryName(_objPath) ?? Application.dataPath;
                    string picked = EditorUtility.SaveFilePanel("Save VOX", startDir, startName, "vox");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        _voxPath = picked;
                    }
                }
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_objPath) || string.IsNullOrEmpty(_voxPath)))
            {
                if (GUILayout.Button("Convert", GUILayout.Height(32)))
                {
                    Convert();
                }
            }
        }

        /// <summary>
        /// Preset picker + per-step overrides. Choosing a preset loads its settings; the toggles
        /// below then act as the per-asset override on top (§4.3). "Reset to preset" re-applies it.
        /// </summary>
        private void DrawPipelineControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var newPreset = (VoxPipelinePreset)EditorGUILayout.EnumPopup(
                    new GUIContent("Preset", "Category starting point. Selecting one loads its step settings, which you can then tweak below."),
                    _preset);
                if (newPreset != _preset)
                {
                    _preset = newPreset;
                    _settings = VoxPipelinePresets.For(_preset);
                }
                if (GUILayout.Button("Reset to preset", GUILayout.Width(120)))
                {
                    _settings = VoxPipelinePresets.For(_preset);
                }
            }

            EditorGUILayout.Space();

            // Step 2 — floaters.
            _settings.removeFloaters = EditorGUILayout.ToggleLeft(
                new GUIContent("Remove floaters",
                    "Delete small disconnected components (voxelization specks). Substantial detached parts are kept."),
                _settings.removeFloaters);
            if (_settings.removeFloaters)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _settings.floaterMinPercent = EditorGUILayout.Slider(
                        new GUIContent("Min component %", "A component covering less than this % of voxels (and < 2 voxels) is removed."),
                        _settings.floaterMinPercent, 0f, 10f);
                }
            }

            // Step 3 — symmetry (opt-in). Both off by default: forcing symmetry erases intentional asymmetry.
            _settings.mirror = EditorGUILayout.ToggleLeft(
                new GUIContent("Mirror (force symmetry)",
                    "Mirror one half about a plane onto the other. Off by default — erases intentional asymmetry (eyepatch, raised paw)."),
                _settings.mirror);
            if (_settings.mirror)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _settings.mirrorAxis = (SymmetryAxis)EditorGUILayout.EnumPopup(
                        new GUIContent("Mirror axis", "Axis the mirror plane is perpendicular to. Left/right (X) is the usual bilateral plane."),
                        _settings.mirrorAxis);
                    _settings.mirrorConfidence = EditorGUILayout.Slider(
                        new GUIContent("Confidence gate", "Min mirror-overlap score to auto-apply. Below this the model is treated as not symmetric and left as-is."),
                        _settings.mirrorConfidence, 0f, 1f);
                    _settings.mirrorForce = EditorGUILayout.ToggleLeft(
                        new GUIContent("Force past gate", "Apply at the best-scoring plane even when the confidence gate fails (for a stubborn asset)."),
                        _settings.mirrorForce);
                }
            }

            _settings.revolve = EditorGUILayout.ToggleLeft(
                new GUIContent("Revolve (force roundness)",
                    "Revolve the radial profile into a true solid of revolution. Off by default — for standalone wheels/cylinders only."),
                _settings.revolve);
            if (_settings.revolve)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _settings.revolveAxis = (SymmetryAxis)EditorGUILayout.EnumPopup(
                        new GUIContent("Spin axis", "Axis the profile is revolved about. Up (Y) is the usual wheel axle."),
                        _settings.revolveAxis);
                    _settings.revolveFillThreshold = EditorGUILayout.Slider(
                        new GUIContent("Ring fill threshold", "A ring is filled when at least this fraction of its cells were occupied."),
                        _settings.revolveFillThreshold, 0f, 1f);
                }
            }

            // Step 4 — de-light.
            _settings.deLight = EditorGUILayout.ToggleLeft(
                new GUIContent("De-light",
                    "Flatten baked shading: grow material regions of similar colour and collapse each to one flat colour."),
                _settings.deLight);
            if (_settings.deLight)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _settings.deLightThreshold = EditorGUILayout.Slider(
                        new GUIContent("Region similarity (Oklab)", "Max perceptual distance between adjacent voxels to join one region. Higher = larger, flatter regions."),
                        _settings.deLightThreshold, 0f, 0.5f);
                }
            }

            // Step 5 — palette-snap.
            _settings.snapToPalette = EditorGUILayout.ToggleLeft(
                new GUIContent("Snap to master palette",
                    "Snap each colour to the nearest swatch in a shared master palette (Oklab) for cross-asset cohesion."),
                _settings.snapToPalette);
            if (_settings.snapToPalette)
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
                            {
                                _palette = CreateStarterPalette();
                            }
                        }
                    }
                }
            }

            // Step 6 — morphology.
            _settings.morphology = EditorGUILayout.ToggleLeft(
                new GUIContent("Despeckle / fill (morphology)",
                    "Mild: remove single-face bumps and fill near-enclosed pinholes. Best left off for organic models — can erode thin features."),
                _settings.morphology);
        }

        private void Convert()
        {
            try
            {
                if (!File.Exists(_objPath))
                {
                    EditorUtility.DisplayDialog("Mesh → VOX", $"Mesh not found:\n{_objPath}", "OK");
                    return;
                }

                var palette = _palette != null ? _palette.ToColor32() : DefaultMasterPalette.Colors;
                VoxConversion.Summary summary = VoxConversion.Run(
                    _objPath, _voxPath, _maxDimVoxels, _settings, palette,
                    new EditorProgressReporter(),
                    (name, fraction) =>
                        EditorUtility.DisplayProgressBar("Mesh → VOX", $"Post-processing: {name}…", 0.9f + 0.09f * fraction));

                Debug.Log($"[VoxelsFromMeshSpike] Wrote {summary}");
            }
            catch (OperationCanceledException)
            {
                EditorUtility.DisplayDialog("Mesh → VOX", "Conversion cancelled.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Mesh → VOX", $"Conversion failed:\n{e.Message}", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static VoxMasterPalette CreateStarterPalette()
        {
            var palette = CreateInstance<VoxMasterPalette>();
            palette.SetColors(DefaultMasterPalette.Colors);

            string dir = Path.GetDirectoryName(DefaultPaletteAssetPath)!;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string path = AssetDatabase.GenerateUniqueAssetPath(DefaultPaletteAssetPath);
            AssetDatabase.CreateAsset(palette, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(palette);
            return palette;
        }

        // ---- EditorPrefs persistence ----------------------------------------
        // All window settings are cached so the next session reopens where you left
        // off. Saved on close/domain-reload (OnDisable), restored on OnEnable.

        private void LoadState()
        {
            _objPath = EditorPrefs.GetString(PrefPrefix + "MeshPath", _objPath);
            _voxPath = EditorPrefs.GetString(PrefPrefix + "VoxPath", _voxPath);
            _maxDimVoxels = EditorPrefs.GetInt(PrefPrefix + "MaxDim", _maxDimVoxels);
            _preset = (VoxPipelinePreset)EditorPrefs.GetInt(PrefPrefix + "Preset", (int)_preset);

            string settingsJson = EditorPrefs.GetString(PrefPrefix + "Settings", "");
            if (!string.IsNullOrEmpty(settingsJson))
            {
                JsonUtility.FromJsonOverwrite(settingsJson, _settings);
            }

            string paletteGuid = EditorPrefs.GetString(PrefPrefix + "PaletteGuid", "");
            if (!string.IsNullOrEmpty(paletteGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(paletteGuid);
                _palette = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<VoxMasterPalette>(path);
            }

            if (!string.IsNullOrEmpty(_objPath) && string.IsNullOrEmpty(_voxPath))
            {
                _voxPath = DefaultVoxPath(_objPath);
            }
        }

        private void SaveState()
        {
            EditorPrefs.SetString(PrefPrefix + "MeshPath", _objPath);
            EditorPrefs.SetString(PrefPrefix + "VoxPath", _voxPath);
            EditorPrefs.SetInt(PrefPrefix + "MaxDim", _maxDimVoxels);
            EditorPrefs.SetInt(PrefPrefix + "Preset", (int)_preset);
            EditorPrefs.SetString(PrefPrefix + "Settings", JsonUtility.ToJson(_settings));

            string assetPath = _palette != null ? AssetDatabase.GetAssetPath(_palette) : "";
            EditorPrefs.SetString(
                PrefPrefix + "PaletteGuid",
                string.IsNullOrEmpty(assetPath) ? "" : AssetDatabase.AssetPathToGUID(assetPath));
        }

        // Default the .vox next to the source mesh, matching its basename.
        private static string DefaultVoxPath(string meshPath) =>
            Path.Combine(
                Path.GetDirectoryName(meshPath) ?? Application.dataPath,
                Path.GetFileNameWithoutExtension(meshPath) + ".vox");

        private sealed class EditorProgressReporter : IProgressReporter
        {
            public bool Report(float fraction, string message) =>
                !EditorUtility.DisplayCancelableProgressBar("Mesh → VOX", message, fraction);
        }
    }
}
