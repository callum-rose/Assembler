using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class TransformAnimationData : BehaviourData
	{
		public IValueProvider<Vector3> Start { get; }
		public IValueProvider<Vector3> End { get; }
		public IValueProvider<float> Duration { get; }
		public IValueProvider<string> Easing { get; }

		public TransformAnimationData(
			string id,
						IValueProvider<Vector3> start,
			IValueProvider<Vector3> end,
			IValueProvider<float> duration,
			IValueProvider<string> easing) : base(id)
		{
			Start = start;
			End = end;
			Duration = duration;
			Easing = easing;
		}
	}
}
