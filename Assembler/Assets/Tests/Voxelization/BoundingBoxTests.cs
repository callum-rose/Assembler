using System.Linq;
using System.Threading;
using Assembler.Voxelization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	public sealed class BoundingBoxTests
	{
		[Test]
		public void ManifestYaml_RoundTripsBoundingBoxFields()
		{
			var manifest = ManifestYaml.Read(ManifestYaml.Write(new SetManifest
			{
				Game = "g",
				Unit = 1f,
				Assets = new[]
				{
					new ManifestAsset { Id = "car", RealWorldHeight = 6f, Length = 14f, Width = 7f, Tolerance = 2 },
				},
			}));

			var car = manifest.Assets.Single();
			Assert.That(car.RealWorldHeight, Is.EqualTo(6f));
			Assert.That(car.Length, Is.EqualTo(14f));
			Assert.That(car.Width, Is.EqualTo(7f));
			Assert.That(car.Tolerance, Is.EqualTo(2));
		}

		[Test]
		public void ManifestYaml_StillReadsTheLegacyHeightKey()
		{
			var manifest = ManifestYaml.Read("game: g\nunit: 1\nassets:\n  - id: a\n    real_world_height: 10\n");

			Assert.That(manifest.Assets.Single().RealWorldHeight, Is.EqualTo(10f));
			Assert.That(manifest.Assets.Single().Length, Is.EqualTo(0f), "unspecified extents stay unconstrained");
			Assert.That(manifest.Assets.Single().Tolerance, Is.EqualTo(1));
		}

		[Test]
		public void WideCarPlan_IsRejected_LengthMustRunAlongZ()
		{
			// The recurring failure: 11 wide (x) x 7 long (z) against a car that
			// must be 7 wide x 11 long. Both axes miss their targets.
			var model = CarModel(targetLength: 11, targetWidth: 7,
				Planned("body", new Vector3Int(11, 4, 7), new Vector3Int(-5, 0, -3)));

			var errors = PlanGeometryChecks.Errors(model);

			Assert.That(errors.Any(e => e.Contains("long") && e.Contains("must be 11")), Is.True,
				string.Join("\n", errors));
			Assert.That(errors.Any(e => e.Contains("wide") && e.Contains("must be 7")), Is.True);
		}

		[Test]
		public void RightProportionedPlan_PassesTheBoxGate()
		{
			var model = CarModel(targetLength: 11, targetWidth: 7,
				Planned("body", new Vector3Int(7, 4, 11), new Vector3Int(-3, 0, -5)));

			Assert.That(PlanGeometryChecks.Errors(model), Is.Empty);
		}

		[Test]
		public void Tolerance_LoosensTheGate()
		{
			// 9 long vs target 11 fails at the default ±1 but passes at ±2.
			var strict = CarModel(targetLength: 11, targetWidth: 0,
				Planned("body", new Vector3Int(7, 4, 9), new Vector3Int(-3, 0, -4)));
			Assert.That(PlanGeometryChecks.Errors(strict), Is.Not.Empty);

			var loose = strict with { SizeTolerance = 2 };
			Assert.That(PlanGeometryChecks.Errors(loose), Is.Empty);
		}

		[Test]
		public void Validator_ReportsAWrongLengthOnTheComposedModel()
		{
			// A 2-wide, 2-tall, 1-deep block against a target of 4 long.
			var model = new VoxelRigModel
			{
				Id = "t",
				Unit = 1f,
				RealWorldHeight = 2f,
				TargetLength = 4,
				Palette = Palette,
				Parts = new[]
				{
					new VoxelPart
					{
						Id = "solo",
						Pivot = Vector3Int.zero,
						Data = new LayersPartData(new Vector3Int(2, 2, 1), Vector3Int.zero, new[] { "AA", "AA" }),
					},
				},
			};

			var assembled = new ModelAssembler(StubScriptRunner.Failing("no scripts expected"))
				.AssembleAsync(model, CancellationToken.None).GetAwaiter().GetResult();
			var report = new ModelValidator().Validate(assembled, ReferenceBrief.None);

			Assert.That(report.Issues.Any(i => i.Code == IssueCode.ScaleMismatch && i.Message.Contains("long")), Is.True,
				string.Join("\n", report.Issues));
		}

		[Test]
		public void Planner_AnchorsTheTargetBoxFromTheManifest()
		{
			var manifest = new SetManifest
			{
				Game = "test",
				Unit = 1f,
				Assets = new[]
				{
					new ManifestAsset { Id = "crate", RealWorldHeight = 2f, Length = 1f, Width = 2f, Tolerance = 2 },
				},
			};
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

			var plan = new ModelPlanner(gateway, VoxelizationConfig.Default)
				.PlanAsync(manifest, manifest.Assets[0], Assembler.Anthropic.AnthropicImage.None, ReferenceBrief.None, string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(plan.Skeleton.TargetLength, Is.EqualTo(1));
			Assert.That(plan.Skeleton.TargetWidth, Is.EqualTo(2));
			Assert.That(plan.Skeleton.SizeTolerance, Is.EqualTo(2));
		}

		[Test]
		public void VModelYaml_RoundTripsTheTargetBox()
		{
			var model = new VoxelRigModel
			{
				Id = "car",
				Unit = 1f,
				RealWorldHeight = 6f,
				TargetLength = 11,
				TargetWidth = 7,
				SizeTolerance = 2,
				Palette = Palette,
			};

			var read = VModelYaml.Read(VModelYaml.Write(model));

			Assert.That(read.TargetLength, Is.EqualTo(11));
			Assert.That(read.TargetWidth, Is.EqualTo(7));
			Assert.That(read.SizeTolerance, Is.EqualTo(2));
		}

		private static readonly PaletteEntry[] Palette =
		{
			new('A', new Color32(255, 0, 0, 255)),
		};

		private static VoxelRigModel CarModel(int targetLength, int targetWidth, params VoxelPart[] parts) => new()
		{
			Id = "car",
			Symmetry = "none",
			Unit = 1f,
			RealWorldHeight = 4f,
			TargetLength = targetLength,
			TargetWidth = targetWidth,
			Palette = Palette,
			Parts = parts,
		};

		private static VoxelPart Planned(string id, Vector3Int size, Vector3Int offset) => new()
		{
			Id = id,
			Parent = "root",
			Pivot = Vector3Int.zero,
			Data = new PlannedPartData(PartEncoding.Layers, size, offset, string.Empty),
		};
	}
}
