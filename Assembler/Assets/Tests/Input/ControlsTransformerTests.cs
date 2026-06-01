using Assembler.Deserialisation;
using Assembler.Parsing.Controls;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;

namespace Tests.Input
{
	public class ControlsTransformerTests
	{
		private const string Yaml = @"
Controls:
  Actions:
    move-up:   { Type: button, Phase: hold }
    fire:      { Type: button, Phase: down }
    aim:       { Type: value, ValueType: vector2 }
  Bindings:
    desktop:
      move-up: [ ""<Keyboard>/w"" ]
      fire:    [ ""<Mouse>/leftButton"" ]
      aim:
        - Composite: 2DVector
          Up: ""<Keyboard>/w""
          Down: ""<Keyboard>/s""
          Left: ""<Keyboard>/a""
          Right: ""<Keyboard>/d""
    gamepad:
      aim: [ ""<Gamepad>/leftStick"" ]
";

		private static ControlsInfo Transform()
		{
			var dto = new GameFileParser().Parse(Yaml);
			return ControlsTransformer.Transform(dto.Controls);
		}

		[Test]
		public void ParsesActionKindsAndPhases()
		{
			var controls = Transform();

			Assert.AreEqual(3, controls.Actions.Count);

			Assert.AreEqual(ActionKind.Button, controls.Actions["move-up"].Kind);
			Assert.AreEqual(ButtonPhase.Hold, controls.Actions["move-up"].Phase);

			Assert.AreEqual(ActionKind.Button, controls.Actions["fire"].Kind);
			Assert.AreEqual(ButtonPhase.Down, controls.Actions["fire"].Phase);

			Assert.AreEqual(ActionKind.Value, controls.Actions["aim"].Kind);
			Assert.AreEqual("vector2", controls.Actions["aim"].ValueType);
		}

		[Test]
		public void ParsesSimpleBindingPaths()
		{
			var controls = Transform();

			var binding = controls.Bindings["desktop"]["move-up"][0];

			Assert.IsFalse(binding.IsComposite);
			Assert.AreEqual("<Keyboard>/w", binding.Path);
		}

		[Test]
		public void ParsesCompositeBindings()
		{
			var controls = Transform();

			var binding = controls.Bindings["desktop"]["aim"][0];

			Assert.IsTrue(binding.IsComposite);
			Assert.AreEqual("2DVector", binding.Composite);
			Assert.AreEqual("<Keyboard>/w", binding.Parts["Up"]);
			Assert.AreEqual("<Keyboard>/d", binding.Parts["Right"]);
		}

		[Test]
		public void GroupsBindingsByPlatform()
		{
			var controls = Transform();

			Assert.IsTrue(controls.Bindings.ContainsKey("desktop"));
			Assert.IsTrue(controls.Bindings.ContainsKey("gamepad"));
			Assert.AreEqual("<Gamepad>/leftStick", controls.Bindings["gamepad"]["aim"][0].Path);
		}

		[Test]
		public void NullControlsYieldsEmpty()
		{
			var controls = ControlsTransformer.Transform(null);

			Assert.AreEqual(0, controls.Actions.Count);
			Assert.AreEqual(0, controls.Bindings.Count);
		}
	}
}
