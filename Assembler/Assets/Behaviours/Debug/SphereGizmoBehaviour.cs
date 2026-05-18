using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug
{
	public class SphereGizmoBehaviour : GameBehaviour<SphereGizmoData>
	{
		public override void Execute()
		{

		}

		private void OnDrawGizmos()
		{
			if (Data == null) return;

			Gizmos.color = Data.Colour.Value;

			if (Data.IsWire.Value)
				Gizmos.DrawWireSphere(transform.position, Data.Radius.Value);
			else
				Gizmos.DrawSphere(transform.position, Data.Radius.Value);
		}
	}
}
