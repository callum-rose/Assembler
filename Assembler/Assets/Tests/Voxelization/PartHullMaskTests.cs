using System.Collections.Generic;
using System.Linq;
using Assembler.Voxelization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	public sealed class PartHullMaskTests
	{
		[Test]
		public void Mask_IsHullIntersectBox_InTheTargetFrame()
		{
			// Frame 8 wide, the front silhouette solid only in its left half. A part box
			// straddling the envelope edge (world x 2..5) keeps the cells inside the
			// envelope (x 2,3) and forbids the ones outside (x 4,5).
			var hull = HullOf(Sil("front", "####____"));
			var mask = MaskFor(offset: new Vector3Int(2, 0, 0), size: new Vector3Int(4, 1, 1),
				hull, frameMin: Vector3Int.zero, frameSize: new Vector3Int(8, 1, 1));

			Assert.That(mask.Allows(new Vector3Int(2, 0, 0)), Is.True);
			Assert.That(mask.Allows(new Vector3Int(3, 0, 0)), Is.True);
			Assert.That(mask.Allows(new Vector3Int(4, 0, 0)), Is.False);
			Assert.That(mask.Allows(new Vector3Int(5, 0, 0)), Is.False);
			Assert.That(mask.SolidCount, Is.EqualTo(2));
			Assert.That(mask.InHullFraction, Is.EqualTo(0.5f).Within(1e-4f));
			Assert.That(mask.IsEmpty, Is.False);
		}

		[Test]
		public void Allows_IsFalseOutsideTheDeclaredBox()
		{
			var hull = HullOf(Sil("front", "####"));
			var mask = MaskFor(Vector3Int.zero, new Vector3Int(4, 1, 1), hull, Vector3Int.zero, new Vector3Int(4, 1, 1));

			Assert.That(mask.Allows(new Vector3Int(0, 0, 0)), Is.True, "in-box, in-hull");
			Assert.That(mask.Allows(new Vector3Int(9, 0, 0)), Is.False, "outside the box is never allowed");
			Assert.That(mask.Allows(new Vector3Int(0, 0, 5)), Is.False);
		}

		[Test]
		public void MultiView_CarvesDepthOneViewCannot()
		{
			// Front keeps the middle x column; side keeps the middle z column. Together
			// only cells with x==1 AND z==1 stay allowed.
			var hull = HullOf(Sil("front", "_#_", "_#_", "_#_"), Sil("side", "_#_", "_#_", "_#_"));
			var mask = MaskFor(Vector3Int.zero, new Vector3Int(3, 3, 3), hull, Vector3Int.zero, new Vector3Int(3, 3, 3));

			for (var x = 0; x < 3; x++)
			{
				for (var y = 0; y < 3; y++)
				{
					for (var z = 0; z < 3; z++)
					{
						Assert.That(mask.Allows(new Vector3Int(x, y, z)), Is.EqualTo(x == 1 && z == 1),
							$"cell {x},{y},{z}");
					}
				}
			}

			Assert.That(mask.SolidCount, Is.EqualTo(3));
		}

		[Test]
		public void ToAsciiLayers_RendersAllowedRegionInAuthoredForm()
		{
			// 1 layer (y), size.z=1 row of size.x=4 chars: '#' allowed, '.' forbidden.
			var hull = HullOf(Sil("front", "##__"));
			var mask = MaskFor(Vector3Int.zero, new Vector3Int(4, 1, 1), hull, Vector3Int.zero, new Vector3Int(4, 1, 1));

			Assert.That(mask.ToAsciiLayers(), Is.EqualTo(new[] { "##.." }));
		}

		[Test]
		public void Feasibility_BoxEntirelyOutsideTheEnvelopeIsEmpty()
		{
			// Frame 8 wide, solid only in the left half; a box over the right half
			// (world x 4..7) has no in-hull cells — the infeasible re-plan signal.
			var hull = HullOf(Sil("front", "####____"));
			var mask = MaskFor(new Vector3Int(4, 0, 0), new Vector3Int(4, 1, 1),
				hull, Vector3Int.zero, new Vector3Int(8, 1, 1));

			Assert.That(mask.SolidCount, Is.EqualTo(0));
			Assert.That(mask.InHullFraction, Is.EqualTo(0f));
			Assert.That(mask.IsEmpty, Is.True);
		}

		// --- helpers -------------------------------------------------------------

		private static PartHullMask MaskFor(
			Vector3Int offset, Vector3Int size, SilhouetteHull hull, Vector3Int frameMin, Vector3Int frameSize)
		{
			var model = new VoxelRigModel();
			var part = new VoxelPart { Id = "p", Pivot = Vector3Int.zero };
			return PartHullMask.For(model, part, offset, size, hull, frameMin, frameSize);
		}

		private static SilhouetteHull HullOf(params SilhouetteSpec[] silhouettes) =>
			SilhouetteHull.Build(BriefOf(silhouettes), dilation: 0);

		private static SilhouetteSpec Sil(string face, params string[] rows)
		{
			var height = rows.Length;
			var width = height == 0 ? 0 : rows[0].Length;
			return new SilhouetteSpec(face, new Vector3Int(width, height, 0), rows);
		}

		private static ReferenceBrief BriefOf(IReadOnlyList<SilhouetteSpec> silhouettes) =>
			new() { Source = "ref", Silhouettes = silhouettes.ToArray() };
	}
}
