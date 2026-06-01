using UnityEngine;

namespace Assembler.Input
{

	/// <summary>
	/// Picks the <see cref="InputPlatform"/> for the running build from the Unity runtime. The editor can
	/// override this (see <c>GameLauncherWindow</c>) to simulate a platform without deploying to a device.
	/// </summary>
	public static class PlatformSelector
	{
		public static InputPlatform Resolve()
		{
			if (Application.isMobilePlatform)
			{
				return InputPlatform.Mobile;
			}

			return Application.platform switch
			{
				RuntimePlatform.PS4 => InputPlatform.Console,
				RuntimePlatform.PS5 => InputPlatform.Console,
				RuntimePlatform.XboxOne => InputPlatform.Console,
				RuntimePlatform.GameCoreXboxOne => InputPlatform.Console,
				RuntimePlatform.GameCoreXboxSeries => InputPlatform.Console,
				RuntimePlatform.Switch => InputPlatform.Console,
				_ => InputPlatform.Desktop
			};
		}

		/// <summary>The descriptor binding-group key for a platform (matches the <c>Bindings:</c> keys).</summary>
		public static string GroupKey(this InputPlatform platform) => platform switch
		{
			InputPlatform.Desktop => "desktop",
			InputPlatform.Gamepad => "gamepad",
			InputPlatform.Mobile => "mobile",
			InputPlatform.Console => "console",
			_ => "desktop"
		};
	}
}
