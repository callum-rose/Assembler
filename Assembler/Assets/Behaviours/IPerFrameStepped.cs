namespace Assembler.Behaviours
{
	/// <summary>
	/// The non-generic surface a central driver uses to advance a per-frame behaviour without knowing its
	/// <c>TData</c>. Implemented by <see cref="PerFrameBehaviour{TData}"/>; the run's <c>BehaviourRegistry</c>
	/// collects every implementer in registration order and a <c>PerFrameDriver</c> steps them in that order once
	/// per advanced game frame, so a shared-velocity stack (acceleration → drag → speed limit → velocity) runs in a
	/// deterministic order instead of Unity's undefined per-component <c>Update</c> order (issue #241).
	/// </summary>
	public interface IPerFrameStepped
	{
		/// <summary>Perform this behaviour's per-frame work. Called once per advanced game frame by the driver.</summary>
		void Step();
	}
}
