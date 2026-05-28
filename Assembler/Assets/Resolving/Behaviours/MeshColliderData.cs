namespace Assembler.Resolving.Behaviours
{
	public sealed class MeshColliderData : BehaviourData
	{
		public IValueProvider<bool> Convex { get; init; } = NullValueProvider<bool>.Instance;
		public IValueProvider<bool> IsTrigger { get; init; } = NullValueProvider<bool>.Instance;

		public MeshColliderData(string id) : base(id) { }
	}
}
