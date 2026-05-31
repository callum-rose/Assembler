using UnityEngine;

namespace Assembler.Behaviours.Animations
{
	/// <summary>Tweens the entity's world position from Start to End over Duration, then notifies listeners on completion.</summary>
	public sealed class MoveAnimation : TransformAnimation
	{
		protected override Vector3 Current
		{
			get => transform.position;
			set => transform.position = value;
		}
	}
}
