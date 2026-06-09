using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Parsing
{
	public class UIBlocksParseTests
	{
		private static GameInfo Parse(string yaml) =>
			Transformer.Transform(new GameFileParser().Parse(yaml));

		private const string Yaml = @"
Entities:
  ui:
    Behaviours:
      canvas:
        Type: ui canvas
        Properties:
          MatchWidthOrHeight: 0.25
      panel:
        Type: ui container
        Properties:
          Direction: horizontal
          Spacing: 8
          Padding: 4
          FitContent: true
      label:
        Type: text label
        Properties:
          Text: hi
          FontSize: 20
          PreferredHeight: 30
      btn:
        Type: ui button
        Properties:
          Label: Go
          PreferredWidth: 100
";

		[Test]
		public void UiBlocksTransformIntoTheirInfoTypes()
		{
			var behaviours = Parse(Yaml).Entities[0].Behaviours;

			Assert.IsInstanceOf<UICanvasInfo>(behaviours[0]);
			Assert.IsInstanceOf<UIContainerInfo>(behaviours[1]);
			Assert.IsInstanceOf<TextLabelInfo>(behaviours[2]);
			Assert.IsInstanceOf<UIButtonInfo>(behaviours[3]);
		}

		[Test]
		public void OmittedValueTypePropertiesResolveToNone()
		{
			// Regression: an omitted value-type property must become None (NullValueProvider at runtime), not
			// a ConstantSource of default(T). Otherwise the behaviour's ValueOr(default) never fires — which
			// made `ui canvas` resolve ReferenceResolution to (0,0,0) and collapse the whole UI to size 0.
			var behaviours = Parse(Yaml).Entities[0].Behaviours;
			var canvas = (UICanvasInfo)behaviours[0];
			var label = (TextLabelInfo)behaviours[2];

			Assert.IsInstanceOf<None<Vector3>>(canvas.ReferenceResolution,
				"Omitted ReferenceResolution must be None, else the CanvasScaler reference resolution becomes (0,0).");
			Assert.IsInstanceOf<None<float>>(label.PreferredWidth,
				"Omitted PreferredWidth must be None so the block's default size applies.");
		}

		[Test]
		public void CanvasMatchValueParsesAsConstant()
		{
			var canvas = (UICanvasInfo)Parse(Yaml).Entities[0].Behaviours[0];

			Assert.AreEqual(0.25f, ((ConstantSource<float>)canvas.MatchWidthOrHeight).Value);
		}

		[Test]
		public void ContainerPropertiesParseAsConstants()
		{
			var container = (UIContainerInfo)Parse(Yaml).Entities[0].Behaviours[1];

			Assert.AreEqual(LayoutDirection.Horizontal, ((ConstantSource<LayoutDirection>)container.Direction).Value);
			Assert.AreEqual(8f, ((ConstantSource<float>)container.Spacing).Value);
			Assert.AreEqual(4f, ((ConstantSource<float>)container.Padding).Value);
			Assert.IsTrue(((ConstantSource<bool>)container.FitContent).Value);
		}

		[Test]
		public void LabelAndButtonCaptionsParseAsConstants()
		{
			var behaviours = Parse(Yaml).Entities[0].Behaviours;
			var label = (TextLabelInfo)behaviours[2];
			var button = (UIButtonInfo)behaviours[3];

			Assert.AreEqual("hi", ((ConstantSource<string>)label.Text).Value);
			Assert.AreEqual("Go", ((ConstantSource<string>)button.Label).Value);
		}

		private const string KeyedChildrenYaml = @"
Entities:
  ui:
    Behaviours:
      canvas:
        Type: ui canvas
    Children:
      hud:
        Behaviours:
          layout:
            Type: ui container
        Children:
          title:
            Behaviours:
              label:
                Type: text label
                Properties:
                  Text: hi
";

		private const string ListChildrenYaml = @"
Entities:
  ui:
    Behaviours:
      canvas:
        Type: ui canvas
    Children:
      - Behaviours:
          label:
            Type: text label
            Properties:
              Text: hi
";

		[Test]
		public void KeyedChildrenPromoteTheirKeyToTheRelativeId()
		{
			// The key becomes the child's relative IdSuffix, which the builder
			// prefixes with the parent path (e.g. "ui/hud", "ui/hud/title") so
			// listeners can target nested children by their full hierarchical id.
			var ui = Parse(KeyedChildrenYaml).Entities[0];

			Assert.AreEqual(1, ui.Children.Count);
			Assert.AreEqual("hud", ui.Children[0].IdSuffix);
			Assert.AreEqual("title", ui.Children[0].Children[0].IdSuffix);
		}

		[Test]
		public void ListChildrenAreRejected()
		{
			// Children must be a keyed mapping; the sequence/list form is no longer supported.
			Assert.Catch(() => Parse(ListChildrenYaml));
		}
	}
}
