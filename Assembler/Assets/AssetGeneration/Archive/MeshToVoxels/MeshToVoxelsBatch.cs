using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels
{
	/// <summary>
	/// Headless entry point for the mesh → VOX stage, so an automated harness (or an AI) can voxelize a
	/// mesh and inspect the output without the editor window. Drives the same pipeline as the window via
	/// <see cref="VoxConversion.RunSynchronous"/> (the blocking variant — there is no interactive editor
	/// to keep responsive under -batchmode); only the I/O is different (CLI args + log instead of GUI +
	/// progress bars).
	///
	/// Invoked via:
	///   Unity -batchmode -nographics -projectPath &lt;project&gt; -quit -logFile - \
	///         -executeMethod Assembler.AssetGeneration.VoxelPipeline.MeshToVoxelsBatch.Run \
	///         -meshPath &lt;mesh.obj|.fbx&gt; [-voxPath &lt;out.vox&gt;] [-maxDim 32] \
	///         [-preset Creature|Prop|RawVoxelCleanup] [-palettePath Assets/…/MasterPalette.asset] \
	///         [-removeFloaters true|false] [-mirror …] [-revolve …] [-deLight …] \
	///         [-snapToHistogramPeaks true|false] [-histogramPeakVariety &lt;float&gt;] [-histogramPeakCount &lt;int&gt;] \
	///         [-snapToPalette …] [-morphology …]
	///
	/// Boolean step flags override the preset's defaults. Exits 0 on success, non-zero on any failure.
	/// </summary>
	public static class MeshToVoxelsBatch
	{
		/// <summary>
		/// Pipeline entry point, reached as <c>unity command voxelize_mesh</c>. Same conversion as
		/// <see cref="Run"/>, driven from a resident editor instead of a batch boot.
		/// </summary>
		/// <remarks>
		/// <para>
		/// <see cref="VoxConversion.RunSynchronous"/> blocks the main thread for the whole conversion, so
		/// a large mesh at a high <paramref name="maxDim"/> can outrun the 60s main-thread budget a
		/// command gets — and will freeze the editor meanwhile. That is acceptable for a one-shot
		/// generation step the developer explicitly asked for; for a long run use
		/// <c>unity command --detach</c>, which is unbounded and polled via <c>unity job</c>.
		/// </para>
		/// <para>
		/// The per-step boolean/numeric knobs arrive as one <paramref name="overrides"/> string rather
		/// than a parameter each, because their semantics are "unset means keep the preset's default" —
		/// which a plain <c>bool</c> parameter cannot express, since it would always carry a value.
		/// </para>
		/// </remarks>
		[CliCommand("voxelize_mesh", "Convert a mesh (.obj/.fbx) to a .vox voxel model through the "
			+ "MeshToVoxels pipeline, applying a preset and optional per-step overrides.",
			Tags = new[] { "assembler/assetgen" })]
		public static string VoxelizeMeshCommand(
			[CliArg("mesh-path", "Mesh to voxelize, e.g. 'Assets/Models/Tree.obj'.")]
			string meshPath,
			[CliArg("vox-path", "Output .vox path. Defaults to the mesh path with a .vox extension.")]
			string? voxPath = null,
			[CliArg("max-dim", "Longest-axis resolution of the voxel grid.")]
			int maxDim = 32,
			[CliArg("preset", "Pipeline preset: Creature, Prop or RawVoxelCleanup.")]
			string preset = "Creature",
			[CliArg("palette-path", "VoxMasterPalette asset to snap colours to. "
				+ "Defaults to the built-in master palette.")]
			string? palettePath = null,
			[CliArg("overrides", "Comma-separated key=value overrides of the preset's steps, e.g. "
				+ "'removeFloaters=false,maxDim=64'. Keys: removeFloaters, mirror, revolve, deLight, "
				+ "snapToHistogramPeaks, histogramPeakVariety, histogramPeakCount, snapToPalette, morphology.")]
			string? overrides = null)
		{
			if (string.IsNullOrWhiteSpace(meshPath))
			{
				throw new ArgumentException("mesh-path is required.", nameof(meshPath));
			}

			// Pick up a mesh written or changed on disk since the editor last imported.
			AssetDatabase.Refresh();

			string resolvedVoxPath = string.IsNullOrWhiteSpace(voxPath) ? DefaultVoxPath(meshPath) : voxPath!;
			VoxPipelinePreset parsedPreset = ParseEnum(preset, VoxPipelinePreset.Creature);
			VoxPipelineSettings settings = VoxPipelinePresets.For(parsedPreset);
			ApplyOverrides(ParseOverrides(overrides), settings);
			IReadOnlyList<Color32> palette = LoadPalette(palettePath);

			VoxConversion.Summary summary =
				VoxConversion.RunSynchronous(meshPath, resolvedVoxPath, maxDim, settings, palette);

			// The .vox landed inside Assets/ in the common case; make the editor aware of it.
			AssetDatabase.Refresh();

			return $"[voxelize_mesh] OK: wrote {summary} (mesh='{meshPath}' out='{resolvedVoxPath}' "
				+ $"maxDim={maxDim} preset={parsedPreset})";
		}

		// Splits "a=1,b=false" into a lookup. Unknown keys are ignored by ApplyOverrides, which only
		// reads the ones it knows — the same forgiving behaviour the -flag parsing had.
		private static Dictionary<string, string> ParseOverrides(string? raw)
		{
			var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (string.IsNullOrWhiteSpace(raw))
			{
				return result;
			}

			foreach (string pair in raw!.Split(',', StringSplitOptions.RemoveEmptyEntries))
			{
				int eq = pair.IndexOf('=');
				if (eq <= 0)
				{
					throw new ArgumentException($"malformed override '{pair.Trim()}' — expected key=value.");
				}

				result[pair.Substring(0, eq).Trim()] = pair.Substring(eq + 1).Trim();
			}

			return result;
		}

		// Dictionary-driven twin of the argv-driven ApplyOverrides below; both leave a setting alone
		// when its key is absent, so the preset's default survives.
		private static void ApplyOverrides(IReadOnlyDictionary<string, string> o, VoxPipelineSettings settings)
		{
			string? V(string key) => o.TryGetValue(key, out string? v) ? v : null;

			settings.removeFloaters = ParseBool(V("removeFloaters"), settings.removeFloaters);
			settings.mirror = ParseBool(V("mirror"), settings.mirror);
			settings.revolve = ParseBool(V("revolve"), settings.revolve);
			settings.deLight = ParseBool(V("deLight"), settings.deLight);
			settings.snapToHistogramPeaks = ParseBool(V("snapToHistogramPeaks"), settings.snapToHistogramPeaks);
			settings.histogramPeakVariety = ParseFloat(V("histogramPeakVariety"), settings.histogramPeakVariety);
			settings.histogramPeakCount = ParseInt(V("histogramPeakCount"), settings.histogramPeakCount);
			settings.snapToPalette = ParseBool(V("snapToPalette"), settings.snapToPalette);
			settings.morphology = ParseBool(V("morphology"), settings.morphology);
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

		private static int ParseInt(string? value, int fallback) =>
			int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : fallback;

		private static float ParseFloat(string? value, float fallback) =>
			float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : fallback;

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
