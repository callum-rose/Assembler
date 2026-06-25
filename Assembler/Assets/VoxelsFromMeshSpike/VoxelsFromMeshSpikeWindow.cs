using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelsFromMeshSpike
{
    /// <summary>
    /// Spike editor window: pick a textured OBJ, choose a resolution, solid-fill it
    /// into a coloured MagicaVoxel <c>.vox</c> for import by the Voxel Toolkit.
    /// Intentionally standalone and trivially deletable.
    /// </summary>
    public sealed class VoxelsFromMeshSpikeWindow : EditorWindow
    {
        private string _objPath = "";
        private string _voxPath = "";
        private int _maxDimVoxels = 32;

        private bool _quantise = true;
        private int _maxColors = 16;
        private float _similarity = 0.12f;
        private float _minRegionPercent = 1.0f;

        [MenuItem("Window/Voxels/OBJ → VOX (Spike)")]
        private static void Open() => GetWindow<VoxelsFromMeshSpikeWindow>("OBJ → VOX (Spike)");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("OBJ → VOX (Spike)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Solid-fills a textured OBJ into a coloured MagicaVoxel .vox using a " +
                "fast-winding-number occupancy test. Standalone spike — safe to delete.",
                MessageType.Info);

            EditorGUILayout.Space();

            // OBJ source.
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("OBJ", GUILayout.Width(40));
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrEmpty(_objPath) ? "(none selected)" : _objPath,
                    EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Browse…", GUILayout.Width(80)))
                {
                    string picked = EditorUtility.OpenFilePanel("Select OBJ", "", "obj");
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

            // Colour quantisation.
            _quantise = EditorGUILayout.ToggleLeft(
                new GUIContent("Quantise colours",
                    "Extract the model's basic colours, snap noisy colours to the nearest one, " +
                    "and turn soft gradients at boundaries into hard colour steps."),
                _quantise);
            if (_quantise)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    _maxColors = EditorGUILayout.IntSlider(
                        new GUIContent("Max basic colours", "Upper bound on the extracted palette size."),
                        _maxColors, 1, 64);
                    _similarity = EditorGUILayout.Slider(
                        new GUIContent("Merge similarity", "Colours closer than this (0..1) are merged into one basic colour. Higher = fewer, blockier colours."),
                        _similarity, 0f, 0.5f);
                    _minRegionPercent = EditorGUILayout.Slider(
                        new GUIContent("Min region %", "A colour must cover at least this % of voxels to count as basic — filters thin gradient/noise bands."),
                        _minRegionPercent, 0f, 10f);
                }
            }

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
                    EditorUtility.DisplayDialog("OBJ → VOX", $"OBJ not found:\n{_objPath}", "OK");
                    return;
                }

                VoxResult result = ObjToVoxConverter.Convert(_objPath, _maxDimVoxels, reporter);

                if (_quantise)
                {
                    EditorUtility.DisplayProgressBar("OBJ → VOX", "Quantising colours…", 0.99f);
                    var options = new ColorQuantizer.Options(_maxColors, _similarity, _minRegionPercent / 100f);
                    result = ColorQuantizer.Quantise(result, options);
                }

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
                    "OBJ → VOX",
                    $"Wrote {result.Cells.Count:N0} voxels ({result.GridX}×{result.GridY}×{result.GridZ}), " +
                    $"{colorCount:N0} colour(s) to:\n{_voxPath}",
                    "OK");
            }
            catch (OperationCanceledException)
            {
                EditorUtility.DisplayDialog("OBJ → VOX", "Conversion cancelled.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("OBJ → VOX", $"Conversion failed:\n{e.Message}", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
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
                !EditorUtility.DisplayCancelableProgressBar("OBJ → VOX", message, fraction);
        }
    }
}
