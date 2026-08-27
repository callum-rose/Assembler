using Assembler.Shell.Typography;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Tests.Shell
{
	/// <summary>
	/// The drop cap's arithmetic and string edits, away from the component that drives them.
	/// </summary>
	public class DropCapFormatterTests
	{
		private const string FontPath = "Assets/Fonts/Newsreader SDF.asset";

		private TMP_FontAsset _font = null!;

		[SetUp]
		public void SetUp()
		{
			_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
			Assert.IsNotNull(_font, $"the shell's baked font should be at {FontPath}");
		}

		[Test]
		public void OpenIndentLiftsTheFirstLetterAndOpensAnIndent()
		{
			string opened = DropCapFormatter.OpenIndent("Winter came early.", 40f);

			Assert.AreEqual("<indent=40px>inter came early.", opened);
			Assert.AreEqual('W', DropCapFormatter.CapCharacter("Winter came early."));
		}

		// Leading whitespace as the cap would set an invisible glyph and indent for nothing.
		[Test]
		public void OpenIndentSkipsLeadingWhitespace()
		{
			Assert.AreEqual('W', DropCapFormatter.CapCharacter("  Winter"));
			Assert.AreEqual("<indent=12px>inter", DropCapFormatter.OpenIndent("  Winter", 12f));
		}

		[Test]
		public void OpenIndentLeavesNothingToDoAlone()
		{
			Assert.AreEqual('\0', DropCapFormatter.CapCharacter(null));
			Assert.AreEqual('\0', DropCapFormatter.CapCharacter("   "));
			Assert.AreEqual(string.Empty, DropCapFormatter.OpenIndent(null, 40f));
			Assert.AreEqual("   ", DropCapFormatter.OpenIndent("   ", 40f));
		}

		// A fractional indent has to be written with an invariant decimal point, or a comma-decimal machine
		// emits "<indent=39,5px>" and TextMeshPro silently drops the tag.
		[Test]
		public void OpenIndentWritesTheWidthInvariantly()
		{
			StringAssert.Contains("<indent=39.5px>", DropCapFormatter.OpenIndent("Winter", 39.5f));
		}

		[Test]
		public void CloseIndentInsertsTheClosingTag()
		{
			Assert.AreEqual("ab</indent>cd", DropCapFormatter.CloseIndent("abcd", 2));
			Assert.AreEqual("abcd", DropCapFormatter.CloseIndent("abcd", -1));
			Assert.AreEqual("abcd", DropCapFormatter.CloseIndent("abcd", 99));
		}

		// The printed rule the size is built on: the cap's ink runs from the first line's cap line down to the
		// last line's baseline, so it grows by exactly one line height per extra line it hangs through — the same
		// step every time, whatever the font's cap height turns out to be.
		[Test]
		public void CapSizeGrowsByAConstantStepPerLine()
		{
			float one = DropCapFormatter.CapPointSize(_font, _font, 15f, 52f, 1);
			float two = DropCapFormatter.CapPointSize(_font, _font, 15f, 52f, 2);
			float three = DropCapFormatter.CapPointSize(_font, _font, 15f, 52f, 3);

			Assert.Greater(two, one);
			Assert.AreEqual(two - one, three - two, 0.01f);
		}

		// A one-line cap is a cap the height of the body's own capitals — which is to say, not a drop cap at all,
		// and the one case where the answer is knowable without the font's metrics.
		[Test]
		public void ASingleLineCapMatchesTheBodysCapHeight()
		{
			Assert.AreEqual(15f, DropCapFormatter.CapPointSize(_font, _font, 15f, 52f, 1), 0.01f);
		}

		// The prototype sets the lead story's two-line cap at 47px over 15px/1.52 body copy. That number was
		// arrived at by eye in CSS; this is the arithmetic landing in the same place.
		[Test]
		public void TheLeadStorysCapMatchesThePrototype()
		{
			Assert.AreEqual(47f, DropCapFormatter.CapPointSize(_font, _font, 15f, 52f, 2), 2f);
		}

		[Test]
		public void LeadingAddsHundredthsOfAnEm()
		{
			float tight = DropCapFormatter.LineHeight(_font, 15f, 0f);
			float loose = DropCapFormatter.LineHeight(_font, 15f, 52f);

			Assert.AreEqual(52f * 15f * 0.01f, loose - tight, 0.001f);
		}

		[Test]
		public void MissingFontsFallBackToTheBodySize()
		{
			Assert.AreEqual(15f, DropCapFormatter.CapPointSize(null, _font, 15f, 52f, 2));
			Assert.AreEqual(15f, DropCapFormatter.CapPointSize(_font, null, 15f, 52f, 2));
			Assert.AreEqual(0f, DropCapFormatter.Ascender(null, 15f), 0.001f);
		}
	}
}
