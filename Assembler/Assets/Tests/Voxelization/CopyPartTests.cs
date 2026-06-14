using System.Linq;
using System.Threading;
using Assembler.Voxelization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	public sealed class CopyPartTests
	{
		private static readonly PaletteEntry[] Palette =
		{
			new('A', new Color32(255, 0, 0, 255)),
		};

		[Test]
		public void VModelYaml_RoundTripsCopyParts()
		{
			var read = VModelYaml.Read(VModelYaml.Write(WheelModel()));

			var copy = (CopyPartData)read.Parts.Single(p => p.Id == "wheel.BL").Data;
			Assert.That(copy.Source, Is.EqualTo("wheel.FL"));
			Assert.That(read.Parts.Single(p => p.Id == "wheel.BL").Pivot, Is.EqualTo(new Vector3Int(0, 0, -3)));
		}

		[Test]
		public void Assembler_PlacesTheCopyAtItsOwnPivot()
		{
			var assembled = new ModelAssembler(StubScriptRunner.Failing("no scripts expected"))
				.AssembleAsync(WheelModel(), CancellationToken.None).GetAwaiter().GetResult();

			Assert.That(assembled.AssemblyIssues.IsValid, Is.True,
				string.Join("\n", assembled.AssemblyIssues.Issues));

			// Same local grid, different world cells: source at z=0, copy at z=-3.
			var world = assembled.Composed.Voxels.Keys.ToList();
			Assert.That(world, Does.Contain(new Vector3Int(0, 0, 0)));
			Assert.That(world, Does.Contain(new Vector3Int(0, 0, -3)));
		}

		[Test]
		public void CopyDeclaredBeforeItsSource_IsRejectedAtPlanTime()
		{
			var model = WheelModel() with
			{
				Parts = WheelModel().Parts.Reverse().ToArray(),
			};

			Assert.That(PlanGeometryChecks.Errors(model).Any(e => e.Contains("declared after its source")), Is.True);
		}

		[Test]
		public void CopyBoxes_CountTowardsTheVerticalExtent()
		{
			// Source spans y 0..1; the copy lifts the same box to y 2..3, stacking
			// to the 4-voxel manifest height. Without the copy's box the plan
			// would read as 2 tall and be rejected.
			var model = new VoxelRigModel
			{
				Id = "tower",
				TargetHeight = 4,
				Palette = Palette,
				Parts = new[]
				{
					Authored("base", Vector3Int.zero, new Vector3Int(1, 2, 1)),
					new VoxelPart
					{
						Id = "upper",
						Parent = "base",
						Pivot = new Vector3Int(0, 2, 0),
						Data = new CopyPartData("base"),
					},
				},
			};

			Assert.That(PlanGeometryChecks.Errors(model), Is.Empty);
		}

		[Test]
		public void Bilateral_OffCentreCopyWithoutAMirrorTwin_IsRejected()
		{
			var model = new VoxelRigModel
			{
				Id = "t",
				Symmetry = "bilateral",
				TargetHeight = 2,
				Palette = Palette,
				Parts = new[]
				{
					Authored("torso", Vector3Int.zero, new Vector3Int(3, 2, 1), new Vector3Int(-1, 0, 0)),
					Authored("arm.L", new Vector3Int(-2, 1, 0), new Vector3Int(1, 1, 1), parent: "torso"),
					new VoxelPart
					{
						Id = "arm.L2",
						Parent = "torso",
						Pivot = new Vector3Int(-2, 0, 0),
						Data = new CopyPartData("arm.L"),
					},
					new VoxelPart
					{
						Id = "arm.R",
						Parent = "torso",
						Pivot = new Vector3Int(2, 1, 0),
						Data = new MirrorPartData("arm.L", MirrorAxis.X),
					},
				},
			};

			var errors = PlanGeometryChecks.Errors(model);

			Assert.That(errors.Single(), Does.Contain("arm.L2").And.Contain("mirror twin"));
		}

		private static VoxelRigModel WheelModel() => new()
		{
			Id = "cart",
			TargetHeight = 1,
			Palette = Palette,
			Parts = new[]
			{
				Authored("wheel.FL", Vector3Int.zero, Vector3Int.one),
				new VoxelPart
				{
					Id = "wheel.BL",
					Parent = "root",
					Pivot = new Vector3Int(0, 0, -3),
					Data = new CopyPartData("wheel.FL"),
				},
			},
		};

		private static VoxelPart Authored(
			string id,
			Vector3Int pivot,
			Vector3Int size,
			Vector3Int? offset = null,
			string parent = "root") => new()
		{
			Id = id,
			Parent = parent,
			Pivot = pivot,
			Data = new LayersPartData(size, offset ?? Vector3Int.zero, FullLayers(size)),
		};

		private static string[] FullLayers(Vector3Int size) =>
			Enumerable.Range(0, size.y)
				.Select(_ => string.Join("\n", Enumerable.Range(0, size.z).Select(_ => new string('A', size.x))))
				.ToArray();
	}
}
