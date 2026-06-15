using System.Collections.Generic;
using System.Linq;
using Assembler.Voxelization;
using Assembler.Voxels;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	public sealed class ForwardHullBoundTests
	{
		// Dilation 0 keeps masks exact; the floor is disabled (1f) so per-part tiering
		// is observable. Mask uses ratios above 1 so every partial removal stays in the
		// light tier (applied), making survivors observable without tiering refusing.
		private static readonly HullClipSettings Tiers = new(0, 0.20f, 0.50f, 1f);
		private static readonly HullClipSettings Mask = new(0, 1.1f, 1.2f, 1f);

		[Test]
		public void NoReference_IsAByteForByteNoOp()
		{
			var (model, part) = Single("p", Bar(10));
			var parts = new[] { part };

			var result = ForwardHullBound.Apply(parts, model, ReferenceBrief.None, Tiers);

			Assert.That(result.Parts, Is.SameAs(parts), "no silhouettes => assemble unchanged");
			Assert.That(result.Issues, Is.Empty);
			Assert.That(result.HullDiscarded, Is.False);
		}

		[Test]
		public void FrontMask_ClipsAgainstTheTargetFrame()
		{
			var (model, part) = Single("p", Bar(10));
			var result = ForwardHullBound.Apply(new[] { part }, model, BriefOf(Sil("front", "#######___")), Mask);

			Assert.That(SurvivorKeys(result, "p").All(k => k.x < 7), Is.True);
			Assert.That(SurvivorKeys(result, "p").Count, Is.EqualTo(7));
		}

		[Test]
		public void MultiView_CarvesDepthOneViewCannot()
		{
			// 3x3x3 cube; front keeps x==1, side keeps z==1 — only the centre column survives.
			var voxels = new List<Vector3Int>();
			for (var x = 0; x < 3; x++)
			{
				for (var y = 0; y < 3; y++)
				{
					for (var z = 0; z < 3; z++)
					{
						voxels.Add(new Vector3Int(x, y, z));
					}
				}
			}

			var (model, part) = Single("p", voxels);
			var brief = BriefOf(Sil("front", "_#_", "_#_", "_#_"), Sil("side", "_#_", "_#_", "_#_"));
			var result = ForwardHullBound.Apply(new[] { part }, model, brief, Mask);

			Assert.That(SurvivorKeys(result, "p").All(k => k.x == 1 && k.z == 1), Is.True);
			Assert.That(SurvivorKeys(result, "p").Count, Is.EqualTo(3));
		}

		[Test]
		public void Moderate_AppliesAndFlagsPart()
		{
			var (model, part) = Single("p", Bar(10));
			var result = ForwardHullBound.Apply(new[] { part }, model, BriefOf(Sil("front", "#######___")), Tiers);

			Assert.That(SurvivorKeys(result, "p").Count, Is.EqualTo(7), "trim is applied");
			var issue = result.Issues.Single();
			Assert.That(issue.Tier, Is.EqualTo(ClipTier.Moderate));
			Assert.That(issue.PartId, Is.EqualTo("p"));
		}

		[Test]
		public void SevereByRatio_KeepsGeometry()
		{
			var (model, part) = Single("p", Bar(10));
			var result = ForwardHullBound.Apply(new[] { part }, model, BriefOf(Sil("front", "####______")), Tiers);

			Assert.That(result.Parts[0].Grid.Voxels.Count, Is.EqualTo(10), "severe never applies the clip");
			Assert.That(result.Issues.Single().Reason, Is.EqualTo(ClipSevereReason.Ratio));
		}

		[Test]
		public void LooseExemption_AppliesDisconnectingClip()
		{
			var (model, part) = Single("p", Bar(10), loose: true);
			var result = ForwardHullBound.Apply(new[] { part }, model, BriefOf(Sil("front", "####_#####")), Tiers);

			Assert.That(result.Issues, Is.Empty, "loose parts may fragment");
			Assert.That(SurvivorKeys(result, "p").Count, Is.EqualTo(9));
		}

		[Test]
		public void GlobalFloor_DiscardsHullAndKeepsAllParts()
		{
			// Two halves of an 0..19 bar across two parts; the right half is forbidden,
			// so 10/20 removed > 0.30 floor => discard.
			var model = ModelOf(
				Authored("a", Bar(10, fromX: 0)),
				Authored("b", Bar(10, fromX: 10)));
			var parts = model.Parts.Select(p => AssembledFrom(model, p)).ToArray();
			var brief = BriefOf(Sil("front", "##########__________"));

			var result = ForwardHullBound.Apply(parts, model, brief, new HullClipSettings(0, 0.2f, 0.5f, 0.3f));

			Assert.That(result.HullDiscarded, Is.True);
			Assert.That(result.Issues, Is.Empty);
			Assert.That(result.RemovedFraction, Is.EqualTo(0.5f).Within(1e-4f));
			Assert.That(result.Parts[0], Is.SameAs(parts[0]));
			Assert.That(result.Parts[1], Is.SameAs(parts[1]));
		}

		[Test]
		public void MirrorAndSource_ClipToReflectionsUnderASymmetricHull()
		{
			// Source bar at world x 2..5; its x-mirror at world x -5..-2. The frame
			// spans x -5..5 (11 wide); a symmetric silhouette forbids only the two
			// outermost columns, so source drops x=5 and the mirror drops x=-5 — the
			// survivors are exact reflections.
			var sourceVox = Enumerable.Range(2, 4).Select(x => new Vector3Int(x, 0, 0)).ToList();
			var mirrorVox = sourceVox.Select(p => new Vector3Int(-p.x, p.y, p.z)).ToList();

			var sourcePart = new VoxelPart
			{
				Id = "s",
				Pivot = Vector3Int.zero,
				Data = new PlannedPartData(PartEncoding.Layers, new Vector3Int(4, 1, 1), new Vector3Int(2, 0, 0), string.Empty),
			};
			var mirrorPart = new VoxelPart { Id = "s.m", Pivot = Vector3Int.zero, Data = new MirrorPartData("s", MirrorAxis.X) };
			var model = new VoxelRigModel
			{
				Id = "m",
				TargetHeight = 1,
				Symmetry = "bilateral",
				Palette = new[] { new PaletteEntry('A', new Color32(255, 0, 0, 255)) },
				Parts = new[] { sourcePart, mirrorPart },
			};

			var parts = new[]
			{
				new AssembledPart(sourcePart, GridOf(sourceVox), Vector3Int.zero),
				new AssembledPart(mirrorPart, GridOf(mirrorVox), Vector3Int.zero),
			};

			var result = ForwardHullBound.Apply(parts, model, BriefOf(Sil("front", "_#########_")), Mask);

			var sourceWorld = WorldKeys(result, "s").OrderBy(x => x).ToList();
			var mirrorWorld = WorldKeys(result, "s.m").OrderBy(x => x).ToList();
			Assert.That(sourceWorld, Is.EqualTo(new[] { 2, 3, 4 }), "source drops its outer x=5");
			Assert.That(mirrorWorld, Is.EqualTo(new[] { -4, -3, -2 }), "mirror drops its outer x=-5");
			Assert.That(mirrorWorld, Is.EqualTo(sourceWorld.Select(x => -x).OrderBy(x => x).ToList()),
				"survivors are exact reflections");
		}

		// --- helpers -------------------------------------------------------------

		private static (VoxelRigModel Model, AssembledPart Part) Single(string id, IEnumerable<Vector3Int> voxels, bool loose = false)
		{
			var part = Authored(id, voxels, loose);
			var model = ModelOf(part);
			return (model, AssembledFrom(model, part));
		}

		private static VoxelPart Authored(string id, IEnumerable<Vector3Int> voxels, bool loose = false)
		{
			var keys = voxels.ToList();
			var (min, max) = Bounds(keys);
			var size = max - min + Vector3Int.one;
			return new VoxelPart
			{
				Id = id,
				Loose = loose,
				Pivot = Vector3Int.zero,
				Data = new PlannedPartData(PartEncoding.Layers, size, min, string.Empty),
				// Geometry stored on the part lets AssembledFrom rebuild the grid.
				Parent = VoxelRigModel.RootId,
			};
		}

		private static VoxelRigModel ModelOf(params VoxelPart[] parts) => new()
		{
			Id = "m",
			TargetHeight = parts.Length == 0 ? 1 : Bounds(parts.SelectMany(BoxKeys)).Max.y + 1,
			Palette = new[] { new PaletteEntry('A', new Color32(255, 0, 0, 255)) },
			Parts = parts,
		};

		private static AssembledPart AssembledFrom(VoxelRigModel model, VoxelPart part)
		{
			var (offset, size) = part.Data switch
			{
				PlannedPartData planned => (planned.Offset, planned.Size),
				_ => (Vector3Int.zero, Vector3Int.zero),
			};
			var keys = BoxKeysOf(offset, size);
			return new AssembledPart(part, GridOf(keys), PlanGeometryChecks.WorldPivot(model, part));
		}

		private static IEnumerable<Vector3Int> BoxKeys(VoxelPart part) => part.Data switch
		{
			PlannedPartData planned => BoxKeysOf(planned.Offset, planned.Size),
			_ => Enumerable.Empty<Vector3Int>(),
		};

		private static IEnumerable<Vector3Int> BoxKeysOf(Vector3Int offset, Vector3Int size)
		{
			for (var x = 0; x < size.x; x++)
			{
				for (var y = 0; y < size.y; y++)
				{
					for (var z = 0; z < size.z; z++)
					{
						yield return offset + new Vector3Int(x, y, z);
					}
				}
			}
		}

		private static VoxelModel GridOf(IEnumerable<Vector3Int> voxels)
		{
			var dict = voxels.ToDictionary(p => p, _ => (byte)1);
			var (min, max) = Bounds(dict.Keys);
			return new VoxelModel(dict, new[] { new Color32(255, 0, 0, 255) }, min, max);
		}

		private static IEnumerable<Vector3Int> Bar(int length, int fromX = 0) =>
			Enumerable.Range(fromX, length).Select(x => new Vector3Int(x, 0, 0));

		private static SilhouetteSpec Sil(string face, params string[] rows)
		{
			var height = rows.Length;
			var width = height == 0 ? 0 : rows[0].Length;
			return new SilhouetteSpec(face, new Vector3Int(width, height, 0), rows);
		}

		private static ReferenceBrief BriefOf(params SilhouetteSpec[] silhouettes) =>
			new() { Source = "ref", Silhouettes = silhouettes };

		private static IReadOnlyCollection<Vector3Int> SurvivorKeys(ClipResult result, string partId) =>
			result.Parts.First(p => p.Part.Id == partId).Grid.Voxels.Keys.ToList();

		private static IEnumerable<int> WorldKeys(ClipResult result, string partId)
		{
			var part = result.Parts.First(p => p.Part.Id == partId);
			return part.Grid.Voxels.Keys.Select(k => (k + part.WorldPivot).x);
		}

		private static (Vector3Int Min, Vector3Int Max) Bounds(IEnumerable<Vector3Int> cells)
		{
			var min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
			var max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
			foreach (var c in cells)
			{
				min = Vector3Int.Min(min, c);
				max = Vector3Int.Max(max, c);
			}

			return (min, max);
		}
	}
}
