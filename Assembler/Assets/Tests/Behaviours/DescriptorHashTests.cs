using Assembler.Building.Replay;
using NUnit.Framework;

namespace Tests.Behaviours
{
	/// <summary>
	/// Verifies descriptor hashing is stable for identical text and sensitive to any change, so replays bind to the
	/// exact descriptor they were recorded from. See the Determinism (Level 1) section in CLAUDE.md.
	/// </summary>
	public class DescriptorHashTests
	{
		[Test]
		public void IdenticalText_HashesEqual()
		{
			const string yaml = "name: Snake\nentities:\n  snake: {}\n";
			Assert.AreEqual(DescriptorHash.Compute(yaml), DescriptorHash.Compute(yaml));
		}

		[Test]
		public void DifferentText_HashesDiffer()
		{
			Assert.AreNotEqual(
				DescriptorHash.Compute("name: Snake\n"),
				DescriptorHash.Compute("name: Snake \n"));
		}

		[Test]
		public void Produces64CharHexDigest()
		{
			var hash = DescriptorHash.Compute("anything");
			Assert.AreEqual(64, hash.Length);
			StringAssert.IsMatch("^[0-9a-f]{64}$", hash);
		}
	}
}
