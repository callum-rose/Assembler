using System;
using System.Collections.Generic;
using Assembler.Behaviours.VariableUpdaters;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Parsing
{
	public class VariableAdjustTests
	{
		private static readonly IReadOnlyList<Action> NoListeners = Array.Empty<Action>();

		private static T ExecuteAdjust<TBehaviour, T>(T initial, T delta)
			where TBehaviour : VariableAdjustBehaviour<T>
		{
			var go = new GameObject(typeof(TBehaviour).Name);
			try
			{
				var behaviour = go.AddComponent<TBehaviour>();
				var variable = new ValueProvider<T>(initial);
				var deltaProvider = new ValueProvider<T>(delta);
				behaviour.Initialise(new VariableAdjustData<T>("test", NoListeners, variable, deltaProvider));

				behaviour.Execute();

				return variable.Value;
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void IntAdjust_PositiveDelta_AddsToVariable()
		{
			Assert.AreEqual(15, ExecuteAdjust<IntAdjust, int>(10, 5));
		}

		[Test]
		public void IntAdjust_NegativeDelta_SubtractsFromVariable()
		{
			Assert.AreEqual(7, ExecuteAdjust<IntAdjust, int>(10, -3));
		}

		[Test]
		public void FloatAdjust_PositiveDelta_AddsToVariable()
		{
			Assert.AreEqual(3.5f, ExecuteAdjust<FloatAdjust, float>(1.0f, 2.5f), 1e-5f);
		}

		[Test]
		public void FloatAdjust_NegativeDelta_SubtractsFromVariable()
		{
			Assert.AreEqual(-1.5f, ExecuteAdjust<FloatAdjust, float>(1.0f, -2.5f), 1e-5f);
		}

		[Test]
		public void Vector3Adjust_PositiveDelta_AddsToVariable()
		{
			var result = ExecuteAdjust<Vector3Adjust, Vector3>(new Vector3(1, 2, 3), new Vector3(4, 5, 6));
			Assert.AreEqual(new Vector3(5, 7, 9), result);
		}

		[Test]
		public void Vector3Adjust_NegativeDelta_SubtractsFromVariable()
		{
			var result = ExecuteAdjust<Vector3Adjust, Vector3>(new Vector3(5, 5, 5), new Vector3(-1, -2, -3));
			Assert.AreEqual(new Vector3(4, 3, 2), result);
		}
	}
}
