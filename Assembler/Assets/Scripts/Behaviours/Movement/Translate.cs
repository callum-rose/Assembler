using Assembler.Parsing.Phase3;
using UnityEngine;

namespace Assembler.Behaviours.Movement
{
	public class Translate : PositionBehaviour<TranslateData>
	{
		private IValueProvider<Vector3> displacement;

		public Translate(IValueProvider<Vector3> displacement)
		{
			this.displacement = displacement;
		}

		protected override void OnInitialise(TranslateData behaviourInfo)
		{
			displacement = behaviourInfo.Displacement;
		}

		public override void Execute()
		{
			transform.position += displacement.Value * Time.deltaTime;
		}
	}
}