using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	/// <summary>
	/// Runtime configuration for the perceive sensor. All inputs and outputs are live providers; an omitted
	/// optional one resolves to a <see cref="NullValueProvider{T}"/> null-object rather than C# null. Omitting
	/// <see cref="ConeAngle"/> / <see cref="Forward"/> selects an omnidirectional scan; omitting an output means
	/// that blackboard variable is simply not written (the behaviour checks via the null-object).
	/// </summary>
	public sealed class PerceiveData : BehaviourData
	{
		public IValueProvider<string> Tag { get; }
		public IValueProvider<float> Radius { get; }
		public IValueProvider<float> ConeAngle { get; }
		public IValueProvider<Vector3> Forward { get; }
		public IValueProvider<bool> RequireLineOfSight { get; }
		public IValueProvider<string> Obstacles { get; }
		public IValueProvider<float> Interval { get; }
		public IValueProvider<string> TargetId { get; }
		public IValueProvider<Vector3> TargetPosition { get; }
		public IValueProvider<bool> HasTarget { get; }
		public IValueProvider<Vector3> LastKnownPosition { get; }

		public PerceiveData(
			string id,
			IValueProvider<string> tag,
			IValueProvider<float> radius,
			IValueProvider<float> coneAngle,
			IValueProvider<Vector3> forward,
			IValueProvider<bool> requireLineOfSight,
			IValueProvider<string> obstacles,
			IValueProvider<float> interval,
			IValueProvider<string> targetId,
			IValueProvider<Vector3> targetPosition,
			IValueProvider<bool> hasTarget,
			IValueProvider<Vector3> lastKnownPosition) : base(id)
		{
			Tag = tag;
			Radius = radius;
			ConeAngle = coneAngle;
			Forward = forward;
			RequireLineOfSight = requireLineOfSight;
			Obstacles = obstacles;
			Interval = interval;
			TargetId = targetId;
			TargetPosition = targetPosition;
			HasTarget = hasTarget;
			LastKnownPosition = lastKnownPosition;
		}
	}
}
