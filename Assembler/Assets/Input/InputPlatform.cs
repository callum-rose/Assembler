namespace Assembler.Input
{
	/// <summary>The platform families a game can declare bindings for.</summary>
	public enum InputPlatform
	{
		Desktop,
		Gamepad,
		Mobile,
		Console,

		/// <summary>
		/// A non-device automation scheme: masks in the game's <c>auto</c> binding group so a harness can drive the
		/// declared actions from a synthetic device. Never returned by <see cref="PlatformSelector"/> — it is
		/// selected explicitly (e.g. the deterministic record/replay tests, issue #101) via the build's override
		/// platform, exactly as the editor overrides the platform to simulate a device.
		/// </summary>
		Auto
	}
}
