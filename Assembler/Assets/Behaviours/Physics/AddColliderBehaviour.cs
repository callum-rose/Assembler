using System.Collections.Generic;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	/// <summary>
	/// Base for the collider behaviours. Owns everything shared across box/sphere/capsule/mesh: adding the
	/// concrete <see cref="Collider"/>s (deferred to <see cref="CreateColliders"/>), applying <c>isTrigger</c>,
	/// and building/owning the optional <see cref="PhysicsMaterial"/>.
	/// </summary>
	/// <remarks>
	/// One behaviour may produce several colliders — <c>box collider</c>/<c>sphere collider</c> with
	/// <c>Fit: parts</c> put one on each visual part, forming a compound collider under the entity's
	/// Rigidbody. <c>isTrigger</c> is applied to every one of them and they all share a single
	/// <see cref="PhysicsMaterial"/>, so the trigger flag and physics-material properties mean the same thing
	/// however many colliders a behaviour ends up adding.
	/// The material is a runtime <see cref="Object"/> not owned by the scene graph, so destroying the host
	/// GameObject does not free it; it is destroyed here in <c>OnDestroy</c> to avoid leaking one material per
	/// material-bearing collider until the next scene load. One material per behaviour keeps that single
	/// cleanup correct no matter how many colliders reference it.
	/// </remarks>
	public abstract class AddColliderBehaviour<TData> : GameBehaviour<TData> where TData : ColliderData
	{
		private PhysicsMaterial? _physicsMaterial;

		protected sealed override void OnInitialise(TData data)
		{
			var colliders = CreateColliders(data);

			data.IsTrigger.UseIfValueExists(v =>
			{
				foreach (var collider in colliders)
				{
					collider.isTrigger = v;
				}
			});

			_physicsMaterial = ApplyMaterial(data, colliders);
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

		/// <summary>Adds this behaviour's collider components and applies their shape-specific properties.
		/// Usually one; the fitting modes may return several.</summary>
		protected abstract IReadOnlyList<Collider> CreateColliders(TData data);

		// Builds a PhysicsMaterial from the set properties and assigns it to every collider, returning it so
		// OnDestroy can free it. Returns null (touching nothing) when no property is set, so the colliders keep
		// the default material.
		private static PhysicsMaterial? ApplyMaterial(ColliderData data, IReadOnlyList<Collider> colliders)
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

			foreach (var collider in colliders)
			{
				collider.sharedMaterial = material;
			}

			return material;
		}
	}
}
