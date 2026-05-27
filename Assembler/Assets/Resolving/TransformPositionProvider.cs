using UnityEngine;

namespace Assembler.Resolving
{
	// Live wrapper: every Value read returns the current transform.position, so
	// consumers never see a stale value cached from an earlier frame.
	public sealed class TransformPositionProvider : IValueProvider<Vector3>
	{
		public Vector3 Value
		{
			get => _transform.position;
			set => _transform.position = value;
		}

		object IValueProvider.Value => _transform.position;
		
		private readonly Transform _transform;

		public TransformPositionProvider(Transform transform)
		{
			_transform = transform;
		}

	}
}
