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
		public IReadOnlyDictionary<string, IValueProvider<object>> Parameters { get; }

		public SpawnerData(
			string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<string> templateId,
			IValueProvider<Vector3> position,
			IValueProvider<Vector3> rotation,
			IReadOnlyDictionary<string, IValueProvider<object>> parameters) : base(id, listeners) =>
			(TemplateId, Position, Rotation, Parameters) = (templateId, position, rotation, parameters);
	}
}
