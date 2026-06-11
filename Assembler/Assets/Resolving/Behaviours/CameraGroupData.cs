using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class CameraGroupData : BehaviourData
	{
		public IValueProvider<string> Tag { get; }
		public IValueProvider<int> Priority { get; }
		public IValueProvider<float> Damping { get; }
		public IValueProvider<float> FramingSize { get; }
		public IValueProvider<float> Lens { get; }

		/// <summary>Live query (closure over the build-time behaviour registry) returning the transforms of every
		/// entity carrying a given tag, re-queried each frame so the group catches entities spawned after build.</summary>
		public Func<string, IReadOnlyList<Transform>> ResolveByEntityTag { get; }

		public CameraGroupData(string id,
			IValueProvider<string> tag,
			IValueProvider<int> priority,
			IValueProvider<float> damping,
			IValueProvider<float> framingSize,
			IValueProvider<float> lens,
			Func<string, IReadOnlyList<Transform>> resolveByEntityTag) : base(id) =>
			(Tag, Priority, Damping, FramingSize, Lens, ResolveByEntityTag) =
			(tag, priority, damping, framingSize, lens, resolveByEntityTag);
	}
}
