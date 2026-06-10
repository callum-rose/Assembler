using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug
{
	/// <summary>Debug-draws a line gizmo between two points in the entity's local transform space. Editor-only: gizmos render in the Scene view (or the Game view with Gizmos enabled), never in a built player or the default Game view — use `primitive` for geometry that renders in-game.</summary>
	/// <remarks>
	/// Properties:
	///   Start: Line start point in local transform space.
	///   End: Line end point in local transform space.
	///   Colour: Gizmo colour.
	/// </remarks>
	public class LineGizmoBehaviour : GameBehaviour<LineGizmoData>
	{
		public override void Execute(TriggerContext ctx)
		{

		}

		private void OnDrawGizmos()
		{
			if (Data == null)
			{
				return;
			}

			Gizmos.color = Data.Colour.Get();
			Gizmos.DrawLine(
				transform.TransformPoint(Data.Start.Get()),
				transform.TransformPoint(Data.End.Get()));
		}
	}
}
