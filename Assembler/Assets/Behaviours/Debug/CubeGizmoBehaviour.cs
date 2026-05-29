using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug
{
	/// <summary>Debug-draws a cube gizmo at the entity's position in the Scene view.</summary>
	/// <remarks>
	/// Properties:
	///   Size: Cube dimensions in world units.
	///   IsWire: When true draws an outline; when false draws a filled cube.
	///   Colour: Gizmo colour.
	/// </remarks>
	public class CubeGizmoBehaviour : GameBehaviour<CubeGizmoData>
	{
		public override void Execute(TriggerContext ctx)
		{

		}

		private void OnDrawGizmos()
		{
			if (Data == null) return;

			Gizmos.color = Data.Colour.Get();

			if (Data.IsWire.Get())
				Gizmos.DrawWireCube(transform.position, Data.Size.Get());
			else
				Gizmos.DrawCube(transform.position, Data.Size.Get());
		}
	}
}
