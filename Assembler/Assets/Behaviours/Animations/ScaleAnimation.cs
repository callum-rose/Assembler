using UnityEngine;

namespace Assembler.Behaviours.Animations
{
	public sealed class ScaleAnimation : TransformAnimation
	{
		protected override Vector3 Current
		{
			get => transform.localScale;
			set => transform.localScale = value;
		}
	}
}
