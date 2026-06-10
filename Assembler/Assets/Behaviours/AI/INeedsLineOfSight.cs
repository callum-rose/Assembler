namespace Assembler.Behaviours.AI
{
	/// <summary>
	/// Marker for a behaviour that needs the shared <see cref="LineOfSightService"/>. The build pipeline sets
	/// <see cref="Sight"/> after construction (mirrors <c>INeedsGameClock</c> / <c>INeedsSpawner</c>).
	/// </summary>
	public interface INeedsLineOfSight
	{
		LineOfSightService Sight { get; set; }
	}
}
