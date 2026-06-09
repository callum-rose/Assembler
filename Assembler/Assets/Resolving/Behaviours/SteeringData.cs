using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	/// <summary>One resolved steering force: a live Vector3 provider paired with its blend weight.</summary>
	public readonly struct SteeringForce
	{
		public IValueProvider<Vector3> Force { get; }
		public IValueProvider<float> Weight { get; }

		public SteeringForce(IValueProvider<Vector3> force, IValueProvider<float> weight)
		{
			Force = force;
			Weight = weight;
		}
	}

	public sealed class SteeringData : BehaviourData
	{
		public IReadOnlyList<SteeringForce> Forces { get; }
		public IValueProvider<float> MaxSpeed { get; }

		/// <summary>Velocity variable to write; a <see cref="NullValueProvider{T}"/> drives the transform directly.</summary>
		public IValueProvider<Vector3> Output { get; }

		public SteeringData(
			string id,
			IReadOnlyList<SteeringForce> forces,
			IValueProvider<float> maxSpeed,
			IValueProvider<Vector3> output) : base(id)
		{
			Forces = forces;
			MaxSpeed = maxSpeed;
			Output = output;
		}
	}
}
