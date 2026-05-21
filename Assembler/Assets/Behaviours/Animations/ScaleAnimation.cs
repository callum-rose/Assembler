using UnityEngine;

namespace Assembler.Behaviours.Animations
{
	public sealed class ScaleAnimation : TransformAnimation
	{
		protected override Vector3 ReadCurrent() => transform.localScale;
		protected override void Apply(Vector3 value) => transform.localScale = value;
	}
}
