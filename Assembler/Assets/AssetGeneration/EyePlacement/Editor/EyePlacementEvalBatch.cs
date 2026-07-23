using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// The issue #479 acceptance command: one headless entry point that takes the eye corpus (a
    /// directory of <c>.vox</c> models each paired with a <c>&lt;name&gt;.eyes.json</c> ground-truth
    /// sidecar), runs the current placement pipeline on each, scores the resolved 3D anchors against
    /// the ground truth (<see cref="EyePlacementScorer"/>) and writes a machine-readable per-model
    /// PASS/FAIL summary plus an orbit montage (<see cref="EyeMontage"/>) per model. This is the only
    /// sanctioned way to claim an accuracy number — no run merges without a score from here.
    ///
    /// <para><b>Mode.</b> With an API key it runs the full vision pipeline (<see cref="EyePlacer.PlaceAsync"/>,
    /// the "current pipeline" whose baseline is expected ≈0/N) — run it <b>without</b> <c>-nographics</c>
    /// so the vision cue is the crisp GPU render. With no key it scores the offline geometric fallback
    /// (<see cref="EyePlacer.PlaceGeometric"/>), which needs no GPU or network.</para>
    /// <code>
    ///   Unity -batchmode -quit -projectPath &lt;project&gt;
    ///         -executeMethod Assembler.AssetGeneration.EyePlacement.EyePlacementEvalBatch.Evaluate
    ///         -corpusDir &lt;dir-of-vox-and-eyes.json&gt; -outDir &lt;dir&gt;
    ///         [-apiKey sk-...] [-mode vision|geometric] [-gtDir &lt;dir&gt;]
    ///         [-tolerance 2.5] [-upDot 0.6] [-viewCount 8] [-imageSize 1024] [-pitch 30]
    /// </code>
    /// </summary>
    public static class EyePlacementEvalBatch
    {
        private const string ApiKeyPref = "Assembler.Generation.ApiKey";

        public static void Evaluate()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                string corpusDir = LastArg(args, "-corpusDir")
                    ?? throw new ArgumentException("-corpusDir is required");
                string gtDir = LastArg(args, "-gtDir") ?? corpusDir;
                string outDir = LastArg(args, "-outDir")
                    ?? throw new ArgumentException("-outDir is required");
                string apiKey = LastArg(args, "-apiKey") ?? EditorPrefs.GetString(ApiKeyPref, string.Empty);
                string mode = (LastArg(args, "-mode") ?? (string.IsNullOrWhiteSpace(apiKey) ? "geometric" : "vision"))
                    .Trim().ToLowerInvariant();
                bool vision = mode == "vision";

                var scoreOptions = new EyeScoreOptions
                {
                    DefaultToleranceVoxels = FloatArg(args, "-tolerance", 2.5f),
                    UpNormalDotThreshold = FloatArg(args, "-upDot", 0.6f),
                };
                var placeOptions = new EyePlacementOptions
                {
                    ImageSize = IntArg(args, "-imageSize", 1024),
                    PitchDegrees = FloatArg(args, "-pitch", 30f),
                };
                var montageOptions = new MontageOptions
                {
                    ViewCount = IntArg(args, "-viewCount", 8),
                    PitchDegrees = placeOptions.PitchDegrees,
                    DefaultToleranceVoxels = scoreOptions.DefaultToleranceVoxels,
                };

                if (vision && string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new ArgumentException("-mode vision needs an -apiKey (or the shared editor key).");
                }

                Directory.CreateDirectory(outDir);
                var models = DiscoverModels(corpusDir, gtDir).ToList();
                if (models.Count == 0)
                {
                    throw new ArgumentException(
                        $"no .vox + .eyes.json pairs found under '{corpusDir}' (ground truth in '{gtDir}').");
                }

                var results = new List<ModelScore>();
                foreach ((string voxPath, string gtPath) in models)
                {
                    string name = Path.GetFileNameWithoutExtension(voxPath);
                    VoxelModel model = VoxReader.Read(File.ReadAllBytes(voxPath));
                    EyeGroundTruth truth = EyeGroundTruth.FromJson(File.ReadAllText(gtPath));

                    IReadOnlyList<EyeAnchor> anchors = vision
                        ? AsyncPump.Run(() => EyePlacer.PlaceAsync(apiKey, model, placeOptions)).Eyes
                        : EyePlacer.PlaceGeometric(model, placeOptions).Eyes;

                    ModelScore score = EyePlacementScorer.Score(model, truth, anchors, scoreOptions);
                    results.Add(score);

                    byte[] montage = EyeMontage.Render(model, truth, anchors, score, montageOptions);
                    File.WriteAllBytes(Path.Combine(outDir, $"{name}_montage.png"), montage);
                    Debug.Log(score.Summary);
                }

                string summaryPath = Path.Combine(outDir, "eval_summary.json");
                File.WriteAllText(summaryPath, SummaryJson(mode, scoreOptions, results));
                Debug.Log(Report(mode, results, outDir));
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("EyePlacementEvalBatch failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static IEnumerable<(string VoxPath, string GtPath)> DiscoverModels(string corpusDir, string gtDir)
        {
            if (!Directory.Exists(corpusDir))
            {
                throw new ArgumentException($"-corpusDir '{corpusDir}' does not exist.");
            }

            foreach (string voxPath in Directory.EnumerateFiles(corpusDir, "*.vox", SearchOption.AllDirectories)
                         .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileNameWithoutExtension(voxPath);
                string gtPath = Path.Combine(gtDir, $"{name}.eyes.json");
                if (!File.Exists(gtPath))
                {
                    gtPath = Path.Combine(Path.GetDirectoryName(voxPath) ?? corpusDir, $"{name}.eyes.json");
                }

                if (File.Exists(gtPath))
                {
                    yield return (voxPath, gtPath);
                }
                else
                {
                    Debug.LogWarning($"EyePlacementEvalBatch: no ground truth for '{name}' (expected {name}.eyes.json) — skipped.");
                }
            }
        }

        private static string Report(string mode, IReadOnlyList<ModelScore> results, string outDir)
        {
            int passed = results.Count(r => r.Pass);
            var sb = new StringBuilder();
            sb.AppendLine("============== Eye placement evaluation ==============");
            sb.AppendLine($"Mode: {mode}");
            foreach (ModelScore r in results)
            {
                sb.AppendLine((r.Pass ? "PASS  " : "FAIL  ") + r.Summary);
            }

            sb.AppendLine();
            sb.AppendLine($"Baseline: {passed}/{results.Count} models placed correctly.");
            sb.AppendLine($"Summary + montages: {outDir}");
            sb.AppendLine("=====================================================");
            return sb.ToString();
        }

        private static string SummaryJson(string mode, EyeScoreOptions options, IReadOnlyList<ModelScore> results)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"mode\": \"{mode}\",\n");
            sb.Append($"  \"defaultToleranceVoxels\": {F(options.DefaultToleranceVoxels)},\n");
            sb.Append($"  \"upNormalDotThreshold\": {F(options.UpNormalDotThreshold)},\n");
            sb.Append($"  \"passed\": {results.Count(r => r.Pass)},\n");
            sb.Append($"  \"total\": {results.Count},\n");
            sb.Append("  \"models\": [\n");
            sb.Append(string.Join(",\n", results.Select(ModelJson)));
            sb.Append("\n  ]\n}\n");
            return sb.ToString();
        }

        private static string ModelJson(ModelScore model)
        {
            int correct = model.Eyes.Count(e => e.Pass);
            string eyes = string.Join(",\n", model.Eyes.Select(EyeJson));
            var sb = new StringBuilder();
            sb.Append("    {\n");
            sb.Append($"      \"name\": \"{Escape(model.Name)}\",\n");
            sb.Append($"      \"pass\": {(model.Pass ? "true" : "false")},\n");
            sb.Append($"      \"eyesCorrect\": {correct},\n");
            sb.Append($"      \"eyesTotal\": {model.Eyes.Count},\n");
            sb.Append($"      \"extraAnchors\": {model.ExtraAnchors},\n");
            sb.Append("      \"eyes\": [\n");
            sb.Append(eyes);
            sb.Append("\n      ]\n    }");
            return sb.ToString();
        }

        private static string EyeJson(EyeScore eye)
        {
            string anchor = eye.Anchor is { } a
                ? $"[{F(a.Position.x)}, {F(a.Position.y)}, {F(a.Position.z)}]"
                : "null";
            string normal = eye.Anchor is { } n
                ? $"[{F(n.Normal.x)}, {F(n.Normal.y)}, {F(n.Normal.z)}]"
                : "null";
            string distance = float.IsFinite(eye.DistanceVoxels) ? F(eye.DistanceVoxels) : "null";
            return "        {" +
                   $" \"pass\": {(eye.Pass ? "true" : "false")}," +
                   $" \"distanceVoxels\": {distance}," +
                   $" \"withinTolerance\": {(eye.WithinTolerance ? "true" : "false")}," +
                   $" \"onSurface\": {(eye.OnSurface ? "true" : "false")}," +
                   $" \"normalNotUp\": {(eye.NormalNotUp ? "true" : "false")}," +
                   $" \"target\": [{F(eye.Target.x)}, {F(eye.Target.y)}, {F(eye.Target.z)}]," +
                   $" \"anchor\": {anchor}," +
                   $" \"normal\": {normal}," +
                   $" \"reason\": \"{Escape(eye.Reason)}\" }}";
        }

        private static string F(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string? LastArg(string[] args, string flag)
        {
            for (int i = args.Length - 2; i >= 0; i--)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static int IntArg(string[] args, string flag, int fallback) =>
            LastArg(args, flag) is { } raw
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;

        private static float FloatArg(string[] args, string flag, float fallback) =>
            LastArg(args, flag) is { } raw
            && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                ? value
                : fallback;

        /// <summary>
        /// Minimal single-thread message pump (Stephen Toub's AsyncPump), same as the voxelization
        /// batch: lets the async vision pipeline's main-thread continuations (render / PNG encode) run
        /// on the editor thread while a blocked <c>-executeMethod</c> would otherwise stall Unity's
        /// own update loop.
        /// </summary>
        private static class AsyncPump
        {
            public static T Run<T>(Func<Task<T>> func)
            {
                SynchronizationContext? previous = SynchronizationContext.Current;
                var context = new SingleThreadSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(context);
                try
                {
                    Task<T> task = func();
                    task.ContinueWith(_ => context.Complete(), TaskScheduler.Default);
                    context.RunOnCurrentThread();
                    return task.GetAwaiter().GetResult();
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previous);
                }
            }

            private sealed class SingleThreadSynchronizationContext : SynchronizationContext
            {
                private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

                public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

                public override void Send(SendOrPostCallback d, object? state) => d(state);

                public void Complete() => _queue.CompleteAdding();

                public void RunOnCurrentThread()
                {
                    foreach ((SendOrPostCallback callback, object? state) in _queue.GetConsumingEnumerable())
                    {
                        callback(state);
                    }
                }
            }
        }
    }
}
