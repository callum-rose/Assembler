using System;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Resolving
{
	// Live wrapper: every read returns the current rigidbody property, so consumers never see a stale
	// value cached from an earlier frame. Backs the !rigidbody { Id, Property } tag (Vector3-valued).
	public sealed class RigidbodyPropertyProvider : IWriteValueProvider<Vector3>
	{
		private readonly Rigidbody _rigidbody;
		private readonly RigidbodyProperty _property;

		public RigidbodyPropertyProvider(Transform transform, RigidbodyProperty property)
		{
			_rigidbody = transform.GetComponent<Rigidbody>();
			_property = property;
		}

		public Vector3 Get(TriggerContext ctx) => Read();

		object IValueProvider.Get(TriggerContext ctx) => Read();

		public void Set(Vector3 value)
		{
			switch (_property)
			{
				case RigidbodyProperty.Velocity:
					_rigidbody.linearVelocity = value;
					break;
				case RigidbodyProperty.AngularVelocity:
					_rigidbody.angularVelocity = value;
					break;
				case RigidbodyProperty.Position:
					_rigidbody.position = value;
					break;
				case RigidbodyProperty.Rotation:
					_rigidbody.rotation = Quaternion.Euler(value);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private Vector3 Read() =>
			_property switch
			{
				RigidbodyProperty.Velocity => _rigidbody.linearVelocity,
				RigidbodyProperty.AngularVelocity => _rigidbody.angularVelocity,
				RigidbodyProperty.Position => _rigidbody.position,
				RigidbodyProperty.Rotation => _rigidbody.rotation.eulerAngles,
				_ => throw new ArgumentOutOfRangeException()
			};
	}
}
