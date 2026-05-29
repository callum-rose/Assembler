using UnityEngine;

namespace Assembler.Resolving
{
	// Live wrapper: every read returns the current transform.position, so consumers never see a stale value
	// cached from an earlier frame.
	public sealed class TransformPositionProvider : IValueProvider<Vector3>
	{
		private readonly Transform _transform;

		public TransformPositionProvider(Transform transform)
		{
			_transform = transform;
		}

		public Vector3 Get(TriggerContext ctx) => _transform.position;

		object IValueProvider.Get(TriggerContext ctx) => _transform.position;

		public void Set(Vector3 value) => _transform.position = value;
	}
}
