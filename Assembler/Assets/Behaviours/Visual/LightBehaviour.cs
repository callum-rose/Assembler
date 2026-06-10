using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Visual
{
	/// <summary>Adds a realtime <c>UnityEngine.Light</c> to the entity so a 3D scene is lit
	/// (without one, <c>primitive</c> meshes render near-black under URP's Lit shader).</summary>
	/// <remarks>
	/// A directional light shines along the entity's forward axis, so orient it via the entity's rotation;
	/// at the default (identity) rotation it points straight down the +Z axis. Point and spot lights radiate
	/// from the entity's position. Range and SpotAngle apply to point/spot lights and are ignored by a
	/// directional light. Defaults mirror Unity's own (white, intensity 1, range 10, spot angle 30).
	/// Properties:
	///   Type: One of "directional", "point", "spot" (defaults to "directional").
	///   Colour: Optional light colour (defaults to white).
	///   Intensity: Optional brightness multiplier (defaults to 1).
	///   Range [float]: Optional reach in world units for point/spot lights (defaults to 10).
	///   SpotAngle [float]: Optional cone angle in degrees for spot lights (defaults to 30).
	/// </remarks>
	public class LightBehaviour : GameBehaviour<LightData>
	{
		protected override void OnInitialise(LightData data)
		{
			var light = gameObject.AddComponent<Light>();
			light.type = data.Type.ValueOr(LightKind.Directional) switch
			{
				LightKind.Point => LightType.Point,
				LightKind.Spot => LightType.Spot,
				_ => LightType.Directional
			};
			light.color = data.Colour.ValueOr(Color.white);
			light.intensity = data.Intensity.ValueOr(1f);
			light.range = data.Range.ValueOr(10f);
			light.spotAngle = data.SpotAngle.ValueOr(30f);
		}

		public override void Execute(TriggerContext ctx) { }
	}
}
