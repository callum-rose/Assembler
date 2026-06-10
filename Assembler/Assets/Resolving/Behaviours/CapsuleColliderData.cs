namespace Assembler.Resolving.Behaviours
{
	public sealed class CapsuleColliderData : BehaviourData
	{
		public IValueProvider<float> Radius { get; init; } = NullValueProvider<float>.Instance;
		public IValueProvider<float> Height { get; init; } = NullValueProvider<float>.Instance;
		public IValueProvider<int> Direction { get; init; } = NullValueProvider<int>.Instance;
		public IValueProvider<bool> IsTrigger { get; init; } = NullValueProvider<bool>.Instance;
		public PhysicsMaterialProviders Material { get; init; } = PhysicsMaterialProviders.None;

		public CapsuleColliderData(string id) : base(id) { }
	}
}
