using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Movement
{
	/// <summary>Sets the entity's world position to <c>Position</c> when Executed (typically via a trigger).</summary>
	/// <remarks>
	/// Properties:
	///   Position: World-space position to teleport the entity to on each execution.
	/// </remarks>
	public class SetPosition : GameBehaviour<SetPositionData>
	{
		private IValueProvider<Vector3> _position;
		
		protected override void OnInitialise(SetPositionData behaviourInfo)
		{
			_position = behaviourInfo.ValueExpression;
		}

		public override void Execute()
		{
			transform.position = _position.Value;
		}
	}
}