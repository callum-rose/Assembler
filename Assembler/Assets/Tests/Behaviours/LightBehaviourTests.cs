using System;
using Assembler.Behaviours;
using Assembler.Behaviours.Visual;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class LightBehaviourTests
	{
		// The MonoBehaviour is named LightBehaviour (not Light) specifically so AddComponent<Light> resolves to
		// UnityEngine.Light rather than recursing into itself. This guards that a real Unity Light is created and
		// the LightKind -> UnityEngine.LightType mapping is correct.
		[TestCase(LightKind.Directional, LightType.Directional)]
		[TestCase(LightKind.Point, LightType.Point)]
		[TestCase(LightKind.Spot, LightType.Spot)]
		public void Light_AddsUnityLightOfMappedType(LightKind kind, LightType expected)
		{
			var go = new GameObject("light host");
			try
			{
				var behaviour = go.AddComponent<LightBehaviour>();
				behaviour.Initialise(
					new LightData("l",
						new ValueProvider<LightKind>(kind),
						NullValueProvider<Color>.Instance,
						NullValueProvider<float>.Instance,
						NullValueProvider<float>.Instance,
						NullValueProvider<float>.Instance),
					Array.Empty<Listener>());

				var light = go.GetComponent<Light>();
				Assert.IsNotNull(light, "light behaviour should add a UnityEngine.Light component.");
				Assert.AreEqual(expected, light.type);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// Absent optional properties resolve to NullValueProvider, so the behaviour must fall back to Unity's
		// own defaults rather than zeroing the light (intensity 0 / black would still render the scene dark).
		[Test]
		public void Light_AppliesUnityDefaultsWhenPropertiesOmitted()
		{
			var go = new GameObject("light host");
			try
			{
				var behaviour = go.AddComponent<LightBehaviour>();
				behaviour.Initialise(
					new LightData("l",
						new ValueProvider<LightKind>(LightKind.Spot),
						NullValueProvider<Color>.Instance,
						NullValueProvider<float>.Instance,
						NullValueProvider<float>.Instance,
						NullValueProvider<float>.Instance),
					Array.Empty<Listener>());

				var light = go.GetComponent<Light>();
				Assert.AreEqual(Color.white, light.color);
				Assert.AreEqual(1f, light.intensity, 1e-4f);
				Assert.AreEqual(10f, light.range, 1e-4f);
				Assert.AreEqual(30f, light.spotAngle, 1e-4f);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}
	}
}
