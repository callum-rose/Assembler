using System;
using System.Collections.Generic;
using Assembler.Parsing.Info;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assembler.Resolving
{
	public sealed class AssetRegistry
	{
		private readonly Dictionary<string, Object> _assets = new();

		public void LoadAll(IReadOnlyList<AssetInfo> assetInfos)
		{
			foreach (var assetInfo in assetInfos)
			{
				var loader = GetLoader(assetInfo.Source);

				Object asset = assetInfo switch
				{
					SpriteAssetInfo => loader.Load<Sprite>(assetInfo.Path),
					AudioClipAssetInfo => loader.Load<AudioClip>(assetInfo.Path),
					MeshAssetInfo => loader.Load<Mesh>(assetInfo.Path),
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

		private static IAssetLoader GetLoader(string source) =>
			source switch
			{
				"resources" => new ResourcesAssetLoader(),
				_ => throw new NotImplementedException($"Unknown asset source: '{source}'")
			};
	}
}
