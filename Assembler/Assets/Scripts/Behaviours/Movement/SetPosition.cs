using Assembler.Parsing.Phase3;
using Assembler.Parsing2.Info;
using Core;
using UnityEngine;

namespace Behaviours.Movement
{
	public class SetPosition : PositionBehaviour<SetPositionInfo>
	{
		private Vector3 _position;
		
		protected override void OnInitialise(SetPositionInfo behaviourInfo)
		{
			_position = behaviourInfo.ValueExpression.ToUnity();
		}

		public override void Execute()
		{
			transform.position = _position;
		}
	}
}