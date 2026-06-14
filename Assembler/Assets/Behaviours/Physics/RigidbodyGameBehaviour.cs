using Assembler.Resolving;
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
	/// <typeparam name="TData">The behaviour's resolved data type.</typeparam>
	public abstract class RigidbodyGameBehaviour<TData> : GameBehaviour<TData>, IAmExecutable
		where TData : BehaviourData
	{
		private Rigidbody? _rigidbody;
		private bool _warnedMissing;

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
