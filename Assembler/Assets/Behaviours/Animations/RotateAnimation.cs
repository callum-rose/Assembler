using UnityEngine;

namespace Assembler.Behaviours.Animations
{
	/// <summary>Tweens the entity's euler-angle rotation from Start to End over Duration, then notifies listeners on completion.</summary>
	public sealed class RotateAnimation : TransformAnimation
	{
		protected override Vector3 Current
		{
			get => transform.eulerAngles;
			set => transform.eulerAngles = value;
		}
	}
}
