using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// Headless entry point that renders the standard eye-placement candidate yaw views for one or
    /// more <c>.vox</c> files to PNGs on disk — the same ring of views <see cref="ModelOrientation"/>
    /// renders, but with no vision call, so it needs no API key. Invoked:
    /// <code>
    ///   Unity -batchmode -quit -nographics -projectPath &lt;project&gt;
    ///         -executeMethod Assembler.AssetGeneration.EyePlacement.EyePlacementSpikeBatch.Render
    ///         -voxPath &lt;file.vox&gt; [-voxPath &lt;file.vox&gt; ...] -outDir &lt;dir&gt;
    ///         [-imageSize 1024] [-pitch 30] [-viewCount 8]
    /// </code>
    /// Under <c>-nographics</c> <see cref="VoxelRender"/> falls back to the deterministic CPU splat.
    /// </summary>
    public static class EyePlacementSpikeBatch
    {
        public static void Render()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                List<string> voxPaths = ArgValues(args, "-voxPath");
                string outDir = ArgValues(args, "-outDir").LastOrDefault()
                    ?? throw new ArgumentException("-outDir is required");
                int imageSize = IntArg(args, "-imageSize", 1024);
                float pitch = FloatArg(args, "-pitch", 30f);
                int viewCount = IntArg(args, "-viewCount", 8);
                if (voxPaths.Count == 0)
                {
                    throw new ArgumentException("at least one -voxPath is required");
                }

                Directory.CreateDirectory(outDir);
                int written = 0;
                foreach (string voxPath in voxPaths)
                {
                    VoxelModel model = VoxReader.Read(File.ReadAllBytes(voxPath));
                    string name = Path.GetFileNameWithoutExtension(voxPath);
                    foreach (float yaw in ModelOrientation.CandidateYaws(viewCount))
                    {
                        var view = OrthographicView.FromZUpAngles(yaw, pitch);
                        var projection = new VoxelViewProjection(view, model);
                        byte[] png = VoxelRender.ToPng(model, projection, imageSize);
                        File.WriteAllBytes(Path.Combine(outDir, $"{name}_yaw{yaw:000}.png"), png);
                        written++;
                    }

                    Debug.Log($"EyePlacementSpikeBatch: rendered {name} ({model.Voxels.Count} voxels).");
                }

                Debug.Log($"EyePlacementSpikeBatch: wrote {written} PNG(s) to {outDir}.");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("EyePlacementSpikeBatch failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static List<string> ArgValues(string[] args, string flag) =>
            args.Select((arg, i) => (arg, i))
                .Where(p => string.Equals(p.arg, flag, StringComparison.OrdinalIgnoreCase) && p.i + 1 < args.Length)
                .Select(p => args[p.i + 1])
                .ToList();

        private static int IntArg(string[] args, string flag, int fallback) =>
            ArgValues(args, flag) is { Count: > 0 } values
            && int.TryParse(values[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallback;

        private static float FloatArg(string[] args, string flag, float fallback) =>
            ArgValues(args, flag) is { Count: > 0 } values
            && float.TryParse(values[^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : fallback;
    }
}
