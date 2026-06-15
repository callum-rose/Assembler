using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>
	/// Base for the collider behaviours. Owns everything shared across box/sphere/capsule/mesh: adding the
	/// concrete <see cref="Collider"/> (deferred to <see cref="CreateCollider"/>), applying <c>isTrigger</c>,
	/// and building/owning the optional <see cref="PhysicsMaterial"/>.
	/// </summary>
	/// <remarks>
	/// The material is a runtime <see cref="Object"/> not owned by the scene graph, so destroying the host
	/// GameObject does not free it; it is destroyed here in <c>OnDestroy</c> to avoid leaking one material per
	/// material-bearing collider until the next scene load.
	/// </remarks>
	public abstract class AddColliderBehaviour<TData> : GameBehaviour<TData> where TData : ColliderData
	{
		private Collider _collider;
		private PhysicsMaterial? _physicsMaterial;

		protected sealed override void OnInitialise(TData data)
		{
			// Create the collider once and reuse it across pooled lives: OnInitialise re-runs on every reuse, so a
			// guard keeps the entity's single collider rather than stacking a second one — re-adding/removing
			// colliders also forces a PhysX recompute, exactly the native churn pooling exists to avoid. A guard
			// rather than Awake because Awake does not run in edit mode (the sandbox validator / EditMode tests
			// build via OnInitialise). The collider TYPE is fixed per behaviour; its shape and material come from
			// Data and are (re)applied each life below.
			if (_collider == null)
			{
				_collider = CreateCollider();
			}

			ApplyShape(_collider, data);
			data.IsTrigger.UseIfValueExists(v => _collider.isTrigger = v);
			ApplyMaterial(data);
		}

		private void OnDestroy()
		{
			if (_physicsMaterial == null)
			{
				return;
			}

			// DestroyImmediate outside play mode (e.g. the edit-mode sandbox build), where Destroy throws.
			if (Application.isPlaying)
			{
				Destroy(_physicsMaterial);
			}
			else
			{
				DestroyImmediate(_physicsMaterial);
			}
		}

		/// <summary>Adds the concrete collider component (type only — shape properties are applied in
		/// <see cref="ApplyShape"/>, which has the resolved <typeparamref name="TData"/>).</summary>
		protected abstract Collider CreateCollider();

		/// <summary>Applies this collider's shape-specific properties (size/radius/height/convex) from the
		/// resolved data. Re-run on every reuse to pick up this spawn's parameters.</summary>
		protected abstract void ApplyShape(Collider collider, TData data);

		// Builds (once) and reuses a PhysicsMaterial from the set properties and assigns it. Touches nothing when
		// no property is set, so the collider keeps the default material. The material is reused across pooled
		// lives — a per-template id, so the "material needed?" decision is stable — and freed in OnDestroy.
		private void ApplyMaterial(ColliderData data)
		{
			if (data.Bounciness is NullValueProvider<float>
				&& data.DynamicFriction is NullValueProvider<float>
				&& data.StaticFriction is NullValueProvider<float>)
			{
				return;
			}

			_physicsMaterial ??= new PhysicsMaterial { hideFlags = HideFlags.DontSave };
			data.Bounciness.UseIfValueExists(v => _physicsMaterial.bounciness = v);
			data.DynamicFriction.UseIfValueExists(v => _physicsMaterial.dynamicFriction = v);
			data.StaticFriction.UseIfValueExists(v => _physicsMaterial.staticFriction = v);
			_collider.sharedMaterial = _physicsMaterial;
		}
	}
}
