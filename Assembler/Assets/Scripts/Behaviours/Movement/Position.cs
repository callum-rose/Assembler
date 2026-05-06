using Assembler.Generators.Attributes;
using Core;
using UnityEngine;

namespace Behaviours.Movement
{
	public partial class Position : MovementBehaviour
	{
		[Inject("Position")]  private Vector3 position;

		public override void Execute()
		{
			transform.position = position;
		}
	}
}