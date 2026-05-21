using UnityEngine;

namespace Assembler.Behaviours.Animations
{
	public sealed class RotateAnimation : TransformAnimation
	{
		protected override Vector3 Current
		{
			get => transform.eulerAngles;
			set => transform.eulerAngles = value;
		}
	}
}
