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
		private PhysicsMaterial? _physicsMaterial;

		protected sealed override void OnInitialise(TData data)
		{
			var collider = CreateCollider(data);
			data.IsTrigger.UseIfValueExists(v => collider.isTrigger = v);
			_physicsMaterial = ApplyMaterial(data, collider);
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

		/// <summary>Adds the concrete collider component and applies its shape-specific properties.</summary>
		protected abstract Collider CreateCollider(TData data);

		// Builds a PhysicsMaterial from the set properties and assigns it, returning it so OnDestroy can free
		// it. Returns null (touching nothing) when no property is set, so the collider keeps the default material.
		private static PhysicsMaterial? ApplyMaterial(ColliderData data, Collider collider)
		{
			if (data.Bounciness is NullValueProvider<float>
				&& data.DynamicFriction is NullValueProvider<float>
				&& data.StaticFriction is NullValueProvider<float>)
			{
				return null;
			}

			var material = new PhysicsMaterial { hideFlags = HideFlags.DontSave };
			data.Bounciness.UseIfValueExists(v => material.bounciness = v);
			data.DynamicFriction.UseIfValueExists(v => material.dynamicFriction = v);
			data.StaticFriction.UseIfValueExists(v => material.staticFriction = v);
			collider.sharedMaterial = material;
			return material;
		}
	}
}
