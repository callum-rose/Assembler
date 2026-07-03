using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Assembler.Voxels.Pipeline.Stages
{
	/// <summary>
	/// Writes <c>VoxBytes</c> to <c>path</c> via <c>ctx.FileSink</c>; sets
	/// <c>SavedVoxPath</c>.
	/// </summary>
	public sealed class WriteVoxFileStage : IVoxelStage
	{
		private readonly string _path;
		public WriteVoxFileStage(string path) => _path = path;
		public string Name => $"WriteVoxFile({_path})";

		public async Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			if (ctx.VoxBytes == null)
			{
				throw new InvalidOperationException($"{Name}: VoxBytes is required.");
			}

			await ctx.FileSink.WriteAsync(_path, ctx.VoxBytes, ct).ConfigureAwait(false);
			return ctx with { SavedVoxPath = _path };
		}
	}

	/// <summary>
	/// Writes the .voxproj sidecar next to <c>SavedVoxPath</c>. Mirrors the
	/// prompt + persistent instructions into the project so a future load
	/// rehydrates context.
	/// </summary>
	public sealed class SaveProjectSidecarStage : IVoxelStage
	{
		public string Name => "SaveProjectSidecar";

		public Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(ctx.SavedVoxPath))
			{
				throw new InvalidOperationException($"{Name}: SavedVoxPath is required (run WriteVoxFile first).");
			}

			var project = new VoxelProject
			{
				prompt = ctx.UserPrompt ?? ctx.Project.prompt ?? string.Empty,
				persistentInstructions = ctx.PersistentInstructions ?? ctx.Project.persistentInstructions ?? string.Empty,
				history = new List<VoxelProject.HistoryEntry>(ctx.Project.history),
			};

			var sidecarPath = VoxelProject.SidecarPathFor(ctx.SavedVoxPath!);
			VoxelProject.Save(sidecarPath, project);
			return Task.FromResult(ctx with { Project = project, SavedProjectPath = sidecarPath });
		}
	}

	/// <summary>
	/// Appends a <see cref="VoxelProject.HistoryEntry"/> to the project's
	/// history list. <paramref name="kindResolver"/> picks the kind, the prompt
	/// is read from <c>UserPrompt</c> or <c>RefinementInstruction</c>.
	/// </summary>
	public sealed class RecordHistoryStage : IVoxelStage
	{
		private readonly string _kind;
		public RecordHistoryStage(string kind) => _kind = kind;
		public string Name => $"RecordHistory({_kind})";

		public Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(ctx.GoxelTextZUp))
			{
				throw new InvalidOperationException($"{Name}: GoxelTextZUp is required.");
			}

			var promptForEntry = _kind switch
			{
				"refine-fresh" or "refine-chat" => ctx.RefinementInstruction ?? string.Empty,
				_ => ctx.UserPrompt ?? string.Empty,
			};

			var entry = new VoxelProject.HistoryEntry
			{
				kind = _kind,
				prompt = promptForEntry,
				goxelText = ctx.GoxelTextZUp ?? string.Empty,
				timestampIso = ctx.Clock.UtcNow.ToString("o"),
			};

			var newProject = new VoxelProject
			{
				prompt = ctx.Project.prompt,
				persistentInstructions = ctx.Project.persistentInstructions,
				history = new List<VoxelProject.HistoryEntry>(ctx.Project.history) { entry },
			};

			return Task.FromResult(ctx with { Project = newProject });
		}
	}

	/// <summary>
	/// Wraps an arbitrary user-supplied function as a stage.
	/// </summary>
	public sealed class DelegatePostProcessStage : IVoxelStage
	{
		private readonly Func<VoxelPipelineContext, CancellationToken, Task<VoxelPipelineContext>> _fn;
		public DelegatePostProcessStage(string name, Func<VoxelPipelineContext, CancellationToken, Task<VoxelPipelineContext>> fn)
		{
			Name = name;
			_fn = fn;
		}
		public string Name { get; }
		public Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct) => _fn(ctx, ct);
	}
}
