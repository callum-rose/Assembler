using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Assembler.Resolving
{
	/// <summary>
	/// Loads assets through Unity Addressables, supporting locally-bundled and remotely-downloaded content. The
	/// descriptor's <c>Path</c> is the Addressables address/key, not a Resources path. Every loaded asset's
	/// <see cref="AsyncOperationHandle"/> is retained and released on <see cref="Dispose"/>; not releasing leaks
	/// the underlying bundle and pins its ref count, so the registry that owns this loader disposes it on
	/// teardown.
	/// </summary>
	public sealed class AddressablesAssetLoader : IAssetLoader, IDisposable
	{
		private readonly List<AsyncOperationHandle> _handles = new();

		public async Task<T> LoadAsync<T>(string path) where T : Object
		{
			var handle = Addressables.LoadAssetAsync<T>(path);

			// Track the handle before awaiting so a failed load is still released on Dispose (Addressables
			// requires failed handles to be released too).
			_handles.Add(handle);

			var asset = await handle.Task;

			if (handle.Status != AsyncOperationStatus.Succeeded || asset == null)
			{
				throw new InvalidOperationException(
					$"Failed to load Addressables asset of type '{typeof(T).Name}' at address '{path}'" +
					(handle.OperationException != null ? $": {handle.OperationException.Message}" : "."));
			}

			return asset;
		}

		public void Dispose()
		{
			foreach (var handle in _handles)
			{
				if (handle.IsValid())
				{
					Addressables.Release(handle);
				}
			}

			_handles.Clear();
		}
	}
}
