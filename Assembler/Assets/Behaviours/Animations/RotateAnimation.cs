using UnityEngine;

namespace Assembler.Behaviours.Animations
{
	public sealed class RotateAnimation : TransformAnimation
	{
		protected override Vector3 ReadCurrent() => transform.eulerAngles;
		protected override void Apply(Vector3 value) => transform.eulerAngles = value;
	}
}
