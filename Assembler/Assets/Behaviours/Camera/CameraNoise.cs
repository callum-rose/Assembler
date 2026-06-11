using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Unity.Cinemachine;
using UnityEngine;

namespace Assembler.Behaviours.Camera
{
	/// <summary>Adds constant handheld/ambient camera shake to a virtual camera via a Cinemachine
	/// <c>BasicMultiChannelPerlin</c> noise component. Pick a bundled noise profile by name and scale its
	/// amplitude/frequency.</summary>
	/// <remarks>
	/// This is a <b>modifier</b> behaviour: it needs a virtual camera (<c>camera follow</c>/<c>camera orbit</c>/
	/// <c>camera group</c>) on the same entity and must be listed <b>after</b> it, or initialisation throws.
	/// Noise runs on real frame time (presentation-only) and never feeds back into game logic. Profiles are loaded
	/// from <c>Resources/CinemachineNoise/</c>; an unknown name falls back to <c>Handheld_normal_mild</c>.
	/// Properties:
	///   Profile: Bundled noise profile name (default "Handheld_normal_mild"). e.g. "Handheld_normal_strong", "6D Shake".
	///   Amplitude: Multiplier on the profile's positional/rotational shake (default 1 = the profile's own amount).
	///   Frequency: Multiplier on how fast the shake oscillates (default 1 = the profile's own rate).
	/// </remarks>
	public sealed class CameraNoise : GameBehaviour<CameraNoiseData>
	{
		private const string DefaultProfile = "Handheld_normal_mild";

		protected override void OnInitialise(CameraNoiseData data)
		{
			CameraModifier.RequireVirtualCamera(gameObject, "camera noise");

			var perlin = gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();
			perlin.NoiseProfile = LoadProfile(data.Profile.ValueOr(DefaultProfile));
			data.Amplitude.UseIfValueExists(a => perlin.AmplitudeGain = a);
			data.Frequency.UseIfValueExists(f => perlin.FrequencyGain = f);
		}

		// Cinemachine's bundled NoiseSettings live inside the package and aren't runtime-loadable by name, so a
		// curated set is copied into Resources/CinemachineNoise/. Fall back to the default profile on a typo.
		private static NoiseSettings LoadProfile(string name)
		{
			var profile = Resources.Load<NoiseSettings>($"CinemachineNoise/{name}");
			if (profile == null)
			{
				profile = Resources.Load<NoiseSettings>($"CinemachineNoise/{DefaultProfile}");
				UnityEngine.Debug.LogWarning(
					$"camera noise: unknown profile '{name}', falling back to '{DefaultProfile}'.");
			}

			return profile;
		}
	}
}
