using System;
using Assembler.Behaviours;
using Assembler.Behaviours.Movement;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class MovementBoundsTests
	{
		// ---- SpeedLimit ----

		[Test]
		public void SpeedLimit_ClampsMagnitudeAndKeepsDirection()
		{
			var go = new GameObject("speed limit");
			try
			{
				var shared = new ValueProvider<Vector3>(new Vector3(100f, 0f, 0f));
				var limit = go.AddComponent<SpeedLimit>();
				limit.Initialise(new SpeedLimitData("s", shared, new ValueProvider<float>(20f)),
					Array.Empty<Listener>());

				limit.Execute(TriggerContext.Empty);

				var v = shared.Get(TriggerContext.Empty);
				Assert.AreEqual(20f, v.magnitude, 1e-4f);
				Assert.AreEqual(new Vector3(20f, 0f, 0f), v);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void SpeedLimit_LeavesUnderLimitVelocityUntouched()
		{
			var go = new GameObject("speed limit");
			try
			{
				var shared = new ValueProvider<Vector3>(new Vector3(3f, 4f, 0f)); // magnitude 5
				var limit = go.AddComponent<SpeedLimit>();
				limit.Initialise(new SpeedLimitData("s", shared, new ValueProvider<float>(20f)),
					Array.Empty<Listener>());

				limit.Execute(TriggerContext.Empty);

				Assert.AreEqual(new Vector3(3f, 4f, 0f), shared.Get(TriggerContext.Empty));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void SpeedLimit_RequiresWritableVelocity()
		{
			var go = new GameObject("speed limit");
			try
			{
				var limit = go.AddComponent<SpeedLimit>();

				Assert.Throws<InvalidOperationException>(() =>
					limit.Initialise(new SpeedLimitData("s", NullValueProvider<Vector3>.Instance,
						new ValueProvider<float>(20f)), Array.Empty<Listener>()));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// ---- ClampPosition ----

		[Test]
		public void ClampPosition_ClampsEachAxisIntoBounds()
		{
			var go = new GameObject("clamp position");
			try
			{
				go.transform.position = new Vector3(15f, -8f, 3f);

				var clamp = go.AddComponent<ClampPosition>();
				clamp.Initialise(new ClampPositionData("c",
					new ValueProvider<Vector3>(new Vector3(-5f, -5f, -5f)),
					new ValueProvider<Vector3>(new Vector3(5f, 5f, 5f))), Array.Empty<Listener>());

				clamp.Execute(TriggerContext.Empty);

				Assert.AreEqual(new Vector3(5f, -5f, 3f), go.transform.position);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// ---- WrapPosition ----

		[Test]
		public void WrapPosition_WrapsPastEachBoundToOppositeEdge()
		{
			var go = new GameObject("wrap position");
			try
			{
				// x past Max -> Min; y below Min -> Max; z in range -> unchanged.
				go.transform.position = new Vector3(6f, -7f, 2f);

				var wrap = go.AddComponent<WrapPosition>();
				wrap.Initialise(new WrapPositionData("w",
					new ValueProvider<Vector3>(new Vector3(-5f, -5f, -5f)),
					new ValueProvider<Vector3>(new Vector3(5f, 5f, 5f))), Array.Empty<Listener>());

				wrap.Execute(TriggerContext.Empty);

				Assert.AreEqual(new Vector3(-5f, 5f, 2f), go.transform.position);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}
	}
}
