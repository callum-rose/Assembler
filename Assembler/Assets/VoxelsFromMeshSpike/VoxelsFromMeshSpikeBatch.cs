using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelsFromMeshSpike
{
    /// <summary>
    /// Headless entry point for the mesh → VOX spike, so an automated harness (or an AI) can voxelize a
    /// mesh and inspect the output without the editor window. Drives the exact same
    /// <see cref="VoxConversion.Run"/> as the window; only the I/O is different (CLI args + log instead of
    /// GUI + progress bars).
    ///
    /// Invoked via:
    ///   Unity -batchmode -nographics -projectPath &lt;project&gt; -quit -logFile - \
    ///         -executeMethod VoxelsFromMeshSpike.VoxelsFromMeshSpikeBatch.Run \
    ///         -meshPath &lt;mesh.obj|.fbx&gt; [-voxPath &lt;out.vox&gt;] [-maxDim 32] \
    ///         [-preset Creature|Prop|RawVoxelCleanup] [-palettePath Assets/…/MasterPalette.asset] \
    ///         [-removeFloaters true|false] [-mirror …] [-revolve …] [-deLight …] \
    ///         [-snapToHistogramPeaks true|false] [-histogramPeakCount &lt;int&gt;] \
    ///         [-snapToPalette …] [-morphology …]
    ///
    /// Boolean step flags override the preset's defaults. Exits 0 on success, non-zero on any failure.
    /// </summary>
    public static class VoxelsFromMeshSpikeBatch
    {
        public static void Run()
        {
            // In batch mode Unity stamps a script stack trace onto every Debug.Log; mute it so the
            // summary reads as a clean block rather than looking like a failure (mirrors EditorBatchCli).
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);

            try
            {
                string[] args = Environment.GetCommandLineArgs();

                string? meshPath = ArgValue(args, "-meshPath");
                if (string.IsNullOrEmpty(meshPath))
                {
                    Fail("Missing required -meshPath <mesh.obj|.fbx>.");
                    return;
                }

                string voxPath = ArgValue(args, "-voxPath") ?? DefaultVoxPath(meshPath);
                int maxDim = ParseInt(ArgValue(args, "-maxDim"), 32);

                VoxPipelinePreset preset = ParseEnum(ArgValue(args, "-preset"), VoxPipelinePreset.Creature);
                VoxPipelineSettings settings = VoxPipelinePresets.For(preset);
                ApplyOverrides(args, settings);

                IReadOnlyList<Color32> palette = LoadPalette(ArgValue(args, "-palettePath"));

                Debug.Log(
                    $"[VoxelsFromMeshSpikeBatch] mesh='{meshPath}' out='{voxPath}' maxDim={maxDim} " +
                    $"preset={preset} (floaters={settings.removeFloaters}, mirror={settings.mirror}, " +
                    $"revolve={settings.revolve}, deLight={settings.deLight}, " +
                    $"histogramPeaks={settings.snapToHistogramPeaks}×{settings.histogramPeakCount}, " +
                    $"snap={settings.snapToPalette}, morphology={settings.morphology})");

                VoxConversion.Summary summary = VoxConversion.Run(meshPath, voxPath, maxDim, settings, palette);

                Debug.Log($"[VoxelsFromMeshSpikeBatch] OK: wrote {summary}");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                // ToString() carries the trace, so the muted-trace setting above doesn't hide it.
                Debug.LogError($"[VoxelsFromMeshSpikeBatch] FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        private static void ApplyOverrides(string[] args, VoxPipelineSettings settings)
        {
            settings.removeFloaters = ParseBool(ArgValue(args, "-removeFloaters"), settings.removeFloaters);
            settings.mirror = ParseBool(ArgValue(args, "-mirror"), settings.mirror);
            settings.revolve = ParseBool(ArgValue(args, "-revolve"), settings.revolve);
            settings.deLight = ParseBool(ArgValue(args, "-deLight"), settings.deLight);
            settings.snapToHistogramPeaks = ParseBool(ArgValue(args, "-snapToHistogramPeaks"), settings.snapToHistogramPeaks);
            settings.histogramPeakCount = ParseInt(ArgValue(args, "-histogramPeakCount"), settings.histogramPeakCount);
            settings.snapToPalette = ParseBool(ArgValue(args, "-snapToPalette"), settings.snapToPalette);
            settings.morphology = ParseBool(ArgValue(args, "-morphology"), settings.morphology);
        }

        private static IReadOnlyList<Color32> LoadPalette(string? palettePath)
        {
            if (string.IsNullOrEmpty(palettePath))
            {
                return DefaultMasterPalette.Colors;
            }

            var palette = AssetDatabase.LoadAssetAtPath<VoxMasterPalette>(palettePath);
            if (palette == null)
            {
                throw new FileNotFoundException($"Master palette asset not found at '{palettePath}'.");
            }
            return palette.ToColor32();
        }

        private static void Fail(string message)
        {
            Debug.LogError($"[VoxelsFromMeshSpikeBatch] FAILED: {message}");
            EditorApplication.Exit(2);
        }

        // ---- arg parsing -----------------------------------------------------

        // Returns the token immediately after the last occurrence of flag, or null if absent.
        private static string? ArgValue(string[] args, string flag)
        {
            for (int i = args.Length - 2; i >= 0; i--)
            {
                if (args[i] == flag)
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private static int ParseInt(string? value, int fallback) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : fallback;

        private static bool ParseBool(string? value, bool fallback) =>
            bool.TryParse(value, out bool b) ? b : fallback;

        private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct =>
            Enum.TryParse(value, ignoreCase: true, out TEnum parsed) ? parsed : fallback;

        private static string DefaultVoxPath(string meshPath) =>
            Path.Combine(
                Path.GetDirectoryName(meshPath) ?? ".",
                Path.GetFileNameWithoutExtension(meshPath) + ".vox");
    }
}
