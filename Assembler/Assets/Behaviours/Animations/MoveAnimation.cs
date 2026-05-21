using UnityEngine;

namespace Assembler.Behaviours.Animations
{
	public sealed class MoveAnimation : TransformAnimation
	{
		protected override Vector3 ReadCurrent() => transform.position;
		protected override void Apply(Vector3 value) => transform.position = value;
	}
}
