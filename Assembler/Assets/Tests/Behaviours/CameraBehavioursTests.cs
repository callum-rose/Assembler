using System;
using System.Collections.Generic;
using System.Reflection;
using Assembler.Behaviours;
using Assembler.Behaviours.Camera;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

namespace Tests.Behaviours
{
	// Covers the Cinemachine-wrapping camera behaviours. Each asserts the right Cinemachine component is added and
	// configured; the modifier behaviours (noise/zoom/confiner) also assert they reject a missing virtual camera.
	// The vcam-driven wiring that runs in Update (orbit target, confiner bounds, group members) is exercised by
	// invoking the private Update via reflection, since EditMode tests don't pump the Unity loop.
	public class CameraBehavioursTests
	{
		private readonly List<GameObject> _spawned = new();

		[TearDown]
		public void TearDown()
		{
			foreach (var go in _spawned)
			{
				if (go != null)
				{
					UnityEngine.Object.DestroyImmediate(go);
				}
			}

			_spawned.Clear();
		}

		private GameObject NewGo(string name)
		{
			var go = new GameObject(name);
			_spawned.Add(go);
			return go;
		}

		private static void InvokeUpdate(MonoBehaviour behaviour) =>
			behaviour.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!
				.Invoke(behaviour, null);

		[Test]
		public void Shake_AddsImpulseSourceAndFiresWithoutThrowing()
		{
			var go = NewGo("shake");
			var behaviour = go.AddComponent<CameraShake>();
			behaviour.Initialise(
				new CameraShakeData("s",
					new ValueProvider<float>(3f),
					new ValueProvider<float>(0.5f),
					new ValueProvider<Vector3>(Vector3.up)),
				Array.Empty<Listener>());

			var source = go.GetComponent<CinemachineImpulseSource>();
			Assert.IsNotNull(source);
			Assert.AreEqual(0.5f, source.ImpulseDefinition.ImpulseDuration, 1e-4f);
			Assert.AreEqual(Vector3.up, source.DefaultVelocity);
			Assert.DoesNotThrow(() => behaviour.Execute(TriggerContext.Empty));
		}

		[Test]
		public void Noise_RequiresVirtualCamera()
		{
			var go = NewGo("noise");
			var behaviour = go.AddComponent<CameraNoise>();
			Assert.Throws<MissingComponentException>(() => behaviour.Initialise(
				new CameraNoiseData("n",
					new ValueProvider<string>("Handheld_normal_mild"),
					NullValueProvider<float>.Instance,
					NullValueProvider<float>.Instance),
				Array.Empty<Listener>()));
		}

		[Test]
		public void Noise_AddsPerlinWithLoadedProfileAndGains()
		{
			var go = NewGo("noise");
			go.AddComponent<CinemachineCamera>();
			var behaviour = go.AddComponent<CameraNoise>();
			behaviour.Initialise(
				new CameraNoiseData("n",
					new ValueProvider<string>("Handheld_normal_mild"),
					new ValueProvider<float>(2f),
					new ValueProvider<float>(3f)),
				Array.Empty<Listener>());

			var perlin = go.GetComponent<CinemachineBasicMultiChannelPerlin>();
			Assert.IsNotNull(perlin);
			Assert.IsNotNull(perlin.NoiseProfile, "noise profile should load from Resources/CinemachineNoise.");
			Assert.AreEqual(2f, perlin.AmplitudeGain, 1e-4f);
			Assert.AreEqual(3f, perlin.FrequencyGain, 1e-4f);
		}

		[Test]
		public void Zoom_RequiresVirtualCamera()
		{
			var go = NewGo("zoom");
			var behaviour = go.AddComponent<CameraZoom>();
			Assert.Throws<MissingComponentException>(() => behaviour.Initialise(
				new CameraZoomData("z",
					NullValueProvider<float>.Instance,
					NullValueProvider<float>.Instance,
					NullValueProvider<float>.Instance,
					NullValueProvider<float>.Instance),
				Array.Empty<Listener>()));
		}

		[Test]
		public void Zoom_AddsFollowZoomWithWidthAndFovRange()
		{
			var go = NewGo("zoom");
			go.AddComponent<CinemachineCamera>();
			var behaviour = go.AddComponent<CameraZoom>();
			behaviour.Initialise(
				new CameraZoomData("z",
					new ValueProvider<float>(4f),
					new ValueProvider<float>(1f),
					new ValueProvider<float>(10f),
					new ValueProvider<float>(50f)),
				Array.Empty<Listener>());

			var zoom = go.GetComponent<CinemachineFollowZoom>();
			Assert.IsNotNull(zoom);
			Assert.AreEqual(4f, zoom.Width, 1e-4f);
			Assert.AreEqual(10f, zoom.FovRange.x, 1e-4f);
			Assert.AreEqual(50f, zoom.FovRange.y, 1e-4f);
		}

