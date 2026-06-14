using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>Adds a Unity Rigidbody to the entity so it participates in physics simulation.</summary>
	/// <remarks>
	/// Properties:
	///   UseGravity: When true the rigidbody is affected by gravity.
	///   IsKinematic: When true the rigidbody ignores forces and is moved only by transform writes.
	///   Mass: Mass of the rigidbody in kilograms.
	///   LinearDamping: Damping applied to linear velocity (Unity's Rigidbody.linearDamping / drag).
	///   AngularDamping: Damping applied to angular velocity (Unity's Rigidbody.angularDamping).
	///   FreezePosition: Per-axis position freeze (any non-zero component locks that axis, e.g. (1, 0, 1) freezes X and Z).
	///   FreezeRotation: Per-axis rotation freeze (any non-zero component locks that axis).
	///   CentreOfMass: Local-space centre of mass offset. Overrides Unity's auto-computed centre so the body rotates and balances about this point (e.g. push it back to spin a vehicle about its rear axle). Omit to keep the automatic centre.
	/// </remarks>
	public sealed class RigidbodyBehaviour : GameBehaviour<RigidbodyData>
	{
		private Rigidbody _rigidbody;

		protected override void OnInitialise(RigidbodyData data)
		{
			// Create the body once and reuse it across pooled lives: OnInitialise re-runs on every reuse, so a
			// guard (not an unconditional AddComponent) keeps the entity's single Rigidbody rather than stacking a
			// second one. A guard rather than Awake because Awake does not run in edit mode (the sandbox validator
			// and EditMode tests build via OnInitialise without entering play mode).
			if (_rigidbody == null)
			{
				_rigidbody = gameObject.AddComponent<Rigidbody>();
			}

			data.IsKinematic.UseIfValueExists(v => _rigidbody.isKinematic = v);
			data.UseGravity.UseIfValueExists(v => _rigidbody.useGravity = v);
			data.Mass.UseIfValueExists(v => _rigidbody.mass = v);
			data.LinearDamping.UseIfValueExists(v => _rigidbody.linearDamping = v);
			data.AngularDamping.UseIfValueExists(v => _rigidbody.angularDamping = v);
			data.FreezePosition.UseIfValueExists(v => _rigidbody.constraints = ApplyPositionFreeze(_rigidbody.constraints, v));
			data.FreezeRotation.UseIfValueExists(v => _rigidbody.constraints = ApplyRotationFreeze(_rigidbody.constraints, v));
			data.CentreOfMass.UseIfValueExists(v => _rigidbody.centerOfMass = v);
		}

		// A pooled body carries its previous life's momentum; a respawned entity must start at rest. Kinematic
		// bodies hold no solver velocity, so leave them untouched (writing velocity on one is meaningless).
		public override void OnReuse()
		{
			if (_rigidbody.isKinematic)
			{
				return;
			}

			_rigidbody.linearVelocity = Vector3.zero;
			_rigidbody.angularVelocity = Vector3.zero;
		}

		private static RigidbodyConstraints ApplyPositionFreeze(RigidbodyConstraints current, Vector3 freeze)
		{
			current &= ~(RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ);
			if (freeze.x != 0f)
			{
				current |= RigidbodyConstraints.FreezePositionX;
			}

			if (freeze.y != 0f)
			{
				current |= RigidbodyConstraints.FreezePositionY;
			}

			if (freeze.z != 0f)
			{
				current |= RigidbodyConstraints.FreezePositionZ;
			}

			return current;
		}

		private static RigidbodyConstraints ApplyRotationFreeze(RigidbodyConstraints current, Vector3 freeze)
		{
			current &= ~(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ);
			if (freeze.x != 0f)
			{
				current |= RigidbodyConstraints.FreezeRotationX;
			}

			if (freeze.y != 0f)
			{
				current |= RigidbodyConstraints.FreezeRotationY;
			}

			if (freeze.z != 0f)
			{
				current |= RigidbodyConstraints.FreezeRotationZ;
			}

			return current;
		}
	}
}
