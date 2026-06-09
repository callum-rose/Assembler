using System;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Resolving
{
	// Live wrapper: every read returns the current transform property, so consumers never see a stale
	// value cached from an earlier frame. Backs the !entity { Id, Property } tag (Vector3-valued).
	public sealed class TransformPropertyProvider : IValueProvider<Vector3>
	{
		private readonly Transform _transform;
		private readonly EntityProperty _property;

		public TransformPropertyProvider(Transform transform, EntityProperty property)
		{
			_transform = transform;
			_property = property;
		}

		public Vector3 Get(TriggerContext ctx) => Read();

		object IValueProvider.Get(TriggerContext ctx) => Read();

		public void Set(Vector3 value)
		{
			switch (_property)
			{
				case EntityProperty.Position:
					_transform.position = value;
					break;
				case EntityProperty.Rotation:
					_transform.eulerAngles = value;
					break;
				case EntityProperty.Scale:
					_transform.localScale = value;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private Vector3 Read() =>
			_property switch
			{
				EntityProperty.Position => _transform.position,
				EntityProperty.Rotation => _transform.eulerAngles,
				EntityProperty.Scale => _transform.localScale,
				_ => throw new ArgumentOutOfRangeException()
			};
	}
}
