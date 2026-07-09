using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assembler.Resolving
{
	public sealed class ResourcesAssetLoader : IAssetLoader
	{
		// Resources has no genuinely asynchronous load path, so this completes synchronously and honours the
		// single async interface by returning an already-completed task.
		public Task<T> LoadAsync<T>(string path) where T : Object
		{
			var pathWithoutExtension = Path.ChangeExtension(path, null);
			var asset = Resources.Load<T>(pathWithoutExtension);

			if (asset == null)
			{
				throw new InvalidOperationException($"Failed to load asset of type '{typeof(T).Name}' at path '{pathWithoutExtension}'");
			}

			return Task.FromResult(asset);
		}
	}
}
