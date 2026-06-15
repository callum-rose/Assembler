using System.Collections.Generic;
using System.Linq;
using Assembler.Voxelization;
using Assembler.Voxels;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	public sealed class HullClipTests
	{
		// Dilation 0 keeps the masks exact so ratios are predictable; the global
		// floor is disabled (1f) so per-part tiering is observable in isolation — a
		// single part clipped past the floor would otherwise discard the whole hull.
		// Dilation and the floor are exercised on their own in Dilation_* / GlobalFloor_*.
		private static readonly HullClipSettings Tiers = new(0, 0.20f, 0.50f, 1f);

		// Projection tests assert *which* voxels are trimmed on a small grid where the
		// removed fraction is large; ratios above 1 keep every partial removal in the
		// light tier (applied) so the survivors are observable without tiering refusing.
		private static readonly HullClipSettings Mask = new(0, 1.1f, 1.2f, 1f);

		[Test]
		public void FrontMask_ClipsByX()
		{
			// 3x3 front plane; a vertical bar silhouette keeps only the middle column.
			var part = Part("p", Plane3X3Front());
			var result = HullClip.Apply(new[] { part }, BriefOf(Sil("front", "_#_", "_#_", "_#_")), Mask);

			Assert.That(SurvivorKeys(result, "p").All(k => k.x == 1), Is.True);
			Assert.That(SurvivorKeys(result, "p").Count, Is.EqualTo(3));
		}

		[Test]
		public void SideMask_ClipsByZ()
		{
			// Side projects u = z, v = y; the bar keeps only z == 1.
			var voxels = Grid3(3, 3, (y, z) => new Vector3Int(0, y, z));
			var result = HullClip.Apply(new[] { Part("p", voxels) }, BriefOf(Sil("side", "_#_", "_#_", "_#_")), Mask);

			Assert.That(SurvivorKeys(result, "p").All(k => k.z == 1), Is.True);
			Assert.That(SurvivorKeys(result, "p").Count, Is.EqualTo(3));
		}

		[Test]
		public void TopMask_ClipsByX()
		{
			// Top projects u = x, v = z; the bar keeps only x == 1.
			var voxels = Grid3(3, 3, (x, z) => new Vector3Int(x, 0, z));
			var result = HullClip.Apply(new[] { Part("p", voxels) }, BriefOf(Sil("top", "_#_", "_#_", "_#_")), Mask);

			Assert.That(SurvivorKeys(result, "p").All(k => k.x == 1), Is.True);
			Assert.That(SurvivorKeys(result, "p").Count, Is.EqualTo(3));
		}

		[Test]
		public void FrontMask_HonoursImageTopFirstRowFlip()
		{
			// A 1x4 column; the silhouette is solid in its top two (image) rows, which
			// must map to the HIGH-y voxels (the v flip), so y 0..1 are clipped.
			var voxels = Enumerable.Range(0, 4).Select(y => new Vector3Int(0, y, 0));
			var result = HullClip.Apply(new[] { Part("p", voxels) }, BriefOf(Sil("front", "#", "#", "_", "_")), Mask);

			Assert.That(SurvivorKeys(result, "p").Select(k => k.y), Is.EquivalentTo(new[] { 2, 3 }));
		}

		[Test]
		public void Dilation_KeepsAOneVoxelOverhang()
		{
			var bar = Bar(10);
			var brief = BriefOf(Sil("front", "#########_")); // x 9 falls outside the raw mask

			var removed = HullClip.Apply(new[] { Part("p", bar) }, brief, new HullClipSettings(0, 0.2f, 0.5f, 0.3f));
			Assert.That(SurvivorKeys(removed, "p").Count, Is.EqualTo(9), "dilation 0 trims the overhang");

			var kept = HullClip.Apply(new[] { Part("p", bar) }, brief, new HullClipSettings(1, 0.2f, 0.5f, 0.3f));
			Assert.That(SurvivorKeys(kept, "p").Count, Is.EqualTo(10), "dilation 1 keeps the on-edge voxel");
		}

		[Test]
		public void Ratio_IsRemovedOverTotal()
		{
			// 3 of 10 outside.
			var result = HullClip.Apply(new[] { Part("p", Bar(10)) }, BriefOf(Sil("front", "#######___")), Tiers);

			var issue = result.Issues.Single();
			Assert.That(issue.Ratio, Is.EqualTo(0.3f).Within(1e-4f));
		}

		[Test]
		public void Light_AppliesSilently()
		{
			// 1 of 10 outside (< moderate ratio).
			var result = HullClip.Apply(new[] { Part("p", Bar(10)) }, BriefOf(Sil("front", "#########_")), Tiers);

			Assert.That(result.Issues, Is.Empty);
			Assert.That(SurvivorKeys(result, "p").Count, Is.EqualTo(9));
		}

		[Test]
		public void Moderate_AppliesAndFlagsPart()
		{
			var result = HullClip.Apply(new[] { Part("p", Bar(10)) }, BriefOf(Sil("front", "#######___")), Tiers);

			Assert.That(SurvivorKeys(result, "p").Count, Is.EqualTo(7), "trim is applied");
			var issue = result.Issues.Single();
			Assert.That(issue.Tier, Is.EqualTo(ClipTier.Moderate));
			Assert.That(issue.PartId, Is.EqualTo("p"));
		}

		[Test]
		public void SevereByRatio_KeepsGeometry()
		{
			var input = new[] { Part("p", Bar(10)) };
			var result = HullClip.Apply(input, BriefOf(Sil("front", "####______")), Tiers); // 6 of 10 outside

			Assert.That(result.Parts[0].Grid.Voxels.Count, Is.EqualTo(10), "severe never applies the clip");
			var issue = result.Issues.Single();
			Assert.That(issue.Tier, Is.EqualTo(ClipTier.Severe));
			Assert.That(issue.Reason, Is.EqualTo(ClipSevereReason.Ratio));
		}

		[Test]
		public void SevereByFullRemoval_KeepsGeometry()
		{
			var result = HullClip.Apply(new[] { Part("p", Bar(10)) }, BriefOf(Sil("front", "__________")), Tiers);

			Assert.That(result.Parts[0].Grid.Voxels.Count, Is.EqualTo(10));
			Assert.That(result.Issues.Single().Reason, Is.EqualTo(ClipSevereReason.FullRemoval));
		}

		[Test]
		public void SevereByDisconnection_KeepsGeometry()
		{
			// Removing the middle splits the bar in two; a non-loose part refuses.
			var result = HullClip.Apply(new[] { Part("p", Bar(10)) }, BriefOf(Sil("front", "####_#####")), Tiers);

			Assert.That(result.Parts[0].Grid.Voxels.Count, Is.EqualTo(10));
			Assert.That(result.Issues.Single().Reason, Is.EqualTo(ClipSevereReason.Disconnection));
		}

		[Test]
		public void LooseExemption_AppliesDisconnectingClip()
		{
			// Same splitting clip on a loose part: allowed to fragment, applied as light.
			var result = HullClip.Apply(new[] { Part("p", Bar(10), loose: true) }, BriefOf(Sil("front", "####_#####")), Tiers);

			Assert.That(result.Issues, Is.Empty);
			Assert.That(SurvivorKeys(result, "p").Count, Is.EqualTo(9));
		}

		[Test]
		public void GlobalFloor_DiscardsHullAndKeepsAllParts()
		{
			// Part a is entirely outside, part b entirely inside: 10/20 removed > 0.30.
			var a = Part("a", Bar(10, fromX: 0));
			var b = Part("b", Bar(10, fromX: 10));
			var brief = BriefOf(Sil("front", "__________##########"));

			var result = HullClip.Apply(new[] { a, b }, brief, new HullClipSettings(0, 0.2f, 0.5f, 0.3f));

			Assert.That(result.HullDiscarded, Is.True);
			Assert.That(result.Issues, Is.Empty);
			Assert.That(result.Parts[0], Is.SameAs(a));
			Assert.That(result.Parts[1], Is.SameAs(b));
			Assert.That(result.RemovedFraction, Is.EqualTo(0.5f).Within(1e-4f));
		}

		[Test]
		public void FrontOnly_DoesNotClipDepth()
		{
			// A line along z with a single solid front cell: z is unconstrained, so
			// nothing is removed despite the part spanning depth.
			var voxels = Enumerable.Range(0, 3).Select(z => new Vector3Int(0, 0, z));
			var result = HullClip.Apply(new[] { Part("p", voxels) }, BriefOf(Sil("front", "#")), Tiers);

			Assert.That(result.Issues, Is.Empty);
			Assert.That(SurvivorKeys(result, "p").Count, Is.EqualTo(3));
		}

		[Test]
		public void NoSilhouettes_IsANoOp()
		{
			var input = new[] { Part("p", Bar(10)) };

			Assert.That(HullClip.Apply(input, ReferenceBrief.None, Tiers).Parts, Is.SameAs(input));
			// An empty silhouette in the list is skipped, leaving no masks.
			var emptyOnly = BriefOf(SilhouetteSpec.None);
			var result = HullClip.Apply(input, emptyOnly, Tiers);
			Assert.That(result.Parts, Is.SameAs(input));
			Assert.That(result.Issues, Is.Empty);
		}

		// --- helpers -------------------------------------------------------------

		private static AssembledPart Part(string id, IEnumerable<Vector3Int> voxels, Vector3Int pivot = default, bool loose = false)
		{
			var dict = voxels.ToDictionary(p => p, _ => (byte)1);
			var (min, max) = Bounds(dict.Keys);
			var grid = new VoxelModel(dict, new[] { new Color32(255, 0, 0, 255) }, min, max);
			return new AssembledPart(new VoxelPart { Id = id, Loose = loose }, grid, pivot);
		}

		private static IEnumerable<Vector3Int> Bar(int length, int fromX = 0) =>
			Enumerable.Range(fromX, length).Select(x => new Vector3Int(x, 0, 0));

		private static IEnumerable<Vector3Int> Plane3X3Front() =>
			Grid3(3, 3, (x, y) => new Vector3Int(x, y, 0));

		private static IEnumerable<Vector3Int> Grid3(int countA, int countB, System.Func<int, int, Vector3Int> map)
		{
			for (var a = 0; a < countA; a++)
			{
				for (var b = 0; b < countB; b++)
				{
					yield return map(a, b);
				}
			}
		}

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
