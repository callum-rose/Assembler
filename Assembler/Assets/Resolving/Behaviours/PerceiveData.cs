using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	/// <summary>
	/// Runtime configuration for the perceive sensor. Inputs are read-only providers; the outputs are
	/// <see cref="IWriteValueProvider{T}"/> the sensor writes back into. An omitted optional input or output
	/// resolves to a <see cref="NullValueProvider{T}"/> null-object rather than C# null. Omitting
	/// <see cref="ConeAngle"/> / <see cref="Forward"/> selects an omnidirectional scan; omitting an output leaves
	/// its blackboard variable unwritten (the null-object's <c>Set</c> is a no-op).
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
		public IWriteValueProvider<string> TargetId { get; }
		public IWriteValueProvider<Vector3> TargetPosition { get; }
		public IWriteValueProvider<bool> HasTarget { get; }
		public IWriteValueProvider<Vector3> LastKnownPosition { get; }

		public PerceiveData(
			string id,
			IValueProvider<string> tag,
			IValueProvider<float> radius,
			IValueProvider<float> coneAngle,
			IValueProvider<Vector3> forward,
			IValueProvider<bool> requireLineOfSight,
			IValueProvider<string> obstacles,
			IValueProvider<float> interval,
			IWriteValueProvider<string> targetId,
			IWriteValueProvider<Vector3> targetPosition,
			IWriteValueProvider<bool> hasTarget,
			IWriteValueProvider<Vector3> lastKnownPosition) : base(id)
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
