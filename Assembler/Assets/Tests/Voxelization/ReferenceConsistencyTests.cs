using Assembler.Voxelization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	public sealed class ReferenceConsistencyTests
	{
		[Test]
		public void ProportionConflict_FlagsAPinnedLengthThatDisagreesWithTheLeftAspect()
		{
			// The dog case: a left image read 36x20 implies length ~36 at height 20,
			// but the manifest pins length 28 — an inconsistent input that should be
			// caught up front with a suggested dimension.
			var asset = new ManifestAsset { Id = "dog", Height = 20, Length = 28, Width = 12, Tolerance = 1 };
			var brief = LeftSilhouette(36, 20);

			var conflict = ReferenceConsistency.ProportionConflict(asset, brief, aspectTolerance: 0.15f);

			Assert.That(conflict, Is.Not.Null);
			Assert.That(conflict, Does.Contain("length ≈ 36").And.Contain("length 28").And.Contain("left"));
		}

		[Test]
		public void ProportionConflict_NullWhenTheReferenceMatchesTheBoundingBox()
		{
			var asset = new ManifestAsset { Id = "dog", Height = 20, Length = 28, Width = 12, Tolerance = 1 };
			var brief = new ReferenceBrief
			{
				Silhouettes = new[]
				{
					new SilhouetteSpec("left", new Vector3Int(28, 20, 0), SolidRows(28, 20)),
					new SilhouetteSpec("front", new Vector3Int(12, 20, 0), SolidRows(12, 20)),
				},
			};

			Assert.That(ReferenceConsistency.ProportionConflict(asset, brief, 0.15f), Is.Null);
		}

		[Test]
		public void ProportionConflict_NullWhenTheAxisIsUnconstrained()
		{
			// Length 0 = unconstrained: the image is then the sole proportion signal,
			// so there is nothing to contradict.
			var asset = new ManifestAsset { Id = "dog", Height = 20, Length = 0, Width = 12, Tolerance = 1 };

			Assert.That(ReferenceConsistency.ProportionConflict(asset, LeftSilhouette(36, 20), 0.15f), Is.Null);
		}

		[Test]
		public void ProportionConflict_NullForASmallDifferenceWithinTolerance()
		{
			// 30 vs 28 is within max(tolerance, 15% of 28 = 4), so it is not flagged.
			var asset = new ManifestAsset { Id = "dog", Height = 20, Length = 28, Width = 12, Tolerance = 1 };

			Assert.That(ReferenceConsistency.ProportionConflict(asset, LeftSilhouette(30, 20), 0.15f), Is.Null);
		}

		[Test]
		public void ProportionConflict_ZeroToleranceFlagsEvenAOneVoxelDifference()
		{
			var asset = new ManifestAsset { Id = "dog", Height = 20, Length = 28, Width = 12, Tolerance = 0 };

			Assert.That(ReferenceConsistency.ProportionConflict(asset, LeftSilhouette(30, 20), aspectTolerance: 0f), Is.Not.Null);
		}

		private static ReferenceBrief LeftSilhouette(int width, int height) => new()
		{
			Silhouettes = new[] { new SilhouetteSpec("left", new Vector3Int(width, height, 0), SolidRows(width, height)) },
		};

		private static string[] SolidRows(int width, int height)
		{
			var rows = new string[height];
			for (var i = 0; i < height; i++)
			{
				rows[i] = new string('#', width);
			}

			return rows;
		}
	}
}
