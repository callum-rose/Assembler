using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Assembler.Voxels.Pipeline
{
	/// <summary>
	/// Bundle of side-effect services + default observer that a pipeline runs
	/// under. Swap implementations per mode (editor vs runtime vs tests).
	/// </summary>
	public sealed record VoxelPipelineServices
	{
		public IVoxelFileSink FileSink { get; init; } = new SystemVoxelFileSink();
		public IAssetDatabaseService AssetDb { get; init; } = new NoOpAssetDatabaseService();
		public IVoxelPipelineObserver Observer { get; init; } = NullVoxelPipelineObserver.Instance;
		public IVoxelClock Clock { get; init; } = SystemVoxelClock.Instance;

		public static VoxelPipelineServices Default { get; } = new();
	}

	public interface IVoxelFileSink
	{
		Task WriteAsync(string path, byte[] bytes, CancellationToken ct);
	}

	public sealed class SystemVoxelFileSink : IVoxelFileSink
	{
		public Task WriteAsync(string path, byte[] bytes, CancellationToken ct)
		{
			var dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
			File.WriteAllBytes(path, bytes);
			return Task.CompletedTask;
		}
	}

	public interface IAssetDatabaseService
	{
		void Refresh();
		Mesh? LoadMesh(string path);
	}

	public sealed class NoOpAssetDatabaseService : IAssetDatabaseService
	{
		public void Refresh() { }
		public Mesh? LoadMesh(string path) => null;
	}

	public interface IVoxelClock
	{
		DateTime UtcNow { get; }
	}

	public sealed class SystemVoxelClock : IVoxelClock
	{
		public static readonly SystemVoxelClock Instance = new();
		public DateTime UtcNow => DateTime.UtcNow;
	}
}
