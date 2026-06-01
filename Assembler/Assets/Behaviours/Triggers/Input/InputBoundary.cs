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

	/// <summary>
	/// The single seam between live input and record/replay. Input triggers consult this when firing: in normal
	/// mode they record (if a <see cref="Sink"/> is attached) and notify; in replay mode they go silent so the
	/// <see cref="Source"/> can re-inject recorded activations instead. Single-threaded (Unity main thread); see
	/// the Determinism (Level 1) section in CLAUDE.md.
	/// </summary>
	public static class InputBoundary
	{
		/// <summary>True while a replay is driving input; live triggers suppress their own firing.</summary>
		public static bool ReplayActive { get; private set; }

		/// <summary>The active recorder, or null when not recording.</summary>
		public static IInputSink? Sink { get; set; }

		/// <summary>The active replay source, or null when not replaying.</summary>
		public static IInputSource? Source { get; private set; }

		/// <summary>Switches into replay mode with the given source.</summary>
		public static void BeginReplay(IInputSource source)
		{
			Source = source;
			ReplayActive = true;
		}

		/// <summary>Clears all record/replay state. Called at build start and on game teardown so nothing leaks between runs.</summary>
		public static void Reset()
		{
			Sink = null;
			Source = null;
			ReplayActive = false;
		}
	}
}
