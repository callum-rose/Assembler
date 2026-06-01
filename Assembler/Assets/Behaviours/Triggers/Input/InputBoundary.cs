using Assembler.Parsing.Info;
using Assembler.Resolving;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Receives every input activation as it fires, for recording. Implemented Building-side by the recorder.</summary>
	public interface IInputSink
	{
		/// <summary>Records that <paramref name="descriptor"/>'s trigger fired with the given emitted context this tick.</summary>
		void Record(BehaviourDescriptor descriptor, TriggerContext ctx);
	}

	/// <summary>Marker for the replay-driving source (implemented Building-side by the player). Drives <see cref="IReplayableInputTrigger.ReplayFire"/> per tick.</summary>
	public interface IInputSource
	{
	}

	/// <summary>An input trigger that can be re-fired from a recorded context during replay.</summary>
	public interface IReplayableInputTrigger
	{
		/// <summary>Re-fires this trigger with a recorded context, bypassing live device polling.</summary>
		void ReplayFire(TriggerContext ctx);
	}

	/// <summary>Marker for behaviours the build pipeline injects the per-run <see cref="InputBoundary"/> into (mirrors <c>INeedsGameClock</c>).</summary>
	public interface INeedsInputBoundary
	{
		InputBoundary InputBoundary { set; }
	}

	/// <summary>
	/// The seam between live input and record/replay for a single run. Input triggers consult their injected
	/// boundary when firing: in normal mode they record (if a <see cref="Sink"/> is attached) and notify; in replay
	/// mode they go silent so the <see cref="Source"/> can re-inject recorded activations instead. One instance is
	/// created per build and injected into every input trigger, so there is no shared static state to leak between
	/// runs. See the Determinism (Level 1) section in CLAUDE.md.
	/// </summary>
	public sealed class InputBoundary
	{
		/// <summary>True while a replay is driving input; live triggers suppress their own firing.</summary>
		public bool ReplayActive { get; private set; }

		/// <summary>The active recorder, or null when not recording.</summary>
		public IInputSink? Sink { get; set; }

		/// <summary>The active replay source, or null when not replaying.</summary>
		public IInputSource? Source { get; private set; }

		/// <summary>Switches this boundary into replay mode with the given source.</summary>
		public void BeginReplay(IInputSource source)
		{
			Source = source;
			ReplayActive = true;
		}
	}
}
