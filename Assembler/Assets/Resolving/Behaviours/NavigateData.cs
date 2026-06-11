using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class NavigateData : BehaviourData
	{
		public IValueProvider<Vector3> Target { get; }
		public IValueProvider<float> Speed { get; }
		public IValueProvider<float> SlowingRadius { get; }
		public IValueProvider<float> Recompute { get; }
		public IValueProvider<string> Mode { get; }

		/// <summary>Clearance for this agent's route; a null provider (unset) inherits the game-wide Navigation
		/// default via <c>ValueOr</c> at the point of use.</summary>
		public IValueProvider<float> AgentRadius { get; }

		/// <summary>Velocity variable to write; a <see cref="NullValueProvider{T}"/> drives the transform directly.</summary>
		public IWriteValueProvider<Vector3> Output { get; }

		public NavigateData(
			string id,
			IValueProvider<Vector3> target,
			IValueProvider<float> speed,
			IValueProvider<float> slowingRadius,
			IValueProvider<float> recompute,
			IValueProvider<string> mode,
			IValueProvider<float> agentRadius,
			IWriteValueProvider<Vector3> output) : base(id)
		{
			Target = target;
			Speed = speed;
			SlowingRadius = slowingRadius;
			Recompute = recompute;
			Mode = mode;
			AgentRadius = agentRadius;
			Output = output;
		}
	}
}
