namespace Assembler.Resolving.Behaviours
{
	public sealed class SphereColliderData : ColliderData
	{
		public IValueProvider<float> Radius { get; init; } = NullValueProvider<float>.Instance;

		public SphereColliderData(string id) : base(id) { }
	}
}
