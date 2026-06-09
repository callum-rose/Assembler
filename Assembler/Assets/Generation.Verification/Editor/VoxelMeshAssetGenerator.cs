using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;
using Assembler.Voxels.Editor.Pipeline;
using Assembler.Voxels.Pipeline;
using Assembler.Voxels.Scripting;
using UnityEngine;

namespace Assembler.Generation.Verification.Editor
{
	/// <summary>
	/// Generates a voxel <c>.vox</c> via <see cref="VoxelGenerationPipeline"/> and saves
	/// it under <c>Assets/Resources/</c> so the Voxel Toolkit importer turns it into a
	/// loadable <c>Mesh</c> sub-asset. Owns its own client + executor (never shared),
	/// and omits all scratch/preview/refresh stages — the batch does the one import pass.
	/// </summary>
	public sealed class VoxelMeshAssetGenerator : IAssetGenerator
	{
		public string AssetType => "mesh";

		public async Task<string> GenerateAsync(AssetRequest req, string apiKey, AssetGenerationOptions opts,
			IGeneratorLogger logger, CancellationToken ct)
		{
			var savePath = $"Assets/Resources/{req.ResourcesPath}.vox";
			logger.Log($"[mesh:{req.Id}] generating voxel model -> {savePath}");

			using var client = new AnthropicClient(apiKey);
			var executor = new VoxelScriptExecutor(opts.VoxelLimits);

			await VoxelGenerationPipeline.CreateNew(EditorVoxelServices.Default)
				.WithAnthropic(client)
				.WithScriptExecutor(executor)
				.WithScriptLimits(opts.VoxelLimits)
				.WithPrompt(req.Prompt)
				.DedupeVoxels()
				.ParseModel()
				.EncodeVox()
				.SaveAsVoxFile(savePath)
				.ExecuteAsync(ct);

			logger.Log($"[mesh:{req.Id}] wrote {savePath}");
			return savePath;
		}

		public bool CanLoadGenerated(AssetRequest req)
		{
			return Resources.Load<Mesh>(req.ResourcesPath) != null;
		}
	}
}
