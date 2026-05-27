using System.Collections.Generic;
using Assembler.Behaviours.VariableUpdaters;
using NUnit.Framework;

namespace Tests.Behaviours
{
	public class FormatStringSetterTests
	{
		[Test]
		public void Format_FormatsIntegerArgument()
		{
			var result = FormatStringSetter.FormatString("Score: {0}", new object[] { 42 });

			Assert.AreEqual("Score: 42", result);
		}

		[Test]
		public void Format_FormatsMultipleIntegerArguments()
		{
			var result = FormatStringSetter.FormatString("{0}/{1}", new object[] { 3, 10 });

			Assert.AreEqual("3/10", result);
		}

		[Test]
		public void Format_FormatsMixedStringAndIntArguments()
		{
			var result = FormatStringSetter.FormatString("{0}: {1}", new object[] { "Lives", 5 });

			Assert.AreEqual("Lives: 5", result);
		}

		[Test]
		public void Format_NullArgumentIsRenderedAsEmpty()
		{
			var result = FormatStringSetter.FormatString("[{0}]", new object[] { null });

			Assert.AreEqual("[]", result);
		}

		[Test]
		public void Format_NullFormatStringReturnsEmpty()
		{
			var result = FormatStringSetter.FormatString(null, new object[] { 1 });

			Assert.AreEqual(string.Empty, result);
		}

		[Test]
		public void Format_NullArgumentsListIsHandled()
		{
			var result = FormatStringSetter.FormatString("hello", null);

			Assert.AreEqual("hello", result);
		}

		[Test]
		public void Format_MalformedFormatStringReturnsLiteralFormat()
		{
			var result = FormatStringSetter.FormatString("Score: {0", new object[] { 42 });

			Assert.AreEqual("Score: {0", result);
		}

		[Test]
		public void Format_UsesInvariantCultureForFloat()
		{
			var result = FormatStringSetter.FormatString("{0:F2}", new object[] { 1.5f });

			Assert.AreEqual("1.50", result);
		}

		[Test]
		public void Format_AcceptsReadOnlyListAsArguments()
		{
			IReadOnlyList<object> args = new List<object> { "a", 1, 2.5f };

			var result = FormatStringSetter.FormatString("{0}-{1}-{2:F1}", args);

			Assert.AreEqual("a-1-2.5", result);
		}
	}
}
