using System.Linq;
using Assembler.Voxelization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	public sealed class PlanGeometryChecksTests
	{
		[Test]
		public void Villager_HasNoGeometryErrors() =>
			Assert.That(PlanGeometryChecks.Errors(VillagerFixture.Build()), Is.Empty);

		[Test]
		public void NonBilateralModels_AreNotChecked()
		{
			// An even-width centre part is fine when no symmetry is declared.
			var model = Model(Planned("torso", "root", new Vector3Int(0, 0, 0), new Vector3Int(4, 2, 1), new Vector3Int(-2, 0, 0)))
				with
			{ Symmetry = "none" };

			Assert.That(PlanGeometryChecks.Errors(model), Is.Empty);
		}

		[Test]
		public void EvenWidthCentrePart_IsRejected()
		{
			var model = Model(Planned("torso", "root", new Vector3Int(0, 0, 0), new Vector3Int(4, 2, 1), new Vector3Int(-2, 0, 0)));
			var errors = PlanGeometryChecks.Errors(model);

			Assert.That(errors.Single(), Does.Contain("even width"));
		}

		[Test]
		public void OffCentreCentrePart_IsRejected()
		{
			var model = Model(Planned("torso", "root", new Vector3Int(0, 0, 0), new Vector3Int(3, 2, 1), new Vector3Int(0, 0, 0)));
			var errors = PlanGeometryChecks.Errors(model);

			Assert.That(errors.Single(), Does.Contain("offset.x=-1"));
		}

		[Test]
		public void SidePartWithoutMirrorTwin_IsRejected()
		{
			var model = Model(
				Planned("torso", "root", new Vector3Int(0, 0, 0), new Vector3Int(3, 2, 1), new Vector3Int(-1, 0, 0)),
				Planned("arm.L", "torso", new Vector3Int(-2, 1, 0), new Vector3Int(1, 2, 1), new Vector3Int(0, -1, 0)));
			var errors = PlanGeometryChecks.Errors(model);

			Assert.That(errors.Single(), Does.Contain("mirror twin"));
		}

		[Test]
		public void MirrorAtTheWrongPivot_IsRejected()
		{
			var model = Model(
				Planned("torso", "root", new Vector3Int(0, 0, 0), new Vector3Int(3, 2, 1), new Vector3Int(-1, 0, 0)),
				Planned("arm.L", "torso", new Vector3Int(-2, 1, 0), new Vector3Int(1, 2, 1), new Vector3Int(0, -1, 0)),
				new VoxelPart
				{
					Id = "arm.R",
					Parent = "torso",
					Pivot = new Vector3Int(3, 1, 0), // reflection of arm.L is x=2
					Data = new MirrorPartData("arm.L", MirrorAxis.X),
				});
			var errors = PlanGeometryChecks.Errors(model);

			Assert.That(errors.Single(), Does.Contain("reflection"));
		}

		[Test]
		public void MirrorAcrossANonXAxis_IsRejected()
		{
			var model = Model(
				Planned("torso", "root", new Vector3Int(0, 0, 0), new Vector3Int(3, 2, 1), new Vector3Int(-1, 0, 0)),
				new VoxelPart
				{
					Id = "twin",
					Parent = "root",
					Pivot = new Vector3Int(0, 2, 0),
					Data = new MirrorPartData("torso", MirrorAxis.Y),
				});
			var errors = PlanGeometryChecks.Errors(model);

			Assert.That(errors.Single(), Does.Contain("mirror across x"));
		}

		private static VoxelRigModel Model(params VoxelPart[] parts) => new()
		{
			Id = "t",
			Symmetry = "bilateral",
			Unit = 1f,
			RealWorldHeight = 2f,
			Palette = new[] { new PaletteEntry('A', new Color32(255, 0, 0, 255)) },
			Parts = parts,
		};

		private static VoxelPart Planned(string id, string parent, Vector3Int pivot, Vector3Int size, Vector3Int offset) => new()
		{
			Id = id,
			Parent = parent,
			Pivot = pivot,
			Data = new PlannedPartData(PartEncoding.Layers, size, offset, string.Empty),
		};
	}
}
