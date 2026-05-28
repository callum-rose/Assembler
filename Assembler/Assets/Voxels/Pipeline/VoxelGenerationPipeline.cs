using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;
using Assembler.Voxels.Pipeline.Stages;

namespace Assembler.Voxels.Pipeline
{
	/// <summary>
	/// Fluent composer for voxel generation pipelines. Stages run sequentially;
	/// each receives the prior context and returns a new one. The runner wraps
	/// each stage in observer events and (optionally) auto-inserts ParseModel /
	/// EncodeVox if a downstream stage needs them and they're missing.
	/// </summary>
	public sealed class VoxelGenerationPipeline
	{
		private readonly List<IVoxelStage> _stages = new();
		private VoxelPipelineContext _ctx;

		private VoxelGenerationPipeline(VoxelPipelineContext ctx) => _ctx = ctx;

		// Entry points.
		public static VoxelGenerationPipeline CreateNew(VoxelPipelineServices? services = null)
		{
			services ??= VoxelPipelineServices.Default;
			var ctx = new VoxelPipelineContext
			{
				FileSink = services.FileSink,
				AssetDb = services.AssetDb,
				Observer = services.Observer,
				Clock = services.Clock,
				MainThread = services.MainThread,
			};
			return new VoxelGenerationPipeline(ctx);
		}

		public static VoxelGenerationPipeline FromExisting(VoxelPipelineResult prior, VoxelPipelineServices? services = null)
		{
			if (prior == null) throw new ArgumentNullException(nameof(prior));
			var ctx = prior.Context;
			if (services != null)
			{
				ctx = ctx with
				{
					FileSink = services.FileSink,
					AssetDb = services.AssetDb,
					Observer = services.Observer,
					Clock = services.Clock,
					MainThread = services.MainThread,
				};
			}
			// Drop AnthropicClient — its lifecycle belongs to the caller, the prior
			// run's client may already be disposed. Caller re-supplies via WithAnthropic.
			ctx = ctx with { AnthropicClient = null, RefinementInstruction = null, UseChatHistory = false };
			return new VoxelGenerationPipeline(ctx);
		}

		public static VoxelGenerationPipeline FromVoxFile(string path, VoxelPipelineServices? services = null)
		{
			var bytes = File.ReadAllBytes(path);
			var pipeline = CreateNew(services);
			pipeline._ctx = pipeline._ctx with { VoxBytes = bytes };
			pipeline._stages.Add(new DecodeVoxStage());
			return pipeline;
		}

		// Inputs (no stage appended).
		public VoxelGenerationPipeline WithAnthropic(AnthropicClient client)
		{
			_ctx = _ctx with { AnthropicClient = client };
			return this;
		}

		public VoxelGenerationPipeline WithPrompt(string prompt)
		{
			_ctx = _ctx with { UserPrompt = prompt };
			// WithPrompt always implies a fresh generate cycle: clear refine state
			// so a re-used pipeline doesn't accidentally run RefineGoxelTextStage.
			_ctx = _ctx with { RefinementInstruction = null, UseChatHistory = false };
			EnsureLoadSystemPrompt();
			if (!ContainsStage<GenerateGoxelTextStage>()) _stages.Add(new GenerateGoxelTextStage());
			return this;
		}

		public VoxelGenerationPipeline WithPersistentInstructions(string instructions)
		{
			// Clear cached SystemPrompt so EnsureLoadSystemPrompt picks up the
			// new instructions on the next Generate/Refine.
			_ctx = _ctx with { PersistentInstructions = instructions, SystemPrompt = null };
			return this;
		}

		public VoxelGenerationPipeline WithObserver(IVoxelPipelineObserver observer)
		{
			_ctx = _ctx with { Observer = observer };
			return this;
		}

		// Stages.
		public VoxelGenerationPipeline GenerateGoxelText()
		{
			EnsureLoadSystemPrompt();
			if (!ContainsStage<GenerateGoxelTextStage>()) _stages.Add(new GenerateGoxelTextStage());
			return this;
		}

