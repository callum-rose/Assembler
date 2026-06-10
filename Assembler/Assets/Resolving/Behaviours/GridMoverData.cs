using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class GridMoverData : BehaviourData
	{
		/// <summary>The requested heading, re-read each frame; snapped to a cardinal by the mover.</summary>
		public IValueProvider<Vector3> Direction { get; }

		/// <summary>Movement speed in units per second.</summary>
		public IValueProvider<float> Speed { get; }

		public GridMoverData(string id, IValueProvider<Vector3> direction, IValueProvider<float> speed) : base(id)
		{
			Direction = direction;
			Speed = speed;
		}
	}
}
