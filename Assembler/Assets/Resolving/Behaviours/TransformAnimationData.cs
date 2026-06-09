using System;
using System.Collections.Generic;
using Assembler.Parsing.Info.Behaviours;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class TransformAnimationData : BehaviourData
	{
		public IValueProvider<Vector3> Start { get; }
		public IValueProvider<Vector3> End { get; }
		public IValueProvider<float> Duration { get; }
		public IValueProvider<Easing> Easing { get; }

		public TransformAnimationData(
			string id,
						IValueProvider<Vector3> start,
			IValueProvider<Vector3> end,
			IValueProvider<float> duration,
			IValueProvider<Easing> easing) : base(id)
		{
			Start = start;
			End = end;
			Duration = duration;
			Easing = easing;
		}
	}
}
