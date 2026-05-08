using Core;
using UnityEngine;

namespace Behaviours.Movement
{
	public partial class Position : MovementBehaviour
	{
		private Vector3 position;

		public override void Execute()
		{
			transform.position = position;
		}
	}
}