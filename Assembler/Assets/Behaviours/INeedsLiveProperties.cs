namespace Assembler.Behaviours
{
	/// <summary>
	/// Marker for behaviours that drive component properties live. The build pipeline injects the shared
	/// <see cref="LivePropertyUpdater"/> after construction (mirrors <c>INeedsGameClock</c>), so a behaviour
	/// never reaches for it directly. <see cref="GameBehaviour"/> implements this, so every behaviour can
	/// <c>BindLive</c> without opting in.
	/// </summary>
	public interface INeedsLiveProperties
	{
		LivePropertyUpdater LiveProperties { set; }
	}
}
