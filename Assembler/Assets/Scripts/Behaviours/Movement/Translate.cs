using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using AssemblerAlpha.Core;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Movement
{
	public partial class Translate : PositionBehaviour<TranslateInfo>
	{
		private Vector3 displacement;
		
		protected override void OnInitialise(TranslateInfo behaviourInfo)
		{
			displacement = behaviourInfo.Displacement.ToUnity();
		}
		
		public override void Execute()
		{
			transform.position += displacement * Time.deltaTime;
		}
	}
}