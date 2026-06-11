using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	/// <summary>
	/// Runtime configuration for the multi-target perceive sensor. Inputs are read-only providers. The list outputs
	/// (<see cref="Positions"/>, <see cref="Ids"/>, <see cref="Velocities"/>) are read providers whose live list the
	/// sensor clears and repopulates in place each scan; <see cref="Count"/> is a write-back scalar. An omitted
	/// optional input or output resolves to a <see cref="NullValueProvider{T}"/> null-object rather than C# null, so
	/// the sensor skips any output it isn't wired to.
	/// </summary>
	public sealed class PerceiveAllData : BehaviourData
	{
		public IValueProvider<string> Tag { get; }
		public IValueProvider<float> Radius { get; }
		public IValueProvider<float> ConeAngle { get; }
		public IValueProvider<Vector3> Forward { get; }
		public IValueProvider<bool> RequireLineOfSight { get; }
		public IValueProvider<string> Obstacles { get; }
		public IValueProvider<float> Interval { get; }
		public IValueProvider<List<Vector3>> Positions { get; }
		public IValueProvider<List<string>> Ids { get; }
		public IValueProvider<List<Vector3>> Velocities { get; }
		public IWriteValueProvider<int> Count { get; }

		public PerceiveAllData(
			string id,
			IValueProvider<string> tag,
			IValueProvider<float> radius,
			IValueProvider<float> coneAngle,
			IValueProvider<Vector3> forward,
			IValueProvider<bool> requireLineOfSight,
			IValueProvider<string> obstacles,
			IValueProvider<float> interval,
			IValueProvider<List<Vector3>> positions,
			IValueProvider<List<string>> ids,
			IValueProvider<List<Vector3>> velocities,
			IWriteValueProvider<int> count) : base(id)
		{
			Tag = tag;
			Radius = radius;
			ConeAngle = coneAngle;
			Forward = forward;
			RequireLineOfSight = requireLineOfSight;
			Obstacles = obstacles;
			Interval = interval;
			Positions = positions;
			Ids = ids;
			Velocities = velocities;
			Count = count;
		}
	}
}
