using System.Collections.Generic;
using System.Linq;
using Assembler.Libraries;
using NUnit.Framework;

namespace Tests.Resolving
{
	public class DeterministicRngTests
	{
		[Test]
		public void SameSeedProducesIdenticalUIntSequence()
		{
			var a = new DeterministicRng(12345);
			var b = new DeterministicRng(12345);

			var first = Enumerable.Range(0, 64).Select(_ => a.NextUInt()).ToArray();
			var second = Enumerable.Range(0, 64).Select(_ => b.NextUInt()).ToArray();

			CollectionAssert.AreEqual(first, second);
		}

		[Test]
		public void SameSeedProducesIdenticalFloatSequence()
		{
			var a = new DeterministicRng(999);
			var b = new DeterministicRng(999);

			for (int i = 0; i < 64; i++)
			{
				Assert.That(a.NextFloat(), Is.EqualTo(b.NextFloat()));
			}
		}

		[Test]
		public void DifferentSeedsDiverge()
		{
			var a = new DeterministicRng(1);
			var b = new DeterministicRng(2);

			var first = Enumerable.Range(0, 64).Select(_ => a.NextUInt()).ToArray();
			var second = Enumerable.Range(0, 64).Select(_ => b.NextUInt()).ToArray();

			// The streams must not be identical (a stuck/ignored seed would make them equal).
			Assert.That(first, Is.Not.EqualTo(second));
		}

		[Test]
		public void NextFloatStaysInUnitRange()
		{
			var rng = new DeterministicRng(7);
			for (int i = 0; i < 10000; i++)
			{
				float v = rng.NextFloat();
				Assert.That(v, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
			}
		}

		[Test]
		public void NextIntStaysInHalfOpenRange()
		{
			var rng = new DeterministicRng(42);
			for (int i = 0; i < 10000; i++)
			{
				int v = rng.NextInt(3, 8);
				Assert.That(v, Is.GreaterThanOrEqualTo(3).And.LessThan(8));
			}
		}

		[Test]
		public void NextIntEmptyRangeReturnsMin()
		{
			var rng = new DeterministicRng(42);
			Assert.That(rng.NextInt(5, 5), Is.EqualTo(5));
			Assert.That(rng.NextInt(5, 4), Is.EqualTo(5));
		}

		[Test]
		public void NextIntCoversEveryValueInRange()
		{
			var rng = new DeterministicRng(2024);
			var seen = new HashSet<int>();
			for (int i = 0; i < 10000; i++)
			{
				seen.Add(rng.NextInt(0, 6));
			}

			CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4, 5 }, seen);
		}
	}
}
