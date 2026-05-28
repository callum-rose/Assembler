using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assembler.Voxels.Pipeline.Stages
{
	/// <summary>Parses <c>GoxelTextZUp</c> into a <see cref="VoxelModel"/>.</summary>
	public sealed class ParseGoxelTextStage : IVoxelStage
	{
		public string Name => "ParseGoxelText";

		public Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(ctx.GoxelTextZUp))
			{
				throw new InvalidOperationException($"{Name}: GoxelTextZUp is required.");
			}

			var model = GoxelTextParser.Parse(ctx.GoxelTextZUp!);
			return Task.FromResult(ctx with { Model = model });
		}
	}

	/// <summary>Serialises <c>Model</c> to .vox bytes (<c>VoxBytes</c>).</summary>
	public sealed class EncodeVoxStage : IVoxelStage
	{
		public string Name => "EncodeVox";

		public Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			if (ctx.Model == null) throw new InvalidOperationException($"{Name}: Model is required.");
			var bytes = VoxWriter.Write(ctx.Model);
			return Task.FromResult(ctx with { VoxBytes = bytes });
		}
	}

	/// <summary>
	/// Decodes <c>VoxBytes</c> into <c>Model</c> + <c>GoxelTextZUp</c> via
	/// <see cref="VoxReader"/> + <see cref="GoxelTextWriter"/>. Used by
	/// <c>FromVoxFile</c>.
	/// </summary>
	public sealed class DecodeVoxStage : IVoxelStage
	{
		public string Name => "DecodeVox";

		public Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			if (ctx.VoxBytes == null) throw new InvalidOperationException($"{Name}: VoxBytes is required.");
			var model = VoxReader.Read(ctx.VoxBytes);
			var text = GoxelTextWriter.Write(model);
			return Task.FromResult(ctx with { Model = model, GoxelTextZUp = text });
		}
	}
}
