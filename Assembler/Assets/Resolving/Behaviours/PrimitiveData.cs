using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class PrimitiveData : BehaviourData
	{
		public IValueProvider<string> Shape { get; }
		public IValueProvider<Color> Colour { get; }
		public IValueProvider<Vector3> Size { get; }

		public PrimitiveData(string id,
			IValueProvider<string> shape,
			IValueProvider<Color> colour,
			IValueProvider<Vector3> size) : base(id) =>
			(Shape, Colour, Size) = (shape, colour, size);
	}
}
