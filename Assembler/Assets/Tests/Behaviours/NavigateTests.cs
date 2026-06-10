using System;
using Assembler.Behaviours;
using Assembler.Behaviours.AI;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class NavigateTests
	{
		private sealed class FakeClock : IGameClock
		{
			public float DeltaTime { get; set; } = 0.1f;
			public float UnscaledDeltaTime { get; set; }
			public double Time { get; set; }
			public int FrameCount { get; set; }
			public float TimeScale { get; set; } = 1f;
			public bool IsPaused { get; set; }
			public void Pause() { }
			public void Resume() { }
			public void Step(int frames = 1) { }
		}

		private GameObject _go = null!;

		[TearDown]
		public void TearDown()
		{
			if (_go != null)
			{
				UnityEngine.Object.DestroyImmediate(_go);
			}
		}

		[Test]
		public void ReachesTargetInOpenSpace()
		{
			_go = new GameObject("agent");
			var navigate = _go.AddComponent<Navigate>();
			navigate.Clock = new FakeClock();

			navigate.Initialise(new NavigateData("n",
				new ValueProvider<Vector3>(new Vector3(5, 0, 0)),
				new ValueProvider<float>(4f),
				new ValueProvider<float>(0.5f),
				new ValueProvider<float>(0f),
				new ValueProvider<string>("astar"),
				output: NullValueProvider<Vector3>.Instance), Array.Empty<Listener>());

			// Drive the steering loop directly (Execute integrates onto the transform when no output is set).
			for (var i = 0; i < 400; i++)
			{
				navigate.Execute(TriggerContext.Empty);
			}

			Assert.Less(Vector3.Distance(_go.transform.position, new Vector3(5, 0, 0)), 0.2f);
		}
	}
}
