using UnityEngine;

namespace Assembler.Behaviours.Visual
{
	/// <summary>Adds a 3D sphere primitive mesh as a child of the entity.</summary>
	/// <remarks>
	/// Properties:
	///   Colour: Optional tint applied to the primitive's material.
	///   Size: Optional local scale of the primitive (defaults to a 1-unit-diameter sphere).
	/// </remarks>
	public class Sphere : PrimitiveBehaviour
	{
		protected override PrimitiveType Primitive => PrimitiveType.Sphere;
	}
}
