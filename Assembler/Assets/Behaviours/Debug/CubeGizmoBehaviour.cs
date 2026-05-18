using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug
{
	public class CubeGizmoBehaviour : GameBehaviour<CubeGizmoData>
	{
		public override void Execute()
		{

		}

		private void OnDrawGizmos()
		{
			if (Data == null) return;

			Gizmos.color = Data.Colour.Value;

			if (Data.IsWire.Value)
				Gizmos.DrawWireCube(transform.position, Data.Size.Value);
			else
				Gizmos.DrawCube(transform.position, Data.Size.Value);
		}
	}
}
