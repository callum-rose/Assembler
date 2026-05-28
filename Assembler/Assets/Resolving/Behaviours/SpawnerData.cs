using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class SpawnerData : BehaviourData
	{
		public IValueProvider<string> TemplateId { get; }
		public IValueProvider<Vector3> Position { get; }
		public IValueProvider<Vector3> Rotation { get; }
		public IReadOnlyDictionary<string, IValueProvider> Parameters { get; }

		public SpawnerData(
			string id,
						IValueProvider<string> templateId,
			IValueProvider<Vector3> position,
			IValueProvider<Vector3> rotation,
			IReadOnlyDictionary<string, IValueProvider> parameters) : base(id) =>
			(TemplateId, Position, Rotation, Parameters) = (templateId, position, rotation, parameters);
	}
}
