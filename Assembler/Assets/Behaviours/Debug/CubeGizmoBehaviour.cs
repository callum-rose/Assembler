using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug
{
	/// <summary>Debug-draws a cube gizmo at the entity's position. Editor-only: gizmos render in the Scene view (or the Game view with Gizmos enabled), never in a built player or the default Game view — use `primitive` for geometry that renders in-game.</summary>
	/// <remarks>
	/// Properties:
	///   Size: Cube dimensions in world units.
	///   IsWire: When true draws an outline; when false draws a filled cube.
	///   Colour: Gizmo colour.
	/// </remarks>
	public class CubeGizmoBehaviour : GameBehaviour<CubeGizmoData>
	{
		private void OnDrawGizmos()
		{
			if (Data == null)
			{
				return;
			}

			Gizmos.color = Data.Colour.Get();

			if (Data.IsWire.Get())
			{
				Gizmos.DrawWireCube(transform.position, Data.Size.Get());
			}
			else
			{
				Gizmos.DrawCube(transform.position, Data.Size.Get());
			}
		}
	}
}
