using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class SpawnerData : BehaviourData
	{
		public IValueProvider<string> TemplateId { get; }
		public IValueProvider<Vector3> Position { get; }

		public SpawnerData(
			string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<string> templateId,
			IValueProvider<Vector3> position) : base(id, listeners) =>
			(TemplateId, Position) = (templateId, position);
	}
}