using UnityEngine;

namespace Assembler.Behaviours.Visual
{
	/// <summary>Adds a 3D cube primitive mesh as a child of the entity.</summary>
	/// <remarks>
	/// Properties:
	///   Colour: Optional tint applied to the primitive's material.
	///   Size: Optional local scale of the primitive (defaults to a 1×1×1 unit cube).
	/// </remarks>
	public class Cube : PrimitiveBehaviour
	{
		protected override PrimitiveType Primitive => PrimitiveType.Cube;
	}
}
