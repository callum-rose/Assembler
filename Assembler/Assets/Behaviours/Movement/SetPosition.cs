using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Behaviours.Movement
{
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