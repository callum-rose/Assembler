using System;
using Assembler.Resolving;
using NUnit.Framework;

namespace Tests.Resolving
{
	public class AssetRegistryLoaderTests
	{
		[Test]
		public void GetLoader_Resources_ReturnsResourcesLoader() =>
			Assert.That(new AssetRegistry().GetLoader("resources"), Is.TypeOf<ResourcesAssetLoader>());

		[Test]
		public void GetLoader_Addressables_ReturnsAddressablesLoader() =>
			Assert.That(new AssetRegistry().GetLoader("addressables"), Is.TypeOf<AddressablesAssetLoader>());

		[Test]
		public void GetLoader_UnknownSource_Throws() =>
			Assert.Throws<NotImplementedException>(() => new AssetRegistry().GetLoader("nope"));

		// Each source resolves to a single cached loader instance, so a stateful loader (Addressables) can pool
		// every handle it produces in one place for release on Dispose.
		[Test]
		public void GetLoader_SameSource_ReturnsCachedInstance()
		{
			var registry = new AssetRegistry();
			Assert.That(registry.GetLoader("addressables"), Is.SameAs(registry.GetLoader("addressables")));
		}
	}
}
