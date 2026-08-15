using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxel.Editor
{
    /// <summary>
    /// One-shot repair for the <c>Assets/TestModels</c> <c>.vox</c> files a text-based git shelf
    /// corrupted: restoring the shelf decoded each binary <c>.vox</c> as UTF-8, so every byte
    /// ≥ 0x80 became the replacement character <c>EF BF BD</c> — destroying the palette and most
    /// voxel positions. Only the small header/SIZE values (all &lt; 128) survived.
    ///
    /// This re-voxelises each damaged file from its untouched sibling mesh (<c>.obj</c>/<c>.fbx</c>),
    /// reusing the Mesh → Voxel window's saved <see cref="Settings"/> (read from the same
    /// <c>EditorPrefs</c> keys) and matching the original resolution via the max grid dimension
    /// still readable from the corrupt file's SIZE chunk. Overwrites in place; files that already
    /// parse as valid VOX (version 150) are skipped, so it is safe to re-run.
    ///
    /// Must run from an interactive editor (OBJ/FBX load + texture decode need the main thread).
    /// </summary>
    public static class TestModelsRevoxeliser
    {
        private const string TestModelsDir = "Assets/TestModels";
        private const string PrefPrefix = "MeshToVoxel.";

        [MenuItem("Assembler/Voxelisation/Re-voxelise corrupt TestModels")]
        public static void Run()
        {
            if (!Directory.Exists(TestModelsDir))
            {
                EditorUtility.DisplayDialog("Re-voxelise", $"Not found: {TestModelsDir}", "OK");
                return;
            }

            List<string> corrupt = Directory
                .EnumerateFiles(TestModelsDir, "*.vox", SearchOption.AllDirectories)
                .Where(IsCorruptVox)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

            if (corrupt.Count == 0)
            {
                EditorUtility.DisplayDialog("Re-voxelise", "No corrupt .vox files found — nothing to do.", "OK");
                return;
            }

            (string vox, string? mesh, int maxDim)[] jobs = corrupt
                .Select(vox => (vox, mesh: FindSiblingMesh(vox), maxDim: RecoverMaxDim(vox)))
                .ToArray();

            string[] orphans = jobs.Where(j => j.mesh is null).Select(j => Path.GetFileName(j.vox)).ToArray();
            (string vox, string? mesh, int maxDim)[] runnable = jobs.Where(j => j.mesh is not null).ToArray();

            if (!EditorUtility.DisplayDialog(
                    "Re-voxelise corrupt TestModels",
                    $"Found {corrupt.Count} corrupt .vox; {runnable.Length} have a sibling mesh and will be "
                    + $"re-voxelised (in place) at their recovered max-dim, using the Mesh → Voxel window's "
                    + $"current settings."
                    + (orphans.Length > 0 ? $"\n\nSkipping {orphans.Length} with no source mesh:\n  {string.Join("\n  ", orphans)}" : ""),
                    "Re-voxelise", "Cancel"))
            {
                return;
            }

            var log = new List<string>();
            int ok = 0, failed = 0;

            try
            {
                for (int n = 0; n < runnable.Length; n++)
                {
                    (string vox, string? mesh, int maxDim) = runnable[n];
                    string name = Path.GetFileNameWithoutExtension(vox);

                    // Force MaxDimSlider mode so the per-model recovered dim actually drives resolution
                    // (WorldSize mode would ignore MaxDimVoxels).
                    Settings settings = BuildSettings(maxDim);
                    int dim = settings.MaxDimVoxels;

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Re-voxelising TestModels",
                            $"[{n + 1}/{runnable.Length}] {name}  (max-dim {dim})",
                            (float)n / runnable.Length))
                    {
                        log.Add("— cancelled by user —");
                        break;
                    }

                    try
                    {
                        StageResult result = Pipeline.Run(mesh!, settings);
                        int written = VoxExport.Write(vox, result.Occupancy, result.VoxelColours);
                        ok++;
                        log.Add($"OK    {name}: {written:N0} voxels ({result.GridX}×{result.GridY}×{result.GridZ}), max-dim {dim}");
                    }
                    catch (Exception e)
                    {
                        failed++;
                        log.Add($"FAIL  {name}: {e.Message}");
                        Debug.LogException(e);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[Re-voxelise TestModels] {ok} ok, {failed} failed, {orphans.Length} skipped (no mesh)\n"
                + string.Join("\n", log));
            EditorUtility.DisplayDialog(
                "Re-voxelise corrupt TestModels",
                $"Done: {ok} re-voxelised, {failed} failed, {orphans.Length} skipped.\n\nSee the Console for the per-model report.",
                "OK");
        }

        /// <summary>A .vox is intact iff bytes 4..7 are the little-endian version 150 (0x96,0,0,0).</summary>
        private static bool IsCorruptVox(string path)
        {
            try
            {
                using FileStream fs = File.OpenRead(path);
                Span<byte> head = stackalloc byte[8];
                if (fs.Read(head) < 8)
                {
                    return true;
                }
                bool magic = head[0] == 'V' && head[1] == 'O' && head[2] == 'X' && head[3] == ' ';
                bool version150 = head[4] == 0x96 && head[5] == 0 && head[6] == 0 && head[7] == 0;
                return !(magic && version150);
            }
            catch
            {
                return false; // unreadable → leave it alone
            }
        }

        /// <summary>
        /// Recover the model's max grid dimension from the corrupt file's SIZE chunk. The chunk tag
        /// and the three int32 dims are all small (&lt; 128), so they survived the UTF-8 mangling.
        /// Returns -1 when SIZE can't be located or a dim looks corrupted (byte ≥ 128).
        /// </summary>
        private static int RecoverMaxDim(string path)
        {
            byte[] data;
            try
            {
                data = File.ReadAllBytes(path);
            }
            catch
            {
                return -1;
            }

            int i = IndexOf(data, new byte[] { (byte)'S', (byte)'I', (byte)'Z', (byte)'E' });
            if (i < 0 || i + 24 > data.Length)
            {
                return -1;
            }

            int p = i + 12; // skip 4-byte tag + 4-byte contentSize + 4-byte childSize
            int x = BitConverter.ToInt32(data, p);
            int y = BitConverter.ToInt32(data, p + 4);
            int z = BitConverter.ToInt32(data, p + 8);

            static bool Sane(int v) => v is >= 1 and <= 256;
            if (!Sane(x) || !Sane(y) || !Sane(z))
            {
                return -1;
            }
            return Math.Max(x, Math.Max(y, z));
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j])
                {
                    j++;
                }
                if (j == needle.Length)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Prefer a same-named mesh, then any .obj, then any .fbx in the .vox's folder.</summary>
        private static string? FindSiblingMesh(string voxPath)
        {
            string dir = Path.GetDirectoryName(voxPath)!;
            string stem = Path.GetFileNameWithoutExtension(voxPath);

            string sameObj = Path.Combine(dir, stem + ".obj");
            if (File.Exists(sameObj))
            {
                return sameObj;
            }
            string sameFbx = Path.Combine(dir, stem + ".fbx");
            if (File.Exists(sameFbx))
            {
                return sameFbx;
            }

            // Fallback: any mesh in the folder. Match the extension exactly — the legacy "*.obj"
            // wildcard can also catch names like "*.obj.meta" on some runtimes.
            static bool HasExt(string p, string ext) => Path.GetExtension(p).Equals(ext, StringComparison.OrdinalIgnoreCase);
            string[] files = Directory.GetFiles(dir);
            return files.FirstOrDefault(p => HasExt(p, ".obj")) ?? files.FirstOrDefault(p => HasExt(p, ".fbx"));
        }

        /// <summary>
        /// Rebuild the <see cref="Settings"/> the Mesh → Voxel window is configured with, straight
        /// from its persisted <c>EditorPrefs</c> (same keys + field defaults as the window's
        /// <c>LoadState</c>), but pinned to <see cref="ResolutionInput.MaxDimSlider"/> at
        /// <paramref name="maxDim"/> so each model regenerates at its recovered resolution. A
        /// non-positive <paramref name="maxDim"/> falls back to the window's own max-dim.
        /// MasterPalette colour mode isn't supported here (no palette asset is loaded), so it
        /// degrades to a per-model palette.
        /// </summary>
        private static Settings BuildSettings(int maxDim)
        {
            ColourMode colourMode = (ColourMode)EditorPrefs.GetInt(PrefPrefix + "ColourMode", (int)ColourMode.PerModelPalette);
            if (colourMode == ColourMode.MasterPalette)
            {
                colourMode = ColourMode.PerModelPalette;
                Debug.LogWarning("[Re-voxelise TestModels] MasterPalette mode isn't supported by the batch; using PerModelPalette.");
            }

            return new Settings
            {
                ResolutionInput = ResolutionInput.MaxDimSlider,
                MaxDimVoxels = maxDim > 0 ? maxDim : EditorPrefs.GetInt(PrefPrefix + "MaxDim", 24),
                VoxelWorldSize = EditorPrefs.GetFloat(PrefPrefix + "VoxelWorldSize", 0.1f),
                TargetWorldSize = EditorPrefs.GetFloat(PrefPrefix + "TargetWorldSize", 2f),
                GridSearch = EditorPrefs.GetBool(PrefPrefix + "GridSearch", true),
                ScaleFlex = EditorPrefs.GetBool(PrefPrefix + "ScaleFlex", true),
                ThinFeatureKeep = EditorPrefs.GetBool(PrefPrefix + "ThinFeatureKeep", true),
                FineFactor = EditorPrefs.GetInt(PrefPrefix + "FineFactor", 3),
                Coverage = EditorPrefs.GetFloat(PrefPrefix + "Coverage", 0.5f),
                RemoveFloaters = EditorPrefs.GetBool(PrefPrefix + "RemoveFloaters", true),
                CleanupStrength = EditorPrefs.GetInt(PrefPrefix + "CleanupStrength", 1),
                FillCorners = EditorPrefs.GetBool(PrefPrefix + "FillCorners", false),
                CornerFillColourTolerance = EditorPrefs.GetFloat(PrefPrefix + "CornerFillTolerance", 0.1f),
                CornerFillNeighbourThreshold = EditorPrefs.GetInt(PrefPrefix + "CornerFillNeighbourThreshold", CornerFill.DefaultNeighbourThreshold),
                CornerFillRequireMajority = EditorPrefs.GetBool(PrefPrefix + "CornerFillRequireMajority", true),
                Symmetry = (SymmetryAxes)EditorPrefs.GetInt(PrefPrefix + "Symmetry", (int)SymmetryAxes.None),
                ForceMirror = EditorPrefs.GetBool(PrefPrefix + "ForceMirror", false),
                FaceWeight = EditorPrefs.GetFloat(PrefPrefix + "FaceWeight", 1f),
                IouWeight = EditorPrefs.GetFloat(PrefPrefix + "IouWeight", 1f),
                GapWeight = EditorPrefs.GetFloat(PrefPrefix + "GapWeight", 2f),
                ColWeight = EditorPrefs.GetFloat(PrefPrefix + "ColWeight", 0f),
                UvDilate = EditorPrefs.GetBool(PrefPrefix + "UvDilate", true),
                UvDilatePasses = EditorPrefs.GetInt(PrefPrefix + "UvDilatePasses", UvIslandDilation.DefaultPasses),
                MultiSampleColour = EditorPrefs.GetBool(PrefPrefix + "MultiSample", true),
                PottsStrength = EditorPrefs.GetFloat(PrefPrefix + "PottsStrength", 0.5f),
                TaubinPasses = EditorPrefs.GetInt(PrefPrefix + "TaubinPasses", 5),
                TaubinLambda = EditorPrefs.GetFloat(PrefPrefix + "TaubinLambda", 0.5f),
                TaubinMu = EditorPrefs.GetFloat(PrefPrefix + "TaubinMu", 0.53f),
                SurfaceReproject = EditorPrefs.GetBool(PrefPrefix + "Reproject", false),
                ColourMode = colourMode,
                PaletteSize = EditorPrefs.GetInt(PrefPrefix + "PaletteSize", 8),
                ConsolidateTolerance = EditorPrefs.GetFloat(PrefPrefix + "ConsolidateTolerance", 0.06f),
                ConsolidateMaxColours = EditorPrefs.GetInt(PrefPrefix + "ConsolidateMaxColours", 0),
                NormalConsistency = EditorPrefs.GetBool(PrefPrefix + "NormalConsistency", false),
            };
        }
    }
}