		public VoxelGenerationPipeline Refine(string instruction, bool useChatHistory = true)
		{
			_ctx = _ctx with { RefinementInstruction = instruction, UseChatHistory = useChatHistory };
			EnsureLoadSystemPrompt();
			_stages.Add(new RefineGoxelTextStage());
			return this;
		}

		public VoxelGenerationPipeline PostProcess(Func<VoxelPipelineContext, CancellationToken, Task<VoxelPipelineContext>> fn)
		{
			_stages.Add(new DelegatePostProcessStage("PostProcess", fn));
			return this;
		}

		public VoxelGenerationPipeline PostProcess(Action<VoxelPipelineContext> fn)
		{
			_stages.Add(new DelegatePostProcessStage("PostProcess", (ctx, _) =>
			{
				fn(ctx);
				return Task.FromResult(ctx);
			}));
			return this;
		}

		public VoxelGenerationPipeline AddStage(IVoxelStage stage)
		{
			_stages.Add(stage);
			return this;
		}

		public VoxelGenerationPipeline ParseModel()
		{
			_stages.Add(new ParseGoxelTextStage());
			return this;
		}

		public VoxelGenerationPipeline EncodeVox()
		{
			EnsureParseModel();
			_stages.Add(new EncodeVoxStage());
			return this;
		}

		public VoxelGenerationPipeline SaveAsVoxFile(string path)
		{
			EnsureParseModel();
			EnsureEncodeVox();
			_stages.Add(new WriteVoxFileStage(path));
			return this;
		}

		public VoxelGenerationPipeline SaveProjectSidecar()
		{
			_stages.Add(new SaveProjectSidecarStage());
			return this;
		}

		public VoxelGenerationPipeline RecordHistory(string kind)
		{
			_stages.Add(new RecordHistoryStage(kind));
			return this;
		}

		public async Task<VoxelPipelineResult> ExecuteAsync(CancellationToken ct = default)
		{
			// Stages may resume on thread-pool threads (Anthropic streaming
			// awaits land on the pool). Observer callbacks are marshaled via the
			// configured IMainThreadDispatcher so the editor's Repaint() etc.
			// always sees the Unity main thread. Stages that touch Unity APIs
			// (asset DB, asset import) wrap their own work via ctx.MainThread.
			var ctx = _ctx;
			foreach (var stage in _stages)
			{
				ct.ThrowIfCancellationRequested();
				var startedCtx = ctx;
				await ctx.MainThread.RunAsync(() => startedCtx.Observer.OnStageStarted(stage.Name)).ConfigureAwait(false);

				var sw = Stopwatch.StartNew();
				VoxelPipelineContext nextCtx;
				try
				{
					nextCtx = await stage.ExecuteAsync(ctx, ct).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					sw.Stop();
					var failedCtx = ctx;
					var stageRef = stage;
					await failedCtx.MainThread.RunAsync(() => failedCtx.Observer.OnStageFailed(stageRef.Name, ex)).ConfigureAwait(false);
					System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
					throw; // unreachable; keeps the compiler happy.
				}
				sw.Stop();

				ctx = nextCtx;
				var finishedCtx = ctx;
				var stageName = stage.Name;
				var elapsed = sw.Elapsed;
				await finishedCtx.MainThread.RunAsync(() => finishedCtx.Observer.OnStageFinished(stageName, elapsed)).ConfigureAwait(false);
			}
			return new VoxelPipelineResult(ctx);
		}

		private void EnsureLoadSystemPrompt()
		{
			if (!ContainsStage<LoadSystemPromptStage>() && string.IsNullOrEmpty(_ctx.SystemPrompt))
			{
				_stages.Add(new LoadSystemPromptStage());
			}
		}

		private void EnsureParseModel()
		{
			if (!ContainsStage<ParseGoxelTextStage>()) _stages.Add(new ParseGoxelTextStage());
		}

		private void EnsureEncodeVox()
		{
			if (!ContainsStage<EncodeVoxStage>()) _stages.Add(new EncodeVoxStage());
		}

		private bool ContainsStage<T>() where T : IVoxelStage
		{
			foreach (var s in _stages) if (s is T) return true;
			return false;
		}
	}
}
