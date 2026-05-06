using Assembler.Generators.Attributes;
using Core;
using UnityEngine;

namespace Behaviours.Movement
{
	public partial class ConstantTranslate : MovementBehaviour
	{
		[Inject("Displacement")] private Vector3 displacement;
		
		public override void Execute()
		{
			transform.position += displacement * Time.deltaTime;
		}
	}
}