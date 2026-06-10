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
	public sealed class PhysicsMaterialProviders
	{
		public readonly static PhysicsMaterialProviders None = new();

		public IValueProvider<float> Bounciness { get; init; } = NullValueProvider<float>.Instance;
		public IValueProvider<float> DynamicFriction { get; init; } = NullValueProvider<float>.Instance;
		public IValueProvider<float> StaticFriction { get; init; } = NullValueProvider<float>.Instance;

		public void ApplyTo(Collider collider)
		{
			if (Bounciness is NullValueProvider<float>
				&& DynamicFriction is NullValueProvider<float>
				&& StaticFriction is NullValueProvider<float>)
			{
				return;
			}

			var material = new PhysicsMaterial();
			Bounciness.UseIfValueExists(v => material.bounciness = v);
			DynamicFriction.UseIfValueExists(v => material.dynamicFriction = v);
			StaticFriction.UseIfValueExists(v => material.staticFriction = v);
			collider.sharedMaterial = material;
		}
	}
}
