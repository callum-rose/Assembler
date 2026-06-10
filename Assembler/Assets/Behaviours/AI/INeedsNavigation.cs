namespace Assembler.Behaviours.AI
{
	/// <summary>
	/// Marker for a behaviour that needs the shared <see cref="NavGridService"/>. The build pipeline sets
	/// <see cref="Nav"/> after construction (mirrors <c>INeedsGameClock</c> / <c>INeedsSpawner</c>).
	/// </summary>
	public interface INeedsNavigation
	{
		NavGridService Nav { get; set; }
	}
}
