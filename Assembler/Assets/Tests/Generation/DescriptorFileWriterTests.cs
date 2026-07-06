using Assembler.Generation;
using NUnit.Framework;

namespace Tests.Generation
{
	public class DescriptorFileWriterTests
	{
		[Test]
		public void BuildFileName_falls_back_to_timestamp_when_title_blank()
		{
			var name = DescriptorFileWriter.BuildFileName(null);
			Assert.IsTrue(name.StartsWith("game-"), $"expected timestamp fallback, got {name}");
			Assert.IsTrue(name.EndsWith(".yaml"));
		}

		[Test]
		public void BuildFileName_uses_sanitised_title()
		{
			Assert.AreEqual("Snake-2.yaml", DescriptorFileWriter.BuildFileName("Snake 2"));
		}
	}
}
