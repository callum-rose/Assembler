using System;
using Assembler.Behaviours;
using Assembler.Behaviours.Movement;
using Assembler.Behaviours.Rotation;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	/// <summary>
	/// Motion behaviours (velocity, acceleration, drag, move-towards, smooth-move, angular velocity) driven
	/// by a hand-stepped <see cref="FakeGameClock"/> — asserts they integrate by <c>velocity * delta</c> and
	/// freeze when the clock is paused.
	/// </summary>
	public class MotionBehaviourClockTests : BehaviourTestFixture
	{
		// ---- Motion: Velocity ----

		[Test]
		public void Velocity_MovesByVelocityTimesDelta()
		{
			var go = Track(new GameObject("velocity"));
			var fake = new FakeGameClock { DeltaTime = 0.5f };
			var velocity = NewBehaviour<Velocity>(go, fake);
			velocity.Initialise(new VelocityData("v", new ValueProvider<Vector3>(new Vector3(2f, 0f, 0f))),
				Array.Empty<Listener>());

			velocity.Step();

			Assert.AreEqual(new Vector3(1f, 0f, 0f), go.transform.position);
		}

		[Test]
		public void Velocity_FrozenWhenPaused()
		{
			var go = Track(new GameObject("velocity"));
			var fake = new FakeGameClock();
			fake.Pause();
			var velocity = NewBehaviour<Velocity>(go, fake);
			velocity.Initialise(new VelocityData("v", new ValueProvider<Vector3>(new Vector3(5f, 5f, 5f))),
				Array.Empty<Listener>());

			velocity.Step();

			Assert.AreEqual(Vector3.zero, go.transform.position);
		}

		[Test]
		public void Velocity_HalfDeltaMovesHalfDistance()
		{
			var go = Track(new GameObject("velocity"));
			var fake = new FakeGameClock { DeltaTime = 0.25f };
			var velocity = NewBehaviour<Velocity>(go, fake);
			velocity.Initialise(new VelocityData("v", new ValueProvider<Vector3>(new Vector3(4f, 0f, 0f))),
				Array.Empty<Listener>());

			velocity.Step();

			Assert.AreEqual(new Vector3(1f, 0f, 0f), go.transform.position);
		}

		// ---- Motion: Acceleration ----

		[Test]
		public void Acceleration_IntegratesVelocityOverTwoExecutes()
		{
			var go = Track(new GameObject("acceleration"));
			var fake = new FakeGameClock { DeltaTime = 1f };
			var acceleration = NewBehaviour<Acceleration>(go, fake);
			acceleration.Initialise(new AccelerationData("a", new ValueProvider<Vector3>(new Vector3(0f, 1f, 0f)),
					NullValueProvider<Vector3>.Instance),
				Array.Empty<Listener>());

			// Frame 1: v = (0,1,0); pos += v*dt = (0,1,0)
			acceleration.Step();
			Assert.AreEqual(new Vector3(0f, 1f, 0f), go.transform.position);

			// Frame 2: v = (0,2,0); pos += v*dt = (0,3,0)
			acceleration.Step();
			Assert.AreEqual(new Vector3(0f, 3f, 0f), go.transform.position);
		}

		[Test]
		public void Acceleration_FrozenWhenPaused()
		{
			var go = Track(new GameObject("acceleration"));
			var fake = new FakeGameClock();
			fake.Pause();
			var acceleration = NewBehaviour<Acceleration>(go, fake);
			acceleration.Initialise(new AccelerationData("a", new ValueProvider<Vector3>(new Vector3(0f, 9f, 0f)),
					NullValueProvider<Vector3>.Instance),
				Array.Empty<Listener>());

			acceleration.Step();
			acceleration.Step();

			Assert.AreEqual(Vector3.zero, go.transform.position);
		}

		// ---- Shared velocity: Acceleration (shared mode) + Velocity integrator ----

		[Test]
		public void Acceleration_SharedMode_WritesVelocityAndLeavesPositionToIntegrator()
		{
			var go = Track(new GameObject("shared velocity"));
			var fake = new FakeGameClock { DeltaTime = 1f };

			// One shared, writable velocity variable that both behaviours touch.
			var shared = new ValueProvider<Vector3>(Vector3.zero);

			var acceleration = NewBehaviour<Acceleration>(go, fake);
			acceleration.Initialise(
				new AccelerationData("a", new ValueProvider<Vector3>(new Vector3(0f, 10f, 0f)), shared),
				Array.Empty<Listener>());

			acceleration.Step();

			// Shared mode: it integrates into the shared velocity but does NOT move the entity.
			Assert.AreEqual(new Vector3(0f, 10f, 0f), shared.Get(TriggerContext.Empty));
			Assert.AreEqual(Vector3.zero, go.transform.position);

			// The Velocity integrator, fed the SAME provider, moves the entity by vel*dt.
			var velocity = NewBehaviour<Velocity>(go, fake);
			velocity.Initialise(new VelocityData("v", shared), Array.Empty<Listener>());

			velocity.Step();

			Assert.AreEqual(new Vector3(0f, 10f, 0f), go.transform.position);
		}

		// ---- Drag (exponential decay on shared velocity) ----

		[Test]
		public void Drag_DecaysVelocityExponentially()
		{
			var go = Track(new GameObject("drag"));
			var fake = new FakeGameClock { DeltaTime = 0.5f };
			var shared = new ValueProvider<Vector3>(new Vector3(10f, 0f, 0f));

			var drag = NewBehaviour<DragBehaviour>(go, fake);
			drag.Initialise(new DragData("d", shared, new ValueProvider<float>(2f)), Array.Empty<Listener>());

			drag.Step();

			// magnitude == 10 * exp(-2 * 0.5) == 10 * exp(-1)
			Assert.AreEqual(10f * Mathf.Exp(-1f), shared.Get(TriggerContext.Empty).magnitude, 1e-4f);
		}

		[Test]
		public void Drag_RequiresWritableVelocity()
		{
			var go = Track(new GameObject("drag"));
			var fake = new FakeGameClock { DeltaTime = 0.5f };
			var drag = NewBehaviour<DragBehaviour>(go, fake);

			Assert.Throws<InvalidOperationException>(() =>
				drag.Initialise(new DragData("d", NullValueProvider<Vector3>.Instance, new ValueProvider<float>(2f)),
					Array.Empty<Listener>()));
		}

		// ---- MoveTowards ----

		[Test]
		public void MoveTowards_StepsTowardTargetAtSpeed()
		{
			var go = Track(new GameObject("move towards"));
			var fake = new FakeGameClock { DeltaTime = 0.5f };
			var move = NewBehaviour<MoveTowards>(go, fake);
			move.Initialise(new MoveTowardsData("m",
				new ValueProvider<Vector3>(new Vector3(10f, 0f, 0f)),
				new ValueProvider<float>(2f)), Array.Empty<Listener>());

			move.Step(); // 2 units/s * 0.5s = 1 unit toward (10,0,0)

			Assert.AreEqual(new Vector3(1f, 0f, 0f), go.transform.position);
		}

		[Test]
		public void MoveTowards_DoesNotOvershootTarget()
		{
			var go = Track(new GameObject("move towards"));
			var fake = new FakeGameClock { DeltaTime = 1f };
			go.transform.position = new Vector3(9.5f, 0f, 0f);

			var move = NewBehaviour<MoveTowards>(go, fake);
			move.Initialise(new MoveTowardsData("m",
				new ValueProvider<Vector3>(new Vector3(10f, 0f, 0f)),
				new ValueProvider<float>(100f)), Array.Empty<Listener>()); // step far exceeds remaining 0.5

			move.Step();

			Assert.AreEqual(new Vector3(10f, 0f, 0f), go.transform.position);
		}

		// ---- SmoothMove ----

		[Test]
		public void SmoothMove_EasesTowardTargetWithoutOvershoot()
		{
			var go = Track(new GameObject("smooth move"));
			var fake = new FakeGameClock { DeltaTime = 0.1f };
			var smooth = NewBehaviour<SmoothMove>(go, fake);
			smooth.Initialise(new SmoothMoveData("s",
				new ValueProvider<Vector3>(new Vector3(10f, 0f, 0f)),
				new ValueProvider<float>(1f)), Array.Empty<Listener>());

			smooth.Step();

			// Moved toward the target but nowhere near overshooting it.
			Assert.Greater(go.transform.position.x, 0f);
			Assert.Less(go.transform.position.x, 10f);
		}

		// ---- Motion: AngularVelocity ----

		[Test]
		public void AngularVelocity_RotatesByAngularVelocityTimesDelta()
		{
			var go = Track(new GameObject("angular"));
			var fake = new FakeGameClock { DeltaTime = 0.1f };
			var angular = NewBehaviour<AngularVelocity>(go, fake);
			angular.Initialise(
				new AngularVelocityData("av", new ValueProvider<Vector3>(new Vector3(0f, 0f, 10f))),
				Array.Empty<Listener>());

			angular.Step();

			// 10 deg/s * 0.1s = 1 deg about z (small angle, no wrap ambiguity).
			Assert.AreEqual(1f, go.transform.eulerAngles.z, 1e-3f);
		}

		[Test]
		public void AngularVelocity_FrozenWhenPaused()
		{
			var go = Track(new GameObject("angular"));
			var fake = new FakeGameClock();
			fake.Pause();
			var angular = NewBehaviour<AngularVelocity>(go, fake);
			angular.Initialise(
				new AngularVelocityData("av", new ValueProvider<Vector3>(new Vector3(0f, 0f, 90f))),
				Array.Empty<Listener>());

			angular.Step();

			Assert.AreEqual(Quaternion.identity, go.transform.rotation);
		}
	}
}
