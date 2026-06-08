using UnityEngine;

namespace Assembler.Behaviours.Visual
{
	/// <summary>Adds a flat 3D plane primitive mesh as a child of the entity.</summary>
	/// <remarks>
	/// Properties:
	///   Colour: Optional tint applied to the primitive's material.
	///   Size: Optional local scale of the primitive (the base plane spans 10×10 units at scale 1).
	/// </remarks>
	public class Plane : PrimitiveBehaviour
	{
		protected override PrimitiveType Primitive => PrimitiveType.Plane;
	}
}
