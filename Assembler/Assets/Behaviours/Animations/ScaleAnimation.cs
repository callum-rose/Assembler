using UnityEngine;

namespace Assembler.Behaviours.Animations
{
	/// <summary>Tweens the entity's local scale from Start to End over Duration, then notifies listeners on completion.</summary>
	public sealed class ScaleAnimation : TransformAnimation
	{
		protected override Vector3 Current
		{
			get => transform.localScale;
			set => transform.localScale = value;
		}
	}
}
