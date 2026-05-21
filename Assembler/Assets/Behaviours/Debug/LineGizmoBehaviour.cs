using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Debug
{
	/// <summary>Debug-draws a line gizmo between two points in the entity's local transform space.</summary>
	/// <remarks>
	/// Properties:
	///   Start: Line start point in local transform space.
	///   End: Line end point in local transform space.
	///   Colour: Gizmo colour.
	/// </remarks>
	public class LineGizmoBehaviour : GameBehaviour<LineGizmoData>
	{
		public override void Execute()
		{

		}

		private void OnDrawGizmos()
		{
			if (Data == null) return;

			Gizmos.color = Data.Colour.Value;
			Gizmos.DrawLine(
				transform.TransformPoint(Data.Start.Value),
				transform.TransformPoint(Data.End.Value));
		}
	}
}
