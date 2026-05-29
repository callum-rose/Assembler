using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug
{
	/// <summary>Debug-draws a sphere gizmo at the entity's position in the Scene view.</summary>
	/// <remarks>
	/// Properties:
	///   Radius: Sphere radius in world units.
	///   IsWire: When true draws an outline; when false draws a filled sphere.
	///   Colour: Gizmo colour.
	/// </remarks>
	public class SphereGizmoBehaviour : GameBehaviour<SphereGizmoData>
	{
		public override void Execute(TriggerContext ctx)
		{

		}

		private void OnDrawGizmos()
		{
			if (Data == null) return;

			Gizmos.color = Data.Colour.Get();

			if (Data.IsWire.Get())
				Gizmos.DrawWireSphere(transform.position, Data.Radius.Get());
			else
				Gizmos.DrawSphere(transform.position, Data.Radius.Get());
		}
	}
}
