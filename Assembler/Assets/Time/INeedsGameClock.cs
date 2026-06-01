namespace Assembler.Time
{
	/// <summary>
	/// Marker for behaviours that read game time. The build pipeline injects the shared
	/// <see cref="IGameClock"/> after construction (mirrors <c>INeedsSpawner</c>), so behaviours
	/// never reach for <see cref="UnityEngine.Time"/> directly.
	/// </summary>
	public interface INeedsGameClock
	{
		IGameClock Clock { get; set; }
	}
}
