using System.Collections.Generic;
using Assembler.Behaviours;
using Assembler.Behaviours.Triggers.Input;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	/// <summary>
	/// Live device polling is impractical to drive in a unit test, so this locks in the value-forwarding shape of
	/// the <c>input action</c> relay: a value action emits axis/x/y into the trigger context exactly like the
	/// legacy AxisTrigger, so downstream gameplay stays binding-agnostic.
	/// </summary>
	public class InputActionTriggerTests
	{
		private sealed class CapturingListener : Listener
		{
			public TriggerContext? Last { get; private set; }
			public int CallCount { get; private set; }

			public CapturingListener() : base(new Dictionary<string, string>()) { }

			public override void Notify(TriggerContext ctx)
			{
				Last = Prepare(ctx);
				CallCount++;
			}
		}

		[Test]
		public void ValueAction_ForwardsVector2AsAxisXY()
		{
			var go = new GameObject("InputActionValueTest");
			try
			{
				var trigger = go.AddComponent<InputActionTrigger>();
				var listener = new CapturingListener();

				// Null InputAction: the notify path under test takes the Vector2 directly, so no device is needed.
				trigger.Initialise(
					new InputActionTriggerData("aim", "aim", ActionKind.Value, ButtonPhase.Hold, null),
					new List<Listener> { listener });

				trigger.Emit(new Vector2(0.5f, -0.25f));

				Assert.AreEqual(1, listener.CallCount);
				Assert.AreEqual(new Vector2(0.5f, -0.25f), listener.Last!.Get<Vector2>("axis"));
				Assert.AreEqual(0.5f, listener.Last!.Get<float>("x"));
				Assert.AreEqual(-0.25f, listener.Last!.Get<float>("y"));
			}
			finally
			{
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void BuildValueContext_MirrorsAxisTriggerOutputs()
		{
			var ctx = InputActionTrigger.BuildValueContext(new Vector2(1f, 2f));

			Assert.AreEqual(new Vector2(1f, 2f), ctx.Get<Vector2>("axis"));
			Assert.AreEqual(1f, ctx.Get<float>("x"));
			Assert.AreEqual(2f, ctx.Get<float>("y"));
		}
	}
}
