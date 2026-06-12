namespace Assembler.Behaviours
{
	/// <summary>
	/// Implemented by a behaviour that drives one or more component properties live via
	/// <see cref="LivePropertyBindingExtensions.BindLive{T,TOwner}"/>. The build pipeline injects the shared
	/// <see cref="LivePropertyUpdater"/> after construction (mirrors <c>INeedsGameClock</c>), so the behaviour
	/// never reaches for it directly. Only behaviours that actually bind live opt in — most don't.
	/// </summary>
	public interface INeedsLiveProperties
	{
		LivePropertyUpdater LiveProperties { get; set; }
	}
}
