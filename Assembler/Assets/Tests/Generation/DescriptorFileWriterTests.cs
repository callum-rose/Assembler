using Assembler.Generation;
using NUnit.Framework;

namespace Tests.Generation
{
	public class DescriptorFileWriterTests
	{
		[Test]
		public void Sanitise_replaces_spaces_with_hyphens()
		{
			Assert.AreEqual("My-Cool-Game", DescriptorFileWriter.Sanitise("My Cool Game"));
		}

		[Test]
		public void Sanitise_collapses_runs_of_whitespace_into_single_hyphen()
		{
			Assert.AreEqual("A-B", DescriptorFileWriter.Sanitise("A   B"));
		}

		[Test]
		public void Sanitise_strips_path_separators()
		{
			Assert.AreEqual("foo-bar-baz", DescriptorFileWriter.Sanitise("foo/bar\\baz"));
		}

		[Test]
		public void Sanitise_strips_colons()
		{
			Assert.AreEqual("Pong-Reloaded", DescriptorFileWriter.Sanitise("Pong: Reloaded"));
		}

		[Test]
		public void Sanitise_returns_empty_for_null()
		{
			Assert.AreEqual(string.Empty, DescriptorFileWriter.Sanitise(null));
		}

		[Test]
		public void Sanitise_returns_empty_for_whitespace()
		{
			Assert.AreEqual(string.Empty, DescriptorFileWriter.Sanitise("   "));
		}

		[Test]
		public void Sanitise_trims_leading_and_trailing_separators()
		{
			Assert.AreEqual("Game", DescriptorFileWriter.Sanitise("  /Game/  "));
		}

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
