using Assembler.Parsing.Info.Behaviours;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class BoxColliderData : ColliderData
	{
		public IValueProvider<Vector3> Size { get; init; } = NullValueProvider<Vector3>.Instance;
		public IValueProvider<ColliderFit> Fit { get; init; } = NullValueProvider<ColliderFit>.Instance;

		public BoxColliderData(string id) : base(id) { }
	}
}
