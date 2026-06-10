using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class GridMoverData : BehaviourData
	{
		/// <summary>The requested heading, re-read each frame; snapped to a cardinal by the mover.</summary>
		public IValueProvider<Vector3> Direction { get; }

		/// <summary>Movement speed in units per second.</summary>
		public IValueProvider<float> Speed { get; }

		/// <summary>Clearance used for walkability checks; negative inherits the game-wide Navigation default.</summary>
		public IValueProvider<float> AgentRadius { get; }

		public GridMoverData(string id, IValueProvider<Vector3> direction, IValueProvider<float> speed,
			IValueProvider<float> agentRadius) : base(id)
		{
			Direction = direction;
			Speed = speed;
			AgentRadius = agentRadius;
		}
	}
}
