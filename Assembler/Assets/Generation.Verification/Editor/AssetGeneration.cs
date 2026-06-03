using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Voxels.Scripting;

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
	/// Per-type generation knobs. Voxel-only for v1; new asset types add their own
	/// fields here as they land.
	/// </summary>
	public sealed record AssetGenerationOptions(VoxelScriptLimits VoxelLimits)
	{
		public static AssetGenerationOptions Default { get; } = new(VoxelScriptLimits.Default);
	}

	/// <summary>
	/// Produces one asset type. Implementations do their own generation + persistence
	/// to <c>Assets/Resources/&lt;ResourcesPath&gt;.&lt;ext&gt;</c> and make NO
	/// <c>AssetDatabase</c>/<c>Resources</c> calls inside <see cref="GenerateAsync"/>
	/// — the batch owns the single import pass and runs <see cref="TryLoadGenerated"/>
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
		Task<string> GenerateAsync(AssetRequest req, string apiKey, AssetGenerationOptions opts,
			IGeneratorLogger? logger, CancellationToken ct);

		/// <summary>
		/// Main-thread probe: does the generated asset now load? (e.g. via
		/// <c>Resources.Load</c>). The batch calls this after the import pass.
		/// </summary>
		bool TryLoadGenerated(AssetRequest req);
	}

	/// <summary>
	/// Maps a descriptor asset <see cref="AssetRequest.Type"/> to its generator. New
	/// asset types slot in by implementing <see cref="IAssetGenerator"/> and
	/// registering it here — no change to the orchestrator, manifest, batch, or window.
	/// </summary>
	public sealed class AssetGeneratorRegistry
	{
		private readonly Dictionary<string, IAssetGenerator> _generators =
			new(StringComparer.OrdinalIgnoreCase);

		public AssetGeneratorRegistry Register(IAssetGenerator generator)
		{
			if (generator == null) throw new ArgumentNullException(nameof(generator));
			_generators[generator.AssetType] = generator;
			return this;
		}

		public IAssetGenerator? Get(string? type)
		{
			if (string.IsNullOrWhiteSpace(type)) return null;
			return _generators.TryGetValue(type!, out var g) ? g : null;
		}

		/// <summary>The asset types we can generate — surfaced to Claude in the prompt.</summary>
		public IReadOnlyList<string> SupportedTypes => new List<string>(_generators.Keys);

		/// <summary>The shipping registry: one concrete generator (voxel meshes).</summary>
		public static AssetGeneratorRegistry Default =>
			new AssetGeneratorRegistry().Register(new VoxelMeshAssetGenerator());
	}
}
