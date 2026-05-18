using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assembler.Resolving
{
	public sealed class ResourcesAssetLoader : IAssetLoader
	{
		public T Load<T>(string path) where T : Object
		{
			var pathWithoutExtension = Path.ChangeExtension(path, null);
			var asset = Resources.Load<T>(pathWithoutExtension);

			if (asset == null)
			{
				throw new InvalidOperationException($"Failed to load asset of type '{typeof(T).Name}' at path '{pathWithoutExtension}'");
			}

			return asset;
		}
	}
}