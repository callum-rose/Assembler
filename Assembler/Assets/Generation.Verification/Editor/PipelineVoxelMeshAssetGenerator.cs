using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Assembler.AssetGeneration.MeshToVoxels;
using Assembler.AssetGeneration.TextToVoxelPipeline.Editor;
using UnityEngine;

namespace Assembler.Generation.Verification.Editor
{
	/// <summary>
	/// Generates a voxel <c>.vox</c> by driving the full asset-generation pipeline
	/// (<see cref="VoxelPipeline.RunAsync"/>): text → image → Meshy mesh → voxels. Produced
	/// into a scratch dir under <c>Assets/TextToVoxel/</c>, then only the <c>.vox</c> is copied
	/// to <c>Assets/Resources/&lt;ResourcesPath&gt;.vox</c> — so the intermediate <c>.png</c>/
	/// <c>.obj</c>/textures stay out of <c>Resources/</c> (and player builds). The Voxel Toolkit
	/// importer then turns the Resources <c>.vox</c> into a loadable <c>Mesh</c> sub-asset, exactly
	/// as for <see cref="VoxelMeshAssetGenerator"/>, so the descriptor contract is unchanged.
	///
	/// The pipeline touches <c>AssetDatabase</c> and Unity APIs and captures the calling
	/// <c>SynchronizationContext</c> to marshal those back to the main thread, so it must be driven
	/// from the main-thread call chain — which the orchestrator/window guarantee (nothing on the
	/// path to here uses <c>Task.Run</c>/<c>ConfigureAwait(false)</c>).
	/// </summary>
	public sealed class PipelineVoxelMeshAssetGenerator : IAssetGenerator
	{
		// Scratch root for the pipeline's intermediates; kept out of Resources/ so the .png/.obj/maps
		// are not baked into player builds. Per-asset subfolders (mirroring the Resources path) keep
		// concurrent generations from clobbering each other.
		private const string ScratchRoot = "Assets/TextToVoxel/_generated";

		public string AssetType => "mesh";

		public async Task<string> GenerateAsync(AssetRequest req, string apiKey, AssetGenerationOptions opts,
			IGeneratorLogger logger, CancellationToken ct)
		{
			var p = opts.Pipeline;
			var savePath = $"Assets/Resources/{req.ResourcesPath}.vox";
			var scratchDir = $"{ScratchRoot}/{req.ResourcesPath}";
			const string baseName = "model";

			logger.Log($"[mesh:{req.Id}] running text→image→Meshy→voxel pipeline -> {savePath}");

			var settings = new VoxelPipelineSettings
			{
				// Stage 1 — text → image.
				ImageProvider = p.ImageProvider,
				ImageApiKey = p.ImageApiKey,
				ImageModel = p.ImageModel,
				Prompt = req.Prompt,

				// Stage 2 — image → mesh (Meshy defaults from VoxelPipelineSettings are fine).
				MeshyApiKey = p.MeshyApiKey,

				// Stage 3 — mesh → voxels.
				MaxDimVoxels = p.MaxDimVoxels,
				VoxSettings = VoxPipelinePresets.For(p.VoxPreset),
				Palette = p.Palette,

				// Generate-then-copy: write intermediates straight into the scratch dir, no per-run subfolder.
				OutputDir = scratchDir,
				BaseName = baseName,
				AutoSubfolderPerRun = false,
			};

			var result = await VoxelPipeline.RunAsync(
				settings, ct, onStatus: msg => logger.Log($"[mesh:{req.Id}] {msg}"));

			switch (result)
			{
				case VoxelPipeline.Result.Success:
					CopyVoxToResources($"{scratchDir}/{baseName}.vox", savePath);
					logger.Log($"[mesh:{req.Id}] wrote {savePath}");
					return savePath;

				case VoxelPipeline.Result.Cancelled:
					throw new OperationCanceledException(ct);

				default:
					// Surface the pipeline's failure as an exception so the batch records it as a failed
					// AssetResult and the orchestrator feeds the reason into Claude's next fix pass.
					throw FailureToException(result);
			}
		}

		public bool CanLoadGenerated(AssetRequest req) =>
			Resources.Load<Mesh>(req.ResourcesPath) != null;

		// File-level copy only (no AssetDatabase): the batch owns the single import + load-probe pass.
		private static void CopyVoxToResources(string scratchVoxPath, string savePath)
		{
			if (!File.Exists(scratchVoxPath))
			{
				throw new FileNotFoundException(
					$"pipeline reported success but produced no .vox at '{scratchVoxPath}'", scratchVoxPath);
			}

			var dir = Path.GetDirectoryName(savePath);
			if (!string.IsNullOrEmpty(dir))
			{
				Directory.CreateDirectory(dir);
			}

			File.Copy(scratchVoxPath, savePath, overwrite: true);
		}

		private static Exception FailureToException(VoxelPipeline.Result result) =>
			result switch
			{
				VoxelPipeline.Result.InvalidInput invalid =>
					new InvalidOperationException($"pipeline rejected input: {invalid.Message}"),
				VoxelPipeline.Result.ImageFailed f =>
					new InvalidOperationException($"image generation failed: {f.Error.Message}", f.Error),
				VoxelPipeline.Result.MeshFailed f =>
					new InvalidOperationException($"mesh conversion failed: {f.Error.Message}", f.Error),
				VoxelPipeline.Result.VoxelizationFailed f =>
					new InvalidOperationException($"voxelization failed: {f.Error.Message}", f.Error),
				_ => new InvalidOperationException($"pipeline failed: {result}"),
			};
	}
}
