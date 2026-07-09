using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// Headless entry points for the eye-placement spikes. Two modes, both offline apart from the
    /// render (no vision call, so no API key):
    /// <list type="bullet">
    /// <item><see cref="Render"/> — renders the standard eye-placement candidate yaw ring for one or
    /// more <c>.vox</c> files to PNGs, and writes a <c>&lt;name&gt;_meta.json</c> sidecar per model
    /// carrying the projection metadata (per-yaw <c>worldPerNorm</c> etc.) a harness needs to convert
    /// a normalised pick distance into voxels. Run <b>without</b> <c>-nographics</c> for the crisp
    /// URP/GPU camera render; under <c>-nographics</c> <see cref="VoxelRender"/> falls back to the CPU
    /// splat.</item>
    /// <item><see cref="Anchors"/> — takes a <c>.vox</c>, a yaw and one or more 2D picks and runs the
    /// production reprojection (<see cref="EyeReprojection.BuildAnchors"/>) so the end-to-end back-half
    /// can be verified with no GPU: it prints the resolved 3D anchors as JSON.</item>
    /// </list>
    /// <code>
    ///   Unity -batchmode -quit -projectPath &lt;project&gt;
    ///         -executeMethod Assembler.AssetGeneration.EyePlacement.EyePlacementSpikeBatch.Render
    ///         -voxPath &lt;file.vox&gt; [-voxPath ...] -outDir &lt;dir&gt; [-imageSize 1024] [-pitch 30] [-viewCount 8]
    ///
    ///   Unity -batchmode -quit -nographics -projectPath &lt;project&gt;
    ///         -executeMethod Assembler.AssetGeneration.EyePlacement.EyePlacementSpikeBatch.Anchors
    ///         -voxPath &lt;file.vox&gt; -yaw 270 [-pitch 30] [-imageSize 1024]
    ///         -pick "0.48,0.34" [-pick "0.53,0.34" ...] -out &lt;result.json&gt;
    /// </code>
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

                    var yawMeta = new List<string>();
                    foreach (float yaw in ModelOrientation.CandidateYaws(viewCount))
                    {
                        var view = OrthographicView.FromZUpAngles(yaw, pitch);
                        var projection = new VoxelViewProjection(view, model);
                        byte[] png = VoxelRender.ToPng(model, projection, imageSize);
                        File.WriteAllBytes(Path.Combine(outDir, $"{name}_yaw{yaw:000}.png"), png);
                        written++;

                        // worldPerNorm is the world span mapped across the [0,1] image edge, so a
                        // normalised pick distance d converts to voxels as d * worldPerNorm.
                        float worldPerNorm = projection.OrthographicSize * 2f;
                        yawMeta.Add(
                            $"    {{ \"yaw\": {F(yaw)}, \"worldPerNorm\": {F(worldPerNorm)}, " +
                            $"\"orthoSize\": {F(projection.OrthographicSize)}, " +
                            $"\"voxelPixelSize\": {F(projection.VoxelPixelSize(imageSize))} }}");
                    }

                    File.WriteAllText(Path.Combine(outDir, $"{name}_meta.json"), ModelMetaJson(
                        name, model, imageSize, pitch, yawMeta));

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

        /// <summary>
        /// Reprojects 2D eye picks on a chosen yaw view into 3D anchors through the production
        /// <see cref="EyeReprojection.BuildAnchors"/>, using the default <see cref="EyePlacementOptions"/>
        /// (two symmetric eyes, snap radius 2). Prints/writes the anchors as JSON so a spike harness can
        /// confirm the geometry back-half lands picks on the right voxels — e.g. that a cow's eyes reach
        /// the face rather than the top ear row — with no GPU and no vision call.
        /// </summary>
        public static void Anchors()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                string voxPath = ArgValues(args, "-voxPath").LastOrDefault()
                    ?? throw new ArgumentException("-voxPath is required");
                float yaw = FloatArg(args, "-yaw", 270f);
                float pitch = FloatArg(args, "-pitch", 30f);
                int imageSize = IntArg(args, "-imageSize", 1024);
                string? outPath = ArgValues(args, "-out").LastOrDefault();

                var picks = ArgValues(args, "-pick").Select(ParsePick).ToList();
                if (picks.Count == 0)
                {
                    throw new ArgumentException("at least one -pick \"u,v\" is required");
                }

                VoxelModel model = VoxReader.Read(File.ReadAllBytes(voxPath));
                var view = OrthographicView.FromZUpAngles(yaw, pitch);
                var projection = new VoxelViewProjection(view, model);
                var options = new EyePlacementOptions { View = view, AutoOrient = false, ImageSize = imageSize };

                IReadOnlyList<EyeAnchor> anchors = EyeReprojection.BuildAnchors(model, projection, options, picks);
                string json = AnchorsJson(Path.GetFileNameWithoutExtension(voxPath), model, yaw, pitch, picks, anchors);

                if (!string.IsNullOrWhiteSpace(outPath))
                {
                    File.WriteAllText(outPath, json);
                }

                Debug.Log("EyePlacementSpikeBatch.Anchors result:\n" + json);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("EyePlacementSpikeBatch.Anchors failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static Vector2 ParsePick(string raw)
        {
            float[] parts = raw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture))
                .ToArray();
            if (parts.Length < 2)
            {
                throw new ArgumentException($"pick \"{raw}\" must be \"u,v\"");
            }

            return new Vector2(Mathf.Clamp01(parts[0]), Mathf.Clamp01(parts[1]));
        }

        private static string ModelMetaJson(
            string name, VoxelModel model, int imageSize, float pitch, IReadOnlyList<string> yawMeta)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"name\": \"{name}\",\n");
            sb.Append($"  \"imageSize\": {imageSize},\n");
            sb.Append($"  \"pitch\": {F(pitch)},\n");
            sb.Append($"  \"voxelCount\": {model.Voxels.Count},\n");
            sb.Append($"  \"min\": [{model.Min.x}, {model.Min.y}, {model.Min.z}],\n");
            sb.Append($"  \"max\": [{model.Max.x}, {model.Max.y}, {model.Max.z}],\n");
            sb.Append($"  \"size\": [{model.Size.x}, {model.Size.y}, {model.Size.z}],\n");
            sb.Append("  \"yaws\": [\n");
            sb.Append(string.Join(",\n", yawMeta));
            sb.Append("\n  ]\n}\n");
            return sb.ToString();
        }

        private static string AnchorsJson(
            string name, VoxelModel model, float yaw, float pitch,
            IReadOnlyList<Vector2> picks, IReadOnlyList<EyeAnchor> anchors)
        {
            string pickJson = string.Join(", ", picks.Select(p => $"[{F(p.x)}, {F(p.y)}]"));
            string anchorJson = string.Join(",\n", anchors.Select(a =>
                $"    {{ \"position\": [{F(a.Position.x)}, {F(a.Position.y)}, {F(a.Position.z)}], " +
                $"\"normal\": [{F(a.Normal.x)}, {F(a.Normal.y)}, {F(a.Normal.z)}] }}"));

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"name\": \"{name}\",\n");
            sb.Append($"  \"yaw\": {F(yaw)},\n");
            sb.Append($"  \"pitch\": {F(pitch)},\n");
            sb.Append($"  \"modelMaxZ\": {model.Max.z},\n");
            sb.Append($"  \"modelMinZ\": {model.Min.z},\n");
            sb.Append($"  \"picks\": [{pickJson}],\n");
            sb.Append("  \"anchors\": [\n");
            sb.Append(anchorJson);
            sb.Append("\n  ]\n}\n");
            return sb.ToString();
        }

        private static string F(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);

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
