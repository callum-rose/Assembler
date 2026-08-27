namespace Assembler.Resolving.Behaviours
{
	/// <summary>Runtime data for <c>part colliders</c>. It adds nothing of its own — every property it takes
	/// (the trigger flag and the physics-material trio) is shared with the other collider behaviours and
	/// lives on <see cref="ColliderData"/>. The shape of each collider comes from the visual, not the
	/// descriptor, so there is nothing left to author.</summary>
	public sealed class PartColliderData : ColliderData
	{
		public PartColliderData(string id) : base(id) { }
	}
}
