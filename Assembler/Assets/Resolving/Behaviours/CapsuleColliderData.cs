namespace Assembler.Resolving.Behaviours
{
	public sealed class CapsuleColliderData : ColliderData
	{
		public IValueProvider<float> Radius { get; init; } = NullValueProvider<float>.Instance;
		public IValueProvider<float> Height { get; init; } = NullValueProvider<float>.Instance;
		public IValueProvider<int> Direction { get; init; } = NullValueProvider<int>.Instance;

		public CapsuleColliderData(string id) : base(id) { }
	}
}
