using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class PrimitiveData : BehaviourData
	{
		public IValueProvider<PrimitiveType> Shape { get; }
		public IValueProvider<Color> Colour { get; }
		public IValueProvider<Vector3> Size { get; }

		public PrimitiveData(string id,
			IValueProvider<PrimitiveType> shape,
			IValueProvider<Color> colour,
			IValueProvider<Vector3> size) : base(id) =>
			(Shape, Colour, Size) = (shape, colour, size);
	}
}
