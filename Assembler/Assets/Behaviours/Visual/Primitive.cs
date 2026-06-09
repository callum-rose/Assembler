using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Visual
{
	/// <summary>Adds a 3D primitive mesh (chosen by <c>Shape</c>) as a child of the entity.</summary>
	/// <remarks>
	/// Properties:
	///   Shape: Which primitive to create — one of "cube", "sphere", "capsule", "cylinder", "plane", "quad" (defaults to "cube").
	///   Colour: Optional tint applied to the primitive's material.
	///   Size: Optional local scale of the primitive child.
	/// </remarks>
	public class Primitive : GameBehaviour<PrimitiveData>
	{
		// URP's Lit shader exposes the main colour as _BaseColor; _Color covers the built-in pipeline.
		private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
		private static readonly int ColorId = Shader.PropertyToID("_Color");

		protected override void OnInitialise(PrimitiveData data)
		{
			var shape = data.Shape.ValueOr(PrimitiveType.Cube);
			var primitive = GameObject.CreatePrimitive(shape);
			primitive.name = shape.ToString();
			primitive.transform.SetParent(transform, false);

			data.Size.UseIfValueExists(size => primitive.transform.localScale = size);

			data.Colour.UseIfValueExists(colour =>
			{
				var block = new MaterialPropertyBlock();
				block.SetColor(BaseColorId, colour);
				block.SetColor(ColorId, colour);
				primitive.GetComponent<MeshRenderer>().SetPropertyBlock(block);
			});
		}

		public override void Execute(TriggerContext ctx) { }
	}
}
