using System.Threading;
using Assembler.Anthropic;
using Assembler.Voxelization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	public sealed class StyleGuidanceTests
	{
		private const string Guidance = "prefer rounded boxes; only as many parts as needed";

		private static readonly VoxelizationConfig Config = VoxelizationConfig.Default with
		{
			StyleGuidance = Guidance,
		};

		private static readonly SetManifest Manifest = new()
		{
			Game = "test",
			Unit = 1f,
			Assets = new[] { new ManifestAsset { Id = "crate", RealWorldHeight = 2f } },
		};

		[Test]
		public void Planner_ReceivesTheGlobalStyleGuidance()
		{
			var gateway = new FakeGateway().Enqueue(@"```vmodel
model: crate
version: 1
rigged: false
unit: 1
real_world_height: 99
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

			new ModelPlanner(gateway, Config)
				.PlanAsync(Manifest, Manifest.Assets[0], AnthropicImage.None, ReferenceBrief.None, string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(gateway.Calls[0].Messages[0].Content, Does.Contain(Guidance));
		}

		[Test]
		public void PartAuthor_ReceivesTheGlobalStyleGuidance()
		{
			var model = new VoxelRigModel
			{
				Id = "crate",
				Unit = 1f,
				RealWorldHeight = 2f,
				Palette = new[] { new PaletteEntry('W', new Color32(170, 119, 51, 255)) },
				Parts = new[]
				{
					new VoxelPart
					{
						Id = "box",
						Pivot = Vector3Int.zero,
						Data = new PlannedPartData(PartEncoding.Layers, new Vector3Int(2, 2, 1), Vector3Int.zero, "crate"),
					},
				},
			};
			var gateway = new FakeGateway().Enqueue("```layers\nWW\n\nWW\n```");

			new PartAuthor(gateway, Config)
				.AuthorAsync(model, ReferenceBrief.None, model.Parts[0], (PlannedPartData)model.Parts[0].Data, string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(gateway.Calls[0].Messages[0].Content, Does.Contain(Guidance));
		}
	}
}
