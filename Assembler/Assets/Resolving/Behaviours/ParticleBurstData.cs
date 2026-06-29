using Assembler.Parsing.Info.Behaviours;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class ParticleBurstData : BehaviourData
	{
		public IValueProvider<int> Count { get; }
		public IValueProvider<Vector3> Direction { get; }
		public IValueProvider<float> Spread { get; }
		public IValueProvider<float> Speed { get; }
		public IValueProvider<float> SpeedVariation { get; }
		public IValueProvider<Vector3> InheritVelocity { get; }
		public IValueProvider<float> InheritFactor { get; }
		public IValueProvider<float> Lifetime { get; }
		public IValueProvider<Color> StartColour { get; }
		public IValueProvider<Color> EndColour { get; }
		public IValueProvider<float> StartSize { get; }
		public IValueProvider<float> EndSize { get; }
		public IValueProvider<float> Gravity { get; }
		public IValueProvider<float> Drag { get; }
		public IValueProvider<ParticleShape> Shape { get; }
		public IValueProvider<bool> Collision { get; }

		public ParticleBurstData(string id,
			IValueProvider<int> count,
			IValueProvider<Vector3> direction,
			IValueProvider<float> spread,
			IValueProvider<float> speed,
			IValueProvider<float> speedVariation,
			IValueProvider<Vector3> inheritVelocity,
			IValueProvider<float> inheritFactor,
			IValueProvider<float> lifetime,
			IValueProvider<Color> startColour,
			IValueProvider<Color> endColour,
			IValueProvider<float> startSize,
			IValueProvider<float> endSize,
			IValueProvider<float> gravity,
			IValueProvider<float> drag,
			IValueProvider<ParticleShape> shape,
			IValueProvider<bool> collision) : base(id) =>
			(Count, Direction, Spread, Speed, SpeedVariation, InheritVelocity, InheritFactor, Lifetime,
				StartColour, EndColour, StartSize, EndSize, Gravity, Drag, Shape, Collision) =
			(count, direction, spread, speed, speedVariation, inheritVelocity, inheritFactor, lifetime,
				startColour, endColour, startSize, endSize, gravity, drag, shape, collision);
	}
}
