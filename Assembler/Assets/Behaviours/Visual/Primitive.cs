using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Visual
{
	/// <summary>Adds a 3D primitive mesh (chosen by <c>Shape</c>) as a child of the entity.</summary>
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

		// The primitive child is created lazily and only once: unlike the other heavy behaviours its mesh shape
		// comes from Data (unavailable in Awake), so creation can't move to Awake — but a per-template shape is
		// constant, so guarding on a null field gives the same "build once, reuse across pooled lives" effect.
		// OnInitialise re-binds the live scale and re-applies the colour to the persisted child each spawn.
		private GameObject? _primitive;
		private MeshRenderer _renderer = null!;

		protected override void OnInitialise(PrimitiveData data)
		{
			if (_primitive == null)
			{
				var shape = data.Shape.ValueOr(PrimitiveType.Cube);
				_primitive = GameObject.CreatePrimitive(shape);
				_primitive.name = shape.ToString();
				_primitive.transform.SetParent(transform, false);

				_renderer = _primitive.GetComponent<MeshRenderer>();
				_renderer.sharedMaterial = Resources.Load<Material>("Materials/Primitive");

				// Drop the collider CreatePrimitive adds: primitives are visual, collision is declared explicitly.
				// DestroyImmediate when not playing so the edit-mode sandbox build (which instantiates without
				// entering play mode) can strip it too — plain Destroy throws in edit mode.
				if (_primitive.TryGetComponent<Collider>(out var collider))
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
			}

			// Live-bind the scale so a !var/!expr/!clock animates the primitive's size; an omitted Size falls
			// back to Vector3.one, matching the transform's default (so the no-Size case is unchanged).
			data.Size.BindLive(this, size => _primitive.transform.localScale = size, Vector3.one);

			data.Colour.UseIfValueExists(colour =>
			{
				var block = new MaterialPropertyBlock();
				block.SetColor(BaseColorId, colour);
				block.SetColor(ColorId, colour);
				_renderer.SetPropertyBlock(block);
			});
		}
	}
}
