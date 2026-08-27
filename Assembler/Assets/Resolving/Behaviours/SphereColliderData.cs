using Assembler.Parsing.Info.Behaviours;

namespace Assembler.Resolving.Behaviours
{
	public sealed class SphereColliderData : ColliderData
	{
		public IValueProvider<float> Radius { get; init; } = NullValueProvider<float>.Instance;
		public IValueProvider<ColliderFit> Fit { get; init; } = NullValueProvider<ColliderFit>.Instance;

		public SphereColliderData(string id) : base(id) { }
	}
}
