using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assembler.AssetGeneration.MeshToVoxels;
using Assembler.AssetGeneration.TextToImage.Editor;
using Assembler.Voxels.Scripting;
using UnityEngine;

namespace Assembler.Generation.Verification.Editor
{
	/// <summary>
	/// A single asset the descriptor generator asked us to produce. <see cref="Type"/>
	/// is the descriptor <c>Assets:</c> <c>Type</c> key (e.g. "mesh") used to pick a
	/// generator; <see cref="ResourcesPath"/> is the Resources-relative path (no
	/// extension) that both the descriptor's <c>Path</c> and the generator agree on.
	/// </summary>
	public sealed record AssetRequest(string Type, string Id, string ResourcesPath, string Prompt);

	/// <summary>Outcome of generating (or skipping) one <see cref="AssetRequest"/>.</summary>
	public sealed record AssetResult(AssetRequest Request, bool Success, string? Error);

	/// <summary>
	/// Per-type generation knobs. <see cref="VoxelLimits"/> bounds the Anthropic-only
	/// <see cref="VoxelMeshAssetGenerator"/>; <see cref="Pipeline"/> configures the full
	/// text→image→Meshy→voxel <see cref="PipelineVoxelMeshAssetGenerator"/>. Which of the two
	/// runs is chosen by the orchestrator's <see cref="MeshSource"/>.
	/// </summary>
	public sealed record AssetGenerationOptions(VoxelScriptLimits VoxelLimits, PipelineAssetOptions Pipeline)
	{
		public static AssetGenerationOptions Default { get; } =
			new(VoxelScriptLimits.Default, PipelineAssetOptions.Default);
	}

	/// <summary>
	/// Config for the full text→image→Meshy→voxel pipeline driven by
	/// <see cref="PipelineVoxelMeshAssetGenerator"/>. API keys are caller-supplied — the pipeline
	/// never looks them up itself — so <see cref="Default"/> leaves them blank for the window to fill.
	/// </summary>
	public sealed record PipelineAssetOptions(
		ImageProvider ImageProvider,
		string ImageApiKey,
		string ImageModel,
		string MeshyApiKey,
		int MaxDimVoxels,
		VoxPipelinePreset VoxPreset,
		IReadOnlyList<Color32> Palette)
	{
		public static PipelineAssetOptions Default { get; } = new(
			ImageProvider.GoogleGemini,
			string.Empty,
			ImageGeneratorFactory.DefaultModelFor(ImageProvider.GoogleGemini),
			string.Empty,
			32,
			VoxPipelinePreset.Creature,
			DefaultMasterPalette.Colors);
	}

	/// <summary>Which concrete "mesh" generator the orchestrator wires up.</summary>
	public enum MeshSource
	{
		/// <summary>Anthropic-only voxel-drawing script — near-instant, free, clean/blocky.</summary>
		Script,

		/// <summary>Full text→image→Meshy→voxel pipeline — slow, paid (image + Meshy), organic.</summary>
		Pipeline,
	}

	/// <summary>
	/// Produces one asset type. Implementations do their own generation + persistence
	/// to <c>Assets/Resources/&lt;ResourcesPath&gt;.&lt;ext&gt;</c> and make NO
	/// <c>AssetDatabase</c>/<c>Resources</c> calls inside <see cref="GenerateAsync"/>
	/// — the batch owns the single import pass and runs <see cref="CanLoadGenerated"/>
	/// on the main thread.
	/// </summary>
	public interface IAssetGenerator
	{
		/// <summary>Descriptor <c>Assets:</c> <c>Type</c> key this generator handles.</summary>
		string AssetType { get; }

		/// <summary>
		/// Generate one asset to disk. Returns the project-relative path written.
		/// Must not touch <c>AssetDatabase</c>/<c>Resources</c>.
		/// </summary>
		Task<string> GenerateAsync(AssetRequest req,
			string apiKey,
			AssetGenerationOptions opts,
			IGeneratorLogger logger,
			CancellationToken ct);

		/// <summary>
		/// Main-thread probe: can the generated asset now load? (e.g. via
		/// <c>Resources.Load</c>). The batch calls this after the import pass.
		/// </summary>
		bool CanLoadGenerated(AssetRequest req);
	}

	/// <summary>
	/// Maps a descriptor asset <see cref="AssetRequest.Type"/> to its generator. New
	/// asset types slot in by implementing <see cref="IAssetGenerator"/> and
	/// registering it here — no change to the orchestrator, manifest, batch, or window.
	/// </summary>
	public sealed class AssetGeneratorRegistry
	{
		private readonly Dictionary<string, IAssetGenerator> _generators = new(StringComparer.OrdinalIgnoreCase);

		public AssetGeneratorRegistry Register(IAssetGenerator generator)
		{
			_generators[generator.AssetType] = generator;
			return this;
		}

		public IAssetGenerator? Get(string? type) =>
			string.IsNullOrWhiteSpace(type) ? null : _generators.GetValueOrDefault(type!);

		/// <summary>The asset types we can generate — surfaced to Claude in the prompt.</summary>
		public IReadOnlyList<string> SupportedTypes => new List<string>(_generators.Keys);

		/// <summary>The default registry: the Anthropic-only voxel-script mesh generator.</summary>
		public static AssetGeneratorRegistry Default =>
			new AssetGeneratorRegistry().Register(new VoxelMeshAssetGenerator());

		/// <summary>The registry whose "mesh" generator drives the full asset-generation pipeline.</summary>
		public static AssetGeneratorRegistry Pipeline =>
			new AssetGeneratorRegistry().Register(new PipelineVoxelMeshAssetGenerator());

		/// <summary>Pick the registry for a <see cref="MeshSource"/>.</summary>
		public static AssetGeneratorRegistry For(MeshSource source) =>
			source is MeshSource.Pipeline ? Pipeline : Default;
	}
}
