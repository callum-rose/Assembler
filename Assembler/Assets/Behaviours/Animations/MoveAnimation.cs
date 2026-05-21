using UnityEngine;

namespace Assembler.Behaviours.Animations
{
	public sealed class MoveAnimation : TransformAnimation
	{
		protected override Vector3 Current
		{
			get => transform.position;
			set => transform.position = value;
		}
	}
}
