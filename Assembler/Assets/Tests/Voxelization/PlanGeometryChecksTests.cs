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

		[Test]
		public void ChildDeclaredBeforeItsParent_IsRejected()
		{
			var model = Model(
				Planned("head", "torso", new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 1), new Vector3Int(0, 0, 0)),
				Planned("torso", "root", new Vector3Int(0, 0, 0), new Vector3Int(3, 2, 1), new Vector3Int(-1, 0, 0)));
			var errors = PlanGeometryChecks.Errors(model);

			Assert.That(errors.Single(), Does.Contain("declared before its parent"));
		}

		[Test]
		public void WrongVerticalSpan_IsRejected()
		{
			// 4 voxels of boxes against a 2-voxel manifest height.
			var model = Model(Planned("column", "root", new Vector3Int(0, 0, 0), new Vector3Int(1, 4, 1), new Vector3Int(0, 0, 0)));
			var errors = PlanGeometryChecks.Errors(model);

			Assert.That(errors.Single(), Does.Contain("tall").And.Contain("must be 2"));
		}

		[Test]
		public void GeometryFloatingAboveTheGround_IsRejected()
		{
			var model = Model(Planned("column", "root", new Vector3Int(0, 2, 0), new Vector3Int(1, 2, 1), new Vector3Int(0, 0, 0)));
			var errors = PlanGeometryChecks.Errors(model);

			Assert.That(errors.Single(), Does.Contain("y=0"));
		}

		[Test]
		public void SilhouetteFeasibility_RejectsPlansWhoseBoxesCannotCoverTheReference()
		{
			// A single 1-wide column of boxes vs a fully solid 3-wide reference:
			// the side columns are unreachable by any authoring.
			var model = Model(Planned("column", "root", new Vector3Int(0, 0, 0), new Vector3Int(1, 4, 1), new Vector3Int(0, 0, 0)))
				with
			{ TargetHeight = 4 };
			var brief = FullSilhouetteBrief();

			var error = PlanGeometryChecks.SilhouetteFeasibilityError(model, brief, 0.9f);

			Assert.That(error, Does.Contain("REFERENCE SILHOUETTE"));
		}

		[Test]
		public void SilhouetteFeasibility_AcceptsPlansWhoseBoxesCoverTheReference()
		{
			var model = Model(Planned("slab", "root", new Vector3Int(0, 0, 0), new Vector3Int(3, 4, 1), new Vector3Int(-1, 0, 0)))
				with
			{ TargetHeight = 4 };

			Assert.That(PlanGeometryChecks.SilhouetteFeasibilityError(model, FullSilhouetteBrief(), 0.9f), Is.Null);
		}

		[Test]
		public void SilhouetteFeasibility_ToleratesBlobNoise_WhenTheWidthIsRight()
		{
			// Vision silhouettes tend to read gaps as solid. A right-width plan
			// missing a few such cells (85% coverage here) must NOT be rejected.
			var model = Model(
					Planned("slab", "root", new Vector3Int(0, 1, 0), new Vector3Int(5, 3, 1), new Vector3Int(-2, 0, 0)),
					Planned("stub", "root", new Vector3Int(0, 0, 0), new Vector3Int(2, 1, 1), new Vector3Int(-2, 0, 0)))
				with
			{ TargetHeight = 4 };
			var brief = new ReferenceBrief
			{
				Source = "ref.png",
				Silhouettes = new[] { new SilhouetteSpec("front", new Vector3Int(5, 4, 0), new[] { "#####", "#####", "#####", "#####" }) },
			};

			Assert.That(PlanGeometryChecks.SilhouetteFeasibilityError(model, brief, 0.8f), Is.Null);
		}

		[Test]
		public void SilhouetteFeasibility_IsFaceAware_PassesFrontButFailsLeft()
		{
			// A thin slab spanning the full width (x) and height but only 1 deep (z).
			// The front view (u=x) is fully covered; the left view (u=z) is a 1-wide
			// column against a 3-deep solid reference, so left is infeasible.
			var model = Model(Planned("slab", "root", new Vector3Int(0, 0, 0), new Vector3Int(3, 4, 1), new Vector3Int(-1, 0, 0)))
				with
			{ TargetHeight = 4 };

			var front = new ReferenceBrief
			{
				Source = "ref.png",
				Silhouettes = new[] { new SilhouetteSpec("front", new Vector3Int(3, 4, 0), new[] { "###", "###", "###", "###" }) },
			};
			Assert.That(PlanGeometryChecks.SilhouetteFeasibilityError(model, front, 0.9f), Is.Null);

			var left = new ReferenceBrief
			{
				Source = "ref.png",
				Silhouettes = new[] { new SilhouetteSpec("left", new Vector3Int(3, 4, 0), new[] { "###", "###", "###", "###" }) },
			};
			Assert.That(PlanGeometryChecks.SilhouetteFeasibilityError(model, left, 0.9f), Does.Contain("left"));
		}

		[Test]
		public void SilhouetteFeasibility_DoesNotRejectAManifestPinnedAxisForADifferingSilhouetteAspect()
		{
			// The vision model read a side view 12 cells long, but the manifest pins
			// the length to 6 voxels (TargetLength). With the axis constrained, the
			// overall span is the bounding-box checks' job — feasibility must judge
			// SHAPE in the plan's own frame, not reject the proportion mismatch (this
			// is the dog-side-view failure: a 22-cell read against a 16-voxel length).
			var model = Model(Planned("body", "root", new Vector3Int(0, 0, 0), new Vector3Int(3, 4, 6), new Vector3Int(-1, 0, 0)))
				with
			{ TargetHeight = 4, TargetLength = 6 };
			var left = new ReferenceBrief
			{
				Source = "ref.png",
				Silhouettes = new[] { new SilhouetteSpec("left", new Vector3Int(12, 4, 0), new[] { "############", "############", "############", "############" }) },
			};

			Assert.That(PlanGeometryChecks.SilhouetteFeasibilityError(model, left, 0.9f), Is.Null);
		}

		[Test]
		public void SilhouetteFeasibility_SkipsTopAndBottomFaces()
		{
			// Top/bottom have no height anchor — the plan-stage pre-check must ignore
			// them (they are enforced solely by the validator's IoU gate).
			var model = Model(Planned("slab", "root", new Vector3Int(0, 0, 0), new Vector3Int(3, 4, 1), new Vector3Int(-1, 0, 0)))
				with
			{ TargetHeight = 4 };
			var brief = new ReferenceBrief
			{
				Source = "ref.png",
				Silhouettes = new[] { new SilhouetteSpec("top", new Vector3Int(9, 9, 0), new[] { "#########" }) },
			};

			Assert.That(PlanGeometryChecks.SilhouetteFeasibilityError(model, brief, 0.9f), Is.Null);
		}

		private static ReferenceBrief FullSilhouetteBrief() => new()
		{
			Source = "ref.png",
			Silhouettes = new[] { new SilhouetteSpec("front", new Vector3Int(3, 4, 0), new[] { "###", "###", "###", "###" }) },
		};

		private static VoxelRigModel Model(params VoxelPart[] parts) => new()
		{
			Id = "t",
			Symmetry = "bilateral",
			TargetHeight = 2,
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
