using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Voxels.Pipeline;
using Assembler.Voxels.Pipeline.Stages;
using UnityEditor;
using UnityEngine;

namespace Assembler.Voxels.Editor.Pipeline
{
	/// <summary>
	/// Editor-only side-effect stages (scratch preview, AssetDatabase refresh,
	/// mesh load) and their fluent extension methods. Importing this namespace
	/// is how callers in the editor unlock <c>.WriteScratchPreview(...)</c>,
	/// <c>.RefreshAssetDatabase()</c>, <c>.LoadPreviewMesh()</c>.
	/// </summary>
	public static class EditorPipelineExtensions
	{
		public static VoxelGenerationPipeline WriteScratchPreview(this VoxelGenerationPipeline p, string path)
			=> p.AddStage(new WriteScratchPreviewStage(path));

		public static VoxelGenerationPipeline RefreshAssetDatabase(this VoxelGenerationPipeline p)
			=> p.AddStage(new RefreshAssetDatabaseStage());

		public static VoxelGenerationPipeline LoadPreviewMesh(this VoxelGenerationPipeline p, string path)
			=> p.AddStage(new LoadPreviewMeshStage(path));

		/// <summary>
		/// Renders the imported model at <paramref name="path"/> from the option's
		/// angles into <c>RenderedImages</c> (Phase 3).
		/// </summary>
		public static VoxelGenerationPipeline RenderModelImages(
			this VoxelGenerationPipeline p, string path, VisionRefinementOptions options)
			=> p.AddStage(new RenderModelImagesStage(path, options));

		/// <summary>
		/// Vision-feedback loop controller (Phase 3): runs
		/// <paramref name="options"/>.Iterations passes of
		/// EncodeVox → WriteScratchPreview → RefreshAssetDatabase → RenderModelImages
		/// → VisionCritiqueRefine → DedupeVoxels → ParseModel → ValidateGeometry.
		/// Last-iteration-wins. The model + geometry from the final ValidateGeometry
		/// flow on for the caller to encode/preview.
		/// </summary>
		public static VoxelGenerationPipeline RefineWithVision(
			this VoxelGenerationPipeline p, VisionRefinementOptions options, string scratchPath)
		{
			for (var i = 0; i < options.Iterations; i++)
			{
				p = p.EncodeVox()
					.WriteScratchPreview(scratchPath)
					.RefreshAssetDatabase()
					.RenderModelImages(scratchPath, options)
					.VisionCritiqueRefine()
					.DedupeVoxels()
					.ParseModel()
					.ValidateGeometry();
			}

			return p;
		}
	}

	/// <summary>
	/// Writes <c>VoxBytes</c> to a fixed editor-scratch path so the Voxel Toolkit
	/// importer can produce a Mesh sub-asset for preview. Does NOT set
	/// <c>SavedVoxPath</c> — that's reserved for user-facing saves.
	/// </summary>
	public sealed class WriteScratchPreviewStage : IVoxelStage
	{
		private readonly string _scratchPath;
		public WriteScratchPreviewStage(string scratchPath) => _scratchPath = scratchPath;
		public string Name => $"WriteScratchPreview({_scratchPath})";

		public async Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			if (ctx.VoxBytes == null)
			{
				throw new InvalidOperationException($"{Name}: VoxBytes is required.");
			}

			var bytes = ctx.VoxBytes;
			var path = _scratchPath;
			await ctx.MainThread.RunAsync(() =>
			{
				var dir = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(dir))
				{
					Directory.CreateDirectory(dir);
				}

				File.WriteAllBytes(path, bytes);
			}).ConfigureAwait(false);
			return ctx;
		}
	}

	public sealed class RefreshAssetDatabaseStage : IVoxelStage
	{
		public string Name => "RefreshAssetDatabase";
		public async Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			var assetDb = ctx.AssetDb;
			await ctx.MainThread.RunAsync(assetDb.Refresh).ConfigureAwait(false);
			return ctx;
		}
	}

	public sealed class LoadPreviewMeshStage : IVoxelStage
	{
		private readonly string _path;
		public LoadPreviewMeshStage(string path) => _path = path;
		public string Name => $"LoadPreviewMesh({_path})";

		public async Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			Mesh? mesh = null;
			var assetDb = ctx.AssetDb;
			var path = _path;
			await ctx.MainThread.RunAsync(() => mesh = assetDb.LoadMesh(path)).ConfigureAwait(false);
			return ctx with { PreviewMesh = mesh };
		}
	}

	public sealed class EditorAssetDatabaseService : IAssetDatabaseService
	{
		public static readonly EditorAssetDatabaseService Instance = new();
		public void Refresh() => AssetDatabase.Refresh();
		public Mesh? LoadMesh(string path) => AssetDatabase.LoadAssetAtPath<Mesh>(path);
	}

	/// <summary>
	/// Marshals work onto the Unity editor main thread via a concurrent queue
	/// drained by <c>EditorApplication.update</c>. This pattern is safe to
	/// enqueue from any thread (whereas subscribing to
	/// <c>EditorApplication.delayCall</c> directly is not).
	/// </summary>
	public sealed class EditorMainThreadDispatcher : IMainThreadDispatcher
	{
		public static readonly EditorMainThreadDispatcher Instance = new();

		private static readonly ConcurrentQueue<Action> s_queue = new();
		private static int s_mainThreadId;

		[InitializeOnLoadMethod]
		private static void Install()
		{
			s_mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
			// Idempotent: subscribing twice with the same delegate is a no-op
			// for Unity events, but use an explicit unsubscribe+subscribe for
			// clarity after script reloads.
			EditorApplication.update -= Pump;
			EditorApplication.update += Pump;
		}

		private static void Pump()
		{
			while (s_queue.TryDequeue(out var action))
			{
				try { action(); }
				catch (Exception ex) { Debug.LogException(ex); }
			}
		}

		public Task RunAsync(Action action)
		{
			if (IsOnMainThread())
			{
				try { action(); return Task.CompletedTask; }
				catch (Exception ex) { return Task.FromException(ex); }
			}

			var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			s_queue.Enqueue(() =>
			{
				try { action(); tcs.SetResult(true); }
				catch (Exception ex) { tcs.SetException(ex); }
			});
			return tcs.Task;
		}

		private static bool IsOnMainThread() => System.Threading.Thread.CurrentThread.ManagedThreadId == s_mainThreadId;
	}

	public static class EditorVoxelServices
	{
		public static VoxelPipelineServices Default { get; } = new()
		{
			AssetDb = EditorAssetDatabaseService.Instance,
			MainThread = EditorMainThreadDispatcher.Instance,
		};
	}
}
