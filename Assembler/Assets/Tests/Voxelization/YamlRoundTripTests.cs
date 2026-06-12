using System.Linq;
using Assembler.Voxelization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	public sealed class YamlRoundTripTests
	{
		[Test]
		public void VModel_WriteReadWrite_IsStable()
		{
			var model = VillagerFixture.Build();

			var written = VModelYaml.Write(model);
			var read = VModelYaml.Read(written);
			var rewritten = VModelYaml.Write(read);

			Assert.That(rewritten, Is.EqualTo(written));
		}

		[Test]
		public void VModel_ReadRecoversStructure()
		{
			var read = VModelYaml.Read(VModelYaml.Write(VillagerFixture.Build()));

			Assert.That(read.Id, Is.EqualTo("villager"));
			Assert.That(read.Rigged, Is.True);
			Assert.That(read.Symmetry, Is.EqualTo("bilateral"));
			Assert.That(read.HeightInVoxels, Is.EqualTo(10));
			Assert.That(read.Palette.Select(p => p.Key), Is.EqualTo(new[] { 'S', 'B', 'K' }));
			Assert.That(read.Parts.Count, Is.EqualTo(6));
			Assert.That(read.FindPart("arm.R")!.Data, Is.InstanceOf<MirrorPartData>());
			Assert.That(read.Poses.Single(p => p.Name == "wave").Rotations["arm.R"], Is.EqualTo(new Vector3(0f, 0f, -160f)));

			var torso = (LayersPartData)read.FindPart("torso")!.Data;
			Assert.That(torso.Size, Is.EqualTo(new Vector3Int(3, 4, 2)));
			Assert.That(torso.Offset, Is.EqualTo(new Vector3Int(-1, 0, -1)));
			Assert.That(torso.Layers.Count, Is.EqualTo(4));
			Assert.That(torso.Layers[0], Is.EqualTo("BBB\nBBB"));
		}

		[Test]
		public void VModel_MirrorPivotIsDerivedWhenOmitted()
		{
			const string yaml = @"model: t
unit: 1
real_world_height: 2
palette:
  A: ""#ff0000""
parts:
  - id: left
    parent: root
    pivot: [-3, 1, 2]
    data:
      encoding: layers
      size: [1, 1, 1]
      layers:
        - A
  - id: right
    parent: root
    loose: true
    mirror: { source: left, axis: x }
";

			var model = VModelYaml.Read(yaml);

			Assert.That(model.FindPart("right")!.Pivot, Is.EqualTo(new Vector3Int(3, 1, 2)));
			Assert.That(model.FindPart("right")!.Loose, Is.True);
			Assert.That(model.FindPart("left")!.Loose, Is.False);
		}

		[Test]
		public void VModel_ScriptPartRoundTrips()
		{
			var model = new VoxelRigModel
			{
				Id = "tree",
				Unit = 0.18f,
				RealWorldHeight = 9f,
				Palette = new[] { new PaletteEntry('T', new Color32(0x6b, 0x4a, 0x2b, 0xff)) },
				Parts = new[]
				{
					new VoxelPart
					{
						Id = "trunk",
						Pivot = Vector3Int.zero,
						Data = new ScriptPartData(
							new Vector3Int(5, 20, 5),
							new Vector3Int(-2, 0, -2),
							"var trunk = b.Hex(\"#6b4a2b\");\nfor (int y = 0; y < 14; y++)\n{\n    b.Set(0, y, 0, trunk);\n}\nreturn b.Build();"),
					},
				},
			};

			var read = VModelYaml.Read(VModelYaml.Write(model));
			var script = (ScriptPartData)read.FindPart("trunk")!.Data;

			Assert.That(script.Source, Is.EqualTo(((ScriptPartData)model.Parts[0].Data).Source));
			Assert.That(script.Size, Is.EqualTo(new Vector3Int(5, 20, 5)));
		}

		[Test]
		public void Manifest_RoundTrips()
		{
			var manifest = new SetManifest
			{
				Game = "medieval_village",
				Unit = 0.18f,
				Assets = new[]
				{
					new ManifestAsset { Id = "villager", RealWorldHeight = 1.8f, Symmetry = "bilateral", Rig = true, Reference = "villager.png" },
					new ManifestAsset { Id = "oak_tree", RealWorldHeight = 9f, Symmetry = "none" },
				},
			};

			var read = ManifestYaml.Read(ManifestYaml.Write(manifest));

			Assert.That(read.Game, Is.EqualTo("medieval_village"));
			Assert.That(read.Unit, Is.EqualTo(0.18f).Within(1e-5f));
			Assert.That(read.Assets.Count, Is.EqualTo(2));
			Assert.That(read.Assets[0].Reference, Is.EqualTo("villager.png"));
			Assert.That(read.Assets[1].HasReference, Is.False);
			Assert.That(read.HeightInVoxels(read.Assets[1]), Is.EqualTo(50));
		}

		[Test]
		public void ReferenceBrief_RoundTrips()
		{
			var brief = new ReferenceBrief
			{
				Source = "car_side.jpg",
				RealWorldDims = new RealWorldDims(1.4f, 1.8f, 4.5f),
				Palette = new[] { new PaletteEntry('R', new Color32(200, 30, 30, 255)) },
				Proportions = new System.Collections.Generic.Dictionary<string, float> { ["cabin"] = 0.4f },
				SignatureFeatures = new[] { "red body", "black wheels" },
				Silhouette = new SilhouetteSpec("side", new Vector3Int(25, 8, 0), new[] { ".####....", "#########" }),
			};

			var read = ReferenceBriefYaml.Read(ReferenceBriefYaml.Write(brief));

			Assert.That(read.Source, Is.EqualTo("car_side.jpg"));
			Assert.That(read.RealWorldDims.Depth, Is.EqualTo(4.5f).Within(1e-5f));
			Assert.That(read.Palette.Single().Key, Is.EqualTo('R'));
			Assert.That(read.Proportions["cabin"], Is.EqualTo(0.4f).Within(1e-5f));
			Assert.That(read.SignatureFeatures, Is.EqualTo(new[] { "red body", "black wheels" }));
			Assert.That(read.Silhouette.Face, Is.EqualTo("side"));
			Assert.That(read.Silhouette.Rows.Count, Is.EqualTo(2));
		}
	}
}
