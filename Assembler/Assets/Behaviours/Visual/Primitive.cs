using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Visual
{
	/// <summary>
	/// Adds a single 3D primitive mesh (chosen by <c>Shape</c>) as a child of the entity. Use this when an
	/// entity needs exactly one shape; for more than one, use <c>model</c>, which places each part with its
	/// own offset, rotation, anchor and colour. Never stack repeated <c>primitive</c> behaviours on one
	/// entity — every one of them renders at the entity origin, axis-aligned, on top of the others. Note
	/// that <c>Size</c> here is a raw localScale, not a true world size: Unity's native cylinder and capsule
	/// are 2 units tall and its plane is 10 by 10, so <c>model</c>'s normalised <c>Size</c> is the easier
	/// option when real dimensions matter. The mesh is visual only — for collision, add a <c>box collider</c>
	/// or <c>sphere collider</c> with <c>Fit: bounds</c>, listed after this behaviour, and it is sized to the
	/// primitive for you (<c>part colliders</c> does the same, shape-matched, and is the better fit for a
	/// capsule or cylinder).
	/// </summary>
	/// <remarks>
	/// Visual only: <see cref="GameObject.CreatePrimitive"/> bundles a default collider onto every primitive,
	/// but collision in Assembler is opt-in via the explicit collider behaviours. The auto-added collider is
	/// stripped here so a primitive is purely cosmetic — otherwise every visual mesh would silently
	/// participate in physics (e.g. a floating rigidbody grinding on a "ground" mesh, or doubled-up colliders
	/// on an entity that also declares its own).
	/// Properties:
	///   Shape: Which primitive to create — one of "cube", "sphere", "capsule", "cylinder", "plane", "quad" (defaults to "cube").
	///   Colour: Optional tint applied to the primitive's material.
	///   Size: Optional local scale of the primitive child.
	/// </remarks>
	public class Primitive : GameBehaviour<PrimitiveData>, INeedsLiveProperties
	{
		// URP's Lit shader exposes the main colour as _BaseColor; _Color covers the built-in pipeline.
		private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
		private static readonly int ColorId = Shader.PropertyToID("_Color");

		public LivePropertyUpdater LiveProperties { get; set; } = null!;

		protected override void OnInitialise(PrimitiveData data)
		{
			var shape = data.Shape.ValueOr(PrimitiveType.Cube);
			var primitive = GameObject.CreatePrimitive(shape);
			primitive.name = shape.ToString();
			primitive.transform.SetParent(transform, false);

			var renderer = primitive.GetComponent<MeshRenderer>();
			renderer.sharedMaterial = Resources.Load<Material>("Materials/Primitive");

			// Record the shape so `part colliders` can match a collider to it; nothing else reads it.
			primitive.AddComponent<PrimitiveShape>().Shape = shape;

			// Drop the collider CreatePrimitive adds: primitives are visual, collision is declared explicitly.
			// DestroyImmediate when not playing so the edit-mode sandbox build (which instantiates without
			// entering play mode) can strip it too — plain Destroy throws in edit mode.
			if (primitive.TryGetComponent<Collider>(out var collider))
			{
#if UNITY_EDITOR
				if (Application.isPlaying)
				{
#endif
					Destroy(collider);
#if UNITY_EDITOR
				}
				else
				{
					DestroyImmediate(collider);
				}
#endif
			}

			// Live-bind the scale so a !var/!expr/!clock animates the primitive's size; an omitted Size falls
			// back to Vector3.one, matching the transform's default (so the no-Size case is unchanged).
			data.Size.BindLive(this, size => primitive.transform.localScale = size, Vector3.one);

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
