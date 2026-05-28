using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class CameraData : BehaviourData
	{
		public IValueProvider<string> Perspective { get; }
		public IValueProvider<float> Size { get; }

		public CameraData(string id,
						IValueProvider<string> perspective,
			IValueProvider<float> size) : base(id) => (Perspective, Size) = (perspective, size);
	}
}