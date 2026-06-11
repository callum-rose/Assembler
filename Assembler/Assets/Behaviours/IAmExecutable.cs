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
}
