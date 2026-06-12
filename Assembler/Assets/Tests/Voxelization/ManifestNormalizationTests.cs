using System.Linq;
using System.Threading;
using Assembler.Voxelization;
using NUnit.Framework;

namespace Tests.Voxelization
{
	public sealed class ManifestNormalizationTests
	{
		[Test]
		public void GeneratedManifests_AlwaysComeOutAtUnitOne_PreservingVoxelHeights()
		{
			// A metres-style manifest (the old convention): 1.8m at 0.18m/voxel
			// = 10 voxels. Normalization must keep that 10.
			var gateway = new FakeGateway().Enqueue(
				"```yaml\ngame: g\nunit: 0.18\nassets:\n  - id: villager\n    real_world_height: 1.8\n```");

			var manifest = new ManifestGenerator(gateway, VoxelizationConfig.Default)
				.GenerateAsync("a game", CancellationToken.None).GetAwaiter().GetResult();

			Assert.That(manifest.Unit, Is.EqualTo(1f));
			Assert.That(manifest.Assets.Single().RealWorldHeight, Is.EqualTo(10f));
			Assert.That(manifest.HeightInVoxels(manifest.Assets.Single()), Is.EqualTo(10));
		}

		[Test]
		public void UnitOneManifests_PassThroughUntouched()
		{
			var gateway = new FakeGateway().Enqueue(
				"```yaml\ngame: g\nunit: 1\nassets:\n  - id: car\n    real_world_height: 7\n```");

			var manifest = new ManifestGenerator(gateway, VoxelizationConfig.Default)
				.GenerateAsync("a game", CancellationToken.None).GetAwaiter().GetResult();

			Assert.That(manifest.Unit, Is.EqualTo(1f));
			Assert.That(manifest.Assets.Single().RealWorldHeight, Is.EqualTo(7f));
		}
	}
}
