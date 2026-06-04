using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;

namespace Tests.Parsing
{
	public class StateMachineInfoTests
	{
		private static TransformContext EmptyContext() =>
			new(new List<ValueInfo>(),
				new Dictionary<string, AssemblerValue>(),
				new Dictionary<string, ExpressionInfo>(),
				new Dictionary<string, Type>(),
				new Dictionary<Type, System.Reflection.MethodInfo>(),
				new InlineExpressionAccumulator());

		private static AssemblerValue States(params string[] names) =>
			new DictValue(names.ToDictionary(
				n => n,
				n => (AssemblerValue)new DictValue(new Dictionary<string, AssemblerValue>())));

		private static AssemblerValue Transition(string from, string to) =>
			new DictValue(new Dictionary<string, AssemblerValue>
			{
				["from"] = new StringValue(from),
				["to"] = new StringValue(to),
				["when"] = new BoolValue(true)
			});

		private static StateMachineInfo Create(IReadOnlyDictionary<string, AssemblerValue> props) =>
			StateMachineInfo.Create("ai", Array.Empty<ListenerInfo>(), props, EmptyContext());

		[Test]
		public void Create_ValidMachine_ParsesStatesAndTransitions()
		{
			var info = Create(new Dictionary<string, AssemblerValue>
			{
				["StateVariable"] = new StringValue("ai_state"),
				["Initial"] = new StringValue("patrol"),
				["States"] = States("patrol", "chase", "flee"),
				["Transitions"] = new ListValue(new[]
				{
					Transition("patrol", "chase"),
					Transition("chase", "flee")
				})
			});

			Assert.AreEqual("ai_state", info.StateVariable);
			Assert.AreEqual("patrol", info.Initial);
			Assert.AreEqual(3, info.States.Count);
			Assert.AreEqual(2, info.Transitions.Count);
			Assert.AreEqual("chase", info.Transitions[0].To);
		}

		[Test]
		public void Create_TransitionToUnknownState_Throws()
		{
			var ex = Assert.Throws<ParsingException>(() => Create(new Dictionary<string, AssemblerValue>
			{
				["StateVariable"] = new StringValue("ai_state"),
				["Initial"] = new StringValue("patrol"),
				["States"] = States("patrol", "chase"),
				["Transitions"] = new ListValue(new[] { Transition("patrol", "chaze") })
			}));

			StringAssert.Contains("chaze", ex!.Message);
		}

		[Test]
		public void Create_TransitionFromUnknownState_Throws()
		{
			var ex = Assert.Throws<ParsingException>(() => Create(new Dictionary<string, AssemblerValue>
			{
				["StateVariable"] = new StringValue("ai_state"),
				["Initial"] = new StringValue("patrol"),
				["States"] = States("patrol", "chase"),
				["Transitions"] = new ListValue(new[] { Transition("patroll", "chase") })
			}));

			StringAssert.Contains("patroll", ex!.Message);
		}

		[Test]
		public void Create_InitialNotADeclaredState_Throws()
		{
			var ex = Assert.Throws<ParsingException>(() => Create(new Dictionary<string, AssemblerValue>
			{
				["StateVariable"] = new StringValue("ai_state"),
				["Initial"] = new StringValue("idle"),
				["States"] = States("patrol", "chase"),
				["Transitions"] = new ListValue(Array.Empty<AssemblerValue>())
			}));

			StringAssert.Contains("idle", ex!.Message);
		}

		[Test]
		public void Create_TransitionMissingWhen_Throws()
		{
			var transitionWithoutWhen = new DictValue(new Dictionary<string, AssemblerValue>
			{
				["from"] = new StringValue("patrol"),
				["to"] = new StringValue("chase")
			});

			var ex = Assert.Throws<ParsingException>(() => Create(new Dictionary<string, AssemblerValue>
			{
				["StateVariable"] = new StringValue("ai_state"),
				["Initial"] = new StringValue("patrol"),
				["States"] = States("patrol", "chase"),
				["Transitions"] = new ListValue(new AssemblerValue[] { transitionWithoutWhen })
			}));

			StringAssert.Contains("when", ex!.Message);
		}

		[Test]
		public void Create_StateWithHooks_ParsesDirectAndNestedGameOverListeners()
		{
			var onEnter = new ListValue(new AssemblerValue[]
			{
				new DictValue(new Dictionary<string, AssemblerValue>
				{
					["EntityId"] = new StringValue("enemy"),
					["BehaviourId"] = new StringValue("play chase anim")
				}),
				new GameOverMarker() // a nested `!gameover` survives as this marker
			});

			var states = new DictValue(new Dictionary<string, AssemblerValue>
			{
				["patrol"] = new DictValue(new Dictionary<string, AssemblerValue>()),
				["chase"] = new DictValue(new Dictionary<string, AssemblerValue> { ["OnEnter"] = onEnter })
			});

			var info = Create(new Dictionary<string, AssemblerValue>
			{
				["StateVariable"] = new StringValue("ai_state"),
				["Initial"] = new StringValue("patrol"),
				["States"] = states,
				["Transitions"] = new ListValue(new[] { Transition("patrol", "chase") })
			});

			var chase = info.States.Single(s => s.Name == "chase");
			Assert.AreEqual(2, chase.OnEnter.Count);
			Assert.IsInstanceOf<DirectListenerInfo>(chase.OnEnter[0]);
			Assert.IsInstanceOf<GameOverListenerInfo>(chase.OnEnter[1]);
		}

		[Test]
		public void Create_MissingStateVariable_Throws()
		{
			var ex = Assert.Throws<ParsingException>(() => Create(new Dictionary<string, AssemblerValue>
			{
				["Initial"] = new StringValue("patrol"),
				["States"] = States("patrol"),
				["Transitions"] = new ListValue(Array.Empty<AssemblerValue>())
			}));

			StringAssert.Contains("StateVariable", ex!.Message);
		}
	}
}
