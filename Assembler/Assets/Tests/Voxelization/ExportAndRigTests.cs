using System.Linq;
using System.Threading;
using Assembler.Voxelization;
using Assembler.Voxels;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	public sealed class ExportAndRigTests
	{
		private static AssembledModel AssembleVillager() =>
			new ModelAssembler(StubScriptRunner.Failing("no scripts expected"))
				.AssembleAsync(VillagerFixture.Build(), CancellationToken.None).GetAwaiter().GetResult();

		[Test]
		public void Export_EmitsRigYamlPartVoxesFlattenedVoxAndPreviews()
		{
			var export = ModelExporter.Export(AssembleVillager());

			Assert.That(export.Files.Keys, Does.Contain("villager.vmodel.yaml"));
			Assert.That(export.Files.Keys, Does.Contain("villager.vox"));
			Assert.That(export.Files.Keys, Does.Contain("villager.goxel.txt"));
			Assert.That(export.Files.Keys, Does.Contain("preview_front.png"));
			Assert.That(export.Files.Keys, Does.Contain("preview_iso.png"));
			Assert.That(export.Files.Keys, Does.Contain("parts/torso.vox"));
			Assert.That(export.Files.Keys, Does.Contain("parts/arm.R.vox"));
		}

		[Test]
		public void PerPartVox_RoundTripsThroughVoxReader()
		{
			var assembled = AssembleVillager();
			var export = ModelExporter.Export(assembled);

			var torso = assembled.FindPart("torso")!.Grid;
			var read = VoxReader.Read(export.Files["parts/torso.vox"]);
			var backToYUp = VoxelGridConvert.SwapYZ(read);

			Assert.That(backToYUp.Voxels.Count, Is.EqualTo(torso.Voxels.Count));

			// VoxWriter translates the bbox min to zero, so compare shapes
			// relative to their own minimum.
			var expected = torso.Voxels.Keys.Select(p => p - torso.Min).ToHashSet();
			var actual = backToYUp.Voxels.Keys.Select(p => p - backToYUp.Min).ToHashSet();
			Assert.That(actual, Is.EquivalentTo(expected));

			// Colours survive the trip.
			var expectedColours = torso.Voxels
				.ToDictionary(kv => kv.Key - torso.Min, kv => torso.Palette[kv.Value - 1]);
			foreach (var kv in backToYUp.Voxels)
			{
				var colour = backToYUp.Palette[kv.Value - 1];
				var expectedColour = expectedColours[kv.Key - backToYUp.Min];
				Assert.That((Color)colour, Is.EqualTo((Color)expectedColour));
			}
		}

		[Test]
		public void FlattenedGoxelText_IsZUp()
		{
			var export = ModelExporter.Export(AssembleVillager());
			var text = System.Text.Encoding.UTF8.GetString(export.Files["villager.goxel.txt"]);
			var parsed = GoxelTextParser.Parse(text);

			// Y-up height 10 becomes a z extent of 10 in Goxel's Z-up text.
			Assert.That(parsed.Size.z, Is.EqualTo(10));
			Assert.That(parsed.Size.y, Is.EqualTo(2), "depth lands on y in Z-up storage");
		}

		[Test]
		public void RigInstantiator_BuildsPivotTreeAndAppliesPoses()
		{
			var assembled = AssembleVillager();
			var root = RigInstantiator.Instantiate(assembled, new VoxelMeshBuilder());
			try
			{
				var torso = root.transform.Find("torso");
				Assert.That(torso, Is.Not.Null);
				Assert.That(torso!.localPosition.y, Is.EqualTo(4f).Within(1e-5f));

				var armR = torso.Find("arm.R");
				Assert.That(armR, Is.Not.Null);
				Assert.That(armR!.localPosition.x, Is.EqualTo(2f).Within(1e-5f));
				Assert.That(armR.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));

				RigInstantiator.ApplyPose(root, assembled.Model, "wave");
				Assert.That(Quaternion.Angle(armR.localRotation, Quaternion.Euler(0f, 0f, -160f)), Is.LessThan(0.01f));

				RigInstantiator.ApplyPose(root, assembled.Model, "idle");
				Assert.That(Quaternion.Angle(armR.localRotation, Quaternion.identity), Is.LessThan(0.01f));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void MeshBuilder_CullsInteriorFaces()
		{
			var assembled = AssembleVillager();
			var torso = assembled.FindPart("torso")!.Grid;
			var mesh = new VoxelMeshBuilder().BuildMesh("torso", torso);

			// A solid 3x4x2 box has 52 exposed faces (2*(3*4) + 2*(4*2) + 2*(3*2)),
			// 4 vertices each — interior faces must not be emitted.
			Assert.That(mesh.vertexCount, Is.EqualTo(52 * 4));
		}
	}
}
