using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Rotation
{
	/// <summary>Sets the entity's world rotation to <c>Rotation</c> (Euler degrees) when Executed (typically via a trigger).</summary>
	/// <remarks>
	/// Properties:
	///   Rotation: World-space Euler angles (degrees) to set the entity's rotation to on each execution.
	/// </remarks>
	public class SetRotation : GameBehaviour<SetRotationData>
	{
		private IValueProvider<Vector3> _rotation;

		protected override void OnInitialise(SetRotationData behaviourInfo)
		{
			_rotation = behaviourInfo.ValueExpression;
		}

		public override void Execute()
		{
			transform.eulerAngles = _rotation.Value;
		}
	}
}
