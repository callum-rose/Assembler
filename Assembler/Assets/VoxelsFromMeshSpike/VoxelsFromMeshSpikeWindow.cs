using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelsFromMeshSpike
{
    /// <summary>
    /// Spike editor window: pick a textured mesh (.obj or .fbx), choose a resolution,
    /// solid-fill it into a coloured MagicaVoxel <c>.vox</c>, then run the conservative
    /// post-processing trio (floaters → de-light → palette-snap → morphology) over the
    /// dense <see cref="VoxModel"/>. Intentionally standalone and trivially deletable.
    /// </summary>
    public sealed class VoxelsFromMeshSpikeWindow : EditorWindow
    {
        private const string DefaultPaletteAssetPath = "Assets/VoxelsFromMeshSpike/MasterPalette.asset";

        private string _objPath = "";
        private string _voxPath = "";
        private int _maxDimVoxels = 32;

        private bool _removeFloaters = true;
        private float _floaterMinPercent = 0.5f;

        private bool _deLight = true;
        private float _deLightThreshold = 0.10f;

        private bool _snapToPalette = true;
        private VoxMasterPalette? _palette;

        private bool _morphology;

        [MenuItem("Window/Voxels/Mesh → VOX (Spike)")]
        private static void Open() => GetWindow<VoxelsFromMeshSpikeWindow>("Mesh → VOX (Spike)");

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

            // Step 2 — floaters.
            _removeFloaters = EditorGUILayout.ToggleLeft(
                new GUIContent("Remove floaters",
                    "Delete small disconnected components (voxelization specks). Substantial detached parts are kept."),
                _removeFloaters);
            if (_removeFloaters)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _floaterMinPercent = EditorGUILayout.Slider(
                        new GUIContent("Min component %", "A component covering less than this % of voxels (and < 2 voxels) is removed."),
                        _floaterMinPercent, 0f, 10f);
                }
            }

            // Step 4 — de-light.
            _deLight = EditorGUILayout.ToggleLeft(
                new GUIContent("De-light",
                    "Flatten baked shading: grow material regions of similar colour and collapse each to one flat colour."),
                _deLight);
            if (_deLight)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _deLightThreshold = EditorGUILayout.Slider(
                        new GUIContent("Region similarity (Oklab)", "Max perceptual distance between adjacent voxels to join one region. Higher = larger, flatter regions."),
                        _deLightThreshold, 0f, 0.5f);
                }
            }

            // Step 5 — palette-snap.
            _snapToPalette = EditorGUILayout.ToggleLeft(
                new GUIContent("Snap to master palette",
                    "Snap each colour to the nearest swatch in a shared master palette (Oklab) for cross-asset cohesion."),
                _snapToPalette);
            if (_snapToPalette)
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
            _morphology = EditorGUILayout.ToggleLeft(
                new GUIContent("Despeckle / fill (morphology)",
                    "Mild: remove single-face bumps and fill near-enclosed pinholes. Off by default — can erode thin features."),
                _morphology);

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
                    string picked = EditorUtility.SaveFilePanel("Save VOX", Application.dataPath, startName, "vox");
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

        private void Convert()
        {
            var reporter = new EditorProgressReporter();
            try
            {
                if (!File.Exists(_objPath))
                {
                    EditorUtility.DisplayDialog("Mesh → VOX", $"Mesh not found:\n{_objPath}", "OK");
                    return;
                }

                VoxResult result = ObjToVoxConverter.Convert(_objPath, _maxDimVoxels, reporter);

                // Post-processing operates on the dense working model (canonical pipeline order:
                // floaters → de-light → palette-snap → morphology), then exports back to a VoxResult.
                EditorUtility.DisplayProgressBar("Mesh → VOX", "Post-processing…", 0.99f);
                VoxModel model = VoxModel.FromResult(result);

                if (_removeFloaters)
                {
                    FloaterRemoval.Apply(model, new FloaterRemoval.Options(2, _floaterMinPercent / 100f));
                }
                if (_deLight)
                {
                    DeLight.Apply(model, new DeLight.Options(_deLightThreshold));
                }
                if (_snapToPalette)
                {
                    PaletteSnap.Apply(model, _palette != null ? _palette.ToColor32() : DefaultMasterPalette.Colors);
                }
                if (_morphology)
                {
                    Morphology.Apply(model, Morphology.Options.Default);
                }

                result = model.ToResult();

                int colorCount = CountDistinctColors(result);

                string? dir = Path.GetDirectoryName(_voxPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                VoxWriter.Write(_voxPath, result);

                if (_voxPath.Replace('\\', '/').Contains(Application.dataPath.Replace('\\', '/')))
                {
                    AssetDatabase.Refresh();
                }

                EditorUtility.DisplayDialog(
                    "Mesh → VOX",
                    $"Wrote {result.Cells.Count:N0} voxels ({result.GridX}×{result.GridY}×{result.GridZ}), " +
                    $"{colorCount:N0} colour(s) to:\n{_voxPath}",
                    "OK");
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

        private static string DefaultVoxPath(string objPath) =>
            Path.Combine(Application.dataPath, Path.GetFileNameWithoutExtension(objPath) + ".vox");

        private static int CountDistinctColors(VoxResult result)
        {
            var seen = new HashSet<int>();
            foreach (VoxCell cell in result.Cells)
            {
                seen.Add((cell.Color.r << 16) | (cell.Color.g << 8) | cell.Color.b);
            }
            return seen.Count;
        }

        private sealed class EditorProgressReporter : IProgressReporter
        {
            public bool Report(float fraction, string message) =>
                !EditorUtility.DisplayCancelableProgressBar("Mesh → VOX", message, fraction);
        }
    }
}
