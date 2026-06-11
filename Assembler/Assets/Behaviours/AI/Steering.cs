using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.AI
{
	/// <summary>Blends a weighted list of steering forces into one velocity each frame.</summary>
	/// <remarks>
	/// Composes seek + separate + avoid (etc.) without hand-summing them in one expression: each force is a
	/// Vector3 (typically a SteeringMath call) with a weight. The weighted sum is clamped to <c>MaxSpeed</c> and
	/// either written to the <c>Output</c> velocity variable (for an integrator like <c>velocity</c> to apply)
	/// or, when no output is named, integrated onto the entity transform directly.
	/// Properties:
	///   Forces: List of { Force, Weight } entries; Force is a Vector3 (e.g. !expr Seek(...)), Weight a float.
	///   MaxSpeed: Upper bound on the blended velocity's magnitude.
	///   Output: Name of the vector variable to write the blended velocity into (omit to move the entity directly).
	/// </remarks>
	public sealed class Steering : GameBehaviour<SteeringData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private void Update()
		{
			var blended = Vector3.zero;

			foreach (var force in Data.Forces)
			{
				blended += force.Force.Get() * force.Weight.Get();
			}

			var maxSpeed = Data.MaxSpeed.Get();

			if (blended.sqrMagnitude > maxSpeed * maxSpeed)
			{
				blended = blended.normalized * maxSpeed;
			}

			if (Data.Output is NullValueProvider<Vector3>)
			{
				transform.position += blended * Clock.DeltaTime;
			}
			else
			{
				Data.Output.Set(blended);
			}
		}
	}
}
