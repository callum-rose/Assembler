using Assembler.Extensions;
using NUnit.Framework;

namespace Tests.Generation
{
	public class FileNameSanitiserTests
	{
		[Test]
		public void Sanitise_replaces_spaces_with_hyphens()
		{
			Assert.AreEqual("My-Cool-Game", FileNameSanitiser.Sanitise("My Cool Game"));
		}

		[Test]
		public void Sanitise_collapses_runs_of_whitespace_into_single_hyphen()
		{
			Assert.AreEqual("A-B", FileNameSanitiser.Sanitise("A   B"));
		}

		[Test]
		public void Sanitise_strips_path_separators()
		{
			Assert.AreEqual("foo-bar-baz", FileNameSanitiser.Sanitise("foo/bar\\baz"));
		}

		[Test]
		public void Sanitise_strips_colons()
		{
			Assert.AreEqual("Pong-Reloaded", FileNameSanitiser.Sanitise("Pong: Reloaded"));
		}

		[Test]
		public void Sanitise_returns_empty_for_null()
		{
			Assert.AreEqual(string.Empty, FileNameSanitiser.Sanitise(null));
		}

		[Test]
		public void Sanitise_returns_empty_for_whitespace()
		{
			Assert.AreEqual(string.Empty, FileNameSanitiser.Sanitise("   "));
		}

		[Test]
		public void Sanitise_trims_leading_and_trailing_separators()
		{
			Assert.AreEqual("Game", FileNameSanitiser.Sanitise("  /Game/  "));
		}
	}
}
