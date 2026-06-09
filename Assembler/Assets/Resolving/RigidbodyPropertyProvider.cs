using System;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Resolving
{
	// Live wrapper: every read returns the current rigidbody property, so consumers never see a stale
	// value cached from an earlier frame. Backs the !rigidbody { Id, Property } tag (Vector3-valued).
	// The Rigidbody is added by the `rigidbody` behaviour during behaviour init, so it is fetched
	// lazily off the entity transform and cached once found; absent ⇒ Vector3.zero (no-op on Set).
	public sealed class RigidbodyPropertyProvider : IValueProvider<Vector3>
	{
		private readonly Transform _transform;
		private readonly RigidbodyProperty _property;
		private Rigidbody? _rigidbody;

		public RigidbodyPropertyProvider(Transform transform, RigidbodyProperty property)
		{
			_transform = transform;
			_property = property;
		}

		public Vector3 Get(TriggerContext ctx) => Read();

		object IValueProvider.Get(TriggerContext ctx) => Read();

		public void Set(Vector3 value)
		{
			var rigidbody = Resolve();

			if (rigidbody == null)
			{
				return;
			}

			switch (_property)
			{
				case RigidbodyProperty.Velocity:
					rigidbody.linearVelocity = value;
					break;
				case RigidbodyProperty.AngularVelocity:
					rigidbody.angularVelocity = value;
					break;
				case RigidbodyProperty.Position:
					rigidbody.position = value;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private Vector3 Read()
		{
			var rigidbody = Resolve();

			if (rigidbody == null)
			{
				return Vector3.zero;
			}

			return _property switch
			{
				RigidbodyProperty.Velocity => rigidbody.linearVelocity,
				RigidbodyProperty.AngularVelocity => rigidbody.angularVelocity,
				RigidbodyProperty.Position => rigidbody.position,
				_ => throw new ArgumentOutOfRangeException()
			};
		}

		private Rigidbody? Resolve()
		{
			if (_rigidbody == null)
			{
				_transform.TryGetComponent(out _rigidbody);
			}

			return _rigidbody;
		}
	}
}
