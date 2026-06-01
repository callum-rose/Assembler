using UnityEngine;

namespace Assembler.Behaviours.Animations
{
	/// <summary>Tweens the entity's local scale from Start to End over Duration. See <see cref="TransformAnimation"/>.</summary>
	public sealed class ScaleAnimation : TransformAnimation
	{
		protected override Vector3 Current
		{
			get => transform.localScale;
			set => transform.localScale = value;
		}
	}
}
