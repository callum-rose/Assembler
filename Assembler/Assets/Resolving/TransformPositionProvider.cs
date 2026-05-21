using System;
using UnityEngine;

namespace Assembler.Resolving
{
	// Live wrapper: every Value read returns the current transform.position, so
	// consumers never see a stale value cached from an earlier frame.
	public sealed class TransformPositionProvider : IValueProvider<Vector3>
	{
		private readonly Transform _transform;

		public TransformPositionProvider(Transform transform)
		{
			_transform = transform;
		}

		public Vector3 Value
		{
			get => _transform.position;
			set => throw new InvalidOperationException("TransformPositionProvider is read-only");
		}

		object IValueProvider.Value => _transform.position;
	}
}
