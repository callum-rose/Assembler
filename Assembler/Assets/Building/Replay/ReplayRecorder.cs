using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours.Triggers.Input;
using Assembler.Input;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using Assembler.Time;

namespace Assembler.Building.Replay
{
	/// <summary>
	/// Captures every input activation at the trigger boundary, bucketed by logical tick (the clock's
	/// <c>FrameCount</c>, which the clock driver advances before any behaviour runs, so all firings in a Unity frame
	/// share a tick). Wired in as <see cref="InputBoundary.Sink"/> during record mode; <see cref="Build"/> emits the
	/// finished <see cref="Replay"/>.
	/// </summary>
	public sealed class ReplayRecorder : IInputSink
	{
		private readonly List<InputFrame> _frames = new();

		private IGameClock _clock = null!;
		private string _descriptorHash = string.Empty;
		private uint _seed;
		private float _fixedDeltaTime;
		private InputPlatform _platform;

		private int _currentTick = -1;
		private List<InputActivation>? _currentActivations;

		/// <summary>Supplies the run header and the clock used to bucket activations. Called by the builder before play.</summary>
		public void Initialise(IGameClock clock, string descriptorHash, uint seed, float fixedDeltaTime, InputPlatform platform)
		{
			_clock = clock;
			_descriptorHash = descriptorHash;
			_seed = seed;
			_fixedDeltaTime = fixedDeltaTime;
			_platform = platform;
		}

		public void Record(BehaviourDescriptor descriptor, TriggerContext ctx)
		{
			var tick = _clock.FrameCount;
			if (_currentActivations == null || tick != _currentTick)
			{
				_currentActivations = new List<InputActivation>();
				_frames.Add(new InputFrame(tick, _currentActivations));
				_currentTick = tick;
			}

			_currentActivations.Add(new InputActivation(descriptor, ExtractPayload(ctx)));
		}

		/// <summary>Builds the recorded session. Safe to call after the run has advanced the desired number of ticks.</summary>
		public Replay Build() => new(_descriptorHash, _seed, _fixedDeltaTime, _platform, _frames);

		// Snapshot the context as ordered key/value pairs. Keys are sorted so the serialized form is canonical
		// regardless of the dictionary's internal iteration order (replay reconstruction is order-independent).
		private static IReadOnlyList<KeyValuePair<string, object>> ExtractPayload(TriggerContext ctx) =>
			ctx.Keys
				.OrderBy(key => key, StringComparer.Ordinal)
				.Select(key => new KeyValuePair<string, object>(key, ctx.Get<object>(key)))
				.ToArray();
	}
}
