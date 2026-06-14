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
			var model = SinglePartModel(new[] { "A", ".", "A" }, new Vector3Int(1, 3, 1), targetHeight: 3);
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
				TargetHeight = 2,
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
				TargetHeight = 2,
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
		public void ScriptBuiltAtTheOrigin_IsSnappedIntoTheDeclaredWindow()
		{
			var palette = new[] { new PaletteEntry('A', new Color32(255, 0, 0, 255)) };
			var model = new VoxelRigModel
			{
				Id = "t",
				TargetHeight = 2,
				Palette = palette,
				Parts = new[]
				{
					new VoxelPart
					{
						Id = "arm",
						Pivot = Vector3Int.zero,
						// Declared window: x 0..0, y -2..-1, z -1..0.
						Data = new ScriptPartData(new Vector3Int(1, 2, 2), new Vector3Int(0, -2, -1), "stub"),
					},
				},
			};

			// The "script" builds the right shape but at the origin, the classic
			// authoring mistake — it must be snapped into the window, not failed.
			var grid = LayersCodec.ToModel(new Dictionary<Vector3Int, byte>
			{
				[new Vector3Int(0, 0, 0)] = 1,
				[new Vector3Int(0, 1, 0)] = 1,
				[new Vector3Int(0, 0, 1)] = 1,
				[new Vector3Int(0, 1, 1)] = 1,
			}, palette);

			var assembled = new ModelAssembler(new StubScriptRunner(_ => grid))
				.AssembleAsync(model, CancellationToken.None).GetAwaiter().GetResult();
			var report = new ModelValidator().Validate(assembled, ReferenceBrief.None);

			Assert.That(report.Issues.Any(i => i.Code == IssueCode.SizeExceeded), Is.False,
				string.Join("\n", report.Issues));
			Assert.That(assembled.FindPart("arm")!.Grid.Voxels.Keys, Is.EquivalentTo(new[]
			{
				new Vector3Int(0, -2, -1),
				new Vector3Int(0, -1, -1),
				new Vector3Int(0, -2, 0),
				new Vector3Int(0, -1, 0),
			}));
		}

		[Test]
		public void BriefPaletteMismatch_IsReported()
		{
			var model = SinglePartModel(new[] { "A" }, new Vector3Int(1, 1, 1), targetHeight: 1);
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
			var model = SinglePartModel(new[] { "AA", "AA" }, new Vector3Int(2, 2, 1), targetHeight: 2);

			var matching = new ReferenceBrief
			{
				Source = "ref.png",
				Silhouettes = new[] { new SilhouetteSpec("front", new Vector3Int(2, 2, 0), new[] { "##", "##" }) },
			};
			Assert.That(Validate(model, matching).Issues.Any(i => i.Code == IssueCode.SilhouetteMismatch), Is.False);

			var mismatching = new ReferenceBrief
			{
				Source = "ref.png",
				Silhouettes = new[] { new SilhouetteSpec("front", new Vector3Int(2, 2, 0), new[] { "#.", ".." }) },
			};
			Assert.That(Validate(model, mismatching).Issues.Any(i => i.Code == IssueCode.SilhouetteMismatch), Is.True);
		}

		[Test]
		public void WrongTopView_IsReported_WhileFrontPasses()
		{
			// A 2x2x2 cube: its front projection is a full 2x2, but the top view here
			// claims a single cell. Front passes; top is flagged — proving the
			// validator gates each labelled face independently.
			var model = SinglePartModel(new[] { "AA\nAA", "AA\nAA" }, new Vector3Int(2, 2, 2), targetHeight: 2);
			var brief = new ReferenceBrief
			{
				Source = "ref.png",
				Silhouettes = new[]
				{
					new SilhouetteSpec("front", new Vector3Int(2, 2, 0), new[] { "##", "##" }),
					new SilhouetteSpec("top", new Vector3Int(2, 2, 0), new[] { "#.", ".." }),
				},
			};

			var mismatches = Validate(model, brief).Issues.Where(i => i.Code == IssueCode.SilhouetteMismatch).ToList();
			Assert.That(mismatches.Count, Is.EqualTo(1));
			Assert.That(mismatches.Single().Message, Does.Contain("top"));
		}

		[Test]
		public void ColourKeyedSilhouetteRows_CountAsSolid()
		{
			// Vision transcriptions sometimes use palette keys instead of '#'.
			// They must read as occupancy, not as an all-empty silhouette.
			var model = SinglePartModel(new[] { "AA", "AA" }, new Vector3Int(2, 2, 1), targetHeight: 2);
			var brief = new ReferenceBrief
			{
				Source = "ref.png",
				Silhouettes = new[] { new SilhouetteSpec("front", new Vector3Int(2, 2, 0), new[] { "WW", "WW" }) },
			};

			Assert.That(Validate(model, brief).Issues.Any(i => i.Code == IssueCode.SilhouetteMismatch), Is.False);
		}

		[Test]
		public void LoosePart_MaySplitIntoChunks()
		{
			var model = SinglePartModel(new[] { "A", ".", "A" }, new Vector3Int(1, 3, 1), targetHeight: 3, loose: true);
			var report = Validate(model);

			Assert.That(report.Issues.Any(i => i.Code == IssueCode.FloatingChunk), Is.False,
				"loose parts are allowed disconnected chunks");
		}

		[Test]
		public void BilateralAsymmetry_IsReported()
		{
			// Occupancy is lopsided in x: mirroring across the bbox centre plane
			// does not reproduce the model.
			var model = SinglePartModel(new[] { "A.A\nAA." }, new Vector3Int(3, 1, 2), targetHeight: 1, symmetry: "bilateral");
			var report = Validate(model);

			Assert.That(report.Issues.Any(i => i.Code == IssueCode.Asymmetric), Is.True);

			// The lopsided part sits on the mirror plane, so the issue must be
			// attributed to it — that's what lets the re-authoring loop target it.
			Assert.That(report.Issues.Any(i => i.Code == IssueCode.Asymmetric && i.PartId == "solo"), Is.True);
		}

		[Test]
		public void BilateralSymmetricModel_PassesTheSymmetryCheck()
		{
			var model = SinglePartModel(new[] { "AA", "AA" }, new Vector3Int(2, 2, 1), targetHeight: 2, symmetry: "bilateral");
			var report = Validate(model);

			Assert.That(report.Issues.Any(i => i.Code == IssueCode.Asymmetric), Is.False);
		}

		private static VoxelRigModel SinglePartModel(
			string[] layers,
			Vector3Int size,
			int targetHeight = 10,
			bool loose = false,
			string symmetry = "none") => new()
		{
			Id = "t",
			TargetHeight = targetHeight,
			Symmetry = symmetry,
			Palette = new[] { new PaletteEntry('A', new Color32(255, 0, 0, 255)) },
			Parts = new[]
			{
				new VoxelPart
				{
					Id = "solo",
					Pivot = Vector3Int.zero,
					Loose = loose,
					Data = new LayersPartData(size, Vector3Int.zero, layers),
				},
			},
		};
	}
}
