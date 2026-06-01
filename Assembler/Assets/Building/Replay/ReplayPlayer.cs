using System.Collections.Generic;
using Assembler.Behaviours.Triggers.Input;
using Assembler.Resolving;
using Assembler.Time;

namespace Assembler.Building.Replay
{
	/// <summary>
	/// Drives input from a recorded <see cref="Replay"/>. Each tick it looks up the recorded <see cref="InputFrame"/>
	/// for the clock's current <c>FrameCount</c>, resolves each activation's trigger via the registry, and re-fires it
	/// with the recorded payload in recorded order. Wired in as <see cref="InputBoundary.Source"/> during replay; the
	/// live triggers go silent so only these recorded activations advance gameplay.
	/// </summary>
	public sealed class ReplayPlayer : IInputSource
	{
		private readonly Dictionary<int, InputFrame> _framesByTick = new();

		private IGameClock _clock = null!;
		private IReadOnlyBehaviourRegistry _registry = null!;

		public ReplayPlayer(Replay replay)
		{
			Replay = replay;
			foreach (var frame in replay.Frames)
			{
				_framesByTick[frame.Tick] = frame;
			}
		}

		/// <summary>The session being replayed. Used by the builder to validate the descriptor hash and force the clock config.</summary>
		public Replay Replay { get; }

		/// <summary>Supplies the live clock and registry. Called by the builder after the behaviour graph is registered.</summary>
		public void Initialise(IGameClock clock, IReadOnlyBehaviourRegistry registry)
		{
			_clock = clock;
			_registry = registry;
		}

		/// <summary>Re-injects this tick's recorded activations. Driven once per frame by <see cref="ReplayDriver"/>.</summary>
		public void PlayTick()
		{
			if (!_framesByTick.TryGetValue(_clock.FrameCount, out var frame))
			{
				return;
			}

			foreach (var activation in frame.Activations)
			{
				if (_registry[activation.Trigger] is IReplayableInputTrigger trigger)
				{
					trigger.ReplayFire(TriggerContext.Empty.WithMany(activation.Payload));
				}
			}
		}
	}
}
