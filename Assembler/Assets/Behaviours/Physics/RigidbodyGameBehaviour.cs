using Assembler.Resolving;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>
	/// Base for behaviours that act on the entity's <see cref="Rigidbody"/> — add force/impulse/torque, set
	/// linear/angular velocity, etc. Centralises the fetch-on-initialise + lazy re-fetch dance every such
	/// behaviour repeated, and turns a missing Rigidbody from a silent no-op into a one-time warning: a
	/// descriptor that drives forces on an entity that never added a <c>rigidbody</c> behaviour is a common
	/// (and otherwise invisible) authoring mistake.
	/// </summary>
	/// <remarks>
	/// Clock-aware (<see cref="INeedsGameClock"/>) so a continuous behaviour can scale by <see cref="IGameClock.DeltaTime"/>
	/// rather than the frame rate — e.g. an <c>add force</c> fired from a per-frame trigger applies one frame's worth of
	/// impulse per call instead of a whole force, so thrust doesn't scale with refresh rate (and stops while paused). The
	/// build pipeline injects the shared clock.
	/// </remarks>
	/// <typeparam name="TData">The behaviour's resolved data type.</typeparam>
	public abstract class RigidbodyGameBehaviour<TData> : GameBehaviour<TData>, IAmExecutable, INeedsGameClock
		where TData : BehaviourData
	{
		public IGameClock Clock { get; set; } = null!;

		private Rigidbody? _rigidbody;
		private bool _warnedMissing;

		/// <summary>
		/// The elapsed time of the step this behaviour is currently executing in, for converting a continuous force
		/// into a per-step impulse. Inside a physics step (a fixed-update or collision trigger) that is the constant
		/// fixed timestep — so an <c>F * StepDelta</c> impulse equals the old <c>ForceMode.Force</c> of <c>F</c>;
		/// otherwise it is the game-clock frame delta, so a per-frame trigger's force is frame-rate independent (and
		/// zero while paused).
		/// </summary>
		protected float StepDelta =>
			UnityEngine.Time.inFixedTimeStep ? UnityEngine.Time.fixedDeltaTime : Clock.DeltaTime;

		protected override void OnInitialise(TData data) => _rigidbody = GetComponent<Rigidbody>();

		public void Execute(TriggerContext ctx)
		{
			// A Rigidbody can be added after this behaviour initialises (build order isn't guaranteed), so
			// re-fetch lazily before giving up.
			if (_rigidbody == null)
			{
				_rigidbody = GetComponent<Rigidbody>();
			}

			if (_rigidbody == null)
			{
				WarnMissingOnce();
				return;
			}

			Apply(_rigidbody, ctx);
		}

		/// <summary>Apply this behaviour's effect to the resolved <paramref name="rigidbody"/>.</summary>
		protected abstract void Apply(Rigidbody rigidbody, TriggerContext ctx);

		private void WarnMissingOnce()
		{
			if (_warnedMissing)
			{
				return;
			}

			_warnedMissing = true;
			UnityEngine.Debug.LogWarning(
				$"{GetType().Name} '{Id}' on entity '{Entity.Id}' found no Rigidbody, so it does nothing. Add a " +
				"`rigidbody` behaviour to this entity for physics force/velocity behaviours to take effect.");
		}
	}
}
