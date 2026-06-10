using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	/// <summary>
	/// Optional physics-material settings shared by every collider behaviour (box, sphere, capsule, mesh).
	/// Each property is an optional <see cref="IValueProvider{T}"/>: an unset property is a
	/// <see cref="NullValueProvider{T}"/> and is left at Unity's PhysicsMaterial default. When at least one
	/// property is set, <see cref="ApplyTo"/> builds a fresh <see cref="PhysicsMaterial"/> and assigns it to
	/// the collider's <see cref="Collider.sharedMaterial"/>; when none are set the collider keeps the global
	/// default material (no allocation).
	/// </summary>
	/// <remarks>
	/// The material created by <see cref="ApplyTo"/> is a runtime <see cref="UnityEngine.Object"/> that is not
	/// owned by the scene graph, so destroying the collider's GameObject does not free it. The owning
	/// behaviour must therefore keep the returned reference and pass it to <see cref="Cleanup"/> in its
	/// <c>OnDestroy</c>, otherwise one material leaks per material-bearing collider until the next scene load.
	/// </remarks>
	public sealed class PhysicsMaterialProviders
	{
		public readonly static PhysicsMaterialProviders None = new();

		public IValueProvider<float> Bounciness { get; init; } = NullValueProvider<float>.Instance;
		public IValueProvider<float> DynamicFriction { get; init; } = NullValueProvider<float>.Instance;
		public IValueProvider<float> StaticFriction { get; init; } = NullValueProvider<float>.Instance;

		/// <summary>
		/// Builds a <see cref="PhysicsMaterial"/> from the set properties and assigns it to
		/// <paramref name="collider"/>, returning it so the caller can destroy it in <c>OnDestroy</c>. Returns
		/// <c>null</c> (and touches nothing) when no property is set, so the collider keeps the default material.
		/// </summary>
		public PhysicsMaterial? ApplyTo(Collider collider)
		{
			if (Bounciness is NullValueProvider<float>
				&& DynamicFriction is NullValueProvider<float>
				&& StaticFriction is NullValueProvider<float>)
			{
				return null;
			}

			var material = new PhysicsMaterial { hideFlags = HideFlags.DontSave };
			Bounciness.UseIfValueExists(v => material.bounciness = v);
			DynamicFriction.UseIfValueExists(v => material.dynamicFriction = v);
			StaticFriction.UseIfValueExists(v => material.staticFriction = v);
			collider.sharedMaterial = material;
			return material;
		}

		/// <summary>
		/// Destroys a material previously returned by <see cref="ApplyTo"/>. Safe to call with <c>null</c>.
		/// Uses <see cref="Object.DestroyImmediate"/> outside play mode (e.g. the edit-mode sandbox build),
		/// where <see cref="Object.Destroy"/> throws.
		/// </summary>
		public static void Cleanup(PhysicsMaterial? material)
		{
			if (material == null)
			{
				return;
			}

			if (Application.isPlaying)
			{
				Object.Destroy(material);
			}
			else
			{
				Object.DestroyImmediate(material);
			}
		}
	}
}
