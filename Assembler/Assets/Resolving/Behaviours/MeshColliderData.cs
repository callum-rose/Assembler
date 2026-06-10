namespace Assembler.Resolving.Behaviours
{
	public sealed class MeshColliderData : ColliderData
	{
		public IValueProvider<bool> Convex { get; init; } = NullValueProvider<bool>.Instance;

		public MeshColliderData(string id) : base(id) { }
	}
}
