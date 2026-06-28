using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assembler.Parsing.Info;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assembler.Resolving
{
	public sealed class AssetRegistry : IDisposable
	{
		private readonly Dictionary<string, Object> _assets = new();

		// One loader instance per source, so a stateful loader (Addressables) accumulates all its handles in a
		// single place and releases them together on Dispose. Stateless loaders (Resources) are cached the same way.
		private readonly Dictionary<string, IAssetLoader> _loaders = new();

		public async Task LoadAllAsync(IReadOnlyList<AssetInfo> assetInfos)
		{
			foreach (var assetInfo in assetInfos)
			{
				var loader = GetLoader(assetInfo.Source);

				Object asset = assetInfo switch
				{
					SpriteAssetInfo => await loader.LoadAsync<Sprite>(assetInfo.Path),
					AudioClipAssetInfo => await loader.LoadAsync<AudioClip>(assetInfo.Path),
					MeshAssetInfo => await loader.LoadAsync<Mesh>(assetInfo.Path),
					_ => throw new NotImplementedException($"Unknown asset info type: {assetInfo.GetType().Name}")
				};

				_assets[assetInfo.Id] = asset;
			}
		}

		public T Get<T>(string id)
		{
			if (!_assets.TryGetValue(id, out var asset))
			{
				throw new InvalidOperationException($"No asset registered with id '{id}'");
			}

			if (asset is T typed)
			{
				return typed;
			}

			throw new InvalidOperationException(
				$"Asset '{id}' is of type '{asset.GetType().Name}', not '{typeof(T).Name}'");
		}

		// Releases every loaded asset's underlying resource (Addressables handles); Resources-sourced assets need
		// no release. Tied to game teardown via AssetRegistryOwner so destroying the game root frees all handles.
		public void Dispose()
		{
			foreach (var disposable in _loaders.Values.OfType<IDisposable>())
			{
				disposable.Dispose();
			}

			_loaders.Clear();
			_assets.Clear();
		}

		// Caches one loader per source so stateful loaders can track and release the handles they produce.
		// Unknown sources throw — adding a source means adding a case here and (if it holds resources) IDisposable.
		public IAssetLoader GetLoader(string source)
		{
			if (_loaders.TryGetValue(source, out var existing))
			{
				return existing;
			}

			IAssetLoader loader = source switch
			{
				"resources" => new ResourcesAssetLoader(),
				"addressables" => new AddressablesAssetLoader(),
				_ => throw new NotImplementedException($"Unknown asset source: '{source}'")
			};

			_loaders[source] = loader;
			return loader;
		}
	}
}
