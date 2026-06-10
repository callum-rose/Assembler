namespace Assembler.Resolving.Behaviours
{
	/// <summary>
	/// Shared runtime data for every collider behaviour (box, sphere, capsule, mesh): the trigger flag and the
	/// optional physics-material properties. Each is an optional <see cref="IValueProvider{T}"/> — an unset
	/// property is a <see cref="NullValueProvider{T}"/> and leaves the collider at Unity's default. The base
	/// <c>AddColliderBehaviour</c> reads these to apply <c>isTrigger</c> and build the collider's
	/// <c>PhysicsMaterial</c>; subclasses add only their shape-specific providers (size, radius, …).
	/// </summary>
	public abstract class ColliderData : BehaviourData
	{
		public IValueProvider<bool> IsTrigger { get; init; } = NullValueProvider<bool>.Instance;
		public IValueProvider<float> Bounciness { get; init; } = NullValueProvider<float>.Instance;
		public IValueProvider<float> DynamicFriction { get; init; } = NullValueProvider<float>.Instance;
		public IValueProvider<float> StaticFriction { get; init; } = NullValueProvider<float>.Instance;

		protected ColliderData(string id) : base(id) { }
	}
}
