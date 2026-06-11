using System;
using Assembler.Resolving;

namespace Assembler.Behaviours
{
	/// <summary>Implemented by behaviours that do meaningful work when invoked by a trigger's
	/// listener — i.e. valid <c>Listeners:</c> targets. The interface owns the <see cref="Execute"/>
	/// contract: behaviours that are pure event sources (most triggers) or self-driven (run every
	/// frame via <c>Update</c>) do NOT implement it and expose no <c>Execute</c>, so targeting them
	/// is a build-time error. Orthogonal to <c>Trigger&lt;T&gt;</c> — forwarding/gating triggers
	/// (condition gates, debounce, throttle, deferred, …) implement it too, since they receive an
	/// Execute and conditionally re-emit.</summary>
	public interface IAmExecutable
	{
		void Execute(TriggerContext ctx);
	}

	public static class ExecutableExtensions
	{
		/// <summary>Casts a resolved listener target to <see cref="IAmExecutable"/>, throwing a descriptive
		/// error when it is not — i.e. when a listener resolves to a trigger or self-driven behaviour that does
		/// nothing when invoked (see issue #201). Build-time-known targets are checked when the listener is
		/// built; tagged listeners resolve targets dynamically and check at notify time.
		/// <paramref name="targetDescription"/> names what the listener pointed at, e.g.
		/// <c>"targeting behaviour 'x' on entity 'y'"</c>.</summary>
		public static IAmExecutable EnsureExecutable(this GameBehaviour behaviour, string targetDescription) =>
			behaviour as IAmExecutable ?? throw new InvalidOperationException(
				$"Listener {targetDescription} resolved to behaviour '{behaviour.GetType().Name}', which is not " +
				"executable — it is a trigger or continuous (self-driven) behaviour that runs itself and does " +
				"nothing when invoked by a listener. For input-driven motion, mutate a velocity variable that a " +
				"single `velocity` behaviour integrates, or use `translate`.");
	}
}
