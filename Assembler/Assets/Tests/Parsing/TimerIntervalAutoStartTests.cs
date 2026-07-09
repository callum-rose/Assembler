using System;
using System.Collections.Generic;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;

namespace Tests.Parsing
{
	// Pins the default for the timer/interval `AutoStart` property. Omitting it must self-arm
	// (resolve to a constant `true`) — see issue #511, where a bare `timer trigger` silently
	// defaulted to `false` and never fired at runtime.
	public class TimerIntervalAutoStartTests
	{
		private static TransformContext EmptyContext() =>
			new(new List<ValueInfo>(),
				new Dictionary<string, AssemblerValue>(),
				new Dictionary<string, ExpressionInfo>(),
				new Dictionary<string, Type>(),
				new Dictionary<Type, System.Reflection.MethodInfo>(),
				new InlineExpressionAccumulator(),
				RecordSchemaRegistry.Empty);

		[Test]
		public void TimerTrigger_OmittedAutoStart_DefaultsToTrue()
		{
			var info = TimerTriggerInfo.Create("timer", Array.Empty<ListenerInfo>(),
				new Dictionary<string, AssemblerValue> { ["Delay"] = new FloatValue(1f) },
				EmptyContext());

			Assert.AreEqual(new ConstantSource<bool>(true), info.AutoStart);
		}

		[Test]
		public void TimerTrigger_ExplicitFalse_IsHonoured()
		{
			var info = TimerTriggerInfo.Create("timer", Array.Empty<ListenerInfo>(),
				new Dictionary<string, AssemblerValue>
				{
					["Delay"] = new FloatValue(1f),
					["AutoStart"] = new BoolValue(false)
				},
				EmptyContext());

			Assert.AreEqual(new ConstantSource<bool>(false), info.AutoStart);
		}

		[Test]
		public void IntervalTrigger_OmittedAutoStart_DefaultsToTrue()
		{
			var info = IntervalTriggerInfo.Create("interval", Array.Empty<ListenerInfo>(),
				new Dictionary<string, AssemblerValue> { ["Interval"] = new FloatValue(1f) },
				EmptyContext());

			Assert.AreEqual(new ConstantSource<bool>(true), info.AutoStart);
		}

		[Test]
		public void IntervalTrigger_ExplicitFalse_IsHonoured()
		{
			var info = IntervalTriggerInfo.Create("interval", Array.Empty<ListenerInfo>(),
				new Dictionary<string, AssemblerValue>
				{
					["Interval"] = new FloatValue(1f),
					["AutoStart"] = new BoolValue(false)
				},
				EmptyContext());

			Assert.AreEqual(new ConstantSource<bool>(false), info.AutoStart);
		}
	}
}
