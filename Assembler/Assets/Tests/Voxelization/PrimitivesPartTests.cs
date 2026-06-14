using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Assembler.Voxelization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	public sealed class PrimitivesPartTests
	{
		private static readonly IReadOnlyList<PaletteEntry> Palette = new[]
		{
			new PaletteEntry('W', new Color32(170, 119, 51, 255)),
			new PaletteEntry('K', new Color32(0, 0, 0, 255)),
		};

		[Test]
		public void Box_FillsExactlyTheDeclaredCells()
		{
			var grid = Decode(new Vector3Int(3, 2, 2), Vector3Int.zero, "box W 0 0 0 3 2 2");

			Assert.That(grid.Voxels.Count, Is.EqualTo(12));
			Assert.That(grid.Min, Is.EqualTo(Vector3Int.zero));
			Assert.That(grid.Max, Is.EqualTo(new Vector3Int(2, 1, 1)));
		}

		[Test]
		public void RoundedBox_DropsCornersAndEdges_KeepsFaceCentres()
		{
			var grid = Decode(new Vector3Int(3, 3, 3), Vector3Int.zero, "box W 0 0 0 3 3 3 round 1");

			// A 3-cube rounded by 1 keeps the centre and the six face centres.
			Assert.That(grid.Voxels.Count, Is.EqualTo(7));
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(1, 1, 1)), Is.True);
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(0, 1, 1)), Is.True);
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(0, 0, 0)), Is.False, "corner survived the rounding");
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(0, 0, 1)), Is.False, "edge survived the rounding");
		}

		[Test]
		public void PerFaceRounding_RoundsOnlyTheSelectedFacesEdges()
		{
			var grid = Decode(new Vector3Int(3, 3, 3), Vector3Int.zero, "box W 0 0 0 3 3 3 round 1 +y");

			// Only the top face's rim is carved; everything below it stays square.
			Assert.That(grid.Voxels.Count, Is.EqualTo(19));
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(1, 2, 1)), Is.True, "top-face centre");
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(0, 2, 0)), Is.False, "top corner stayed");
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(1, 2, 0)), Is.False, "top edge stayed");
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(0, 0, 0)), Is.True, "bottom corner was rounded");
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(0, 1, 0)), Is.True, "vertical edge was rounded");
		}

		[Test]
		public void PerEdgeRounding_RoundsOnlyTheSelectedEdge()
		{
			var grid = Decode(new Vector3Int(3, 3, 3), Vector3Int.zero, "box W 0 0 0 3 3 3 round 1 +y+z");

			// The single +y/+z edge runs along x — just that line of three cells goes.
			Assert.That(grid.Voxels.Count, Is.EqualTo(24));
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(0, 2, 2)), Is.False);
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(1, 2, 2)), Is.False);
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(0, 2, 0)), Is.True, "a different top corner stayed");
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(1, 2, 1)), Is.True);
		}

		[Test]
		public void RoundingTwoEdgesOnlyTwoVoxelsApart_IsRejected()
		{
			// A box two voxels thick in z: rounding all edges would collapse the
			// x/y faces and shrink the box, so it is banned.
			var ex = Assert.Throws<FormatException>(() =>
				Decode(new Vector3Int(3, 3, 2), Vector3Int.zero, "box W 0 0 0 3 3 2 round 1"));
			Assert.That(ex!.Message, Does.Contain("collapse"));
		}

		[Test]
		public void RoundingOneFaceOfAThinSlab_IsAllowedAndKeepsFullSize()
		{
			// Rounding a single major face's rim only rounds each side face's
			// near edge (not the far one), so nothing collapses and the slab
			// keeps its full 5x5x2 extent. (Rounding both major faces, or all
			// over, would pair up the side edges and is rejected.)
			var grid = Decode(new Vector3Int(5, 5, 2), Vector3Int.zero, "box W 0 0 0 5 5 2 round 1 +z");

			Assert.That(grid.Min, Is.EqualTo(Vector3Int.zero));
			Assert.That(grid.Max, Is.EqualTo(new Vector3Int(4, 4, 1)));
		}

		[Test]
		public void RoundingBothMajorFacesOfAThinSlab_IsRejected()
		{
			// +z and -z together round both edges of every side face, which are
			// only two voxels apart — so the side faces would collapse.
			var ex = Assert.Throws<FormatException>(() =>
				Decode(new Vector3Int(5, 5, 2), Vector3Int.zero, "box W 0 0 0 5 5 2 round 1 +z -z"));
			Assert.That(ex!.Message, Does.Contain("collapse"));
		}

		[Test]
		public void Round_WithoutARadius_Throws()
		{
			var ex = Assert.Throws<FormatException>(() =>
				Decode(new Vector3Int(2, 2, 2), Vector3Int.zero, "box W 0 0 0 2 2 2 round"));
			Assert.That(ex!.Message, Does.Contain("radius"));
		}

		[Test]
		public void Round_WithAnUnknownSelector_Throws()
		{
			var ex = Assert.Throws<FormatException>(() =>
				Decode(new Vector3Int(3, 3, 3), Vector3Int.zero, "box W 0 0 0 3 3 3 round 1 +q"));
			Assert.That(ex!.Message, Does.Contain("face"));
		}

		[Test]
		public void Sphere_RasterizesSymmetrically()
		{
			var grid = Decode(new Vector3Int(5, 5, 5), Vector3Int.zero, "sphere W 2 2 2 2");

			Assert.That(grid.Voxels.Count, Is.EqualTo(33));
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(0, 2, 2)), Is.True);
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(2, 0, 2)), Is.True);
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(0, 0, 0)), Is.False);
		}

		[Test]
		public void Hemisphere_KeepsOnlyTheClippedSide()
		{
			var grid = Decode(new Vector3Int(5, 5, 5), Vector3Int.zero, "sphere W 2 2 2 2 half +y");

			Assert.That(grid.Voxels.Count, Is.EqualTo(23));
			Assert.That(grid.Voxels.Keys.All(p => p.y >= 2), Is.True);
		}

		[Test]
		public void Cylinder_RunsAlongItsAxis()
		{
			var grid = Decode(new Vector3Int(5, 4, 5), Vector3Int.zero, "cylinder W y 2 0 2 1.5 4");

			// 9 cells per disc (radius 1.5 catches the diagonals), 4 discs tall.
			Assert.That(grid.Voxels.Count, Is.EqualTo(36));
			Assert.That(grid.Voxels.Keys.Select(p => p.y).Distinct().Count(), Is.EqualTo(4));
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(1, 0, 1)), Is.True);
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(0, 0, 0)), Is.False);
		}

		[Test]
		public void FractionalCentre_CentresAnEvenShapeBetweenCells()
		{
			// A radius-2 sphere centred between cells spans 0..3 symmetrically.
			var grid = Decode(new Vector3Int(4, 4, 4), Vector3Int.zero, "sphere W 1.5 1.5 1.5 2");

			Assert.That(grid.Voxels.Keys.Min(p => p.x), Is.EqualTo(0));
			Assert.That(grid.Voxels.Keys.Max(p => p.x), Is.EqualTo(3));
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(0, 0, 0)), Is.False, "corner should round off");
		}

		[Test]
		public void ShapesAreClippedToTheDeclaredWindow()
		{
			var grid = Decode(new Vector3Int(2, 2, 2), Vector3Int.zero, "box W -3 -3 -3 10 10 10");

			Assert.That(grid.Voxels.Count, Is.EqualTo(8));
		}

		[Test]
		public void LaterShapesOverwriteEarlierOnes()
		{
			var grid = Decode(new Vector3Int(3, 1, 1), Vector3Int.zero,
				"box W 0 0 0 3 1 1",
				"box K 1 0 0 1 1 1");

			Assert.That(grid.Voxels[new Vector3Int(0, 0, 0)], Is.EqualTo(1));
			Assert.That(grid.Voxels[new Vector3Int(1, 0, 0)], Is.EqualTo(2));
		}

		[Test]
		public void Cut_RemovesVoxelsRatherThanColouringThem()
		{
			var grid = Decode(new Vector3Int(3, 1, 1), Vector3Int.zero,
				"box W 0 0 0 3 1 1",
				"cut box 1 0 0 1 1 1");

			Assert.That(grid.Voxels.Count, Is.EqualTo(2));
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(1, 0, 0)), Is.False, "cut cell should be gone, not recoloured");
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(0, 0, 0)), Is.True);
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(2, 0, 0)), Is.True);
		}

		[Test]
		public void Cut_HollowsAShell_LeavingTheRind()
		{
			var grid = Decode(new Vector3Int(3, 3, 3), Vector3Int.zero,
				"box W 0 0 0 3 3 3",
				"cut box 1 1 1 1 1 1");

			// A 3-cube (27 cells) minus its single centre cell.
			Assert.That(grid.Voxels.Count, Is.EqualTo(26));
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(1, 1, 1)), Is.False);
		}

		[Test]
		public void Cut_ReusesShapeGeometry_SoSphereAndCylinderCarveToo()
		{
			var grid = Decode(new Vector3Int(5, 5, 5), Vector3Int.zero,
				"box W 0 0 0 5 5 5",
				"cut sphere 2 2 2 1");

			// The sphere of radius 1 around the centre carves the centre plus its
			// six face neighbours (7 cells) out of the 125-cell block.
			Assert.That(grid.Voxels.Count, Is.EqualTo(118));
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(2, 2, 2)), Is.False);
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(3, 2, 2)), Is.False);
		}

		[Test]
		public void Cut_OfEmptySpace_IsANoOp_AndLaterFillCanRefill()
		{
			var grid = Decode(new Vector3Int(3, 1, 1), Vector3Int.zero,
				"cut box 0 0 0 3 1 1",
				"box W 1 0 0 1 1 1");

			Assert.That(grid.Voxels.Count, Is.EqualTo(1));
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(1, 0, 0)), Is.True);
		}

		[Test]
		public void Cut_WithNoShape_Throws()
		{
			var ex = Assert.Throws<FormatException>(() =>
				Decode(new Vector3Int(1, 1, 1), Vector3Int.zero, "box W 0 0 0 1 1 1", "cut"));
			Assert.That(ex!.Message, Does.Contain("cut"));
		}

		[Test]
		public void CommentsAndBlankLines_AreIgnored()
		{
			var grid = Decode(new Vector3Int(1, 1, 1), Vector3Int.zero,
				"# the whole part",
				"",
				"box W 0 0 0 1 1 1 # one cell");

			Assert.That(grid.Voxels.Count, Is.EqualTo(1));
		}

		[Test]
		public void TheGrid_IsPlacedAtTheDeclaredOffset()
		{
			var grid = Decode(new Vector3Int(2, 1, 1), new Vector3Int(-1, 4, 0), "box W 0 0 0 2 1 1");

			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(-1, 4, 0)), Is.True);
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(0, 4, 0)), Is.True);
		}

		[Test]
		public void UnknownPaletteKey_Throws()
		{
			var ex = Assert.Throws<FormatException>(() =>
				Decode(new Vector3Int(1, 1, 1), Vector3Int.zero, "box Z 0 0 0 1 1 1"));
			Assert.That(ex!.Message, Does.Contain("palette key"));
		}

		[Test]
		public void UnknownShape_ThrowsWithTheLine()
		{
			var ex = Assert.Throws<FormatException>(() =>
				Decode(new Vector3Int(1, 1, 1), Vector3Int.zero, "pyramid W 0 0 0 1"));
			Assert.That(ex!.Message, Does.Contain("pyramid"));
		}

		[Test]
		public void VModelYaml_RoundTripsPrimitivesParts()
		{
			var model = new VoxelRigModel
			{
				Id = "cart",
				TargetHeight = 3,
				Palette = Palette,
				Parts = new[]
				{
					new VoxelPart
					{
						Id = "body",
						Pivot = Vector3Int.zero,
						Data = new PrimitivesPartData(
							new Vector3Int(5, 3, 5),
							Vector3Int.zero,
							new[] { "box W 0 0 0 5 2 5 round 1", "cylinder K z 2 0.5 0 1 5" }),
					},
					new VoxelPart
					{
						Id = "later",
						Parent = "body",
						Pivot = new Vector3Int(0, 3, 0),
						Data = new PlannedPartData(PartEncoding.Primitives, Vector3Int.one, Vector3Int.zero, "todo"),
					},
				},
			};

			var read = VModelYaml.Read(VModelYaml.Write(model));

			var body = (PrimitivesPartData)read.Parts[0].Data;
			Assert.That(body.Shapes, Is.EqualTo(new[] { "box W 0 0 0 5 2 5 round 1", "cylinder K z 2 0.5 0 1 5" }));
			Assert.That(body.Size, Is.EqualTo(new Vector3Int(5, 3, 5)));

			var planned = (PlannedPartData)read.Parts[1].Data;
			Assert.That(planned.PlannedEncoding, Is.EqualTo(PartEncoding.Primitives));
		}

		[Test]
		public void Assembler_ResolvesPrimitivesParts()
		{
			var model = new VoxelRigModel
			{
				Id = "slab",
				TargetHeight = 2,
				Palette = Palette,
				Parts = new[]
				{
					new VoxelPart
					{
						Id = "base",
						Pivot = Vector3Int.zero,
						Data = new PrimitivesPartData(new Vector3Int(2, 2, 2), Vector3Int.zero, new[] { "box W 0 0 0 2 2 2" }),
					},
				},
			};

			var assembled = new ModelAssembler(StubScriptRunner.Failing("no scripts expected"))
				.AssembleAsync(model, CancellationToken.None).GetAwaiter().GetResult();
			var report = assembled.AssemblyIssues.Merge(new ModelValidator().Validate(assembled, ReferenceBrief.None));

			Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues));
			Assert.That(assembled.Composed.Voxels.Count, Is.EqualTo(8));
		}

		[Test]
		public void Assembler_ReportsInvalidShapesAsPartIssues()
		{
			var model = new VoxelRigModel
			{
				Id = "broken",
				TargetHeight = 1,
				Palette = Palette,
				Parts = new[]
				{
					new VoxelPart
					{
						Id = "p",
						Pivot = Vector3Int.zero,
						Data = new PrimitivesPartData(Vector3Int.one, Vector3Int.zero, new[] { "blob W 0 0 0" }),
					},
				},
			};

			var assembled = new ModelAssembler(StubScriptRunner.Failing("no scripts expected"))
				.AssembleAsync(model, CancellationToken.None).GetAwaiter().GetResult();

			Assert.That(assembled.AssemblyIssues.Issues.Any(i => i.Code == IssueCode.PrimitivesInvalid && i.PartId == "p"), Is.True);
		}

		[Test]
		public void PartAuthor_ParsesAPrimitivesFence_AndRetriesOnBadShapes()
		{
			var model = new VoxelRigModel
			{
				Id = "cart",
				TargetHeight = 2,
				Palette = Palette,
				Parts = new[]
				{
					new VoxelPart
					{
						Id = "body",
						Pivot = Vector3Int.zero,
						Data = new PlannedPartData(PartEncoding.Primitives, new Vector3Int(2, 2, 2), Vector3Int.zero, "a block"),
					},
				},
			};

			var gateway = new FakeGateway()
				.Enqueue("```primitives\nbox Z 0 0 0 2 2 2\n```")
				.Enqueue("```primitives\nbox W 0 0 0 2 2 2\n```");
			var author = new PartAuthor(gateway, VoxelizationConfig.Default);

			var data = author.AuthorAsync(
					model, ReferenceBrief.None, model.Parts[0], (PlannedPartData)model.Parts[0].Data, string.Empty, CancellationToken.None)
				.GetAwaiter().GetResult();

			Assert.That(gateway.Calls.Count, Is.EqualTo(2));
			Assert.That(gateway.Calls[0].Stage, Is.EqualTo(PartAuthor.Stage));
			Assert.That(((PrimitivesPartData)data).Shapes.Any(s => s.Contains("box W")), Is.True);
		}

		private static Assembler.Voxels.VoxelModel Decode(Vector3Int size, Vector3Int offset, params string[] shapes) =>
			PrimitivesCodec.Decode(new PrimitivesPartData(size, offset, shapes), Palette);
	}
}
