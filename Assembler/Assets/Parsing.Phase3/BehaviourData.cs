using UnityEngine;

namespace Assembler.Parsing.Phase3.Parsing.Phase3
{
	public abstract class BehaviourData
	{
	}
	
	public class VelocityData : BehaviourData
	{
		public ValueContainer<Vector3> Velocity { get; }
	}
}