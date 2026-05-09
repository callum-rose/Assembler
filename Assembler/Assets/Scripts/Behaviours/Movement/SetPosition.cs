using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using AssemblerAlpha.Core;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Movement
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