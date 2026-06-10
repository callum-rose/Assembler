using System;
using Assembler.Parsing.Info;
using Assembler.Time;

namespace Assembler.Resolving
{
	// Live wrapper over the injected game clock: every read returns the current value of the requested
	// property, so consumers see the frozen-while-paused / scaled-by-timescale value each frame rather
	// than a stale snapshot. Mirrors TransformPropertyProvider. T is one of float, int or double.
	public sealed class ClockValueProvider<T> : IValueProvider<T>
	{
		private readonly IGameClock _clock;
		private readonly ClockProperty _property;

		public ClockValueProvider(IGameClock clock, ClockProperty property)
		{
			_clock = clock;
			_property = property;
		}

		public T Get(TriggerContext ctx)
		{
			object value = _property switch
			{
				ClockProperty.DeltaTime => _clock.DeltaTime,
				ClockProperty.UnscaledDeltaTime => _clock.UnscaledDeltaTime,
				ClockProperty.Time => _clock.Time,
				ClockProperty.FrameCount => _clock.FrameCount,
				_ => throw new ArgumentOutOfRangeException(nameof(_property), _property, "Unknown clock property")
			};

			return (T)Convert.ChangeType(value, typeof(T));
		}

		object IValueProvider.Get(TriggerContext ctx) => Get(ctx)!;
	}
}
