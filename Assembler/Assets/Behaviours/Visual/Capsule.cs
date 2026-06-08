using UnityEngine;

namespace Assembler.Behaviours.Visual
{
	/// <summary>Adds a 3D capsule primitive mesh as a child of the entity.</summary>
	/// <remarks>
	/// Properties:
	///   Colour: Optional tint applied to the primitive's material.
	///   Size: Optional local scale of the primitive (defaults to a 1-wide, 2-tall capsule).
	/// </remarks>
	public class Capsule : PrimitiveBehaviour
	{
		protected override PrimitiveType Primitive => PrimitiveType.Capsule;
	}
}
