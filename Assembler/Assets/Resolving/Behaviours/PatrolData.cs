using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class PatrolData : BehaviourData
	{
		public IValueProvider<List<Vector3>> Waypoints { get; }
		public IValueProvider<bool> Loop { get; }
		public IValueProvider<bool> PingPong { get; }
		public IValueProvider<float> ArriveRadius { get; }
		public IValueProvider<float> Speed { get; }

		/// <summary>Velocity variable to write; a <see cref="NullValueProvider{T}"/> drives the transform directly.</summary>
		public IWriteValueProvider<Vector3> Output { get; }

		/// <summary>Current waypoint index to publish; a <see cref="NullValueProvider{T}"/> when unbound.</summary>
		public IWriteValueProvider<int> CurrentIndex { get; }

		public PatrolData(
			string id,
			IValueProvider<List<Vector3>> waypoints,
			IValueProvider<bool> loop,
			IValueProvider<bool> pingPong,
			IValueProvider<float> arriveRadius,
			IValueProvider<float> speed,
			IWriteValueProvider<Vector3> output,
			IWriteValueProvider<int> currentIndex) : base(id)
		{
			Waypoints = waypoints;
			Loop = loop;
			PingPong = pingPong;
			ArriveRadius = arriveRadius;
			Speed = speed;
			Output = output;
			CurrentIndex = currentIndex;
		}
	}
}
