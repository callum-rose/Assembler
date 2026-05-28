using System;
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

		public Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			if (ctx.VoxBytes == null) throw new InvalidOperationException($"{Name}: VoxBytes is required.");
			var dir = Path.GetDirectoryName(_scratchPath);
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
			File.WriteAllBytes(_scratchPath, ctx.VoxBytes);
			return Task.FromResult(ctx);
		}
	}

	public sealed class RefreshAssetDatabaseStage : IVoxelStage
	{
		public string Name => "RefreshAssetDatabase";
		public Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			ctx.AssetDb.Refresh();
			return Task.FromResult(ctx);
		}
	}

	public sealed class LoadPreviewMeshStage : IVoxelStage
	{
		private readonly string _path;
		public LoadPreviewMeshStage(string path) => _path = path;
		public string Name => $"LoadPreviewMesh({_path})";

		public Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			var mesh = ctx.AssetDb.LoadMesh(_path);
			return Task.FromResult(ctx with { PreviewMesh = mesh });
		}
	}

	public sealed class EditorAssetDatabaseService : IAssetDatabaseService
	{
		public static readonly EditorAssetDatabaseService Instance = new();
		public void Refresh() => AssetDatabase.Refresh();
		public Mesh? LoadMesh(string path) => AssetDatabase.LoadAssetAtPath<Mesh>(path);
	}

	public static class EditorVoxelServices
	{
		public static VoxelPipelineServices Default { get; } = new()
		{
			AssetDb = EditorAssetDatabaseService.Instance,
		};
	}
}
