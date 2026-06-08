using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Visual
{
	// Shared base for the primitive-mesh behaviours (Cube, Sphere, Capsule, Plane). Each subclass only
	// supplies its PrimitiveType; the build/colour/scale logic lives here. Doc comments belong on the
	// concrete subclasses (one summary per primitive) — this abstract base is never mapped by doc-gen.
	public abstract class PrimitiveBehaviour : GameBehaviour<PrimitiveData>
	{
		// URP's Lit shader exposes the main colour as _BaseColor; _Color covers the built-in pipeline.
		private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
		private static readonly int ColorId = Shader.PropertyToID("_Color");

		protected abstract PrimitiveType Primitive { get; }

		protected override void OnInitialise(PrimitiveData data)
		{
			var primitive = GameObject.CreatePrimitive(Primitive);
			primitive.name = Primitive.ToString();
			primitive.transform.SetParent(transform, false);

			data.Size.UseIfValueExists(size => primitive.transform.localScale = size);

			data.Colour.UseIfValueExists(colour =>
			{
				var meshRenderer = primitive.GetComponent<MeshRenderer>();
				var block = new MaterialPropertyBlock();
				block.SetColor(BaseColorId, colour);
				block.SetColor(ColorId, colour);
				meshRenderer.SetPropertyBlock(block);
			});
		}

		public override void Execute(TriggerContext ctx) { }
	}
}
