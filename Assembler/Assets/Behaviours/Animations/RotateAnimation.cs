using UnityEngine;

namespace Assembler.Behaviours.Animations
{
	/// <summary>Tweens the entity's euler angles from Start to End over Duration. See <see cref="TransformAnimation"/>.</summary>
	public sealed class RotateAnimation : TransformAnimation
	{
		protected override Vector3 Current
		{
			get => transform.eulerAngles;
			set => transform.eulerAngles = value;
		}
	}
}
