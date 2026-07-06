using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxel.Editor
{
    /// <summary>
    /// Batch-runs the pipeline over a locked test-set folder of meshes (.obj/.fbx) with one shared
    /// settings bundle — the eval harness the pipeline is judged against. Each mesh's stages appear as
    /// a row in the scene (rows stacked along +Z), the metrics land in the console as a table, and
    /// the whole readout is returned as CSV for the window's copy-to-clipboard button. Failures are
    /// logged and skipped so one bad mesh doesn't sink the run.
    /// </summary>
    public static class TestSetRunner
    {
        private const float RowGap = 1.5f;

        public sealed class Entry
        {
            public string Name { get; init; } = "";
            public VoxelMetrics Metrics { get; init; }
        }

        public sealed class BatchResult
        {
            public IReadOnlyList<Entry> Entries { get; init; } = Array.Empty<Entry>();
            public IReadOnlyList<string> Failures { get; init; } = Array.Empty<string>();
            public string Csv { get; init; } = "";
        }

        /// <summary>Run every mesh in <paramref name="folder"/> (non-recursive, sorted by name).</summary>
        public static BatchResult Run(string folder, Settings settings, float rowSpacing)
        {
            string[] meshPaths = Directory.GetFiles(folder)
                .Where(p => p.EndsWith(".obj", StringComparison.OrdinalIgnoreCase)
                    || p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (meshPaths.Length == 0)
            {
                throw new InvalidDataException($"No .obj/.fbx meshes found in:\n{folder}");
            }

            StagePreviewer.ClearPrevious();
            var parent = new GameObject("MeshToVoxel Preview");

            var entries = new List<Entry>();
            var failures = new List<string>();
            float zCursor = 0f;

            try
            {
                for (int m = 0; m < meshPaths.Length; m++)
                {
                    string path = meshPaths[m];
                    string name = Path.GetFileNameWithoutExtension(path);
                    float baseFraction = (float)m / meshPaths.Length;

                    try
                    {
                        StageResult result = Pipeline.Run(path, settings,
                            (fraction, stage) => EditorUtility.DisplayProgressBar(
                                "Mesh → Voxel test set",
                                $"{name} ({m + 1}/{meshPaths.Length}) — {stage}…",
                                baseFraction + fraction / meshPaths.Length));

                        GameObject row = StagePreviewer.BuildRow(parent.transform, result, rowSpacing);
                        row.name = name;
                        Bounds rowBounds = RowBounds(row);
                        row.transform.localPosition = new Vector3(0f, 0f, zCursor + rowBounds.extents.z - rowBounds.center.z);
                        zCursor += rowBounds.size.z + RowGap;

                        entries.Add(new Entry { Name = name, Metrics = result.Metrics });
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[MeshToVoxel] Test-set run failed for '{name}': {e.Message}");
                        Debug.LogException(e);
                        failures.Add(name);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string csv = BuildCsv(entries);
            LogTable(entries, failures, folder);
            return new BatchResult { Entries = entries, Failures = failures, Csv = csv };
        }

        public static string BuildCsv(IReadOnlyList<Entry> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine(VoxelMetrics.CsvHeader);
            foreach (Entry entry in entries)
            {
                sb.AppendLine(entry.Metrics.ToCsvRow(entry.Name));
            }
            return sb.ToString();
        }

        private static void LogTable(IReadOnlyList<Entry> entries, IReadOnlyList<string> failures, string folder)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[MeshToVoxel] Test set: {entries.Count} mesh(es) from {folder}"
                + (failures.Count > 0 ? $" ({failures.Count} failed: {string.Join(", ", failures)})" : ""));
            foreach (Entry entry in entries)
            {
                sb.AppendLine($"  {entry.Name}: {entry.Metrics.ToLogString()}");
            }
            Debug.Log(sb.ToString());
        }

        private static Bounds RowBounds(GameObject row)
        {
            Renderer[] renderers = row.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(row.transform.position, Vector3.one);
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers.Skip(1))
            {
                bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }
    }
}
