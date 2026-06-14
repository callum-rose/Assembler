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
	public class SetPosition : GameBehaviour<SetPositionData>, IAmExecutable
	{
		// Assigned in OnInitialise before any Execute; never observed null.
		private IValueProvider<Vector3> _position = null!;

		protected override void OnInitialise(SetPositionData behaviourInfo)
		{
			_position = behaviourInfo.ValueExpression;
		}

		public void Execute(TriggerContext ctx)
		{
			transform.position = _position.Get(ctx);
		}
	}
}