		[Test]
		public void Orbit_AddsOrbitalFollowAndTracksTargetOnUpdate()
		{
			var go = NewGo("orbit");
			var target = NewGo("target").transform;
			var behaviour = go.AddComponent<CameraOrbit>();
			behaviour.Initialise(
				new CameraOrbitData("o",
					new ValueProvider<Transform>(target),
					new ValueProvider<float>(7f),
					new ValueProvider<float>(2f),
					new ValueProvider<float>(45f),
					NullValueProvider<float>.Instance,
					NullValueProvider<int>.Instance,
					NullValueProvider<float>.Instance),
				Array.Empty<Listener>());

			var cam = go.GetComponent<CinemachineCamera>();
			var orbit = go.GetComponent<CinemachineOrbitalFollow>();
			Assert.IsNotNull(cam);
			Assert.IsNotNull(orbit);
			Assert.AreEqual(7f, orbit.Radius, 1e-4f);
			Assert.AreEqual(new Vector3(0f, 2f, 0f), orbit.TargetOffset);

			InvokeUpdate(behaviour);
			Assert.AreEqual(target, cam.Follow);
		}

		[Test]
		public void Confiner_RequiresVirtualCamera()
		{
			var go = NewGo("confiner");
			var behaviour = go.AddComponent<CameraConfiner>();
			Assert.Throws<MissingComponentException>(() => behaviour.Initialise(
				new CameraConfinerData("c",
					NullValueProvider<Transform>.Instance,
					new ValueProvider<CameraConfinerMode>(CameraConfinerMode.TwoD),
					NullValueProvider<float>.Instance,
					NullValueProvider<float>.Instance),
				Array.Empty<Listener>()));
		}

		[Test]
		public void Confiner_2dResolvesCollider2DOnUpdate()
		{
			var go = NewGo("confiner");
			go.AddComponent<CinemachineCamera>();
			var bounds = NewGo("bounds");
			var collider = bounds.AddComponent<BoxCollider2D>();
			var behaviour = go.AddComponent<CameraConfiner>();
			behaviour.Initialise(
				new CameraConfinerData("c",
					new ValueProvider<Transform>(bounds.transform),
					new ValueProvider<CameraConfinerMode>(CameraConfinerMode.TwoD),
					new ValueProvider<float>(1f),
					new ValueProvider<float>(0.5f)),
				Array.Empty<Listener>());

			var confiner = go.GetComponent<CinemachineConfiner2D>();
			Assert.IsNotNull(confiner);
			Assert.IsNull(go.GetComponent<CinemachineConfiner3D>());
			Assert.AreEqual(1f, confiner.Damping, 1e-4f);
			Assert.AreEqual(0.5f, confiner.SlowingDistance, 1e-4f);

			InvokeUpdate(behaviour);
			Assert.AreEqual(collider, confiner.BoundingShape2D);
		}

		[Test]
		public void Confiner_3dResolvesColliderOnUpdate()
		{
			var go = NewGo("confiner");
			go.AddComponent<CinemachineCamera>();
			var bounds = NewGo("bounds");
			var collider = bounds.AddComponent<BoxCollider>();
			var behaviour = go.AddComponent<CameraConfiner>();
			behaviour.Initialise(
				new CameraConfinerData("c",
					new ValueProvider<Transform>(bounds.transform),
					new ValueProvider<CameraConfinerMode>(CameraConfinerMode.ThreeD),
					NullValueProvider<float>.Instance,
					new ValueProvider<float>(0.25f)),
				Array.Empty<Listener>());

			var confiner = go.GetComponent<CinemachineConfiner3D>();
			Assert.IsNotNull(confiner);
			Assert.IsNull(go.GetComponent<CinemachineConfiner2D>());
			Assert.AreEqual(0.25f, confiner.SlowingDistance, 1e-4f);

			InvokeUpdate(behaviour);
			Assert.AreEqual(collider, confiner.BoundingVolume);
		}

		[Test]
		public void Group_BuildsTargetGroupAndFramingThenRebuildsMembersOnUpdate()
		{
			var go = NewGo("group");
			var member = NewGo("member").transform;
			var behaviour = go.AddComponent<CameraGroup>();

			IReadOnlyList<Transform> Resolve(string tag) =>
				tag == "crowd" ? new[] { member } : Array.Empty<Transform>();

			behaviour.Initialise(
				new CameraGroupData("g",
					new ValueProvider<string>("crowd"),
					NullValueProvider<int>.Instance,
					new ValueProvider<float>(1f),
					new ValueProvider<float>(0.7f),
					NullValueProvider<float>.Instance,
					Resolve),
				Array.Empty<Listener>());

			var cam = go.GetComponent<CinemachineCamera>();
			var group = go.GetComponent<CinemachineTargetGroup>();
			Assert.IsNotNull(group);
			Assert.IsNotNull(go.GetComponent<CinemachineGroupFraming>());
			Assert.AreEqual(group.transform, cam.Follow);

			InvokeUpdate(behaviour);
			Assert.AreEqual(1, group.Targets.Count);
			Assert.AreEqual(member, group.Targets[0].Object);
		}
	}
}
