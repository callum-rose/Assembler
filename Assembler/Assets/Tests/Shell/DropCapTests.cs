using System.Collections.Generic;
using Assembler.Shell.Typography;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Tests.Shell
{
	/// <summary>
	/// The drop cap end to end, against the prefab the shell actually ships: two passes in, the lines the cap
	/// hangs through are indented and the line below it is not.
	/// </summary>
	public class DropCapTests
	{
		private const string PrefabPath = "Assets/Shell/Prefabs/LeadParagraph.prefab";

		private const string Paragraph =
			"Winter came early this year, and the presses ran late into the evening while the city slept " +
			"beneath a thin grey cover of new snow that nobody had thought to forecast.";

		private readonly List<GameObject> _created = new();

		[TearDown]
		public void TearDown()
		{
			foreach (var created in _created)
			{
				if (created != null)
				{
					Object.DestroyImmediate(created);
				}
			}

			_created.Clear();
		}

		[Test]
		public void IndentsTheLinesTheCapHangsThroughAndNoMore()
		{
			var (body, _) = LayOut();
			var info = body.textInfo;

			Assert.Greater(info.lineCount, 2, "the sample paragraph should run past the cap");

			float first = LineStart(info, 0);
			float second = LineStart(info, 1);
			float third = LineStart(info, 2);

			Assert.AreEqual(first, second, 1f, "both lines beside the cap should start at the same indent");
			Assert.Less(third, first - 10f, "the line below the cap should return to the full measure");
		}

		[Test]
		public void SetsTheCapToTheParagraphsFirstLetter()
		{
			var (_, cap) = LayOut();

			Assert.AreEqual("W", cap.text);
		}

		// The indent tags live in what TextMeshPro parses, never in the label — so a presenter reading this back
		// gets the copy it wrote.
		[Test]
		public void LeavesTheAuthoredParagraphAlone()
		{
			var (body, _) = LayOut();

			Assert.AreEqual(Paragraph, body.text);
			StringAssert.DoesNotContain("indent", body.text);
		}

		[Test]
		public void SizesTheCapToSpanTheLinesItHangsThrough()
		{
			var (body, cap) = LayOut();

			float expected = DropCapFormatter.CapPointSize(cap.font, body.font, body.fontSize, body.lineSpacing, 2);

			Assert.AreEqual(expected, cap.fontSize, 0.01f);
		}

		// The whole point of closing the indent at a line boundary: nothing above the insertion moves, so the
		// second pass lays the first lines out exactly where the first pass measured them.
		[Test]
		public void TheSecondPassDoesNotMoveTheFirstLines()
		{
			var (body, _) = LayOut();
			var info = body.textInfo;

			float firstBefore = LineStart(info, 0);
			int linesBefore = info.lineCount;

			body.GetComponent<DropCap>().Rebuild();

			Assert.AreEqual(firstBefore, LineStart(body.textInfo, 0), 0.01f);
			Assert.AreEqual(linesBefore, body.textInfo.lineCount);
		}

		private (TMP_Text Body, TMP_Text Cap) LayOut()
		{
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
			Assert.IsNotNull(prefab, $"the shell's lead paragraph should be at {PrefabPath}");

			var canvas = new GameObject("Canvas", typeof(Canvas));
			_created.Add(canvas);

			var instance = Object.Instantiate(prefab, canvas.transform);
			var dropCap = instance.GetComponent<DropCap>();

			dropCap.Text = Paragraph;
			dropCap.Rebuild();

			var body = instance.GetComponent<TMP_Text>();
			var cap = instance.transform.Find("Cap").GetComponent<TMP_Text>();

			return (body, cap);
		}

		private static float LineStart(TMP_TextInfo info, int line)
		{
			return info.characterInfo[info.lineInfo[line].firstCharacterIndex].origin;
		}
	}
}
