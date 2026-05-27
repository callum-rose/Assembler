using UnityEngine;

namespace Assembler.Behaviours.VariableUpdaters
{
	public class Vector3Adjust : VariableAdjustBehaviour<Vector3>
	{
		protected override Vector3 Add(Vector3 current, Vector3 delta) => current + delta;
	}
}
