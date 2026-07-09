using System.Collections.Generic;
using Assembler.Behaviours;
using Assembler.Time;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Behaviours
{
	/// <summary>
	/// Base fixture for behaviour tests that spin up throwaway <see cref="Object"/>s (usually
	/// <see cref="GameObject"/>s). Register each with <see cref="Track{T}"/> and it is destroyed in
	/// <c>[TearDown]</c> — replacing the per-test <c>try/finally … DestroyImmediate</c> ceremony.
	/// </summary>
	public abstract class BehaviourTestFixture
	{
		private readonly List<Object> _tracked = new();

		[TearDown]
		public void DestroyTracked()
		{
			foreach (var obj in _tracked)
			{
				if (obj != null)
				{
					Object.DestroyImmediate(obj);
				}
			}

			_tracked.Clear();
		}

		/// <summary>Registers <paramref name="obj"/> for destruction in <c>[TearDown]</c> and returns it.</summary>
		protected T Track<T>(T obj) where T : Object
		{
			_tracked.Add(obj);
			return obj;
		}

		/// <summary>
		/// Adds a <typeparamref name="T"/> behaviour to <paramref name="go"/>, injecting
		/// <paramref name="clock"/> when the behaviour implements <see cref="INeedsGameClock"/>.
		/// </summary>
		protected static T NewBehaviour<T>(GameObject go, FakeGameClock clock) where T : GameBehaviour
		{
			var behaviour = go.AddComponent<T>();
			if (behaviour is INeedsGameClock needsClock)
			{
				needsClock.Clock = clock;
			}

			return behaviour;
		}
	}
}
