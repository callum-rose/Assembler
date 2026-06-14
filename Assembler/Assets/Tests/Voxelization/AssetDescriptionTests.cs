using System.Linq;
using System.Threading;
using Assembler.Anthropic;
using Assembler.Voxelization;
using NUnit.Framework;

namespace Tests.Voxelization
{
	public sealed class AssetDescriptionTests
	{
		private const string Description = "bright green boxy car, dark wheels, crossy-road minimal style";

		[Test]
		public void ManifestYaml_RoundTripsTheDescription()
		{
			var manifest = ManifestYaml.Read(ManifestYaml.Write(new SetManifest
			{
				Game = "g",
				Assets = new[]
				{
					new ManifestAsset { Id = "car", Description = Description, Height = 6 },
				},
			}));

			Assert.That(manifest.Assets.Single().Description, Is.EqualTo(Description));
		}

		[Test]
		public void Planner_ReceivesTheDescription_AndAnchorsItOnTheModel()
		{
			var manifest = new SetManifest
			{
				Game = "test",
				Assets = new[]
				{
					new ManifestAsset { Id = "crate", Description = Description, Height = 2 },
				},
			};
			var gateway = new FakeGateway().Enqueue(@"```vmodel
model: crate
version: 1
rigged: false
target_height: 99
origin: feet_center
palette:
  _: none
  W: ""#aa7733""
parts:
  - id: box
    parent: root
    pivot: [0, 0, 0]
    data: { encoding: planned, planned: layers, size: [2, 2, 1], offset: [-1, 0, 0], note: ""crate"" }
poses:
```");

			var plan = new ModelPlanner(gateway, VoxelizationConfig.Default)
				.PlanAsync(manifest, manifest.Assets[0], AnthropicImage.None, ReferenceBrief.None, string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(gateway.Calls[0].Messages[0].Content, Does.Contain(Description));
			Assert.That(plan.Skeleton.Description, Is.EqualTo(Description));
		}

		[Test]
		public void VModelYaml_RoundTripsTheDescription()
		{
			var model = new VoxelRigModel
			{
				Id = "car",
				Description = Description,
				TargetHeight = 6,
				Palette = new[] { new PaletteEntry('A', new UnityEngine.Color32(255, 0, 0, 255)) },
			};

			Assert.That(VModelYaml.Read(VModelYaml.Write(model)).Description, Is.EqualTo(Description));
		}
	}
}
