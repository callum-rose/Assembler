using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class PrimitiveData : BehaviourData
	{
		public IValueProvider<Color> Colour { get; }
		public IValueProvider<Vector3> Size { get; }

		public PrimitiveData(string id,
			IValueProvider<Color> colour,
			IValueProvider<Vector3> size) : base(id) =>
			(Colour, Size) = (colour, size);
	}
}
