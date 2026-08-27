using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Visual
{
	/// <summary>
	/// Adds a single 3D primitive mesh (chosen by <c>Shape</c>) as a child of the entity. Use this when an
	/// entity needs exactly one shape; for more than one, use <c>model</c>, which places each part with its
	/// own offset, rotation, anchor and colour. Never stack repeated <c>primitive</c> behaviours on one
	/// entity — every one of them renders at the entity origin, axis-aligned, on top of the others.
	/// <c>Size</c> is a true world bounding box in metres, exactly as it is in <c>model</c>: a cylinder of
	/// Size 1,3,1 is genuinely 3 units tall and a plane of Size 4,1,4 genuinely 4 by 4. The mesh is visual
	/// only — for collision, add a <c>box collider</c> or <c>sphere collider</c> with <c>Fit: bounds</c>,
	/// listed after this behaviour, and it is sized to the primitive for you (<c>part colliders</c> does the
	/// same, shape-matched, and is the better fit for a capsule, cylinder, wedge or cone).
	/// </summary>
	/// <remarks>
	/// Visual only: collision in Assembler is opt-in via the explicit collider behaviours, so the mesh child
	/// carries a MeshFilter and a MeshRenderer and nothing else. Otherwise every visual mesh would silently
	/// participate in physics (e.g. a floating rigidbody grinding on a "ground" mesh, or doubled-up colliders
	/// on an entity that also declares its own).
	/// Properties:
	///   Shape: Which primitive to create — one of "cube", "sphere", "capsule", "cylinder", "plane", "quad", "wedge", "cone", "hemisphere" (defaults to "cube").
	///   Colour: Optional tint applied to the primitive's material.
	///   Size: Optional true world size of the primitive child, in metres (defaults to 1,1,1).
	/// </remarks>
	public class Primitive : GameBehaviour<PrimitiveData>, INeedsLiveProperties
	{
		// URP's Lit shader exposes the main colour as _BaseColor; _Color covers the built-in pipeline.
		private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
		private static readonly int ColorId = Shader.PropertyToID("_Color");

		public LivePropertyUpdater LiveProperties { get; set; } = null!;

		protected override void OnInitialise(PrimitiveData data)
		{
			var shape = data.Shape.ValueOr(ShapeKind.Cube);
			var primitive = PrimitiveMeshes.Create(shape, shape.ToString(), transform);
			var renderer = primitive.GetComponent<MeshRenderer>();

			// Live-bind the scale so a !var/!expr/!clock animates the primitive's size; an omitted Size falls
			// back to Vector3.one, matching the transform's default (so the no-Size case is unchanged).
			// Normalise is what makes Size a world measurement rather than a raw localScale — the same
			// conversion `model` applies to a part, so the two behaviours mean the same thing by Size.
			data.Size.BindLive(this,
				size => primitive.transform.localScale = ModelGeometry.Normalise(shape, size),
				Vector3.one);

			// Live-bind the colour so a !var/!expr re-tints the primitive at runtime (matching Size and the
			// light behaviour). The no-fallback overload leaves an omitted colour untouched, so the primitive
			// keeps the shared material's own colour — preserving the previous UseIfValueExists behaviour.
			var block = new MaterialPropertyBlock();
			data.Colour.BindLive(this, colour =>
			{
				block.SetColor(BaseColorId, colour);
				block.SetColor(ColorId, colour);
				renderer.SetPropertyBlock(block);
			});
		}
	}
}
