using System;
using System.Collections.Generic;
using Assembler.Input;
using Assembler.Parsing.Controls;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Input
{
	public class ControlsValidatorTests
	{
		private static GameInfo GameUsing(string actionName)
		{
			var trigger = new InputActionTriggerInfo(
				"trigger",
				Array.Empty<ListenerInfo>(),
				new ConstantSource<string>(actionName));

			var entity = new ConcreteEntityInfo(
				"player",
				Array.Empty<string>(),
				None<Vector3>.Instance,
				None<Vector3>.Instance,
				new BehaviourInfo[] { trigger },
				Array.Empty<ValueInfo>(),
				Array.Empty<ChildEntityInfo>());

			return new GameInfo(
				new AboutInfo("t", "d"),
				new WorldInfo(2, Color.black),
				new PhysicsInfo(Vector3.zero),
				Array.Empty<AssetInfo>(),
				Array.Empty<ValueInfo>(),
				Array.Empty<ExpressionInfo>(),
				Array.Empty<EntityInfo>(),
				new[] { entity },
				None<bool>.Instance);
		}

		private static ControlsInfo Controls(bool declareAction, bool bindOnDesktop)
		{
			var actions = new Dictionary<string, ActionInfo>();
			if (declareAction)
			{
				actions["aim"] = new ActionInfo("aim", ActionKind.Button, ButtonPhase.Hold, null);
			}

			var desktop = new Dictionary<string, IReadOnlyList<BindingInfo>>();
			if (bindOnDesktop)
			{
				desktop["aim"] = new[] { BindingInfo.Simple("<Keyboard>/space") };
			}

			var bindings = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<BindingInfo>>>
			{
				["desktop"] = desktop
			};

			return new ControlsInfo(actions, bindings);
		}

		[Test]
		public void Throws_WhenUsedActionIsUndeclared()
		{
			Assert.Throws<InvalidOperationException>(() =>
				ControlsValidator.Validate(GameUsing("aim"), Controls(declareAction: false, bindOnDesktop: false), "desktop"));
		}

		[Test]
		public void Throws_WhenUsedActionHasNoBindingForPlatform()
		{
			Assert.Throws<InvalidOperationException>(() =>
				ControlsValidator.Validate(GameUsing("aim"), Controls(declareAction: true, bindOnDesktop: false), "desktop"));
		}

		[Test]
		public void Passes_WhenActionIsDeclaredAndBound()
		{
			Assert.DoesNotThrow(() =>
				ControlsValidator.Validate(GameUsing("aim"), Controls(declareAction: true, bindOnDesktop: true), "desktop"));
		}

		[Test]
		public void FallbackResolvesToDesktop_WhenPlatformHasNoBindings()
		{
			var controls = Controls(declareAction: true, bindOnDesktop: true);

			Assert.AreEqual("desktop", PlatformFallback.ResolveGroup(InputPlatform.Mobile, controls));
		}
	}
}
