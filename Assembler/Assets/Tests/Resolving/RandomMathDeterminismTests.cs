using System.Collections.Generic;
using Assembler.Libraries;
using NUnit.Framework;

namespace Tests.Resolving
{
	/// <summary>
	/// Locks in the Level 1 determinism guarantee for randomness: <see cref="DeterministicRng"/> produces a
	/// stable per-seed sequence, and <see cref="RandomMath"/> follows whatever <see cref="RandomState.Seed"/>
	/// was last set. See the Determinism (Level 1) section in CLAUDE.md.
	/// </summary>
	public class RandomMathDeterminismTests
	{
		[Test]
		public void DeterministicRng_SameSeed_ProducesIdenticalSequence()
		{
			var a = new DeterministicRng(42);
			var b = new DeterministicRng(42);

			for (var i = 0; i < 1000; i++)
				Assert.AreEqual(a.NextUInt(), b.NextUInt(), $"diverged at draw {i}");
		}

		[Test]
		public void DeterministicRng_DifferentSeed_ProducesDifferentSequence()
		{
			var a = new DeterministicRng(1);
			var b = new DeterministicRng(2);

			var differs = false;
			for (var i = 0; i < 16 && !differs; i++)
				differs = a.NextUInt() != b.NextUInt();

			Assert.IsTrue(differs, "different seeds should yield a different sequence");
		}

		[Test]
		public void DeterministicRng_Value_StaysInUnitInterval()
		{
			var rng = new DeterministicRng(7);
			for (var i = 0; i < 1000; i++)
			{
				var v = rng.Value;
				Assert.That(v, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
			}
		}

		[Test]
		public void DeterministicRng_NextInt_StaysInHalfOpenRange()
		{
			var rng = new DeterministicRng(7);
			for (var i = 0; i < 1000; i++)
			{
				var v = rng.NextInt(3, 9);
				Assert.That(v, Is.GreaterThanOrEqualTo(3).And.LessThan(9));
			}
		}

		[Test]
		public void DeterministicRng_NextInt_EmptyRange_ReturnsMin()
		{
			var rng = new DeterministicRng(7);
			Assert.AreEqual(5, rng.NextInt(5, 5));
			Assert.AreEqual(5, rng.NextInt(5, 4));
		}

		[Test]
		public void RandomMath_FollowsRandomStateSeed()
		{
			RandomState.Seed(98765);
			var first = DrawSequence();

			RandomState.Seed(98765);
			var second = DrawSequence();

			CollectionAssert.AreEqual(first, second);
		}

		[Test]
		public void RandomMath_DifferentSeeds_DivergeAcrossDraws()
		{
			RandomState.Seed(1);
			var a = DrawSequence();

			RandomState.Seed(2);
			var b = DrawSequence();

			CollectionAssert.AreNotEqual(a, b);
		}

		// Exercises several RandomMath entry points so the captured sequence reflects real consumption order.
		private static List<float> DrawSequence()
		{
			var values = new List<float>();
			for (var i = 0; i < 50; i++)
			{
				values.Add(RandomMath.RandomFloat(0f, 100f));
				values.Add(RandomMath.RandomInt(0, 1000));
				var c = RandomMath.RandomColor();
				values.Add(c.r);
				values.Add(c.g);
				values.Add(c.b);
				var p = RandomMath.RandomOnCircle(3f);
				values.Add(p.x);
				values.Add(p.y);
			}
			return values;
		}
	}
}
