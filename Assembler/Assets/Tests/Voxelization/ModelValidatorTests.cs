using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Assembler.Voxelization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	public sealed class ModelValidatorTests
	{
		private static AssembledModel Assemble(VoxelRigModel model) =>
			new ModelAssembler(StubScriptRunner.Failing("no scripts expected"))
				.AssembleAsync(model, CancellationToken.None).GetAwaiter().GetResult();

		private static ValidationReport Validate(VoxelRigModel model, ReferenceBrief? brief = null) =>
			new ModelValidator().Validate(Assemble(model), brief ?? ReferenceBrief.None);

		[Test]
		public void Villager_ValidatesClean()
		{
			var report = Validate(VillagerFixture.Build());
			Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues));
		}

		[Test]
		public void ScaleDrift_IsReported()
		{
			// 1.8m at 0.18m/voxel demands 10 voxels; this model is 4 tall.
			var model = SinglePartModel(new[] { "A", "A", "A", "A" }, new Vector3Int(1, 4, 1));
			var report = Validate(model);

			Assert.That(report.Issues.Any(i => i.Code == IssueCode.ScaleMismatch), Is.True);
		}

		[Test]
		public void FloatingChunkInsideAPart_IsReported()
		{
			// Two voxels separated by a gap on y.
			var model = SinglePartModel(new[] { "A", ".", "A" }, new Vector3Int(1, 3, 1), realWorldHeight: 0.54f);
			var report = Validate(model);

			Assert.That(report.Issues.Any(i => i.Code == IssueCode.FloatingChunk && i.PartId == "solo"), Is.True);
		}

		[Test]
		public void ChildNotTouchingParent_IsReported()
		{
			var palette = new[] { new PaletteEntry('A', new Color32(255, 0, 0, 255)) };
			var model = new VoxelRigModel
			{
				Id = "t",
				Unit = 0.18f,
				RealWorldHeight = 0.36f,
				Palette = palette,
				Parts = new[]
				{
					new VoxelPart
					{
						Id = "base",
						Pivot = Vector3Int.zero,
						Data = new LayersPartData(new Vector3Int(1, 1, 1), Vector3Int.zero, new[] { "A" }),
					},
					new VoxelPart
					{
						Id = "floater",
						Parent = "base",
						// Pivot 5 up: the child's voxel sits at world y=5, nowhere near the base.
						Pivot = new Vector3Int(0, 5, 0),
						Data = new LayersPartData(new Vector3Int(1, 1, 1), Vector3Int.zero, new[] { "A" }),
					},
				},
			};

			var report = Validate(model);

			Assert.That(report.Issues.Any(i => i.Code == IssueCode.DisconnectedPart && i.PartId == "floater"), Is.True);
		}

		[Test]
		public void ScriptExceedingDeclaredSize_IsReported()
		{
			var palette = new[] { new PaletteEntry('A', new Color32(255, 0, 0, 255)) };
			var model = new VoxelRigModel
			{
				Id = "t",
				Unit = 1f,
				RealWorldHeight = 2f,
				Palette = palette,
				Parts = new[]
				{
					new VoxelPart
					{
						Id = "p",
						Pivot = Vector3Int.zero,
						// Declares 1x1x1 but the "script" produces a 1x2x1 column.
						Data = new ScriptPartData(Vector3Int.one, Vector3Int.zero, "stub"),
					},
				},
			};

			var grid = LayersCodec.ToModel(new Dictionary<Vector3Int, byte>
			{
				[new Vector3Int(0, 0, 0)] = 1,
				[new Vector3Int(0, 1, 0)] = 1,
			}, palette);

			var assembled = new ModelAssembler(new StubScriptRunner(_ => grid))
				.AssembleAsync(model, CancellationToken.None).GetAwaiter().GetResult();
			var report = new ModelValidator().Validate(assembled, ReferenceBrief.None);

			Assert.That(report.Issues.Any(i => i.Code == IssueCode.SizeExceeded && i.PartId == "p"), Is.True);
		}

		[Test]
		public void BriefPaletteMismatch_IsReported()
		{
			var model = SinglePartModel(new[] { "A" }, new Vector3Int(1, 1, 1), realWorldHeight: 0.18f);
			var brief = new ReferenceBrief
			{
				Source = "ref.png",
				Palette = new[] { new PaletteEntry('Z', new Color32(0, 0, 0, 255)) },
			};

			var report = Validate(model, brief);

			Assert.That(report.Issues.Any(i => i.Code == IssueCode.PaletteMismatch), Is.True);
		}

		[Test]
		public void SilhouetteMatch_PassesWhenExact_FailsWhenDifferent()
		{
			// A 2-wide, 2-tall front-facing block.
			var model = SinglePartModel(new[] { "AA", "AA" }, new Vector3Int(2, 2, 1), realWorldHeight: 0.36f);

			var matching = new ReferenceBrief
			{
				Source = "ref.png",
				Silhouette = new SilhouetteSpec("front", new Vector3Int(2, 2, 0), new[] { "##", "##" }),
			};
			Assert.That(Validate(model, matching).Issues.Any(i => i.Code == IssueCode.SilhouetteMismatch), Is.False);

			var mismatching = new ReferenceBrief
			{
				Source = "ref.png",
				Silhouette = new SilhouetteSpec("front", new Vector3Int(2, 2, 0), new[] { "#.", ".." }),
			};
			Assert.That(Validate(model, mismatching).Issues.Any(i => i.Code == IssueCode.SilhouetteMismatch), Is.True);
		}

		private static VoxelRigModel SinglePartModel(string[] layers, Vector3Int size, float realWorldHeight = 1.8f) => new()
		{
			Id = "t",
			Unit = 0.18f,
			RealWorldHeight = realWorldHeight,
			Palette = new[] { new PaletteEntry('A', new Color32(255, 0, 0, 255)) },
			Parts = new[]
			{
				new VoxelPart
				{
					Id = "solo",
					Pivot = Vector3Int.zero,
					Data = new LayersPartData(size, Vector3Int.zero, layers),
				},
			},
		};
	}
}
